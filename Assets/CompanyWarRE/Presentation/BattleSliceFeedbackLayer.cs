using System.Collections.Generic;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceFeedbackLayer : MonoBehaviour
    {
        public const int MaxActiveFeedback = 96;

        private sealed class FeedbackToken
        {
            public GameObject Root;
            public Material RingMaterial;
            public Transform Tracer;
            public Vector3 Start;
            public Vector3 End;
            public bool IsTracer;
            public float Remaining;
            public float Lifetime;
        }

        private readonly List<FeedbackToken> _tokens = new List<FeedbackToken>();

        public void Show(string text, Vector3 localPosition, Color color, float lifetime = 1f)
        {
            EnsureCapacity();
            var root = new GameObject("Feedback_" + text);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = localPosition;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ring.transform.localScale = new Vector3(1.25f, 0.025f, 1.25f);
            var collider = ring.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var ringMaterial = CreateMaterial(color);
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 40;
            label.characterSize = 0.07f;
            label.color = color;

            var safeLifetime = Mathf.Max(0.25f, lifetime);
            _tokens.Add(new FeedbackToken
            {
                Root = root,
                RingMaterial = ringMaterial,
                Remaining = safeLifetime,
                Lifetime = safeLifetime
            });
        }

        public void ShowTracer(
            Vector3 localStart,
            Vector3 localEnd,
            Color color,
            float lifetime = 0.28f)
        {
            EnsureCapacity();
            var root = new GameObject("Feedback_AttackTracer");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = localStart;

            var tracerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tracerObject.name = "Tracer";
            tracerObject.transform.SetParent(root.transform, false);
            tracerObject.transform.localScale = Vector3.one * 0.18f;
            var collider = tracerObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var material = CreateMaterial(color);
            tracerObject.GetComponent<Renderer>().sharedMaterial = material;
            var safeLifetime = Mathf.Max(0.08f, lifetime);
            _tokens.Add(new FeedbackToken
            {
                Root = root,
                RingMaterial = material,
                Tracer = tracerObject.transform,
                Start = localStart,
                End = localEnd,
                IsTracer = true,
                Remaining = safeLifetime,
                Lifetime = safeLifetime
            });
        }

        public void Clear()
        {
            for (var index = _tokens.Count - 1; index >= 0; index--)
            {
                DestroyToken(_tokens[index]);
            }

            _tokens.Clear();
        }

        private void Update()
        {
            var camera = Camera.main;
            for (var index = _tokens.Count - 1; index >= 0; index--)
            {
                var token = _tokens[index];
                token.Remaining -= Time.unscaledDeltaTime;
                if (token.Remaining <= 0f || token.Root == null)
                {
                    DestroyToken(token);
                    _tokens.RemoveAt(index);
                    continue;
                }

                var progress = 1f - token.Remaining / token.Lifetime;
                if (token.IsTracer)
                {
                    token.Root.transform.localPosition = Vector3.Lerp(
                        token.Start,
                        token.End,
                        Mathf.SmoothStep(0f, 1f, progress));
                    if (token.Tracer != null)
                    {
                        var pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.65f;
                        token.Tracer.localScale = Vector3.one * (0.18f * pulse);
                    }
                    continue;
                }

                token.Root.transform.localPosition += Vector3.up * Time.unscaledDeltaTime * 0.55f;
                var scale = 1f + progress * 0.75f;
                var ring = token.Root.transform.Find("Ring");
                if (ring != null)
                {
                    ring.localScale = new Vector3(1.25f * scale, 0.025f, 1.25f * scale);
                }

                var label = token.Root.transform.Find("Label");
                if (label != null && camera != null)
                {
                    label.rotation = Quaternion.LookRotation(label.position - camera.transform.position);
                }
            }
        }

        private void EnsureCapacity()
        {
            while (_tokens.Count >= MaxActiveFeedback)
            {
                DestroyToken(_tokens[0]);
                _tokens.RemoveAt(0);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            return new Material(shader) { color = color };
        }

        private static void DestroyToken(FeedbackToken token)
        {
            if (token == null)
            {
                return;
            }

            if (token.RingMaterial != null)
            {
                Destroy(token.RingMaterial);
            }

            if (token.Root != null)
            {
                Destroy(token.Root);
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
