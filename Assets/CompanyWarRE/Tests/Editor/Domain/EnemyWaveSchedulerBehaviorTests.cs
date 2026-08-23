using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Domain;
using NUnit.Framework;

namespace CompanyWarRE.Domain.Tests
{
    public sealed class EnemyWaveSchedulerBehaviorTests
    {
        [Test]
        public void Interval_EmitsConfiguredWaveWithUniqueColumns()
        {
            var grid = CreateOwnedGrid(5, 9, 3);
            var scheduler = CreateScheduler(5, 9, 2d, 3);

            Assert.That(scheduler.Advance(1.99d, grid), Is.Empty);
            var wave = scheduler.Advance(0.01d, grid);

            Assert.That(wave.Count, Is.EqualTo(3));
            Assert.That(wave.Select(item => item.Position.Column).Distinct().Count(), Is.EqualTo(3));
            Assert.That(wave.All(item => item.Position.Row == 9), Is.True);
            Assert.That(wave.All(item => item.WaveIndex == 1 && item.EnemyId == "E01"), Is.True);
        }

        [Test]
        public void LostControlledColumn_IsExcludedFromLaterWaves()
        {
            var grid = CreateOwnedGrid(3, 9, 3);
            var scheduler = CreateScheduler(3, 9, 1d, 3);
            for (var row = 1; row <= 3; row++)
            {
                grid.SetOwnership(new GridPosition(1, row), false);
            }

            var wave = scheduler.Advance(1d, grid);

            Assert.That(wave.Count, Is.EqualTo(2));
            Assert.That(wave.Any(item => item.Position.Column == 1), Is.False);
        }

        [Test]
        public void BuildingMovesWholeControlBlockColumnForward_AndDestructionRetreatsIt()
        {
            var scheduler = CreateScheduler(5, 9, 1d, 1);

            scheduler.RegisterEnemyBuilding(new GridPosition(1, 9));

            Assert.That(scheduler.SpawnPoints.Where(item => item.Column <= 3).All(item => item.Row == 6), Is.True);
            Assert.That(scheduler.SpawnPoints.Where(item => item.Column > 3).All(item => item.Row == 9), Is.True);

            Assert.That(scheduler.DestroyEnemyBuilding(new GridPosition(2, 8)), Is.True);
            Assert.That(scheduler.SpawnPoints.All(item => item.Row == 9), Is.True);
        }

        [Test]
        public void StagesAdvanceAndStopAfterTheirPeriods()
        {
            var grid = CreateOwnedGrid(1, 9, 3);
            var stages = new List<EnemyWaveStage>
            {
                Stage("Stage1", 2d, 1d, 1, "E01"),
                Stage("Stage2", 2d, 1d, 1, "E02")
            };
            var scheduler = new EnemyWaveScheduler(1, 9, stages, 17);

            var waves = scheduler.Advance(4d, grid);

            Assert.That(waves.Select(item => item.EnemyId), Is.EqualTo(new[] { "E01", "E01", "E02", "E02" }));
            Assert.That(scheduler.IsStopped, Is.True);
            Assert.That(scheduler.Advance(10d, grid), Is.Empty);
        }

        [Test]
        public void WeightedSelection_IsDeterministicForTheSameSeed()
        {
            var grid = CreateOwnedGrid(3, 9, 3);
            var stage = new EnemyWaveStage(
                "Weighted",
                10d,
                1d,
                3,
                new[] { new EnemySpawnWeight("E01", 3), new EnemySpawnWeight("E02", 1) });
            var first = new EnemyWaveScheduler(3, 9, new[] { stage }, 17);
            var second = new EnemyWaveScheduler(3, 9, new[] { stage }, 17);

            var firstWave = first.Advance(1d, grid);
            var secondWave = second.Advance(1d, grid);

            Assert.That(firstWave.Select(item => item.EnemyId), Is.EqualTo(secondWave.Select(item => item.EnemyId)));
            Assert.That(firstWave.Select(item => item.Position), Is.EqualTo(secondWave.Select(item => item.Position)));
        }

        private static EnemyWaveScheduler CreateScheduler(int columns, int rows, double interval, int perWave)
        {
            return new EnemyWaveScheduler(
                columns,
                rows,
                new[] { Stage("Stage1", 60d, interval, perWave, "E01") },
                17);
        }

        private static EnemyWaveStage Stage(
            string name,
            double duration,
            double interval,
            int perWave,
            string enemyId)
        {
            return new EnemyWaveStage(
                name,
                duration,
                interval,
                perWave,
                new[] { new EnemySpawnWeight(enemyId, 1) });
        }

        private static BattleGrid CreateOwnedGrid(int columns, int rows, int controlledRows)
        {
            var grid = new BattleGrid(columns, rows);
            for (var column = 1; column <= columns; column++)
            {
                for (var row = 1; row <= controlledRows; row++)
                {
                    grid.SetOwnership(new GridPosition(column, row), true);
                }
            }

            return grid;
        }
    }
}
