using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;

namespace CompanyWarRE.Infrastructure.Levels
{
    [Serializable]
    [DataContract]
    public sealed class FormalLevelDocumentDto
    {
        [DataMember(Name = "SchemaVersion")]
        public int SchemaVersion;
        [DataMember(Name = "Id")]
        public string Id;
        [DataMember(Name = "CoordinateSpace")]
        public string CoordinateSpace;
        [DataMember(Name = "Columns")]
        public int Columns;
        [DataMember(Name = "Rows")]
        public int Rows;
        [DataMember(Name = "InitialControlledRows")]
        public int InitialControlledRows;
        [DataMember(Name = "InitialResources")]
        public int InitialResources;
        [DataMember(Name = "FixedProductionIntervalSeconds")]
        public float FixedProductionIntervalSeconds;
        [DataMember(Name = "TransmitterProductionIntervalSeconds")]
        public float TransmitterProductionIntervalSeconds;
        [DataMember(Name = "TestUnitId")]
        public string TestUnitId;
        [DataMember(Name = "TestEnemyId")]
        public string TestEnemyId;
        [DataMember(Name = "EnemyWaveRandomSeed")]
        public int EnemyWaveRandomSeed;
        [DataMember(Name = "Stages")]
        public List<string> Stages;
        [DataMember(Name = "EnemyBuildings")]
        public List<LegacyBuildingPlacementDto> EnemyBuildings;
        [DataMember(Name = "Objective")]
        public FormalLevelObjectiveDto Objective;
        [DataMember(Name = "Environment")]
        public FormalLevelEnvironmentDto Environment;
    }

    [Serializable]
    [DataContract]
    public sealed class FormalLevelObjectiveDto
    {
        [DataMember(Name = "Mode")]
        public string Mode;
        [DataMember(Name = "RequiredAssaultScore")]
        public int RequiredAssaultScore;
        [DataMember(Name = "VictoryByEnemyBuildings")]
        public bool VictoryByEnemyBuildings;
    }

    [Serializable]
    [DataContract]
    public sealed class FormalLevelEnvironmentDto
    {
        [DataMember(Name = "EnvironmentId")]
        public string EnvironmentId;
        [DataMember(Name = "DecorRing")]
        public int DecorRing;
        [DataMember(Name = "OuterGroundSize")]
        public float OuterGroundSize;
        [DataMember(Name = "SkylineDistance")]
        public float SkylineDistance;
        [DataMember(Name = "SkylineDensity")]
        public float SkylineDensity;
        [DataMember(Name = "Seed")]
        public int Seed;
        [DataMember(Name = "SceneProps")]
        public List<FormalLevelScenePropDto> SceneProps;
    }

    [Serializable]
    [DataContract]
    public sealed class FormalLevelScenePropDto
    {
        [DataMember(Name = "PrefabId")]
        public string PrefabId;
        [DataMember(Name = "OffsetX")]
        public float OffsetX;
        [DataMember(Name = "OffsetZ")]
        public float OffsetZ;
        [DataMember(Name = "RotationY")]
        public float RotationY;
        [DataMember(Name = "Scale")]
        public float Scale;
    }

    public sealed class FormalLevelRuntimeMetadata
    {
        internal FormalLevelRuntimeMetadata(FormalLevelDocumentDto document)
        {
            SchemaVersion = document.SchemaVersion;
            LevelId = document.Id ?? string.Empty;
            CoordinateSpace = document.CoordinateSpace ?? string.Empty;
            SourceColumns = document.Columns;
            SourceRows = document.Rows;
            RuntimeColumns = FormalLevelCoordinateConverter.ToSmallCellDimension(document.Columns);
            RuntimeRows = FormalLevelCoordinateConverter.ToSmallCellDimension(document.Rows);
            InitialControlledSourceRows = document.InitialControlledRows;
            InitialControlledRuntimeRows = FormalLevelCoordinateConverter.ToSmallCellDimension(
                document.InitialControlledRows);
            Objective = document.Objective;
            Environment = document.Environment;
        }

        public int SchemaVersion { get; }
        public string LevelId { get; }
        public string CoordinateSpace { get; }
        public int SourceColumns { get; }
        public int SourceRows { get; }
        public int RuntimeColumns { get; }
        public int RuntimeRows { get; }
        public int InitialControlledSourceRows { get; }
        public int InitialControlledRuntimeRows { get; }
        public FormalLevelObjectiveDto Objective { get; }
        public FormalLevelEnvironmentDto Environment { get; }
    }

