using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Runtime presentation counterpart of one logical 3x3 control block.
    /// </summary>
    public sealed class BattlePillarView : MonoBehaviour
    {
        private Transform _body;

        public GridPosition ControlBlockPosition { get; private set; }
        public Transform TopAnchor { get; private set; }
        public Transform BuildAnchor { get; private set; }
        public float Height { get; private set; }
        public float BottomY { get; private set; }

        public void Initialize(
            GridPosition controlBlockPosition,
            float width,
            float height,
            Material bodyMaterial,
            Material topMaterial,
            float topThickness)
        {
            ControlBlockPosition = controlBlockPosition;
            Height = Mathf.Max(0.1f, height);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, Height, width);
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            _body = body.transform;
            BottomY = transform.localPosition.y;

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TopSurface";
            top.transform.SetParent(transform, false);
            var safeTopThickness = Mathf.Max(0.01f, topThickness);
            // Keep the authored pillar height equal to the usable top elevation.
            // The cap extends downward so TopAnchor and coordinate mapping stay exact.
            top.transform.localPosition = new Vector3(0f, Height - safeTopThickness * 0.5f, 0f);
            top.transform.localScale = new Vector3(width, safeTopThickness, width);
            top.GetComponent<Renderer>().sharedMaterial = topMaterial;
            var topCollider = top.GetComponent<Collider>();
            if (topCollider != null)
            {
                topCollider.enabled = false;
            }

            TopAnchor = new GameObject("TopAnchor").transform;
            TopAnchor.SetParent(transform, false);
            TopAnchor.localPosition = new Vector3(0f, Height, 0f);

            BuildAnchor = new GameObject("BuildAnchor").transform;
            BuildAnchor.SetParent(TopAnchor, false);
            BuildAnchor.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Extends only the visual body downward. TopAnchor and the logical top elevation
        /// remain unchanged, so deployment and combat coordinates are unaffected.
        /// </summary>
        public void ExtendBodyToBottom(float bottomYInParentSpace)
        {
            if (_body == null)
            {
                return;
            }

            var topYInParentSpace = transform.localPosition.y + Height;
            BottomY = Mathf.Min(bottomYInParentSpace, topYInParentSpace - 0.1f);
            var bottomYInPillarSpace = BottomY - transform.localPosition.y;
            var visualHeight = Height - bottomYInPillarSpace;
            var scale = _body.localScale;
            _body.localPosition = new Vector3(
                0f,
                (Height + bottomYInPillarSpace) * 0.5f,
                0f);
            _body.localScale = new Vector3(scale.x, visualHeight, scale.z);
        }
    }
}
