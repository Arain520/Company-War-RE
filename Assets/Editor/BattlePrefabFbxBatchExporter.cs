using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.EditorTools
{
    /// <summary>
    /// Exports gameplay PF_Uxx / PF_Exx prefabs that are not already backed by FBX.
    /// Content prefabs take priority over CowLegacy prefabs with the same unit id.
    /// </summary>
    public static class BattlePrefabFbxBatchExporter
    {
        private const string MenuPath = "Company War/Art/Export non-FBX battle prefabs";
        private const string OutputFolder = "Exports/BattleModels_FBX";
        private const string AutoExportRequest = "Migration/ExportBattlePrefabsToFbx.request";

        private static readonly Regex BattlePrefabName =
            new Regex("^PF_[UE]\\d{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] SearchRoots =
        {
            "Assets/CompanyWarRE/Content/Prefabs",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Units",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Enemies",
            "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Buildings"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedExport()
        {
            if (File.Exists(Path.GetFullPath(AutoExportRequest)))
            {
                EditorApplication.delayCall += TryRunRequestedExport;
            }
        }

        private static void TryRunRequestedExport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRunRequestedExport;
                return;
            }

            var requestPath = Path.GetFullPath(AutoExportRequest);
            var runningPath = requestPath + ".running";
            try
            {
                File.Move(requestPath, runningPath);
            }
            catch (IOException)
            {
                return;
            }

            try
            {
                ExportAll();
                File.Delete(runningPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (File.Exists(runningPath) && !File.Exists(requestPath))
                {
                    File.Move(runningPath, requestPath);
                }
            }
        }

        [MenuItem(MenuPath)]
        public static void ExportFromMenu()
        {
            ExportAll();
            EditorUtility.RevealInFinder(Path.GetFullPath(OutputFolder));
        }

        // Entry point for: Unity.exe -batchmode -executeMethod
        public static void ExportFromCommandLine()
        {
            try
            {
                ExportAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void ExportAll()
        {
            var outputRoot = Path.GetFullPath(OutputFolder);
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(Path.Combine(outputRoot, "Units"));
            Directory.CreateDirectory(Path.Combine(outputRoot, "Enemies"));

            var prefabs = FindPreferredBattlePrefabs();
            var report = new StringBuilder("Id,Result,Source,Output\n");
            var exported = 0;
            var skipped = 0;

            foreach (var pair in prefabs.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                var id = pair.Key;
                var prefabPath = pair.Value;

                if (AssetDatabase.GetDependencies(prefabPath, true)
                    .Any(path => string.Equals(Path.GetExtension(path), ".fbx", StringComparison.OrdinalIgnoreCase)))
                {
                    report.AppendLine(Csv(id, "Skipped: already uses FBX", prefabPath, string.Empty));
                    skipped++;
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    report.AppendLine(Csv(id, "Failed: prefab could not be loaded", prefabPath, string.Empty));
                    continue;
                }

                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    report.AppendLine(Csv(id, "Failed: prefab could not be instantiated", prefabPath, string.Empty));
                    continue;
                }

                try
                {
                    StripNonModelComponents(instance);
                    if (!ContainsMesh(instance))
                    {
                        report.AppendLine(Csv(id, "Failed: no mesh found", prefabPath, string.Empty));
                        continue;
                    }

                    var category = id.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "Units" : "Enemies";
                    var outputPath = Path.Combine(outputRoot, category, id.ToUpperInvariant() + ".fbx");
                    var exportedPath = ExportBinaryObject(outputPath, instance);
                    if (string.IsNullOrWhiteSpace(exportedPath) || !File.Exists(outputPath))
                    {
                        report.AppendLine(Csv(id, "Failed: FBX exporter returned no file", prefabPath, outputPath));
                        continue;
                    }

                    report.AppendLine(Csv(id, "Exported", prefabPath, outputPath));
                    exported++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            var reportPath = Path.Combine(outputRoot, "export-report.csv");
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(true));
            Debug.Log($"Battle FBX export complete: {exported} exported, {skipped} already-FBX prefabs skipped. Output: {outputRoot}");
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, string> FindPreferredBattlePrefabs()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in SearchRoots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                {
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!BattlePrefabName.IsMatch(name))
                    {
                        continue;
                    }

                    var id = name.Substring(3).ToUpperInvariant();
                    if (!result.ContainsKey(id))
                    {
                        result.Add(id, path);
                    }
                }
            }

            return result;
        }

        private static bool ContainsMesh(GameObject root)
        {
            return root.GetComponentsInChildren<MeshFilter>(true).Any(filter => filter.sharedMesh != null) ||
                   root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.sharedMesh != null);
        }

        private static void StripNonModelComponents(GameObject root)
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                UnityEngine.Object.DestroyImmediate(camera);
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                UnityEngine.Object.DestroyImmediate(light);
            }

            foreach (var audioSource in root.GetComponentsInChildren<AudioSource>(true))
            {
                UnityEngine.Object.DestroyImmediate(audioSource);
            }
        }

        private static string Csv(params string[] values)
        {
            return string.Join(",", values.Select(value => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));
        }

        private static string ExportBinaryObject(string outputPath, GameObject instance)
        {
            // FBX Exporter 4.2 exposes only its ASCII defaults through the public convenience API.
            // Invoke the package's internal options overload so Blender receives Binary FBX files.
            var exporterAssembly = typeof(ModelExporter).Assembly;
            var settingsType = exporterAssembly.GetType(
                "UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize", true);
            var formatType = exporterAssembly.GetType(
                "UnityEditor.Formats.Fbx.Exporter.ExportSettings+ExportFormat", true);
            var settings = Activator.CreateInstance(settingsType, true);
            var binary = Enum.Parse(formatType, "Binary");
            settingsType.GetMethod("SetExportFormat", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(settings, new[] { binary });

            var exportMethod = typeof(ModelExporter)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "ExportObject" && method.GetParameters().Length == 3);
            return exportMethod.Invoke(null, new[] { outputPath, instance, settings }) as string;
        }
    }
}
