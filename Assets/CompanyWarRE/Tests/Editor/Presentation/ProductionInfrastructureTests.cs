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
    }
}
