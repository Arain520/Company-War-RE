using NUnit.Framework;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class BattleDeploymentGhostTests
    {
        [TestCase("U01", "Assets/CompanyWarRE/Content/Prefabs/Units/PF_U01.prefab", false)]
        [TestCase("U08", "Assets/CompanyWarRE/Resources/CowLegacy/_Game/Art/Prefabs/Buildings/PF_U08.prefab", true)]
        public void Ghost_UsesPrefabChildMeshesWhenRootHasNoMeshFilter(string unitId, string path, bool building)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshFilter>() == null, Is.True);
            var root = new GameObject("PrefabGhostTest");
            try
            {
                var catalog = root.AddComponent<BattleSliceVisualCatalog>();
                var serialized = new SerializedObject(catalog);
                var mappings = serialized.FindProperty("unitMappings");
                mappings.arraySize = 1;
                var item = mappings.GetArrayElementAtIndex(0);
                item.FindPropertyRelative("unitId").stringValue = unitId;
                item.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var ghostRoot = new GameObject("Ghost");
                ghostRoot.transform.SetParent(root.transform, false);
                var ghost = ghostRoot.AddComponent<BattleDeploymentGhostView>();

                Assert.DoesNotThrow(() => ghost.Initialize(catalog, unitId, building, 4f));
                Assert.DoesNotThrow(() => ghost.Show(new Vector3(5f, 128f, 7f), new Vector2(4f, 4f), true));
                var visual = ghost.transform.Find("Visual");
                Assert.That(visual.Find("FallbackVisual"), Is.Null, "Preview must use the actual prefab's child meshes.");
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(bounds.min.y, Is.EqualTo(128f).Within(0.01f));
                Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty);
                ghost.Hide();
                Assert.That(ghost.gameObject.activeSelf, Is.False);
                Assert.DoesNotThrow(() => ghost.Show(new Vector3(6f, 130f, 7f), new Vector2(4f, 4f), false));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Ghost_ClonesOnlyMeshesAndPreservesSourceMaterials()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var root = new GameObject("GhostTest");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Color.blue);
            try
            {
                source.AddComponent<Rigidbody>().isKinematic = true;
                source.AddComponent<AudioSource>();
                source.AddComponent<GhostBehaviourProbe>();
                source.GetComponent<Renderer>().sharedMaterial = material;
                var catalog = root.AddComponent<BattleSliceVisualCatalog>();
                var serialized = new SerializedObject(catalog);
                var mappings = serialized.FindProperty("unitMappings");
                mappings.arraySize = 1;
                var item = mappings.GetArrayElementAtIndex(0);
                item.FindPropertyRelative("unitId").stringValue = "U01";
                item.FindPropertyRelative("prefab").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var ghostRoot = new GameObject("Ghost");
                ghostRoot.transform.SetParent(root.transform, false);
                var ghost = ghostRoot.AddComponent<BattleDeploymentGhostView>();
                ghost.Initialize(catalog, "U01", false, 4f);
                ghost.Show(new Vector3(5f, 128f, 7f), new Vector2(4f, 4f), true);
                Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(ghost.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(ghost.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(ghost.GetComponentsInChildren<GhostBehaviourProbe>(true), Is.Empty);
                var renderer = ghost.transform.Find("Visual").GetComponentInChildren<MeshRenderer>();
                Assert.That(renderer.bounds.min.y, Is.EqualTo(128f).Within(0.001f));
                Assert.That(renderer.bounds.size.x, Is.EqualTo(3.8f).Within(0.001f));
                Assert.That(renderer.sharedMaterial.color.a, Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                var actorRoot = new GameObject("ActualCombatant");
                actorRoot.transform.SetParent(root.transform, false);
                var view = actorRoot.AddComponent<BattleSliceCombatantView>();
                view.Initialize("actual", catalog);
                view.ConfigureVisualScale(4f);
                var combat = new CombatSimulation(3, 3);
                combat.TryAddActor("actual", Team.Ally, new CombatantDefinition("U01", "Staff", 10, 1, 1, 1, 1), 1, 1);
                view.Render(new BattleSliceCombatantSnapshot(combat.CreateSnapshot()[0]), new Vector3(5f, 128f, 7f));
                var actualRenderer = actorRoot.transform.Find("ImportedVisual_U01").GetComponentInChildren<MeshRenderer>();
                Assert.That(actualRenderer.bounds.min.y, Is.EqualTo(renderer.bounds.min.y).Within(0.001f),
                    "Dropping a unit must preserve the preview's ground height at visual scale 4.");
                Assert.That(actualRenderer.bounds.size.x, Is.EqualTo(renderer.bounds.size.x).Within(0.001f));
                ghost.Show(new Vector3(5f, 130f, 7f), new Vector2(12f, 12f), false);
                Assert.That(renderer.sharedMaterial.color.r, Is.GreaterThan(renderer.sharedMaterial.color.b));
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
                var ghostMaterial = renderer.sharedMaterial;
                Object.DestroyImmediate(ghost.gameObject);
                Assert.That(ghostMaterial == null, Is.True, "Owned ghost materials must be released.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(material);
            }
        }
    }

    public sealed class GhostBehaviourProbe : MonoBehaviour { }
}
