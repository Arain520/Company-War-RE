using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;

namespace CompanyWarRE.Infrastructure.Configuration
{
    [Serializable]
    [DataContract]
    public sealed class LegacyUnitsDocumentDto
    {
        [DataMember(Name = "Units")]
        public List<LegacyUnitDto> Units;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacyUnitDto
    {
        [DataMember(Name = "Id")]
        public string Id;
        [DataMember(Name = "Name")]
        public string Name;
        [DataMember(Name = "Type")]
        public string Type;
        [DataMember(Name = "Durability")]
        public int Durability;
        [DataMember(Name = "Attack")]
        public float Attack;
        [DataMember(Name = "Speed")]
        public float Speed;
        [DataMember(Name = "AttackInterval")]
        public float AttackInterval;
        [DataMember(Name = "Range")]
        public int Range;
        [DataMember(Name = "ResourceCost")]
        public int ResourceCost;
        [DataMember(Name = "DeployCooldown")]
        public float DeployCooldown;
        [DataMember(Name = "Effect")]
        public string Effect;
        [DataMember(Name = "ResourceRate")]
        public float ResourceRate;
        [DataMember(Name = "ScoreRate")]
        public float ScoreRate;
        [DataMember(Name = "CanDeployOutside")]
        public bool CanDeployOutside;
        [DataMember(Name = "FootprintType")]
        public string FootprintType;
        [DataMember(Name = "FootprintSize")]
        public int FootprintSize;
        [DataMember(Name = "VisualScale")]
        public float VisualScale;
        [DataMember(Name = "BlockMovement")]
        public bool BlockMovement;
        [DataMember(Name = "BlockDeployment")]
        public bool BlockDeployment;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacyEnemiesDocumentDto
    {
        [DataMember(Name = "Enemies")]
        public List<LegacyEnemyDto> Enemies;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacyEnemyDto
    {
        [DataMember(Name = "Id")]
        public string Id;
        [DataMember(Name = "Name")]
        public string Name;
        [DataMember(Name = "Type")]
        public string Type;
        [DataMember(Name = "Durability")]
        public int Durability;
        [DataMember(Name = "Attack")]
        public float Attack;
        [DataMember(Name = "Speed")]
        public float Speed;
        [DataMember(Name = "AttackInterval")]
        public float AttackInterval;
        [DataMember(Name = "Range")]
        public int Range;
        [DataMember(Name = "AssaultScoreReward")]
        public int AssaultScoreReward;
    }

    [Serializable]
    [DataContract]
    public sealed class BattleSliceSettingsDto
    {
        [DataMember(Name = "SchemaVersion")]
        public int SchemaVersion;
        [DataMember(Name = "Columns")]
        public int Columns;
        [DataMember(Name = "Rows")]
        public int Rows;
        [DataMember(Name = "ControlledRows")]
        public int ControlledRows;
        [DataMember(Name = "ControlledColumns")]
        public int LegacyControlledColumns;
        [DataMember(Name = "InitialResources")]
        public int InitialResources;
        [DataMember(Name = "FixedProductionIntervalSeconds")]
        public float FixedProductionIntervalSeconds;
        [DataMember(Name = "TransmitterProductionIntervalSeconds")]
        public float TransmitterProductionIntervalSeconds;
        [DataMember(Name = "TransmitterColumn")]
        public int TransmitterColumn;
        [DataMember(Name = "TransmitterRow")]
        public int TransmitterRow;
        [DataMember(Name = "TransmitterAmount")]
        public int TransmitterAmount;
        [DataMember(Name = "TestUnitId")]
        public string TestUnitId;
        [DataMember(Name = "TestEnemyId")]
        public string TestEnemyId;
        [DataMember(Name = "EnemySpawnColumn")]
        public int EnemySpawnColumn;
        [DataMember(Name = "EnemySpawnRow")]
        public int EnemySpawnRow;
    }

    public interface IConfigurationTextSource
    {
        bool TryRead(string key, out string text, out string error);
    }

    public sealed class DictionaryConfigurationTextSource : IConfigurationTextSource
    {
        private readonly IReadOnlyDictionary<string, string> _documents;

        public DictionaryConfigurationTextSource(IReadOnlyDictionary<string, string> documents)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        }

        public bool TryRead(string key, out string text, out string error)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Configuration key is empty.";
                return false;
            }

            if (!_documents.TryGetValue(key, out text) || string.IsNullOrWhiteSpace(text))
            {
                error = $"Configuration document was not found or was empty: {key}";
                return false;
            }

            error = null;
            return true;
        }
    }

