using UnityEngine;

namespace CompanyWarRE.Presentation
{
    [DisallowMultipleComponent]
    public sealed class FormalBattleMapView : MonoBehaviour
    {
        public const int EnvironmentLayer = 2; // Unity built-in Ignore Raycast.

        [SerializeField] private string mapId = "BASE";
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Transform battleBoardAnchor;
        [SerializeField, Min(1)] private int previewColumns = 18;
        [SerializeField, Min(1)] private int previewRows = 30;
        [SerializeField] private bool showBoardGizmos = true;
        [SerializeField] private Color smallCellColor = new Color(0.2f, 0.65f, 1f, 0.28f);
        [SerializeField] private Color controlBlockColor = new Color(0.1f, 0.9f, 1f, 0.9f);
        [SerializeField] private Color forwardColor = new Color(1f, 0.65f, 0.1f, 1f);

        public string MapId => mapId;
        public Transform EnvironmentRoot => environmentRoot;
        public Transform BattleBoardAnchor => battleBoardAnchor != null ? battleBoardAnchor : transform;
        public int PreviewColumns => previewColumns;
        public int PreviewRows => previewRows;

        public void Configure(
            string value,
            Transform environment,
            Transform boardAnchor,
            int columns,
            int rows)
        {
            mapId = string.IsNullOrWhiteSpace(value) ? "BASE" : value.Trim();
            environmentRoot = environment;
            battleBoardAnchor = boardAnchor;
            previewColumns = Mathf.Max(1, columns);
            previewRows = Mathf.Max(1, rows);
        }

        public void SetPreviewSize(int columns, int rows)
        {
            previewColumns = Mathf.Max(1, columns);
            previewRows = Mathf.Max(1, rows);
        }

        public bool HasUniformAnchorScale(float tolerance = 0.0001f)
        {
            var scale = BattleBoardAnchor.lossyScale;
            return Mathf.Abs(scale.x - scale.y) <= tolerance &&
                   Mathf.Abs(scale.y - scale.z) <= tolerance;
        }

        public void NormalizeAnchorScale()
        {
            var anchor = BattleBoardAnchor;
            var world = anchor.lossyScale;
            var uniform = Mathf.Max(
                0.0001f,
                (Mathf.Abs(world.x) + Mathf.Abs(world.y) + Mathf.Abs(world.z)) / 3f);
            var parentScale = anchor.parent != null ? anchor.parent.lossyScale : Vector3.one;
            anchor.localScale = new Vector3(
                uniform / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                uniform / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                uniform / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        public void PrepareForBattle(int columns, int rows)
        {
            SetPreviewSize(columns, rows);
            if (!HasUniformAnchorScale())
            {
                Debug.LogError(
                    $"Map '{mapId}' BattleBoardAnchor must use uniform scale. Current lossy scale: " +
                    BattleBoardAnchor.lossyScale + ". Runtime instance was normalized.",
                    this);
                NormalizeAnchorScale();
            }

            if (environmentRoot == null)
            {
                return;
            }

            SetLayerRecursively(environmentRoot, EnvironmentLayer);
            foreach (var collider in environmentRoot.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var index = 0; index < root.childCount; index++)
            {
                SetLayerRecursively(root.GetChild(index), layer);
            }
        }

        private void OnDrawGizmos()
        {
            if (showBoardGizmos)
            {
                DrawBoardGizmos();
            }
        }

        private void DrawBoardGizmos()
        {
            var anchor = BattleBoardAnchor;
            var columns = Mathf.Max(1, previewColumns);
            var rows = Mathf.Max(1, previewRows);
            var halfWidth = columns * 0.5f;
            var halfLength = rows * 0.5f;
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = anchor.localToWorldMatrix;

            for (var boundary = 0; boundary <= columns; boundary++)
            {
                var major = boundary % 3 == 0 || boundary == columns;
                Gizmos.color = major ? controlBlockColor : smallCellColor;
                var x = boundary - halfWidth;
                Gizmos.DrawLine(new Vector3(x, 0f, -halfLength), new Vector3(x, 0f, halfLength));
            }

            for (var boundary = 0; boundary <= rows; boundary++)
            {
                var major = boundary % 3 == 0 || boundary == rows;
                Gizmos.color = major ? controlBlockColor : smallCellColor;
                var z = boundary - halfLength;
                Gizmos.DrawLine(new Vector3(-halfWidth, 0f, z), new Vector3(halfWidth, 0f, z));
            }

            Gizmos.color = forwardColor;
            var arrowLength = Mathf.Min(4f, Mathf.Max(1.5f, rows * 0.18f));
            var tip = new Vector3(0f, 0.08f, halfLength + arrowLength);
            var origin = new Vector3(0f, 0.08f, halfLength);
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, tip + new Vector3(-0.45f, 0f, -0.7f));
            Gizmos.DrawLine(tip, tip + new Vector3(0.45f, 0f, -0.7f));

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
