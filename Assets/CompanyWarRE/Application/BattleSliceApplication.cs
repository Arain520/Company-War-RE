using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Domain;
using QFramework;

namespace CompanyWarRE.Application
{
    public sealed class BattleSliceArchitecture : Architecture<BattleSliceArchitecture>
    {
        protected override void Init()
        {
            RegisterModel(new BattleSliceModel());
        }
    }

    public sealed class BattleSliceCellSnapshot
    {
        public BattleSliceCellSnapshot(
            GridPosition position,
            bool isOwned,
            bool isPolluted,
            bool isBlockedByBuilding,
            int occupantCount)
        {
            Position = position;
            IsOwned = isOwned;
            IsPolluted = isPolluted;
            IsBlockedByBuilding = isBlockedByBuilding;
            OccupantCount = occupantCount;
        }

        public GridPosition Position { get; }
        public bool IsOwned { get; }
        public bool IsPolluted { get; }
        public bool IsBlockedByBuilding { get; }
        public int OccupantCount { get; }
    }

    public sealed class BattleSliceCombatantSnapshot
    {
        public BattleSliceCombatantSnapshot(CombatActorSnapshot actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            ActorId = actor.ActorId;
            TemplateId = actor.TemplateId;
            Team = actor.Team;
            Column = actor.Column;
            LanePosition = actor.LanePosition;
            HitPoints = actor.HitPoints;
            MaximumHitPoints = actor.MaximumHitPoints;
            AttackProgress = actor.AttackProgress;
            IsAlive = actor.IsAlive;
            IsBuilding = actor.IsBuilding;
            FootprintColumns = actor.FootprintColumns;
            FootprintRows = actor.FootprintRows;
            FootprintStartColumn = actor.FootprintStartColumn;
            FootprintEndColumn = actor.FootprintEndColumn;
            FootprintStartRow = actor.FootprintStartRow;
            FootprintEndRow = actor.FootprintEndRow;
        }

        public string ActorId { get; }
        public string TemplateId { get; }
        public Team Team { get; }
        public int Column { get; }
        public double LanePosition { get; }
        public double HitPoints { get; }
        public double MaximumHitPoints { get; }
        public double AttackProgress { get; }
        public bool IsAlive { get; }
        public bool IsBuilding { get; }
        public int FootprintColumns { get; }
        public int FootprintRows { get; }
        public int FootprintStartColumn { get; }
        public int FootprintEndColumn { get; }
        public int FootprintStartRow { get; }
        public int FootprintEndRow { get; }
    }

    public sealed class BattleSliceSnapshot
    {
        public BattleSliceSnapshot(
            int columns,
            int rows,
            int resources,
            double elapsedSeconds,
            double remainingCooldown,
            string unitId,
            int unitResourceCost,
            IReadOnlyList<BattleSliceCellSnapshot> cells,
            IReadOnlyList<BattleSliceCombatantSnapshot> combatants,
            IReadOnlyList<CombatEvent> combatEvents,
            string currentWaveStage,
            int waveIndex,
            IReadOnlyList<EnemyWaveSpawn> enemySpawns,
            BattleState battleState,
            int assaultScore,
            int requiredAssaultScore,
            int enemyBuildingCount,
            int validSpawnPointCount)
        {
            Columns = columns;
            Rows = rows;
            Resources = resources;
            ElapsedSeconds = elapsedSeconds;
            RemainingCooldown = remainingCooldown;
            UnitId = unitId;
            UnitResourceCost = unitResourceCost;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            Combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            CombatEvents = combatEvents ?? throw new ArgumentNullException(nameof(combatEvents));
            CurrentWaveStage = currentWaveStage ?? string.Empty;
            WaveIndex = waveIndex;
            EnemySpawns = enemySpawns ?? throw new ArgumentNullException(nameof(enemySpawns));
            BattleState = battleState;
            AssaultScore = assaultScore;
            RequiredAssaultScore = requiredAssaultScore;
            EnemyBuildingCount = enemyBuildingCount;
            ValidSpawnPointCount = validSpawnPointCount;
        }

        public int Columns { get; }
        public int Rows { get; }
        public int Resources { get; }
        public double ElapsedSeconds { get; }
        public double RemainingCooldown { get; }
        public string UnitId { get; }
        public int UnitResourceCost { get; }
        public IReadOnlyList<BattleSliceCellSnapshot> Cells { get; }
        public IReadOnlyList<BattleSliceCombatantSnapshot> Combatants { get; }
        public IReadOnlyList<CombatEvent> CombatEvents { get; }
        public string CurrentWaveStage { get; }
        public int WaveIndex { get; }
        public IReadOnlyList<EnemyWaveSpawn> EnemySpawns { get; }
        public BattleState BattleState { get; }
        public int AssaultScore { get; }
        public int RequiredAssaultScore { get; }
        public int EnemyBuildingCount { get; }
        public int ValidSpawnPointCount { get; }
    }

