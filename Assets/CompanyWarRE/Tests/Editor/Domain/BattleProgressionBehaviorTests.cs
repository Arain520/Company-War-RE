using CompanyWarRE.Domain;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class BattleProgressionBehaviorTests
    {
        [Test]
        public void EnemyDeath_AwardsScoreExactlyOnce()
        {
            var progression = new BattleProgression(8, true);
            Assert.That(progression.RegisterEnemy("enemy-E01-01", 2, false), Is.True);

            Assert.That(progression.RecordEnemyDeath("enemy-E01-01"), Is.True);
            Assert.That(progression.RecordEnemyDeath("enemy-E01-01"), Is.False);

            Assert.That(progression.AssaultScore, Is.EqualTo(2));
            Assert.That(progression.EnemyBuildingCount, Is.Zero);
        }

        [Test]
        public void LastEnemyBuildingDeath_ProducesVictoryAndKeepsScoreInformational()
        {
            var grid = OwnedGrid();
            var progression = new BattleProgression(100, true);
            progression.RegisterEnemy("building-E06-01", 1, true);

            Assert.That(progression.Evaluate(grid, true), Is.EqualTo(BattleState.Running));
            progression.RecordEnemyDeath("building-E06-01");

            Assert.That(progression.AssaultScore, Is.EqualTo(1));
            Assert.That(progression.Evaluate(grid, true), Is.EqualTo(BattleState.Victory));
        }

        [Test]
        public void LostTerritory_TakesPriorityOverBuildingVictory()
        {
            var progression = new BattleProgression(0, true);

            Assert.That(
                progression.Evaluate(new BattleGrid(3, 3), false),
                Is.EqualTo(BattleState.Defeat));
        }

        [Test]
        public void NoValidSpawn_IsDefeatOnlyWhenBuildingObjectiveIsDisabled()
        {
            var grid = OwnedGrid();
            var progression = new BattleProgression(0, false);
            progression.RegisterEnemy("enemy-E01-01", 1, false);

            Assert.That(progression.Evaluate(grid, false), Is.EqualTo(BattleState.Defeat));
        }

        private static BattleGrid OwnedGrid()
        {
            var grid = new BattleGrid(3, 3);
            grid.SetOwnership(new GridPosition(1, 1), true);
            return grid;
        }
    }
}
