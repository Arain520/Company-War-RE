using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;
using CompanyWarRE.Infrastructure.Levels;
using NUnit.Framework;

namespace CompanyWarRE.Infrastructure.Tests
{
    public sealed class FormalLevelConfigurationPipelineTests
    {
        private const string Root = "Assets/CompanyWarRE/ConfigSamples/Compatibility/";
        private const string UnitsPath = Root + "LegacyUnits.All.json";
        private const string EnemiesPath = Root + "LegacyEnemies.All.json";
        private const string SchedulesPath = Root + "LegacySpawnSchedules.json";

        [TestCase("L02", 6, 10, 18, 30, 8, 4, 11, 2, 23, 17, 29)]
        [TestCase("L03", 8, 12, 24, 36, 10, 4, 14, 2, 29, 23, 29)]
        [TestCase("L04", 8, 12, 24, 36, 12, 5, 17, 2, 23, 23, 26)]
        [TestCase("L05", 8, 15, 24, 45, 15, 5, 21, 5, 32, 23, 38)]
        public void L02ThroughL05_LoadThroughVersionedFormalPipeline(
            string levelId,
            int sourceColumns,
            int sourceRows,
            int runtimeColumns,
            int runtimeRows,
            int buildingCount,
            int stageCount,
            int requiredScore,
            int firstColumn,
            int firstRow,
            int lastColumn,
            int lastRow)
        {
            var result = CreatePipeline(levelId).Load("units", "enemies", "schedules", "level");

            Assert.That(result.Succeeded, Is.True, JoinIssues(result.Issues));
            Assert.That(result.Level.SchemaVersion, Is.EqualTo(1));
            Assert.That(result.Level.LevelId, Is.EqualTo(levelId));
            Assert.That(result.Level.CoordinateSpace, Is.EqualTo(FormalLevelCoordinateConverter.MacroCoordinateSpace));
            Assert.That(result.Level.SourceColumns, Is.EqualTo(sourceColumns));
            Assert.That(result.Level.SourceRows, Is.EqualTo(sourceRows));
            Assert.That(result.Level.RuntimeColumns, Is.EqualTo(runtimeColumns));
            Assert.That(result.Level.RuntimeRows, Is.EqualTo(runtimeRows));
            Assert.That(result.Level.InitialControlledSourceRows, Is.EqualTo(2));
            Assert.That(result.Configuration.Columns, Is.EqualTo(runtimeColumns));
            Assert.That(result.Configuration.Rows, Is.EqualTo(runtimeRows));
            Assert.That(result.Configuration.ControlledRows, Is.EqualTo(6));
            Assert.That(result.Configuration.EnemyBuildings.Count, Is.EqualTo(buildingCount));
            Assert.That(result.Configuration.EnemyWaveStages.Count, Is.EqualTo(stageCount));
            Assert.That(result.Configuration.RequiredAssaultScore, Is.EqualTo(requiredScore));
            Assert.That(result.Configuration.VictoryByEnemyBuildings, Is.True);
            Assert.That(result.Configuration.EnemyBuildings.First().Position,
                Is.EqualTo(new GridPosition(firstColumn, firstRow)));
            Assert.That(result.Configuration.EnemyBuildings.Last().Position,
                Is.EqualTo(new GridPosition(lastColumn, lastRow)));
            Assert.That(result.Level.Environment.EnvironmentId, Is.EqualTo("Cow.DefaultIndustrial"));
            Assert.That(result.Level.Environment.DecorRing, Is.EqualTo(5));
            Assert.That(result.Level.Environment.SkylineDensity, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(result.Level.Environment.Seed, Is.EqualTo(1001));
        }

        [Test]
        public void MacroConversion_AlwaysMapsOneBlockToThreeCellsAndUsesItsCenter()
        {
            Assert.That(FormalLevelCoordinateConverter.ToSmallCellDimension(8), Is.EqualTo(24));
            Assert.That(FormalLevelCoordinateConverter.ToSmallCellStart(1), Is.EqualTo(1));
            Assert.That(FormalLevelCoordinateConverter.ToSmallCellCenter(1), Is.EqualTo(2));
            Assert.That(FormalLevelCoordinateConverter.ToSmallCellStart(8), Is.EqualTo(22));
            Assert.That(FormalLevelCoordinateConverter.ToSmallCellCenter(8), Is.EqualTo(23));
        }

        [Test]
        public void BatchValidator_ReportsAllFourFormalLevelsIndividually()
        {
            var documents = CommonDocuments();
            var keys = new[] { "L02", "L03", "L04", "L05" };
            foreach (var key in keys)
            {
                documents[key] = File.ReadAllText(Root + "FormalLevel." + key + ".json");
            }

            var report = new FormalLevelConfigurationPipeline(
                    new DictionaryConfigurationTextSource(documents))
                .ValidateBatch("units", "enemies", "schedules", keys);

            Assert.That(report.Succeeded, Is.True, JoinIssues(
                report.Issues.Concat(report.Entries.SelectMany(entry => entry.Issues))));
            Assert.That(report.Entries.Count, Is.EqualTo(4));
            Assert.That(report.Entries.Select(entry => entry.LevelId),
                Is.EqualTo(new[] { "L02", "L03", "L04", "L05" }));
            Assert.That(report.ErrorCount, Is.Zero);
        }

        [Test]
        public void FormalValidator_ReportsVersionCoordinateAndEnvironmentPaths()
        {
            var invalid = File.ReadAllText(Root + "FormalLevel.L02.json")
                .Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2")
                .Replace("\"MacroControlBlock3x3\"", "\"SmallCells\"")
                .Replace("\"EnvironmentId\": \"Cow.DefaultIndustrial\"", "\"EnvironmentId\": \"\"");
            var documents = CommonDocuments();
            documents["level"] = invalid;

            var result = new FormalLevelConfigurationPipeline(
                    new DictionaryConfigurationTextSource(documents))
                .Load("units", "enemies", "schedules", "level");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "CFG_SCHEMA" && issue.Path == "level.SchemaVersion"), Is.True);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "CFG_ENUM" && issue.Path == "level.CoordinateSpace"), Is.True);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "CFG_REQUIRED" && issue.Path == "level.Environment"), Is.True);
        }

        [Test]
        public void BatchValidator_RejectsDuplicateLevelIdsAndEmptyBatches()
        {
            var documents = CommonDocuments();
            documents["first"] = File.ReadAllText(Root + "FormalLevel.L02.json");
            documents["second"] = documents["first"];
            var pipeline = new FormalLevelConfigurationPipeline(
                new DictionaryConfigurationTextSource(documents));

            var duplicate = pipeline.ValidateBatch(
                "units",
                "enemies",
                "schedules",
                new[] { "first", "second" });
            var empty = pipeline.ValidateBatch(
                "units",
                "enemies",
                "schedules",
                new string[0]);

            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Issues.Any(issue => issue.Code == "CFG_DUPLICATE"), Is.True);
            Assert.That(empty.Succeeded, Is.False);
            Assert.That(empty.Issues.Any(issue => issue.Code == "CFG_REQUIRED"), Is.True);
        }

        private static FormalLevelConfigurationPipeline CreatePipeline(string levelId)
        {
            var documents = CommonDocuments();
            documents["level"] = File.ReadAllText(Root + "FormalLevel." + levelId + ".json");
            return new FormalLevelConfigurationPipeline(new DictionaryConfigurationTextSource(documents));
        }

        private static Dictionary<string, string> CommonDocuments()
        {
            return new Dictionary<string, string>
            {
                ["units"] = File.ReadAllText(UnitsPath),
                ["enemies"] = File.ReadAllText(EnemiesPath),
                ["schedules"] = File.ReadAllText(SchedulesPath)
            };
        }

        private static string JoinIssues(IEnumerable<ConfigurationIssue> issues)
        {
            return string.Join("\n", issues.Select(issue => issue.ToString()));
        }
    }
}
