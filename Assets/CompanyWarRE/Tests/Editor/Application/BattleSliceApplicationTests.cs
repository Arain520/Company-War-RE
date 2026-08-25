using System.Linq;
using CompanyWarRE.Domain;
using NUnit.Framework;
using QFramework;

namespace CompanyWarRE.Application.Tests
{
    public sealed class BattleSliceApplicationTests
    {
        private IArchitecture _architecture;

        [SetUp]
        public void SetUp()
        {
            _architecture = BattleSliceArchitecture.Interface;
            _architecture.SendCommand(new ConfigureBattleSliceCommand(CreateConfiguration()));
        }

        [TearDown]
        public void TearDown()
        {
            _architecture.Deinit();
        }

        [Test]
        public void Reset_CreatesSixBySixGridWithOwnedBottomHalf()
        {
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.Cells.Count, Is.EqualTo(36));
            Assert.That(snapshot.Cells.Count(cell => cell.IsOwned), Is.EqualTo(18));
            Assert.That(snapshot.Cells.Where(cell => cell.IsOwned).All(cell => cell.Position.Row <= 3), Is.True);
            Assert.That(snapshot.Resources, Is.EqualTo(10));
            Assert.That(snapshot.UnitId, Is.EqualTo("U01"));
            Assert.That(snapshot.UnitResourceCost, Is.EqualTo(1));
            Assert.That(snapshot.RemainingCooldown, Is.Zero);
            Assert.That(snapshot.Combatants, Is.Empty);
            Assert.That(snapshot.WaveIndex, Is.Zero);
            Assert.That(snapshot.EnemySpawns, Is.Empty);
        }

