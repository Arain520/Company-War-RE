using System;
using System.Collections.Generic;
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
    public sealed class ProductionResourceMigrationTests
    {
        private const string FormalScenePath = "Assets/CompanyWarRE/Scenes/FormalBattle.unity";

        private static readonly IReadOnlyDictionary<string, string> ExpectedGuids =
            new Dictionary<string, string>
            {
                ["Assets/CompanyWarRE/Content/Prefabs/Units/PF_U01.prefab"] =
                    "8800a5eda99ca9148b0e9a94258025eb",
                ["Assets/CompanyWarRE/Content/Prefabs/Enemies/PF_E01.prefab"] =
                    "88b3bd44a9baaa14ca68d58fcfc8b60f",
                ["Assets/CompanyWarRE/Content/Prefabs/Buildings/PF_E06.prefab"] =
                    "0b36364c0d8c8c34a91ea39f779676fc",
                ["Assets/CompanyWarRE/Content/Prefabs/Buildings/PF_E07.prefab"] =
                    "0f913102eb457e246b99ac01ec3e9871",
                ["Assets/CompanyWarRE/Content/Materials/Mat_U01.mat"] =
                    "17b34ab7ec0f811408ec679715b1f948",
                ["Assets/CompanyWarRE/Content/Materials/Mat_E01.mat"] =
                    "a326d73ee601f3946ab28f97c0edc432",
                ["Assets/CompanyWarRE/Content/Materials/Mat_E06.mat"] =
                    "184cbfe1ed6d3c241b7ec9d9b5d2326d",
                ["Assets/CompanyWarRE/Content/Materials/Mat_E07.mat"] =
                    "73e65bf99733ab54ebbdacf1938482a6",
                ["Assets/CompanyWarRE/Content/Models/U01.obj"] =
                    "25cebf529e493fb4ebbedd04c000cf1f",
                ["Assets/CompanyWarRE/Content/Models/E01.fbx"] =
                    "d3bdc38d9e243df47a93adcee51b362e",
                ["Assets/CompanyWarRE/Content/Models/E06.fbx"] =
                    "6587bd970d7104147955315630d14712",
                ["Assets/CompanyWarRE/Content/Models/E07.fbx"] =
                    "68413b30f6236ad45840f8c4294add33"
            };

        [Test]
        public void Batch01Assets_PreserveCowGuidsAndResolveDependencies()
        {
            foreach (var pair in ExpectedGuids)
            {
                Assert.That(AssetDatabase.GUIDToAssetPath(pair.Value), Is.EqualTo(pair.Key), pair.Key);
                Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }

            AssertPrefabDependencies(
                "Assets/CompanyWarRE/Content/Prefabs/Units/PF_U01.prefab",
                "Assets/CompanyWarRE/Content/Materials/Mat_U01.mat",
                "Assets/CompanyWarRE/Content/Models/U01.obj");
            AssertPrefabDependencies(
                "Assets/CompanyWarRE/Content/Prefabs/Enemies/PF_E01.prefab",
                "Assets/CompanyWarRE/Content/Materials/Mat_E01.mat",
                "Assets/CompanyWarRE/Content/Models/E01.fbx");
            AssertPrefabDependencies(
                "Assets/CompanyWarRE/Content/Prefabs/Buildings/PF_E06.prefab",
                "Assets/CompanyWarRE/Content/Materials/Mat_E06.mat",
                "Assets/CompanyWarRE/Content/Models/E06.fbx");
            AssertPrefabDependencies(
                "Assets/CompanyWarRE/Content/Prefabs/Buildings/PF_E07.prefab",
                "Assets/CompanyWarRE/Content/Materials/Mat_E07.mat",
                "Assets/CompanyWarRE/Content/Models/E07.fbx");
        }

        [Test]
        public void Batch01Prefabs_HaveNoMissingScriptsOrMaterialsAndUseUrp()
        {
            foreach (var path in ExpectedGuids.Keys.Where(path => path.EndsWith(".prefab")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                        Is.Zero,
                        path + ":" + transform.name);
                }

                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty, path + ":" + renderer.name);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, path + ":" + renderer.name);
                        Assert.That(material.shader, Is.Not.Null, material.name);
                        StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
                    }
                }
            }
        }

        [Test]
        public void FormalBattleCatalog_ResolvesImportedVisualsWithoutAResourceBackendCommitment()
        {
            var catalogType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceVisualCatalog, CompanyWarRE.Presentation",
                true);
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(FormalScenePath, OpenSceneMode.Single);
                var catalog = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent(catalogType))
                    .FirstOrDefault(component => component != null);
                Assert.That(catalog, Is.Not.Null);

                var tryResolve = catalogType.GetMethod("TryResolve", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryResolve, Is.Not.Null);
                foreach (var templateId in new[] { "U01", "E01", "E06", "E07" })
                {
                    var arguments = new object[] { templateId, null };
                    Assert.That((bool)tryResolve.Invoke(catalog, arguments), Is.True, templateId);
                    Assert.That(arguments[1], Is.Not.Null, templateId);
                    var prefab = arguments[1].GetType().GetProperty("Prefab")?.GetValue(arguments[1]);
                    Assert.That(prefab, Is.Not.Null, templateId);
                }

                var missingArguments = new object[] { "U36", null };
                Assert.That((bool)tryResolve.Invoke(catalog, missingArguments), Is.False);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        [Test]
        public void ImportedVisuals_NormalizeMovingUnitsToOneCellAndBuildingsToThreeByThree()
        {
            var viewType = Type.GetType(
                "CompanyWarRE.Presentation.BattleSliceCombatantView, CompanyWarRE.Presentation",
                true);
            var calculateScale = viewType.GetMethod(
                "CalculateFootprintScale",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(calculateScale, Is.Not.Null);

            var movingScale = (float)calculateScale.Invoke(
                null,
                new object[] { new Vector3(3.9f, 8f, 1.3f), false });
            var buildingScale = (float)calculateScale.Invoke(
                null,
                new object[] { new Vector3(5.3f, 2f, 2.65f), true });

            Assert.That(3.9f * movingScale, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(5.3f * buildingScale, Is.EqualTo(2.65f).Within(0.001f));

            AssertVisualFit(
                viewType,
                "Assets/CompanyWarRE/Content/Prefabs/Units/PF_U01.prefab",
                false,
                0.95f);
            AssertVisualFit(
                viewType,
                "Assets/CompanyWarRE/Content/Prefabs/Enemies/PF_E01.prefab",
                false,
                0.95f);
            AssertVisualFit(
                viewType,
                "Assets/CompanyWarRE/Content/Prefabs/Buildings/PF_E06.prefab",
                true,
                2.65f);
        }

        [Test]
        public void Batch01SnapshotAndCompleteCatalogManifest_ArePresentAndVerified()
        {
            const string snapshotRoot =
                "Migration/Baseline/Cow/20260825-production-resources-batch-01";
            var verification = File.ReadAllText(Path.Combine(snapshotRoot, "restore-verification.txt"));
            StringAssert.Contains("verification: PASS", verification);
            StringAssert.Contains("FileCount: 26", verification);
            StringAssert.Contains("ArchiveRestoreVerified: true", verification);
            Assert.That(new FileInfo(Path.Combine(snapshotRoot, "selected-resources.zip")).Length, Is.GreaterThan(0));

            var visualRows = File.ReadAllLines(
                "Migration/Inventory/ProductionResources/CombatVisualResourceMap.csv");
            Assert.That(visualRows.Length, Is.EqualTo(52), "Header plus U01-U36 and E01-E15 expected.");
            Assert.That(visualRows.Count(line => line.Contains("\"ImportedBatch01\"")), Is.EqualTo(4));

            var compatibilityRows = File.ReadAllLines(
                "Migration/Inventory/ProductionResources/SerializationCompatibilityBatch01.csv");
            Assert.That(compatibilityRows.Length, Is.EqualTo(6));
            StringAssert.Contains("e4e65e1ec4a34c068baf55ae0319f61c", compatibilityRows[1]);
        }

        private static void AssertPrefabDependencies(
            string prefabPath,
            string materialPath,
            string modelPath)
        {
            var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
            Assert.That(dependencies, Does.Contain(materialPath), prefabPath);
            Assert.That(dependencies, Does.Contain(modelPath), prefabPath);
        }

        private static void AssertVisualFit(
            Type viewType,
            string prefabPath,
            bool isBuilding,
            float expectedHorizontalSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var host = new GameObject("VisualFitTestHost");
            try
            {
                var view = host.AddComponent(viewType);
                var instance = UnityEngine.Object.Instantiate(prefab, host.transform, false);
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                viewType.GetField("_importedBody", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(view, instance.transform);
                viewType.GetField("_importedRenderers", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(view, renderers);
                var fit = viewType.GetMethod(
                    "FitImportedBodyToFootprint",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fit, Is.Not.Null);
                fit.Invoke(view, new object[] { isBuilding });

                var groundOffset = (float)(viewType.GetField(
                        "_importedVerticalGroundOffset",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) ?? 0f);
                if (!isBuilding)
                {
                    host.transform.position += Vector3.up * groundOffset;
                }

                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Assert.That(
                    Mathf.Max(bounds.size.x, bounds.size.z),
                    Is.EqualTo(expectedHorizontalSize).Within(0.01f),
                    prefabPath);
                Assert.That(
                    bounds.min.y - host.transform.position.y,
                    isBuilding
                        ? Is.EqualTo(0f).Within(0.01f)
                        : Is.EqualTo(-groundOffset).Within(0.01f),
                    prefabPath + " should preserve its expected transform-relative bottom.");
                Assert.That(
                    bounds.center.x - host.transform.position.x,
                    Is.EqualTo(0f).Within(0.01f),
                    prefabPath + " should be centered on the cell X coordinate.");
                Assert.That(
                    bounds.center.z - host.transform.position.z,
                    Is.EqualTo(0f).Within(0.01f),
                    prefabPath + " should be centered on the cell Z coordinate.");
                if (!isBuilding)
                {
                    Assert.That(
                        bounds.center.y - host.transform.position.y,
                        Is.EqualTo(0f).Within(0.01f),
                        prefabPath + " geometry center should match its Transform center.");
                    Assert.That(
                        bounds.min.y,
                        Is.EqualTo(0f).Within(0.01f),
                        prefabPath + " should still rest on the battlefield plane.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
