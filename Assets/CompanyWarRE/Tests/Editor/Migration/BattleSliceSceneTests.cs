using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class BattleSliceSceneTests
    {
        private const string ScenePath = "Assets/CompanyWarRE/Scenes/BattleSliceTest.unity";
        private const string FormalScenePath = "Assets/CompanyWarRE/Scenes/FormalBattle.unity";

        [Test]
        public void BattleSliceScene_IsStandaloneAndContainsPresentationController()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Length, Is.EqualTo(1));
                Assert.That(roots[0].name, Is.EqualTo("BattleSliceBootstrap"));

                var componentNames = roots[0]
                    .GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null)
                    .Select(component => component.GetType().FullName)
                    .ToArray();
                Assert.That(componentNames, Does.Contain("CompanyWarRE.Presentation.BattleSliceController"));
            }
            finally
            {
                RestoreSceneSetup(previousSetup);
            }
        }

        [Test]
        public void SliceAssemblies_KeepDomainBehindApplicationBoundary()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Cannot resolve project root.");
            var domain = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/CompanyWarRE/Domain/CompanyWarRE.Domain.asmdef"));
            var application = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/CompanyWarRE/Application/CompanyWarRE.Application.asmdef"));
            var presentation = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/CompanyWarRE/Presentation/CompanyWarRE.Presentation.asmdef"));
            var infrastructure = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/CompanyWarRE/Infrastructure/CompanyWarRE.Infrastructure.asmdef"));

            StringAssert.DoesNotContain("QFramework", domain);
            StringAssert.Contains("\"noEngineReferences\": true", domain);
            StringAssert.Contains("CompanyWarRE.Domain", application);
            StringAssert.Contains("QFramework", application);
            StringAssert.Contains("CompanyWarRE.Application", presentation);
            StringAssert.Contains("CompanyWarRE.Infrastructure", presentation);
            StringAssert.Contains("QFramework", presentation);
            StringAssert.Contains("CompanyWarRE.Application", infrastructure);
            StringAssert.Contains("CompanyWarRE.Domain", infrastructure);
        }

        [Test]
        public void BattleSliceScene_ReferencesAllCompatibilityDocuments()
        {
            var controllerGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/Presentation/BattleSliceController.cs");
            var unitsGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.U01.json");
            var settingsGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/BattleSliceRuntime.L01.json");
            var enemiesGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.E01.json");
            var spawnSchedulesGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacySpawnSchedules.json");
            var levelGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyLevel.L01.json");
            var yaml = File.ReadAllText(ScenePath);

            StringAssert.Contains("guid: " + controllerGuid, yaml);
            StringAssert.Contains("guid: " + unitsGuid, yaml);
            StringAssert.Contains("guid: " + enemiesGuid, yaml);
            StringAssert.Contains("guid: " + settingsGuid, yaml);
            StringAssert.Contains("guid: " + spawnSchedulesGuid, yaml);
            StringAssert.Contains("guid: " + levelGuid, yaml);
        }

        [Test]
        public void FormalBattleScene_WiresL02ThroughL05IntoFormalStartup()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(FormalScenePath), Is.Not.Null);
            var controllerGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/Presentation/BattleSliceController.cs");
            var environmentGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/Presentation/BattleSliceEnvironmentView.cs");
            var unitsGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.All.json");
            var enemiesGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.All.json");
            var schedulesGuid = AssetDatabase.AssetPathToGUID(
                "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacySpawnSchedules.json");
            var yaml = File.ReadAllText(FormalScenePath);

            StringAssert.Contains("m_Name: FormalBattleBootstrap", yaml);
            StringAssert.Contains("guid: " + controllerGuid, yaml);
            StringAssert.Contains("guid: " + environmentGuid, yaml);
            StringAssert.Contains("guid: " + unitsGuid, yaml);
            StringAssert.Contains("guid: " + enemiesGuid, yaml);
            StringAssert.Contains("guid: " + schedulesGuid, yaml);
            StringAssert.Contains("useFormalLevelConfiguration: 1", yaml);
            StringAssert.Contains("formalLevelId: L02", yaml);
            foreach (var levelId in new[] { "L02", "L03", "L04", "L05" })
            {
                var guid = AssetDatabase.AssetPathToGUID(
                    $"Assets/CompanyWarRE/ConfigSamples/Compatibility/FormalLevel.{levelId}.json");
                StringAssert.Contains("guid: " + guid, yaml, levelId + " is not wired to the scene.");
            }
        }

        [Test]
        public void FormalBattleController_ExposesCompleteFormalFlowUseCases()
        {
            var controllerType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceController, CompanyWarRE.Presentation",
                true);

            foreach (var methodName in new[]
                     {
                         "StartFormalLevel",
                         "OpenFormalLevelSelect",
                         "ReturnFormalMainMenu",
                         "ToggleFormalPause",
                         "RestartFormalLevel",
                         "RequestAuthorization",
                         "AcceptAuthorization",
                         "CancelAuthorizationChoice",
                         "SaveAudioSetting",
                         "LoadProductionAssetAsync",
                         "LoadProductionSceneAsync",
                         "SelectDeploymentUnit",
                         "SetRuntimeHudVisible",
                         "SetRuntimeUiPointerBlocked"
                     })
            {
                Assert.That(
                    controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public),
                    Is.Not.Null,
                    methodName);
            }

            Assert.That(controllerType.GetProperty("CurrentFlow"), Is.Not.Null);
            Assert.That(controllerType.GetProperty("CurrentSnapshot"), Is.Not.Null);
            Assert.That(controllerType.GetProperty("SaveStatus"), Is.Not.Null);
            Assert.That(controllerType.GetProperty("SavePath"), Is.Not.Null);
            Assert.That(controllerType.GetProperty("IsSaveWritable"), Is.Not.Null);
            Assert.That(controllerType.GetProperty("Performance"), Is.Not.Null);
        }

        [Test]
        public void FormalBattle_UsesProductionLoadingPoolingAudioAndPerformanceBoundaries()
        {
            var controller = File.ReadAllText(
                "Assets/CompanyWarRE/Presentation/BattleSliceController.cs");
            var loading = File.ReadAllText(
                "Assets/CompanyWarRE/Presentation/ProductionAssetLoading.cs");
            var decision = File.ReadAllText(
                "Migration/Specifications/ProductionInfrastructure-v1.md");

            StringAssert.Contains("ProductionComponentPool<BattleSliceCombatantView>", controller);
            StringAssert.Contains("MaterialPropertyBlock", File.ReadAllText(
                "Assets/CompanyWarRE/Presentation/BattleSliceCellView.cs"));
            StringAssert.Contains("ResKitWithResourcesFallbackProvider", loading);
            StringAssert.Contains("SceneManager.LoadSceneAsync", loading);
            StringAssert.Contains("FormalAudioService", controller);
            StringAssert.Contains("BattleRuntimePerformanceMonitor", controller);
            StringAssert.Contains("Addressables 暂不安装", decision);
            StringAssert.Contains("禁止同一资源同时进入 Addressables 与 ResKit 清单", decision);
        }

        [Test]
        public void FormalBattleScene_UsesGeneratedHudWithoutLegacyCowBootstrap()
        {
            const string bootstrapPath =
                "Assets/CompanyWarRE/Compatibility/CowUI/FormalCowUiBootstrap.cs";
            const string hudPath =
                "Assets/CompanyWarRE/Presentation/UI/FormalBattleHudController.cs";
            var yaml = File.ReadAllText(FormalScenePath);

            var bootstrapGuid = AssetDatabase.AssetPathToGUID(bootstrapPath);
            var hudGuid = AssetDatabase.AssetPathToGUID(hudPath);
            Assert.That(bootstrapGuid, Is.Not.Empty);
            Assert.That(hudGuid, Is.Not.Empty);
            StringAssert.DoesNotContain("guid: " + bootstrapGuid, yaml);
            StringAssert.Contains("guid: " + hudGuid, yaml);
            StringAssert.Contains("[Generated] FormalBattleHUD", yaml);

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(FormalScenePath, OpenSceneMode.Single);
                var components = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null)
                    .ToArray();
                Assert.That(components.Any(component =>
                    component.GetType().FullName == "CompanyWar.UI.FormalCowUiBootstrap"), Is.False);
                Assert.That(components.Any(component =>
                    component.GetType().FullName ==
                    "CompanyWarRE.Presentation.UI.FormalBattleHudController"), Is.True);
            }
            finally
            {
                RestoreSceneSetup(previousSetup);
            }
        }

        [Test]
        public void CowTmpShader_UsesInstalledTargetEssentialResources()
        {
            const string shaderPath =
                "Assets/CompanyWarRE/Resources/CowLegacy/TextMesh Pro/Shaders/TMP_SDF.shader";
            var shader = File.ReadAllText(shaderPath);

            StringAssert.Contains(
                "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc",
                shader);
            StringAssert.Contains("Assets/TextMesh Pro/Shaders/TMPro.cginc", shader);
            Assert.That(File.Exists("Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc"), Is.True);
            Assert.That(File.Exists("Assets/TextMesh Pro/Shaders/TMPro.cginc"), Is.True);
        }

        [Test]
        public void CowTmpFontAsset_IsDynamicAndContainsFormalUguiCharacters()
        {
            const string fontAssetPath =
                "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Fonts/Default SDF.asset";
            const string requiredCharacters =
                "资源授权战斗结算重新开始下一关卡选择返回主菜单成长一个新单位加入部署列表" +
                "胜利失败突击分剩余建筑用时已完成可挑战未解锁继续暂停";
            var prefabPaths = new[]
            {
                "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Scripts/UI/Menu.prefab",
                "Assets/CompanyWarRE/LevelSelectPanel.prefab",
                "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Scripts/UI/BattlePanel.prefab"
            };
            var fontAssetType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro", true);
            var fontAsset = AssetDatabase.LoadAssetAtPath(fontAssetPath, fontAssetType);

            Assert.That(fontAsset, Is.Not.Null, fontAssetPath);
            foreach (var prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                var textComponents = prefab
                    .GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component =>
                        component != null && component.GetType().FullName == "TMPro.TextMeshProUGUI")
                    .ToArray();
                Assert.That(textComponents, Is.Not.Empty, prefabPath);
                foreach (var textComponent in textComponents)
                {
                    var serializedText = new SerializedObject(textComponent);
                    Assert.That(
                        AssetDatabase.GetAssetPath(
                            serializedText.FindProperty("m_fontAsset").objectReferenceValue),
                        Is.EqualTo(fontAssetPath),
                        prefabPath + ":" + textComponent.name);
                }
            }

            var testFontAsset = UnityEngine.Object.Instantiate(fontAsset);
            try
            {
                var serializedFontAsset = new SerializedObject(testFontAsset);
                Assert.That(
                    serializedFontAsset.FindProperty("m_AtlasPopulationMode").intValue,
                    Is.EqualTo(1),
                    "Target TMP 3.0.7 requires AtlasPopulationMode.Dynamic (1).");
                Assert.That(
                    serializedFontAsset.FindProperty("m_SourceFontFile").objectReferenceValue,
                    Is.Not.Null,
                    "Dynamic font assets require the real source TTF.");

                var tryAddCharacters = fontAssetType.GetMethod(
                    "TryAddCharacters",
                    new[] { typeof(string), typeof(string).MakeByRefType(), typeof(bool) });
                Assert.That(tryAddCharacters, Is.Not.Null);
                var arguments = new object[] { requiredCharacters, string.Empty, false };
                Assert.That((bool)tryAddCharacters.Invoke(testFontAsset, arguments), Is.True);
                Assert.That(arguments[1] as string ?? string.Empty, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testFontAsset);
            }
        }

        [Test]
        public void FormalBattleScene_ContainsControllerAndEnvironmentConsumerWithoutMissingComponents()
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(FormalScenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Length, Is.EqualTo(1));
                Assert.That(roots[0].name, Is.EqualTo("FormalBattleBootstrap"));
                var componentNames = roots[0]
                    .GetComponents<MonoBehaviour>()
                    .Where(component => component != null)
                    .Select(component => component.GetType().FullName)
                    .ToArray();
                Assert.That(componentNames, Does.Contain(
                    "CompanyWarRE.Presentation.BattleSliceController"));
                Assert.That(componentNames, Does.Contain(
                    "CompanyWarRE.Presentation.BattleSliceEnvironmentView"));
                var controller = roots[0].GetComponents<MonoBehaviour>()
                    .Single(component => component != null &&
                                         component.GetType().FullName ==
                                         "CompanyWarRE.Presentation.BattleSliceController");
                var serializedController = new SerializedObject(controller);
                Assert.That(
                    serializedController.FindProperty("battleMapPrefab")?.objectReferenceValue,
                    Is.Not.Null,
                    "FormalBattle must explicitly bind the shared editable map prefab.");
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(roots[0]), Is.Zero);
            }
            finally
            {
                RestoreSceneSetup(previousSetup);
            }
        }

        [Test]
        public void FormalEnvironmentConsumer_ResolvesCowDefaultsFromLoadedL02Metadata()
        {
            var sourceType = Type.GetType(
                "CompanyWarRE.Infrastructure.Configuration.DictionaryConfigurationTextSource, " +
                "CompanyWarRE.Infrastructure",
                true);
            var sourceInterface = Type.GetType(
                "CompanyWarRE.Infrastructure.Configuration.IConfigurationTextSource, " +
                "CompanyWarRE.Infrastructure",
                true);
            var pipelineType = Type.GetType(
                "CompanyWarRE.Infrastructure.Levels.FormalLevelConfigurationPipeline, " +
                "CompanyWarRE.Infrastructure",
                true);
            var environmentType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceEnvironmentView, CompanyWarRE.Presentation",
                true);
            var documents = new System.Collections.Generic.Dictionary<string, string>
            {
                ["units"] = File.ReadAllText(
                    "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyUnits.All.json"),
                ["enemies"] = File.ReadAllText(
                    "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacyEnemies.All.json"),
                ["schedules"] = File.ReadAllText(
                    "Assets/CompanyWarRE/ConfigSamples/Compatibility/LegacySpawnSchedules.json"),
                ["level"] = File.ReadAllText(
                    "Assets/CompanyWarRE/ConfigSamples/Compatibility/FormalLevel.L02.json")
            };
            var source = Activator.CreateInstance(sourceType, documents);
            var pipeline = pipelineType.GetConstructor(new[] { sourceInterface })
                ?.Invoke(new[] { source });
            Assert.That(pipeline, Is.Not.Null);
            var load = pipelineType.GetMethod("Load")?.Invoke(
                pipeline,
                new object[] { "units", "enemies", "schedules", "level" });
            var level = load?.GetType().GetProperty("Level")?.GetValue(load);
            Assert.That(level, Is.Not.Null);

            var layout = environmentType.GetMethod(
                    "ResolveLayout",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new[] { level });
            Assert.That(layout, Is.Not.Null);
            Assert.That(ReadProperty<string>(layout, "EnvironmentId"), Is.EqualTo("Cow.DefaultIndustrial"));
            Assert.That(ReadProperty<int>(layout, "DecorRing"), Is.EqualTo(5));
            Assert.That(ReadProperty<float>(layout, "OuterGroundSize"), Is.EqualTo(80f));
            Assert.That(ReadProperty<float>(layout, "SkylineDistance"), Is.EqualTo(35.2f).Within(0.001f));
            Assert.That(ReadProperty<float>(layout, "SkylineDensity"), Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(ReadProperty<int>(layout, "Seed"), Is.EqualTo(1001));
        }

        private static T ReadProperty<T>(object source, string propertyName)
        {
            return (T)source.GetType().GetProperty(propertyName)?.GetValue(source);
        }

        private static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup.Any(scene => scene.isLoaded))
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void L01Presentation_UsesContinuousGridWithControlBlockBordersAndFitsAdaptiveCamera()
        {
            var controllerType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceController, CompanyWarRE.Presentation",
                true);
            var cameraRigType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceCameraRig, CompanyWarRE.Presentation",
                true);
            var getColumnWorldX = controllerType.GetMethod(
                "GetColumnWorldX",
                BindingFlags.Static | BindingFlags.NonPublic);
            var getRowWorldZ = controllerType.GetMethod(
                "GetRowWorldZ",
                BindingFlags.Static | BindingFlags.NonPublic);
            var calculateCameraSize = cameraRigType.GetMethod(
                "CalculateOrthographicSize",
                BindingFlags.Static | BindingFlags.Public);
            var getBoundaryWorldCoordinate = controllerType.GetMethod(
                "GetControlBlockBoundaryWorldCoordinate",
                BindingFlags.Static | BindingFlags.NonPublic);
            var cellVisualSize = controllerType.GetField(
                "CellVisualSize",
                BindingFlags.Static | BindingFlags.NonPublic);
            var controlBlockBorderWidth = controllerType.GetField(
                "ControlBlockBorderWidth",
                BindingFlags.Static | BindingFlags.NonPublic);
            var columnGroupBorderWidth = controllerType.GetField(
                "ColumnGroupBorderWidth",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(getColumnWorldX, Is.Not.Null);
            Assert.That(getRowWorldZ, Is.Not.Null);
            Assert.That(calculateCameraSize, Is.Not.Null);
            Assert.That(getBoundaryWorldCoordinate, Is.Not.Null);
            Assert.That(cellVisualSize, Is.Not.Null);
            Assert.That(controlBlockBorderWidth, Is.Not.Null);
            Assert.That(columnGroupBorderWidth, Is.Not.Null);

            var column3 = (float)getColumnWorldX.Invoke(null, new object[] { 3 });
            var column4 = (float)getColumnWorldX.Invoke(null, new object[] { 4 });
            var row3 = (float)getRowWorldZ.Invoke(null, new object[] { 3 });
            var row4 = (float)getRowWorldZ.Invoke(null, new object[] { 4 });
            Assert.That(column4 - column3, Is.EqualTo(1f).Within(0.001f));
            Assert.That(row4 - row3, Is.EqualTo(1f).Within(0.001f));
            Assert.That((float)cellVisualSize.GetRawConstantValue(), Is.EqualTo(0.92f).Within(0.001f));
            Assert.That(
                (float)getBoundaryWorldCoordinate.Invoke(null, new object[] { 3 }),
                Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(
                (float)columnGroupBorderWidth.GetRawConstantValue(),
                Is.GreaterThan((float)controlBlockBorderWidth.GetRawConstantValue()));

            var width = (float)getColumnWorldX.Invoke(null, new object[] { 18 }) + 1f;
            var length = (float)getRowWorldZ.Invoke(null, new object[] { 30 }) + 1f;
            var cameraSize = (float)calculateCameraSize.Invoke(
                null,
                new object[] { width, length, 16f / 9f });
            Assert.That(cameraSize, Is.GreaterThan(length * 0.6f));
        }

        [Test]
        public void PlayablePresentationScripts_AreUnityAssetsWithoutGuidCollisions()
        {
            var cameraPath = "Assets/CompanyWarRE/Presentation/BattleSliceCameraRig.cs";
            var feedbackPath = "Assets/CompanyWarRE/Presentation/BattleSliceFeedbackLayer.cs";
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(cameraPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(feedbackPath), Is.Not.Null);

            var cameraGuid = AssetDatabase.AssetPathToGUID(cameraPath);
            var feedbackGuid = AssetDatabase.AssetPathToGUID(feedbackPath);
            Assert.That(cameraGuid, Is.Not.Empty);
            Assert.That(feedbackGuid, Is.Not.Empty);
            Assert.That(cameraGuid, Is.Not.EqualTo(feedbackGuid));
        }

        [Test]
        public void CowStyleCameraControl_ClampsZoomPitchAndBattlefieldFocus()
        {
            var cameraRigType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceCameraRig, CompanyWarRE.Presentation",
                true);
            var calculateZoomSize = cameraRigType.GetMethod(
                "CalculateZoomSize",
                BindingFlags.Static | BindingFlags.Public);
            var clampFocus = cameraRigType.GetMethod(
                "ClampFocusToBounds",
                BindingFlags.Static | BindingFlags.Public);
            var clampOrbitPitch = cameraRigType.GetMethod(
                "ClampOrbitPitch",
                BindingFlags.Static | BindingFlags.Public);

            Assert.That(calculateZoomSize, Is.Not.Null);
            Assert.That(clampFocus, Is.Not.Null);
            Assert.That(clampOrbitPitch, Is.Not.Null);

            var zoomedIn = (float)calculateZoomSize.Invoke(
                null,
                new object[] { 20f, 1f, 5f, 36f, 0.1f });
            var minimumZoom = (float)calculateZoomSize.Invoke(
                null,
                new object[] { 5f, 10f, 5f, 36f, 0.1f });
            var maximumZoom = (float)calculateZoomSize.Invoke(
                null,
                new object[] { 36f, -10f, 5f, 36f, 0.1f });
            Assert.That(zoomedIn, Is.EqualTo(18f).Within(0.001f));
            Assert.That(minimumZoom, Is.EqualTo(5f).Within(0.001f));
            Assert.That(maximumZoom, Is.EqualTo(36f).Within(0.001f));

            var clamped = (Vector3)clampFocus.Invoke(
                null,
                new object[] { new Vector3(-4f, 9f, 45f), Vector2.zero, new Vector2(22f, 38f) });
            Assert.That(clamped, Is.EqualTo(new Vector3(0f, 0f, 38f)));

            var minimumPitch = (float)clampOrbitPitch.Invoke(null, new object[] { 10f, 25f, 75f });
            var maximumPitch = (float)clampOrbitPitch.Invoke(null, new object[] { 90f, 25f, 75f });
            Assert.That(minimumPitch, Is.EqualTo(25f));
            Assert.That(maximumPitch, Is.EqualTo(75f));
        }

        [Test]
        public void CompleteCowLevelCatalogAndRecoverableSnapshot_ArePresent()
        {
            var levelIds = Enumerable.Range(0, 21)
                .Select(index => $"L{index:00}")
                .Concat(new[] { "L_ENDLESS" })
                .ToArray();
            foreach (var levelId in levelIds)
            {
                var path =
                    "Assets/CompanyWarRE/Resources/CompanyWarRE/Configs/Levels/" +
                    levelId + ".json";
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(path), Is.Not.Null, levelId);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty, levelId);
            }

            const string snapshotRoot =
                "Migration/Baseline/Cow/20260830-all-level-configs";
            Assert.That(File.Exists(snapshotRoot + "/cow-level-configs.zip"), Is.True);
            Assert.That(new FileInfo(snapshotRoot + "/cow-level-configs.zip").Length, Is.GreaterThan(0));
            var verification = File.ReadAllText(snapshotRoot + "/restore-verification.txt");
            StringAssert.Contains("FileCount: 50", verification);
            StringAssert.Contains("ArchiveRestoreVerified: true", verification);
            StringAssert.Contains("verification: PASS", verification);

            var controllerType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceController, CompanyWarRE.Presentation",
                true);
            var available = ((System.Collections.IEnumerable)controllerType
                    .GetProperty("AvailableFormalLevelIds", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
            Assert.That(available, Is.EqualTo(levelIds));
        }

        [Test]
        public void FormalBattleBoard_IsAReusablePrefabWithoutMissingScripts()
        {
            const string prefabPath =
                "Assets/CompanyWarRE/Resources/CompanyWarRE/Battle/PF_FormalBattleBoard.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<Component>(true).Any(component => component == null),
                Is.False);
            Assert.That(
                prefab.GetComponents<MonoBehaviour>()
                    .Any(component => component != null &&
                                      component.GetType().FullName ==
                                      "CompanyWarRE.Presentation.FormalBattleBoardView"),
                Is.True);
            Assert.That(
                prefab.GetComponents<MonoBehaviour>()
                    .Any(component => component != null &&
                                      component.GetType().FullName ==
                                      "CompanyWarRE.Presentation.CowBoardVisualRenderer"),
                Is.True);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/CompanyWarRE/Resources/CompanyWarRE/Battle/" +
                    "CowBoardVisualTheme_Default.asset"),
                Is.Not.Null);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var board = instance.GetComponents<MonoBehaviour>()
                    .Single(component => component != null &&
                                         component.GetType().FullName ==
                                         "CompanyWarRE.Presentation.FormalBattleBoardView");
                board.GetType().GetMethod("Prepare", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(board, new object[] { 24, 45 });
                Assert.That(instance.name, Is.EqualTo("FormalBattleBoard_24x45"));
                Assert.That(instance.transform.Find("RuntimeGrid"), Is.Not.Null);
                Assert.That(instance.transform.Find("RuntimeCombatants"), Is.Not.Null);
                Assert.That(instance.transform.Find("RuntimeFeedback"), Is.Not.Null);
                var decoration = instance.transform.Find("CowBoardDecoration");
                Assert.That(decoration, Is.Not.Null);
                Assert.That(decoration.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "Cow board decoration must never intercept board interaction rays.");
                var decorationRenderers = decoration.GetComponentsInChildren<Renderer>(true);
                Assert.That(decorationRenderers, Is.Not.Empty);
                Assert.That(
                    decorationRenderers.All(renderer =>
                        renderer.sharedMaterial != null &&
                        renderer.sharedMaterial.shader != null &&
                        renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"),
                    Is.True,
                    "The migrated Cow board must use URP materials only.");
                Assert.That(
                    (float)board.GetType().GetProperty("CellVisualFill")?.GetValue(board),
                    Is.EqualTo(0.92f).Within(0.0001f),
                    "Cow generates the board procedurally with a 0.92 visual fill ratio.");
                Assert.That(
                    (float)board.GetType().GetProperty("CellHeight")?.GetValue(board),
                    Is.EqualTo(0.1125f).Within(0.0001f),
                    "Cow's formal 0.045 height is normalized from its 0.4 small-cell pitch to target pitch 1.0.");
                Assert.That(
                    (float)board.GetType().GetProperty("SurfaceOffsetY")?.GetValue(board),
                    Is.EqualTo(-0.02f).Within(0.0001f));
                var themeType = Type.GetType(
                    "CompanyWarRE.Presentation.CowBoardVisualTheme, CompanyWarRE.Presentation",
                    true);
                Assert.That(
                    (float)themeType.GetField("SourceSmallCellPitch")?.GetRawConstantValue(),
                    Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SharedFormalMap_ProvidesCenteredRotatableAnchorAndNonCollidingEnvironment()
        {
            const string prefabPath =
                "Assets/CompanyWarRE/Content/Maps/PF_BaseFormalBattleMap.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<Component>(true).Any(component => component == null),
                Is.False);

            var map = prefab.GetComponents<MonoBehaviour>()
                .Single(component => component != null &&
                                     component.GetType().FullName ==
                                     "CompanyWarRE.Presentation.FormalBattleMapView");
            var anchor = (Transform)map.GetType().GetProperty("BattleBoardAnchor")?.GetValue(map);
            var environment = (Transform)map.GetType().GetProperty("EnvironmentRoot")?.GetValue(map);
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.name, Is.EqualTo("BattleBoardAnchor"));
            Assert.That(anchor.localScale, Is.EqualTo(Vector3.one),
                "The target presentation normalizes Cow's 0.4 pitch to one visual unit per cell.");
            Assert.That(environment, Is.Not.Null);
            Assert.That(environment.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(environment.GetComponentInChildren<Light>(true), Is.Not.Null,
                "The shared map must expose editable lighting under EnvironmentRoot.");
            var cowEnvironment = environment.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component != null &&
                                     component.GetType().FullName ==
                                     "CompanyWarRE.Presentation.CowIndustrialEnvironmentView");
            Assert.That(cowEnvironment, Is.Not.Null);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("IncludesRailings")?.GetValue(cowEnvironment), Is.True);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("IncludesPipes")?.GetValue(cowEnvironment), Is.True);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("IncludesStairs")?.GetValue(cowEnvironment), Is.True);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("IncludesSkyline")?.GetValue(cowEnvironment), Is.True);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("IncludesOptionalModels")?.GetValue(cowEnvironment), Is.True);
            Assert.That((bool)cowEnvironment.GetType().GetProperty("HasUrpMaterialTemplate")?.GetValue(cowEnvironment), Is.True);
            Assert.That((int)cowEnvironment.GetType().GetProperty("ConfiguredOptionalModelCount")?.GetValue(cowEnvironment), Is.EqualTo(7));
            var ground = environment.Find("BaseGround");
            Assert.That(ground, Is.Not.Null);
            var groundMaterial = ground.GetComponent<Renderer>()?.sharedMaterial;
            Assert.That(groundMaterial, Is.Not.Null);
            Assert.That(groundMaterial.shader, Is.Not.Null);
            Assert.That(groundMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"),
                "The base map must not use Unity's built-in default material in the URP project.");
            Assert.That(
                AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/CompanyWarRE/EditorTools/FormalBattleMapEditorWindow.cs"),
                Is.Not.Null);
            Assert.That(
                (bool)map.GetType().GetMethod("HasUniformAnchorScale")
                    ?.Invoke(map, new object[] { 0.0001f }),
                Is.True);

            var controllerType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceController, CompanyWarRE.Presentation",
                true);
            var centeredColumn = controllerType.GetMethod(
                "GetCenteredColumnCoordinate",
                BindingFlags.Static | BindingFlags.NonPublic);
            var centeredBoundary = controllerType.GetMethod(
                "GetCenteredBoundaryCoordinate",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(centeredColumn, Is.Not.Null);
            Assert.That(centeredBoundary, Is.Not.Null);
            Assert.That(
                (float)centeredColumn.Invoke(null, new object[] { 1, 18 }),
                Is.EqualTo(-8.5f).Within(0.0001f));
            Assert.That(
                (float)centeredColumn.Invoke(null, new object[] { 18, 18 }),
                Is.EqualTo(8.5f).Within(0.0001f));
            Assert.That(
                (float)centeredBoundary.Invoke(null, new object[] { 0, 18 }),
                Is.EqualTo(-9f).Within(0.0001f));
            Assert.That(
                (float)centeredBoundary.Invoke(null, new object[] { 18, 18 }),
                Is.EqualTo(9f).Within(0.0001f));
        }

        [Test]
        public void CowIndustrialEnvironment_UsesMigratedModelsUrpAndNoActiveCollision()
        {
            var modelPaths = new[]
            {
                "货运单元2.fbx",
                "货运单元1.fbx",
                "栏杆左到右123.fbx",
                "服务器终端.fbx",
                "储装罐.fbx",
                "信号基站（拆件.fbx",
                "棋盘part (1).fbx"
            };
            foreach (var fileName in modelPaths)
            {
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/CompanyWarRE/Content/Environment/Cow/Models/" + fileName),
                    Is.Not.Null,
                    fileName);
            }

            const string prefabPath =
                "Assets/CompanyWarRE/Content/Maps/PF_BaseFormalBattleMap.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                var baked = instance.transform.Find(
                    "EnvironmentRoot/CowIndustrialEnvironment/BakedCowEnvironment");
                Assert.That(baked, Is.Not.Null, "Environment must be baked into the map prefab.");
                var map = instance.GetComponent<MonoBehaviour>();
                map.GetType().GetMethod("PrepareForBattle")?.Invoke(map, new object[] { 18, 30 });
                Assert.That(instance.transform.Find(
                    "EnvironmentRoot/CowIndustrialEnvironment/GeneratedCowEnvironment"), Is.Null,
                    "Formal battle must not generate environment geometry at runtime.");
                var ground = instance.transform.Find("EnvironmentRoot/BaseGround");
                Assert.That(ground, Is.Not.Null);
                Assert.That(ground.localScale.x, Is.EqualTo(240f).Within(0.001f));
                Assert.That(ground.localScale.z, Is.EqualTo(240f).Within(0.001f));
                var groundMaterial = ground.GetComponent<Renderer>()?.sharedMaterial;
                Assert.That(groundMaterial, Is.Not.Null);
                Assert.That(groundMaterial.name, Does.StartWith("MAT_CowIndustrialGround_"));
                Assert.That(groundMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                var groundTiling = groundMaterial.GetTextureScale("_BaseMap");
                Assert.That(groundTiling.x, Is.EqualTo(7.5f).Within(0.001f));
                Assert.That(groundTiling.y, Is.EqualTo(7.5f).Within(0.001f));
                Assert.That(baked.Find("Skyline"), Is.Not.Null);
                var skylineBlocks = baked.Find("Skyline").Cast<Transform>()
                    .Where(child => child.name.StartsWith("Skyline_Block_"))
                    .ToArray();
                Assert.That(skylineBlocks.Length, Is.EqualTo(96));
                Assert.That(
                    skylineBlocks.All(block =>
                        Mathf.Abs(block.localPosition.x) > 19f ||
                        Mathf.Abs(block.localPosition.z) > 25f),
                    Is.True,
                    "Scattered buildings must leave the battle-board safety area clear.");
                Assert.That(
                    skylineBlocks.Any(block =>
                        Mathf.Abs(block.localPosition.x) < 100f &&
                        Mathf.Abs(block.localPosition.z) < 100f),
                    Is.True,
                    "Buildings must be scattered through the map instead of forming only an edge ring.");
                Assert.That(baked.Find("SeededEnvironmentModelFill"), Is.Not.Null);
                var importedMaterials = baked.Find("SeededEnvironmentModelFill")
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .ToArray();
                Assert.That(
                    importedMaterials.Any(material =>
                        material.name.StartsWith("MAT_CowImported_")),
                    Is.True,
                    "Imported Cow models must preserve their material slots as persistent URP materials.");
                Assert.That(instance.transform.Find(
                    "EnvironmentRoot/CowIndustrialEnvironment/EditableEnvironmentModelFill"),
                    Is.Not.Null,
                    "Manual environment additions must survive automatic rebakes.");
                Assert.That(baked.Cast<Transform>().Any(child => child.name.StartsWith("Pipe_")), Is.True);
                Assert.That(baked.Cast<Transform>().Any(child => child.name.StartsWith("Stairs_")), Is.True);
                Assert.That(baked.Cast<Transform>().Any(child => child.name.StartsWith("ENV_Railing_")), Is.True);
                Assert.That(
                    baked.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled),
                    Is.True);
                Assert.That(
                    baked.GetComponentsInChildren<Renderer>(true).All(renderer =>
                        renderer.sharedMaterial != null &&
                        renderer.sharedMaterial.shader != null &&
                        renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Lit" &&
                        AssetDatabase.Contains(renderer.sharedMaterial)),
                    Is.True);

                var environment = instance.GetComponentsInChildren<MonoBehaviour>(true)
                    .Single(component => component != null &&
                                         component.GetType().FullName ==
                                         "CompanyWarRE.Presentation.CowIndustrialEnvironmentView");
                var editable = environment.transform.Find("EditableEnvironmentModelFill");
                var manualCargo = new GameObject("ENV_CargoUnit02");
                manualCargo.transform.SetParent(editable, false);
                Assert.That(
                    (bool)environment.GetType().GetMethod("ExcludeGeneratedObject")
                        ?.Invoke(environment, new object[] { manualCargo.name }),
                    Is.True);
                environment.GetType().GetMethod("Build")
                    ?.Invoke(environment, new object[] { 18, 30 });
                var regenerated = environment.transform.Find("GeneratedCowEnvironment");
                Assert.That(regenerated, Is.Not.Null);
                Assert.That(
                    regenerated.GetComponentsInChildren<Transform>(true)
                        .Any(child => child.name == manualCargo.name),
                    Is.False,
                    "A converted manual object must be excluded from future generated output.");
                Assert.That(editable.Find(manualCargo.name), Is.Not.Null,
                    "Manual environment objects must survive a rebuild.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void FormalBattle_RequiresExplicitBakedMapAndHasNoRuntimeMapFallback()
        {
            var source = File.ReadAllText(
                "Assets/CompanyWarRE/Presentation/BattleSliceController.cs");
            Assert.That(source, Does.Not.Contain("Resources.Load<FormalBattleMapView>"));
            Assert.That(source, Does.Not.Contain("PF_BaseFormalBattleMap_Fallback"));
            Assert.That(source, Does.Contain(
                "Formal battle requires an explicitly assigned, editor-baked map prefab."));
        }
    }
}
