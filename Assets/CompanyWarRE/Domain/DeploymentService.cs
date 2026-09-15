using System;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public enum DeploymentFailure
    {
        None,
        MissingUnit,
        MissingActorId,
        DuplicateActorId,
        UnsupportedMode,
        OutsideGrid,
        TerritoryNotOwned,
        Polluted,
        Occupied,
        InsufficientResources,
        CooldownActive,
        OccupancyRejected,
        CombatRegistrationRejected,
        BattleEnded
    }

    public sealed class DeploymentResult
    {
        private DeploymentResult(bool succeeded, DeploymentFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public DeploymentFailure Failure { get; }

        public static DeploymentResult Success()
        {
            return new DeploymentResult(true, DeploymentFailure.None);
        }

        public static DeploymentResult Reject(DeploymentFailure failure)
        {
            if (failure == DeploymentFailure.None)
            {
                throw new ArgumentException("A rejection reason is required.", nameof(failure));
            }

            return new DeploymentResult(false, failure);
        }
    }

    public sealed class DeploymentService
    {
        private readonly BattleGrid _grid;
        private readonly ResourceEconomy _resources;

        public DeploymentService(BattleGrid grid, ResourceEconomy resources)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public DeploymentResult TryDeploy(UnitDefinition unit, string actorId, GridPosition target)
        {
            if (unit == null) return DeploymentResult.Reject(DeploymentFailure.MissingUnit);
            if (string.IsNullOrWhiteSpace(actorId)) return DeploymentResult.Reject(DeploymentFailure.MissingActorId);
            var validation = Validate(unit, target);
            if (!validation.Succeeded) return validation;
            if (!_resources.TryDeploy(unit))
                return DeploymentResult.Reject(DeploymentFailure.InsufficientResources);

            var occupied = unit.DeploymentMode == DeploymentMode.Building
                ? _grid.TryOccupyBuildingControlBlock(target, actorId)
                : _grid.TryAddOccupant(target, actorId, 1);
            if (!occupied)
            {
                _resources.RefundDeployment(unit);
                return DeploymentResult.Reject(DeploymentFailure.OccupancyRejected);
            }
            return DeploymentResult.Success();
        }

        /// <summary>Read-only validation shared by placement previews and deployment.</summary>
        public DeploymentResult Validate(UnitDefinition unit, GridPosition target)
        {
            if (unit == null)
            {
                return DeploymentResult.Reject(DeploymentFailure.MissingUnit);
            }

            if (unit.DeploymentMode != DeploymentMode.StandardUnit &&
                unit.DeploymentMode != DeploymentMode.Building)
            {
                return DeploymentResult.Reject(DeploymentFailure.UnsupportedMode);
            }

            var cell = _grid.GetCell(target);
            if (cell == null)
            {
                return DeploymentResult.Reject(DeploymentFailure.OutsideGrid);
            }

            if (!cell.IsOwned && !unit.CanDeployOutside)
            {
                return DeploymentResult.Reject(DeploymentFailure.TerritoryNotOwned);
            }

            if (cell.IsPolluted)
            {
                return DeploymentResult.Reject(DeploymentFailure.Polluted);
            }

            if (unit.DeploymentMode == DeploymentMode.Building)
            {
                var block = _grid.GetControlBlockForCell(target);
                if (block == null || block.Cells.Count != BattleGrid.ControlBlockSize * BattleGrid.ControlBlockSize ||
                    (!unit.CanDeployOutside && !block.IsControlled) ||
                    block.IsPolluted ||
                    block.Cells.Any(candidate => candidate.IsBlockedByBuilding || candidate.OccupantCount > 0))
                {
                    return DeploymentResult.Reject(DeploymentFailure.Occupied);
                }
            }
            else if (cell.IsBlockedByBuilding || cell.OccupantCount >= 1)
            {
                return DeploymentResult.Reject(DeploymentFailure.Occupied);
            }

            if (_resources.Resources < unit.ResourceCost)
            {
                return DeploymentResult.Reject(DeploymentFailure.InsufficientResources);
            }

            if (_resources.GetRemainingCooldown(unit) > 0d)
            {
                return DeploymentResult.Reject(DeploymentFailure.CooldownActive);
            }

            return DeploymentResult.Success();
        }
    }
}
