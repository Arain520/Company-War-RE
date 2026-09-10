using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>Map-owned presentation parameters. Logical dimensions still come from the level.</summary>
    [DisallowMultipleComponent]
    public sealed class SkyBattlefieldSettings : MonoBehaviour
    {
        [SerializeField] private GameObject pillarModel;
        [SerializeField, Min(0.1f)] private float pillarWidth = 3f;
        [SerializeField, Min(0f)] private float gapRatio = 0.5f;
        [SerializeField, Min(0.1f)] private float minimumHeight = 29f;
        [SerializeField, Min(0.1f)] private float maximumHeight = 35f;
        [SerializeField] private int heightSeed = 1977;
        [SerializeField, Min(0.01f)] private float visualScale = 4f;
        [SerializeField] private Vector2 cameraAngles = new Vector2(48f, -32f);

        public Vector2 CameraAngles => cameraAngles;

        public void Configure(GameObject model, float width, float gap, float minimum,
            float maximum, int seed, float scale)
        {
            pillarModel = model;
            pillarWidth = Mathf.Max(0.1f, width);
            gapRatio = Mathf.Max(0f, gap);
            minimumHeight = Mathf.Max(0.1f, Mathf.Min(minimum, maximum));
            maximumHeight = Mathf.Max(minimumHeight, Mathf.Max(minimum, maximum));
            heightSeed = seed;
            visualScale = Mathf.Max(0.01f, scale);
        }

        public void ApplyTo(FormalBattleBoardView board)
        {
            board.ConfigureSkyPillars(pillarModel, pillarWidth, gapRatio,
                minimumHeight, maximumHeight, heightSeed, visualScale);
        }
    }
}
