using System;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public sealed class BattleSupportAbilityService
    {
        private readonly BattleGrid _grid;
        private readonly ResourceEconomy _economy;
        private readonly CombatSimulation _combat;

        public BattleSupportAbilityService(
            BattleGrid grid,
            ResourceEconomy economy,
            CombatSimulation combat)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public DeploymentResult TryExecute(UnitDefinition unit, GridPosition target)
        {
            var validation = Validate(unit, target);
            if (!validation.Succeeded) return validation;
            if (!_economy.TryDeploy(unit))
                return DeploymentResult.Reject(DeploymentFailure.InsufficientResources);
            Execute(unit.Effect, target);
            return DeploymentResult.Success();
        }

        public DeploymentResult Validate(UnitDefinition unit, GridPosition target)
        {
            if (unit == null)
            {
                return DeploymentResult.Reject(DeploymentFailure.MissingUnit);
            }

            if (unit.DeploymentMode != DeploymentMode.SupportEffect &&
                unit.DeploymentMode != DeploymentMode.TerrainBuild)
            {
                return DeploymentResult.Reject(DeploymentFailure.UnsupportedMode);
            }

            if (!_grid.IsInside(target))
            {
                return DeploymentResult.Reject(DeploymentFailure.OutsideGrid);
            }

            if (!IsSupported(unit.Effect))
            {
                return DeploymentResult.Reject(DeploymentFailure.UnsupportedMode);
            }

            if (unit.DeploymentMode == DeploymentMode.TerrainBuild && !CanBuildTerrain(target))
            {
                return DeploymentResult.Reject(DeploymentFailure.TerritoryNotOwned);
            }

            if (_economy.Resources < unit.ResourceCost)
            {
                return DeploymentResult.Reject(DeploymentFailure.InsufficientResources);
            }

            if (_economy.GetRemainingCooldown(unit) > 0d)
            {
                return DeploymentResult.Reject(DeploymentFailure.CooldownActive);
            }

            return DeploymentResult.Success();
        }

        private bool CanBuildTerrain(GridPosition target)
        {
            var targetBlock = _grid.GetControlBlockForCell(target);
            if (targetBlock == null || targetBlock.IsControlled ||
                targetBlock.Cells.Any(cell => cell.IsBlockedByBuilding || cell.OccupantCount > 0))
            {
                return false;
            }

            return Enumerable.Range(1, _grid.ControlBlockColumns)
                .SelectMany(column => Enumerable.Range(1, _grid.ControlBlockRows)
                    .Select(row => _grid.GetControlBlock(new GridPosition(column, row))))
                .Any(block =>
                    block != null &&
                    block.IsControlled &&
                    Math.Abs(block.Position.Column - targetBlock.Position.Column) +
                    Math.Abs(block.Position.Row - targetBlock.Position.Row) == 1);
        }

        private void Execute(string effect, GridPosition target)
        {
            if (EqualsEffect(effect, "Strike"))
            {
                _combat.ApplySupportDamage(
                    new GridPosition(target.Column, Math.Min(_grid.Rows, target.Row + 1)),
                    0,
                    5d);
            }
            else if (EqualsEffect(effect, "Bomb3x3"))
            {
                _combat.ApplySupportDamage(target, 1, 5d);
            }
            else if (EqualsEffect(effect, "Destroy5x5"))
            {
                _combat.ApplySupportDamage(target, 2, 5d);
            }
            else if (EqualsEffect(effect, "Freeze"))
            {
                _combat.ApplyGlobalFreeze(5);
            }
            else if (EqualsEffect(effect, "Silence50"))
            {
                _combat.ApplyEnemySpeedMultiplier(0.5d, 10d);
            }
            else if (EqualsEffect(effect, "Silence80"))
            {
                _combat.ApplyEnemySpeedMultiplier(0.2d, 13d);
            }
            else if (EqualsEffect(effect, "Lure3x3"))
            {
                _combat.ApplyLure(target, 1, 5d);
            }
            else if (EqualsEffect(effect, "Lure5x5"))
            {
                _combat.ApplyLure(target, 2, 5d);
            }
            else if (EqualsEffect(effect, "TerrainBuild"))
            {
                var block = _grid.GetControlBlockForCell(target);
                _grid.SetPollution(target, false);
                foreach (var cell in block.Cells)
                {
                    _grid.SetOwnership(cell.Position, true);
                }
            }
        }

        private static bool IsSupported(string effect)
        {
            return EqualsEffect(effect, "Strike") ||
                   EqualsEffect(effect, "Bomb3x3") ||
                   EqualsEffect(effect, "Destroy5x5") ||
                   EqualsEffect(effect, "Freeze") ||
                   EqualsEffect(effect, "Silence50") ||
                   EqualsEffect(effect, "Silence80") ||
                   EqualsEffect(effect, "Lure3x3") ||
                   EqualsEffect(effect, "Lure5x5") ||
                   EqualsEffect(effect, "TerrainBuild");
        }

        private static bool EqualsEffect(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
