using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
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
        [DataMember(Name = "EnemySpawnColumns")]
        public List<int> EnemySpawnColumns;
        [DataMember(Name = "EnemyWaveRandomSeed")]
        public int EnemyWaveRandomSeed;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacySpawnSchedulesDto
    {
        [DataMember(Name = "Stages")]
        public List<LegacySpawnStageDto> Stages;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacySpawnStageDto
    {
        [DataMember(Name = "Name")]
        public string Name;
        [DataMember(Name = "Period")]
        public string Period;
        [DataMember(Name = "Rate")]
        public string Rate;
        [DataMember(Name = "PerWave")]
        public int PerWave;
        [DataMember(Name = "Types")]
        public List<LegacySpawnTypeWeightDto> Types;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacySpawnTypeWeightDto
    {
        [DataMember(Name = "Id")]
        public string Id;
        [DataMember(Name = "Weight")]
        public int Weight;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacyLevelDto
    {
        [DataMember(Name = "Id")]
        public string Id;
        [DataMember(Name = "Columns")]
        public int Columns;
        [DataMember(Name = "Rows")]
        public int Rows;
        [DataMember(Name = "RequiredAssaultScore")]
        public int RequiredAssaultScore;
        [DataMember(Name = "Stages")]
        public List<string> Stages;
        [DataMember(Name = "EnemyBuildings")]
        public List<LegacyBuildingPlacementDto> EnemyBuildings;
    }

    [Serializable]
    [DataContract]
    public sealed class LegacyBuildingPlacementDto
    {
        [DataMember(Name = "Type")]
        public string Type;
        [DataMember(Name = "Col")]
        public int Column;
        [DataMember(Name = "Row")]
        public int Row;
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

    public sealed class LegacyCombatCatalog
    {
        public LegacyCombatCatalog(
            IReadOnlyDictionary<string, UnitDefinition> units,
            IReadOnlyDictionary<string, CombatantDefinition> allies,
            IReadOnlyDictionary<string, CombatantDefinition> enemies)
        {
            Units = units ?? throw new ArgumentNullException(nameof(units));
            Allies = allies ?? throw new ArgumentNullException(nameof(allies));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        }

        public IReadOnlyDictionary<string, UnitDefinition> Units { get; }
        public IReadOnlyDictionary<string, CombatantDefinition> Allies { get; }
        public IReadOnlyDictionary<string, CombatantDefinition> Enemies { get; }
    }

    public sealed class LegacyCombatCatalogLoadResult
    {
        public LegacyCombatCatalogLoadResult(
            LegacyCombatCatalog catalog,
            IReadOnlyList<ConfigurationIssue> issues)
        {
            Catalog = catalog;
            Issues = issues ?? Array.Empty<ConfigurationIssue>();
        }

        public LegacyCombatCatalog Catalog { get; }
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
        public bool Succeeded => Catalog != null && Issues.Count == 0;
    }

    public sealed class LegacyBattleSliceConfigurationProvider
    {
        public const int SupportedSettingsSchemaVersion = 1;

        private readonly IConfigurationTextSource _source;

        public LegacyBattleSliceConfigurationProvider(IConfigurationTextSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public LegacyCombatCatalogLoadResult LoadCatalog(string unitsKey, string enemiesKey)
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

            if (issues.Count > 0)
            {
                return new LegacyCombatCatalogLoadResult(null, issues);
            }

            var units = Parse<LegacyUnitsDocumentDto>(unitsJson, unitsKey, issues);
            var enemies = Parse<LegacyEnemiesDocumentDto>(enemiesJson, enemiesKey, issues);
            if (units == null || enemies == null)
            {
                return new LegacyCombatCatalogLoadResult(null, issues);
            }

            ValidateAndFindUnit(units, null, unitsKey, issues);
            ValidateAndFindEnemy(enemies, null, enemiesKey, issues);
            var mappedUnits = MapUnitDefinitions(units, unitsKey, issues);
            var mappedAllies = MapAllyCombatants(units);
            var mappedEnemies = enemies.Enemies == null
                ? new Dictionary<string, CombatantDefinition>(StringComparer.OrdinalIgnoreCase)
                : enemies.Enemies
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(MapEnemyCombatant)
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            return issues.Count == 0
                ? new LegacyCombatCatalogLoadResult(
                    new LegacyCombatCatalog(mappedUnits, mappedAllies, mappedEnemies),
                    issues)
                : new LegacyCombatCatalogLoadResult(null, issues);
        }

        public BattleSliceConfigurationLoadResult Load(
            string unitsKey,
            string enemiesKey,
            string settingsKey,
            string spawnSchedulesKey = null,
            string levelKey = null)
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

            string spawnSchedulesJson = null;
            if (!string.IsNullOrWhiteSpace(spawnSchedulesKey) &&
                !_source.TryRead(spawnSchedulesKey, out spawnSchedulesJson, out var spawnSchedulesReadError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", spawnSchedulesKey, spawnSchedulesReadError));
            }

            string levelJson = null;
            if (!string.IsNullOrWhiteSpace(levelKey) &&
                !_source.TryRead(levelKey, out levelJson, out var levelReadError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", levelKey, levelReadError));
            }

            if (issues.Count > 0)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            var units = Parse<LegacyUnitsDocumentDto>(unitsJson, unitsKey, issues);
            var enemies = Parse<LegacyEnemiesDocumentDto>(enemiesJson, enemiesKey, issues);
            var settings = Parse<BattleSliceSettingsDto>(settingsJson, settingsKey, issues);
            var spawnSchedules = string.IsNullOrWhiteSpace(spawnSchedulesKey)
                ? null
                : Parse<LegacySpawnSchedulesDto>(spawnSchedulesJson, spawnSchedulesKey, issues);
            var level = string.IsNullOrWhiteSpace(levelKey)
                ? null
                : Parse<LegacyLevelDto>(levelJson, levelKey, issues);
            if (units == null || enemies == null || settings == null ||
                (!string.IsNullOrWhiteSpace(spawnSchedulesKey) && spawnSchedules == null) ||
                (!string.IsNullOrWhiteSpace(levelKey) && level == null))
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            ValidateSettings(settings, settingsKey, issues);
            var selectedUnit = ValidateAndFindUnit(units, settings.TestUnitId, unitsKey, issues);
            var selectedEnemy = ValidateAndFindEnemy(enemies, settings.TestEnemyId, enemiesKey, issues);
            var unitDefinitions = MapUnitDefinitions(units, unitsKey, issues);
            var allyDefinitions = MapAllyCombatants(units);
            var enemyDefinitions = enemies.Enemies == null
                ? new List<CombatantDefinition>()
                : enemies.Enemies
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(MapEnemyCombatant)
                    .ToList();
            var enemyWaveStages = spawnSchedules == null
                ? null
                : MapSpawnStages(
                    spawnSchedules,
                    spawnSchedulesKey,
                    enemyDefinitions,
                    issues,
                    level?.Stages);
            var enemyBuildings = level == null
                ? Array.Empty<EnemyBuildingPlacement>()
                : MapEnemyBuildings(level, levelKey, settings, enemyDefinitions, issues);
            if (issues.Count > 0 || selectedUnit == null || selectedEnemy == null)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            if (issues.Count > 0)
            {
                return new BattleSliceConfigurationLoadResult(null, issues);
            }

            var unit = unitDefinitions[selectedUnit.Id];
            var controlledRows = ResolveControlledRows(settings);
            var allyCombatant = allyDefinitions[selectedUnit.Id];
            var enemyCombatant = MapEnemyCombatant(selectedEnemy);
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
                new GridPosition(settings.EnemySpawnColumn, settings.EnemySpawnRow),
                enemyWaveStages,
                enemyDefinitions,
                settings.EnemySpawnColumns,
                settings.EnemyWaveRandomSeed == 0 ? 17 : settings.EnemyWaveRandomSeed,
                enemyBuildings,
                level?.RequiredAssaultScore ?? 0,
                level != null,
                level != null,
                unitDefinitions.Values.ToArray(),
                allyDefinitions.Values.ToArray());
            return new BattleSliceConfigurationLoadResult(configuration, issues);
        }

        private static IReadOnlyList<EnemyBuildingPlacement> MapEnemyBuildings(
            LegacyLevelDto level,
            string path,
            BattleSliceSettingsDto settings,
            IReadOnlyCollection<CombatantDefinition> enemies,
            ICollection<ConfigurationIssue> issues)
        {
            var result = new List<EnemyBuildingPlacement>();
            var coordinateScale = ResolveLevelCoordinateScale(level, settings);
            if (coordinateScale == 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_CONFLICT",
                    path,
                    "Level dimensions must either match the small-cell grid or expand to it by the 3x3 control-block scale."));
            }

            if (level.RequiredAssaultScore < 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".RequiredAssaultScore",
                    "RequiredAssaultScore cannot be negative."));
            }

            if (level.EnemyBuildings == null || level.EnemyBuildings.Count == 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_REQUIRED",
                    path + ".EnemyBuildings",
                    "Building-victory levels require at least one enemy building."));
                return result;
            }

            var definitions = enemies
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var occupied = new HashSet<GridPosition>();
            for (var index = 0; index < level.EnemyBuildings.Count; index++)
            {
                var building = level.EnemyBuildings[index];
                var buildingPath = $"{path}.EnemyBuildings[{index}]";
                if (building == null || string.IsNullOrWhiteSpace(building.Type))
                {
                    issues.Add(new ConfigurationIssue("CFG_REQUIRED", buildingPath + ".Type", "Building type is required."));
                    continue;
                }

                if (!definitions.TryGetValue(building.Type, out var definition) || !definition.IsBuilding)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_REFERENCE",
                        buildingPath + ".Type",
                        "Enemy building definition was not found: " + building.Type + "."));
                    continue;
                }

                if (building.Column < 1 || building.Column > level.Columns ||
                    building.Row < 1 || building.Row > level.Rows)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_RANGE",
                        buildingPath,
                        "Building position must be inside the source level grid."));
                    continue;
                }

                if (coordinateScale == 0)
                {
                    continue;
                }

                var position = new GridPosition(
                    MapLevelCoordinate(building.Column, coordinateScale),
                    MapLevelCoordinate(building.Row, coordinateScale));

                var startColumn = GetControlBlockStart(position.Column);
                var startRow = GetControlBlockStart(position.Row);
                if (startColumn + BattleGrid.ControlBlockSize - 1 > settings.Columns ||
                    startRow + BattleGrid.ControlBlockSize - 1 > settings.Rows)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_RANGE",
                        buildingPath,
                        "The building's 3x3 control-block footprint must fit inside the grid."));
                    continue;
                }

                var footprint = Enumerable.Range(startColumn, BattleGrid.ControlBlockSize)
                    .SelectMany(column => Enumerable.Range(startRow, BattleGrid.ControlBlockSize)
                        .Select(row => new GridPosition(column, row)))
                    .ToArray();
                if (footprint.Any(cell => occupied.Contains(cell)))
                {
                    issues.Add(new ConfigurationIssue("CFG_DUPLICATE", buildingPath, "Building footprints cannot overlap."));
                    continue;
                }

                foreach (var cell in footprint)
                {
                    occupied.Add(cell);
                }

                result.Add(new EnemyBuildingPlacement(building.Type, position));
            }

            return result;
        }

        private static int ResolveLevelCoordinateScale(
            LegacyLevelDto level,
            BattleSliceSettingsDto settings)
        {
            if (level.Columns == settings.Columns && level.Rows == settings.Rows)
            {
                return 1;
            }

            return level.Columns > 0 && level.Rows > 0 &&
                   level.Columns * BattleGrid.ControlBlockSize == settings.Columns &&
                   level.Rows * BattleGrid.ControlBlockSize == settings.Rows
                ? BattleGrid.ControlBlockSize
                : 0;
        }

        private static int MapLevelCoordinate(int sourceCoordinate, int coordinateScale)
        {
            return coordinateScale == 1
                ? sourceCoordinate
                : ((Math.Max(1, sourceCoordinate) - 1) * coordinateScale) +
                  (coordinateScale / 2) + 1;
        }

        private static int GetControlBlockStart(int cellIndex)
        {
            return ((Math.Max(1, cellIndex) - 1) / BattleGrid.ControlBlockSize) *
                   BattleGrid.ControlBlockSize + 1;
        }

        private static CombatantDefinition MapEnemyCombatant(LegacyEnemyDto enemy)
        {
            return new CombatantDefinition(
                enemy.Id,
                enemy.Type,
                enemy.Durability,
                enemy.Attack,
                enemy.Speed,
                enemy.AttackInterval,
                enemy.Range,
                enemy.AssaultScoreReward,
                string.Empty,
                enemy.Name);
        }

        private static IReadOnlyDictionary<string, UnitDefinition> MapUnitDefinitions(
            LegacyUnitsDocumentDto document,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            var result = new Dictionary<string, UnitDefinition>(StringComparer.OrdinalIgnoreCase);
            if (document?.Units == null)
            {
                return result;
            }

            foreach (var unit in document.Units.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id)))
            {
                var mode = MapDeploymentMode(unit, path, issues);
                var footprint = mode == DeploymentMode.Building
                    ? UnitFootprint.ControlBlock
                    : MapFootprint(unit.FootprintType, path, unit.Id, issues);
                result[unit.Id] = new UnitDefinition(
                    unit.Id,
                    unit.ResourceCost,
                    unit.DeployCooldown,
                    mode,
                    footprint,
                    unit.Name,
                    unit.Effect,
                    unit.ResourceRate,
                    unit.ScoreRate,
                    unit.CanDeployOutside);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, CombatantDefinition> MapAllyCombatants(
            LegacyUnitsDocumentDto document)
        {
            if (document?.Units == null)
            {
                return new Dictionary<string, CombatantDefinition>(StringComparer.OrdinalIgnoreCase);
            }

            return document.Units
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .Select(unit => new CombatantDefinition(
                    unit.Id,
                    unit.Type,
                    unit.Durability,
                    unit.Attack,
                    unit.Speed,
                    unit.AttackInterval,
                    unit.Range,
                    0,
                    unit.Effect,
                    unit.Name))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<EnemyWaveStage> MapSpawnStages(
            LegacySpawnSchedulesDto document,
            string path,
            IReadOnlyCollection<CombatantDefinition> enemies,
            ICollection<ConfigurationIssue> issues,
            IReadOnlyList<string> selectedStageNames = null)
        {
            var result = new List<EnemyWaveStage>();
            if (document.Stages == null || document.Stages.Count == 0)
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Stages", "Spawn stages cannot be empty."));
                return result;
            }

            var configuredStages = SelectLevelStages(document.Stages, selectedStageNames, path, issues);
            var enemyIds = new HashSet<string>(enemies.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < configuredStages.Count; index++)
            {
                var stage = configuredStages[index];
                var stagePath = $"{path}.Stages[{index}]";
                if (stage == null || string.IsNullOrWhiteSpace(stage.Name))
                {
                    issues.Add(new ConfigurationIssue("CFG_REQUIRED", stagePath + ".Name", "Stage name is required."));
                    continue;
                }

                if (!TryParsePeriodSeconds(stage.Period, out var duration))
                {
                    issues.Add(new ConfigurationIssue("CFG_FORMAT", stagePath + ".Period", "Expected mm:ss-mm:ss."));
                }

                if (!TryParseRate(stage.Rate, out var rateNumerator, out var interval))
                {
                    issues.Add(new ConfigurationIssue("CFG_FORMAT", stagePath + ".Rate", "Expected count/seconds sec."));
                }

                var weights = new List<EnemySpawnWeight>();
                if (stage.Types == null || stage.Types.Count == 0)
                {
                    issues.Add(new ConfigurationIssue("CFG_REQUIRED", stagePath + ".Types", "Enemy weights cannot be empty."));
                }
                else
                {
                    for (var typeIndex = 0; typeIndex < stage.Types.Count; typeIndex++)
                    {
                        var type = stage.Types[typeIndex];
                        var typePath = $"{stagePath}.Types[{typeIndex}]";
                        if (type == null || string.IsNullOrWhiteSpace(type.Id))
                        {
                            issues.Add(new ConfigurationIssue("CFG_REQUIRED", typePath + ".Id", "Enemy ID is required."));
                            continue;
                        }

                        if (!enemyIds.Contains(type.Id))
                        {
                            issues.Add(new ConfigurationIssue("CFG_REFERENCE", typePath + ".Id", "Enemy definition was not found: " + type.Id + "."));
                        }

                        if (type.Weight < 0)
                        {
                            issues.Add(new ConfigurationIssue("CFG_RANGE", typePath + ".Weight", "Weight cannot be negative."));
                        }

                        weights.Add(new EnemySpawnWeight(type.Id, type.Weight));
                    }
                }

                if (duration > 0d && interval > 0d && weights.Count > 0)
                {
                    result.Add(new EnemyWaveStage(
                        stage.Name,
                        duration,
                        interval,
                        stage.PerWave > 0 ? stage.PerWave : Math.Max(1, rateNumerator),
                        weights));
                }
            }

            return result;
        }

        private static IReadOnlyList<LegacySpawnStageDto> SelectLevelStages(
            IReadOnlyList<LegacySpawnStageDto> allStages,
            IReadOnlyList<string> selectedStageNames,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (selectedStageNames == null || selectedStageNames.Count == 0)
            {
                return allStages.ToArray();
            }

            var byName = allStages
                .Where(stage => stage != null && !string.IsNullOrWhiteSpace(stage.Name))
                .GroupBy(stage => stage.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var result = new List<LegacySpawnStageDto>();
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < selectedStageNames.Count; index++)
            {
                var stageName = selectedStageNames[index];
                if (string.IsNullOrWhiteSpace(stageName) || !selected.Add(stageName))
                {
                    continue;
                }

                if (!byName.TryGetValue(stageName, out var stage))
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_REFERENCE",
                        path + ".Stages",
                        "Level stage was not found in spawn schedules: " + stageName + "."));
                    continue;
                }

                result.Add(stage);
            }

            return result;
        }

        private static bool TryParseRate(string value, out int numerator, out double intervalSeconds)
        {
            numerator = 0;
            intervalSeconds = 0d;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var slash = value.IndexOf('/');
            if (slash <= 0 || !int.TryParse(value.Substring(0, slash).Trim(), out numerator))
            {
                return false;
            }

            var denominator = value.Substring(slash + 1).Trim().Split(' ')[0];
            return numerator > 0 &&
                   double.TryParse(denominator, NumberStyles.Float, CultureInfo.InvariantCulture, out intervalSeconds) &&
                   intervalSeconds > 0d;
        }

        private static bool TryParsePeriodSeconds(string value, out double durationSeconds)
        {
            durationSeconds = 0d;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('-');
            if (parts.Length != 2 || !TryParseClock(parts[0], out var start) || !TryParseClock(parts[1], out var end) || end < start)
            {
                return false;
            }

            durationSeconds = (end - start) + 1d;
            return true;
        }

        private static bool TryParseClock(string value, out int seconds)
        {
            seconds = 0;
            var parts = value.Trim().Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes) || !int.TryParse(parts[1], out var remainder) ||
                minutes < 0 || remainder < 0 || remainder > 59)
            {
                return false;
            }

            seconds = (minutes * 60) + remainder;
            return true;
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

            if (settings.EnemySpawnColumns != null &&
                settings.EnemySpawnColumns.Any(column => column < 1 || column > settings.Columns))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".EnemySpawnColumns",
                    "Every enemy spawn column must be inside the grid."));
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

                var isBuilding = !string.IsNullOrWhiteSpace(enemy.Type) &&
                                 enemy.Type.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0;
                if (enemy.Durability <= 0 || enemy.Attack < 0f || enemy.Speed < 0f ||
                    (!isBuilding && (enemy.AttackInterval <= 0f || enemy.Range <= 0)) ||
                    enemy.AssaultScoreReward < 0)
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
            if (string.Equals(unit.Effect, "Stealth", StringComparison.OrdinalIgnoreCase))
            {
                return DeploymentMode.StandardUnit;
            }

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