    public static class FormalLevelCoordinateConverter
    {
        public const string MacroCoordinateSpace = "MacroControlBlock3x3";
        public const int Scale = BattleGrid.ControlBlockSize;

        public static int ToSmallCellDimension(int macroDimension)
        {
            return Math.Max(0, macroDimension) * Scale;
        }

        public static int ToSmallCellCenter(int macroCoordinate)
        {
            return ((Math.Max(1, macroCoordinate) - 1) * Scale) + 2;
        }

        public static int ToSmallCellStart(int macroCoordinate)
        {
            return ((Math.Max(1, macroCoordinate) - 1) * Scale) + 1;
        }
    }

    public sealed class FormalLevelLoadResult
    {
        public FormalLevelLoadResult(
            FormalLevelRuntimeMetadata level,
            BattleSliceConfiguration configuration,
            IReadOnlyList<ConfigurationIssue> issues)
        {
            Level = level;
            Configuration = configuration;
            Issues = issues ?? Array.Empty<ConfigurationIssue>();
        }

        public FormalLevelRuntimeMetadata Level { get; }
        public BattleSliceConfiguration Configuration { get; }
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
        public bool Succeeded => Level != null && Configuration != null && Issues.Count == 0;
    }

    public sealed class FormalLevelBatchEntry
    {
        public FormalLevelBatchEntry(string documentKey, FormalLevelLoadResult result)
        {
            DocumentKey = documentKey ?? string.Empty;
            LevelId = result?.Level?.LevelId ?? string.Empty;
            Succeeded = result != null && result.Succeeded;
            Issues = result?.Issues ?? Array.Empty<ConfigurationIssue>();
        }

        public string DocumentKey { get; }
        public string LevelId { get; }
        public bool Succeeded { get; }
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
    }

    public sealed class FormalLevelBatchValidationReport
    {
        public FormalLevelBatchValidationReport(
            IReadOnlyList<FormalLevelBatchEntry> entries,
            IReadOnlyList<ConfigurationIssue> issues)
        {
            Entries = entries ?? Array.Empty<FormalLevelBatchEntry>();
            Issues = issues ?? Array.Empty<ConfigurationIssue>();
        }

        public IReadOnlyList<FormalLevelBatchEntry> Entries { get; }
        public IReadOnlyList<ConfigurationIssue> Issues { get; }
        public int ErrorCount => Issues.Count + Entries.Sum(entry => entry.Issues.Count);
        public bool Succeeded => Entries.Count > 0 && Entries.All(entry => entry.Succeeded) && Issues.Count == 0;
    }

    public sealed class FormalLevelConfigurationPipeline
    {
        public const int SupportedSchemaVersion = 1;
        public const string DestroyEnemyBuildingsObjective = "DestroyEnemyBuildings";

        private readonly IConfigurationTextSource _source;

