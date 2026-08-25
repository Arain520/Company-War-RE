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

        [Test]
        public void NineCellBuilding_CanBeFocusedFromEitherFootprintEdge()
        {
            var simulation = new CombatSimulation(9, 9);
            var ally = new CombatantDefinition("U01", "Staff", 5, 1d, 1d, 1d, 1);
            var building = new CombatantDefinition("E06", "Building", 2, 0d, 0d, 0d, 0, 1);
            simulation.TryAddActor("ally-left", Team.Ally, ally, 4, 8.88d);
            simulation.TryAddActor("ally-right", Team.Ally, ally, 6, 8.88d);
            simulation.TryAddActor("building", Team.Enemy, building, 5, 9d);

            simulation.Advance(1d);

            var buildingSnapshot = simulation.CreateSnapshot().Single(item => item.ActorId == "building");
            Assert.That(buildingSnapshot.IsAlive, Is.False);
            Assert.That(buildingSnapshot.FootprintStartColumn, Is.EqualTo(4));
            Assert.That(buildingSnapshot.FootprintEndColumn, Is.EqualTo(6));
            Assert.That(buildingSnapshot.FootprintStartRow, Is.EqualTo(7));
            Assert.That(buildingSnapshot.FootprintEndRow, Is.EqualTo(9));
            Assert.That(
                buildingSnapshot.FootprintColumns * buildingSnapshot.FootprintRows,
                Is.EqualTo(9));
            Assert.That(
                simulation.Events
                    .Where(item => item.Type == CombatEventType.Attack && item.TargetActorId == "building")
                    .Select(item => item.ActorId),
                Is.EquivalentTo(new[] { "ally-left", "ally-right" }));
        }

        [Test]
        public void E06E07AndE12_AreStationaryDataOnlyBuildings()
        {
            var simulation = new CombatSimulation(9, 9);
            var observer = new CombatantDefinition("observer", "Support", 20, 0d, 0d, 1d, 1);
            var buildings = new[]
            {
                new CombatantDefinition("E06", "Building", 7, 0d, 0d, 0d, 0, 1),
                new CombatantDefinition("E07", "Building", 12, 0d, 0d, 0d, 0, 2),
                new CombatantDefinition("E12", "Building", 15, 0d, 0d, 0d, 0, 2)
            };

            for (var index = 0; index < buildings.Length; index++)
            {
                var column = 2 + index * 3;
                Assert.That(
                    simulation.TryAddActor("building-" + buildings[index].Id, Team.Enemy, buildings[index], column, 9d),
                    Is.True);
                Assert.That(
                    simulation.TryAddActor("observer-" + index, Team.Ally, observer, column, 8.5d),
                    Is.True);
            }

            simulation.Advance(30d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(
                snapshot.Where(actor => actor.IsBuilding).All(actor => actor.LanePosition == 9d),
                Is.True);
            Assert.That(
                snapshot.Where(actor => actor.Team == Team.Ally).All(actor => actor.HitPoints == 20d),
                Is.True);
            Assert.That(simulation.Events.Any(item => item.Type == CombatEventType.Attack), Is.False);
        }

        [Test]
        public void E13Curse_AccumulatesFractionalDamageAndStacksLivingSources()
        {
            var simulation = new CombatSimulation(15, 12);
            var e13 = new CombatantDefinition("E13", "Building", 18, 1d, 0d, 0.5d, 3, 5);
            var ally = new CombatantDefinition("U-Target", "Support", 5, 0d, 0d, 1d, 1);
            Assert.That(simulation.TryAddActor("curse-a", Team.Enemy, e13, 2, 11d), Is.True);
            Assert.That(simulation.TryAddActor("curse-b", Team.Enemy, e13, 8, 11d), Is.True);
            Assert.That(simulation.TryAddActor("ally", Team.Ally, ally, 5, 8d), Is.True);

            simulation.Advance(4.9d);
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "ally").HitPoints,
                Is.EqualTo(5d));

            simulation.Advance(0.1d);
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "ally").HitPoints,
                Is.EqualTo(4d));
            Assert.That(simulation.Events.Any(item => item.Type == CombatEventType.Attack), Is.False);
        }

        [Test]
        public void E13Curse_UsesControlBlockRangeAndIncludesAlliedBuildings()
        {
            var simulation = new CombatSimulation(12, 12);
            var e13 = new CombatantDefinition("E13", "Building", 18, 1d, 0d, 0.5d, 3, 5);
            var ally = new CombatantDefinition("U-Target", "Support", 5, 0d, 0d, 1d, 1);
            var alliedBuilding = new CombatantDefinition("U08", "Building", 5, 0d, 0d, 1d, 1);
            simulation.TryAddActor("curse", Team.Enemy, e13, 2, 11d);
            simulation.TryAddActor("inside", Team.Ally, ally, 8, 8d);
            simulation.TryAddActor("outside", Team.Ally, ally, 11, 2d);
            simulation.TryAddActor("allied-building", Team.Ally, alliedBuilding, 5, 5d);

            simulation.Advance(10d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(actor => actor.ActorId == "inside").HitPoints, Is.EqualTo(4d));
            Assert.That(snapshot.Single(actor => actor.ActorId == "allied-building").HitPoints, Is.EqualTo(4d));
            Assert.That(snapshot.Single(actor => actor.ActorId == "outside").HitPoints, Is.EqualTo(5d));
        }

        [Test]
        public void E13Curse_ResolvesBeforeMovementAndAttackPhases()
        {
            var simulation = new CombatSimulation(9, 9);
            var e13 = new CombatantDefinition("E13", "Building", 18, 1d, 0d, 0.5d, 3, 5);
            var fragileAttacker = new CombatantDefinition("U-Fragile", "Support", 1, 20d, 0d, 0.1d, 99);
            simulation.TryAddActor("curse", Team.Enemy, e13, 2, 9d);
            simulation.TryAddActor("fragile", Team.Ally, fragileAttacker, 2, 6d);

            simulation.Advance(10d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(actor => actor.ActorId == "fragile").IsAlive, Is.False);
            Assert.That(snapshot.Single(actor => actor.ActorId == "curse").HitPoints, Is.EqualTo(18d));
            Assert.That(
                simulation.Events.Any(item =>
                    item.Type == CombatEventType.Attack && item.ActorId == "fragile"),
                Is.False);
        }

        [Test]
        public void E14KillHeal_AddsOneHpAndCanExceedInitialDurability()
        {
            var simulation = new CombatSimulation(9, 9);
            var e14 = new CombatantDefinition("E14", "Building", 20, 1d, 0d, 1d, 1, 5);
            var target = new CombatantDefinition("U-Target", "Support", 1, 0d, 0d, 1d, 1);
            simulation.TryAddActor("e14", Team.Enemy, e14, 2, 9d);
            simulation.TryAddActor("target", Team.Ally, target, 2, 8.5d);

            simulation.Advance(1d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(actor => actor.ActorId == "target").IsAlive, Is.False);
            Assert.That(snapshot.Single(actor => actor.ActorId == "e14").HitPoints, Is.EqualTo(21d));
            Assert.That(snapshot.Single(actor => actor.ActorId == "e14").MaximumHitPoints, Is.EqualTo(20d));
        }

        [Test]
        public void E14KillHeal_RequiresE14ToLandTheKillingDamage()
        {
            var simulation = new CombatSimulation(9, 9);
            var firstAttacker = new CombatantDefinition("E-First", "Support", 5, 1d, 0d, 1d, 1);
            var e14 = new CombatantDefinition("E14", "Building", 20, 1d, 0d, 1d, 1, 5);
            var target = new CombatantDefinition("U-Target", "Support", 1, 0d, 0d, 1d, 1);
            simulation.TryAddActor("first", Team.Enemy, firstAttacker, 2, 9d);
            simulation.TryAddActor("e14", Team.Enemy, e14, 2, 9d);
            simulation.TryAddActor("target", Team.Ally, target, 2, 8.5d);

            simulation.Advance(1d);

            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "e14").HitPoints,
                Is.EqualTo(20d));
            Assert.That(
                simulation.Events.Count(item =>
                    item.Type == CombatEventType.Attack && item.TargetActorId == "target"),
                Is.EqualTo(2));
        }

        [Test]
        public void E14PendingKillHeal_CanReviveItBeforeDeathReporting()
        {
            var simulation = new CombatSimulation(9, 9);
            var ally = new CombatantDefinition("U-Duelist", "Support", 1, 1d, 0d, 1d, 1);
            var e14 = new CombatantDefinition("E14", "Building", 1, 1d, 0d, 1d, 1, 5);
            simulation.TryAddActor("ally", Team.Ally, ally, 2, 8.5d);
            simulation.TryAddActor("e14", Team.Enemy, e14, 2, 9d);

            simulation.Advance(1d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(actor => actor.ActorId == "ally").IsAlive, Is.False);
            Assert.That(snapshot.Single(actor => actor.ActorId == "e14").HitPoints, Is.EqualTo(1d));
            Assert.That(
                simulation.Events.Any(item =>
                    item.Type == CombatEventType.Death && item.ActorId == "e14"),
                Is.False);
        }

        [Test]
        public void E15Execution_WaitsForFifteenSecondInitialDelayAndRetainsRemainder()
        {
            var simulation = new CombatSimulation(9, 9);
            var e15 = new CombatantDefinition("E15", "Building", 15, 0d, 0d, 12d, 99, 5);
            var target = new CombatantDefinition("U-Target", "Support", 3, 0d, 0d, 1d, 1);
            simulation.TryAddActor("e15", Team.Enemy, e15, 2, 9d);
            simulation.TryAddActor("target", Team.Ally, target, 5, 6d);

            simulation.Advance(14.9d);

            var waiting = simulation.CreateSnapshot();
            Assert.That(waiting.Single(actor => actor.ActorId == "target").IsAlive, Is.True);
            Assert.That(
                waiting.Single(actor => actor.ActorId == "e15").AttackProgress,
                Is.EqualTo(11.9d).Within(0.0001d));

            simulation.Advance(0.2d);

            var completed = simulation.CreateSnapshot();
            Assert.That(completed.Single(actor => actor.ActorId == "target").IsAlive, Is.False);
            Assert.That(
                completed.Single(actor => actor.ActorId == "e15").AttackProgress,
                Is.EqualTo(0.1d).Within(0.0001d));
            var execution = simulation.Events.Single(item =>
                item.Type == CombatEventType.Attack && item.ActorId == "e15");
            Assert.That(execution.TargetActorId, Is.EqualTo("target"));
            Assert.That(execution.Amount, Is.EqualTo(3d));
        }

        [Test]
        public void E15Execution_ExcludesBuildingsAndEnemyVisibleStealthActors()
        {
            var simulation = new CombatSimulation(15, 12);
            var e15 = new CombatantDefinition("E15", "Building", 15, 0d, 0d, 12d, 99, 5);
            var excludedBuilding = new CombatantDefinition("U08", "Building", 1, 0d, 0d, 1d, 1);
            var stealthU30 = new CombatantDefinition("U30", "Support", 1, 0d, 0d, 1d, 1);
            var stealthU31 = new CombatantDefinition("U31", "Support", 1, 0d, 0d, 1d, 1);
            var eligible = new CombatantDefinition("U01", "Support", 2, 0d, 0d, 1d, 1);
            simulation.TryAddActor("e15", Team.Enemy, e15, 2, 11d);
            simulation.TryAddActor("building", Team.Ally, excludedBuilding, 5, 8d);
            simulation.TryAddActor("u30", Team.Ally, stealthU30, 8, 8d);
            simulation.TryAddActor("u31", Team.Ally, stealthU31, 11, 8d);
            simulation.TryAddActor("eligible", Team.Ally, eligible, 14, 8d);

            simulation.Advance(15d);

            var snapshot = simulation.CreateSnapshot();
            Assert.That(snapshot.Single(actor => actor.ActorId == "building").IsAlive, Is.True);
            Assert.That(snapshot.Single(actor => actor.ActorId == "u30").IsAlive, Is.True);
            Assert.That(snapshot.Single(actor => actor.ActorId == "u31").IsAlive, Is.True);
            Assert.That(snapshot.Single(actor => actor.ActorId == "eligible").IsAlive, Is.False);
            Assert.That(
                simulation.Events.Single(item =>
                    item.Type == CombatEventType.Attack && item.ActorId == "e15").TargetActorId,
                Is.EqualTo("eligible"));
        }

        [Test]
        public void E15Execution_UsesHpThenControlBlockThenSmallCellDistance()
        {
            var e15 = new CombatantDefinition("E15", "Building", 15, 0d, 0d, 12d, 99, 5);

            var hpPriority = new CombatSimulation(15, 12);
            hpPriority.TryAddActor("e15", Team.Enemy, e15, 2, 11d);
            hpPriority.TryAddActor(
                "low-hp-far",
                Team.Ally,
                new CombatantDefinition("U-Low", "Support", 1, 0d, 0d, 1d, 1),
                14,
                2d);
            hpPriority.TryAddActor(
                "high-hp-near",
                Team.Ally,
                new CombatantDefinition("U-High", "Support", 2, 0d, 0d, 1d, 1),
                2,
                8d);
            hpPriority.Advance(15d);
            Assert.That(
                hpPriority.Events.Single(item => item.Type == CombatEventType.Attack).TargetActorId,
                Is.EqualTo("low-hp-far"));

            var blockPriority = new CombatSimulation(15, 12);
            blockPriority.TryAddActor("e15", Team.Enemy, e15, 2, 11d);
            blockPriority.TryAddActor(
                "far-block",
                Team.Ally,
                new CombatantDefinition("U-Far", "Support", 1, 0d, 0d, 1d, 1),
                14,
                2d);
            blockPriority.TryAddActor(
                "near-block",
                Team.Ally,
                new CombatantDefinition("U-Near", "Support", 1, 0d, 0d, 1d, 1),
                2,
                8d);
            blockPriority.Advance(15d);
            Assert.That(
                blockPriority.Events.Single(item => item.Type == CombatEventType.Attack).TargetActorId,
                Is.EqualTo("near-block"));

            var cellPriority = new CombatSimulation(15, 12);
            cellPriority.TryAddActor("e15", Team.Enemy, e15, 2, 11d);
            cellPriority.TryAddActor(
                "far-cell",
                Team.Ally,
                new CombatantDefinition("U-FarCell", "Support", 1, 0d, 0d, 1d, 1),
                6,
                7d);
            cellPriority.TryAddActor(
                "near-cell",
                Team.Ally,
                new CombatantDefinition("U-NearCell", "Support", 1, 0d, 0d, 1d, 1),
                4,
                9d);
            cellPriority.Advance(15d);
            Assert.That(
                cellPriority.Events.Single(item => item.Type == CombatEventType.Attack).TargetActorId,
                Is.EqualTo("near-cell"));
        }

        [Test]
        public void E15Execution_LargeTickCastsMultipleTimesAndConsumesEmptyCycles()
        {
            var simulation = new CombatSimulation(15, 12);
            var e15 = new CombatantDefinition("E15", "Building", 15, 0d, 0d, 12d, 99, 5);
            var target = new CombatantDefinition("U-Target", "Support", 1, 0d, 0d, 1d, 1);
            simulation.TryAddActor("e15", Team.Enemy, e15, 2, 11d);
            simulation.TryAddActor("first", Team.Ally, target, 5, 8d);
            simulation.TryAddActor("second", Team.Ally, target, 8, 8d);

            simulation.Advance(28d);

            Assert.That(
                simulation.Events.Count(item => item.Type == CombatEventType.Attack && item.ActorId == "e15"),
                Is.EqualTo(2));
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "e15").AttackProgress,
                Is.EqualTo(1d).Within(0.0001d));

            simulation.Advance(24d);
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "e15").AttackProgress,
                Is.EqualTo(1d).Within(0.0001d));
            simulation.TryAddActor("late", Team.Ally, target, 11, 8d);
            simulation.Advance(10.9d);
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "late").IsAlive,
                Is.True);
            simulation.Advance(0.1d);
            Assert.That(
                simulation.CreateSnapshot().Single(actor => actor.ActorId == "late").IsAlive,
                Is.False);
        }

        [Test]
        public void E15Execution_KeepsPreviouslyRegisteredAllyAttackPending()
        {
            var simulation = new CombatSimulation(9, 9);
            var ally = new CombatantDefinition("U-Duelist", "Support", 1, 15d, 0d, 12d, 1);
            var e15 = new CombatantDefinition("E15", "Building", 15, 0d, 0d, 12d, 99, 5);
            simulation.TryAddActor("ally", Team.Ally, ally, 2, 8.5d);
            simulation.TryAddActor("e15", Team.Enemy, e15, 2, 9d);

            simulation.Advance(15d);

            Assert.That(simulation.CreateSnapshot().All(actor => !actor.IsAlive), Is.True);
            Assert.That(
                simulation.Events.Any(item =>
                    item.Type == CombatEventType.Attack &&
                    item.ActorId == "ally" &&
                    item.TargetActorId == "e15"),
                Is.True);
            Assert.That(
                simulation.Events.Any(item =>
                    item.Type == CombatEventType.Attack &&
                    item.ActorId == "e15" &&
                    item.TargetActorId == "ally"),
                Is.True);
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
