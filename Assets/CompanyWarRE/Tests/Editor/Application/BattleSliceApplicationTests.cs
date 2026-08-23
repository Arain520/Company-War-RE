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
        public void Reset_CreatesSixBySixGridWithOwnedLeftHalf()
        {
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.Cells.Count, Is.EqualTo(36));
            Assert.That(snapshot.Cells.Count(cell => cell.IsOwned), Is.EqualTo(18));
            Assert.That(snapshot.Resources, Is.EqualTo(10));
            Assert.That(snapshot.UnitId, Is.EqualTo("U01"));
            Assert.That(snapshot.UnitResourceCost, Is.EqualTo(1));
            Assert.That(snapshot.RemainingCooldown, Is.Zero);
            Assert.That(snapshot.Combatants.Count, Is.EqualTo(1));
            Assert.That(snapshot.Combatants.Single().TemplateId, Is.EqualTo("E01"));
            Assert.That(snapshot.Combatants.Single().Team, Is.EqualTo(Team.Enemy));
        }

        [Test]
        public void DeployCommand_ConnectsQFrameworkToDomainRules()
        {
            var first = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(1, 1),
                "test-unit-01"));
            var cooldownRejection = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(1, 2),
                "test-unit-02"));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(cooldownRejection.Failure, Is.EqualTo(DeploymentFailure.CooldownActive));
            Assert.That(_architecture.SendQuery(new GetBattleSliceSnapshotQuery()).Resources, Is.EqualTo(9));

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(3d));
            var second = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(1, 2),
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
                new GridPosition(4, 1),
                "test-unit-01"));

            Assert.That(response.Succeeded, Is.False);
            Assert.That(response.Failure, Is.EqualTo(DeploymentFailure.TerritoryNotOwned));
        }

        [Test]
        public void AdvanceCommand_RunsTheU01VersusE01DomainCombatSlice()
        {
            var response = _architecture.SendCommand(new DeployBattleSliceUnitCommand(
                new GridPosition(3, 5),
                "test-unit-01"));

            Assert.That(response.Succeeded, Is.True);
            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(1d));
            var snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());

            Assert.That(snapshot.Combatants.Count, Is.EqualTo(2));
            Assert.That(snapshot.Combatants.All(actor => !actor.IsAlive), Is.True);
            Assert.That(snapshot.CombatEvents.Count(item => item.Type == CombatEventType.Attack), Is.EqualTo(2));
            Assert.That(snapshot.CombatEvents.Count(item => item.Type == CombatEventType.Death), Is.EqualTo(2));
            Assert.That(snapshot.Cells.Single(cell => cell.Position.Equals(new GridPosition(3, 5))).OccupantCount, Is.Zero);
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
                new GridPosition(3, 6));
        }
    }
}
