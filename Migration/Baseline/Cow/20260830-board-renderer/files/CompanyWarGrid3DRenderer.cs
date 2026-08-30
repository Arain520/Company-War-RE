using System;
using System.Collections.Generic;
using CompanyWar.Logic.Domain;
using UnityEngine;

namespace CompanyWar.Prototype3D
{
    /// <summary>
    /// <c>CompanyWarGrid3DRenderer</c> 负责生成并刷新对应对象的可视化表现。
    /// </summary>
    public class CompanyWarGrid3DRenderer : MonoBehaviour
    {
        [SerializeField] private Transform gridRoot;
        [SerializeField] private float cellSize = 1.2f;
        [SerializeField] private float cellHeight = 0.04f;
        [SerializeField] private float surfaceOffsetY = -0.008f;
        [SerializeField] private Vector3 origin = new Vector3(0f, -15f, 0f);
        [SerializeField] private Color ownedColor = new Color(0.04f, 0.24f, 0.40f, 1f);
        [SerializeField] private Color pollutedColor = new Color(0.01f, 0.025f, 0.06f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.54f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.08f, 0.10f, 0.13f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.08f, 0.58f, 1.0f, 1f);
        [SerializeField] private Color smallCellFocusColor = new Color(0.95f, 0.78f, 0.20f, 1f);
        [SerializeField] private Color routePreviewColor = new Color(0.09f, 0.44f, 0.78f, 1f);
        [SerializeField] private Color controlBlockBorderColor = new Color(0.12f, 0.62f, 1.0f, 1f);

        private readonly Dictionary<(int col, int row, int slot), Renderer> _renderers = new Dictionary<(int col, int row, int slot), Renderer>();
        private readonly Dictionary<(int col, int row, int slot), CompanyWarGridCell3D> _cells = new Dictionary<(int col, int row, int slot), CompanyWarGridCell3D>();
        private readonly HashSet<(int col, int row)> _previewPathCells = new HashSet<(int col, int row)>();
        private readonly List<GameObject> _controlBlockBorders = new List<GameObject>();

        private Material _sharedMaterial;
        private GridMap _map;
        private (int col, int row, int slot)? _hoveredCell;
        private bool _logicalSubGridMode;
        private bool _hoverWholeControlBlock = true;
        private bool _hoverControlBlockColumnInSmallCellMode = true;

        public float CellSize => cellSize;
        public float RenderCellSize => GetRenderCellSize();
        public Vector3 Origin => origin;
        public Vector3 WorldOrigin => GetSpaceRoot().TransformPoint(origin);
        public float CellHeight => cellHeight;

        /// <summary>
        /// 设置 <c>Origin</c>。
        /// </summary>
        /// <param name="newOrigin">方法执行所需的 <paramref name="newOrigin"/> 值。</param>
        public void SetOrigin(Vector3 newOrigin)
        {
            origin = newOrigin;
            foreach (var pair in _cells)
            {
                var cell = pair.Value;
                if (cell == null)
                {
                    continue;
                }

                cell.transform.localPosition = CellToLocal(cell.Col, cell.Row, cell.SlotIndex);
            }
        }

        /// <summary>
        /// 设置 <c>LogicalSubGridMode</c>。
        /// </summary>
        /// <param name="enabled">方法执行所需的 <paramref name="enabled"/> 值。</param>
        public void SetLogicalSubGridMode(bool enabled)
        {
            _logicalSubGridMode = enabled;
        }

        /// <summary>
        /// 设置 <c>HoverWholeControlBlock</c>。
        /// </summary>
        /// <param name="enabled">方法执行所需的 <paramref name="enabled"/> 值。</param>
        public void SetHoverWholeControlBlock(bool enabled)
        {
            if (_hoverWholeControlBlock == enabled)
            {
                return;
            }

            ClearHover();
            _hoverWholeControlBlock = enabled;
        }

