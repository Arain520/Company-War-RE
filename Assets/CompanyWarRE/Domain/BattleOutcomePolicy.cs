using System;

namespace CompanyWarRE.Domain
{
    public enum BattleState
    {
        Running,
        Victory,
        Defeat
    }

    public sealed class BattleOutcomeContext
    {
        public BattleOutcomeContext(
            BattleGrid grid,
            int enemyBuildingCount,
            bool victoryByEnemyBuildings,
            bool hasAnyValidSpawnPoint,
            int assaultScore,
            int requiredAssaultScore)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            EnemyBuildingCount = Math.Max(0, enemyBuildingCount);
            VictoryByEnemyBuildings = victoryByEnemyBuildings;
            HasAnyValidSpawnPoint = hasAnyValidSpawnPoint;
            AssaultScore = Math.Max(0, assaultScore);
            RequiredAssaultScore = Math.Max(0, requiredAssaultScore);
        }

        public BattleGrid Grid { get; }
        public int EnemyBuildingCount { get; }
        public bool VictoryByEnemyBuildings { get; }
        public bool HasAnyValidSpawnPoint { get; }
        public int AssaultScore { get; }
        public int RequiredAssaultScore { get; }
    }

    public static class BattleOutcomePolicy
    {
        public static BattleState Evaluate(BattleOutcomeContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.Grid.HasControlledTerritory())
            {
                return BattleState.Defeat;
            }

            if (!context.VictoryByEnemyBuildings && !context.HasAnyValidSpawnPoint)
            {
                return BattleState.Defeat;
            }

            if (context.VictoryByEnemyBuildings && context.EnemyBuildingCount <= 0)
            {
                return BattleState.Victory;
            }

            return BattleState.Running;
        }
    }
}
