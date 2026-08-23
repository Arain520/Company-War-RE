using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CompanyWarRE.Migration.Tests
{
    public sealed class MissingScriptTests
    {
        [Test]
        public void TargetPrefabs_HaveNoMissingScripts()
        {
            var failures = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
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
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
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
