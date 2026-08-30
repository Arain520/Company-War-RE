using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompanyWarRE.Infrastructure.Configuration;
using CompanyWarRE.Infrastructure.Levels;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    public static class FormalLevelBatchValidationTool
    {
        private const string Root = "Assets/CompanyWarRE/ConfigSamples/Compatibility/";
        private const string LevelRoot =
            "Assets/CompanyWarRE/Resources/CompanyWarRE/Configs/Levels/";
        private static readonly string[] LevelPaths = Enumerable.Range(0, 21)
            .Select(index => LevelRoot + $"L{index:00}.json")
            .Concat(new[] { LevelRoot + "L_ENDLESS.json" })
            .ToArray();

        [MenuItem("Company War-RE/Migration/Validate All Cow Formal Levels")]
        public static void ValidateFromMenu()
        {
            var report = ValidateAndWriteReport();
            EditorUtility.DisplayDialog(
                "Formal level validation",
                report.Succeeded
                    ? $"Validated {report.Levels.Length} levels with no errors."
                    : $"Validation failed with {report.ErrorCount} errors. See {ReportPath()}.",
                "OK");
        }

        public static void ValidateForBatch()
        {
            var report = ValidateAndWriteReport();
            if (!report.Succeeded)
            {
                throw new InvalidOperationException(
                    "Formal level validation failed with " + report.ErrorCount + " errors. " + ReportPath());
            }
        }

        private static SerializableReport ValidateAndWriteReport()
        {
            var documents = new Dictionary<string, string>
            {
                [Root + "LegacyUnits.All.json"] = Read(Root + "LegacyUnits.All.json"),
                [Root + "LegacyEnemies.All.json"] = Read(Root + "LegacyEnemies.All.json"),
                [Root + "LegacySpawnSchedules.json"] = Read(Root + "LegacySpawnSchedules.json")
            };
            foreach (var path in LevelPaths)
            {
                documents[path] = Read(path);
            }

            var validation = new FormalLevelConfigurationPipeline(
                    new DictionaryConfigurationTextSource(documents))
                .ValidateBatch(
                    Root + "LegacyUnits.All.json",
                    Root + "LegacyEnemies.All.json",
                    Root + "LegacySpawnSchedules.json",
                    LevelPaths);
            var report = new SerializableReport
            {
                SchemaVersion = 1,
                Succeeded = validation.Succeeded,
                ErrorCount = validation.ErrorCount,
                Levels = validation.Entries.Select(entry => new SerializableLevel
                {
                    Document = entry.DocumentKey,
                    LevelId = entry.LevelId,
                    Succeeded = entry.Succeeded,
                    Issues = entry.Issues.Select(MapIssue).ToArray()
                }).ToArray(),
                Issues = validation.Issues.Select(MapIssue).ToArray()
            };
            var output = ReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            if (report.Succeeded)
            {
                Debug.Log("Formal level validation passed: " + output);
            }
            else
            {
                Debug.LogError("Formal level validation failed: " + output);
            }

            return report;
        }

        private static SerializableIssue MapIssue(ConfigurationIssue issue)
        {
            return new SerializableIssue
            {
                Code = issue.Code,
                Path = issue.Path,
                Message = issue.Message
            };
        }

        private static string Read(string relativePath)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string ReportPath()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "Migration",
                "Reports",
                "FormalLevelValidation.json");
        }

        [Serializable]
        private sealed class SerializableReport
        {
            public int SchemaVersion;
            public bool Succeeded;
            public int ErrorCount;
            public SerializableLevel[] Levels;
            public SerializableIssue[] Issues;
        }

        [Serializable]
        private sealed class SerializableLevel
        {
            public string Document;
            public string LevelId;
            public bool Succeeded;
            public SerializableIssue[] Issues;
        }

        [Serializable]
        private sealed class SerializableIssue
        {
            public string Code;
            public string Path;
            public string Message;
        }
    }
}
