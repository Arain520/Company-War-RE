using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Cow board visual contract. Values are expressed in normalized target units where one
    /// rendered small cell has a pitch of one. Cow's source pitch was 1.2 / 3 = 0.4 units.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CowBoardVisualTheme",
        menuName = "Company War-RE/Presentation/Cow Board Visual Theme")]
    public sealed class CowBoardVisualTheme : ScriptableObject
    {
        public const int SourceSubGridFactor = 3;
        public const float SourceControlBlockSize = 1.2f;
        public const float SourceSmallCellPitch = SourceControlBlockSize / SourceSubGridFactor;

        [Header("Industrial ring")]
        [SerializeField, Min(0)] private int decorRing = 2;
        [SerializeField, Min(0.01f)] private float platformThickness = 0.35f;
        [SerializeField, Min(0.01f)] private float battlePlateThickness = 0.065f;
        [SerializeField, Min(0.01f)] private float outerWallHeight = 0.95f;
        [SerializeField, Min(0.01f)] private float outerWallThickness = 0.55f;

        [Header("Cow formal Battle palette")]
        [SerializeField] private Color platformColor = new Color(0.48f, 0.50f, 0.50f, 1f);
        [SerializeField] private Color tileColorA = new Color(0.62f, 0.63f, 0.62f, 1f);
        [SerializeField] private Color tileColorB = new Color(0.54f, 0.55f, 0.54f, 1f);
        [SerializeField] private Color battlePlateColor = new Color(0.035f, 0.055f, 0.075f, 1f);
        [SerializeField] private Color borderColor = new Color(0.07f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color warningColor = new Color(0.86f, 0.66f, 0.06f, 1f);
        [SerializeField] private Color wallColor = new Color(0.38f, 0.39f, 0.39f, 1f);
        [SerializeField] private Color blueEmission = new Color(0.04f, 0.34f, 0.72f, 1f);
        [SerializeField] private Color warningEmission = new Color(0.85f, 0.55f, 0.02f, 1f);

        public int DecorRing => Mathf.Max(0, decorRing);
        public float PlatformThickness => Mathf.Max(0.01f, platformThickness);
        public float BattlePlateThickness => Mathf.Max(0.01f, battlePlateThickness);
        public float OuterWallHeight => Mathf.Max(0.01f, outerWallHeight);
        public float OuterWallThickness => Mathf.Max(0.01f, outerWallThickness);
        public Color PlatformColor => platformColor;
        public Color TileColorA => tileColorA;
        public Color TileColorB => tileColorB;
        public Color BattlePlateColor => battlePlateColor;
        public Color BorderColor => borderColor;
        public Color WarningColor => warningColor;
        public Color WallColor => wallColor;
        public Color BlueEmission => blueEmission;
        public Color WarningEmission => warningEmission;
    }
}
