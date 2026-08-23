using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;
using NUnit.Framework;

namespace CompanyWarRE.Infrastructure.Tests
{
    public sealed class LegacyConfigCompatibilityTests
    {
        private const string UnitsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.U01.json";
        private const string EnemiesPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.E01.json";
        private const string SettingsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/BattleSliceRuntime.json";
        private const string SpawnSchedulesPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacySpawnSchedules.json";

        [Test]
        public void CompatibilitySamples_MapLegacyU01AndObservedRuntimeDefaults()
        {
            var units = File.ReadAllText(UnitsPath);
            var enemies = File.ReadAllText(EnemiesPath);
            var settings = File.ReadAllText(SettingsPath);
            var schedules = File.ReadAllText(SpawnSchedulesPath);

            var result = CreateProvider(units, settings, enemies, schedules)
                .Load("units", "enemies", "settings", "schedules");

            Assert.That(result.Succeeded, Is.True, JoinIssues(result));
            Assert.That(result.Configuration.Columns, Is.EqualTo(9));
            Assert.That(result.Configuration.Rows, Is.EqualTo(9));
            Assert.That(result.Configuration.ControlledRows, Is.EqualTo(6));
            Assert.That(result.Configuration.InitialResources, Is.EqualTo(10));
            Assert.That(result.Configuration.FixedProductionIntervalSeconds, Is.EqualTo(5d));
            Assert.That(result.Configuration.TransmitterProductionIntervalSeconds, Is.EqualTo(3d));
            Assert.That(result.Configuration.TestUnit.Id, Is.EqualTo("U01"));
            Assert.That(result.Configuration.TestUnit.ResourceCost, Is.EqualTo(1));
            Assert.That(result.Configuration.TestUnit.DeploymentCooldownSeconds, Is.EqualTo(3d));
            Assert.That(result.Configuration.TestUnit.DeploymentMode, Is.EqualTo(DeploymentMode.StandardUnit));
            Assert.That(result.Configuration.TestUnit.Footprint, Is.EqualTo(UnitFootprint.SmallCell));
            Assert.That(result.Configuration.AllyCombatant.Id, Is.EqualTo("U01"));
            Assert.That(result.Configuration.AllyCombatant.Durability, Is.EqualTo(1));
            Assert.That(result.Configuration.AllyCombatant.Attack, Is.EqualTo(1d));
            Assert.That(result.Configuration.EnemyCombatant.Id, Is.EqualTo("E01"));
            Assert.That(result.Configuration.EnemyCombatant.Durability, Is.EqualTo(1));
            Assert.That(result.Configuration.EnemyCombatant.AssaultScoreReward, Is.EqualTo(1));
            Assert.That(result.Configuration.EnemySpawnPosition, Is.EqualTo(new GridPosition(3, 9)));
            Assert.That(result.Configuration.EnemyWaveStages.Count, Is.EqualTo(5));
            Assert.That(result.Configuration.EnemyWaveStages[0].WaveIntervalSeconds, Is.EqualTo(10d));
            Assert.That(result.Configuration.EnemyWaveStages[1].EnemiesPerWave, Is.EqualTo(2));
            Assert.That(result.Configuration.EnemyCombatants.Keys, Does.Contain("E05"));
            Assert.That(result.Configuration.EnemySpawnColumns.Count, Is.EqualTo(9));
        }

        [Test]
        public void SourcePort_ReportsMissingDocumentsWithoutSelectingAStorageTechnology()
        {
            var provider = new LegacyBattleSliceConfigurationProvider(
                new DictionaryConfigurationTextSource(new Dictionary<string, string>()));

            var result = provider.Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Count, Is.EqualTo(3));
            Assert.That(result.Issues.All(issue => issue.Code == "CFG_SOURCE"), Is.True);
        }

        [Test]
        public void Parser_ReportsMalformedJson()
        {
            var result = CreateProvider(
                    "{not-json",
                    ValidSettingsJson())
                .Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CFG_PARSE"));
        }

        [Test]
        public void Validator_RejectsDuplicateUnitIdsCaseInsensitively()
        {
            const string units =
                "{\"Units\":[" +
                "{\"Id\":\"U01\",\"Type\":\"Staff\",\"ResourceCost\":1,\"DeployCooldown\":3}," +
                "{\"Id\":\"u01\",\"Type\":\"Staff\",\"ResourceCost\":1,\"DeployCooldown\":3}" +
                "]}";

            var result = CreateProvider(units, ValidSettingsJson()).Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CFG_DUPLICATE"));
        }

        [Test]
        public void Mapper_PreservesBuildingAndTerrainBuildModes()
        {
            const string buildingUnits =
                "{\"Units\":[{\"Id\":\"U08\",\"Type\":\"Building\",\"ResourceCost\":2," +
                "\"DeployCooldown\":10,\"FootprintType\":\"ControlBlock\"}]}";
            var building = CreateProvider(
                    buildingUnits,
                    ValidSettingsJson("U08"))
                .Load("units", "enemies", "settings");
            Assert.That(building.Succeeded, Is.True, JoinIssues(building));
            Assert.That(building.Configuration.TestUnit.DeploymentMode, Is.EqualTo(DeploymentMode.Building));
            Assert.That(building.Configuration.TestUnit.Footprint, Is.EqualTo(UnitFootprint.ControlBlock));

            const string terrainUnits =
                "{\"Units\":[{\"Id\":\"U15\",\"Type\":\"Support\",\"Effect\":\"TerrainBuild\"," +
                "\"ResourceCost\":10,\"DeployCooldown\":20}]}";
            var terrain = CreateProvider(
                    terrainUnits,
                    ValidSettingsJson("U15"))
                .Load("units", "enemies", "settings");
            Assert.That(terrain.Succeeded, Is.True, JoinIssues(terrain));
            Assert.That(terrain.Configuration.TestUnit.DeploymentMode, Is.EqualTo(DeploymentMode.TerrainBuild));
        }