    public sealed class ConfigurationIssue
    {
        public ConfigurationIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"{Code} at {Path}: {Message}";
        }
    }

    public sealed class BattleSliceConfigurationLoadResult
    {
        public BattleSliceConfigurationLoadResult(
            BattleSliceConfiguration configuration,
            IReadOnlyList<ConfigurationIssue> issues)
        {
            Configuration = configuration;
            Issues = issues ?? Array.Empty<ConfigurationIssue>();
        }

        public BattleSliceConfiguration Configuration { get; }
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
        public bool Succeeded => Configuration != null && Issues.Count == 0;
    }

    public sealed class LegacyBattleSliceConfigurationProvider
    {
        public const int SupportedSettingsSchemaVersion = 1;

        private readonly IConfigurationTextSource _source;

        public LegacyBattleSliceConfigurationProvider(IConfigurationTextSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public BattleSliceConfigurationLoadResult Load(string unitsKey, string enemiesKey, string settingsKey)
        {
            var issues = new List<ConfigurationIssue>();
            if (!_source.TryRead(unitsKey, out var unitsJson, out var unitsReadError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", unitsKey, unitsReadError));
            }

            if (!_source.TryRead(enemiesKey, out var enemiesJson, out var enemiesReadError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", enemiesKey, enemiesReadError));
            }

            if (!_source.TryRead(settingsKey, out var settingsJson, out var settingsReadError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", settingsKey, settingsReadError));
            }

            if (issues.Count > 0)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            var units = Parse<LegacyUnitsDocumentDto>(unitsJson, unitsKey, issues);
            var enemies = Parse<LegacyEnemiesDocumentDto>(enemiesJson, enemiesKey, issues);
            var settings = Parse<BattleSliceSettingsDto>(settingsJson, settingsKey, issues);
            if (units == null || enemies == null || settings == null)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            ValidateSettings(settings, settingsKey, issues);
            var selectedUnit = ValidateAndFindUnit(units, settings.TestUnitId, unitsKey, issues);
            var selectedEnemy = ValidateAndFindEnemy(enemies, settings.TestEnemyId, enemiesKey, issues);
            if (issues.Count > 0 || selectedUnit == null || selectedEnemy == null)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            var deploymentMode = MapDeploymentMode(selectedUnit, unitsKey, issues);
            var footprint = MapFootprint(selectedUnit.FootprintType, unitsKey, selectedUnit.Id, issues);
            if (issues.Count > 0)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            var unit = new UnitDefinition(
                selectedUnit.Id,
                selectedUnit.ResourceCost,
                selectedUnit.DeployCooldown,
                deploymentMode,
                footprint);
            var controlledRows = ResolveControlledRows(settings);
            var allyCombatant = new CombatantDefinition(
                selectedUnit.Id,
                selectedUnit.Type,
                selectedUnit.Durability,
                selectedUnit.Attack,
                selectedUnit.Speed,
                selectedUnit.AttackInterval,
                selectedUnit.Range);
            var enemyCombatant = new CombatantDefinition(
                selectedEnemy.Id,
                selectedEnemy.Type,
                selectedEnemy.Durability,
                selectedEnemy.Attack,
                selectedEnemy.Speed,
                selectedEnemy.AttackInterval,
                selectedEnemy.Range,
                selectedEnemy.AssaultScoreReward);
            var configuration = new BattleSliceConfiguration(
                settings.Columns,
                settings.Rows,
                controlledRows,
                settings.InitialResources,
                settings.FixedProductionIntervalSeconds,
                settings.TransmitterProductionIntervalSeconds,
                new GridPosition(settings.TransmitterColumn, settings.TransmitterRow),
                settings.TransmitterAmount,
                unit,
                allyCombatant,
                enemyCombatant,
                new GridPosition(settings.EnemySpawnColumn, settings.EnemySpawnRow));
            return new BattleSliceConfigurationLoadResult(configuration, issues);
        }

        private static T Parse<T>(string json, string path, ICollection<ConfigurationIssue> issues)
            where T : class
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                T value;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    value = serializer.ReadObject(stream) as T;
                }

                if (value == null)
                {
                    issues.Add(new ConfigurationIssue("CFG_PARSE", path, "JSON produced no object."));
                }

                return value;
            }
            catch (Exception exception)
            {
                issues.Add(new ConfigurationIssue("CFG_PARSE", path, exception.Message));
                return null;
            }
        }

        private static void ValidateSettings(
            BattleSliceSettingsDto settings,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (settings.SchemaVersion != SupportedSettingsSchemaVersion)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_SCHEMA",
                    path + ".SchemaVersion",
                    $"Expected {SupportedSettingsSchemaVersion}, got {settings.SchemaVersion}."));
            }

            if (settings.Columns <= 0 || settings.Rows <= 0)
            {
                issues.Add(new ConfigurationIssue("CFG_RANGE", path, "Grid dimensions must be positive."));
            }

            var controlledRows = ResolveControlledRows(settings);
            if (controlledRows <= 0 || controlledRows > settings.Rows)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".ControlledRows",
                    "ControlledRows must be inside the configured grid."));
            }

            if (settings.InitialResources < 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".InitialResources",
                    "InitialResources cannot be negative."));
            }

            if (settings.FixedProductionIntervalSeconds <= 0f ||
                settings.TransmitterProductionIntervalSeconds <= 0f)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path,
                    "Production intervals must be positive."));
            }

            if (settings.TransmitterAmount < 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".TransmitterAmount",
                    "TransmitterAmount cannot be negative."));
            }

            if (settings.TransmitterAmount > 0 &&
                (settings.TransmitterColumn < 1 || settings.TransmitterColumn > settings.Columns ||
                 settings.TransmitterRow < 1 || settings.TransmitterRow > settings.Rows))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".TransmitterPosition",
                    "Enabled transmitter position must be inside the grid."));
            }

            if (string.IsNullOrWhiteSpace(settings.TestUnitId))
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".TestUnitId", "TestUnitId is required."));
            }

            if (string.IsNullOrWhiteSpace(settings.TestEnemyId))
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".TestEnemyId", "TestEnemyId is required."));
            }

            if (settings.EnemySpawnColumn < 1 || settings.EnemySpawnColumn > settings.Columns ||
                settings.EnemySpawnRow < 1 || settings.EnemySpawnRow > settings.Rows)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".EnemySpawnPosition",
                    "Enemy spawn position must be inside the grid."));
            }
            else if (settings.EnemySpawnRow <= controlledRows)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".EnemySpawnPosition",
                    "Enemy spawn position must be outside ally-controlled rows."));
            }
        }

        private static int ResolveControlledRows(BattleSliceSettingsDto settings)
        {
            return settings.ControlledRows > 0
                ? settings.ControlledRows
                : settings.LegacyControlledColumns;
        }

        private static LegacyUnitDto ValidateAndFindUnit(
            LegacyUnitsDocumentDto document,
            string selectedUnitId,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (document.Units == null || document.Units.Count == 0)
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Units", "Units cannot be empty."));
                return null;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LegacyUnitDto selected = null;
            for (var index = 0; index < document.Units.Count; index++)
            {
                var unit = document.Units[index];
                var unitPath = $"{path}.Units[{index}]";
                if (unit == null || string.IsNullOrWhiteSpace(unit.Id))
                {
                    issues.Add(new ConfigurationIssue("CFG_REQUIRED", unitPath + ".Id", "Unit ID is required."));
                    continue;
                }

                if (!ids.Add(unit.Id))
                {
                    issues.Add(new ConfigurationIssue("CFG_DUPLICATE", unitPath + ".Id", $"Duplicate unit ID: {unit.Id}."));
                }

                if (unit.ResourceCost < 0)
                {
                    issues.Add(new ConfigurationIssue("CFG_RANGE", unitPath + ".ResourceCost", "Cost cannot be negative."));
                }

                if (unit.DeployCooldown < 0f)
                {
                    issues.Add(new ConfigurationIssue("CFG_RANGE", unitPath + ".DeployCooldown", "Cooldown cannot be negative."));
                }

                if (string.Equals(unit.Id, selectedUnitId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = unit;
                }
            }

            if (selected == null && !string.IsNullOrWhiteSpace(selectedUnitId))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_REFERENCE",
                    path + ".Units",
                    $"Configured test unit was not found: {selectedUnitId}."));
            }

            return selected;
        }

        private static LegacyEnemyDto ValidateAndFindEnemy(
            LegacyEnemiesDocumentDto document,
            string selectedEnemyId,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (document.Enemies == null || document.Enemies.Count == 0)
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Enemies", "Enemies cannot be empty."));
                return null;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LegacyEnemyDto selected = null;
            for (var index = 0; index < document.Enemies.Count; index++)
            {
                var enemy = document.Enemies[index];
                var enemyPath = $"{path}.Enemies[{index}]";
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Id))
                {
                    issues.Add(new ConfigurationIssue("CFG_REQUIRED", enemyPath + ".Id", "Enemy ID is required."));
                    continue;
                }

                if (!ids.Add(enemy.Id))
                {
                    issues.Add(new ConfigurationIssue("CFG_DUPLICATE", enemyPath + ".Id", $"Duplicate enemy ID: {enemy.Id}."));
                }

                if (enemy.Durability <= 0 || enemy.Attack < 0f || enemy.Speed < 0f ||
                    enemy.AttackInterval <= 0f || enemy.Range <= 0 || enemy.AssaultScoreReward < 0)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_RANGE",
                        enemyPath,
                        "Combat values must satisfy legacy runtime bounds."));
                }

                if (string.Equals(enemy.Id, selectedEnemyId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = enemy;
                }
            }

            if (selected == null && !string.IsNullOrWhiteSpace(selectedEnemyId))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_REFERENCE",
                    path + ".Enemies",
                    $"Configured test enemy was not found: {selectedEnemyId}."));
            }

            return selected;
        }

        private static DeploymentMode MapDeploymentMode(
            LegacyUnitDto unit,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (string.Equals(unit.Type, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return DeploymentMode.StandardUnit;
            }

            if (string.Equals(unit.Type, "Building", StringComparison.OrdinalIgnoreCase))
            {
                return DeploymentMode.Building;
            }

            if (string.Equals(unit.Type, "Support", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(unit.Effect, "TerrainBuild", StringComparison.OrdinalIgnoreCase)
                    ? DeploymentMode.TerrainBuild
                    : DeploymentMode.SupportEffect;
            }

            issues.Add(new ConfigurationIssue(
                "CFG_ENUM",
                path + ".Units." + unit.Id + ".Type",
                $"Unsupported legacy unit type: {unit.Type}."));
            return DeploymentMode.StandardUnit;
        }

        private static UnitFootprint MapFootprint(
            string legacyValue,
            string path,
            string unitId,
            ICollection<ConfigurationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(legacyValue) ||
                string.Equals(legacyValue, "SmallCell", StringComparison.OrdinalIgnoreCase))
            {
                return UnitFootprint.SmallCell;
            }

            if (string.Equals(legacyValue, "ControlBlock", StringComparison.OrdinalIgnoreCase))
            {
                return UnitFootprint.ControlBlock;
            }

            issues.Add(new ConfigurationIssue(
                "CFG_ENUM",
                path + ".Units." + unitId + ".FootprintType",
                $"Unsupported footprint: {legacyValue}."));
            return UnitFootprint.SmallCell;
        }
    }
}
