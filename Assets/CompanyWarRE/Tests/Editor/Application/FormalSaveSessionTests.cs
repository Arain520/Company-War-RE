using System;
using CompanyWarRE.Domain;
using NUnit.Framework;

namespace CompanyWarRE.Application.Tests
{
    public sealed class FormalSaveSessionTests
    {
        [Test]
        public void FirstRunWithoutCowData_CreatesWritableCurrentSave()
        {
            var store = new MemoryStore();
            var codec = new MemoryCodec();
            var session = CreateSession(store, codec, new MissingLegacySource());

            var result = session.Start(CreateFlow());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State, Is.EqualTo(FormalSaveSessionState.Ready));
            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(result.Value.Campaign.ActiveLevelId, Is.EqualTo("L02"));
        }

        [Test]
        public void ExistingSave_LoadsWithoutRewritingAndCanRestoreFormalFlow()
        {
            var saved = CreateSave("L03", true);
            var store = new MemoryStore { Existing = true, Document = "current" };
            var codec = new MemoryCodec { DecodeResult = SaveOperationResult<SaveGame>.Success(saved) };
            var session = CreateSession(store, codec, new MissingLegacySource());

            var result = session.Start(CreateFlow());

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Campaign.ActiveLevelId, Is.EqualTo("L03"));
            Assert.That(store.WriteCount, Is.Zero);
        }

        [Test]
        public void DamagedCurrentSave_EntersReadOnlyProtectionAndIsNotOverwritten()
        {
            var store = new MemoryStore { Existing = true, Document = "damaged" };
            var codec = new MemoryCodec
            {
                DecodeResult = SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.InvalidJson,
                    "damaged")
            };
            var session = CreateSession(store, codec, new MissingLegacySource());

            var started = session.Start(CreateFlow());
            var attemptedSave = session.SaveCampaign(CreateFlow());

            Assert.That(started.Error, Is.EqualTo(SaveOperationError.InvalidJson));
            Assert.That(attemptedSave.Error, Is.EqualTo(SaveOperationError.InvalidJson));
            Assert.That(session.State, Is.EqualTo(FormalSaveSessionState.ReadOnlyFailure));
            Assert.That(store.WriteCount, Is.Zero);
            Assert.That(store.Document, Is.EqualTo("damaged"));
        }

        [Test]
        public void BattleResultAndAudioSetting_ArePersistedWithoutLosingCampaignStars()
        {
            var store = new MemoryStore { Existing = true, Document = "current" };
            var codec = new MemoryCodec
            {
                DecodeResult = SaveOperationResult<SaveGame>.Success(CreateSave("L02", true))
            };
            var session = CreateSession(store, codec, new MissingLegacySource());
            session.Start(CreateFlow(completedL02: true));

            var battle = CreateBattleSnapshot(resources: 23, authorizationPoints: 4, score: 9);
            var battleResult = session.SaveBattleResult(CreateFlow(completedL02: true), battle);
            var settingsResult = session.SaveAudioSetting("BGM", 0.35d, true);

            Assert.That(battleResult.Succeeded, Is.True);
            Assert.That(settingsResult.Succeeded, Is.True);
            Assert.That(session.Current.Campaign.FindLevel("L02").Stars, Is.EqualTo(3));
            Assert.That(session.Current.Growth.AuthorizationPoints, Is.EqualTo(4));
            Assert.That(session.Current.Growth.DeploymentUnitIds, Is.EqualTo(new[] { "U01", "U17" }));
            Assert.That(session.Current.Economy.Resources, Is.EqualTo(23));
            Assert.That(session.Current.Economy.Score, Is.EqualTo(9));
            Assert.That(session.Current.Settings.FindAudioLayer("BGM").Volume, Is.EqualTo(0.35d));
            Assert.That(session.Current.Settings.FindAudioLayer("BGM").Muted, Is.True);
            Assert.That(store.WriteCount, Is.EqualTo(2));
        }

        [Test]
        public void LoadedGrowth_IsMergedWithLevelDefaultsWithoutRemovingRequiredDeployment()
        {
            var saved = new SaveGame(
                CreateSave("L02", false).Campaign,
                new GrowthSaveProgress(4, new[] { "U17" }),
                EconomySaveProgress.Default,
                GameSettingsSave.Default);
            var store = new MemoryStore { Existing = true, Document = "current" };
            var codec = new MemoryCodec { DecodeResult = SaveOperationResult<SaveGame>.Success(saved) };
            var session = CreateSession(store, codec, new MissingLegacySource());
            session.Start(CreateFlow());
            var u01 = new UnitDefinition("U01", 1, 0d);
            var u17 = new UnitDefinition("U17", 1, 0d);
            var ally01 = new CombatantDefinition("U01", "Staff", 1, 1d, 1d, 1d, 1);
            var ally17 = new CombatantDefinition("U17", "Staff", 1, 1d, 1d, 1d, 1);
            var enemy = new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1);
            var configuration = new BattleSliceConfiguration(
                6,
                6,
                3,
                10,
                1d,
                1d,
                new GridPosition(1, 1),
                0,
                u01,
                ally01,
                enemy,
                new GridPosition(1, 6),
                units: new[] { u01, u17 },
                allyCombatants: new[] { ally01, ally17 },
                initialDeployments: new[] { "U01" },
                initialAuthorizationPoints: 1);

            var applied = session.ApplyGrowth(configuration);

            Assert.That(applied.InitialAuthorizationPoints, Is.EqualTo(4));
            Assert.That(applied.InitialDeployments, Is.EqualTo(new[] { "U01", "U17" }));
            Assert.That(applied.InitialResources, Is.EqualTo(10), "Level resources are not mid-battle resume data.");
        }

        private static FormalSaveSession CreateSession(
            MemoryStore store,
            MemoryCodec codec,
            ILegacySaveSource legacy)
        {
            return new FormalSaveSession(
                new SaveGameUseCases(codec, store, legacy),
                "C:/isolated/save.json",
                "C:/isolated/backups");
        }

        private static FormalGameFlowSnapshot CreateFlow(bool completedL02 = false)
        {
            var progression = new FormalCampaignProgression(new[] { "L02", "L03", "L04" });
            if (completedL02)
            {
                progression.TryStartLevel("L02");
                progression.RecordResult("L02", BattleState.Victory);
            }

            return new FormalGameFlowSnapshot(progression);
        }

        private static SaveGame CreateSave(string activeLevel, bool completedL02)
        {
            return new SaveGame(
                new CampaignSaveProgress(activeLevel, new[]
                {
                    new LevelSaveProgress("L02", true, completedL02, completedL02 ? 3 : 0),
                    new LevelSaveProgress("L03", completedL02, false, 0),
                    new LevelSaveProgress("L04", false, false, 0)
                }),
                GrowthSaveProgress.Default,
                EconomySaveProgress.Default,
                GameSettingsSave.Default);
        }

        private static BattleSliceSnapshot CreateBattleSnapshot(int resources, int authorizationPoints, int score)
        {
            return new BattleSliceSnapshot(
                3,
                3,
                resources,
                authorizationPoints,
                1d,
                0d,
                "U01",
                1,
                Array.Empty<BattleSliceCellSnapshot>(),
                Array.Empty<BattleSliceCombatantSnapshot>(),
                Array.Empty<CombatEvent>(),
                "done",
                1,
                Array.Empty<EnemyWaveSpawn>(),
                BattleState.Victory,
                score,
                score,
                0,
                0,
                AuthorizationState.None,
                0,
                Array.Empty<string>(),
                new[] { "U01", "U17" });
        }

        private sealed class MissingLegacySource : ILegacySaveSource
        {
            public SaveOperationResult<SaveGame> Read()
            {
                return SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.LegacySourceUnavailable,
                    "none");
            }
        }

        private sealed class MemoryCodec : ISaveDocumentCodec
        {
            public SaveOperationResult<SaveGame> DecodeResult { get; set; }
            public SaveGame LastSerialized { get; private set; }

            public string Serialize(SaveGame value)
            {
                LastSerialized = value;
                DecodeResult = SaveOperationResult<SaveGame>.Success(value);
                return "encoded";
            }

            public SaveOperationResult<SaveGame> Deserialize(string document)
            {
                return DecodeResult ?? SaveOperationResult<SaveGame>.Failure(
                    SaveOperationError.InvalidJson,
                    "not encoded");
            }
        }

        private sealed class MemoryStore : ISaveDocumentStore
        {
            public bool Existing { get; set; }
            public string Document { get; set; }
            public int WriteCount { get; private set; }

            public bool Exists(string path) => Existing;
            public string ReadText(string path) => Document;

            public SaveWriteReceipt WriteAtomic(string path, string document, string backupDirectory)
            {
                WriteCount++;
                Existing = true;
                Document = document;
                return new SaveWriteReceipt(path, WriteCount > 1 ? "C:/isolated/backups/previous.bak" : string.Empty);
            }

            public SaveWriteReceipt RestoreAtomic(string path, string backupPath, string backupDirectory)
            {
                throw new NotSupportedException();
            }
        }
    }
}
