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
        public BattleSliceCellSnapshot(GridPosition position, bool isOwned, bool isPolluted, int occupantCount)
        {
            Position = position;
            IsOwned = isOwned;
            IsPolluted = isPolluted;
            OccupantCount = occupantCount;
        }

        public GridPosition Position { get; }
        public bool IsOwned { get; }
        public bool IsPolluted { get; }
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
            IsAlive = actor.IsAlive;
        }

        public string ActorId { get; }
        public string TemplateId { get; }
        public Team Team { get; }
        public int Column { get; }
        public double LanePosition { get; }
        public double HitPoints { get; }
        public double MaximumHitPoints { get; }
        public bool IsAlive { get; }
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
            IReadOnlyList<CombatEvent> combatEvents)
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
            int controlledColumns,
            int initialResources,
            double fixedProductionIntervalSeconds,
            double transmitterProductionIntervalSeconds,
            GridPosition transmitterPosition,
            int transmitterAmount,
            UnitDefinition testUnit,
            CombatantDefinition allyCombatant,
            CombatantDefinition enemyCombatant,
            GridPosition enemySpawnPosition)
        {
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }

            if (controlledColumns <= 0 || controlledColumns > columns)
            {
                throw new ArgumentOutOfRangeException(nameof(controlledColumns));
            }

            Columns = columns;
            Rows = rows;
            ControlledColumns = controlledColumns;
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

            EnemySpawnPosition = enemySpawnPosition;
        }

        public int Columns { get; }
        public int Rows { get; }
        public int ControlledColumns { get; }
        public int InitialResources { get; }
        public double FixedProductionIntervalSeconds { get; }
        public double TransmitterProductionIntervalSeconds { get; }
        public GridPosition TransmitterPosition { get; }
        public int TransmitterAmount { get; }
        public UnitDefinition TestUnit { get; }
        public CombatantDefinition AllyCombatant { get; }
        public CombatantDefinition EnemyCombatant { get; }
        public GridPosition EnemySpawnPosition { get; }
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
            for (var column = 1; column <= _configuration.ControlledColumns; column++)
            {
                for (var row = 1; row <= _configuration.Rows; row++)
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
            _combat.TryAddActor(
                "enemy-" + _configuration.EnemyCombatant.Id + "-01",
                Team.Enemy,
                _configuration.EnemyCombatant,
                _configuration.EnemySpawnPosition.Column,
                _configuration.EnemySpawnPosition.Row);
        }

        public void Advance(double deltaSeconds)
        {
            _economy.Advance(deltaSeconds);
            _combat.Advance(deltaSeconds);
            foreach (var actor in _combat.CreateSnapshot())
            {
                if (!actor.IsAlive && _deploymentPositions.TryGetValue(actor.ActorId, out var position))
                {
                    _grid.RemoveOccupant(position, actor.ActorId);
                    _deploymentPositions.Remove(actor.ActorId);
                }
            }
        }

        public DeploymentResult Deploy(GridPosition position, string actorId)
        {
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
                _combat.Events.ToArray());
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
