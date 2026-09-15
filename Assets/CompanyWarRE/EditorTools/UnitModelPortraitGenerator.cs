using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    [InitializeOnLoad]
    public static class UnitModelPortraitGenerator
    {
        private const string Output = "Assets/CompanyWarRE/Resources/UnitPortraits/ModelPreviews";
        private const string Request = "Temp/GenerateUnitModelPortraits.request";
        [Serializable] private sealed class UnitList { public UnitEntry[] Units; }
        [Serializable] private sealed class UnitEntry { public string Id; }

        static UnitModelPortraitGenerator() { EditorApplication.delayCall += RunRequested; }

        private static void RunRequested()
        {
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Generate(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        [MenuItem("Company War/Art/Generate Unit Model Portraits")]
        public static void Generate()
        {
            Directory.CreateDirectory(Output);
            var units = JsonUtility.FromJson<UnitList>(File.ReadAllText(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.All.json")).Units;
            var temporaryCatalog = new GameObject("PortraitCatalog") { hideFlags = HideFlags.HideAndDontSave };
            var catalog = UnityEngine.Object.FindObjectOfType<BattleSliceVisualCatalog>();
            if (catalog == null) catalog = temporaryCatalog.AddComponent<BattleSliceVisualCatalog>();
            var completed = new List<string>();
            try
            {
                foreach (var unit in units)
                {
                    if (!catalog.TryResolve(unit.Id, out var visual))
                        throw new InvalidOperationException("Missing model: " + unit.Id);
                    // The formal scene overrides U01 with this prefab.
                    if (unit.Id == "U01")
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CompanyWarRE/Content/Prefabs/Units/PF_U01.prefab");
                        if (prefab != null) visual = new BattleSliceVisualCatalog.ResolvedVisual(
                            prefab, visual.MaterialOverride, visual.LocalPosition, visual.LocalRotation, visual.LocalScale);
                    }
                    Render(unit.Id, visual);
                    completed.Add(unit.Id);
                }
                File.WriteAllText("Migration/UnitModelPortraits.txt", "Generated " + completed.Count + " model portraits\n" + string.Join("\n", completed));
                Debug.Log("Generated unit model portraits: " + completed.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporaryCatalog); }
        }

        private static Transform CopyMeshes(Transform source, Transform parent, Material material, List<Mesh> baked)
        {
            var copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            var renderer = source.GetComponent<Renderer>();
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                mesh = new Mesh();
                skinned.BakeMesh(mesh);
                baked.Add(mesh);
            }
            else if (renderer is MeshRenderer)
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            if (mesh != null && renderer.enabled)
            {
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials
                    .Select(value => material != null ? material : value).ToArray();
            }
            foreach (Transform child in source) CopyMeshes(child, copy, material, baked);
            return copy;
        }

        private static void Render(string id, BattleSliceVisualCatalog.ResolvedVisual visual)
        {
            var preview = new PreviewRenderUtility();
            var root = new GameObject("PortraitModel");
            var baked = new List<Mesh>();
            Texture2D texture = null;
            try
            {
                preview.AddSingleGO(root);
                var model = CopyMeshes(visual.Prefab.transform, root.transform, visual.MaterialOverride, baked);
                model.localPosition = visual.LocalPosition;
                model.localRotation = visual.LocalRotation;
                model.localScale = visual.LocalScale;
                var renderers = root.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("Empty model: " + id);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var camera = preview.camera;
                camera.orthographic = true;
                camera.aspect = 1.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.transform.rotation = Quaternion.Euler(22f, 145f, 0f);
                float width = 0f, height = 0f, depth = 0f;
                for (var i = 0; i < 8; i++)
                {
                    var corner = Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var projected = Quaternion.Inverse(camera.transform.rotation) * corner;
                    width = Mathf.Max(width, Mathf.Abs(projected.x));
                    height = Mathf.Max(height, Mathf.Abs(projected.y));
                    depth = Mathf.Max(depth, Mathf.Abs(projected.z));
                }
                camera.orthographicSize = Mathf.Max(0.01f, Mathf.Max(height, width / camera.aspect) * 1.12f);
                var distance = Mathf.Max(bounds.size.magnitude * 2f, 1f);
                camera.transform.position = bounds.center - camera.transform.forward * distance;
                camera.nearClipPlane = Mathf.Max(0.001f, distance - depth * 1.5f);
                camera.farClipPlane = distance + depth * 1.5f + 1f;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, 115f, 0f);
                preview.lights[1].intensity = 0.8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340f, 290f, 0f);
                preview.ambientColor = new Color(0.55f, 0.55f, 0.55f);
                preview.BeginPreview(new Rect(0, 0, 768, 512), GUIStyle.none);
                preview.Render(true);
                var rendered = (RenderTexture)preview.EndPreview();
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = rendered;
                    // Preview targets follow the editor's display scaling (HiDPI).
                    texture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                    texture.Apply();
                }
                finally { RenderTexture.active = previous; }
                var path = Output + "/" + id + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                preview.Cleanup();
                foreach (var mesh in baked) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
