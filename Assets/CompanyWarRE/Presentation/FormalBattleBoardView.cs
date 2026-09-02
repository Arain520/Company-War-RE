using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Reusable runtime board root. Level configuration controls dimensions and content;
    /// the prefab owns only Presentation containers and never stores Domain state.
    /// </summary>
    public sealed class FormalBattleBoardView : MonoBehaviour
    {
        // Cow CompanyWarGrid3DRenderer visual contract. The board is procedural in
        // Cow (there is no source board mesh/prefab), so these values are serialized
        // on the reusable target prefab instead of being hidden in controller code.
        [SerializeField, Range(0.5f, 1f)] private float cellVisualFill = 0.92f;
        [SerializeField] private float cellHeight = 0.1f;
        [SerializeField] private float surfaceOffsetY = -0.02f;
        [SerializeField] private float controlBlockBorderWidth = 0.0625f;
        [SerializeField] private float columnGroupBorderWidth = 0.1f;
        [SerializeField] private Color ownedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color pollutedColor = new Color(0.6f, 0.4f, 0.2f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.7f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.78f, 0.20f, 1f);
        [SerializeField] private Color controlBlockBorderColor = new Color(0.12f, 0.62f, 1f, 1f);

        public Transform GridRoot { get; private set; }
        public Transform CombatantRoot { get; private set; }
        public Transform FeedbackRoot { get; private set; }
        public float CellVisualFill => cellVisualFill;
        public float CellHeight => cellHeight;
        public float SurfaceOffsetY => surfaceOffsetY;
        public float ControlBlockBorderWidth => controlBlockBorderWidth;
        public float ColumnGroupBorderWidth => columnGroupBorderWidth;
        public Color OwnedColor => ownedColor;
        public Color PollutedColor => pollutedColor;
        public Color EnemyColor => enemyColor;
        public Color EmptyColor => emptyColor;
        public Color SelectedColor => selectedColor;
        public Color ControlBlockBorderColor => controlBlockBorderColor;

        public void Prepare(int columns, int rows)
        {
            gameObject.name = $"FormalBattleBoard_{columns}x{rows}";
            var cowVisual = GetComponent<CowBoardVisualRenderer>() ??
                            gameObject.AddComponent<CowBoardVisualRenderer>();
            cowVisual.Build(columns, rows);
            GridRoot = FindOrCreate("RuntimeGrid");
            CombatantRoot = FindOrCreate("RuntimeCombatants");
            FeedbackRoot = FindOrCreate("RuntimeFeedback");
        }

        private Transform FindOrCreate(string childName)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                return child;
            }

            var root = new GameObject(childName).transform;
            root.SetParent(transform, false);
            return root;
        }
    }
}
