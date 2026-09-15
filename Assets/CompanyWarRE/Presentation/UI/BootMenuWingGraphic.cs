using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    /// <summary>Draws the five-point corporate wing silhouette used by the menu buttons.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BootMenuWingGraphic : MaskableGraphic
    {
        [SerializeField] private bool pointRight = true;
        [SerializeField, Range(0.02f, 0.35f)] private float pointRatio = 0.16f;
        [SerializeField, Range(0f, 0.25f)] private float cutRatio = 0.08f;
        [SerializeField, Range(0.05f, 0.45f)] private float tipTopRatio = 0.16f;

        public void Configure(bool pointsRight, float point = 0.16f, float cut = 0.08f)
        {
            pointRight = pointsRight;
            pointRatio = Mathf.Clamp(point, 0.02f, 0.35f);
            cutRatio = Mathf.Clamp(cut, 0f, 0.25f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var tip = rect.width * pointRatio;
            var cut = rect.width * cutRatio;
            var tipY = rect.yMax - rect.height * tipTopRatio;
            Vector2[] points = pointRight
                ? new[]
                {
                    new Vector2(rect.xMin + cut, rect.yMax),
                    new Vector2(rect.xMax - tip * 0.18f, rect.yMax),
                    new Vector2(rect.xMax, tipY),
                    new Vector2(rect.xMax - tip, rect.yMin),
                    new Vector2(rect.xMin, rect.yMin)
                }
                : new[]
                {
                    new Vector2(rect.xMax - cut, rect.yMax),
                    new Vector2(rect.xMin + tip * 0.18f, rect.yMax),
                    new Vector2(rect.xMin, tipY),
                    new Vector2(rect.xMin + tip, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin)
                };

            var center = Vector2.zero;
            foreach (var point in points) center += point;
            center /= points.Length;

            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertexHelper.AddVert(vertex);
            foreach (var point in points)
            {
                vertex.position = point;
                vertexHelper.AddVert(vertex);
            }

            for (var index = 1; index <= points.Length; index++)
            {
                vertexHelper.AddTriangle(0, index, index == points.Length ? 1 : index + 1);
            }
        }
    }
}
