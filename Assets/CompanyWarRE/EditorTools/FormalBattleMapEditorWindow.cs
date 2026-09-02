using System.Collections.Generic;
using System.IO;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    [CustomEditor(typeof(FormalBattleMapView))]
    public sealed class FormalBattleMapViewEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var map = (FormalBattleMapView)target;
            if (!map.HasUniformAnchorScale())
            {
                EditorGUILayout.HelpBox(
                    "BattleBoardAnchor 禁止非等比缩放。正式战斗不会接受该地图状态。",
                    MessageType.Error);
                if (GUILayout.Button("修复锚点为等比缩放"))
                {
                    var anchor = map.BattleBoardAnchor;
                    Undo.RecordObject(anchor, "修复战斗棋盘锚点缩放");
                    map.NormalizeAnchorScale();
                    EditorUtility.SetDirty(anchor);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("棋盘锚点缩放有效。", MessageType.Info);
            }
        }
    }

    public static class FormalBattleMapValidation
    {
        public static IReadOnlyList<string> Validate(FormalBattleMapView map)
        {
            var issues = new List<string>();
            if (map == null)
            {
                issues.Add("未选择 FormalBattleMapView。");
                return issues;
            }

            if (map.EnvironmentRoot == null)
            {
                issues.Add("EnvironmentRoot 未设置。");
            }

            if (map.BattleBoardAnchor == map.transform)
            {
                issues.Add("BattleBoardAnchor 未设置，将错误地回退到地图根节点。");
            }

            if (!map.HasUniformAnchorScale())
            {
                issues.Add("BattleBoardAnchor 存在非等比缩放。");
            }

            if (map.EnvironmentRoot != null)
            {
                var cowEnvironment = map.EnvironmentRoot
                    .GetComponentInChildren<CowIndustrialEnvironmentView>(true);
                if (cowEnvironment == null)
                {
                    issues.Add("尚未安装 CowIndustrialEnvironmentView。");
                }
                else
                {
                    if (!cowEnvironment.HasUrpMaterialTemplate)
                    {
                        issues.Add("Cow 工业环境缺少 URP 材质模板。");
                    }

                    if (cowEnvironment.ConfiguredOptionalModelCount < 7)
                    {
                        issues.Add(
                            $"Cow 可选环境模型仅配置 {cowEnvironment.ConfiguredOptionalModelCount}/7 个。");
                    }

                    if (cowEnvironment.transform.Find("BakedCowEnvironment") == null)
                    {
                        issues.Add("Cow 工业环境尚未烘焙进地图 Prefab。");
                    }
                }

                var colliders = map.EnvironmentRoot.GetComponentsInChildren<Collider>(true);
                if (colliders.Length > 0)
                {
                    issues.Add($"环境中仍有 {colliders.Length} 个 Collider；运行时会禁用，建议从地图 Prefab 删除。");
                }
            }

            return issues;
        }
    }

    public sealed class FormalBattleMapEditorWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/CompanyWarRE/Content/Maps";
        private const string DefaultGroundMaterialPath =
            "Assets/CompanyWarRE/Resources/CompanyWarRE/Maps/MAT_BaseFormalMapGround.mat";
        private const string CowEnvironmentModelFolder =
            "Assets/CompanyWarRE/Content/Environment/Cow/Models";

        [SerializeField] private string mapName = "BaseFormalBattleMap";
        [SerializeField] private int previewColumns = 18;
        [SerializeField] private int previewRows = 30;
        [SerializeField] private FormalBattleMapView selectedMap;
        private Vector2 _scroll;

        [MenuItem("Company War-RE/地图编辑器")]
        public static void Open()
        {
            GetWindow<FormalBattleMapEditorWindow>("正式地图编辑器");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("创建可复用环境地图", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "工具只创建和编辑环境、灯光及棋盘锚点。战斗规则仍由关卡配置负责。",
                MessageType.Info);
            mapName = EditorGUILayout.TextField("地图名称", mapName);
            previewColumns = Mathf.Max(1, EditorGUILayout.IntField("预览列数", previewColumns));
            previewRows = Mathf.Max(1, EditorGUILayout.IntField("预览行数", previewRows));
            if (GUILayout.Button("创建地图 Prefab"))
            {
                selectedMap = CreateMapPrefab(mapName, previewColumns, previewRows);
            }

            EditorGUILayout.Space(14f);
            EditorGUILayout.LabelField("调整已有地图", EditorStyles.boldLabel);
            selectedMap = (FormalBattleMapView)EditorGUILayout.ObjectField(
                "地图 Prefab",
                selectedMap,
                typeof(FormalBattleMapView),
                true);
            if (selectedMap != null)
            {
                if (GUILayout.Button("应用 Gizmos 预览尺寸"))
                {
                    Undo.RecordObject(selectedMap, "更新战斗地图预览尺寸");
                    selectedMap.SetPreviewSize(previewColumns, previewRows);
                    EditorUtility.SetDirty(selectedMap);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("打开 Prefab 编辑环境"))
                {
                    AssetDatabase.OpenAsset(selectedMap.gameObject);
                }

                if (GUILayout.Button("验证地图"))
                {
                    ReportValidation(selectedMap);
                }

                if (GUILayout.Button("安装/更新 Cow 工业环境"))
                {
                    selectedMap = InstallCowEnvironmentOnPrefab(selectedMap);
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "BattleBoardAnchor 可平移和旋转；缩放必须等比。环境对象会置于 Ignore Raycast，运行时所有环境 Collider 都会禁用。",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        public static FormalBattleMapView CreateMapPrefab(string requestedName, int columns, int rows)
        {
            EnsureFolder(DefaultFolder);
            var safeName = SanitizeName(requestedName);
            var root = new GameObject("PF_" + safeName);
            try
            {
                var map = root.AddComponent<FormalBattleMapView>();
                var environment = new GameObject("EnvironmentRoot").transform;
                environment.SetParent(root.transform, false);
                SetLayerRecursively(environment, FormalBattleMapView.EnvironmentLayer);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "BaseGround";
                ground.transform.SetParent(environment, false);
                ground.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                ground.transform.localScale = new Vector3(60f, 0.4f, 60f);
                ground.layer = FormalBattleMapView.EnvironmentLayer;
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                var groundRenderer = ground.GetComponent<Renderer>();
                var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultGroundMaterialPath);
                if (groundRenderer != null && groundMaterial != null)
                {
                    groundRenderer.sharedMaterial = groundMaterial;
                }
                else
                {
                    Debug.LogWarning($"未找到地图默认 URP 材质：{DefaultGroundMaterialPath}");
                }

                var lighting = new GameObject("Lighting").transform;
                lighting.SetParent(environment, false);
                var lightObject = new GameObject("Directional Light");
                lightObject.transform.SetParent(lighting, false);
                lightObject.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;

                CreateCowIndustrialEnvironment(environment);

                var anchor = new GameObject("BattleBoardAnchor").transform;
                anchor.SetParent(root.transform, false);
                map.Configure(safeName, environment, anchor, columns, rows);
                CowIndustrialEnvironmentPrefabBaker.BakeInMemory(map, columns, rows);

                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{DefaultFolder}/PF_{safeName}.prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                return prefab.GetComponent<FormalBattleMapView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ReportValidation(FormalBattleMapView map)
        {
            var issues = FormalBattleMapValidation.Validate(map);
            if (issues.Count == 0)
            {
                Debug.Log($"地图 '{map.MapId}' 验证通过。", map);
                return;
            }

            Debug.LogWarning(string.Join("\n", issues), map);
        }

        private static FormalBattleMapView InstallCowEnvironmentOnPrefab(FormalBattleMapView map)
        {
            var path = AssetDatabase.GetAssetPath(map);
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab"))
            {
                Debug.LogWarning("请选择 Project 窗口中的地图 Prefab 资产。", map);
                return map;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var prefabMap = root.GetComponent<FormalBattleMapView>();
                if (prefabMap == null || prefabMap.EnvironmentRoot == null)
                {
                    Debug.LogError($"地图 Prefab 缺少 FormalBattleMapView/EnvironmentRoot：{path}");
                    return map;
                }

                var existing = prefabMap.EnvironmentRoot.Find("CowIndustrialEnvironment");
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                CreateCowIndustrialEnvironment(prefabMap.EnvironmentRoot);
                SetLayerRecursively(prefabMap.EnvironmentRoot, FormalBattleMapView.EnvironmentLayer);
                CowIndustrialEnvironmentPrefabBaker.BakeInMemory(
                    prefabMap,
                    prefabMap.PreviewColumns,
                    prefabMap.PreviewRows);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            var updated = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = updated;
            EditorGUIUtility.PingObject(updated);
            return updated != null ? updated.GetComponent<FormalBattleMapView>() : map;
        }

        private static CowIndustrialEnvironmentView CreateCowIndustrialEnvironment(Transform parent)
        {
            var environmentObject = new GameObject("CowIndustrialEnvironment");
            environmentObject.transform.SetParent(parent, false);
            environmentObject.layer = FormalBattleMapView.EnvironmentLayer;
            var environment = environmentObject.AddComponent<CowIndustrialEnvironmentView>();
            environment.Configure(
                AssetDatabase.LoadAssetAtPath<Material>(DefaultGroundMaterialPath),
                LoadCowEnvironmentModel("栏杆左到右123.fbx"),
                LoadCowEnvironmentModel("信号基站（拆件.fbx"),
                LoadCowEnvironmentModel("服务器终端.fbx"),
                LoadCowEnvironmentModel("储装罐.fbx"),
                LoadCowEnvironmentModel("货运单元1.fbx"),
                LoadCowEnvironmentModel("货运单元2.fbx"),
                LoadCowEnvironmentModel("棋盘part (1).fbx"));
            return environment;
        }

        private static GameObject LoadCowEnvironmentModel(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{CowEnvironmentModelFolder}/{fileName}");
        }

        private static string SanitizeName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value) ? "FormalBattleMap" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
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
    }
}