        /// <summary>
        /// 设置 <c>HoverControlBlockColumnInSmallCellMode</c>。
        /// </summary>
        /// <param name="enabled">方法执行所需的 <paramref name="enabled"/> 值。</param>
        public void SetHoverControlBlockColumnInSmallCellMode(bool enabled)
        {
            if (_hoverControlBlockColumnInSmallCellMode == enabled)
            {
                return;
            }

            ClearHover();
            _hoverControlBlockColumnInSmallCellMode = enabled;
        }

        /// <summary>
        /// 构建 <c>Build</c>。
        /// </summary>
        /// <param name="map">方法执行所需的 <paramref name="map"/> 值。</param>
        public void Build(GridMap map)
        {
            if (gridRoot == null)
            {
                gridRoot = transform;
            }

            Clear();
            if (map == null)
            {
                return;
            }

            _map = map;
            _sharedMaterial = CreateLitMaterial();

            for (var col = 1; col <= map.Columns; col++)
            {
                for (var row = 1; row <= map.Rows; row++)
                {
                    var slotCount = _logicalSubGridMode ? 1 : GridMap.MaxUnitsPerCell;
                    for (var slot = 0; slot < slotCount; slot++)
                    {
                        var cellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cellObject.name = $"Cell_{col}_{row}_{slot}";
                        cellObject.transform.SetParent(gridRoot, false);
                        cellObject.transform.localPosition = CellToLocal(col, row, slot);
                        cellObject.transform.localRotation = Quaternion.identity;
                        var visualCellSize = GetVisualCellSize();
                        cellObject.transform.localScale = new Vector3(visualCellSize, cellHeight, visualCellSize);

                        var cellComponent = cellObject.AddComponent<CompanyWarGridCell3D>();
                        cellComponent.Setup(col, row, slot);

                        var renderer = cellObject.GetComponent<Renderer>();
                        if (renderer != null && _sharedMaterial != null)
                        {
                            renderer.sharedMaterial = _sharedMaterial;
                        }

                        _renderers[(col, row, slot)] = renderer;
                        _cells[(col, row, slot)] = cellComponent;
                    }
                }
            }

            CreateControlBlockBorders(map);
        }

        /// <summary>
        /// 刷新 <c>Refresh</c>。
        /// </summary>
        /// <param name="map">方法执行所需的 <paramref name="map"/> 值。</param>
        /// <param name="isEnemyOccupant">用于控制 <paramref name="isEnemyOccupant"/> 对应状态的布尔值。</param>
        public void Refresh(GridMap map, Func<string, bool> isEnemyOccupant)
        {
            if (map == null)
            {
                return;
            }

            for (var col = 1; col <= map.Columns; col++)
            {
                for (var row = 1; row <= map.Rows; row++)
                {
                    var cell = map.GetCell(col, row);
                    if (cell == null)
                    {
                        continue;
                    }

                    var color = emptyColor;
                    if (cell.IsPolluted)
                    {
                        color = pollutedColor;
                    }
                    else if (cell.OccupantCount > 0 && isEnemyOccupant != null && HasEnemyOccupant(cell, isEnemyOccupant))
                    {
                        color = enemyColor;
                    }
                    else if (cell.IsOwned)
                    {
                        color = ownedColor;
                    }

                    var slotCount = _logicalSubGridMode ? 1 : GridMap.MaxUnitsPerCell;
                    for (var slot = 0; slot < slotCount; slot++)
                    {
                        if (!_cells.TryGetValue((col, row, slot), out var cellView) || cellView == null)
                        {
                            continue;
                        }

                        cellView.SetBaseColor(color);
                        cellView.SetPreview(_previewPathCells.Contains((col, row)), routePreviewColor);
                    }
                }
            }

            RefreshHoverState();
        }

