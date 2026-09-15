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
        private readonly Color _eradicationColor = new Color(1f, 0.58f, 0.12f);
        private readonly Color _invalidEradicationColor = new Color(0.52f, 0.18f, 0.15f);

        private Renderer _cellRenderer;
        private Renderer _territoryOverlayRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private float _baseY;

        public GridPosition Position { get; private set; }
        public bool IsTerritoryOverlayVisible =>
            _territoryOverlayRenderer != null && _territoryOverlayRenderer.enabled;

        public void Initialize(GridPosition position, Material sharedMaterial)
        {
            Position = position;
            _cellRenderer = GetComponent<Renderer>();
            _cellRenderer.sharedMaterial = sharedMaterial;
            _propertyBlock = new MaterialPropertyBlock();
            _baseY = transform.localPosition.y;
        }

        public void ConfigureTerritoryOverlay(Material sharedMaterial, float localHeight)
        {
            if (sharedMaterial == null || _territoryOverlayRenderer != null)
            {
                return;
            }

            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "AllyTerritoryOverlay";
            overlay.layer = gameObject.layer;
            overlay.transform.SetParent(transform, false);
            overlay.transform.localPosition = new Vector3(0f, localHeight, 0f);
            // Unity's built-in Quad faces local -Z. Rotate it so the visible face
            // points upward toward the battle camera.
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localScale = Vector3.one;

            var overlayCollider = overlay.GetComponent<Collider>();
            if (overlayCollider != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(overlayCollider);
                else DestroyImmediate(overlayCollider);
            }

            _territoryOverlayRenderer = overlay.GetComponent<Renderer>();
            _territoryOverlayRenderer.sharedMaterial = sharedMaterial;
            _territoryOverlayRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _territoryOverlayRenderer.receiveShadows = false;
            _territoryOverlayRenderer.enabled = false;
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

        public void SetVisualVisible(bool visible)
        {
            if (_cellRenderer != null)
            {
                _cellRenderer.enabled = visible;
            }
        }

        public void Render(
            BattleSliceCellSnapshot snapshot,
            bool selected,
            bool eradicationPreview = false,
            bool validEradicationTarget = false)
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
            var selectedColor = eradicationPreview
                ? validEradicationTarget ? _eradicationColor : _invalidEradicationColor
                : _selectedColor;
            _propertyBlock.Clear();
            _propertyBlock.SetColor("_BaseColor", selected ? selectedColor : color);
            _propertyBlock.SetColor("_Color", selected ? selectedColor : color);
            _cellRenderer.SetPropertyBlock(_propertyBlock);
            if (_territoryOverlayRenderer != null)
            {
                _territoryOverlayRenderer.enabled = snapshot.IsOwned;
            }
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                _baseY + (selected ? 0.06f : 0f),
                transform.localPosition.z);
        }

    }
}
