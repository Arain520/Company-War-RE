using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public sealed class EnemySpawnWeight
    {
        public EnemySpawnWeight(string enemyId, int weight)
        {
            if (string.IsNullOrWhiteSpace(enemyId))
            {
                throw new ArgumentException("An enemy id is required.", nameof(enemyId));
            }

            EnemyId = enemyId;
            Weight = Math.Max(0, weight);
        }

        public string EnemyId { get; }
        public int Weight { get; }
    }

    public sealed class EnemyWaveStage
    {
        public EnemyWaveStage(
            string name,
            double durationSeconds,
            double waveIntervalSeconds,
            int enemiesPerWave,
            IReadOnlyList<EnemySpawnWeight> enemyWeights)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A stage name is required.", nameof(name));
            }

            if (enemyWeights == null || enemyWeights.Count == 0)
            {
                throw new ArgumentException("At least one enemy weight is required.", nameof(enemyWeights));
            }

            Name = name;
            DurationSeconds = Math.Max(0.01d, durationSeconds);
            WaveIntervalSeconds = Math.Max(0.01d, waveIntervalSeconds);
            EnemiesPerWave = Math.Max(1, enemiesPerWave);
            EnemyWeights = enemyWeights.ToArray();
        }

        public string Name { get; }
        public double DurationSeconds { get; }
        public double WaveIntervalSeconds { get; }
        public int EnemiesPerWave { get; }
        public IReadOnlyList<EnemySpawnWeight> EnemyWeights { get; }
    }

    public readonly struct EnemySpawnPoint
    {
        public EnemySpawnPoint(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }
        public int Row { get; }
    }

    public sealed class EnemyWaveSpawn
    {
        public EnemyWaveSpawn(string enemyId, GridPosition position, int waveIndex, string stageName)
        {
            EnemyId = enemyId;
            Position = position;
            WaveIndex = waveIndex;
            StageName = stageName;
        }

        public string EnemyId { get; }
        public GridPosition Position { get; }
        public int WaveIndex { get; }
        public string StageName { get; }
    }

    public sealed class EnemyWaveScheduler
    {
        private readonly int _columns;
        private readonly int _rows;
        private readonly int _defaultSpawnRow;
        private readonly Random _random;
        private readonly HashSet<int> _enabledColumns;
        private readonly List<EnemySpawnPoint> _spawnPoints = new List<EnemySpawnPoint>();
        private readonly List<GridPosition> _enemyBuildings = new List<GridPosition>();
        private readonly IReadOnlyList<EnemyWaveStage> _stages;
        private int _stageIndex;
        private int _waveIndex;
        private double _stageElapsedSeconds;
        private double _waveElapsedSeconds;
        private bool _externallyStopped;

        public EnemyWaveScheduler(
            int columns,
            int rows,
            IReadOnlyList<EnemyWaveStage> stages,
            int randomSeed = 17,
            IReadOnlyList<int> enabledColumns = null,
            int? defaultSpawnRow = null)
        {
            if (columns <= 0 || rows <= 0)
            {
                throw new ArgumentException("Grid dimensions must be positive.");
            }

            if (stages == null || stages.Count == 0)
            {
                throw new ArgumentException("At least one enemy wave stage is required.", nameof(stages));
            }

            _columns = columns;
            _rows = rows;
            _defaultSpawnRow = defaultSpawnRow ?? rows;
            if (_defaultSpawnRow < 1 || _defaultSpawnRow > rows)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultSpawnRow));
            }
            _stages = stages.ToArray();
            _random = new Random(randomSeed);
            _enabledColumns = enabledColumns == null
                ? new HashSet<int>(Enumerable.Range(1, columns))
                : new HashSet<int>(enabledColumns);
            if (_enabledColumns.Count == 0 || _enabledColumns.Any(column => column < 1 || column > columns))
            {
                throw new ArgumentException("Enabled spawn columns must be inside the grid.", nameof(enabledColumns));
            }
            RefreshAllSpawnPoints();
        }

        public IReadOnlyList<EnemySpawnPoint> SpawnPoints => _spawnPoints;
        public string CurrentStageName => IsStopped ? string.Empty : _stages[_stageIndex].Name;
        public int WaveIndex => _waveIndex;
        public bool IsStopped => _externallyStopped || _stageIndex >= _stages.Count;

        public int CountValidSpawnPoints(BattleGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            return _spawnPoints.Count(point =>
                _enabledColumns.Contains(point.Column) &&
                HasControlledTerritory(grid, point.Column));
        }

        public bool HasAnyValidSpawnPoint(BattleGrid grid)
        {
            return CountValidSpawnPoints(grid) > 0;
        }

        public void Stop()
        {
            _externallyStopped = true;
        }

        public void RegisterEnemyBuilding(GridPosition position)
        {
            ValidatePosition(position);
            if (!_enemyBuildings.Contains(position))
            {
                _enemyBuildings.Add(position);
                RefreshBlockColumn(position.Column);
            }
        }

        public bool DestroyEnemyBuilding(GridPosition position)
        {
            ValidatePosition(position);
            var blockColumn = ToControlBlockIndex(position.Column);
            var blockRow = ToControlBlockIndex(position.Row);
            var removed = _enemyBuildings.RemoveAll(item =>
                ToControlBlockIndex(item.Column) == blockColumn &&
                ToControlBlockIndex(item.Row) == blockRow) > 0;
            if (removed)
            {
                RefreshBlockColumn(position.Column);
            }

            return removed;
        }

        public IReadOnlyList<EnemyWaveSpawn> Advance(double deltaSeconds, BattleGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (grid.Columns != _columns || grid.Rows != _rows)
            {
                throw new ArgumentException("The scheduler and battle grid dimensions must match.", nameof(grid));
            }

            var result = new List<EnemyWaveSpawn>();
            var remaining = Math.Max(0d, deltaSeconds);
            const double epsilon = 0.0000001d;
            while (remaining > epsilon && !IsStopped)
            {
                var stage = _stages[_stageIndex];
                var untilStageEnd = stage.DurationSeconds - _stageElapsedSeconds;
                var step = Math.Min(remaining, untilStageEnd);
                _stageElapsedSeconds += step;
                _waveElapsedSeconds += step;
                remaining -= step;

                while (_waveElapsedSeconds + epsilon >= stage.WaveIntervalSeconds)
                {
                    _waveElapsedSeconds -= stage.WaveIntervalSeconds;
                    SpawnWave(stage, grid, result);
                }

                if (_stageElapsedSeconds + epsilon >= stage.DurationSeconds)
                {
                    _stageIndex++;
                    _stageElapsedSeconds = 0d;
                    _waveElapsedSeconds = 0d;
                }
            }

            return result;
        }

        private void SpawnWave(EnemyWaveStage stage, BattleGrid grid, ICollection<EnemyWaveSpawn> result)
        {
            _waveIndex++;
            var valid = _spawnPoints
                .Where(point => _enabledColumns.Contains(point.Column))
                .Where(point => HasControlledTerritory(grid, point.Column))
                .ToList();
            for (var index = valid.Count - 1; index > 0; index--)
            {
                var other = _random.Next(index + 1);
                var temporary = valid[index];
                valid[index] = valid[other];
                valid[other] = temporary;
            }

            var count = Math.Min(stage.EnemiesPerWave, valid.Count);
            for (var index = 0; index < count; index++)
            {
                var point = valid[index];
                result.Add(new EnemyWaveSpawn(
                    PickEnemy(stage.EnemyWeights),
                    new GridPosition(point.Column, point.Row),
                    _waveIndex,
                    stage.Name));
            }
        }

        private string PickEnemy(IReadOnlyList<EnemySpawnWeight> weights)
        {
            var total = weights.Sum(item => item.Weight);
            if (total <= 0)
            {
                return weights[0].EnemyId;
            }

            var pick = _random.Next(total);
            var accumulated = 0;
            foreach (var item in weights)
            {
                accumulated += item.Weight;
                if (pick < accumulated)
                {
                    return item.EnemyId;
                }
            }

            return weights[0].EnemyId;
        }

        private static bool HasControlledTerritory(BattleGrid grid, int column)
        {
            for (var row = 1; row <= grid.Rows; row++)
            {
                var cell = grid.GetCell(new GridPosition(column, row));
                if (cell != null && cell.IsOwned && !cell.IsPolluted)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAllSpawnPoints()
        {
            _spawnPoints.Clear();
            for (var column = 1; column <= _columns; column++)
            {
                _spawnPoints.Add(new EnemySpawnPoint(column, ResolveSpawnRow(column)));
            }
        }

        private void RefreshBlockColumn(int smallColumn)
        {
            var blockColumn = ToControlBlockIndex(smallColumn);
            for (var column = 1; column <= _columns; column++)
            {
                if (ToControlBlockIndex(column) == blockColumn)
                {
                    _spawnPoints[column - 1] = new EnemySpawnPoint(column, ResolveSpawnRow(column));
                }
            }
        }

        private int ResolveSpawnRow(int smallColumn)
        {
            var blockColumn = ToControlBlockIndex(smallColumn);
            var nearestBuilding = _enemyBuildings
                .Where(item => ToControlBlockIndex(item.Column) == blockColumn)
                .OrderBy(item => item.Row)
                .FirstOrDefault();
            if (nearestBuilding.Row <= 0)
            {
                return _defaultSpawnRow;
            }

            var buildingBlockStartRow = ((ToControlBlockIndex(nearestBuilding.Row) - 1) * BattleGrid.ControlBlockSize) + 1;
            return Math.Max(1, buildingBlockStartRow - 1);
        }

        private void ValidatePosition(GridPosition position)
        {
            if (position.Column < 1 || position.Column > _columns ||
                position.Row < 1 || position.Row > _rows)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
        }

        private static int ToControlBlockIndex(int smallCellIndex)
        {
            return ((Math.Max(1, smallCellIndex) - 1) / BattleGrid.ControlBlockSize) + 1;
        }
    }
}
