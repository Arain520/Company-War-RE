using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;

namespace CompanyWarRE.Infrastructure.Saves
{
    [DataContract]
    internal sealed class SaveDocumentDto
    {
        [DataMember(Name = "Version")]
        public int Version;
        [DataMember(Name = "Campaign", EmitDefaultValue = false)]
        public CampaignDto Campaign;
        [DataMember(Name = "Growth", EmitDefaultValue = false)]
        public GrowthDto Growth;
        [DataMember(Name = "Economy", EmitDefaultValue = false)]
        public EconomyDto Economy;
        [DataMember(Name = "Settings", EmitDefaultValue = false)]
        public SettingsDto Settings;
    }

    [DataContract]
    internal sealed class CampaignDto
    {
        [DataMember(Name = "ActiveLevelId", EmitDefaultValue = false)]
        public string ActiveLevelId;
        [DataMember(Name = "Levels", EmitDefaultValue = false)]
        public List<LevelDto> Levels;
    }

    [DataContract]
    internal sealed class LevelDto
    {
        [DataMember(Name = "LevelId", EmitDefaultValue = false)]
        public string LevelId;
        [DataMember(Name = "Unlocked")]
        public bool Unlocked;
        [DataMember(Name = "Completed")]
        public bool Completed;
        [DataMember(Name = "Stars")]
        public int Stars;
    }

    [DataContract]
    internal sealed class GrowthDto
    {
        [DataMember(Name = "AuthorizationPoints")]
        public int AuthorizationPoints;
        [DataMember(Name = "DeploymentUnitIds", EmitDefaultValue = false)]
        public List<string> DeploymentUnitIds;
    }

    [DataContract]
    internal sealed class EconomyDto
    {
        [DataMember(Name = "Resources")]
        public int Resources;
        [DataMember(Name = "Score")]
        public int Score;
    }

    [DataContract]
    internal sealed class SettingsDto
    {
        [DataMember(Name = "AudioLayers", EmitDefaultValue = false)]
        public List<AudioLayerDto> AudioLayers;
    }

    [DataContract]
    internal sealed class AudioLayerDto
    {
        [DataMember(Name = "Layer", EmitDefaultValue = false)]
        public string Layer;
        [DataMember(Name = "Volume")]
        public double Volume;
        [DataMember(Name = "Muted")]
        public bool Muted;
    }

    public sealed class DataContractSaveDocumentCodec : ISaveDocumentCodec
    {
        private readonly SaveGameUpgrader _upgrader = new SaveGameUpgrader();

        public string Serialize(SaveGame value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var dto = new SaveDocumentDto
            {
                Version = value.Version,
                Campaign = new CampaignDto
                {
                    ActiveLevelId = value.Campaign.ActiveLevelId,
                    Levels = value.Campaign.Levels.Select(level => new LevelDto
                    {
                        LevelId = level.LevelId,
                        Unlocked = level.Unlocked,
                        Completed = level.Completed,
                        Stars = level.Stars
                    }).ToList()
                },
                Growth = new GrowthDto
                {
                    AuthorizationPoints = value.Growth.AuthorizationPoints,
                    DeploymentUnitIds = value.Growth.DeploymentUnitIds.ToList()
                },
                Economy = new EconomyDto
                {
                    Resources = value.Economy.Resources,
                    Score = value.Economy.Score
                },
                Settings = new SettingsDto
                {
                    AudioLayers = value.Settings.AudioLayers.Select(layer => new AudioLayerDto
                    {
                        Layer = layer.Layer,
                        Volume = layer.Volume,
                        Muted = layer.Muted
                    }).ToList()
                }
            };

            var serializer = new DataContractJsonSerializer(typeof(SaveDocumentDto));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, dto);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public SaveOperationResult<SaveGame> Deserialize(string document)
        {
            if (string.IsNullOrWhiteSpace(document))
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.InvalidJson, "Save document is empty.");
            }

