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
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
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
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(roots[0]), Is.Zero);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
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
            Assert.That((float)cellVisualSize.GetRawConstantValue(), Is.EqualTo(0.98f).Within(0.001f));
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
    }
}
