using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Saves;
using NUnit.Framework;

namespace CompanyWarRE.Infrastructure.Tests
{
    public sealed class SaveCompatibilityTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "CompanyWarRE-SaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void CowPreferenceDto_MapsOnlyRealLegacyFieldsAndDefaultsTargetAdditions()
        {
            var dto = new CowLegacyPreferenceSnapshotDto();
            dto.Strings[CowLegacyPreferenceKeys.LastLevel] = "L04";
            dto.Ints[CowLegacyPreferenceKeys.LevelStarsPrefix + "L02"] = 3;
            dto.Ints[CowLegacyPreferenceKeys.Mute("BGM")] = 1;
            dto.Floats[CowLegacyPreferenceKeys.Volume("Master")] = 0.4f;
            var source = new CowLegacyPreferenceSaveSource(
                new CowLegacySnapshotPreferenceReader(dto),
                new[] { "L02", "L03", "L04", "L05" });

            var result = source.Read();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Campaign.ActiveLevelId, Is.EqualTo("L04"));
            Assert.That(result.Value.Campaign.FindLevel("L02").Completed, Is.True);
            Assert.That(result.Value.Campaign.FindLevel("L02").Stars, Is.EqualTo(3));
            Assert.That(result.Value.Campaign.FindLevel("L04").Unlocked, Is.True);
            Assert.That(result.Value.Campaign.FindLevel("L05").Unlocked, Is.False);
            Assert.That(result.Value.Settings.FindAudioLayer("Master").Volume, Is.EqualTo(0.4d).Within(0.0001d));
            Assert.That(result.Value.Settings.FindAudioLayer("BGM").Muted, Is.True);
            Assert.That(result.Value.Growth.AuthorizationPoints, Is.Zero, "Cow never persisted authorization growth.");
            Assert.That(result.Value.Growth.DeploymentUnitIds, Is.Empty, "Cow never persisted deployment lists.");
            Assert.That(result.Value.Economy.Resources, Is.Zero, "Cow never persisted battle resources.");
            Assert.That(result.Value.Economy.Score, Is.Zero, "Cow never persisted battle score.");
        }

        [Test]
        public void CurrentDocument_RoundTripsAndIgnoresUnknownFields()
        {
            var codec = new DataContractSaveDocumentCodec();
            var source = CurrentSave();
            var json = codec.Serialize(source);
            json = json.Substring(0, json.Length - 1) + ",\"FutureField\":{\"Nested\":true}}";

            var result = codec.Deserialize(json);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Version, Is.EqualTo(SaveSchema.CurrentVersion));
            Assert.That(result.Value.Campaign.ActiveLevelId, Is.EqualTo("L03"));
            Assert.That(result.Value.Growth.DeploymentUnitIds, Is.EqualTo(new[] { "U01", "U17" }));
            Assert.That(result.Value.Economy.Resources, Is.EqualTo(12));
            Assert.That(result.Value.Economy.Score, Is.EqualTo(34));
        }

        [Test]
        public void VersionOneJson_WithMissingTargetSections_UsesUpgradeDefaults()
        {
            const string json =
                "{\"Version\":1,\"Campaign\":{\"ActiveLevelId\":\"L02\",\"Levels\":[" +
                "{\"LevelId\":\"L02\",\"Unlocked\":true,\"Completed\":false,\"Stars\":0}]}}";

            var result = new DataContractSaveDocumentCodec().Deserialize(json);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Growth.AuthorizationPoints, Is.Zero);
            Assert.That(result.Value.Economy.Resources, Is.Zero);
            Assert.That(result.Value.Settings.AudioLayers.Count, Is.EqualTo(5));
            Assert.That(result.Report.Issues.Any(issue => issue.Code == SaveIssueCode.MissingSectionDefaulted), Is.True);
            Assert.That(result.Report.Issues.Count(issue => issue.Code == SaveIssueCode.VersionUpgraded), Is.EqualTo(2));
        }

        [TestCase("{not-json", SaveOperationError.InvalidJson)]
        [TestCase("{\"Version\":99}", SaveOperationError.UnsupportedVersion)]
        public void DamagedOrUnknownDocuments_ReturnTypedErrors(string json, SaveOperationError expected)
        {
            var result = new DataContractSaveDocumentCodec().Deserialize(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
        }

        [Test]
        public void AtomicStore_CreatesUniqueBackupAndSupportsExplicitRestore()
        {
            var target = Path.Combine(_root, "slot", "save.json");
            var backups = Path.Combine(_root, "backups");
            var store = new AtomicLocalSaveDocumentStore();

            store.WriteAtomic(target, "first", backups);
            var second = store.WriteAtomic(target, "second", backups);

            Assert.That(File.ReadAllText(target), Is.EqualTo("second"));
            Assert.That(second.BackupPath, Is.Not.Empty);
            Assert.That(File.Exists(second.BackupPath), Is.True);
            Assert.That(File.ReadAllText(second.BackupPath), Is.EqualTo("first"));

            var restored = store.RestoreAtomic(target, second.BackupPath, backups);
            Assert.That(File.ReadAllText(target), Is.EqualTo("first"));
            Assert.That(restored.BackupPath, Is.Not.Empty, "Restore must back up the replaced current save.");
            Assert.That(restored.BackupPath, Is.Not.EqualTo(second.BackupPath));
            Assert.That(File.ReadAllText(restored.BackupPath), Is.EqualTo("second"));
        }

        [Test]
        public void ReplaceFailure_AutomaticallyRollsBackAndDoesNotOverwriteValidSave()
        {
            var target = Path.Combine(_root, "save.json");
            var backups = Path.Combine(_root, "backups");
            File.WriteAllText(target, "valid-current");
            var store = new AtomicLocalSaveDocumentStore(new FailOnceReplaceOperations());

            var exception = Assert.Throws<SaveStorageException>(() =>
                store.WriteAtomic(target, "new-value", backups));

            Assert.That(exception.Error, Is.EqualTo(SaveOperationError.WriteFailed));
            Assert.That(File.ReadAllText(target), Is.EqualTo("valid-current"));
            Assert.That(Directory.GetFiles(backups, "*.bak").Length, Is.EqualTo(1));
        }

        [Test]
        public void Store_RequiresCallerProvidedAbsolutePathsAndStaysInsideTestRoot()
        {
            var store = new AtomicLocalSaveDocumentStore();

            var exception = Assert.Throws<SaveStorageException>(() =>
                store.WriteAtomic("relative/save.json", "value", Path.Combine(_root, "backups")));
            Assert.That(exception.Error, Is.EqualTo(SaveOperationError.InvalidPath));
            Assert.That(Directory.GetFileSystemEntries(_root), Is.Empty);
        }

        private static SaveGame CurrentSave()
        {
            return new SaveGame(
                new CampaignSaveProgress("L03", new[]
                {
                    new LevelSaveProgress("L02", true, true, 2),
                    new LevelSaveProgress("L03", true, false, 0)
                }),
                new GrowthSaveProgress(3, new[] { "U01", "U17" }),
                new EconomySaveProgress(12, 34),
                GameSettingsSave.Default);
        }

        private sealed class FailOnceReplaceOperations : IAtomicFileOperations
        {
            private readonly SystemAtomicFileOperations _inner = new SystemAtomicFileOperations();
            private bool _failed;

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public string ReadAllText(string path) => _inner.ReadAllText(path);
            public void WriteNewTextAndFlush(string path, string content) => _inner.WriteNewTextAndFlush(path, content);
            public void CopyFile(string source, string destination, bool overwrite) => _inner.CopyFile(source, destination, overwrite);
            public void MoveFile(string source, string destination) => _inner.MoveFile(source, destination);
            public void DeleteFile(string path) => _inner.DeleteFile(path);

            public void ReplaceFile(string source, string destination)
            {
                if (!_failed)
                {
                    _failed = true;
                    File.Delete(destination);
                    throw new IOException("Injected replace failure.");
                }

                _inner.ReplaceFile(source, destination);
            }
        }
    }
}
