using System;

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
            if (unit == null)
            {
                return DeploymentResult.Reject(DeploymentFailure.MissingUnit);
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                return DeploymentResult.Reject(DeploymentFailure.MissingActorId);
            }

            if (unit.DeploymentMode != DeploymentMode.StandardUnit)
            {
                return DeploymentResult.Reject(DeploymentFailure.UnsupportedMode);
            }

            var cell = _grid.GetCell(target);
            if (cell == null)
            {
                return DeploymentResult.Reject(DeploymentFailure.OutsideGrid);
            }

            if (!cell.IsOwned)
            {
                return DeploymentResult.Reject(DeploymentFailure.TerritoryNotOwned);
            }

            if (cell.IsPolluted)
            {
                return DeploymentResult.Reject(DeploymentFailure.Polluted);
            }

            if (cell.OccupantCount >= 1)
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

            if (!_resources.TryDeploy(unit))
            {
                return DeploymentResult.Reject(DeploymentFailure.InsufficientResources);
            }

            if (!_grid.TryAddOccupant(target, actorId, 1))
            {
                _resources.RefundDeployment(unit);
                return DeploymentResult.Reject(DeploymentFailure.OccupancyRejected);
            }

            return DeploymentResult.Success();
        }
    }
}
