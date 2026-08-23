using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class ManifestContractTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve the Unity project root.");

        [Test]
        public void FirstBatchInventory_ContainsRequiredManifests()
        {
            var inventoryRoot = Path.Combine(ProjectRoot, "Migration", "Inventory", "FirstBatch");
            var requiredFiles = new[]
            {
                "AssetInventory.csv",
                "DependencyCompatibilityMatrix.csv",
                "GuidMap.csv",
                "GuidReferences.csv",
                "ModuleMapping.csv",
                "Packages.csv",
                "PersistenceCodeEvidence.csv",
                "PersistenceSchema.csv",
                "ProjectSettingsMigrationMatrix.csv",
                "SerializationAttributeEvidence.csv",
                "SerializationMigrationTable.csv",
                "UnityVersions.csv"
            };

            Assert.That(requiredFiles.Where(file => !File.Exists(Path.Combine(inventoryRoot, file))), Is.Empty);
        }

        [Test]
        public void SerializationManifest_RecordsValidScriptGuidsAndCompatibilityRules()
        {
            var path = Path.Combine(ProjectRoot, "Migration", "Inventory", "FirstBatch", "SerializationMigrationTable.csv");
            var lines = File.ReadAllLines(path);
            Assert.That(lines.Length, Is.GreaterThan(1));
            StringAssert.Contains("ScriptGuid", lines[0]);
            StringAssert.Contains("SerializedFields", lines[0]);
            StringAssert.Contains("TargetComponent", lines[0]);
            StringAssert.Contains("PreservationRule", lines[0]);

            var rowPattern = new Regex("^\"[^\"]+\",\"[^\"]+\",\"[^\"]*\",\"(?<guid>[0-9a-f]{32})\"");
            foreach (var line in lines.Skip(1))
            {
                var match = rowPattern.Match(line);
                Assert.That(match.Success, Is.True, $"Invalid serialization manifest row: {line}");
            }
        }

        [Test]
        public void CowSnapshot_HasPassedRestoreVerification()
        {
            var cowBaselineRoot = Path.Combine(ProjectRoot, "Migration", "Baseline", "Cow");
            var snapshotDirectories = Directory.GetDirectories(cowBaselineRoot);
            Assert.That(snapshotDirectories.Length, Is.GreaterThan(0));

            var verified = snapshotDirectories.Any(directory =>
            {
                var report = Path.Combine(directory, "restore-verification.txt");
                return File.Exists(report) && File.ReadAllText(report).Contains("verification: PASS");
            });

            Assert.That(verified, Is.True, "No restore-verified Cow snapshot was found.");
        }
    }
}
