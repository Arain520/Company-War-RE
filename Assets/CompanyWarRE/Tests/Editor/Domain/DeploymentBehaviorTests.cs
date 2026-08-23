using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class DeploymentBehaviorTests
    {
        [Test]
        public void StandardDeployment_SpendsResourcesAndOccupiesOwnedCell()
        {
            var grid = new BattleGrid(3, 3);
            var economy = new ResourceEconomy();
            var service = new DeploymentService(grid, economy);
            var unit = new UnitDefinition("U01", 3, 2d);
            var position = new GridPosition(1, 1);
            grid.SetOwnership(position, true);
            economy.Reset(5);

            var result = service.TryDeploy(unit, "ally-1", position);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Failure, Is.EqualTo(DeploymentFailure.None));
            Assert.That(economy.Resources, Is.EqualTo(2));
            Assert.That(grid.GetCell(position).Occupants, Does.Contain("ally-1"));
        }

        [TestCase(0, 1, DeploymentFailure.OutsideGrid)]
        [TestCase(2, 2, DeploymentFailure.TerritoryNotOwned)]
        public void StandardDeployment_ExplainsSpatialRejections(int column, int row, DeploymentFailure expected)
        {
            var grid = new BattleGrid(3, 3);
            var economy = new ResourceEconomy();
            economy.Reset(10);
            var service = new DeploymentService(grid, economy);

            var result = service.TryDeploy(
                new UnitDefinition("U01", 1, 0d),
                "ally-1",
                new GridPosition(column, row));

            Assert.That(result.Failure, Is.EqualTo(expected));
        }

        [Test]
        public void StandardDeployment_RejectsPollutionOccupationResourcesAndCooldown()
        {
            var unit = new UnitDefinition("U01", 3, 2d);

            Assert.That(Attempt(unit, 10, polluted: true).Failure, Is.EqualTo(DeploymentFailure.Polluted));
            Assert.That(Attempt(unit, 10, occupied: true).Failure, Is.EqualTo(DeploymentFailure.Occupied));
            Assert.That(Attempt(unit, 2).Failure, Is.EqualTo(DeploymentFailure.InsufficientResources));

            var grid = new BattleGrid(3, 3);
            var economy = new ResourceEconomy();
            var service = new DeploymentService(grid, economy);
            var first = new GridPosition(1, 1);
            var second = new GridPosition(1, 2);
            grid.SetOwnership(first, true);
            grid.SetOwnership(second, true);
            economy.Reset(10);
            Assert.That(service.TryDeploy(unit, "ally-1", first).Succeeded, Is.True);
            Assert.That(service.TryDeploy(unit, "ally-2", second).Failure, Is.EqualTo(DeploymentFailure.CooldownActive));
        }

        [TestCase(DeploymentMode.Building)]
        [TestCase(DeploymentMode.SupportEffect)]
        [TestCase(DeploymentMode.TerrainBuild)]
        [TestCase(DeploymentMode.OuterRing)]
        public void NonStandardDeployment_IsExplicitlyDeferred(DeploymentMode mode)
        {
            var grid = new BattleGrid(3, 3);
            var economy = new ResourceEconomy();
            grid.SetOwnership(new GridPosition(1, 1), true);
            economy.Reset(10);

            var result = new DeploymentService(grid, economy).TryDeploy(
                new UnitDefinition("UX", 1, 0d, mode),
                "ally-1",
                new GridPosition(1, 1));

            Assert.That(result.Failure, Is.EqualTo(DeploymentFailure.UnsupportedMode));
        }

        private static DeploymentResult Attempt(UnitDefinition unit, int resources, bool polluted = false, bool occupied = false)
        {
            var grid = new BattleGrid(3, 3);
            var economy = new ResourceEconomy();
            var position = new GridPosition(1, 1);
            grid.SetOwnership(position, true);
            if (polluted)
            {
                grid.SetPollution(position, true);
            }

            if (occupied)
            {
                grid.TryAddOccupant(position, "existing", 1);
            }

            economy.Reset(resources);
            return new DeploymentService(grid, economy).TryDeploy(unit, "ally-1", position);
        }
    }
}
