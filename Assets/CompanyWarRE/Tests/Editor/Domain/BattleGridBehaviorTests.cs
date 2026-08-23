using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class BattleGridBehaviorTests
    {
        [Test]
        public void Grid_IsOneBasedAndUsesThreeByThreeControlBlocks()
        {
            var grid = new BattleGrid(5, 5);

            Assert.That(grid.ControlBlockColumns, Is.EqualTo(2));
            Assert.That(grid.ControlBlockRows, Is.EqualTo(2));
            Assert.That(grid.GetCell(new GridPosition(0, 1)), Is.Null);
            Assert.That(grid.GetCell(new GridPosition(1, 1)).ControlBlock, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(grid.GetCell(new GridPosition(5, 5)).ControlBlock, Is.EqualTo(new GridPosition(2, 2)));
        }

        [Test]
        public void Pollution_AppliesToTheWholeControlBlockAndRecordsOnlyChanges()
        {
            var grid = new BattleGrid(6, 6);

            Assert.That(grid.SetPollution(new GridPosition(2, 2), true), Is.EqualTo(9));
            Assert.That(grid.SetPollution(new GridPosition(1, 3), true), Is.Zero);
            Assert.That(grid.GetCell(new GridPosition(3, 3)).IsPolluted, Is.True);
            Assert.That(grid.GetCell(new GridPosition(4, 3)).IsPolluted, Is.False);
            Assert.That(grid.PollutionChanges.Count, Is.EqualTo(9));
            Assert.That(grid.SetPollution(new GridPosition(2, 1), false), Is.EqualTo(9));
        }

        [Test]
        public void ControlledTerritory_RequiresAnOwnedUnpollutedCell()
        {
            var grid = new BattleGrid(3, 3);
            var position = new GridPosition(2, 2);

            Assert.That(grid.HasControlledTerritory(), Is.False);
            Assert.That(grid.SetOwnership(position, true), Is.True);
            Assert.That(grid.HasControlledTerritory(), Is.True);
            grid.SetPollution(position, true);
            Assert.That(grid.HasControlledTerritory(), Is.False);
        }

        [Test]
        public void StandardDeploymentCapacity_IsOneEvenThoughStorageCanHoldNine()
        {
            var grid = new BattleGrid(3, 3);
            var position = new GridPosition(1, 1);
            grid.SetOwnership(position, true);

            Assert.That(grid.CanDeployStandardUnit(position), Is.True);
            Assert.That(grid.TryAddOccupant(position, "A", BattleGrid.MaximumOccupantsPerCell), Is.True);
            Assert.That(grid.TryAddOccupant(position, "A", BattleGrid.MaximumOccupantsPerCell), Is.True);
            Assert.That(grid.GetCell(position).OccupantCount, Is.EqualTo(1));
            Assert.That(grid.CanDeployStandardUnit(position), Is.False);
        }
    }
}
