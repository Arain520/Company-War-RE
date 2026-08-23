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
        private static readonly Color SelectedColor = new Color(0.95f, 0.76f, 0.18f);

        private Renderer _cellRenderer;
        private Material _cellMaterial;

        public GridPosition Position { get; private set; }

        public void Initialize(GridPosition position)
        {
            Position = position;
            _cellRenderer = GetComponent<Renderer>();
            _cellMaterial = CreateMaterial(OwnedColor);
            _cellRenderer.sharedMaterial = _cellMaterial;

        }

        public void Render(BattleSliceCellSnapshot snapshot, bool selected)
        {
            if (_cellMaterial == null || snapshot == null)
            {
                return;
            }

            _cellMaterial.color = selected
                ? SelectedColor
                : snapshot.IsPolluted
                    ? PollutedColor
                    : snapshot.IsOwned ? OwnedColor : UnownedColor;
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                selected ? 0.12f : 0f,
                transform.localPosition.z);
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                color = color
            };
            return material;
        }

        private void OnDestroy()
        {
            if (_cellMaterial != null)
            {
                Destroy(_cellMaterial);
            }

        }
    }
}
