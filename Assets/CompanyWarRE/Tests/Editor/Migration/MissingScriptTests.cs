using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class MissingScriptTests
    {
        private static readonly string[] ProjectOwnedAssetRoots =
        {
            "Assets/CompanyWarRE",
            "Assets/Scenes"
        };

        [Test]
        public void TargetPrefabs_HaveNoMissingScripts()
        {
            var failures = new List<string>();
            foreach (var path in FindProjectOwnedAssets("t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && CountMissingScripts(prefab) > 0)
                {
                    failures.Add(path);
                }
            }

            Assert.That(failures, Is.Empty, "Missing scripts in prefabs:\n" + string.Join("\n", failures));
        }

        [Test]
        public void TargetScenes_HaveNoMissingScripts()
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            var failures = new List<string>();
            try
            {
                foreach (var path in FindProjectOwnedScenes())
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var missingCount = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        missingCount += CountMissingScripts(root);
                    }

                    if (missingCount > 0)
                    {
                        failures.Add($"{path}: {missingCount}");
                    }
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            Assert.That(failures, Is.Empty, "Missing scripts in scenes:\n" + string.Join("\n", failures));
        }

        private static IEnumerable<string> FindProjectOwnedAssets(string filter)
        {
            var validRoots = ProjectOwnedAssetRoots.Where(AssetDatabase.IsValidFolder).ToArray();
            if (validRoots.Length == 0)
            {
                return Enumerable.Empty<string>();
            }

            return AssetDatabase.FindAssets(filter, validRoots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct()
                .OrderBy(path => path);
        }

        private static IEnumerable<string> FindProjectOwnedScenes()
        {
            var scenePaths = new HashSet<string>(FindProjectOwnedAssets("t:Scene"));
            foreach (var buildScene in EditorBuildSettings.scenes)
            {
                if (!string.IsNullOrEmpty(buildScene.path))
                {
                    scenePaths.Add(buildScene.path);
                }
            }

            return scenePaths.OrderBy(path => path);
        }

        private static int CountMissingScripts(GameObject root)
        {
            var count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }

            return count;
        }
    }
}