        [Test]
        public void DeployCommand_ConnectsQFrameworkToDomainRules()
        {
            var first = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(4, 1),
                "test-unit-01"));
            var cooldownRejection = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(4, 2),
                "test-unit-02"));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(cooldownRejection.Failure, Is.EqualTo(DeploymentFailure.CooldownActive));
            Assert.That(_architecture.SendQuery(new GetBattleSliceSnapshotQuery()).Resources, Is.EqualTo(9));

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(3d));
            var second = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(4, 2),
                "test-unit-02"));

            Assert.That(second.Succeeded, Is.True);
        }

        [Test]
        public void PollutionCommand_ChangesExactlyOneThreeByThreeBlock()
        {
            var changed = _architecture.SendCommand(
                new ToggleBattleSlicePollutionCommand(new GridPosition(2, 2)));
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(changed, Is.EqualTo(9));
            Assert.That(snapshot.Cells.Count(cell => cell.IsPolluted), Is.EqualTo(9));
        }

        [Test]
        public void UnownedCell_IsRejectedThroughTheApplicationBoundary()
        {
            var response = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(4, 4),
                "test-unit-01"));

            Assert.That(response.Succeeded, Is.False);
            Assert.That(response.Failure, Is.EqualTo(DeploymentFailure.TerritoryNotOwned));
        }

        [Test]
        public void AdvanceCommand_RunsTheU01VersusE01DomainCombatSlice()
        {
            var response = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(3, 3),
                "test-unit-01"));

            Assert.That(response.Succeeded, Is.True);
            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(1d));
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.Combatants.Count, Is.EqualTo(2));
            Assert.That(snapshot.Combatants.All(actor => !actor.IsAlive), Is.True);
            Assert.That(snapshot.CombatEvents.Count(item => item.Type == CombatEventType.Attack), Is.EqualTo(2));
            Assert.That(snapshot.CombatEvents.Count(item => item.Type == CombatEventType.Death), Is.EqualTo(2));
            Assert.That(snapshot.Cells.Single(cell => cell.Position.Equals(new GridPosition(3, 3))).OccupantCount, Is.Zero);
        }

        [Test]
        public void EnemyEnteringOwnedRows_PollutesThreeByThreeBlockAndSelfDestructs()
        {
            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(1d));
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.Cells.Count(cell => cell.IsPolluted), Is.EqualTo(9));
            Assert.That(snapshot.Cells.Count(cell => cell.IsOwned), Is.EqualTo(9));
            Assert.That(snapshot.Combatants.Single().IsAlive, Is.False);
            var pollution = snapshot.CombatEvents.Single(item => item.Type == CombatEventType.Pollution);
            Assert.That(pollution.Column, Is.EqualTo(3));
            Assert.That(pollution.Row, Is.EqualTo(3));
            Assert.That(pollution.ControlBlockColumn, Is.EqualTo(1));
            Assert.That(pollution.ControlBlockRow, Is.EqualTo(1));
        }

        [Test]
        public void WaveSchedule_AddsEnemiesThroughTheApplicationBoundary()
        {
            _architecture.SendCommand(new ConfigureBattleSliceCommand(CreateWaveConfiguration()));

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(2d));
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.WaveIndex, Is.EqualTo(1));
            Assert.That(snapshot.CurrentWaveStage, Is.EqualTo("WaveTest"));
            Assert.That(snapshot.EnemySpawns.Count, Is.EqualTo(2));
            Assert.That(snapshot.EnemySpawns.Select(item => item.Position.Column).Distinct().Count(), Is.EqualTo(2));
            Assert.That(snapshot.Combatants.Count, Is.EqualTo(2));
        }

        [Test]
        public void DestroyingLastEnemyBuilding_UpdatesSpawnPointScoreAndVictory()
        {
            _architecture.SendCommand(new ConfigureBattleSliceCommand(CreateBuildingConfiguration()));
            var initial = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            Assert.That(initial.EnemyBuildingCount, Is.EqualTo(1));
            Assert.That(initial.Combatants.Single(item => item.IsBuilding).LanePosition, Is.EqualTo(6d));
            Assert.That(initial.Combatants.Single(item => item.IsBuilding).FootprintStartColumn, Is.EqualTo(1));
            Assert.That(initial.Combatants.Single(item => item.IsBuilding).FootprintEndColumn, Is.EqualTo(3));
            Assert.That(initial.Combatants.Single(item => item.IsBuilding).FootprintStartRow, Is.EqualTo(4));
            Assert.That(initial.Combatants.Single(item => item.IsBuilding).FootprintEndRow, Is.EqualTo(6));
            Assert.That(initial.Cells.Count(item => item.IsBlockedByBuilding), Is.EqualTo(9));
            Assert.That(initial.ValidSpawnPointCount, Is.EqualTo(1));

            var deployment = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(3, 3),
                "building-attacker"));
            Assert.That(deployment.Succeeded, Is.True);
            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(3d));
            var completed = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(completed.EnemyBuildingCount, Is.Zero);
            Assert.That(completed.AssaultScore, Is.EqualTo(2));
            Assert.That(completed.BattleState, Is.EqualTo(BattleState.Victory));
            Assert.That(completed.CurrentWaveStage, Is.Empty);
            Assert.That(completed.Combatants.Single(item => item.IsBuilding).IsAlive, Is.False);
            Assert.That(completed.Cells.Any(item => item.IsBlockedByBuilding), Is.False);
            Assert.That(
                _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                    new GridPosition(4, 1),
                    "after-victory")).Failure,
                Is.EqualTo(DeploymentFailure.BattleEnded));
            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(10d));
            var stopped = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            Assert.That(stopped.ElapsedSeconds, Is.EqualTo(completed.ElapsedSeconds));
            Assert.That(stopped.WaveIndex, Is.EqualTo(completed.WaveIndex));

            _architecture.SendCommand(new ResetBattleSliceCommand());
            var reset = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            Assert.That(reset.BattleState, Is.EqualTo(BattleState.Running));
            Assert.That(reset.EnemyBuildingCount, Is.EqualTo(1));
            Assert.That(reset.AssaultScore, Is.Zero);
            Assert.That(reset.WaveIndex, Is.Zero);
            Assert.That(reset.Combatants.Single(item => item.IsBuilding).IsAlive, Is.True);
        }

        [Test]
        public void E13Curse_FlowsThroughApplicationSnapshotBeforeUnitAction()
        {
            _architecture.SendCommand(new ConfigureBattleSliceCommand(CreateCurseBuildingConfiguration()));
            var deployment = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(2, 6),
                "curse-target"));
            Assert.That(deployment.Succeeded, Is.True);

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(10d));

            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            Assert.That(snapshot.Combatants.Single(item => item.ActorId == "curse-target").IsAlive, Is.False);
            Assert.That(snapshot.Combatants.Single(item => item.TemplateId == "E13").HitPoints, Is.EqualTo(18d));
            Assert.That(
                snapshot.CombatEvents.Any(item =>
                    item.Type == CombatEventType.Attack && item.ActorId == "curse-target"),
                Is.False);
        }

        [Test]
        public void E14KillHeal_FlowsThroughApplicationSnapshot()
        {
            _architecture.SendCommand(new ConfigureBattleSliceCommand(CreateKillHealBuildingConfiguration()));
            var deployment = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(2, 6),
                "kill-heal-target"));
            Assert.That(deployment.Succeeded, Is.True);

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(1d));

            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            Assert.That(snapshot.Combatants.Single(item => item.ActorId == "kill-heal-target").IsAlive, Is.False);
            var e14 = snapshot.Combatants.Single(item => item.TemplateId == "E14");
            Assert.That(e14.HitPoints, Is.EqualTo(21d));
            Assert.That(e14.MaximumHitPoints, Is.EqualTo(20d));
        }

        private static BattleSliceConfiguration CreateConfiguration()
        {
            return new BattleSliceConfiguration(
                6,
                6,
                3,
                10,
                5d,
                3d,
                new GridPosition(2, 2),
                1,
                new UnitDefinition("U01", 1, 3d),
                new CombatantDefinition("U01", "Staff", 1, 1d, 1d, 1d, 1),
                new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1),
                new GridPosition(3, 4));
        }

        private static BattleSliceConfiguration CreateWaveConfiguration()
        {
            return new BattleSliceConfiguration(
                6,
                6,
                3,
                10,
                5d,
                3d,
                new GridPosition(2, 2),
                1,
                new UnitDefinition("U01", 1, 3d),
                new CombatantDefinition("U01", "Staff", 1, 1d, 1d, 1d, 1),
                new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1),
                new GridPosition(3, 6),
                new[]
                {
                    new EnemyWaveStage(
                        "WaveTest",
                        30d,
                        2d,
                        2,
                        new[] { new EnemySpawnWeight("E01", 1) })
                },
                null,
                new[] { 1, 4 },
                17);
        }

        private static BattleSliceConfiguration CreateBuildingConfiguration()
        {
            var building = new CombatantDefinition("E06", "Building", 1, 0d, 0d, 0d, 0, 2);
            return new BattleSliceConfiguration(
                6,
                6,
                3,
                10,
                5d,
                3d,
                new GridPosition(2, 2),
                1,
                new UnitDefinition("U01", 1, 3d),
                new CombatantDefinition("U01", "Staff", 5, 1d, 1d, 1d, 1),
                new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1),
                new GridPosition(3, 6),
                new[]
                {
                    new EnemyWaveStage(
                        "DeferredWave",
                        200d,
                        100d,
                        1,
                        new[] { new EnemySpawnWeight("E01", 1) })
                },
                new[] { building },
                new[] { 3 },
                17,
                new[] { new EnemyBuildingPlacement("E06", new GridPosition(3, 6)) },
                8,
                true,
                true);
        }

        private static BattleSliceConfiguration CreateCurseBuildingConfiguration()
        {
            var e13 = new CombatantDefinition("E13", "Building", 18, 1d, 0d, 0.5d, 3, 5);
            return new BattleSliceConfiguration(
                9,
                9,
                6,
                10,
                5d,
                3d,
                new GridPosition(2, 2),
                1,
                new UnitDefinition("U01", 1, 3d),
                new CombatantDefinition("U01", "Support", 1, 20d, 0d, 0.1d, 99),
                new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1),
                new GridPosition(3, 9),
                new[]
                {
                    new EnemyWaveStage(
                        "DeferredWave",
                        200d,
                        100d,
                        1,
                        new[] { new EnemySpawnWeight("E01", 1) })
                },
                new[] { e13 },
                new[] { 3 },
                17,
                new[] { new EnemyBuildingPlacement("E13", new GridPosition(2, 9)) },
                5,
                true,
                true);
        }

        private static BattleSliceConfiguration CreateKillHealBuildingConfiguration()
        {
            var e14 = new CombatantDefinition("E14", "Building", 20, 1d, 0d, 1d, 1, 5);
            return new BattleSliceConfiguration(
                9,
                9,
                6,
                10,
                5d,
                3d,
                new GridPosition(2, 2),
                1,
                new UnitDefinition("U01", 1, 3d),
                new CombatantDefinition("U01", "Staff", 1, 0d, 3d, 1d, 1),
                new CombatantDefinition("E01", "Staff", 1, 1d, 1d, 1d, 1, 1),
                new GridPosition(3, 9),
                new[]
                {
                    new EnemyWaveStage(
                        "DeferredWave",
                        200d,
                        100d,
                        1,
                        new[] { new EnemySpawnWeight("E01", 1) })
                },
                new[] { e14 },
                new[] { 3 },
                17,
                new[] { new EnemyBuildingPlacement("E14", new GridPosition(2, 9)) },
                5,
                true,
                true);
        }
    }
}