        /// <summary>
        /// 执行 <c>CellToWorld</c> 对应的业务逻辑。
        /// </summary>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <returns>该操作产生的结果。</returns>
        public Vector3 CellToWorld(int col, int row)
        {
            return GetSpaceRoot().TransformPoint(CellToLocal(col, row));
        }

        /// <summary>
        /// 执行 <c>CellToWorld</c> 对应的业务逻辑。
        /// </summary>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <param name="slotIndex">方法执行所需的 <paramref name="slotIndex"/> 值。</param>
        /// <param name="slotCapacity">方法执行所需的 <paramref name="slotCapacity"/> 值。</param>
        /// <returns>该操作产生的结果。</returns>
        public Vector3 CellToWorld(int col, int row, int slotIndex, int slotCapacity = GridMap.MaxUnitsPerCell)
        {
            var localCenter = CellToLocal(col, row);
            var localOffset = _logicalSubGridMode
                ? Vector3.zero
                : slotCapacity >= GridMap.MaxUnitsPerCell
                ? GetSubCellCenterLocalOffset(slotIndex)
                : GetSlotLocalOffset(slotIndex, slotCapacity);
            return GetSpaceRoot().TransformPoint(localCenter + localOffset);
        }

        /// <summary>
        /// 执行 <c>CellToWorld</c> 对应的业务逻辑。
        /// </summary>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="rowPosition">方法执行所需的 <paramref name="rowPosition"/> 值。</param>
        /// <param name="slotIndex">方法执行所需的 <paramref name="slotIndex"/> 值。</param>
        /// <param name="slotCapacity">方法执行所需的 <paramref name="slotCapacity"/> 值。</param>
        /// <returns>该操作产生的结果。</returns>
        public Vector3 CellToWorld(int col, float rowPosition, int slotIndex, int slotCapacity = GridMap.MaxUnitsPerCell)
        {
            return CellToWorld((float)col, rowPosition, slotIndex, slotCapacity);
        }

        /// <summary>
        /// 将连续的小格坐标转换为世界坐标，用于大格内单位追击。
        /// </summary>
        public Vector3 CellToWorld(float columnPosition, float rowPosition, int slotIndex, int slotCapacity = GridMap.MaxUnitsPerCell)
        {
            var renderCellSize = GetRenderCellSize();
            var localCenter = new Vector3(
                origin.x + (Mathf.Max(1f, columnPosition) - 0.5f) * renderCellSize,
                origin.y + surfaceOffsetY,
                origin.z + (Mathf.Max(0f, rowPosition) - 0.5f) * renderCellSize);
            var localOffset = _logicalSubGridMode
                ? Vector3.zero
                : slotCapacity >= GridMap.MaxUnitsPerCell
                    ? GetSubCellCenterLocalOffset(slotIndex)
                    : GetSlotLocalOffset(slotIndex, slotCapacity);
            return GetSpaceRoot().TransformPoint(localCenter + localOffset);
        }

        /// <summary>
        /// 尝试获取 <c>CellFromRay</c>。
        /// </summary>
        /// <param name="ray">方法执行所需的 <paramref name="ray"/> 值。</param>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <returns>操作或条件成立时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetCellFromRay(Ray ray, out int col, out int row)
        {
            return TryGetCellAndSlotFromRay(ray, out col, out row, out _);
        }

        /// <summary>
        /// 尝试获取 <c>CellAndSlotFromRay</c>。
        /// </summary>
        /// <param name="ray">方法执行所需的 <paramref name="ray"/> 值。</param>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <param name="slotIndex">方法执行所需的 <paramref name="slotIndex"/> 值。</param>
        /// <returns>操作或条件成立时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool TryGetCellAndSlotFromRay(Ray ray, out int col, out int row, out int slotIndex)
        {
            col = 0;
            row = 0;
            slotIndex = 0;

            var hits = Physics.RaycastAll(ray, 1000f);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (var i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    var cell = hit.collider != null ? hit.collider.GetComponent<CompanyWarGridCell3D>() : null;
                    if (cell == null)
                    {
                        continue;
                    }

                    col = cell.Col;
                    row = cell.Row;
                    slotIndex = _logicalSubGridMode ? 0 : Mathf.Clamp(cell.SlotIndex, 0, GridMap.MaxUnitsPerCell - 1);
                    return true;
                }
            }

