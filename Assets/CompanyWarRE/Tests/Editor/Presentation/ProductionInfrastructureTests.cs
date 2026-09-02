using NUnit.Framework;
using UnityEngine;

namespace CompanyWarRE.Presentation.Tests
{
    public sealed class PoolTestComponent : MonoBehaviour
    {
    }

    public sealed class ProductionInfrastructureTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ProductionInfrastructureTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ComponentPool_ReusesReturnedViewsAndReportsCounts()
        {
            var pool = new ProductionComponentPool<PoolTestComponent>(
                () => new GameObject("Pooled").AddComponent<PoolTestComponent>(),
                _root.transform,
                2);

            var first = pool.Rent(_root.transform);
            pool.Return(first);
            var second = pool.Rent(_root.transform);

            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.Zero);
            pool.Return(second);
            pool.Dispose();
        }

        [Test]
        public void AssetKey_UsesResKitOnlyWhenBundleOwnershipIsExplicit()
        {
            var builtIn = new ProductionAssetKey("Menu", resourcesPath: "CowLegacy/Menu");
            var updateable = new ProductionAssetKey("FormalLevel.L06", "formal-levels");

            Assert.That(builtIn.UsesResKit, Is.False);
            Assert.That(updateable.UsesResKit, Is.True);
            Assert.That(updateable.BundleName, Is.EqualTo("formal-levels"));
        }

        [Test]
        public void PerformanceBudget_AcceptsCurrentFormalGridAndRejectsOversizedWorld()
        {
            var current = new BattlePerformanceSnapshot(16f, 25f, 32 * 1024, 18 * 30, 80, 24);
            var oversized = new BattlePerformanceSnapshot(40f, 80f, 512 * 1024, 36 * 30, 300, 0);

            Assert.That(BattleRuntimePerformanceMonitor.ExceedsRecommendedBudget(current), Is.False);
            Assert.That(BattleRuntimePerformanceMonitor.ExceedsRecommendedBudget(oversized), Is.True);
        }

        [Test]
        public void FormalMap_PreparesEnvironmentWithoutAffectingBattleCollision()
        {
            var map = _root.AddComponent<FormalBattleMapView>();
            var environment = new GameObject("EnvironmentRoot").transform;
            environment.SetParent(_root.transform, false);
            var prop = new GameObject("DecorativeBuilding");
            prop.transform.SetParent(environment, false);
            var collider = prop.AddComponent<BoxCollider>();
            var anchor = new GameObject("BattleBoardAnchor").transform;
            anchor.SetParent(_root.transform, false);
            anchor.localPosition = new Vector3(12f, 0f, -7f);
            anchor.localRotation = Quaternion.Euler(0f, 37f, 0f);
            anchor.localScale = Vector3.one * 1.5f;
            map.Configure("BASE", environment, anchor, 18, 30);

            map.PrepareForBattle(24, 45);

            Assert.That(map.PreviewColumns, Is.EqualTo(24));
            Assert.That(map.PreviewRows, Is.EqualTo(45));
            Assert.That(map.BattleBoardAnchor, Is.SameAs(anchor));
            Assert.That(map.HasUniformAnchorScale(), Is.True);
            Assert.That(collider.enabled, Is.False);
            Assert.That(environment.gameObject.layer, Is.EqualTo(FormalBattleMapView.EnvironmentLayer));
            Assert.That(prop.layer, Is.EqualTo(FormalBattleMapView.EnvironmentLayer));
        }

        [Test]
        public void FormalMap_RejectsNonUniformAnchorScale()
        {
            var map = _root.AddComponent<FormalBattleMapView>();
            var environment = new GameObject("EnvironmentRoot").transform;
            environment.SetParent(_root.transform, false);
            var anchor = new GameObject("BattleBoardAnchor").transform;
            anchor.SetParent(_root.transform, false);
            anchor.localScale = new Vector3(1f, 1.2f, 1f);
            map.Configure("BASE", environment, anchor, 18, 30);

            Assert.That(map.HasUniformAnchorScale(), Is.False);
        }
    }
}