    public sealed class BattleSliceDeploymentResponse
    {
        public BattleSliceDeploymentResponse(bool succeeded, DeploymentFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public DeploymentFailure Failure { get; }
    }

    public sealed class BattleSliceConfiguration
    {
        public BattleSliceConfiguration(
            int columns,
            int rows,
            int controlledRows,
            int initialResources,
            double fixedProductionIntervalSeconds,
            double transmitterProductionIntervalSeconds,
            GridPosition transmitterPosition,
            int transmitterAmount,
            UnitDefinition testUnit,
            CombatantDefinition allyCombatant,
            CombatantDefinition enemyCombatant,
            GridPosition enemySpawnPosition,
            IReadOnlyList<EnemyWaveStage> enemyWaveStages = null,
            IReadOnlyList<CombatantDefinition> enemyCombatants = null,
            IReadOnlyList<int> enemySpawnColumns = null,
            int enemyWaveRandomSeed = 17,
            IReadOnlyList<EnemyBuildingPlacement> enemyBuildings = null,
            int requiredAssaultScore = 0,
            bool victoryByEnemyBuildings = false,
            bool enableBattleOutcomes = false)
        {
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }

            if (controlledRows <= 0 || controlledRows > rows)
            {
                throw new ArgumentOutOfRangeException(nameof(controlledRows));
            }

            Columns = columns;
            Rows = rows;
            ControlledRows = controlledRows;
            InitialResources = Math.Max(0, initialResources);
            FixedProductionIntervalSeconds = Math.Max(0.01d, fixedProductionIntervalSeconds);
            TransmitterProductionIntervalSeconds = Math.Max(0.01d, transmitterProductionIntervalSeconds);
            TransmitterPosition = transmitterPosition;
            TransmitterAmount = Math.Max(0, transmitterAmount);
            TestUnit = testUnit ?? throw new ArgumentNullException(nameof(testUnit));
            AllyCombatant = allyCombatant ?? throw new ArgumentNullException(nameof(allyCombatant));
            EnemyCombatant = enemyCombatant ?? throw new ArgumentNullException(nameof(enemyCombatant));
            if (!string.Equals(TestUnit.Id, AllyCombatant.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Deployment and ally combat definitions must use the same ID.");
            }

            if (enemySpawnPosition.Column < 1 || enemySpawnPosition.Column > columns ||
                enemySpawnPosition.Row < 1 || enemySpawnPosition.Row > rows)
            {
                throw new ArgumentOutOfRangeException(nameof(enemySpawnPosition));
            }

            if (enemySpawnPosition.Row <= controlledRows)
            {
                throw new ArgumentException(
                    "Enemy spawn must be outside ally-controlled rows.",
                    nameof(enemySpawnPosition));
            }

            EnemySpawnPosition = enemySpawnPosition;
            EnemyWaveStages = enemyWaveStages == null || enemyWaveStages.Count == 0
                ? new[]
                {
                    new EnemyWaveStage(
                        "CompatibilityStage",
                        3600d,
                        1d,
                        1,
                        new[] { new EnemySpawnWeight(EnemyCombatant.Id, 1) })
                }
                : enemyWaveStages.ToArray();
            var definitions = new List<CombatantDefinition> { EnemyCombatant };
            if (enemyCombatants != null)
            {
                definitions.AddRange(enemyCombatants.Where(item => item != null));
            }

            EnemyCombatants = definitions
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);
            var missingEnemy = EnemyWaveStages
                .SelectMany(stage => stage.EnemyWeights)
                .FirstOrDefault(weight => !EnemyCombatants.ContainsKey(weight.EnemyId));
            if (missingEnemy != null)
            {
                throw new ArgumentException(
                    "Enemy wave references a missing combat definition: " + missingEnemy.EnemyId,
                    nameof(enemyCombatants));
            }

            EnemySpawnColumns = enemySpawnColumns == null || enemySpawnColumns.Count == 0
                ? new[] { enemySpawnPosition.Column }
                : enemySpawnColumns.Distinct().ToArray();
            if (EnemySpawnColumns.Any(column => column < 1 || column > columns))
            {
                throw new ArgumentOutOfRangeException(nameof(enemySpawnColumns));
            }

