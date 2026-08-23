using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var header = ParseCsvLine(lines[0]);
            Assert.That(header, Does.Contain("ScriptGuid"));
            Assert.That(header, Does.Contain("SerializedFields"));
            Assert.That(header, Does.Contain("TargetComponent"));
            Assert.That(header, Does.Contain("PreservationRule"));

            foreach (var line in lines.Skip(1))
            {
                var fields = ParseCsvLine(line);
                Assert.That(fields.Count, Is.EqualTo(header.Count), $"Invalid serialization manifest row: {line}");
                Assert.That(Regex.IsMatch(fields[3], "^[0-9a-f]{32}$"), Is.True,
                    $"Invalid script GUID in row: {line}");
                Assert.That(fields[7], Is.Not.Empty, $"Missing target component in row: {line}");
                StringAssert.Contains("Missing Script prohibited", fields[8],
                    $"Missing compatibility rule in row: {line}");
            }
        }

        private static IReadOnlyList<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var value = new StringBuilder();
            var insideQuotes = false;

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character == '"')
                {
                    if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (character == ',' && !insideQuotes)
                {
                    fields.Add(value.ToString());
                    value.Clear();
                }
                else
                {
                    value.Append(character);
                }
            }

            if (insideQuotes)
            {
                throw new FormatException($"Unterminated quoted CSV field: {line}");
            }

            fields.Add(value.ToString());
            return fields;
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
