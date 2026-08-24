using System;
using System.Collections.Generic;

namespace CompanyWarRE.Domain
{
    public sealed class EnemyBuildingPlacement
    {
        public EnemyBuildingPlacement(string templateId, GridPosition position)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("An enemy building template id is required.", nameof(templateId));
            }

            TemplateId = templateId;
            Position = position;
        }

        public string TemplateId { get; }
        public GridPosition Position { get; }
    }

    public sealed class BattleProgression
    {
        private sealed class EnemyRegistration
        {
            public int AssaultScoreReward;
            public bool IsBuilding;
        }

        private readonly Dictionary<string, EnemyRegistration> _enemies =
            new Dictionary<string, EnemyRegistration>(StringComparer.Ordinal);
        private readonly HashSet<string> _processedDeaths =
            new HashSet<string>(StringComparer.Ordinal);

        public BattleProgression(int requiredAssaultScore, bool victoryByEnemyBuildings)
        {
            RequiredAssaultScore = Math.Max(0, requiredAssaultScore);
            VictoryByEnemyBuildings = victoryByEnemyBuildings;
            State = BattleState.Running;
        }

        public int AssaultScore { get; private set; }
        public int RequiredAssaultScore { get; }
        public int EnemyBuildingCount { get; private set; }
        public bool VictoryByEnemyBuildings { get; }
        public BattleState State { get; private set; }

        public bool RegisterEnemy(
            string actorId,
            int assaultScoreReward,
            bool isBuilding)
        {
            if (string.IsNullOrWhiteSpace(actorId) || _enemies.ContainsKey(actorId))
            {
                return false;
            }

            _enemies.Add(actorId, new EnemyRegistration
            {
                AssaultScoreReward = Math.Max(0, assaultScoreReward),
                IsBuilding = isBuilding
            });
            if (isBuilding)
            {
                EnemyBuildingCount++;
            }

            return true;
        }

        public bool RecordEnemyDeath(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) ||
                !_enemies.TryGetValue(actorId, out var enemy) ||
                !_processedDeaths.Add(actorId))
            {
                return false;
            }

            AssaultScore += enemy.AssaultScoreReward;
            if (enemy.IsBuilding)
            {
                EnemyBuildingCount = Math.Max(0, EnemyBuildingCount - 1);
            }

            return true;
        }

        public BattleState Evaluate(BattleGrid grid, bool hasAnyValidSpawnPoint)
        {
            if (State != BattleState.Running)
            {
                return State;
            }

            State = BattleOutcomePolicy.Evaluate(new BattleOutcomeContext(
                grid,
                EnemyBuildingCount,
                VictoryByEnemyBuildings,
                hasAnyValidSpawnPoint,
                AssaultScore,
                RequiredAssaultScore));
            return State;
        }
    }
}
