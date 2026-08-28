using CompanyWarRE.Domain;
using NUnit.Framework;
using QFramework;

namespace CompanyWarRE.Application.Tests
{
    public sealed class SaveGameApplicationTests
    {
        [Test]
        public void LegacyImport_RunsThroughQFrameworkCommandBoundary()
        {
            var value = ValidSave();
            var store = new FakeStore();
            var useCases = new SaveGameUseCases(
                new FakeCodec(value),
                store,
                new FakeLegacySource(SaveOperationResult<SaveGame>.Success(value)));

            var result = BattleSliceArchitecture.Interface.SendCommand(
                new ImportLegacySaveCommand(useCases, "C:/isolated/save.json", "C:/isolated/backups"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(store.LastPath, Is.EqualTo("C:/isolated/save.json"));
        }

        [Test]
        public void InvalidLegacyData_DoesNotInvokeStorageWrite()
        {
            var store = new FakeStore();
            var useCases = new SaveGameUseCases(
                new FakeCodec(ValidSave()),
                store,
                new FakeLegacySource(SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.ValidationFailed,
                    "bad legacy snapshot")));

            var result = BattleSliceArchitecture.Interface.SendCommand(
                new ImportLegacySaveCommand(useCases, "C:/isolated/save.json", "C:/isolated/backups"));

            Assert.That(result.Error, Is.EqualTo(SaveOperationError.ValidationFailed));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void CorruptCurrentDocument_IsPreservedAndDoesNotFallBackToLegacy()
        {
            var store = new FakeStore { Existing = true, Document = "broken" };
            var legacy = new FakeLegacySource(SaveOperationResult<SaveGame>.Success(ValidSave()));
            var codec = new FakeCodec(ValidSave()) { DecodeError = SaveOperationError.InvalidJson };
            var useCases = new SaveGameUseCases(codec, store, legacy);

            var result = useCases.LoadOrImport("C:/isolated/save.json", "C:/isolated/backups");

            Assert.That(result.Error, Is.EqualTo(SaveOperationError.InvalidJson));
            Assert.That(store.WriteCount, Is.Zero);
            Assert.That(legacy.ReadCount, Is.Zero);
            Assert.That(store.Document, Is.EqualTo("broken"));
        }

        [Test]
        public void RestoredSave_HydratesFormalFlowThroughQFrameworkModel()
        {
            var save = new SaveGame(
                new CampaignSaveProgress("L03", new[]
                {
                    new LevelSaveProgress("L02", true, true, 2),
                    new LevelSaveProgress("L03", true, false, 0),
                    new LevelSaveProgress("L04", false, false, 0)
                }),
                GrowthSaveProgress.Default,
                EconomySaveProgress.Default,
                GameSettingsSave.Default);

            BattleSliceArchitecture.Interface.SendCommand(
                new RestoreFormalGameFlowCommand(save, "L02", "L03", "L04"));
            var snapshot = BattleSliceArchitecture.Interface.SendQuery(new GetFormalGameFlowSnapshotQuery());

            Assert.That(snapshot.ActiveLevelId, Is.EqualTo("L03"));
            Assert.That(snapshot.IsCompleted("L02"), Is.True);
            Assert.That(snapshot.IsUnlocked("L03"), Is.True);
            Assert.That(snapshot.IsUnlocked("L04"), Is.False);
        }

        private static SaveGame ValidSave()
        {
            return new SaveGame(
                new CampaignSaveProgress("L02", new[]
                {
                    new LevelSaveProgress("L02", true, false, 0)
                }),
                GrowthSaveProgress.Default,
                EconomySaveProgress.Default,
                GameSettingsSave.Default);
        }

        private sealed class FakeCodec : ISaveDocumentCodec
        {
            private readonly SaveGame _value;

            public FakeCodec(SaveGame value)
            {
                _value = value;
            }

            public SaveOperationError DecodeError { get; set; }
            public string Serialize(SaveGame value) => "encoded";

            public SaveOperationResult<SaveGame> Deserialize(string document)
            {
                return DecodeError == SaveOperationError.None
                    ? SaveOperationResult<SaveGame>.Success(_value)
                    : SaveOperationResult<SaveGame>.Failure(DecodeError, "decode failed");
            }
        }

        private sealed class FakeLegacySource : ILegacySaveSource
        {
            private readonly SaveOperationResult<SaveGame> _result;

            public FakeLegacySource(SaveOperationResult<SaveGame> result)
            {
                _result = result;
            }

            public int ReadCount { get; private set; }

            public SaveOperationResult<SaveGame> Read()
            {
                ReadCount++;
                return _result;
            }
        }

        private sealed class FakeStore : ISaveDocumentStore
        {
            public bool Existing { get; set; }
            public string Document { get; set; }
            public int WriteCount { get; private set; }
            public string LastPath { get; private set; }

            public bool Exists(string path) => Existing;
            public string ReadText(string path) => Document;

            public SaveWriteReceipt WriteAtomic(string path, string document, string backupDirectory)
            {
                WriteCount++;
                LastPath = path;
                Document = document;
                Existing = true;
                return new SaveWriteReceipt(path, string.Empty);
            }

            public SaveWriteReceipt RestoreAtomic(string path, string backupPath, string backupDirectory)
            {
                return new SaveWriteReceipt(path, string.Empty);
            }
        }
    }
}