        public FormalLevelConfigurationPipeline(IConfigurationTextSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public FormalLevelLoadResult Load(
            string unitsKey,
            string enemiesKey,
            string schedulesKey,
            string levelKey)
        {
            var issues = new List<ConfigurationIssue>();
            if (!_source.TryRead(unitsKey, out var unitsJson, out var unitsError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", unitsKey, unitsError));
            }

            if (!_source.TryRead(enemiesKey, out var enemiesJson, out var enemiesError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", enemiesKey, enemiesError));
            }

            if (!_source.TryRead(schedulesKey, out var schedulesJson, out var schedulesError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", schedulesKey, schedulesError));
            }

            if (!_source.TryRead(levelKey, out var levelJson, out var levelError))
            {
                issues.Add(new ConfigurationIssue("CFG_SOURCE", levelKey, levelError));
            }

            if (issues.Count > 0)
            {
                return new FormalLevelLoadResult(null, null, issues);
            }

            var document = Deserialize<FormalLevelDocumentDto>(levelJson, levelKey, issues);
            if (document == null)
            {
                return new FormalLevelLoadResult(null, null, issues);
            }

            ValidateDocument(document, levelKey, issues);
            var metadata = issues.Count == 0 ? new FormalLevelRuntimeMetadata(document) : null;
            if (issues.Count > 0)
            {
                return new FormalLevelLoadResult(metadata, null, issues);
            }

            var settingsKey = levelKey + "#RuntimeSettings";
            var legacyLevelKey = levelKey + "#LegacyLevel";
            var settings = BuildRuntimeSettings(document);
            var legacyLevel = BuildLegacyLevel(document);
            var documents = new Dictionary<string, string>
            {
                [unitsKey] = unitsJson,
                [enemiesKey] = enemiesJson,
                [schedulesKey] = schedulesJson,
                [settingsKey] = Serialize(settings),
                [legacyLevelKey] = Serialize(legacyLevel)
            };
            var legacy = new LegacyBattleSliceConfigurationProvider(
                    new DictionaryConfigurationTextSource(documents))
                .Load(unitsKey, enemiesKey, settingsKey, schedulesKey, legacyLevelKey);
            issues.AddRange(legacy.Issues.Select(issue => RemapIssue(
                issue,
                settingsKey,
                legacyLevelKey,
                levelKey)));
            return issues.Count == 0
                ? new FormalLevelLoadResult(metadata, legacy.Configuration, issues)
                : new FormalLevelLoadResult(metadata, null, issues);
        }

        public FormalLevelBatchValidationReport ValidateBatch(
            string unitsKey,
            string enemiesKey,
            string schedulesKey,
            IEnumerable<string> levelKeys)
        {
            var entries = new List<FormalLevelBatchEntry>();
            var globalIssues = new List<ConfigurationIssue>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var levelKey in (levelKeys ?? Array.Empty<string>()).Where(key => !string.IsNullOrWhiteSpace(key)))
            {
                var result = Load(unitsKey, enemiesKey, schedulesKey, levelKey);
                entries.Add(new FormalLevelBatchEntry(levelKey, result));
                if (result.Level != null && !ids.Add(result.Level.LevelId))
                {
                    globalIssues.Add(new ConfigurationIssue(
                        "CFG_DUPLICATE",
                        levelKey + ".Id",
                        "Duplicate formal level ID in batch: " + result.Level.LevelId + "."));
                }
            }

            if (entries.Count == 0)
            {
                globalIssues.Add(new ConfigurationIssue(
                    "CFG_REQUIRED",
                    "FormalLevels",
                    "At least one formal level document is required."));
            }

            return new FormalLevelBatchValidationReport(entries, globalIssues);
        }

        private static void ValidateDocument(
            FormalLevelDocumentDto document,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (document.SchemaVersion != SupportedSchemaVersion)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_SCHEMA",
                    path + ".SchemaVersion",
                    $"Expected {SupportedSchemaVersion}, got {document.SchemaVersion}."));
            }

            if (string.IsNullOrWhiteSpace(document.Id))
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Id", "Level ID is required."));
            }

