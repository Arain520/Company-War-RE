using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class ResourceEconomyBehaviorTests
    {
        [Test]
        public void U09AuthorizationProducer_PreservesFractionalScoreRateAndStopsAfterRemoval()
        {
            var score = new AuthorizationScoreEconomy();
            var position = new GridPosition(2, 2);
            score.RegisterProducer(position, 0.33d);

            score.Advance(3d);
            Assert.That(score.Points, Is.EqualTo(0));
            score.Advance(0.04d);
            Assert.That(score.Points, Is.EqualTo(1));
            score.UnregisterProducer(position);
            score.Advance(100d);

            Assert.That(score.Points, Is.EqualTo(1));
        }

        [Test]
        public void Production_PreservesLegacyFixedAndTransmitterCadence()
        {
            var economy = new ResourceEconomy();
            economy.Reset(5);
            economy.ConfigureProduction(2d, 3d);
            economy.RegisterTransmitter(new GridPosition(1, 1), 2);

            economy.Advance();
            economy.Advance();
            economy.Advance();

            Assert.That(economy.Resources, Is.EqualTo(8));
            Assert.That(economy.TickCount, Is.EqualTo(3));
        }

        [Test]
        public void GainAndSpend_PreserveLegacyBoundaryRules()
        {
            var economy = new ResourceEconomy();
            economy.Reset(-10);
            economy.Gain(0);
            economy.Gain(-5);

            Assert.That(economy.Resources, Is.Zero);
            Assert.That(economy.TrySpend(0), Is.True);
            Assert.That(economy.TrySpend(-5), Is.True);
            Assert.That(economy.TrySpend(1), Is.False);
            economy.Gain(3);
            Assert.That(economy.TrySpend(2), Is.True);
            Assert.That(economy.Resources, Is.EqualTo(1));
        }

        [Test]
        public void DeploymentCooldown_IsTrackedByUnitId()
        {
            var economy = new ResourceEconomy();
            var unit = new UnitDefinition("U01", 2, 2d);
            economy.Reset(10);

            Assert.That(economy.TryDeploy(unit), Is.True);
            Assert.That(economy.CanDeploy(unit), Is.False);
            economy.Advance(1d);
            Assert.That(economy.GetRemainingCooldown(unit), Is.EqualTo(1d).Within(0.0001d));
            economy.Advance(1d);
            Assert.That(economy.CanDeploy(unit), Is.True);
        }
    }
}
