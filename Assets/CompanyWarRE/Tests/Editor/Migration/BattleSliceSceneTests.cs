using System;
using System.IO;
using System.Linq;
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

            StringAssert.DoesNotContain("QFramework", domain);
            StringAssert.Contains("\"noEngineReferences\": true", domain);
            StringAssert.Contains("CompanyWarRE.Domain", application);
            StringAssert.Contains("QFramework", application);
            StringAssert.Contains("CompanyWarRE.Application", presentation);
            StringAssert.Contains("QFramework", presentation);
        }
    }
}
