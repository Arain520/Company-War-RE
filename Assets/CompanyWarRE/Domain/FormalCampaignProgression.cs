using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public enum FormalFlowScreen
    {
        MainMenu,
        LevelSelect,
        Battle,
        Paused,
        Result
    }

    public sealed class FormalCampaignProgression
    {
        private readonly List<string> _levelOrder;
        private readonly HashSet<string> _unlocked =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _completed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FormalCampaignProgression(IEnumerable<string> levelOrder)
        {
            _levelOrder = (levelOrder ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (_levelOrder.Count == 0)
            {
                throw new ArgumentException("At least one formal level id is required.", nameof(levelOrder));
            }

            _unlocked.Add(_levelOrder[0]);
            Screen = FormalFlowScreen.MainMenu;
            ActiveLevelId = _levelOrder[0];
        }

        public FormalFlowScreen Screen { get; private set; }
        public string ActiveLevelId { get; private set; }
        public BattleState LastResult { get; private set; } = BattleState.Running;
        public IReadOnlyList<string> LevelOrder => _levelOrder;
        public IReadOnlyCollection<string> UnlockedLevels => _unlocked;
        public IReadOnlyCollection<string> CompletedLevels => _completed;

        public bool IsUnlocked(string levelId)
        {
            return !string.IsNullOrWhiteSpace(levelId) && _unlocked.Contains(levelId);
        }

        public bool IsCompleted(string levelId)
        {
            return !string.IsNullOrWhiteSpace(levelId) && _completed.Contains(levelId);
        }

        public void Restore(CampaignSaveProgress saved)
        {
            if (saved == null)
            {
                return;
            }

            _unlocked.Clear();
            _completed.Clear();
            foreach (var levelId in _levelOrder)
            {
                var progress = saved.FindLevel(levelId);
                if (progress == null)
                {
                    continue;
                }

                if (progress.Unlocked)
                {
                    _unlocked.Add(levelId);
                }

                if (progress.Completed && progress.Unlocked)
                {
                    _completed.Add(levelId);
                }
            }

            _unlocked.Add(_levelOrder[0]);
            var restoredActive = NormalizeKnownLevel(saved.ActiveLevelId);
            ActiveLevelId = restoredActive != null && _unlocked.Contains(restoredActive)
                ? restoredActive
                : _levelOrder[0];
            LastResult = BattleState.Running;
            Screen = FormalFlowScreen.MainMenu;
        }

        public void OpenLevelSelect()
        {
            Screen = FormalFlowScreen.LevelSelect;
        }

        public void ReturnToMainMenu()
        {
            Screen = FormalFlowScreen.MainMenu;
        }

        public bool TryStartLevel(string levelId)
        {
            var normalized = NormalizeKnownLevel(levelId);
            if (normalized == null || !_unlocked.Contains(normalized))
            {
                return false;
            }

            ActiveLevelId = normalized;
            LastResult = BattleState.Running;
            Screen = FormalFlowScreen.Battle;
            return true;
        }

        public bool TogglePause()
        {
            if (Screen == FormalFlowScreen.Battle)
            {
                Screen = FormalFlowScreen.Paused;
                return true;
            }

            if (Screen == FormalFlowScreen.Paused)
            {
                Screen = FormalFlowScreen.Battle;
                return true;
            }

            return false;
        }

        public bool RecordResult(string levelId, BattleState result)
        {
            var normalized = NormalizeKnownLevel(levelId);
            if (normalized == null ||
                !string.Equals(normalized, ActiveLevelId, StringComparison.OrdinalIgnoreCase) ||
                result == BattleState.Running)
            {
                return false;
            }

            LastResult = result;
            if (result == BattleState.Victory)
            {
                _completed.Add(normalized);
                var index = _levelOrder.FindIndex(id =>
                    string.Equals(id, normalized, StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && index + 1 < _levelOrder.Count)
                {
                    _unlocked.Add(_levelOrder[index + 1]);
                }
            }

            Screen = FormalFlowScreen.Result;
            return true;
        }

        public bool RestartActiveLevel()
        {
            if (string.IsNullOrWhiteSpace(ActiveLevelId))
            {
                return false;
            }

            LastResult = BattleState.Running;
            Screen = FormalFlowScreen.Battle;
            return true;
        }

        public string GetNextLevelId()
        {
            var index = _levelOrder.FindIndex(id =>
                string.Equals(id, ActiveLevelId, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= _levelOrder.Count)
            {
                return string.Empty;
            }

            var next = _levelOrder[index + 1];
            return _unlocked.Contains(next) ? next : string.Empty;
        }

        private string NormalizeKnownLevel(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
            {
                return null;
            }

            return _levelOrder.FirstOrDefault(id =>
                string.Equals(id, levelId.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