            var plane = new Plane(GetSpaceRoot().up, WorldOrigin);
            if (!plane.Raycast(ray, out var enter))
            {
                return false;
            }

            var hitPoint = ray.GetPoint(enter);
            if (!WorldToCell(hitPoint, out col, out row))
            {
                return false;
            }

            if (_logicalSubGridMode)
            {
                slotIndex = 0;
                return true;
            }

            var localPos = GetSpaceRoot().InverseTransformPoint(hitPoint);
            var renderCellSize = GetRenderCellSize();
            var cellMinX = origin.x + (col - 1) * renderCellSize;
            var cellMinZ = origin.z + (row - 1) * renderCellSize;
            var nx = Mathf.Clamp01((localPos.x - cellMinX) / renderCellSize);
            var nz = Mathf.Clamp01((localPos.z - cellMinZ) / renderCellSize);

            var subCol = Mathf.Clamp(Mathf.FloorToInt(nx * 3f), 0, 2);
            var subRow = Mathf.Clamp(Mathf.FloorToInt(nz * 3f), 0, 2);
            var rowMajor = subRow * 3 + subCol;
            slotIndex = RowMajorToInternalSlot(rowMajor);
            return true;
        }

        /// <summary>
        /// 执行 <c>WorldToCell</c> 对应的业务逻辑。
        /// </summary>
        /// <param name="worldPos">方法执行所需的 <paramref name="worldPos"/> 值。</param>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <returns>操作或条件成立时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool WorldToCell(Vector3 worldPos, out int col, out int row)
        {
            col = 0;
            row = 0;

            if (_map == null)
            {
                return false;
            }

            var localPos = GetSpaceRoot().InverseTransformPoint(worldPos);
            var renderCellSize = GetRenderCellSize();
            var xOffset = localPos.x - origin.x;
            var zOffset = localPos.z - origin.z;

            col = Mathf.FloorToInt(xOffset / renderCellSize) + 1;
            row = Mathf.FloorToInt(zOffset / renderCellSize) + 1;

            return col >= 1 && col <= _map.Columns && row >= 1 && row <= _map.Rows;
        }

        /// <summary>
        /// 执行 <c>ScreenToCell</c> 对应的业务逻辑。
        /// </summary>
        /// <param name="screenPos">方法执行所需的 <paramref name="screenPos"/> 值。</param>
        /// <param name="camera">方法执行所需的 <paramref name="camera"/> 值。</param>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <returns>操作或条件成立时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        public bool ScreenToCell(Vector2 screenPos, Camera camera, out int col, out int row)
        {
            col = 0;
            row = 0;
            if (camera == null)
            {
                return false;
            }

            return TryGetCellFromRay(camera.ScreenPointToRay(screenPos), out col, out row);
        }

        /// <summary>
        /// 更新 <c>Hover</c>。
        /// </summary>
        /// <param name="screenPos">方法执行所需的 <paramref name="screenPos"/> 值。</param>
        /// <param name="camera">方法执行所需的 <paramref name="camera"/> 值。</param>
        public void UpdateHover(Vector2 screenPos, Camera camera)
        {
            if (camera == null)
            {
                ClearHover();
                return;
            }

            if (TryGetCellAndSlotFromRay(camera.ScreenPointToRay(screenPos), out var col, out var row, out var slot))
            {
                SetHoveredCell(col, row, slot);
            }
            else
            {
                ClearHover();
            }
        }

