using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyWarRE.Presentation
{
    /// <summary>A mesh-only preview: never instantiates scripts, physics or combat actors.</summary>
    [ExecuteAlways]
    public sealed class BattleDeploymentGhostView : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Mesh> _bakedMeshes = new List<Mesh>();
        private LineRenderer _outline;
        private Material _outlineMaterial;
        private bool? _valid;

        public void Initialize(BattleSliceVisualCatalog catalog, string unitId, bool building, float scale)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            if (catalog != null && catalog.TryResolve(unitId, out var resolved))
            {
                var copy = CopyMeshes(resolved.Prefab.transform, visual, resolved.MaterialOverride);
                copy.localPosition = resolved.LocalPosition;
                copy.localRotation = resolved.LocalRotation;
                copy.localScale = resolved.LocalScale;
            }
            if (visual.GetComponentsInChildren<Renderer>().Length == 0)
            {
                var fallback = GameObject.CreatePrimitive(building ? PrimitiveType.Cube : PrimitiveType.Capsule);
                fallback.transform.SetParent(visual, false);
                fallback.name = "FallbackVisual";
                var collider = fallback.GetComponent<Collider>();
                collider.enabled = false;
                Release(collider);
                fallback.GetComponent<Renderer>().sharedMaterial = GhostMaterial(null);
            }
            var renderers = visual.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.localScale *= BattleSliceCombatantView.CalculateFootprintScale(bounds.size, building) * scale;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.localPosition -= transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var outline = new GameObject("Footprint");
            outline.transform.SetParent(transform, false);
            _outline = outline.AddComponent<LineRenderer>();
            _outline.useWorldSpace = false;
            _outline.loop = true;
            _outline.positionCount = 4;
            _outline.widthMultiplier = 0.035f * scale;
            _outlineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            _outline.sharedMaterial = _outlineMaterial;
            _outline.shadowCastingMode = ShadowCastingMode.Off;
            _outline.receiveShadows = false;
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            gameObject.SetActive(false);
        }

        public void Show(Vector3 localPosition, Vector2 footprint, bool valid)
        {
            transform.localPosition = localPosition;
            gameObject.SetActive(true);
            var x = footprint.x * 0.5f;
            var z = footprint.y * 0.5f;
            _outline.SetPositions(new[] { new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z),
                new Vector3(x, 0f, z), new Vector3(x, 0f, -z) });
            if (_valid == valid) return;
            _valid = valid;
            var indicator = valid ? new Color(0.18f, 1f, 0.42f) : new Color(1f, 0.12f, 0.12f);
            _outlineMaterial.color = indicator;
            if (_outlineMaterial.HasProperty("_BaseColor")) _outlineMaterial.SetColor("_BaseColor", indicator);
            for (var index = 0; index < _materials.Count; index++)
            {
                var color = valid ? _colors[index] : Color.Lerp(_colors[index], indicator, 0.75f);
                color.a = 0.45f;
                _materials[index].color = color;
                _materials[index].SetColor("_BaseColor", color);
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private Transform CopyMeshes(Transform source, Transform parent, Material materialOverride)
        {
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            var sourceRenderer = source.GetComponent<Renderer>();
            Mesh mesh = null;
            if (sourceRenderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                mesh = new Mesh { name = "DeploymentPreviewPose" };
                skinned.BakeMesh(mesh);
                _bakedMeshes.Add(mesh);
            }
            else if (sourceRenderer is MeshRenderer)
            {
                // Unity's missing component wrappers require its overloaded null check.
                var filter = source.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh != null && sourceRenderer.enabled)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.gameObject.AddComponent<MeshRenderer>();
                var materials = sourceRenderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                    materials[index] = GhostMaterial(materialOverride != null ? materialOverride : materials[index]);
                renderer.sharedMaterials = materials;
            }
            for (var index = 0; index < source.childCount; index++) CopyMeshes(source.GetChild(index), copy, materialOverride);
            return copy;
        }

        private Material GhostMaterial(Material source)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = source != null ? new Material(source) : new Material(shader);
            material.shader = shader;
            material.name = "MAT_DeploymentGhost";
            var color = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source != null && source.HasProperty("_Color") ? source.color : new Color(0.3f, 0.8f, 0.9f);
            color.a = 0.45f;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_EMISSION");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            _materials.Add(material);
            _colors.Add(color);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in _materials) Release(material);
            foreach (var mesh in _bakedMeshes) Release(mesh);
            Release(_outlineMaterial);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
