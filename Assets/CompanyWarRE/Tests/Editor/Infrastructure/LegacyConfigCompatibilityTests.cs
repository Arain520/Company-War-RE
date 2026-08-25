using System.Collections.Generic;
using System.IO;
using System.Linq;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;
using NUnit.Framework;
using QFramework;

namespace CompanyWarRE.Infrastructure.Tests
{
    public sealed class LegacyConfigCompatibilityTests
    {
        private const string UnitsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.U01.json";
        private const string EnemiesPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.E01.json";
        private const string AllUnitsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.All.json";
        private const string AllEnemiesPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.All.json";
        private const string SettingsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/BattleSliceRuntime.json";
        private const string SpawnSchedulesPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacySpawnSchedules.json";
        private const string LevelPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyLevel.BattleSlice.json";
        private const string CowL01SettingsPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/BattleSliceRuntime.L01.json";
        private const string CowL01LevelPath =
            "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyLevel.L01.json";

        [Test]
        public void CompleteCatalog_MapsU01ThroughU36AndE01ThroughE15WithAbilityMetadata()
        {
            var provider = new LegacyBattleSliceConfigurationProvider(
                new DictionaryConfigurationTextSource(new Dictionary<string, string>
                {
                    ["units"] = File.ReadAllText(AllUnitsPath),
                    ["enemies"] = File.ReadAllText(AllEnemiesPath)
                }));

            var result = provider.LoadCatalog("units", "enemies");

            Assert.That(
                result.Succeeded,
                Is.True,
                string.Join("\n", result.Issues.Select(issue => issue.ToString())));
            Assert.That(result.Catalog.Units.Count, Is.EqualTo(36));
            Assert.That(result.Catalog.Allies.Count, Is.EqualTo(36));
            Assert.That(result.Catalog.Enemies.Count, Is.EqualTo(15));
            Assert.That(
                result.Catalog.Units.Keys.OrderBy(id => id),
                Is.EqualTo(Enumerable.Range(1, 36).Select(index => $"U{index:00}")));
            Assert.That(
                result.Catalog.Enemies.Keys.OrderBy(id => id),
                Is.EqualTo(Enumerable.Range(1, 15).Select(index => $"E{index:00}")));

            var u25 = result.Catalog.Units["U25"];
            Assert.That(u25.Name, Is.EqualTo("边境协防员"));
            Assert.That(u25.ResourceCost, Is.EqualTo(6));
            Assert.That(result.Catalog.Allies["U25"].Speed, Is.EqualTo(0.7d).Within(0.0001d));

            var u30 = result.Catalog.Allies["U30"];
            Assert.That(result.Catalog.Units["U30"].DeploymentMode, Is.EqualTo(DeploymentMode.StandardUnit));
            Assert.That(u30.IsStealth, Is.True);
            Assert.That(u30.IsBuilding, Is.False);
            Assert.That(u30.Attack, Is.EqualTo(0.3d).Within(0.0001d));

            Assert.That(result.Catalog.Units["U32"].Footprint, Is.EqualTo(UnitFootprint.ControlBlock));
            Assert.That(result.Catalog.Allies["U32"].HasConversionAction, Is.True);
            Assert.That(result.Catalog.Allies["U33"].HasHealingAction, Is.True);
            Assert.That(result.Catalog.Allies["U27"].HasAuthorityPushback, Is.True);
            Assert.That(result.Catalog.Enemies["E09"].HasHeavyStrike, Is.True);
            Assert.That(result.Catalog.Enemies["E13"].HasPersistentEnemyCurse, Is.True);
            Assert.That(result.Catalog.Enemies["E15"].HasExecutionCast, Is.True);
        }