        /// <summary>
        /// 清除 <c>Hover</c>。
        /// </summary>
        public void ClearHover()
        {
            if (_hoveredCell == null)
            {
                return;
            }

            SetControlBlockHover(_hoveredCell, false);
            SetControlBlockColumnHover(_hoveredCell, false);
            if (_cells.TryGetValue(_hoveredCell.Value, out var previousCell) && previousCell != null)
            {
                previousCell.SetHover(false, smallCellFocusColor);
                previousCell.SetHover(false, hoverColor);
            }

            _hoveredCell = null;
        }

        /// <summary>
        /// 清除 <c>Clear</c>。
        /// </summary>
        public void Clear()
        {
            _renderers.Clear();
            _cells.Clear();
            _controlBlockBorders.Clear();
            _hoveredCell = null;
            _previewPathCells.Clear();

            if (gridRoot == null)
            {
                return;
            }

            for (var i = gridRoot.childCount - 1; i >= 0; i--)
            {
                var child = gridRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 应用 <c>Theme</c>。
        /// </summary>
        /// <param name="themedCellHeight">方法执行所需的 <paramref name="themedCellHeight"/> 值。</param>
        /// <param name="themedOwnedColor">方法执行所需的 <paramref name="themedOwnedColor"/> 值。</param>
        /// <param name="themedPollutedColor">方法执行所需的 <paramref name="themedPollutedColor"/> 值。</param>
        /// <param name="themedEnemyColor">方法执行所需的 <paramref name="themedEnemyColor"/> 值。</param>
        /// <param name="themedEmptyColor">方法执行所需的 <paramref name="themedEmptyColor"/> 值。</param>
        public void ApplyTheme(float themedCellHeight, Color themedOwnedColor, Color themedPollutedColor, Color themedEnemyColor, Color themedEmptyColor)
        {
            ApplyTheme(themedCellHeight, surfaceOffsetY, themedOwnedColor, themedPollutedColor, themedEnemyColor, themedEmptyColor);
        }

        /// <summary>
        /// 应用 <c>Theme</c>。
        /// </summary>
        /// <param name="themedCellHeight">方法执行所需的 <paramref name="themedCellHeight"/> 值。</param>
        /// <param name="themedSurfaceOffsetY">方法执行所需的 <paramref name="themedSurfaceOffsetY"/> 值。</param>
        /// <param name="themedOwnedColor">方法执行所需的 <paramref name="themedOwnedColor"/> 值。</param>
        /// <param name="themedPollutedColor">方法执行所需的 <paramref name="themedPollutedColor"/> 值。</param>
        /// <param name="themedEnemyColor">方法执行所需的 <paramref name="themedEnemyColor"/> 值。</param>
        /// <param name="themedEmptyColor">方法执行所需的 <paramref name="themedEmptyColor"/> 值。</param>
        public void ApplyTheme(float themedCellHeight, float themedSurfaceOffsetY, Color themedOwnedColor, Color themedPollutedColor, Color themedEnemyColor, Color themedEmptyColor)
        {
            cellHeight = Mathf.Max(0.005f, themedCellHeight);
            surfaceOffsetY = themedSurfaceOffsetY;
            ownedColor = themedOwnedColor;
            pollutedColor = themedPollutedColor;
            enemyColor = themedEnemyColor;
            emptyColor = themedEmptyColor;

            foreach (var pair in _cells)
            {
                var cell = pair.Value;
                if (cell == null)
                {
                    continue;
                }

                cell.transform.localPosition = CellToLocal(cell.Col, cell.Row, cell.SlotIndex);
                var visualCellSize = GetVisualCellSize();
                cell.transform.localScale = new Vector3(visualCellSize, cellHeight, visualCellSize);
            }
        }

        /// <summary>
        /// 设置 <c>RoutePreview</c>。
        /// </summary>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="startRow">方法执行所需的 <paramref name="startRow"/> 值。</param>
        /// <param name="forward">方法执行所需的 <paramref name="forward"/> 值。</param>
        public void SetRoutePreview(int col, int startRow, bool forward)
        {
            _previewPathCells.Clear();

            if (_map == null || !_map.InBounds(col, startRow))
            {
                RefreshHoverState();
                return;
            }

            if (forward)
            {
                var startBlockRow = _map.GetControlBlockRow(startRow);
                for (var blockRow = startBlockRow; blockRow <= _map.ControlBlockRows; blockRow++)
                {
                    AddPreviewControlBlock(_map.GetControlBlock(_map.GetControlBlockCol(col), blockRow));
                }
            }
            else
            {
                var startBlockRow = _map.GetControlBlockRow(startRow);
                for (var blockRow = startBlockRow; blockRow >= 1; blockRow--)
                {
                    AddPreviewControlBlock(_map.GetControlBlock(_map.GetControlBlockCol(col), blockRow));
                }
            }

            RefreshPreviewState();
            RefreshHoverState();
        }

        /// <summary>
        /// 设置 <c>ControlBlockRangePreview</c>。
        /// </summary>
        /// <param name="col">目标网格列索引。</param>
        /// <param name="row">目标网格行索引。</param>
        /// <param name="range">方法执行所需的 <paramref name="range"/> 值。</param>
        /// <param name="forward">方法执行所需的 <paramref name="forward"/> 值。</param>
        public void SetControlBlockRangePreview(int col, int row, int range, bool forward)
        {
            _previewPathCells.Clear();
            if (_map == null || !_map.InBounds(col, row))
            {
                RefreshHoverState();
                return;
            }

            var blockCol = _map.GetControlBlockCol(col);
            var startBlockRow = _map.GetControlBlockRow(row);
            var maxRange = Mathf.Max(1, range);
            for (var step = 1; step <= maxRange; step++)
            {
                var blockRow = startBlockRow + (forward ? step : -step);
                AddPreviewControlBlock(_map.GetControlBlock(blockCol, blockRow));
            }

            RefreshPreviewState();
            RefreshHoverState();
        }

        private void AddPreviewControlBlock(ControlBlock block)
        {
            if (block == null)
            {
                return;
            }

            foreach (var cell in block.SmallCells)
            {
                _previewPathCells.Add((cell.Col, cell.Row));
            }
        }

        /// <summary>
        /// 清除 <c>RoutePreview</c>。
        /// </summary>
        public void ClearRoutePreview()
        {
            if (_previewPathCells.Count == 0)
            {
                return;
            }

            _previewPathCells.Clear();
            RefreshPreviewState();
            RefreshHoverState();
        }

        private Vector3 CellToLocal(int col, int row)
        {
            var renderCellSize = GetRenderCellSize();
            return new Vector3(
                origin.x + (col - 0.5f) * renderCellSize,
                origin.y + surfaceOffsetY,
                origin.z + (row - 0.5f) * renderCellSize);
        }

        private Vector3 CellToLocal(int col, int row, int slot)
        {
            return _logicalSubGridMode ? CellToLocal(col, row) : CellToLocal(col, row) + GetSubCellCenterLocalOffset(slot);
        }

        private Transform GetSpaceRoot()
        {
            return gridRoot != null ? gridRoot : transform;
        }

        private void SetHoveredCell(int col, int row, int slot)
        {
            var key = (col, row, slot);
            if (_hoveredCell == key)
            {
                return;
            }

            SetControlBlockHover(_hoveredCell, false);
            SetControlBlockColumnHover(_hoveredCell, false);
            if (_hoveredCell != null && _cells.TryGetValue(_hoveredCell.Value, out var previousCell) && previousCell != null)
            {
                previousCell.SetHover(false, smallCellFocusColor);
                previousCell.SetHover(false, hoverColor);
            }

            _hoveredCell = key;
            if (_hoverWholeControlBlock)
            {
                SetControlBlockHover(_hoveredCell, true);
            }
            else if (_cells.TryGetValue(key, out var currentCell) && currentCell != null)
            {
                if (_hoverControlBlockColumnInSmallCellMode)
                {
                    SetControlBlockColumnHover(_hoveredCell, true);
                }

                currentCell.SetHover(true, smallCellFocusColor);
            }
        }

        private void RefreshHoverState()
        {
            if (_hoverWholeControlBlock)
            {
                SetControlBlockHover(_hoveredCell, true);
            }
            else if (_hoveredCell != null && _cells.TryGetValue(_hoveredCell.Value, out var hoveredCell) && hoveredCell != null)
            {
                if (_hoverControlBlockColumnInSmallCellMode)
                {
                    SetControlBlockColumnHover(_hoveredCell, true);
                }

                hoveredCell.SetHover(true, smallCellFocusColor);
            }
        }

        private void SetControlBlockHover((int col, int row, int slot)? hovered, bool enabled)
        {
            if (hovered == null || _map == null)
            {
                return;
            }

            var block = _map.GetControlBlockForSmallCell(hovered.Value.col, hovered.Value.row);
            if (block == null)
            {
                return;
            }

            foreach (var cell in block.SmallCells)
            {
                var slotCount = _logicalSubGridMode ? 1 : GridMap.MaxUnitsPerCell;
                for (var slot = 0; slot < slotCount; slot++)
                {
                    if (_cells.TryGetValue((cell.Col, cell.Row, slot), out var cellView) && cellView != null)
                    {
                        cellView.SetHover(enabled, hoverColor);
                    }
                }
            }
        }

        private void SetControlBlockColumnHover((int col, int row, int slot)? hovered, bool enabled)
        {
            if (hovered == null || _map == null)
            {
                return;
            }

            var blockCol = _map.GetControlBlockCol(hovered.Value.col);
            for (var blockRow = 1; blockRow <= _map.ControlBlockRows; blockRow++)
            {
                var block = _map.GetControlBlock(blockCol, blockRow);
                if (block == null)
                {
                    continue;
                }

                foreach (var cell in block.SmallCells)
                {
                    var slotCount = _logicalSubGridMode ? 1 : GridMap.MaxUnitsPerCell;
                    for (var slot = 0; slot < slotCount; slot++)
                    {
                        if (_cells.TryGetValue((cell.Col, cell.Row, slot), out var cellView) && cellView != null)
                        {
                            cellView.SetHover(enabled, hoverColor);
                        }
                    }
                }
            }
        }

        private void RefreshPreviewState()
        {
            foreach (var pair in _cells)
            {
                var cell = pair.Value;
                if (cell == null)
                {
                    continue;
                }

                cell.SetPreview(_previewPathCells.Contains((cell.Col, cell.Row)), routePreviewColor);
            }
        }

        private void CreateControlBlockBorders(GridMap map)
        {
            if (map == null || gridRoot == null)
            {
                return;
            }

            var material = CreateLitMaterial();
            if (material != null)
            {
                material.color = controlBlockBorderColor;
            }

            for (var blockCol = 1; blockCol <= map.ControlBlockColumns; blockCol++)
            {
                for (var blockRow = 1; blockRow <= map.ControlBlockRows; blockRow++)
                {
                    var startCol = map.GetControlBlockStartSmallCol(blockCol);
                    var endCol = map.GetControlBlockEndSmallCol(blockCol);
                    var startRow = map.GetControlBlockStartSmallRow(blockRow);
                    var endRow = map.GetControlBlockEndSmallRow(blockRow);
                    var min = CellToLocal(startCol, startRow);
                    var max = CellToLocal(endCol, endRow);
                    var center = (min + max) * 0.5f;
                    var width = (endCol - startCol + 1) * GetRenderCellSize();
                    var length = (endRow - startRow + 1) * GetRenderCellSize();
                    var y = origin.y + surfaceOffsetY + cellHeight * 0.9f;
                    CreateBorderSegment($"BlockBorder_{blockCol}_{blockRow}_Top", new Vector3(center.x, y, center.z + length * 0.5f), new Vector3(width, cellHeight * 0.55f, 0.025f), material);
                    CreateBorderSegment($"BlockBorder_{blockCol}_{blockRow}_Bottom", new Vector3(center.x, y, center.z - length * 0.5f), new Vector3(width, cellHeight * 0.55f, 0.025f), material);
                    CreateBorderSegment($"BlockBorder_{blockCol}_{blockRow}_Left", new Vector3(center.x - width * 0.5f, y, center.z), new Vector3(0.025f, cellHeight * 0.55f, length), material);
                    CreateBorderSegment($"BlockBorder_{blockCol}_{blockRow}_Right", new Vector3(center.x + width * 0.5f, y, center.z), new Vector3(0.025f, cellHeight * 0.55f, length), material);
                }
            }
        }

        private void CreateBorderSegment(string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(gridRoot, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = localScale;
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            _controlBlockBorders.Add(obj);
        }

        private bool HasEnemyOccupant(GridCell cell, Func<string, bool> isEnemyOccupant)
        {
            if (cell == null || isEnemyOccupant == null)
            {
                return false;
            }

            var ids = cell.OccupantIds;
            for (var i = 0; i < ids.Count; i++)
            {
                if (isEnemyOccupant(ids[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetSlotLocalOffset(int slotIndex, int slotCapacity)
        {
            var clampedCapacity = Mathf.Clamp(slotCapacity, 1, GridMap.MaxUnitsPerCell);
            var clampedIndex = Mathf.Clamp(slotIndex, 0, clampedCapacity - 1);

            if (clampedCapacity <= 1 || clampedIndex == 0)
            {
                return Vector3.zero;
            }

            var step = cellSize * 0.16f;
            var offsets = new Vector3[]
            {
                Vector3.zero,                  // 0 center
                new Vector3(-step, 0f,  step), // 1 left-top
                new Vector3( step, 0f,  step), // 2 right-top
                new Vector3(-step, 0f, -step), // 3 left-bottom
                new Vector3( step, 0f, -step), // 4 right-bottom
                new Vector3(0f,   0f,  step),  // 5 top
                new Vector3(-step,0f, 0f),     // 6 left
                new Vector3( step,0f, 0f),     // 7 right
                new Vector3(0f,   0f, -step),  // 8 bottom
            };

            return offsets[Mathf.Clamp(clampedIndex, 0, offsets.Length - 1)];
        }

        private Vector3 GetSubCellCenterLocalOffset(int slotIndex)
        {
            var clamped = Mathf.Clamp(slotIndex, 0, GridMap.MaxUnitsPerCell - 1);
            var col = clamped % 3;
            var row = clamped / 3;
            var sub = cellSize / 3f;
            var x = (col - 1) * sub;
            var z = (row - 1) * sub;
            return new Vector3(x, 0f, z);
        }

        private float GetSubCellSize()
        {
            return Mathf.Max(0.02f, cellSize / 3f * 0.96f);
        }

        private float GetRenderCellSize()
        {
            return _logicalSubGridMode ? cellSize / 3f : cellSize;
        }

        private float GetVisualCellSize()
        {
            return _logicalSubGridMode
                ? Mathf.Max(0.02f, GetRenderCellSize() * 0.92f)
                : GetSubCellSize();
        }

        private int RowMajorToInternalSlot(int rowMajor)
        {
            switch (rowMajor)
            {
                case 0: return 1;
                case 1: return 5;
                case 2: return 2;
                case 3: return 6;
                case 4: return 0;
                case 5: return 7;
                case 6: return 3;
                case 7: return 8;
                case 8: return 4;
                default: return 0;
            }
        }

        private Material CreateLitMaterial()
        {
            // Prefer build-safe shaders that preserve per-cell tint in player builds.
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("HDRP/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.03f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.03f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            return material;
        }
    }
}