            SaveDocumentDto dto;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(SaveDocumentDto));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(document)))
                {
                    dto = serializer.ReadObject(stream) as SaveDocumentDto;
                }
            }
            catch (Exception exception)
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.InvalidJson, exception.Message);
            }

            if (dto == null)
            {
                return SaveOperationResult<SaveGame>.Failure(SaveOperationError.InvalidJson, "JSON produced no save object.");
            }

            var upgraded = _upgrader.Upgrade(new SaveGameDraft(
                dto.Version,
                Map(dto.Campaign),
                Map(dto.Growth),
                Map(dto.Economy),
                Map(dto.Settings)));
            if (!upgraded.Succeeded)
            {
                var unsupported = upgraded.Report.Issues.Any(issue => issue.Code == SaveIssueCode.UnsupportedVersion);
                return SaveOperationResult<SaveGame>.Failure(
                    unsupported ? SaveOperationError.UnsupportedVersion : SaveOperationError.ValidationFailed,
                    unsupported ? "Unsupported save version." : "Save validation failed.",
                    upgraded.Report);
            }

            return SaveOperationResult<SaveGame>.Success(upgraded.Value, upgraded.Report);
        }

        private static CampaignSaveProgress Map(CampaignDto dto)
        {
            return dto == null
                ? null
                : new CampaignSaveProgress(
                    dto.ActiveLevelId,
                    (dto.Levels ?? new List<LevelDto>()).Select(level =>
                        new LevelSaveProgress(level.LevelId, level.Unlocked, level.Completed, level.Stars)));
        }

        private static GrowthSaveProgress Map(GrowthDto dto)
        {
            return dto == null
                ? null
                : new GrowthSaveProgress(dto.AuthorizationPoints, dto.DeploymentUnitIds ?? new List<string>());
        }

        private static EconomySaveProgress Map(EconomyDto dto)
        {
            return dto == null ? null : new EconomySaveProgress(dto.Resources, dto.Score);
        }

        private static GameSettingsSave Map(SettingsDto dto)
        {
            return dto == null
                ? null
                : new GameSettingsSave((dto.AudioLayers ?? new List<AudioLayerDto>()).Select(layer =>
                    new AudioLayerSaveSettings(layer.Layer, layer.Volume, layer.Muted)));
        }
    }

    [DataContract]
    public sealed class CowLegacyPreferenceSnapshotDto
    {
        [DataMember(Name = "Strings", EmitDefaultValue = false)]
        public Dictionary<string, string> Strings = new Dictionary<string, string>();
        [DataMember(Name = "Ints", EmitDefaultValue = false)]
        public Dictionary<string, int> Ints = new Dictionary<string, int>();
        [DataMember(Name = "Floats", EmitDefaultValue = false)]
        public Dictionary<string, float> Floats = new Dictionary<string, float>();
    }

    public interface ILegacyPreferenceReader
    {
        bool HasKey(string key);
        string GetString(string key, string defaultValue);
        int GetInt(string key, int defaultValue);
        float GetFloat(string key, float defaultValue);
    }

    public sealed class CowLegacySnapshotPreferenceReader : ILegacyPreferenceReader
    {
        private readonly CowLegacyPreferenceSnapshotDto _snapshot;

        public CowLegacySnapshotPreferenceReader(CowLegacyPreferenceSnapshotDto snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _snapshot.Strings = _snapshot.Strings ?? new Dictionary<string, string>();
            _snapshot.Ints = _snapshot.Ints ?? new Dictionary<string, int>();
            _snapshot.Floats = _snapshot.Floats ?? new Dictionary<string, float>();
        }

        public bool HasKey(string key)
        {
            return _snapshot.Strings.ContainsKey(key) ||
                   _snapshot.Ints.ContainsKey(key) ||
                   _snapshot.Floats.ContainsKey(key);
        }

        public string GetString(string key, string defaultValue)
        {
            return _snapshot.Strings.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            return _snapshot.Ints.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public float GetFloat(string key, float defaultValue)
        {
            return _snapshot.Floats.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    public static class CowLegacyPreferenceKeys
    {
        public const string LastLevel = "CompanyWar.LastLevel";
        public const string LevelStarsPrefix = "CompanyWar.LevelStars.";

        public static string Volume(string layer) => "Audio_" + layer + "_Volume";
        public static string Mute(string layer) => "Audio_" + layer + "_Mute";
    }

    public sealed class CowLegacyPreferenceSaveSource : ILegacySaveSource
    {
        private readonly ILegacyPreferenceReader _reader;
        private readonly string[] _knownLevelIds;

        public CowLegacyPreferenceSaveSource(ILegacyPreferenceReader reader, IEnumerable<string> knownLevelIds)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _knownLevelIds = (knownLevelIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (_knownLevelIds.Length == 0)
            {
                throw new ArgumentException("At least one target level id is required.", nameof(knownLevelIds));
            }
        }

        public SaveOperationResult<SaveGame> Read()
        {
            var knownKeys = new[] { CowLegacyPreferenceKeys.LastLevel }
                .Concat(_knownLevelIds.Select(id => CowLegacyPreferenceKeys.LevelStarsPrefix + id))
                .Concat(GameSettingsSave.CowAudioLayers.Select(CowLegacyPreferenceKeys.Volume))
                .Concat(GameSettingsSave.CowAudioLayers.Select(CowLegacyPreferenceKeys.Mute));
            if (!knownKeys.Any(_reader.HasKey))
            {
                return SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.LegacySourceUnavailable,
                    "No real Cow legacy preference keys were found.");
            }

            var report = new SaveValidationReport();
            var active = _reader.GetString(CowLegacyPreferenceKeys.LastLevel, _knownLevelIds[0]);
            var activeIndex = Array.FindIndex(_knownLevelIds, id =>
                string.Equals(id, active, StringComparison.OrdinalIgnoreCase));
            if (activeIndex < 0)
            {
                report.Add(new SaveIssue(
                    SaveIssueCode.InvalidActiveLevel,
                    SaveIssueSeverity.Warning,
                    CowLegacyPreferenceKeys.LastLevel,
                    "Unknown Cow level was mapped to the first target level: " + active));
                activeIndex = 0;
                active = _knownLevelIds[0];
            }

            var levels = new List<LevelSaveProgress>();
            for (var index = 0; index < _knownLevelIds.Length; index++)
            {
                var id = _knownLevelIds[index];
                var key = CowLegacyPreferenceKeys.LevelStarsPrefix + id;
                var rawStars = _reader.GetInt(key, 0);
                var stars = Math.Max(0, Math.Min(3, rawStars));
                if (stars != rawStars)
                {
                    report.Add(new SaveIssue(SaveIssueCode.InvalidStars, SaveIssueSeverity.Warning, key, rawStars.ToString()));
                }

                var completed = stars > 0;
                levels.Add(new LevelSaveProgress(id, index <= activeIndex || completed, completed, stars));
            }

            var settings = new List<AudioLayerSaveSettings>();
            foreach (var layer in GameSettingsSave.CowAudioLayers)
            {
                var volumeKey = CowLegacyPreferenceKeys.Volume(layer);
                var rawVolume = _reader.GetFloat(volumeKey, 1f);
                var volume = Math.Max(0d, Math.Min(1d, rawVolume));
                if (Math.Abs(volume - rawVolume) > 0.0001d)
                {
                    report.Add(new SaveIssue(SaveIssueCode.InvalidVolume, SaveIssueSeverity.Warning, volumeKey, rawVolume.ToString()));
                }

                settings.Add(new AudioLayerSaveSettings(
                    layer,
                    volume,
                    _reader.GetInt(CowLegacyPreferenceKeys.Mute(layer), 0) == 1));
            }

            var upgraded = new SaveGameUpgrader().Upgrade(new SaveGameDraft(
                SaveSchema.CowCompatibleVersion,
                new CampaignSaveProgress(active, levels),
                null,
                null,
                new GameSettingsSave(settings)));
            report.AddRange(upgraded.Report.Issues);
            return upgraded.Succeeded
                ? SaveOperationResult<SaveGame>.Success(upgraded.Value, report)
                : SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.ValidationFailed,
                    "Cow preference mapping failed validation.",
                    report);
        }
    }

    public interface IAtomicFileOperations
    {
        bool FileExists(string path);
        void CreateDirectory(string path);
        string ReadAllText(string path);
        void WriteNewTextAndFlush(string path, string content);
        void CopyFile(string source, string destination, bool overwrite);
        void MoveFile(string source, string destination);
        void ReplaceFile(string source, string destination);
        void DeleteFile(string path);
    }

    public sealed class SystemAtomicFileOperations : IAtomicFileOperations
    {
        public bool FileExists(string path) => File.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
        public void CopyFile(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
        public void MoveFile(string source, string destination) => File.Move(source, destination);
        public void ReplaceFile(string source, string destination) => File.Replace(source, destination, null);
        public void DeleteFile(string path) => File.Delete(path);

        public void WriteNewTextAndFlush(string path, string content)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content ?? string.Empty);
                writer.Flush();
                stream.Flush(true);
            }
        }
    }

    public sealed class AtomicLocalSaveDocumentStore : ISaveDocumentStore
    {
        private readonly IAtomicFileOperations _files;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Func<Guid> _newGuid;

        public AtomicLocalSaveDocumentStore(
            IAtomicFileOperations files = null,
            Func<DateTimeOffset> utcNow = null,
            Func<Guid> newGuid = null)
        {
            _files = files ?? new SystemAtomicFileOperations();
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _newGuid = newGuid ?? Guid.NewGuid;
        }

        public bool Exists(string path)
        {
            return _files.FileExists(RequireAbsolute(path));
        }

        public string ReadText(string path)
        {
            path = RequireAbsolute(path);
            if (!_files.FileExists(path))
            {
                throw new SaveStorageException(SaveOperationError.NotFound, "Save file not found: " + path);
            }

            try
            {
                return _files.ReadAllText(path);
            }
            catch (Exception exception)
            {
                throw new SaveStorageException(SaveOperationError.ReadFailed, "Failed to read save: " + path, exception);
            }
        }

        public SaveWriteReceipt WriteAtomic(string path, string document, string backupDirectory)
        {
            path = RequireAbsolute(path);
            backupDirectory = RequireAbsolute(backupDirectory);
            var targetDirectory = Path.GetDirectoryName(path);
            var token = _newGuid().ToString("N");
            var tempPath = Path.Combine(targetDirectory, "." + Path.GetFileName(path) + "." + token + ".tmp");
            var backupPath = string.Empty;

            _files.CreateDirectory(targetDirectory);
            if (_files.FileExists(path))
            {
                try
                {
                    _files.CreateDirectory(backupDirectory);
                    backupPath = BuildBackupPath(path, backupDirectory, token);
                    _files.CopyFile(path, backupPath, false);
                }
                catch (Exception exception)
                {
                    throw new SaveStorageException(SaveOperationError.BackupFailed, "Failed to create save backup.", exception);
                }
            }

            try
            {
                _files.WriteNewTextAndFlush(tempPath, document);
                if (_files.FileExists(path))
                {
                    _files.ReplaceFile(tempPath, path);
                }
                else
                {
                    _files.MoveFile(tempPath, path);
                }

                return new SaveWriteReceipt(path, backupPath);
            }
            catch (Exception exception)
            {
                SafeDelete(tempPath);
                if (!string.IsNullOrEmpty(backupPath))
                {
                    try
                    {
                        _files.CopyFile(backupPath, path, true);
                    }
                    catch (Exception restoreException)
                    {
                        throw new SaveStorageException(
                            SaveOperationError.WriteFailed,
                            "Atomic write failed and automatic rollback also failed.",
                            new AggregateException(exception, restoreException));
                    }
                }

                throw new SaveStorageException(SaveOperationError.WriteFailed, "Atomic save write failed.", exception);
            }
        }

        public SaveWriteReceipt RestoreAtomic(string path, string backupPath, string backupDirectory)
        {
            path = RequireAbsolute(path);
            backupPath = RequireAbsolute(backupPath);
            if (!_files.FileExists(backupPath))
            {
                throw new SaveStorageException(SaveOperationError.RestoreFailed, "Backup file not found: " + backupPath);
            }

            try
            {
                return WriteAtomic(path, _files.ReadAllText(backupPath), backupDirectory);
            }
            catch (SaveStorageException exception)
            {
                throw new SaveStorageException(SaveOperationError.RestoreFailed, exception.Message, exception);
            }
        }

        private string BuildBackupPath(string targetPath, string backupDirectory, string token)
        {
            var stamp = _utcNow().UtcDateTime.ToString("yyyyMMddTHHmmssfffZ");
            return Path.Combine(backupDirectory, Path.GetFileName(targetPath) + "." + stamp + "." + token + ".bak");
        }

        private static string RequireAbsolute(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new SaveStorageException(SaveOperationError.InvalidPath, "An absolute caller-provided path is required.");
            }

            return Path.GetFullPath(path);
        }

        private void SafeDelete(string path)
        {
            try
            {
                if (_files.FileExists(path))
                {
                    _files.DeleteFile(path);
                }
            }
            catch
            {
                // Preserve the original write error.
            }
        }
    }
}