            EnemyWaveRandomSeed = enemyWaveRandomSeed;
            EnemyBuildings = enemyBuildings == null
                ? Array.Empty<EnemyBuildingPlacement>()
                : enemyBuildings.ToArray();
            var occupiedBuildingCells = new HashSet<GridPosition>();
            foreach (var building in EnemyBuildings)
            {
                if (building == null ||
                    building.Position.Column < 1 || building.Position.Column > columns ||
                    building.Position.Row < 1 || building.Position.Row > rows)
                {
                    throw new ArgumentOutOfRangeException(nameof(enemyBuildings));
                }

                if (!EnemyCombatants.TryGetValue(building.TemplateId, out var buildingDefinition) ||
                    !buildingDefinition.IsBuilding)
                {
                    throw new ArgumentException(
                        "Enemy building placement references a missing or non-building definition: " +
                        building.TemplateId,
                        nameof(enemyBuildings));
                }

                var startColumn = GetControlBlockStart(building.Position.Column);
                var startRow = GetControlBlockStart(building.Position.Row);
                if (startColumn + BattleGrid.ControlBlockSize - 1 > columns ||
                    startRow + BattleGrid.ControlBlockSize - 1 > rows)
                {
                    throw new ArgumentException(
                        "Enemy building's 3x3 control-block footprint must fit inside the grid: " +
                        building.TemplateId,
                        nameof(enemyBuildings));
                }

                for (var column = startColumn; column < startColumn + BattleGrid.ControlBlockSize; column++)
                {
                    for (var row = startRow; row < startRow + BattleGrid.ControlBlockSize; row++)
                    {
                        if (!occupiedBuildingCells.Add(new GridPosition(column, row)))
                        {
                            throw new ArgumentException(
                                "Enemy building control-block footprints cannot overlap: " + building.TemplateId,
                                nameof(enemyBuildings));
                        }
                    }
                }
            }

            RequiredAssaultScore = Math.Max(0, requiredAssaultScore);
            VictoryByEnemyBuildings = victoryByEnemyBuildings;
            EnableBattleOutcomes = enableBattleOutcomes;
        }

        public int Columns { get; }
        public int Rows { get; }
        public int ControlledRows { get; }
        public int InitialResources { get; }
        public double FixedProductionIntervalSeconds { get; }
        public double TransmitterProductionIntervalSeconds { get; }
        public GridPosition TransmitterPosition { get; }
        public int TransmitterAmount { get; }
        public UnitDefinition TestUnit { get; }
        public CombatantDefinition AllyCombatant { get; }
        public CombatantDefinition EnemyCombatant { get; }
        public GridPosition EnemySpawnPosition { get; }
        public IReadOnlyList<EnemyWaveStage> EnemyWaveStages { get; }
        public IReadOnlyDictionary<string, CombatantDefinition> EnemyCombatants { get; }
        public IReadOnlyList<int> EnemySpawnColumns { get; }
        public int EnemyWaveRandomSeed { get; }
        public IReadOnlyList<EnemyBuildingPlacement> EnemyBuildings { get; }
        public int RequiredAssaultScore { get; }
        public bool VictoryByEnemyBuildings { get; }
        public bool EnableBattleOutcomes { get; }

        private static int GetControlBlockStart(int cellIndex)
        {
            return ((Math.Max(1, cellIndex) - 1) / BattleGrid.ControlBlockSize) *
                   BattleGrid.ControlBlockSize + 1;
        }
    }

    public sealed class ConfigureBattleSliceCommand : AbstractCommand
    {
        private readonly BattleSliceConfiguration _configuration;

        public ConfigureBattleSliceCommand(BattleSliceConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        protected override void OnExecute()
        {
            this.GetModel<BattleSliceModel>().Configure(_configuration);
        }
    }

