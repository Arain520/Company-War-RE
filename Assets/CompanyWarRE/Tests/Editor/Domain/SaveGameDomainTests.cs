using System.Linq;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class SaveGameDomainTests
    {
        [Test]
        public void CowCompatibleV1_UpgradesGrowthAndEconomyWithExplicitDefaults()
        {
            var draft = new SaveGameDraft(
                SaveSchema.CowCompatibleVersion,
                new CampaignSaveProgress("L03", new[]
                {
                    new LevelSaveProgress("L02", true, true, 3),
                    new LevelSaveProgress("L03", true, false, 0)
                }),
                null,
                null,
                GameSettingsSave.Default);

            var result = new SaveGameUpgrader().Upgrade(draft);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Version, Is.EqualTo(SaveSchema.CurrentVersion));
            Assert.That(result.Value.Growth.AuthorizationPoints, Is.Zero);
            Assert.That(result.Value.Growth.DeploymentUnitIds, Is.Empty);
            Assert.That(result.Value.Economy.Resources, Is.Zero);
            Assert.That(result.Value.Economy.Score, Is.Zero);
            Assert.That(
                result.Report.Issues.Count(issue => issue.Code == SaveIssueCode.VersionUpgraded),
                Is.EqualTo(2));
        }

        [Test]
        public void GrowthV2_PreservesGrowthAndAddsOnlyEconomyDefaults()
        {
            var result = new SaveGameUpgrader().Upgrade(new SaveGameDraft(
                SaveSchema.GrowthVersion,
                new CampaignSaveProgress("L02", new[]
                {
                    new LevelSaveProgress("L02", true, false, 0)
                }),
                new GrowthSaveProgress(4, new[] { "U01", "U17" }),
                null,
                GameSettingsSave.Default));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.Growth.AuthorizationPoints, Is.EqualTo(4));
            Assert.That(result.Value.Growth.DeploymentUnitIds, Is.EqualTo(new[] { "U01", "U17" }));
            Assert.That(result.Value.Economy.Resources, Is.Zero);
            Assert.That(result.Report.Issues.Count(issue => issue.Code == SaveIssueCode.VersionUpgraded), Is.EqualTo(1));
        }

        [Test]
        public void UnknownVersion_IsRejectedWithoutProducingCurrentSave()
        {
            var result = new SaveGameUpgrader().Upgrade(new SaveGameDraft(
                99,
                null,
                null,
                null,
                null));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Report.Issues.Single().Code, Is.EqualTo(SaveIssueCode.UnsupportedVersion));
        }

        [Test]
        public void InvalidCurrentValues_ProduceTypedValidationErrors()
        {
            var value = new SaveGame(
                new CampaignSaveProgress("L03", new[]
                {
                    new LevelSaveProgress("L02", false, true, 5)
                }),
                new GrowthSaveProgress(-1, new[] { "", "U01", "U01" }),
                new EconomySaveProgress(-1, -2),
                new GameSettingsSave(new[]
                {
                    new AudioLayerSaveSettings("Master", 2d, false)
                }));

            var report = value.Validate();

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(SaveIssueCode.InvalidStars));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(SaveIssueCode.InvalidAuthorizationPoints));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(SaveIssueCode.InvalidResources));
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(SaveIssueCode.InvalidVolume));
        }
    }
}
