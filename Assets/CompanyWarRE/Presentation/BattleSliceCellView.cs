using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceCellView : MonoBehaviour
    {
        private static readonly Color OwnedColor = new Color(0.18f, 0.55f, 0.32f);
        private static readonly Color UnownedColor = new Color(0.22f, 0.24f, 0.28f);
        private static readonly Color PollutedColor = new Color(0.55f, 0.16f, 0.58f);
        private static readonly Color BuildingBlockedColor = new Color(0.35f, 0.1f, 0.14f);
        private static readonly Color SelectedColor = new Color(0.95f, 0.76f, 0.18f);

        private Renderer _cellRenderer;
        private MaterialPropertyBlock _propertyBlock;

        public GridPosition Position { get; private set; }

        public void Initialize(GridPosition position, Material sharedMaterial)
        {
            Position = position;
            _cellRenderer = GetComponent<Renderer>();
            _cellRenderer.sharedMaterial = sharedMaterial;
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Render(BattleSliceCellSnapshot snapshot, bool selected)
        {
            if (_cellRenderer == null || _propertyBlock == null || snapshot == null)
            {
                return;
            }

            var color = snapshot.IsBlockedByBuilding
                ? BuildingBlockedColor
                : snapshot.IsPolluted
                    ? PollutedColor
                    : snapshot.IsOwned ? OwnedColor : UnownedColor;
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", selected ? SelectedColor : color);
            _propertyBlock.SetColor("_Color", selected ? SelectedColor : color);
            _cellRenderer.SetPropertyBlock(_propertyBlock);
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                selected ? 0.12f : 0f,
                transform.localPosition.z);
        }

    }
}
