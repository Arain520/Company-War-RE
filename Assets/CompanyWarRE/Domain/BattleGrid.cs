using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public sealed class GridCell
    {
        private readonly List<string> _occupants = new List<string>();

        internal GridCell(GridPosition position, GridPosition controlBlock)
        {
            Position = position;
            ControlBlock = controlBlock;
        }

        public GridPosition Position { get; }
        public GridPosition ControlBlock { get; }
        public bool IsOwned { get; internal set; }
        public bool IsPolluted { get; internal set; }
        public IReadOnlyList<string> Occupants => _occupants;
        public int OccupantCount => _occupants.Count;

        internal bool AddOccupant(string actorId, int maximumOccupants)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return false;
            }

            if (_occupants.Contains(actorId))
            {
                return true;
            }

            if (_occupants.Count >= Math.Max(1, maximumOccupants))
            {
                return false;
            }

            _occupants.Add(actorId);
            return true;
        }

        internal bool RemoveOccupant(string actorId)
        {
            return !string.IsNullOrWhiteSpace(actorId) && _occupants.Remove(actorId);
        }
    }

    public sealed class ControlBlock
    {
        private readonly List<GridCell> _cells = new List<GridCell>();

        internal ControlBlock(GridPosition position, GridPosition center)
        {
            Position = position;
            Center = center;
        }

        public GridPosition Position { get; }
        public GridPosition Center { get; }
        public IReadOnlyList<GridCell> Cells => _cells;
        public bool IsControlled => _cells.Any(cell => cell.IsOwned && !cell.IsPolluted);
        public bool IsPolluted => _cells.Any(cell => cell.IsPolluted);

        internal void AddCell(GridCell cell)
        {
            _cells.Add(cell);
        }
    }

    public readonly struct PollutionChange
    {
        public PollutionChange(GridPosition position, bool isPolluted)
        {
            Position = position;
            IsPolluted = isPolluted;
        }

        public GridPosition Position { get; }
        public bool IsPolluted { get; }
    }

    public sealed class BattleGrid
    {
        public const int ControlBlockSize = 3;
        public const int MaximumOccupantsPerCell = 9;

        private readonly GridCell[,] _cells;
        private readonly ControlBlock[,] _controlBlocks;
        private readonly List<PollutionChange> _pollutionChanges = new List<PollutionChange>();

        public BattleGrid(int columns, int rows)
        {
            if (columns <= 0 || rows <= 0)
            {
                throw new ArgumentException("Grid dimensions must be positive.");
            }

            Columns = columns;
            Rows = rows;
            ControlBlockColumns = Math.Max(1, (int)Math.Ceiling(columns / (double)ControlBlockSize));
            ControlBlockRows = Math.Max(1, (int)Math.Ceiling(rows / (double)ControlBlockSize));
            _cells = new GridCell[columns, rows];
            _controlBlocks = new ControlBlock[ControlBlockColumns, ControlBlockRows];

            for (var blockColumn = 1; blockColumn <= ControlBlockColumns; blockColumn++)
            {
                for (var blockRow = 1; blockRow <= ControlBlockRows; blockRow++)
                {
                    var centerColumn = ClampColumn(((blockColumn - 1) * ControlBlockSize) + 2);
                    var centerRow = ClampRow(((blockRow - 1) * ControlBlockSize) + 2);
                    _controlBlocks[blockColumn - 1, blockRow - 1] = new ControlBlock(
                        new GridPosition(blockColumn, blockRow),
                        new GridPosition(centerColumn, centerRow));
                }
            }

            for (var column = 1; column <= Columns; column++)
            {
                for (var row = 1; row <= Rows; row++)
                {
                    var blockPosition = new GridPosition(ToControlBlockIndex(column), ToControlBlockIndex(row));
                    var cell = new GridCell(new GridPosition(column, row), blockPosition);
                    _cells[column - 1, row - 1] = cell;
                    GetControlBlock(blockPosition)?.AddCell(cell);
                }
            }
        }

        public int Columns { get; }
        public int Rows { get; }
        public int ControlBlockColumns { get; }
        public int ControlBlockRows { get; }
        public IReadOnlyList<PollutionChange> PollutionChanges => _pollutionChanges;

        public bool IsInside(GridPosition position)
        {
            return position.Column >= 1 && position.Column <= Columns &&
                   position.Row >= 1 && position.Row <= Rows;
        }

        public GridCell GetCell(GridPosition position)
        {
            return IsInside(position) ? _cells[position.Column - 1, position.Row - 1] : null;
        }

        public ControlBlock GetControlBlock(GridPosition blockPosition)
        {
            if (blockPosition.Column < 1 || blockPosition.Column > ControlBlockColumns ||
                blockPosition.Row < 1 || blockPosition.Row > ControlBlockRows)
            {
                return null;
            }

            return _controlBlocks[blockPosition.Column - 1, blockPosition.Row - 1];
        }

        public ControlBlock GetControlBlockForCell(GridPosition position)
        {
            if (!IsInside(position))
            {
                return null;
            }

            return GetControlBlock(new GridPosition(
                ToControlBlockIndex(position.Column),
                ToControlBlockIndex(position.Row)));
        }

        public bool SetOwnership(GridPosition position, bool isOwned)
        {
            var cell = GetCell(position);
            if (cell == null)
            {
                return false;
            }

            cell.IsOwned = isOwned;
            return true;
        }

        public int SetPollution(GridPosition position, bool isPolluted)
        {
            var block = GetControlBlockForCell(position);
            if (block == null)
            {
                return 0;
            }

            var changed = 0;
            foreach (var cell in block.Cells)
            {
                if (cell.IsPolluted == isPolluted)
                {
                    continue;
                }

                cell.IsPolluted = isPolluted;
                _pollutionChanges.Add(new PollutionChange(cell.Position, isPolluted));
                changed++;
            }

            return changed;
        }

        public void ClearPollutionChanges()
        {
            _pollutionChanges.Clear();
        }

        public bool CanDeployStandardUnit(GridPosition position)
        {
            var cell = GetCell(position);
            return cell != null && cell.IsOwned && !cell.IsPolluted && cell.OccupantCount < 1;
        }

        public bool TryAddOccupant(GridPosition position, string actorId, int maximumOccupants = MaximumOccupantsPerCell)
        {
            var cell = GetCell(position);
            return cell != null && cell.AddOccupant(actorId, maximumOccupants);
        }

        public bool RemoveOccupant(GridPosition position, string actorId)
        {
            var cell = GetCell(position);
            return cell != null && cell.RemoveOccupant(actorId);
        }

        public IReadOnlyList<GridCell> GetDeployableCells()
        {
            var result = new List<GridCell>();
            for (var column = 1; column <= Columns; column++)
            {
                for (var row = 1; row <= Rows; row++)
                {
                    var cell = _cells[column - 1, row - 1];
                    if (cell.IsOwned && !cell.IsPolluted)
                    {
                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        public bool HasControlledTerritory()
        {
            for (var column = 1; column <= ControlBlockColumns; column++)
            {
                for (var row = 1; row <= ControlBlockRows; row++)
                {
                    if (_controlBlocks[column - 1, row - 1].IsControlled)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int ClampColumn(int column)
        {
            return Math.Max(1, Math.Min(Columns, column));
        }

        private int ClampRow(int row)
        {
            return Math.Max(1, Math.Min(Rows, row));
        }

        private static int ToControlBlockIndex(int cellIndex)
        {
            return ((Math.Max(1, cellIndex) - 1) / ControlBlockSize) + 1;
        }
    }
}
