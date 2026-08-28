using System;
using CompanyWarRE.Domain;
using QFramework;

namespace CompanyWarRE.Application
{
    public enum SaveOperationError
    {
        None,
        NotFound,
        InvalidPath,
        LegacySourceUnavailable,
        InvalidJson,
        UnsupportedVersion,
        ValidationFailed,
        ReadFailed,
        BackupFailed,
        WriteFailed,
        RestoreFailed
    }

    public sealed class SaveOperationResult<T>
    {
        private SaveOperationResult(
            T value,
            SaveOperationError error,
            string message,
            SaveValidationReport report,
            string backupPath)
        {
            Value = value;
            Error = error;
            Message = message ?? string.Empty;
            Report = report ?? new SaveValidationReport();
            BackupPath = backupPath ?? string.Empty;
        }

        public bool Succeeded => Error == SaveOperationError.None;
        public T Value { get; }
        public SaveOperationError Error { get; }
        public string Message { get; }
        public SaveValidationReport Report { get; }
        public string BackupPath { get; }

        public static SaveOperationResult<T> Success(
            T value,
            SaveValidationReport report = null,
            string backupPath = null)
        {
            return new SaveOperationResult<T>(value, SaveOperationError.None, string.Empty, report, backupPath);
        }

        public static SaveOperationResult<T> Failure(
            SaveOperationError error,
            string message,
            SaveValidationReport report = null)
        {
            if (error == SaveOperationError.None)
            {
                throw new ArgumentException("Failure requires a non-success error.", nameof(error));
            }

            return new SaveOperationResult<T>(default(T), error, message, report, null);
        }
    }

    public sealed class SaveWriteReceipt
    {
        public SaveWriteReceipt(string targetPath, string backupPath)
        {
            TargetPath = targetPath ?? string.Empty;
            BackupPath = backupPath ?? string.Empty;
        }

        public string TargetPath { get; }
        public string BackupPath { get; }
    }

    public sealed class SaveStorageException : Exception
    {
        public SaveStorageException(SaveOperationError error, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Error = error;
        }

        public SaveOperationError Error { get; }
    }

    public interface ISaveDocumentCodec
    {
        string Serialize(SaveGame value);
        SaveOperationResult<SaveGame> Deserialize(string document);
    }

    public interface ISaveDocumentStore
    {
        bool Exists(string path);
        string ReadText(string path);
        SaveWriteReceipt WriteAtomic(string path, string document, string backupDirectory);
        SaveWriteReceipt RestoreAtomic(string path, string backupPath, string backupDirectory);
    }

    public interface ILegacySaveSource
    {
        SaveOperationResult<SaveGame> Read();
    }

    public sealed class SaveGameUseCases
    {
        private readonly ISaveDocumentCodec _codec;
        private readonly ISaveDocumentStore _store;
        private readonly ILegacySaveSource _legacySource;

        public SaveGameUseCases(
            ISaveDocumentCodec codec,
            ISaveDocumentStore store,
            ILegacySaveSource legacySource = null)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _legacySource = legacySource;
        }

        public SaveOperationResult<SaveGame> Load(string path)
        {
            try
            {
                if (!_store.Exists(path))
                {
                    return SaveOperationResult<SaveGame>.Failure(
                        SaveOperationError.NotFound,
                        "Save document does not exist: " + path);
                }

                return _codec.Deserialize(_store.ReadText(path));
            }
            catch (SaveStorageException exception)
            {
                return SaveOperationResult<SaveGame>.Failure(exception.Error, exception.Message);
            }
            catch (Exception exception)
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.ReadFailed, exception.Message);
            }
        }

        public SaveOperationResult<SaveGame> LoadOrImport(string path, string backupDirectory)
        {
            var loaded = Load(path);
            if (loaded.Succeeded || loaded.Error != SaveOperationError.NotFound)
            {
                // A damaged or unknown current document is evidence, not permission to overwrite it.
                return loaded;
            }

            return ImportLegacy(path, backupDirectory);
        }

        public SaveOperationResult<SaveGame> ImportLegacy(string path, string backupDirectory)
        {
            if (_legacySource == null)
            {
                return SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.LegacySourceUnavailable,
                    "No Cow legacy preference source was provided.");
            }

            var imported = _legacySource.Read();
            if (!imported.Succeeded)
            {
                return imported;
            }

            return Save(path, backupDirectory, imported.Value, imported.Report);
        }

        public SaveOperationResult<SaveGame> Save(
            string path,
            string backupDirectory,
            SaveGame value,
            SaveValidationReport existingReport = null)
        {
            if (value == null)
            {
                return SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.ValidationFailed,
                    "Save value is null.");
            }

            var report = new SaveValidationReport();
            report.AddRange(existingReport?.Issues);
            report.AddRange(value.Validate().Issues);
            if (!report.IsValid)
            {
                return SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.ValidationFailed,
                    "Save validation failed.",
                    report);
            }

            try
            {
                var receipt = _store.WriteAtomic(path, _codec.Serialize(value), backupDirectory);
                return SaveOperationResult<SaveGame>.Success(value, report, receipt.BackupPath);
            }
            catch (SaveStorageException exception)
            {
                return SaveOperationResult<SaveGame>.Failure(exception.Error, exception.Message, report);
            }
            catch (Exception exception)
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.WriteFailed, exception.Message, report);
            }
        }

        public SaveOperationResult<SaveGame> Restore(
            string path,
            string backupPath,
            string backupDirectory)
        {
            try
            {
                // Validate the backup before allowing it to replace a valid target.
                var decoded = _codec.Deserialize(_store.ReadText(backupPath));
                if (!decoded.Succeeded)
                {
                    return decoded;
                }

                var receipt = _store.RestoreAtomic(path, backupPath, backupDirectory);
                return SaveOperationResult<SaveGame>.Success(decoded.Value, decoded.Report, receipt.BackupPath);
            }
            catch (SaveStorageException exception)
            {
                return SaveOperationResult<SaveGame>.Failure(exception.Error, exception.Message);
            }
            catch (Exception exception)
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.RestoreFailed, exception.Message);
            }
        }
    }

    public sealed class ImportLegacySaveCommand : AbstractCommand<SaveOperationResult<SaveGame>>
    {
        private readonly SaveGameUseCases _useCases;
        private readonly string _path;
        private readonly string _backupDirectory;

        public ImportLegacySaveCommand(SaveGameUseCases useCases, string path, string backupDirectory)
        {
            _useCases = useCases;
            _path = path;
            _backupDirectory = backupDirectory;
        }

        protected override SaveOperationResult<SaveGame> OnExecute()
        {
            return _useCases.ImportLegacy(_path, _backupDirectory);
        }
    }

    public sealed class SaveCurrentGameCommand : AbstractCommand<SaveOperationResult<SaveGame>>
    {
        private readonly SaveGameUseCases _useCases;
        private readonly SaveGame _value;
        private readonly string _path;
        private readonly string _backupDirectory;

        public SaveCurrentGameCommand(
            SaveGameUseCases useCases,
            SaveGame value,
            string path,
            string backupDirectory)
        {
            _useCases = useCases;
            _value = value;
            _path = path;
            _backupDirectory = backupDirectory;
        }

        protected override SaveOperationResult<SaveGame> OnExecute()
        {
            return _useCases.Save(_path, _backupDirectory, _value);
        }
    }
}
