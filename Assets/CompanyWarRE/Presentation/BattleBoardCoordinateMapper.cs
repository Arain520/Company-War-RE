using System;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Supplies presentation heights without leaking Unity/world coordinates into Domain.
    /// A later procedural or authored height rule can replace the uniform implementation.
    /// </summary>
    public interface IBattlePillarHeightProvider
    {
        float GetHeight(GridPosition controlBlockPosition);
    }

    public sealed class UniformBattlePillarHeightProvider : IBattlePillarHeightProvider
    {
        private readonly float _height;

        public UniformBattlePillarHeightProvider(float height)
        {
            _height = Mathf.Max(0.1f, height);
        }

        public float GetHeight(GridPosition controlBlockPosition)
        {
            return _height;
        }
    }

    /// <summary>
    /// Deterministic presentation-only variation. It never consumes Unity's global random state,
    /// so rebuilding visuals cannot affect combat, waves, saves, or replay behaviour.
    /// </summary>
    public sealed class SeededRandomBattlePillarHeightProvider : IBattlePillarHeightProvider
    {
        private readonly float _minimumHeight;
        private readonly float _maximumHeight;
        private readonly int _seed;

        public SeededRandomBattlePillarHeightProvider(
            float minimumHeight,
            float maximumHeight,
            int seed)
        {
            _minimumHeight = Mathf.Max(0.1f, Mathf.Min(minimumHeight, maximumHeight));
            _maximumHeight = Mathf.Max(_minimumHeight, Mathf.Max(minimumHeight, maximumHeight));
            _seed = seed;
        }

        public float GetHeight(GridPosition controlBlockPosition)
        {
            int positionSeed;
            unchecked
            {
                positionSeed = ((_seed * 397) ^ controlBlockPosition.Column) * 397 ^
                               controlBlockPosition.Row;
            }

            var random = new System.Random(positionSeed);
            return Mathf.Lerp(_minimumHeight, _maximumHeight, (float)random.NextDouble());
        }
    }

    public sealed class ScaledBattlePillarHeightProvider : IBattlePillarHeightProvider
    {
        private readonly IBattlePillarHeightProvider _source;
        private readonly float _scale;

        public ScaledBattlePillarHeightProvider(
            IBattlePillarHeightProvider source,
            float scale)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _scale = Mathf.Max(0.01f, scale);
        }

        public float GetHeight(GridPosition controlBlockPosition)
        {
            return _source.GetHeight(controlBlockPosition) * _scale;
        }
    }


    /// <summary>
    /// Converts the unchanged small-cell/control-block topology into pillar presentation space.
    /// Logical neighbours stay neighbours even though a visual gap is inserted between blocks.
    /// </summary>
    public sealed class BattleBoardCoordinateMapper
    {
        private readonly IBattlePillarHeightProvider _heightProvider;

        public BattleBoardCoordinateMapper(
            int columns,
            int rows,
            float pillarWidth,
            float pillarGapRatio,
            float pillarBaseY,
            IBattlePillarHeightProvider heightProvider)
        {
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            PillarWidth = Mathf.Max(0.1f, pillarWidth);
            PillarGapRatio = Mathf.Max(0f, pillarGapRatio);
            PillarBaseY = pillarBaseY;
            _heightProvider = heightProvider ?? throw new ArgumentNullException(nameof(heightProvider));
            ControlBlockColumns = Mathf.CeilToInt(Columns / (float)BattleGrid.ControlBlockSize);
            ControlBlockRows = Mathf.CeilToInt(Rows / (float)BattleGrid.ControlBlockSize);
        }

        public int Columns { get; }
        public int Rows { get; }
        public int ControlBlockColumns { get; }
        public int ControlBlockRows { get; }
        public float PillarWidth { get; }
        public float PillarGapRatio { get; }
        public float PillarBaseY { get; }
        public float CellPitch => PillarWidth / BattleGrid.ControlBlockSize;
        public float PillarGap => PillarWidth * PillarGapRatio;
        public float ControlBlockPitch => PillarWidth + PillarGap;
        public float VisualWidth =>
            ControlBlockColumns * PillarWidth + Mathf.Max(0, ControlBlockColumns - 1) * PillarGap;
        public float VisualLength =>
            ControlBlockRows * PillarWidth + Mathf.Max(0, ControlBlockRows - 1) * PillarGap;
        public float MinimumPillarTopY => GetPillarTopStatistic(PillarTopStatistic.Minimum);
        public float MaximumPillarTopY => GetPillarTopStatistic(PillarTopStatistic.Maximum);
        public float AveragePillarTopY => GetPillarTopStatistic(PillarTopStatistic.Average);

        public bool IsInsideCell(GridPosition position)
        {
            return position.Column >= 1 && position.Column <= Columns &&
                   position.Row >= 1 && position.Row <= Rows;
        }

        public bool IsInsideControlBlock(GridPosition position)
        {
            return position.Column >= 1 && position.Column <= ControlBlockColumns &&
                   position.Row >= 1 && position.Row <= ControlBlockRows;
        }

        public GridPosition GetControlBlockForCell(GridPosition cellPosition)
        {
            var column = Mathf.Clamp(cellPosition.Column, 1, Columns);
            var row = Mathf.Clamp(cellPosition.Row, 1, Rows);
            return new GridPosition(
                ((column - 1) / BattleGrid.ControlBlockSize) + 1,
                ((row - 1) / BattleGrid.ControlBlockSize) + 1);
        }

        public float GetPillarHeight(GridPosition controlBlockPosition)
        {
            return Mathf.Max(0.1f, _heightProvider.GetHeight(controlBlockPosition));
        }

        public float GetPillarTopY(GridPosition controlBlockPosition)
        {
            return PillarBaseY + GetPillarHeight(controlBlockPosition);
        }

        public Vector3 GetControlBlockCenterLocalPosition(GridPosition controlBlockPosition)
        {
            return new Vector3(
                GetCenteredBlockCoordinate(controlBlockPosition.Column, ControlBlockColumns),
                PillarBaseY,
                GetCenteredBlockCoordinate(controlBlockPosition.Row, ControlBlockRows));
        }

        public Vector3 GetControlBlockTopCenterLocalPosition(
            GridPosition controlBlockPosition,
            float verticalOffset = 0f)
        {
            var center = GetControlBlockCenterLocalPosition(controlBlockPosition);
            center.y = GetPillarTopY(controlBlockPosition) + verticalOffset;
            return center;
        }

        public Vector3 GetCellLocalPosition(GridPosition cellPosition, float verticalOffset = 0f)
        {
            var clamped = new GridPosition(
                Mathf.Clamp(cellPosition.Column, 1, Columns),
                Mathf.Clamp(cellPosition.Row, 1, Rows));
            var block = GetControlBlockForCell(clamped);
            var center = GetControlBlockTopCenterLocalPosition(block, verticalOffset);
            center.x += GetCellOffset(clamped.Column);
            center.z += GetCellOffset(clamped.Row);
            return center;
        }

        public Vector3 GetMovingUnitLocalPosition(
            int column,
            double lanePosition,
            float verticalOffset = 0f)
        {
            var safeColumn = Mathf.Clamp(column, 1, Columns);
            var safeLane = Math.Max(1d, Math.Min(Rows, lanePosition));
            var lowerRow = Mathf.Clamp((int)Math.Floor(safeLane), 1, Rows);
            var upperRow = Mathf.Clamp(lowerRow + 1, 1, Rows);
            var interpolation = (float)(safeLane - lowerRow);
            var lower = GetCellLocalPosition(new GridPosition(safeColumn, lowerRow), verticalOffset);
            var upper = GetCellLocalPosition(new GridPosition(safeColumn, upperRow), verticalOffset);
            return Vector3.Lerp(lower, upper, interpolation);
        }

        public bool TryGetCellAtLocalPoint(
            Vector3 localPoint,
            GridPosition controlBlockPosition,
            out GridPosition cellPosition)
        {
            cellPosition = default;
            if (!IsInsideControlBlock(controlBlockPosition))
            {
                return false;
            }

            var center = GetControlBlockCenterLocalPosition(controlBlockPosition);
            var halfIndex = (BattleGrid.ControlBlockSize - 1) * 0.5f;
            var localColumn = Mathf.Clamp(
                Mathf.RoundToInt((localPoint.x - center.x) / CellPitch + halfIndex),
                0,
                BattleGrid.ControlBlockSize - 1);
            var localRow = Mathf.Clamp(
                Mathf.RoundToInt((localPoint.z - center.z) / CellPitch + halfIndex),
                0,
                BattleGrid.ControlBlockSize - 1);
            var column = Mathf.Min(
                Columns,
                (controlBlockPosition.Column - 1) * BattleGrid.ControlBlockSize + localColumn + 1);
            var row = Mathf.Min(
                Rows,
                (controlBlockPosition.Row - 1) * BattleGrid.ControlBlockSize + localRow + 1);
            cellPosition = new GridPosition(column, row);
            return IsInsideCell(cellPosition);
        }

        private float GetCenteredBlockCoordinate(int blockIndex, int blockCount)
        {
            return (Mathf.Clamp(blockIndex, 1, blockCount) - 1f - (blockCount - 1f) * 0.5f) *
                   ControlBlockPitch;
        }

        private float GetCellOffset(int cellIndex)
        {
            var indexInsideBlock = (Mathf.Max(1, cellIndex) - 1) % BattleGrid.ControlBlockSize;
            return (indexInsideBlock - (BattleGrid.ControlBlockSize - 1f) * 0.5f) * CellPitch;
        }

        private float GetPillarTopStatistic(PillarTopStatistic statistic)
        {
            var minimum = float.MaxValue;
            var maximum = float.MinValue;
            var total = 0f;
            var count = 0;
            for (var column = 1; column <= ControlBlockColumns; column++)
            {
                for (var row = 1; row <= ControlBlockRows; row++)
                {
                    var height = GetPillarTopY(new GridPosition(column, row));
                    minimum = Mathf.Min(minimum, height);
                    maximum = Mathf.Max(maximum, height);
                    total += height;
                    count++;
                }
            }

            switch (statistic)
            {
                case PillarTopStatistic.Minimum:
                    return minimum;
                case PillarTopStatistic.Maximum:
                    return maximum;
                default:
                    return count > 0 ? total / count : PillarBaseY;
            }
        }

        private enum PillarTopStatistic
        {
            Minimum,
            Maximum,
            Average
        }
    }
}
