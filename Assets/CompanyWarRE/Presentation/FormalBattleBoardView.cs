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

        [Header("Pillar Presentation")]
        [SerializeField, Min(0.01f)] private float globalVisualScale = 4f;
        [SerializeField] private bool usePillarPresentation = true;
        [SerializeField] private bool showLegacyGroundVisual;
        [SerializeField] private bool showLogicalCellOverlay;
        [SerializeField, Min(0.1f)] private float pillarWidth = 3f;
        [SerializeField, Min(0f)] private float pillarGapRatio = 0.5f;
        [SerializeField, Min(0.1f)] private float uniformPillarHeight = 32f;
        [SerializeField] private bool randomizePillarHeights = true;
        [SerializeField, Min(0.1f)] private float minimumRandomPillarHeight = 26f;
        [SerializeField, Min(0.1f)] private float maximumRandomPillarHeight = 38f;
        [SerializeField] private int pillarHeightSeed = 1977;
        [SerializeField] private float pillarBaseY;
        [SerializeField, Min(0.01f)] private float pillarTopThickness = 0.12f;
        [SerializeField, Min(0f)] private float flyingUnitVisualSpeedScale = 1f;
        [SerializeField] private Color pillarBodyColor = new Color(0.32f, 0.38f, 0.46f, 1f);
        [SerializeField] private Color pillarTopColor = new Color(0.72f, 0.75f, 0.74f, 1f);

        public Transform GridRoot { get; private set; }
        public Transform PillarRoot { get; private set; }
        public Transform CombatantRoot { get; private set; }
        public Transform FeedbackRoot { get; private set; }
        public BattleBoardCoordinateMapper CoordinateMapper { get; private set; }
        public BattlePillarPresentationGenerator PillarGenerator { get; private set; }
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
        public float GlobalVisualScale => Mathf.Max(0.01f, globalVisualScale);
        public bool UsePillarPresentation => usePillarPresentation;
        public bool ShowLogicalCellOverlay => showLogicalCellOverlay;
        public float FlyingUnitVisualSpeedScale => flyingUnitVisualSpeedScale;
        public float PresentationWidth => CoordinateMapper != null
            ? CoordinateMapper.VisualWidth
            : 0f;
        public float PresentationLength => CoordinateMapper != null
            ? CoordinateMapper.VisualLength
            : 0f;

        public void Prepare(
            int columns,
            int rows,
            IBattlePillarHeightProvider heightProvider = null)
        {
            gameObject.name = $"FormalBattleBoard_{columns}x{rows}";
            var cowVisual = GetComponent<CowBoardVisualRenderer>() ??
                            gameObject.AddComponent<CowBoardVisualRenderer>();
            if (showLegacyGroundVisual || !usePillarPresentation)
            {
                cowVisual.Build(columns, rows);
            }

            GridRoot = FindOrCreate("RuntimeGrid");
            PillarRoot = FindOrCreate("RuntimePillars");
            CombatantRoot = FindOrCreate("RuntimeCombatants");
            FeedbackRoot = FindOrCreate("RuntimeFeedback");

            if (!usePillarPresentation)
            {
                CoordinateMapper = null;
                PillarGenerator = null;
                PillarRoot.gameObject.SetActive(false);
                return;
            }

            PillarRoot.gameObject.SetActive(true);
            var baseHeightProvider = heightProvider ??
                                     (randomizePillarHeights
                                         ? (IBattlePillarHeightProvider)
                                         new SeededRandomBattlePillarHeightProvider(
                                             minimumRandomPillarHeight,
                                             maximumRandomPillarHeight,
                                             pillarHeightSeed)
                                         : new UniformBattlePillarHeightProvider(
                                             uniformPillarHeight));
            var resolvedHeightProvider = new ScaledBattlePillarHeightProvider(
                baseHeightProvider,
                GlobalVisualScale);
            CoordinateMapper = new BattleBoardCoordinateMapper(
                columns,
                rows,
                pillarWidth * GlobalVisualScale,
                pillarGapRatio,
                pillarBaseY * GlobalVisualScale,
                resolvedHeightProvider);
            PillarGenerator = PillarRoot.GetComponent<BattlePillarPresentationGenerator>() ??
                              PillarRoot.gameObject.AddComponent<BattlePillarPresentationGenerator>();
            PillarGenerator.Build(
                CoordinateMapper,
                pillarBodyColor,
                pillarTopColor,
                pillarTopThickness * GlobalVisualScale);
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
