using System.Linq;
using CompanyWarRE.Domain;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class CombatSimulationBehaviorTests
    {
        private static readonly CombatantDefinition U01 =
            new CombatantDefinition("U01", "Staff", 1, 1d, 1d, 1d, 1);
        private static readonly CombatantDefinition E01 =
            new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1);

        [Test]
        public void U01AndE01_ApproachInTheSameColumnWithoutAttackingEarly()
        {
            var simulation = CreateDuel(1d, 9d);

            simulation.Advance(1d);

            var actors = simulation.CreateSnapshot();
            Assert.That(actors.Single(actor => actor.Team == Team.Ally).LanePosition, Is.EqualTo(2d).Within(0.0001d));
            Assert.That(actors.Single(actor => actor.Team == Team.Enemy).LanePosition, Is.EqualTo(8d).Within(0.0001d));
            Assert.That(simulation.Events, Is.Empty);
        }

        [Test]
        public void U01AndE01_ExchangeDamageSimultaneouslyAndDie()
        {
            var simulation = CreateDuel(5d, 6d);

            simulation.Advance(1d);

            Assert.That(simulation.CreateSnapshot().All(actor => !actor.IsAlive), Is.True);
            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Attack), Is.EqualTo(2));
            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Death), Is.EqualTo(2));
        }

        [Test]
        public void DifferentColumns_DoNotMoveTowardOrAttackEachOther()
        {
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("ally", Team.Ally, U01, 3, 5d);
            simulation.TryAddActor("enemy", Team.Enemy, E01, 4, 5.1d);

            simulation.Advance(2d);

            Assert.That(simulation.Events, Is.Empty);
            Assert.That(simulation.CreateSnapshot().All(actor => actor.IsAlive), Is.True);
        }

        [Test]
        public void LargeTick_AccumulatesEveryCompletedAttackInterval()
        {
            var durableEnemy = new CombatantDefinition("E02", "Staff", 10, 0d, 0d, 1d, 1);
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("ally", Team.Ally, U01, 3, 5d);
            simulation.TryAddActor("enemy", Team.Enemy, durableEnemy, 3, 5.1d);

            simulation.Advance(3.2d);

            var enemy = simulation.CreateSnapshot().Single(actor => actor.ActorId == "enemy");
            Assert.That(enemy.HitPoints, Is.EqualTo(7d));
            Assert.That(
                simulation.Events.Count(item =>
                    item.Type == CombatEventType.Attack && item.ActorId == "ally"),
                Is.EqualTo(3));
        }

        [Test]
        public void Death_IsReportedOnceAndDeadActorsNeverAttackAgain()
        {
            var simulation = CreateDuel(5d, 6d);
            simulation.Advance(1d);
            var eventCount = simulation.Events.Count;

            simulation.Advance(5d);

            Assert.That(simulation.Events.Count, Is.EqualTo(eventCount));
        }

        [Test]
        public void SurvivingMeleeUnit_ResumesForwardMovementAfterOpponentDies()
        {
            var survivor = new CombatantDefinition("U01", "Staff", 2, 1d, 1d, 1d, 1);
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("ally", Team.Ally, survivor, 3, 5d);
            simulation.TryAddActor("enemy", Team.Enemy, E01, 3, 6d);
            simulation.Advance(1d);
            var positionAfterCombat = simulation.CreateSnapshot().Single(actor => actor.ActorId == "ally").LanePosition;

            simulation.Advance(1d);

            var ally = simulation.CreateSnapshot().Single(actor => actor.ActorId == "ally");
            Assert.That(ally.IsAlive, Is.True);
            Assert.That(ally.LanePosition, Is.GreaterThan(positionAfterCombat));
            Assert.That(
                simulation.Events.Any(item => item.Type == CombatEventType.MeleeBattlefieldStarted),
                Is.True);
            Assert.That(
                simulation.Events.Any(item => item.Type == CombatEventType.MeleeBattlefieldEnded),
                Is.True);
        }

        [Test]
        public void EnemyEnteringOwnedRows_PollutesWholeControlBlockAndDies()
        {
            var grid = CreateTerritoryGrid();
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("enemy", Team.Enemy, E01, 3, 9d);

            simulation.Advance(3d, grid);

            var invadedBlock = grid.GetControlBlockForCell(new GridPosition(3, 6));
            Assert.That(invadedBlock.Cells.Count(cell => cell.IsPolluted), Is.EqualTo(9));
            Assert.That(invadedBlock.Cells.Any(cell => cell.IsOwned), Is.False);
            Assert.That(simulation.CreateSnapshot().Single().IsAlive, Is.False);
            var pollution = simulation.Events.Single(item => item.Type == CombatEventType.Pollution);
            Assert.That(pollution.Column, Is.EqualTo(3));
            Assert.That(pollution.Row, Is.EqualTo(6));
            Assert.That(pollution.ControlBlockColumn, Is.EqualTo(1));
            Assert.That(pollution.ControlBlockRow, Is.EqualTo(2));
            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Death), Is.EqualTo(1));
        }

        [Test]
        public void EnemyInEnemyTerritory_DoesNotPolluteBeforeCrossingBoundary()
        {
            var grid = CreateTerritoryGrid();
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("enemy", Team.Enemy, E01, 3, 9d);

            simulation.Advance(2d, grid);

            Assert.That(grid.PollutionChanges, Is.Empty);
            Assert.That(simulation.Events.Any(item => item.Type == CombatEventType.Pollution), Is.False);
            Assert.That(simulation.CreateSnapshot().Single().IsAlive, Is.True);
        }

        [Test]
        public void LivingAllyMeleeInInvadedBlock_StartsBattleAndPreventsPollution()
        {
            var grid = CreateTerritoryGrid();
            var simulation = new CombatSimulation(9, 9);
            simulation.TryAddActor("ally", Team.Ally, U01, 2, 5d);
            simulation.TryAddActor("enemy", Team.Enemy, E01, 3, 7d);

            simulation.Advance(1d, grid);

            Assert.That(grid.PollutionChanges, Is.Empty);
            Assert.That(simulation.Events.Any(item => item.Type == CombatEventType.Pollution), Is.False);
            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Attack), Is.EqualTo(2));
            Assert.That(simulation.CreateSnapshot().All(actor => !actor.IsAlive), Is.True);
        }

        [Test]
        public void DifferentSmallColumnsInSameControlBlock_EngageOnReservedBattleRows()
        {
            var ally = new CombatantDefinition("U-LINE", "Staff", 10, 1d, 1d, 1d, 1);
            var enemy = new CombatantDefinition("E-LINE", "Staff", 10, 1d, 1d, 1d, 1);
            var simulation = new CombatSimulation(3, 6);
            simulation.TryAddActor("ally", Team.Ally, ally, 1, 4d);
            simulation.TryAddActor("enemy", Team.Enemy, enemy, 3, 6d);

            simulation.Advance(0.25d);

            var firstSnapshot = simulation.CreateSnapshot();
            Assert.That(firstSnapshot.Single(actor => actor.ActorId == "ally").LanePosition, Is.EqualTo(4d));
            Assert.That(firstSnapshot.Single(actor => actor.ActorId == "enemy").LanePosition, Is.EqualTo(6d));
            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Attack), Is.Zero);
            var started = simulation.Events.Single(item => item.Type == CombatEventType.MeleeBattlefieldStarted);
            Assert.That(started.ControlBlockColumn, Is.EqualTo(1));
            Assert.That(started.ControlBlockRow, Is.EqualTo(2));
            Assert.That(started.Column, Is.EqualTo(2));
            Assert.That(started.Row, Is.EqualTo(5));

            simulation.Advance(0.75d);

            Assert.That(simulation.Events.Count(item => item.Type == CombatEventType.Attack), Is.EqualTo(2));
        }

        [Test]
        public void FasterSide_SelectsWhichAdjacentControlBlockHostsBattle()
        {
            var fastAllySimulation = new CombatSimulation(3, 6);
            fastAllySimulation.TryAddActor(
                "ally",
                Team.Ally,
                new CombatantDefinition("U-FAST", "Staff", 10, 1d, 3d, 1d, 1),
                2,
                3d);
            fastAllySimulation.TryAddActor(
                "enemy",
                Team.Enemy,
                new CombatantDefinition("E-SLOW", "Staff", 10, 1d, 1d, 1d, 1),
                2,
                5d);
            fastAllySimulation.Advance(1d);
            var allyFirst = fastAllySimulation.CreateSnapshot();
            Assert.That(allyFirst.Single(actor => actor.Team == Team.Ally).LanePosition, Is.EqualTo(4d));
            Assert.That(allyFirst.Single(actor => actor.Team == Team.Enemy).LanePosition, Is.EqualTo(6d));

            var fastEnemySimulation = new CombatSimulation(3, 6);
            fastEnemySimulation.TryAddActor(
                "ally",
                Team.Ally,
                new CombatantDefinition("U-SLOW", "Staff", 10, 1d, 1d, 1d, 1),
                2,
                2d);
            fastEnemySimulation.TryAddActor(
                "enemy",
                Team.Enemy,
                new CombatantDefinition("E-FAST", "Staff", 10, 1d, 4d, 1d, 1),
                2,
                5d);
            fastEnemySimulation.Advance(1d);
            var enemyFirst = fastEnemySimulation.CreateSnapshot();
            Assert.That(enemyFirst.Single(actor => actor.Team == Team.Ally).LanePosition, Is.EqualTo(1d));
            Assert.That(enemyFirst.Single(actor => actor.Team == Team.Enemy).LanePosition, Is.EqualTo(3d));
        }

        [Test]
        public void MultipleAlliesInSameControlBlock_CanFocusOneEnemy()
        {
            var allyDefinition = new CombatantDefinition("U-CROWD", "Staff", 10, 1d, 1d, 0.1d, 1);
            var enemyDefinition = new CombatantDefinition("E-CROWD", "Staff", 3, 0d, 1d, 0.1d, 1);
            var simulation = new CombatSimulation(3, 6);
            simulation.TryAddActor("ally-a", Team.Ally, allyDefinition, 1, 5d);
            simulation.TryAddActor("ally-b", Team.Ally, allyDefinition, 3, 5d);
            simulation.TryAddActor("enemy", Team.Enemy, enemyDefinition, 2, 5d);

            simulation.Advance(0.1d);

            var attackingAllies = simulation.Events
                .Where(item => item.Type == CombatEventType.Attack && item.ActorId.StartsWith("ally-"))
                .Select(item => item.ActorId)
                .Distinct()
                .ToArray();
            Assert.That(attackingAllies, Is.EquivalentTo(new[] { "ally-a", "ally-b" }));
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "enemy").HitPoints,
                Is.EqualTo(1d));
        }

        [Test]
        public void MovingAlly_ApproachesAndDestroysStationaryEnemyBuilding()
        {
            var simulation = new CombatSimulation(3, 9);
            var ally = new CombatantDefinition("U01", "Staff", 5, 2d, 1d, 1d, 1);
            var building = new CombatantDefinition("E06", "Building", 4, 0d, 0d, 0d, 0, 1);
            simulation.TryAddActor("ally", Team.Ally, ally, 2, 6d);
            simulation.TryAddActor("building", Team.Enemy, building, 2, 9d);

            simulation.Advance(3d);
            simulation.Advance(2d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(item => item.ActorId == "building").IsAlive, Is.False);
            Assert.That(snapshot.Single(item => item.ActorId == "building").IsBuilding, Is.True);
            Assert.That(snapshot.Single(item => item.ActorId == "building").AssaultScoreReward, Is.EqualTo(1));
            Assert.That(
                simulation.Events.Count(item => item.Type == CombatEventType.Death && item.ActorId == "building"),
                Is.EqualTo(1));
        }

        private static CombatSimulation CreateDuel(double allyLane, double enemyLane)
        {
            var simulation = new CombatSimulation(9, 9);
            Assert.That(simulation.TryAddActor("ally", Team.Ally, U01, 3, allyLane), Is.True);
            Assert.That(simulation.TryAddActor("enemy", Team.Enemy, E01, 3, enemyLane), Is.True);
            return simulation;
        }

        private static BattleGrid CreateTerritoryGrid()
        {
            var grid = new BattleGrid(9, 9);
            for (var column = 1; column <= 9; column++)
            {
                for (var row = 1; row <= 6; row++)
                {
                    grid.SetOwnership(new GridPosition(column, row), true);
                }
            }

            return grid;
        }
    }
}
