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
        }

        private static CombatSimulation CreateDuel(double allyLane, double enemyLane)
        {
            var simulation = new CombatSimulation(9, 9);
            Assert.That(simulation.TryAddActor("ally", Team.Ally, U01, 3, allyLane), Is.True);
            Assert.That(simulation.TryAddActor("enemy", Team.Enemy, E01, 3, enemyLane), Is.True);
            return simulation;
        }
    }
}
