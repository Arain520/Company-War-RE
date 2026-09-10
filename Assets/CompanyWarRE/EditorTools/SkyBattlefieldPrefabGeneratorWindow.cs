using System;
using System.IO;
using System.Collections.Generic;
using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CompanyWarRE.EditorTools
{
    public sealed class SkyBattlefieldPrefabGeneratorWindow : EditorWindow
    {
        public const string OutputFolder = "Assets/CompanyWarRE/Content/Maps/SkyBattlefield";
        public const string DefaultPrefabPath = OutputFolder + "/PF_SkyBattlefield.prefab";
        private const string ArchitectureFolder = "Assets/CompanyWarRE/Content/Environment/SkyArchitecture/";
        private const string PillarPath = "Assets/CompanyWarRE/Content/Environment/SkyPillar/SM_SkyPillar_A.fbx";
        private const string CloudFolder = "Assets/CompanyWarRE/Content/Environment/CloudAbyss/Textures/";

        [Serializable]
        public sealed class Settings
        {
            public string name = "SkyBattlefield";
            public int columns = 18;
            public int rows = 30;
            public int seed = 1977;
            public float scale = 4f;
            public float pillarWidth = 3f;
            public float gap = 0.5f;
            public float minimumHeight = 29f;
            public float maximumHeight = 35f;
            public int distantTowers = 36;
            public float cloudHeight = 40f;

            public float Width => Extent(columns);
            public float Length => Extent(rows);
            public float DeckY => (minimumHeight + maximumHeight) * 0.5f * scale;
            public float ModelScale => pillarWidth * scale / 4f;
            private float Extent(int cells)
            {
                var blocks = Mathf.CeilToInt(cells / 3f);
                return (blocks + Mathf.Max(0, blocks - 1) * gap) * pillarWidth * scale;
            }

            public void Validate()
            {
                columns = Mathf.Clamp(columns, 1, 90);
                rows = Mathf.Clamp(rows, 1, 90);
                scale = Mathf.Clamp(scale, 0.1f, 10f);
                pillarWidth = Mathf.Clamp(pillarWidth, 1f, 10f);
                gap = Mathf.Clamp(gap, 0.15f, 2f);
                minimumHeight = Mathf.Max(4f, minimumHeight);
                maximumHeight = Mathf.Max(minimumHeight, maximumHeight);
                distantTowers = Mathf.Clamp(distantTowers, 8, 80);
                cloudHeight = Mathf.Min(cloudHeight, minimumHeight * scale - pillarWidth * scale * 2f);
            }
        }

        [SerializeField] private Settings settings = new Settings();
        [SerializeField] private FormalBattleMapView generatedMap;
        private Vector2 scroll;

        [MenuItem("Company War-RE/云上战场 Prefab 生成器")]
        public static void Open() => GetWindow<SkyBattlefieldPrefabGeneratorWindow>("云上战场生成器");

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("云海 · 巨柱 · 空中建筑群", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("环境与灯光保存进地图 Prefab；主战场按关卡尺寸动态生成，每个 3×3 控制区对应一根真实随机高度的柱子。尺寸用于外围布局，不修改关卡规则。", MessageType.Info);
            settings.name = EditorGUILayout.TextField("资源名称", settings.name);
            settings.columns = EditorGUILayout.IntField("逻辑列数", settings.columns);
            settings.rows = EditorGUILayout.IntField("逻辑行数", settings.rows);
            settings.seed = EditorGUILayout.IntField("随机种子", settings.seed);
            settings.scale = EditorGUILayout.FloatField("整体视觉倍率", settings.scale);
            settings.pillarWidth = EditorGUILayout.FloatField("柱宽（倍率前）", settings.pillarWidth);
            settings.gap = EditorGUILayout.Slider("柱间隙 / 柱宽", settings.gap, 0.15f, 2f);
            settings.minimumHeight = EditorGUILayout.FloatField("最低柱高（倍率前）", settings.minimumHeight);
            settings.maximumHeight = EditorGUILayout.FloatField("最高柱高（倍率前）", settings.maximumHeight);
            settings.distantTowers = EditorGUILayout.IntSlider("远景塔数量", settings.distantTowers, 8, 80);
            settings.cloudHeight = EditorGUILayout.FloatField("主云层高度（地图坐标）", settings.cloudHeight);
            settings.Validate();
            EditorGUILayout.LabelField("环境预留范围", $"{settings.Width:0.#} × {settings.Length:0.#}，柱顶 {settings.minimumHeight * settings.scale:0.#}–{settings.maximumHeight * settings.scale:0.#}");
            if (GUILayout.Button("生成新地图 Prefab", GUILayout.Height(32)))
            {
                try
                {
                    generatedMap = Generate(settings);
                    Selection.activeObject = generatedMap;
                    EditorGUIUtility.PingObject(generatedMap);
                }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            generatedMap = (FormalBattleMapView)EditorGUILayout.ObjectField("生成的地图", generatedMap, typeof(FormalBattleMapView), false);
            using (new EditorGUI.DisabledScope(generatedMap == null))
            {
                if (GUILayout.Button("打开 Prefab 编辑外围环境")) AssetDatabase.OpenAsset(generatedMap.gameObject);
                if (GUILayout.Button("创建场景预览（含临时动态柱阵）")) CreatePreview(generatedMap);
                if (GUILayout.Button("应用到当前场景战斗控制器")) ApplyToCurrentScene(generatedMap);
            }
            EditorGUILayout.HelpBox("每次生成使用独立资源路径，已有地图可继续手工编辑。预览对象标记为 EditorOnly，进入战斗会自动关闭。外围布局按所填尺寸固定；不同尺寸关卡建议生成对应地图。", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        public static FormalBattleMapView Generate(Settings options)
        {
            options.Validate();
            // Validate all source assets before creating any output.
            var pillar = LoadModel(PillarPath);
            foreach (var name in new[] { "Bridge_8m", "Landing_4m", "CommandRoom", "ServiceRoom",
                "Antenna", "Machinery", "Tower_Slim24m", "Tower_Monolith36m", "Tower_Split28m", "Ring_Quarter_R7m", "Catwalk_8m" })
                LoadModel(ArchitectureFolder + "SM_Sky" + name + ".fbx");
            var cloudTexture = LoadRequired<Texture2D>(CloudFolder + "T_CloudSea_Art.png");
            var flowTexture = LoadRequired<Texture2D>(CloudFolder + "T_CloudFlow_Art.png");
            var cloudShader = Shader.Find("CompanyWarRE/CloudSeaURP");
            if (cloudShader == null) throw new InvalidOperationException("缺少 CloudSeaURP Shader。");
            EnsureFolder(OutputFolder);
            var safeName = string.IsNullOrWhiteSpace(options.name) ? "SkyBattlefield" : options.name.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
            var path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/PF_" + safeName + ".prefab");
            var assetFolder = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(path) + "_Assets";
            EnsureFolder(assetFolder);
            var materials = CreateArchitectureMaterials(assetFolder);
            pillar = CreatePillarVisual(pillar, materials, assetFolder);
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            try
            {
                var map = root.AddComponent<FormalBattleMapView>();
                var environment = Group(root.transform, "EnvironmentRoot");
                var anchor = Group(root.transform, "BattleBoardAnchor");
                map.Configure(safeName, environment, anchor, options.columns, options.rows);
                root.AddComponent<SkyBattlefieldSettings>().Configure(pillar, options.pillarWidth,
                    options.gap, options.minimumHeight, options.maximumHeight, options.seed, options.scale);
                var random = new System.Random(options.seed);
                BuildPerimeter(environment, pillar, options);
                BuildDistantTowers(environment, options, random);
                ApplyArchitectureMaterials(environment, materials);
                var clouds = BuildClouds(environment, options, cloudShader, cloudTexture, flowTexture, assetFolder);
                BuildLighting(environment, options, assetFolder);
                ConfigureAtmosphere(environment, clouds, options);
                foreach (var child in environment.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = FormalBattleMapView.EnvironmentLayer;
                foreach (var collider in environment.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("保存地图 Prefab 失败：" + path);
                AssetDatabase.SaveAssets();
                Debug.Log("云上战场已生成：" + path);
                return prefab.GetComponent<FormalBattleMapView>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildPerimeter(Transform environment, GameObject pillar, Settings s)
        {
            var group = Group(environment, "Near_EntryBridge_And_RearFacilities");
            var unit = s.ModelScale;
            var halfWidth = s.Width * 0.5f;
            var frontZ = -s.Length * 0.5f - 10f * unit;
            var rearZ = s.Length * 0.5f + 8f * unit;
            // Exact module join: 4m landings and 8m spans use 12m centres.
            var spans = Mathf.Max(2, Mathf.CeilToInt((s.Width + 8f * unit) / (12f * unit)));
            for (var side = 0; side < 2; side++)
            {
                var z = side == 0 ? frontZ : rearZ;
                var y = s.DeckY + (side == 0 ? -6f : 5f) * unit;
                for (var index = 0; index <= spans; index++)
                {
                    var x = (index - spans * 0.5f) * 12f * unit;
                    var tower = Instance(pillar, group, "PerimeterPillar", new Vector3(x, 0f, z), Vector3.one);
                    tower.localScale = new Vector3(unit, y / 16f, unit);
                    Module("Landing_4m", group, new Vector3(x, y + 0.65f * unit, z), unit);
                    if (index < spans)
                        Module("Bridge_8m", group, new Vector3(x + 6f * unit, y + 0.65f * unit, z), unit);
                    if (side == 1 && (index == 0 || index == spans))
                        Module(index == 0 ? "Antenna" : "CommandRoom", group, new Vector3(x, y + 0.65f * unit, z), unit * 0.85f);
                }
            }
            // Roof props live on separate fixed support pillars, outside every playable cell.
            for (var side = -1; side <= 1; side += 2)
            for (var index = 0; index < 3; index++)
            {
                var x = side * (halfWidth + 7f * unit);
                var z = (index - 1) * s.Length * 0.38f;
                var y = s.DeckY + (index - 1) * 2f * unit;
                Instance(pillar, group, "FacilityPillar", new Vector3(x, 0f, z), new Vector3(unit, y / 16f, unit));
                Module(index == 0 ? "ServiceRoom" : index == 1 ? "Machinery" : "Antenna",
                    group, new Vector3(x, y, z), unit * 0.85f);
            }
        }

        private static Dictionary<string, Material> CreateArchitectureMaterials(string folder)
        {
            var shader = Shader.Find("CompanyWarRE/SkyArchitectureLitURP");
            if (shader == null) throw new InvalidOperationException("缺少 SkyArchitectureLitURP Shader。");
            var materials = new Dictionary<string, Material>();
            foreach (var name in new[] { "Graphite", "Armor", "Deck", "Steel", "Gold", "Recess", "Light" })
            {
                var source = LoadRequired<Material>("Assets/CompanyWarRE/Content/Environment/SkyPillar/Materials/Pillar_" + name + ".mat");
                var material = new Material(source) { name = source.name, shader = shader, enableInstancing = true };
                if (name == "Deck") material.SetColor("_BaseColor", new Color(0.72f, 0.74f, 0.76f));
                if (name == "Armor") material.SetColor("_BaseColor", new Color(0.38f, 0.43f, 0.50f));
                if (name == "Graphite") material.SetColor("_BaseColor", new Color(0.24f, 0.29f, 0.36f));
                material.SetFloat("_Metallic", name == "Gold" ? 0.55f : 0.18f);
                material.SetFloat("_Smoothness", 0.3f);
                AssetDatabase.CreateAsset(material, folder + "/MAT_Sky_" + name + ".mat");
                materials.Add(source.name, material);
            }
            return materials;
        }

        private static void ApplyArchitectureMaterials(Transform root, Dictionary<string, Material> materials)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (var index = 0; index < slots.Length; index++)
                    if (slots[index] != null && materials.TryGetValue(slots[index].name, out var replacement))
                        slots[index] = replacement;
                renderer.sharedMaterials = slots;
            }
        }

        private static GameObject CreatePillarVisual(GameObject source, Dictionary<string, Material> materials, string folder)
        {
            var root = new GameObject("PF_SkyPillarVisual");
            try
            {
                PrefabUtility.InstantiatePrefab(source, root.transform);
                ApplyArchitectureMaterials(root.transform, materials);
                return PrefabUtility.SaveAsPrefabAsset(root, folder + "/PF_SkyPillarVisual.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildDistantTowers(Transform environment, Settings s, System.Random random)
        {
            var mid = Group(environment, "Mid_SatelliteTowers");
            var far = Group(environment, "Far_MonolithSkyline");
            var unit = s.ModelScale;
            for (var index = 0; index < s.distantTowers; index++)
            {
                var isFar = index >= s.distantTowers / 2;
                // Leave the front viewing wedge open, with a few lower foreground silhouettes.
                var angle = index * 2.399963f + Range(random, -0.12f, 0.12f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var radius = isFar ? Range(random, 62f, 105f) : Range(random, 26f, 49f);
                var x = direction.x * (s.Width * 0.5f + radius * unit);
                var z = direction.y * (s.Length * 0.5f + radius * unit);
                var foreground = direction.y < -0.45f;
                var model = index % 3 == 0 ? "Tower_Monolith36m" : index % 3 == 1 ? "Tower_Slim24m" : "Tower_Split28m";
                var nominalHeight = index % 3 == 0 ? 36f : index % 3 == 1 ? 24f : 28f;
                var widthScale = unit * Range(random, isFar ? 1.5f : 0.8f, isFar ? 2.7f : 1.3f);
                var bottom = s.cloudHeight - 38f * unit;
                var top = foreground ? s.DeckY - Range(random, 10f, 25f) * unit
                    : s.DeckY + Range(random, isFar ? 16f : -6f, isFar ? 70f : 24f) * unit;
                var parent = isFar ? far : mid;
                var tower = Module(model, parent, new Vector3(x, bottom, z), 1f);
                tower.localScale = new Vector3(widthScale, (top - bottom) / nominalHeight, widthScale);
                tower.localRotation = Quaternion.Euler(0f, (index % 4) * 90f, 0f);
                if (index % 6 == 0 && !foreground)
                {
                    for (var quarter = 0; quarter < 4; quarter++)
                    {
                        var ring = Module("Ring_Quarter_R7m", parent,
                            new Vector3(x, Mathf.Lerp(s.cloudHeight, top, 0.72f), z), widthScale);
                        ring.localRotation = Quaternion.Euler(0f, quarter * 90f, 0f);
                    }
                }
            }
        }

        private static Transform BuildClouds(Transform parent, Settings s, Shader shader,
            Texture2D texture, Texture2D flow, string folder)
        {
            var clouds = Group(parent, "BakedCloudSea");
            var diameter = Mathf.Max(1600f, Mathf.Max(s.Width, s.Length) * 10f);
            var layers = Group(clouds, "CloudLayers");
            for (var index = 0; index < 3; index++)
            {
                var material = CloudMaterial(shader, texture, flow, index);
                AssetDatabase.CreateAsset(material, folder + $"/MAT_CloudLayer_{index}.mat");
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "CloudLayer_" + index;
                plane.transform.SetParent(layers, false);
                plane.transform.localPosition = new Vector3(0f, s.cloudHeight - index * 11f * s.ModelScale, 0f);
                plane.transform.localScale = new Vector3(diameter / 10f, 1f, diameter / 10f);
                var renderer = plane.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var billows = Group(clouds, "CloudBanks");
            var bankMaterial = CloudMaterial(shader, texture, flow, 3);
            AssetDatabase.CreateAsset(bankMaterial, folder + "/MAT_CloudBanks.mat");
            var random = new System.Random(s.seed ^ 9031);
            for (var index = 0; index < 42; index++)
            {
                var angle = index * 2.399963f;
                var distance = Range(random, 0.6f, 3.8f);
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"CloudBank_{index:00}";
                card.transform.SetParent(billows, false);
                card.transform.localPosition = new Vector3(Mathf.Cos(angle) * s.Width * distance,
                    s.cloudHeight + Range(random, -10f, 4f) * s.ModelScale,
                    Mathf.Sin(angle) * s.Length * distance);
                card.transform.localRotation = Quaternion.Euler(50f, -32f + Range(random, -25f, 25f), 0f);
                card.transform.localScale = new Vector3(Range(random, 28f, 65f) * s.ModelScale,
                    Range(random, 12f, 25f) * s.ModelScale, 1f);
                var renderer = card.GetComponent<Renderer>();
                renderer.sharedMaterial = bankMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return clouds;
        }

        private static Material CloudMaterial(Shader shader, Texture2D texture, Texture2D flow, int index)
        {
            var material = new Material(shader) { name = "MAT_SkyCloud_" + index };
            material.SetTexture("_NoiseTex", texture);
            material.SetTexture("_FlowMap", flow);
            material.SetColor("_LightColor", new Color(1f, 0.94f, 0.86f));
            material.SetColor("_ShadowColor", new Color(0.52f, 0.62f, 0.74f));
            material.SetFloat("_Tiling", index == 3 ? 1f : 5f - index * 1.5f);
            material.SetVector("_UvOffset", new Vector4(index * 0.217f, index * 0.351f, 0f, 0f));
            material.SetFloat("_Opacity", index == 3 ? 0.40f : 0.55f + index * 0.2f);
            material.SetFloat("_Density", index == 3 ? 0.52f : 0.56f + index * 0.09f);
            material.SetFloat("_Softness", 0.2f);
            material.SetFloat("_EdgeFade", index == 3 ? 0.35f : 0.12f);
            material.SetFloat("_DepthFadeDistance", 18f);
            material.SetFloat("_FlowStrength", 0.04f);
            material.SetFloat("_FlowSpeed", 0.05f);
            return material;
        }

        private static void BuildLighting(Transform environment, Settings s, string folder)
        {
            var lighting = Group(environment, "Lighting");
            var sun = Group(lighting, "WarmSun").gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.localRotation = Quaternion.Euler(38f, -55f, 0f);
            var fill = Group(lighting, "CoolSkyFill").gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.58f, 0.72f, 1f);
            fill.intensity = 0.32f;
            fill.shadows = LightShadows.None;
            fill.transform.localRotation = Quaternion.Euler(65f, 135f, 0f);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "VP_SkyBattlefield";
            AssetDatabase.CreateAsset(profile, folder + "/VP_SkyBattlefield.asset");
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.25f);
            bloom.threshold.Override(1.1f);
            var grading = profile.Add<ColorAdjustments>(true);
            grading.contrast.Override(8f);
            grading.saturation.Override(-5f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            var volume = Group(lighting, "SkyColorGrading").gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);
        }

        private static void ConfigureAtmosphere(Transform environment, Transform clouds, Settings s)
        {
            var atmosphere = environment.gameObject.AddComponent<CloudAbyssEnvironmentView>();
            var serialized = new SerializedObject(atmosphere);
            serialized.FindProperty("bakedCloudRoot").objectReferenceValue = clouds;
            serialized.FindProperty("hideLegacyEnvironmentRenderers").boolValue = false;
            serialized.FindProperty("zoomResponsiveFog").boolValue = false;
            serialized.FindProperty("heightFogTopY").floatValue = s.cloudHeight + 35f;
            serialized.FindProperty("heightFogBottomY").floatValue = s.cloudHeight - 35f;
            serialized.FindProperty("heightFogColor").colorValue = new Color(0.81f, 0.84f, 0.88f);
            serialized.FindProperty("heightFogCurve").floatValue = 0.85f;
            serialized.FindProperty("distanceFogStart").floatValue = Mathf.Max(s.Width, s.Length) * 2.2f;
            serialized.FindProperty("distanceFogEnd").floatValue = Mathf.Max(s.Width, s.Length) * 6f;
            serialized.FindProperty("distanceFogColor").colorValue = new Color(0.72f, 0.78f, 0.84f);
            serialized.FindProperty("skyColor").colorValue = new Color(0.77f, 0.83f, 0.89f);
            serialized.FindProperty("mainLightColor").colorValue = new Color(1f, 0.86f, 0.68f);
            serialized.FindProperty("mainLightIntensity").floatValue = 1.5f;
            serialized.FindProperty("mainLightEulerAngles").vector3Value = new Vector3(38f, -55f, 0f);
            serialized.FindProperty("ambientSkyColor").colorValue = new Color(0.68f, 0.77f, 0.87f);
            serialized.FindProperty("ambientEquatorColor").colorValue = new Color(0.52f, 0.60f, 0.70f);
            var layers = serialized.FindProperty("cloudLayers");
            for (var index = 0; index < layers.arraySize; index++)
                layers.GetArrayElementAtIndex(index).FindPropertyRelative("height").floatValue = s.cloudHeight - index * 11f * s.ModelScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject CreatePreview(FormalBattleMapView map)
        {
            var root = new GameObject("SkyBattlefield_EditorPreview");
            Undo.RegisterCreatedObjectUndo(root, "预览云上战场");
            root.tag = "EditorOnly";
            root.AddComponent<SkyBattlefieldPreview>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(map.gameObject, root.transform);
            var previewMap = instance.GetComponent<FormalBattleMapView>();
            var board = new GameObject("PreviewDynamicBoard").AddComponent<FormalBattleBoardView>();
            board.transform.SetParent(previewMap.BattleBoardAnchor, false);
            instance.GetComponent<SkyBattlefieldSettings>().ApplyTo(board);
            board.Prepare(map.PreviewColumns, map.PreviewRows);
            var atmosphere = previewMap.PrepareForBattle(map.PreviewColumns, map.PreviewRows, board.CoordinateMapper);
            board.PillarGenerator.ConfigureCloudAbyss(atmosphere.MinimumPillarBottomY,
                atmosphere.GetWorldHeight(previewMap.BattleBoardAnchor, atmosphere.HeightFogTopY),
                atmosphere.GetWorldHeight(previewMap.BattleBoardAnchor, atmosphere.HeightFogBottomY),
                atmosphere.HeightFogColor, atmosphere.HeightFogStrength, atmosphere.HeightFogCurve);
            // Preview geometry is ephemeral; never serialize procedural materials into a scene.
            board.gameObject.hideFlags = HideFlags.DontSave;
            Shader.SetGlobalFloat("_CompanyWarCloudOpacityScale", 1f);
            Selection.activeGameObject = root;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.LookAt(new Vector3(0f, board.CoordinateMapper.AveragePillarTopY, 0f),
                    Quaternion.Euler(48f, -32f, 0f), Mathf.Max(board.PresentationWidth, board.PresentationLength) * 0.85f);
            return root;
        }

        private static void ApplyToCurrentScene(FormalBattleMapView map)
        {
            var controller = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<BattleSliceController>() : null;
            if (controller == null)
            {
                var controllers = Object.FindObjectsOfType<BattleSliceController>();
                if (controllers.Length != 1)
                {
                    Debug.LogWarning("请选择需要应用地图的 BattleSliceController；当前场景没有唯一战斗控制器。");
                    return;
                }
                controller = controllers[0];
            }
            Undo.RecordObject(controller, "应用云上战场地图");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("battleMapPrefab").objectReferenceValue = map;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        public static void GenerateDefault()
        {
            Generate(new Settings());
        }

        private static Transform Module(string name, Transform parent, Vector3 position, float scale)
            => Instance(LoadModel(ArchitectureFolder + "SM_Sky" + name + ".fbx"), parent, name, position, Vector3.one * scale);

        private static Transform Instance(GameObject asset, Transform parent, string name, Vector3 position, Vector3 scale)
        {
            // Compose transforms on a parent; never erase the imported FBX axis conversion.
            var instance = Group(parent, name);
            PrefabUtility.InstantiatePrefab(asset, instance);
            instance.localPosition = position;
            instance.localScale = scale;
            return instance;
        }

        private static GameObject LoadModel(string path) => LoadRequired<GameObject>(path);
        private static T LoadRequired<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("缺少必需资源：" + path);

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
