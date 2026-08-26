using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class FormalCampaignProgressionTests
    {
        [Test]
        public void Campaign_StartsAtMenuWithOnlyL02Unlocked()
        {
            var campaign = CreateCampaign();

            Assert.That(campaign.Screen, Is.EqualTo(FormalFlowScreen.MainMenu));
            Assert.That(campaign.IsUnlocked("L02"), Is.True);
            Assert.That(campaign.IsUnlocked("L03"), Is.False);
            Assert.That(campaign.TryStartLevel("L03"), Is.False);
        }

        [Test]
        public void Victory_UnlocksNextLevelAndSupportsContinuousPlay()
        {
            var campaign = CreateCampaign();

            Assert.That(campaign.TryStartLevel("L02"), Is.True);
            Assert.That(campaign.RecordResult("L02", BattleState.Victory), Is.True);
            Assert.That(campaign.IsCompleted("L02"), Is.True);
            Assert.That(campaign.IsUnlocked("L03"), Is.True);
            Assert.That(campaign.GetNextLevelId(), Is.EqualTo("L03"));
            Assert.That(campaign.TryStartLevel(campaign.GetNextLevelId()), Is.True);
            Assert.That(campaign.ActiveLevelId, Is.EqualTo("L03"));
        }

        [Test]
        public void Defeat_DoesNotUnlockNextLevelAndCanRestart()
        {
            var campaign = CreateCampaign();
            campaign.TryStartLevel("L02");

            Assert.That(campaign.RecordResult("L02", BattleState.Defeat), Is.True);
            Assert.That(campaign.IsUnlocked("L03"), Is.False);
            Assert.That(campaign.RestartActiveLevel(), Is.True);
            Assert.That(campaign.Screen, Is.EqualTo(FormalFlowScreen.Battle));
            Assert.That(campaign.LastResult, Is.EqualTo(BattleState.Running));
        }

        [Test]
        public void Pause_OnlyTogglesDuringActiveBattle()
        {
            var campaign = CreateCampaign();
            Assert.That(campaign.TogglePause(), Is.False);
            campaign.TryStartLevel("L02");
            Assert.That(campaign.TogglePause(), Is.True);
            Assert.That(campaign.Screen, Is.EqualTo(FormalFlowScreen.Paused));
            Assert.That(campaign.TogglePause(), Is.True);
            Assert.That(campaign.Screen, Is.EqualTo(FormalFlowScreen.Battle));
        }

        private static FormalCampaignProgression CreateCampaign()
        {
            return new FormalCampaignProgression(new[] { "L02", "L03", "L04", "L05" });
        }
    }
}
