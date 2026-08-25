using System.Linq;
using CompanyWarRE.Domain;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class SupportAbilityBehaviorTests
    {
        [TestCase("DeployEcho1", 1)]
        [TestCase("DeployEcho3", 3)]
        public void U13AndU14_DeclareCowDeploymentEchoCountsInDomain(string effect, int expected)
        {
            var unit = new UnitDefinition("echo", 1, 1d, effect: effect);

            Assert.That(unit.DeploymentEchoCount, Is.EqualTo(expected));
        }

        [Test]
        public void U12_FreezesEnemyMovementForExactlyFiveSimulationTicks()
        {
            var fixture = CreateFixture();
            fixture.Combat.TryAddActor("enemy", Team.Enemy, MovingEnemy(), 2, 8d);

            Assert.That(fixture.Service.TryExecute(Support("U12", "Freeze"), new GridPosition(2, 5)).Succeeded, Is.True);
            for (var tick = 0; tick < 5; tick++)
            {
                fixture.Combat.Advance(1d, fixture.Grid);
            }

            Assert.That(fixture.Combat.CreateSnapshot().Single().LanePosition, Is.EqualTo(8d));
            fixture.Combat.Advance(1d, fixture.Grid);
            Assert.That(fixture.Combat.CreateSnapshot().Single().LanePosition, Is.EqualTo(7d));
        }

        [Test]
        public void U26AndU34_StrongerSilenceWinsAndDurationExtends()
        {
            var fixture = CreateFixture();
            fixture.Combat.TryAddActor("enemy", Team.Enemy, MovingEnemy(), 2, 9d);

            fixture.Service.TryExecute(Support("U26", "Silence50"), new GridPosition(2, 5));
            fixture.Service.TryExecute(Support("U34", "Silence80"), new GridPosition(2, 5));
            fixture.Combat.Advance(1d, fixture.Grid);

            Assert.That(fixture.Combat.CreateSnapshot().Single().LanePosition, Is.EqualTo(8.8d).Within(0.0001d));
            Assert.That(fixture.Combat.EnemySpeedMultiplier, Is.EqualTo(0.2d));
            fixture.Combat.Advance(12d, fixture.Grid);
            Assert.That(fixture.Combat.EnemySpeedMultiplier, Is.EqualTo(1d));
        }

        [TestCase("U06", "Strike", 0)]
        [TestCase("U07", "Bomb3x3", 1)]
        [TestCase("U35", "Destroy5x5", 2)]
        public void DamageSupport_KillsStaffButOnlyRemovesFiveBuildingHp(
            string id,
            string effect,
            int radius)
        {
            var fixture = CreateFixture(15, 15);
            var target = new GridPosition(8, 8);
            var actualTargetRow = effect == "Strike" ? 9 : 8;
            fixture.Combat.TryAddActor("staff", Team.Enemy, MovingEnemy(), 8, actualTargetRow);
            fixture.Combat.TryAddActor(
                "building",
                Team.Enemy,
                new CombatantDefinition("E06", "Building", 20, 0d, 0d, 1d, 1),
                7,
                7d);

            var result = fixture.Service.TryExecute(Support(id, effect), target);
            var actors = fixture.Combat.CreateSnapshot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(actors.Single(actor => actor.ActorId == "staff").IsAlive, Is.False);
            Assert.That(actors.Single(actor => actor.ActorId == "building").HitPoints, Is.EqualTo(15d));
            Assert.That(radius, Is.GreaterThanOrEqualTo(0));
        }

        [TestCase("U28", "Lure3x3")]
        [TestCase("U29", "Lure5x5")]
        public void Lure_MovesNonBuildingEnemyToTargetBlockColumnAndLocksActions(string id, string effect)
        {
            var fixture = CreateFixture();
            fixture.Combat.TryAddActor("enemy", Team.Enemy, MovingEnemy(), 1, 8d);
            fixture.Combat.TryAddActor(
                "building",
                Team.Enemy,
                new CombatantDefinition("E06", "Building", 10, 0d, 0d, 1d, 1),
                1,
                7d);

            fixture.Service.TryExecute(Support(id, effect), new GridPosition(5, 8));
            var actors = fixture.Combat.CreateSnapshot();

            Assert.That(actors.Single(actor => actor.ActorId == "enemy").Column, Is.EqualTo(5));
            Assert.That(actors.Single(actor => actor.ActorId == "enemy").AttackProgress, Is.EqualTo(-5d));
            Assert.That(actors.Single(actor => actor.ActorId == "building").Column, Is.EqualTo(1));
            fixture.Combat.Advance(1d, fixture.Grid);
            var locked = fixture.Combat.CreateSnapshot().Single(actor => actor.ActorId == "enemy");
            Assert.That(locked.LanePosition, Is.EqualTo(8d));
            Assert.That(locked.AttackProgress, Is.EqualTo(-4d));
        }

        [Test]
        public void U15_BuildsOnlyAdjacentTerrainAndClearsWholeBlockPollution()
        {
            var fixture = CreateFixture();
            OwnBlock(fixture.Grid, new GridPosition(2, 2));
            fixture.Grid.SetPollution(new GridPosition(2, 5), true);

            var result = fixture.Service.TryExecute(
                new UnitDefinition("U15", 1, 1d, DeploymentMode.TerrainBuild, effect: "TerrainBuild"),
                new GridPosition(2, 5));
            var block = fixture.Grid.GetControlBlockForCell(new GridPosition(2, 5));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(block.Cells.All(cell => cell.IsOwned), Is.True);
            Assert.That(block.Cells.All(cell => !cell.IsPolluted), Is.True);
        }

        private static Fixture CreateFixture(int columns = 9, int rows = 9)
        {
            var grid = new BattleGrid(columns, rows);
            var economy = new ResourceEconomy();
            economy.Reset(100);
            return new Fixture(grid, economy, new CombatSimulation(columns, rows));
        }

        private static CombatantDefinition MovingEnemy()
        {
            return new CombatantDefinition("E01", "Staff", 10, 0d, 1d, 1d, 1);
        }

        private static UnitDefinition Support(string id, string effect)
        {
            return new UnitDefinition(id, 1, 0d, DeploymentMode.SupportEffect, effect: effect);
        }

        private static void OwnBlock(BattleGrid grid, GridPosition target)
        {
            foreach (var cell in grid.GetControlBlockForCell(target).Cells)
            {
                grid.SetOwnership(cell.Position, true);
            }
        }

        private sealed class Fixture
        {
            public Fixture(BattleGrid grid, ResourceEconomy economy, CombatSimulation combat)
            {
                Grid = grid;
                Economy = economy;
                Combat = combat;
                Service = new BattleSupportAbilityService(grid, economy, combat);
            }

            public BattleGrid Grid { get; }
            public ResourceEconomy Economy { get; }
            public CombatSimulation Combat { get; }
            public BattleSupportAbilityService Service { get; }
        }
    }
}