        [Test]
        public void CompatibilitySamples_MapLegacyU01AndObservedRuntimeDefaults()
        {
            var units = File.ReadAllText(UnitsPath);
            var enemies = File.ReadAllText(EnemiesPath);
            var settings = File.ReadAllText(SettingsPath);
            var schedules = File.ReadAllText(SpawnSchedulesPath);
            var level = File.ReadAllText(LevelPath);

            var result = CreateProvider(units, settings, enemies, schedules, level)
                .Load("units", "enemies", "settings", "schedules", "level");

            Assert.That(result.Succeeded, Is.True, JoinIssues(result));
            Assert.That(result.Configuration.Columns, Is.EqualTo(15));
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
            Assert.That(result.Configuration.EnemyWaveStages.Count, Is.EqualTo(3));
            Assert.That(
                result.Configuration.EnemyWaveStages.Select(item => item.Name),
                Is.EqualTo(new[] { "Stage1", "Stage2", "Finale" }));
            Assert.That(result.Configuration.EnemyWaveStages[0].WaveIntervalSeconds, Is.EqualTo(10d));
            Assert.That(result.Configuration.EnemyWaveStages[1].EnemiesPerWave, Is.EqualTo(2));
            Assert.That(result.Configuration.EnemyCombatants.Keys, Does.Contain("E05"));
            var e06 = result.Configuration.EnemyCombatants["E06"];
            var e07 = result.Configuration.EnemyCombatants["E07"];
            var e12 = result.Configuration.EnemyCombatants["E12"];
            var e13 = result.Configuration.EnemyCombatants["E13"];
            var e14 = result.Configuration.EnemyCombatants["E14"];
            var e15 = result.Configuration.EnemyCombatants["E15"];
            Assert.That(
                new[] { e06.Durability, e07.Durability, e12.Durability },
                Is.EqualTo(new[] { 7, 12, 15 }));
            Assert.That(new[] { e06.Attack, e07.Attack, e12.Attack }, Is.All.Zero);
            Assert.That(
                new[] { e06.AssaultScoreReward, e07.AssaultScoreReward, e12.AssaultScoreReward },
                Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(e13.HasPersistentEnemyCurse, Is.True);
            Assert.That(
                new[] { e13.Durability, e14.Durability, e15.Durability },
                Is.EqualTo(new[] { 18, 20, 15 }));
            Assert.That(new[] { e13.Attack, e14.Attack, e15.Attack }, Is.EqualTo(new[] { 1d, 1d, 0d }));
            Assert.That(
                new[] { e13.AttackIntervalSeconds, e14.AttackIntervalSeconds, e15.AttackIntervalSeconds },
                Is.EqualTo(new[] { 0.5d, 1d, 12d }));
            Assert.That(new[] { e13.Range, e14.Range, e15.Range }, Is.EqualTo(new[] { 3, 1, 99 }));
            Assert.That(result.Configuration.EnemySpawnColumns.Count, Is.EqualTo(15));
            Assert.That(result.Configuration.EnemyBuildings.Count, Is.EqualTo(3));
            Assert.That(result.Configuration.EnemyBuildings.Select(item => item.TemplateId),
                Is.EqualTo(new[] { "E06", "E07", "E06" }));
            Assert.That(result.Configuration.EnemyBuildings.Select(item => item.Position.Column),
                Is.EqualTo(new[] { 2, 8, 14 }));
            Assert.That(result.Configuration.RequiredAssaultScore, Is.EqualTo(8));
            Assert.That(result.Configuration.VictoryByEnemyBuildings, Is.True);
        }

        [Test]
        public void CowL01_ExpandsMacroCoordinatesAndSelectsConfiguredStages()
        {
            var result = CreateProvider(
                    File.ReadAllText(UnitsPath),
                    File.ReadAllText(CowL01SettingsPath),
                    File.ReadAllText(EnemiesPath),
                    File.ReadAllText(SpawnSchedulesPath),
                    File.ReadAllText(CowL01LevelPath))
                .Load("units", "enemies", "settings", "schedules", "level");

            Assert.That(result.Succeeded, Is.True, JoinIssues(result));
            Assert.That(result.Configuration.Columns, Is.EqualTo(18));
            Assert.That(result.Configuration.Rows, Is.EqualTo(30));
            Assert.That(result.Configuration.ControlledRows, Is.EqualTo(6));
            Assert.That(result.Configuration.RequiredAssaultScore, Is.EqualTo(8));
            Assert.That(
                result.Configuration.EnemyWaveStages.Select(item => item.Name),
                Is.EqualTo(new[] { "Stage1", "Stage2", "Finale" }));
            Assert.That(
                result.Configuration.EnemyBuildings.Select(item => item.TemplateId),
                Is.EqualTo(new[] { "E06", "E06", "E06", "E06", "E07", "E07" }));
            Assert.That(
                result.Configuration.EnemyBuildings.Select(item => item.Position.Column),
                Is.EqualTo(new[] { 2, 5, 14, 17, 8, 11 }));
            Assert.That(
                result.Configuration.EnemyBuildings.Select(item => item.Position.Row),
                Is.EqualTo(new[] { 29, 29, 29, 29, 29, 23 }));
        }

        [Test]
        public void CowL01_InitializesApplicationGridBuildingsAndFirstWave()
        {
            var result = CreateProvider(
                    File.ReadAllText(UnitsPath),
                    File.ReadAllText(CowL01SettingsPath),
                    File.ReadAllText(EnemiesPath),
                    File.ReadAllText(SpawnSchedulesPath),
                    File.ReadAllText(CowL01LevelPath))
                .Load("units", "enemies", "settings", "schedules", "level");
            Assert.That(result.Succeeded, Is.True, JoinIssues(result));

            IArchitecture architecture = BattleSliceArchitecture.Interface;
            try
            {
                architecture.SendCommand(new ConfigureBattleSliceCommand(result.Configuration));
                var initial = architecture.SendQuery(new GetBattleSliceSnapshotQuery());

                Assert.That(initial.Cells.Count, Is.EqualTo(18 * 30));
                Assert.That(initial.Cells.Count(item => item.IsOwned), Is.EqualTo(18 * 6));
                Assert.That(initial.Cells.Count(item => item.IsBlockedByBuilding), Is.EqualTo(6 * 9));
                Assert.That(initial.EnemyBuildingCount, Is.EqualTo(6));
                Assert.That(initial.ValidSpawnPointCount, Is.EqualTo(18));
                Assert.That(initial.CurrentWaveStage, Is.EqualTo("Stage1"));

                architecture.SendCommand(new AdvanceBattleSliceTimeCommand(10d));
                var advanced = architecture.SendQuery(new GetBattleSliceSnapshotQuery());
                var spawn = advanced.EnemySpawns.Single();
                var expectedRow = spawn.Position.Column >= 10 && spawn.Position.Column <= 12
                    ? 21
                    : 27;
                Assert.That(spawn.Position.Row, Is.EqualTo(expectedRow));
                Assert.That(spawn.StageName, Is.EqualTo("Stage1"));
            }
            finally
            {
                architecture.Deinit();
            }
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

        [Test]
        public void LevelValidator_RejectsOverlappingAndIncompleteNineCellControlBlocks()
        {
            const string units =
                "{\"Units\":[{\"Id\":\"U01\",\"Type\":\"Staff\",\"Durability\":1," +
                "\"Attack\":1,\"Speed\":1,\"AttackInterval\":1,\"Range\":1," +
                "\"ResourceCost\":1,\"DeployCooldown\":3}]}";
            const string level =
                "{\"Id\":\"invalid\",\"Columns\":5,\"Rows\":6,\"EnemyBuildings\":[" +
                "{\"Type\":\"E06\",\"Col\":2,\"Row\":6}," +
                "{\"Type\":\"E07\",\"Col\":3,\"Row\":6}," +
                "{\"Type\":\"E06\",\"Col\":5,\"Row\":6}]}";
            var provider = CreateProvider(
                units,
                ValidSettingsJson().Replace("\"Columns\":6", "\"Columns\":5"),
                File.ReadAllText(EnemiesPath),
                null,
                level);

            var result = provider.Load("units", "enemies", "settings", null, "level");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "CFG_DUPLICATE"), Is.True);
            Assert.That(result.Issues.Any(issue => issue.Code == "CFG_RANGE"), Is.True);
        }

        private static LegacyBattleSliceConfigurationProvider CreateProvider(
            string units,
            string settings,
            string enemies = null,
            string schedules = null,
            string level = null)
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

            if (level != null)
            {
                documents["level"] = level;
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