        [Test]
        public void Validator_RejectsUnsupportedSchemaAndInvalidRanges()
        {
            const string units =
                "{\"Units\":[{\"Id\":\"U01\",\"Type\":\"Staff\",\"ResourceCost\":-1," +
                "\"DeployCooldown\":-1}]}";
            const string settings =
                "{\"SchemaVersion\":2,\"Columns\":0,\"Rows\":6,\"ControlledRows\":7," +
                "\"InitialResources\":-1,\"FixedProductionIntervalSeconds\":0," +
                "\"TransmitterProductionIntervalSeconds\":0,\"TransmitterAmount\":-1," +
                "\"TestUnitId\":\"U01\"}";

            var result = CreateProvider(units, settings).Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Select(issue => issue.Code), Does.Contain("CFG_SCHEMA"));
            Assert.That(result.Issues.Count(issue => issue.Code == "CFG_RANGE"), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Settings_DeprecatedControlledColumnsAliasMapsToControlledRows()
        {
            const string units =
                "{\"Units\":[{\"Id\":\"U01\",\"Type\":\"Staff\",\"Durability\":1," +
                "\"Attack\":1,\"Speed\":1,\"AttackInterval\":1,\"Range\":1," +
                "\"ResourceCost\":1,\"DeployCooldown\":3}]}";
            var legacySettings = ValidSettingsJson().Replace("ControlledRows", "ControlledColumns");

            var result = CreateProvider(units, legacySettings).Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.True, JoinIssues(result));
            Assert.That(result.Configuration.ControlledRows, Is.EqualTo(3));
        }

        [Test]
        public void Validator_RejectsEnemySpawnInsideAllyControlledRows()
        {
            var settings = ValidSettingsJson().Replace("\"EnemySpawnRow\":6", "\"EnemySpawnRow\":3");

            var result = CreateProvider(
                    "{\"Units\":[{\"Id\":\"U01\",\"Type\":\"Staff\",\"Durability\":1," +
                    "\"Attack\":1,\"Speed\":1,\"AttackInterval\":1,\"Range\":1," +
                    "\"ResourceCost\":1,\"DeployCooldown\":3}]}",
                    settings)
                .Load("units", "enemies", "settings");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Any(issue =>
                issue.Code == "CFG_RANGE" && issue.Path.EndsWith(".EnemySpawnPosition")), Is.True);
        }

        [Test]
        public void SpawnSchedule_RejectsUnknownEnemyReference()
        {
            const string schedules =
                "{\"Stages\":[{\"Name\":\"Stage1\",\"Period\":\"0:00-0:59\"," +
                "\"Rate\":\"1/10 sec\",\"PerWave\":1," +
                "\"Types\":[{\"Id\":\"E99\",\"Weight\":1}]}]}";
            var provider = CreateProvider(
                "{\"Units\":[{\"Id\":\"U01\",\"Type\":\"Staff\",\"Durability\":1," +
                "\"Attack\":1,\"Speed\":1,\"AttackInterval\":1,\"Range\":1," +
                "\"ResourceCost\":1,\"DeployCooldown\":3}]}",
                ValidSettingsJson(),
                null,
                schedules);

            var result = provider.Load("units", "enemies", "settings", "schedules");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "CFG_REFERENCE"), Is.True);
        }

        private static LegacyBattleSliceConfigurationProvider CreateProvider(
            string units,
            string settings,
            string enemies = null,
            string schedules = null)
        {
            var documents = new Dictionary<string, string>
            {
                ["units"] = units,
                ["enemies"] = enemies ?? ValidEnemiesJson(),
                ["settings"] = settings
            };
            if (schedules != null)
            {
                documents["schedules"] = schedules;
            }

            return new LegacyBattleSliceConfigurationProvider(new DictionaryConfigurationTextSource(documents));
        }

        private static string ValidSettingsJson(string unitId = "U01")
        {
            return
                "{\"SchemaVersion\":1,\"Columns\":6,\"Rows\":6,\"ControlledRows\":3," +
                "\"InitialResources\":10,\"FixedProductionIntervalSeconds\":5," +
                "\"TransmitterProductionIntervalSeconds\":3,\"TransmitterColumn\":2," +
                "\"TransmitterRow\":2,\"TransmitterAmount\":1,\"TestUnitId\":\"" + unitId + "\"," +
                "\"TestEnemyId\":\"E01\",\"EnemySpawnColumn\":3,\"EnemySpawnRow\":6}";
        }

        private static string ValidEnemiesJson()
        {
            return
                "{\"Enemies\":[{\"Id\":\"E01\",\"Name\":\"炮灰\",\"Type\":\"Staff\"," +
                "\"Durability\":1,\"Attack\":1,\"Speed\":1,\"AttackInterval\":1," +
                "\"Range\":1,\"AssaultScoreReward\":1}]}";
        }

        private static string JoinIssues(BattleSliceConfigurationLoadResult result)
        {
            return string.Join("\n", result.Issues.Select(issue => issue.ToString()));
        }
    }
}
