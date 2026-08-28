using System;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public readonly struct BattlePerformanceSnapshot
    {
        public BattlePerformanceSnapshot(float averageFrameMilliseconds, float worstFrameMilliseconds,
            long managedMemoryDeltaBytes, int cellViews, int activeCombatantViews, int pooledCombatantViews)
        {
            AverageFrameMilliseconds = averageFrameMilliseconds;
            WorstFrameMilliseconds = worstFrameMilliseconds;
            ManagedMemoryDeltaBytes = managedMemoryDeltaBytes;
            CellViews = cellViews;
            ActiveCombatantViews = activeCombatantViews;
            PooledCombatantViews = pooledCombatantViews;
        }

        public float AverageFrameMilliseconds { get; }
        public float WorstFrameMilliseconds { get; }
        public long ManagedMemoryDeltaBytes { get; }
        public int CellViews { get; }
        public int ActiveCombatantViews { get; }
        public int PooledCombatantViews { get; }
    }

    public sealed class BattleRuntimePerformanceMonitor : MonoBehaviour
    {
        public const int RecommendedMaximumCells = 900;
        public const int RecommendedMaximumCombatantViews = 256;
        public const float RecommendedMaximumAverageFrameMilliseconds = 33.34f;
        public const long RecommendedMaximumManagedGrowthPerSample = 256 * 1024;

        [SerializeField] private float sampleIntervalSeconds = 1f;
        [SerializeField] private bool logBudgetWarnings;

        private float _elapsed;
        private float _frameTotal;
        private float _worstFrame;
        private int _frames;
        private long _previousManagedMemory;
        private int _cellViews;
        private int _activeCombatantViews;
        private int _pooledCombatantViews;

        public BattlePerformanceSnapshot Latest { get; private set; }

        private void OnEnable()
        {
            _previousManagedMemory = GC.GetTotalMemory(false);
        }

        public void ReportWorld(int cellViews, int activeCombatantViews, int pooledCombatantViews)
        {
            _cellViews = Mathf.Max(0, cellViews);
            _activeCombatantViews = Mathf.Max(0, activeCombatantViews);
            _pooledCombatantViews = Mathf.Max(0, pooledCombatantViews);
        }

        private void Update()
        {
            var frame = Time.unscaledDeltaTime;
            _elapsed += frame;
            _frameTotal += frame;
            _worstFrame = Mathf.Max(_worstFrame, frame);
            _frames++;
            if (_elapsed < Mathf.Max(0.25f, sampleIntervalSeconds))
            {
                return;
            }

            var memory = GC.GetTotalMemory(false);
            Latest = new BattlePerformanceSnapshot(
                _frames == 0 ? 0f : _frameTotal * 1000f / _frames,
                _worstFrame * 1000f,
                memory - _previousManagedMemory,
                _cellViews,
                _activeCombatantViews,
                _pooledCombatantViews);
            _previousManagedMemory = memory;
            _elapsed = 0f;
            _frameTotal = 0f;
            _worstFrame = 0f;
            _frames = 0;

            if (logBudgetWarnings && ExceedsRecommendedBudget(Latest))
            {
                Debug.LogWarning(
                    $"Battle performance budget exceeded: avg={Latest.AverageFrameMilliseconds:0.0}ms, " +
                    $"worst={Latest.WorstFrameMilliseconds:0.0}ms, managedDelta={Latest.ManagedMemoryDeltaBytes}, " +
                    $"cells={Latest.CellViews}, combatants={Latest.ActiveCombatantViews}",
                    this);
            }
        }

        public static bool ExceedsRecommendedBudget(BattlePerformanceSnapshot snapshot)
        {
            return snapshot.AverageFrameMilliseconds > RecommendedMaximumAverageFrameMilliseconds ||
                   snapshot.ManagedMemoryDeltaBytes > RecommendedMaximumManagedGrowthPerSample ||
                   snapshot.CellViews > RecommendedMaximumCells ||
                   snapshot.ActiveCombatantViews > RecommendedMaximumCombatantViews;
        }
    }
}
