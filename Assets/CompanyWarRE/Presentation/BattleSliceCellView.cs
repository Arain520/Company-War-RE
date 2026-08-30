using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceCellView : MonoBehaviour
    {
        private Color _ownedColor = new Color(0.04f, 0.24f, 0.40f);
        private Color _unownedColor = new Color(0.08f, 0.10f, 0.13f);
        private Color _pollutedColor = new Color(0.01f, 0.025f, 0.06f);
        private Color _buildingBlockedColor = new Color(0.54f, 0.16f, 0.16f);
        private Color _selectedColor = new Color(0.95f, 0.78f, 0.20f);

        private Renderer _cellRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private float _baseY;

        public GridPosition Position { get; private set; }

        public void Initialize(GridPosition position, Material sharedMaterial)
        {
            Position = position;
            _cellRenderer = GetComponent<Renderer>();
            _cellRenderer.sharedMaterial = sharedMaterial;
            _propertyBlock = new MaterialPropertyBlock();
            _baseY = transform.localPosition.y;
        }

        public void ApplyCowBoardPalette(FormalBattleBoardView board)
        {
            if (board == null)
            {
                return;
            }

            _ownedColor = board.OwnedColor;
            _unownedColor = board.EmptyColor;
            _pollutedColor = board.PollutedColor;
            _buildingBlockedColor = board.EnemyColor;
            _selectedColor = board.SelectedColor;
        }

        public void Render(BattleSliceCellSnapshot snapshot, bool selected)
        {
            if (_cellRenderer == null || _propertyBlock == null || snapshot == null)
            {
                return;
            }

            var color = snapshot.IsBlockedByBuilding
                ? _buildingBlockedColor
                : snapshot.IsPolluted
                    ? _pollutedColor
                    : snapshot.IsOwned ? _ownedColor : _unownedColor;
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", selected ? _selectedColor : color);
            _propertyBlock.SetColor("_Color", selected ? _selectedColor : color);
            _cellRenderer.SetPropertyBlock(_propertyBlock);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                _baseY + (selected ? 0.06f : 0f),
                transform.localPosition.z);
        }

    }
}
