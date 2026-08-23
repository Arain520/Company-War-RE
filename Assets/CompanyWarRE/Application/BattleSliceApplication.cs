using System;
using System.Collections.Generic;
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

    public sealed class BattleSliceSnapshot
    {
        public BattleSliceSnapshot(
            int resources,
            double elapsedSeconds,
            double remainingCooldown,
            IReadOnlyList<BattleSliceCellSnapshot> cells)
        {
            Resources = resources;
            ElapsedSeconds = elapsedSeconds;
            RemainingCooldown = remainingCooldown;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public int Resources { get; }
        public double ElapsedSeconds { get; }
        public double RemainingCooldown { get; }
        public IReadOnlyList<BattleSliceCellSnapshot> Cells { get; }
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
        public const int Columns = 6;
        public const int Rows = 6;

        private BattleGrid _grid;
        private ResourceEconomy _economy;
        private DeploymentService _deployment;
        private UnitDefinition _testUnit;

        protected override void OnInit()
        {
            ResetSlice();
        }

        public void ResetSlice()
        {
            _grid = new BattleGrid(Columns, Rows);
            for (var column = 1; column <= Columns / 2; column++)
            {
                for (var row = 1; row <= Rows; row++)
                {
                    _grid.SetOwnership(new GridPosition(column, row), true);
                }
            }

            _economy = new ResourceEconomy();
            _economy.Reset(8);
            _economy.ConfigureProduction(2d, 3d);
            _economy.RegisterTransmitter(new GridPosition(2, 2), 1);
            _testUnit = new UnitDefinition("U01", 3, 2d);
            _deployment = new DeploymentService(_grid, _economy);
        }

        public void Advance(double deltaSeconds)
        {
            _economy.Advance(deltaSeconds);
        }

        public DeploymentResult Deploy(GridPosition position, string actorId)
        {
            return _deployment.TryDeploy(_testUnit, actorId, position);
        }

        public int TogglePollution(GridPosition position)
        {
            var cell = _grid.GetCell(position);
            return cell == null ? 0 : _grid.SetPollution(position, !cell.IsPolluted);
        }

        public BattleSliceSnapshot CreateSnapshot()
        {
            var cells = new List<BattleSliceCellSnapshot>(Columns * Rows);
            for (var column = 1; column <= Columns; column++)
            {
                for (var row = 1; row <= Rows; row++)
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
                _economy.Resources,
                _economy.ElapsedSeconds,
                _economy.GetRemainingCooldown(_testUnit),
                cells);
        }
    }
}
