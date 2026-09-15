using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    /// <summary>
    /// Replaces only the visual hierarchy of battle prefabs with same-id models from NewFbx.
    /// Prefab roots, GUIDs and colliders are preserved; nested FBX renderers keep their source materials.
    /// </summary>
    public static class BattleModelReplacementTool
    {
        private const string MenuPath = "Company War/Art/Apply same-name models from NewFbx";
        private const string NewModelFolder = "Assets/CompanyWarRE/Resources/NewFbx";
        private const string ReportPath = "Migration/NewFbxReplacementReport.csv";

        private static readonly Regex BattlePrefabName =
            new Regex("^PF_[UE]\\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] PrefabSearchRoots =
        {
            "Assets/CompanyWarRE/Content/Prefabs",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Units",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Enemies",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Buildings"
        };

        private static readonly HashSet<string> AlliedBuildingIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "U08", "U09", "U10", "U11", "U21"
            };

        [MenuItem(MenuPath)]
        public static void ApplyFromMenu()
        {
            ApplyAll();
            EditorUtility.DisplayDialog("Battle models", "NewFbx replacement complete.\nSee " + ReportPath, "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try
            {
                ApplyAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void ApplyAll()
        {
            var prefabs = FindPreferredBattlePrefabs();
            var models = FindNewModels();
            var report = new StringBuilder("Id,Result,Prefab,Model\n");
            var replaced = 0;
            var skipped = 0;

            foreach (var pair in prefabs.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                var id = pair.Key;
                var prefabPath = pair.Value;
                if (!models.TryGetValue(id, out var modelPath))
                {
                    if (AlliedBuildingIds.Contains(id))
                    {
                        EnsureExistingBuildingVisualHeight(prefabPath);
                        report.AppendLine(Csv(id, "Height adjusted; model unchanged because NewFbx is missing", prefabPath, string.Empty));
                        skipped++;
                        continue;
                    }

                    report.AppendLine(Csv(id, "Skipped: same-name NewFbx model missing", prefabPath, string.Empty));
                    skipped++;
                    continue;
                }

                ReplaceVisualHierarchy(id, prefabPath, modelPath);
                report.AppendLine(Csv(id, "Replaced", prefabPath, modelPath));
                replaced++;
            }

            var absoluteReportPath = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReportPath) ?? string.Empty);
            File.WriteAllText(absoluteReportPath, report.ToString(), new UTF8Encoding(true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"NewFbx battle replacement complete: {replaced} replaced, {skipped} missing models skipped.");
        }

        private static void ReplaceVisualHierarchy(string id, string prefabPath, string modelPath)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException($"Could not load NewFbx model for {id}: {modelPath}");
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                for (var index = root.transform.childCount - 1; index >= 0; index--)
                {
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(index).gameObject);
                }

                RemoveRootVisualComponents(root);

                var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
                if (modelInstance == null)
                {
                    throw new InvalidOperationException($"Could not instantiate NewFbx model for {id}: {modelPath}");
                }

                modelInstance.name = "Model_" + id;
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = AlliedBuildingIds.Contains(id)
                    ? new Vector3(1f, 2f / 3f, 1f)
                    : Vector3.one;
                StripNonModelComponents(modelInstance);

                var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0 || !ContainsMesh(modelInstance))
                {
                    throw new InvalidOperationException($"NewFbx model contains no renderable mesh for {id}: {modelPath}");
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException($"Could not save replaced prefab for {id}: {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            if (!dependencies.Contains(modelPath, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Saved prefab {id} does not reference expected model: {modelPath}");
            }
        }

        private static void EnsureExistingBuildingVisualHeight(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var wrapper = root.transform.Find("VisualHeightScale");
                if (wrapper == null)
                {
                    var existingChildren = new List<Transform>();
                    for (var index = 0; index < root.transform.childCount; index++)
                        existingChildren.Add(root.transform.GetChild(index));

                    var wrapperObject = new GameObject("VisualHeightScale");
                    wrapper = wrapperObject.transform;
                    wrapper.SetParent(root.transform, false);
                    foreach (var child in existingChildren) child.SetParent(wrapper, false);
                }

                wrapper.localPosition = Vector3.zero;
                wrapper.localRotation = Quaternion.identity;
                wrapper.localScale = new Vector3(1f, 2f / 3f, 1f);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException("Could not save building height adjustment: " + prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<string, string> FindPreferredBattlePrefabs()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in PrefabSearchRoots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!BattlePrefabName.IsMatch(name)) continue;
                    var id = name.Substring(3).ToUpperInvariant();
                    if (!result.ContainsKey(id)) result.Add(id, path);
                }
            }

            return result;
        }

        private static Dictionary<string, string> FindNewModels()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(NewModelFolder)) return result;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { NewModelFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var id = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
                if (Regex.IsMatch(id, "^[UE]\\d{2}$")) result[id] = path;
            }

            return result;
        }

        private static void RemoveRootVisualComponents(GameObject root)
        {
            foreach (var component in root.GetComponents<Component>())
            {
                if (component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer ||
                    component is Camera || component is Light || component is AudioSource)
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private static void StripNonModelComponents(GameObject root)
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                UnityEngine.Object.DestroyImmediate(camera);
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light);
            foreach (var audioSource in root.GetComponentsInChildren<AudioSource>(true))
                UnityEngine.Object.DestroyImmediate(audioSource);
        }

        private static bool ContainsMesh(GameObject root)
        {
            return root.GetComponentsInChildren<MeshFilter>(true).Any(filter => filter.sharedMesh != null) ||
                   root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.sharedMesh != null);
        }

        private static string Csv(params string[] values)
        {
            return string.Join(",", values.Select(value => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));
        }
    }
}
