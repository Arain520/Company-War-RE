using System.Collections.Generic;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class AuthorizationAndOutcomeBehaviorTests
    {
        [Test]
        public void Authorization_FirstRequirementIsSevenAndNextRequirementIsFourteen()
        {
            var progression = new AuthorizationProgression();
            progression.Configure(
                new[]
                {
                    new AuthorizationStageDefinition("Stage 1", 7, new[]
                    {
                        new AuthorizationItemDefinition("U07"),
                        new AuthorizationItemDefinition("U08"),
                        new AuthorizationItemDefinition("U09"),
                        new AuthorizationItemDefinition("U10")
                    })
                },
                6,
                new[] { "U01" },
                new FirstAvailableAuthorizationCandidateSelector());

            Assert.That(progression.State, Is.EqualTo(AuthorizationState.WaitingForNextRequirement));
            Assert.That(progression.NextRequirement, Is.EqualTo(7));
            progression.GainPoints(1);
            Assert.That(progression.State, Is.EqualTo(AuthorizationState.Available));
            Assert.That(progression.BeginChoice(), Is.True);
            Assert.That(progression.Candidates.Count, Is.EqualTo(4));
            Assert.That(progression.Accept("U07"), Is.True);
            Assert.That(progression.NextRequirement, Is.EqualTo(14));
            Assert.That(progression.DeployList, Does.Contain("U07"));
        }

        [Test]
        public void Authorization_InitialDeployListIsCappedAtTwelve()
        {
            var ids = new List<string>();
            for (var index = 1; index <= 20; index++)
            {
                ids.Add($"U{index:00}");
            }

            var progression = new AuthorizationProgression();
            progression.Configure(null, 0, ids, new FirstAvailableAuthorizationCandidateSelector());

            Assert.That(progression.DeployList.Count, Is.EqualTo(AuthorizationProgression.MaximumDeployListSize));
        }

        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void Authorization_LaterStagesUseLegacyCandidateCaps(int targetStage, int expectedCandidates)
        {
            var stages = new List<AuthorizationStageDefinition>();
            var initialDeployments = new List<string>();
            for (var stageIndex = 0; stageIndex <= targetStage; stageIndex++)
            {
                var items = new List<AuthorizationItemDefinition>();
                for (var itemIndex = 1; itemIndex <= 5; itemIndex++)
                {
                    var id = $"S{stageIndex}U{itemIndex}";
                    items.Add(new AuthorizationItemDefinition(id));
                    if (stageIndex < targetStage)
                    {
                        initialDeployments.Add(id);
                    }
                }

                stages.Add(new AuthorizationStageDefinition($"Stage {stageIndex}", 7, items));
            }

            var progression = new AuthorizationProgression();
            progression.Configure(
                stages,
                7,
                initialDeployments,
                new FirstAvailableAuthorizationCandidateSelector());

            Assert.That(progression.StageIndex, Is.EqualTo(targetStage));
            Assert.That(progression.BeginChoice(), Is.True);
            Assert.That(progression.Candidates.Count, Is.EqualTo(expectedCandidates));
        }

        [Test]
        public void Outcome_NoControlledTerritoryIsDefeat()
        {
            var state = BattleOutcomePolicy.Evaluate(new BattleOutcomeContext(
                new BattleGrid(3, 3),
                1,
                true,
                true,
                0,
                10));

            Assert.That(state, Is.EqualTo(BattleState.Defeat));
        }

        [Test]
        public void Outcome_DestroyingAllEnemyBuildingsIsVictoryWhenConfigured()
        {
            var grid = OwnedGrid();
            var state = BattleOutcomePolicy.Evaluate(new BattleOutcomeContext(grid, 0, true, true, 0, 10));

            Assert.That(state, Is.EqualTo(BattleState.Victory));
        }

        [Test]
        public void Outcome_NoValidSpawnIsDefeatWhenBuildingObjectiveIsDisabled()
        {
            var state = BattleOutcomePolicy.Evaluate(new BattleOutcomeContext(
                OwnedGrid(),
                1,
                false,
                false,
                0,
                10));

            Assert.That(state, Is.EqualTo(BattleState.Defeat));
        }

        [Test]
        public void Outcome_AssaultScoreDoesNotCurrentlyOverrideEnemyBuildingObjective()
        {
            var grid = OwnedGrid();
            var state = BattleOutcomePolicy.Evaluate(new BattleOutcomeContext(grid, 1, true, true, 100, 10));

            Assert.That(state, Is.EqualTo(BattleState.Running),
                "This characterizes current BattleEngine behavior; assault-score victory remains a product decision.");
        }

        private static BattleGrid OwnedGrid()
        {
            var grid = new BattleGrid(3, 3);
            grid.SetOwnership(new GridPosition(1, 1), true);
            return grid;
        }
    }
}