            if (!string.Equals(
                    document.CoordinateSpace,
                    FormalLevelCoordinateConverter.MacroCoordinateSpace,
                    StringComparison.Ordinal))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_ENUM",
                    path + ".CoordinateSpace",
                    "Expected " + FormalLevelCoordinateConverter.MacroCoordinateSpace + "."));
            }

            if (document.Columns <= 0 || document.Rows <= 0)
            {
                issues.Add(new ConfigurationIssue("CFG_RANGE", path, "Macro-grid dimensions must be positive."));
            }

            if (document.InitialControlledRows <= 0 || document.InitialControlledRows >= document.Rows)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".InitialControlledRows",
                    "Initial controlled macro rows must be positive and leave enemy territory."));
            }

            if (document.InitialResources < 0 ||
                document.FixedProductionIntervalSeconds <= 0f ||
                document.TransmitterProductionIntervalSeconds <= 0f)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path + ".Runtime",
                    "Resources cannot be negative and production intervals must be positive."));
            }

            if (string.IsNullOrWhiteSpace(document.TestUnitId) || string.IsNullOrWhiteSpace(document.TestEnemyId))
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_REQUIRED",
                    path + ".Runtime",
                    "TestUnitId and TestEnemyId are required compatibility fallbacks."));
            }

            if (document.Stages == null || document.Stages.Count == 0)
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Stages", "At least one stage is required."));
            }
            else if (document.Stages.Any(string.IsNullOrWhiteSpace) ||
                     document.Stages.Distinct(StringComparer.OrdinalIgnoreCase).Count() != document.Stages.Count)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_DUPLICATE",
                    path + ".Stages",
                    "Stage names must be non-empty and unique."));
            }

            if (document.Objective == null)
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path + ".Objective", "Objective is required."));
            }
            else
            {
                if (!string.Equals(
                        document.Objective.Mode,
                        DestroyEnemyBuildingsObjective,
                        StringComparison.Ordinal))
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_ENUM",
                        path + ".Objective.Mode",
                        "Unsupported objective mode: " + document.Objective.Mode + "."));
                }

                if (!document.Objective.VictoryByEnemyBuildings || document.Objective.RequiredAssaultScore < 0)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_RANGE",
                        path + ".Objective",
                        "Building objective must enable building victory and use a non-negative assault score."));
                }
            }

            if (document.EnemyBuildings == null || document.EnemyBuildings.Count == 0)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_REQUIRED",
                    path + ".EnemyBuildings",
                    "Building objective requires enemy buildings."));
            }

            ValidateEnvironment(document.Environment, path + ".Environment", issues);
        }

        private static void ValidateEnvironment(
            FormalLevelEnvironmentDto environment,
            string path,
            ICollection<ConfigurationIssue> issues)
        {
            if (environment == null || string.IsNullOrWhiteSpace(environment.EnvironmentId))
            {
                issues.Add(new ConfigurationIssue("CFG_REQUIRED", path, "EnvironmentId is required."));
                return;
            }

            if (environment.DecorRing < 0 || environment.OuterGroundSize < 0f ||
                environment.SkylineDistance < 0f || environment.SkylineDensity < 0f ||
                environment.SkylineDensity > 1f)
            {
                issues.Add(new ConfigurationIssue(
                    "CFG_RANGE",
                    path,
                    "Environment distances must be non-negative and skyline density must be between zero and one."));
            }

            for (var index = 0; index < (environment.SceneProps?.Count ?? 0); index++)
            {
                var prop = environment.SceneProps[index];
                if (prop == null || string.IsNullOrWhiteSpace(prop.PrefabId) || prop.Scale <= 0f)
                {
                    issues.Add(new ConfigurationIssue(
                        "CFG_REQUIRED",
                        $"{path}.SceneProps[{index}]",
                        "Scene prop requires a PrefabId and positive scale."));
                }
            }
        }

        private static BattleSliceSettingsDto BuildRuntimeSettings(FormalLevelDocumentDto document)
        {
            var columns = FormalLevelCoordinateConverter.ToSmallCellDimension(document.Columns);
            var rows = FormalLevelCoordinateConverter.ToSmallCellDimension(document.Rows);
            return new BattleSliceSettingsDto
            {
                SchemaVersion = LegacyBattleSliceConfigurationProvider.SupportedSettingsSchemaVersion,
                Columns = columns,
                Rows = rows,
                ControlledRows = FormalLevelCoordinateConverter.ToSmallCellDimension(document.InitialControlledRows),
                InitialResources = document.InitialResources,
                FixedProductionIntervalSeconds = document.FixedProductionIntervalSeconds,
                TransmitterProductionIntervalSeconds = document.TransmitterProductionIntervalSeconds,
                TransmitterColumn = 2,
                TransmitterRow = 2,
                TransmitterAmount = 0,
                TestUnitId = document.TestUnitId,
                TestEnemyId = document.TestEnemyId,
                EnemySpawnColumn = Math.Min(2, columns),
                EnemySpawnRow = rows,
                EnemySpawnColumns = Enumerable.Range(1, columns).ToList(),
                EnemyWaveRandomSeed = document.EnemyWaveRandomSeed == 0 ? 17 : document.EnemyWaveRandomSeed
            };
        }

        private static LegacyLevelDto BuildLegacyLevel(FormalLevelDocumentDto document)
        {
            return new LegacyLevelDto
            {
                Id = document.Id,
                Columns = document.Columns,
                Rows = document.Rows,
                RequiredAssaultScore = document.Objective.RequiredAssaultScore,
                Stages = document.Stages,
                EnemyBuildings = document.EnemyBuildings
            };
        }

        private static ConfigurationIssue RemapIssue(
            ConfigurationIssue issue,
            string settingsKey,
            string legacyLevelKey,
            string levelKey)
        {
            var path = issue.Path
                .Replace(settingsKey, levelKey + ".Runtime")
                .Replace(legacyLevelKey, levelKey);
            return new ConfigurationIssue(issue.Code, path, issue.Message);
        }

        private static T Deserialize<T>(
            string json,
            string path,
            ICollection<ConfigurationIssue> issues)
            where T : class
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var result = serializer.ReadObject(stream) as T;
                    if (result == null)
                    {
                        issues.Add(new ConfigurationIssue("CFG_PARSE", path, "JSON produced no object."));
                    }

                    return result;
                }
            }
            catch (Exception exception)
            {
                issues.Add(new ConfigurationIssue("CFG_PARSE", path, exception.Message));
                return null;
            }
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
