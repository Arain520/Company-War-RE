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
        public void L01Presentation_SeparatesControlBlockGroupsAndFitsAdaptiveCamera()
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

            Assert.That(getColumnWorldX, Is.Not.Null);
            Assert.That(getRowWorldZ, Is.Not.Null);
            Assert.That(calculateCameraSize, Is.Not.Null);

            var column3 = (float)getColumnWorldX.Invoke(null, new object[] { 3 });
            var column4 = (float)getColumnWorldX.Invoke(null, new object[] { 4 });
            var row3 = (float)getRowWorldZ.Invoke(null, new object[] { 3 });
            var row4 = (float)getRowWorldZ.Invoke(null, new object[] { 4 });
            Assert.That(column4 - column3, Is.GreaterThan(1.5f));
            Assert.That(row4 - row3, Is.GreaterThan(1.5f));

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
    }
}