    public sealed class ResetBattleSliceCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetModel<BattleSliceModel>().ResetSlice();
        }
    }

    public sealed class AdvanceBattleSliceTimeCommand : AbstractCommand
    {
        private readonly double _deltaSeconds;

        public AdvanceBattleSliceTimeCommand(double deltaSeconds)
        {
            _deltaSeconds = deltaSeconds;
        }

        protected override void OnExecute()
        {
            this.GetModel<BattleSliceModel>().Advance(_deltaSeconds);
        }
    }

    public sealed class DeployBattleSliceUnitCommand : AbstractCommand<BattleSliceDeploymentResponse>
    {
        private readonly GridPosition _position;
        private readonly string _actorId;

        public DeployBattleSliceUnitCommand(GridPosition position, string actorId)
        {
            _position = position;
            _actorId = actorId;
        }

        protected override BattleSliceDeploymentResponse OnExecute()
        {
            var result = this.GetModel<BattleSliceModel>().Deploy(_position, _actorId);
            return new BattleSliceDeploymentResponse(result.Succeeded, result.Failure);
        }
    }

    public sealed class ToggleBattleSlicePollutionCommand : AbstractCommand<int>
    {
        private readonly GridPosition _position;

        public ToggleBattleSlicePollutionCommand(GridPosition position)
        {
            _position = position;
        }

        protected override int OnExecute()
        {
            return this.GetModel<BattleSliceModel>().TogglePollution(_position);
        }
    }

    public sealed class GetBattleSliceSnapshotQuery : AbstractQuery<BattleSliceSnapshot>
    {
        protected override BattleSliceSnapshot OnDo()
        {
            return this.GetModel<BattleSliceModel>().CreateSnapshot();
        }
    }

    public sealed class BattleSliceModel : AbstractModel
    {
        private BattleSliceConfiguration _configuration;
        private BattleGrid _grid;
        private ResourceEconomy _economy;
        private DeploymentService _deployment;
        private UnitDefinition _testUnit;
        private CombatSimulation _combat;
        private EnemyWaveScheduler _enemyWaves;
        private readonly List<EnemyWaveSpawn> _enemySpawnHistory = new List<EnemyWaveSpawn>();
        private int _enemyActorSequence;
        private BattleProgression _progression;
        private readonly Dictionary<string, GridPosition> _deploymentPositions =
            new Dictionary<string, GridPosition>(StringComparer.Ordinal);

        protected override void OnInit()
        {
        }

        public void Configure(BattleSliceConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            ResetSlice();
        }

        public void ResetSlice()
        {
            EnsureConfigured();
            _grid = new BattleGrid(_configuration.Columns, _configuration.Rows);
            for (var column = 1; column <= _configuration.Columns; column++)
            {
                for (var row = 1; row <= _configuration.ControlledRows; row++)
                {
                    _grid.SetOwnership(new GridPosition(column, row), true);
                }
            }

            _economy = new ResourceEconomy();
            _economy.Reset(_configuration.InitialResources);
            _economy.ConfigureProduction(
                _configuration.FixedProductionIntervalSeconds,
                _configuration.TransmitterProductionIntervalSeconds);
            if (_configuration.TransmitterAmount > 0 && _grid.IsInside(_configuration.TransmitterPosition))
            {
                _economy.RegisterTransmitter(
                    _configuration.TransmitterPosition,
                    _configuration.TransmitterAmount);
            }

            _testUnit = _configuration.TestUnit;
            _deployment = new DeploymentService(_grid, _economy);
            _deploymentPositions.Clear();
            _combat = new CombatSimulation(_configuration.Columns, _configuration.Rows);
            _enemyWaves = new EnemyWaveScheduler(
                _configuration.Columns,
                _configuration.Rows,
                _configuration.EnemyWaveStages,
                _configuration.EnemyWaveRandomSeed,
                _configuration.EnemySpawnColumns,
                _configuration.EnemySpawnPosition.Row);
            _enemySpawnHistory.Clear();
            _enemyActorSequence = 0;
            _progression = new BattleProgression(
                _configuration.RequiredAssaultScore,
                _configuration.VictoryByEnemyBuildings);
            for (var index = 0; index < _configuration.EnemyBuildings.Count; index++)
            {
                var placement = _configuration.EnemyBuildings[index];
                var definition = _configuration.EnemyCombatants[placement.TemplateId];
                var actorId = $"building-{placement.TemplateId}-{index + 1:00}";
                if (!_grid.TryOccupyBuildingControlBlock(placement.Position, actorId))
                {
                    throw new InvalidOperationException("Enemy building footprint registration failed: " + actorId);
                }

                if (!_combat.TryAddActor(
                        actorId,
                        Team.Enemy,
                        definition,
                        placement.Position.Column,
                        placement.Position.Row))
                {
                    throw new InvalidOperationException("Enemy building combat registration failed: " + actorId);
                }

                _progression.RegisterEnemy(actorId, definition.AssaultScoreReward, true);
                _enemyWaves.RegisterEnemyBuilding(placement.Position);
            }
        }

        public void Advance(double deltaSeconds)
        {
            if (_progression.State != BattleState.Running)
            {
                return;
            }

            _economy.Advance(deltaSeconds);
            foreach (var spawn in _enemyWaves.Advance(deltaSeconds, _grid))
            {
                if (!_configuration.EnemyCombatants.TryGetValue(spawn.EnemyId, out var definition))
                {
                    continue;
                }

                _enemyActorSequence++;
                var actorId = $"enemy-{spawn.EnemyId}-wave{spawn.WaveIndex:0000}-{_enemyActorSequence:0000}";
                if (_combat.TryAddActor(
                        actorId,
                        Team.Enemy,
                        definition,
                        spawn.Position.Column,
                        spawn.Position.Row))
                {
                    _enemySpawnHistory.Add(spawn);
                    _progression.RegisterEnemy(actorId, definition.AssaultScoreReward, false);
                }
            }

            _combat.Advance(deltaSeconds, _grid);
            foreach (var actor in _combat.CreateSnapshot())
            {
                if (!actor.IsAlive && _deploymentPositions.TryGetValue(actor.ActorId, out var position))
                {
                    _grid.RemoveOccupant(position, actor.ActorId);
                    _deploymentPositions.Remove(actor.ActorId);
                }
            }

            ProcessEnemyDeathsAndOutcome();
        }

        public DeploymentResult Deploy(GridPosition position, string actorId)
        {
            if (_progression.State != BattleState.Running)
            {
                return DeploymentResult.Reject(DeploymentFailure.BattleEnded);
            }

            if (_combat.ContainsActor(actorId))
            {
                return DeploymentResult.Reject(DeploymentFailure.DuplicateActorId);
            }

            var result = _deployment.TryDeploy(_testUnit, actorId, position);
            if (!result.Succeeded)
            {
                return result;
            }

            if (!_combat.TryAddActor(
                    actorId,
                    Team.Ally,
                    _configuration.AllyCombatant,
                    position.Column,
                    position.Row))
            {
                _grid.RemoveOccupant(position, actorId);
                _economy.RefundDeployment(_testUnit);
                return DeploymentResult.Reject(DeploymentFailure.CombatRegistrationRejected);
            }

            _deploymentPositions[actorId] = position;
            return result;
        }

        public int TogglePollution(GridPosition position)
        {
            var cell = _grid.GetCell(position);
            return cell == null ? 0 : _grid.SetPollution(position, !cell.IsPolluted);
        }

        public BattleSliceSnapshot CreateSnapshot()
        {
            EnsureConfigured();
            var cells = new List<BattleSliceCellSnapshot>(_configuration.Columns * _configuration.Rows);
            for (var column = 1; column <= _configuration.Columns; column++)
            {
                for (var row = 1; row <= _configuration.Rows; row++)
                {
                    var cell = _grid.GetCell(new GridPosition(column, row));
                    cells.Add(new BattleSliceCellSnapshot(
                        cell.Position,
                        cell.IsOwned,
                        cell.IsPolluted,
                        cell.IsBlockedByBuilding,
                        cell.OccupantCount));
                }
            }

            return new BattleSliceSnapshot(
                _configuration.Columns,
                _configuration.Rows,
                _economy.Resources,
                _economy.ElapsedSeconds,
                _economy.GetRemainingCooldown(_testUnit),
                _testUnit.Id,
                _testUnit.ResourceCost,
                cells,
                _combat.CreateSnapshot().Select(actor => new BattleSliceCombatantSnapshot(actor)).ToArray(),
                _combat.Events.ToArray(),
                _enemyWaves.CurrentStageName,
                _enemyWaves.WaveIndex,
                _enemySpawnHistory.ToArray(),
                _progression.State,
                _progression.AssaultScore,
                _progression.RequiredAssaultScore,
                _progression.EnemyBuildingCount,
                _enemyWaves.CountValidSpawnPoints(_grid));
        }

        private void ProcessEnemyDeathsAndOutcome()
        {
            var actors = _combat.CreateSnapshot().ToDictionary(actor => actor.ActorId, StringComparer.Ordinal);
            foreach (var death in _combat.Events.Where(item => item.Type == CombatEventType.Death))
            {
                if (!actors.TryGetValue(death.ActorId, out var actor) || actor.Team != Team.Enemy ||
                    !_progression.RecordEnemyDeath(actor.ActorId))
                {
                    continue;
                }

                if (actor.IsBuilding)
                {
                    _grid.ClearBuildingFootprint(actor.ActorId);
                    _enemyWaves.DestroyEnemyBuilding(new GridPosition(
                        actor.Column,
                        (int)Math.Round(actor.LanePosition)));
                }
            }

            if (!_configuration.EnableBattleOutcomes)
            {
                return;
            }

            var state = _progression.Evaluate(_grid, _enemyWaves.HasAnyValidSpawnPoint(_grid));
            if (state != BattleState.Running)
            {
                _enemyWaves.Stop();
            }
        }

        private void EnsureConfigured()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Battle slice configuration has not been loaded.");
            }
        }
    }
}
