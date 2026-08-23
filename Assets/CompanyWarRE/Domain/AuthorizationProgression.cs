using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public enum AuthorizationState
    {
        None,
        Available,
        Choosing,
        WaitingForNextRequirement
    }

    public sealed class AuthorizationItemDefinition
    {
        public AuthorizationItemDefinition(string id, int weight = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An authorization item id is required.", nameof(id));
            }

            Id = id;
            Weight = Math.Max(0, weight);
        }

        public string Id { get; }
        public int Weight { get; }
    }

    public sealed class AuthorizationStageDefinition
    {
        public AuthorizationStageDefinition(
            string name,
            int requiredPoints,
            IEnumerable<AuthorizationItemDefinition> items)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            RequiredPoints = Math.Max(1, requiredPoints);
            Items = (items ?? Enumerable.Empty<AuthorizationItemDefinition>())
                .Where(item => item != null)
                .ToList();
        }

        public string Name { get; }
        public int RequiredPoints { get; }
        public IReadOnlyList<AuthorizationItemDefinition> Items { get; }
    }

    public interface IAuthorizationCandidateSelector
    {
        IReadOnlyList<string> Select(
            IReadOnlyList<AuthorizationItemDefinition> availableItems,
            int maximumCount);
    }

    public sealed class FirstAvailableAuthorizationCandidateSelector : IAuthorizationCandidateSelector
    {
        public IReadOnlyList<string> Select(
            IReadOnlyList<AuthorizationItemDefinition> availableItems,
            int maximumCount)
        {
            if (availableItems == null || maximumCount <= 0)
            {
                return Array.Empty<string>();
            }

            return availableItems
                .Where(item => item != null)
                .Take(maximumCount)
                .Select(item => item.Id)
                .ToList();
        }
    }

    public sealed class AuthorizationProgression
    {
        public const int MaximumDeployListSize = 12;

        private readonly List<AuthorizationStageDefinition> _stages = new List<AuthorizationStageDefinition>();
        private readonly List<string> _deployList = new List<string>();
        private readonly List<string> _candidates = new List<string>();
        private IAuthorizationCandidateSelector _selector;
        private int _stageIndex;
        private int _currentStageRequestIndex;
        private int _nextRequirement = int.MaxValue;

        public int Points { get; private set; }
        public int StageIndex => _stageIndex;
        public int CurrentStageRequestIndex => _currentStageRequestIndex;
        public int NextRequirement => _nextRequirement;
        public AuthorizationState State { get; private set; } = AuthorizationState.None;
        public IReadOnlyList<string> DeployList => _deployList;
        public IReadOnlyList<string> Candidates => _candidates;

        public void Configure(
            IEnumerable<AuthorizationStageDefinition> stages,
            int initialPoints,
            IEnumerable<string> initialDeployments,
            IAuthorizationCandidateSelector selector)
        {
            _stages.Clear();
            _stages.AddRange((stages ?? Enumerable.Empty<AuthorizationStageDefinition>())
                .Where(stage => stage != null && stage.Items.Count > 0));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _stageIndex = 0;
            _currentStageRequestIndex = 0;
            Points = Math.Max(0, initialPoints);
            _deployList.Clear();
            _candidates.Clear();

            foreach (var id in initialDeployments ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id) || ContainsId(_deployList, id))
                {
                    continue;
                }

                if (_deployList.Count >= MaximumDeployListSize)
                {
                    break;
                }

                _deployList.Add(id);
            }

            _nextRequirement = _stages.Count > 0 ? _stages[0].RequiredPoints : int.MaxValue;
            RefreshState();
        }

        public void GainPoints(int amount)
        {
            Points += Math.Max(0, amount);
            RefreshState();
        }

        public bool BeginChoice()
        {
            if (_stageIndex >= _stages.Count || State != AuthorizationState.Available)
            {
                return false;
            }

            var available = _stages[_stageIndex].Items
                .Where(item => !ContainsId(_deployList, item.Id))
                .ToList();
            var selected = _selector.Select(available, GetCandidateCountForStage(_stageIndex));
            _candidates.Clear();
            foreach (var id in selected ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(id) &&
                    available.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) &&
                    !ContainsId(_candidates, id))
                {
                    _candidates.Add(id);
                }
            }

            if (_candidates.Count == 0)
            {
                RefreshState();
                return false;
            }

            State = AuthorizationState.Choosing;
            return true;
        }

        public bool Accept(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || State != AuthorizationState.Choosing ||
                !ContainsId(_candidates, id) || ContainsId(_deployList, id) ||
                _deployList.Count >= MaximumDeployListSize)
            {
                return false;
            }

            _deployList.Add(id);
            _candidates.Clear();
            _currentStageRequestIndex++;

            var completedRequirement = _nextRequirement;
            _nextRequirement += _stages[_stageIndex].RequiredPoints;
            var advanced = ProgressCompletedStages();
            if (advanced && _stageIndex < _stages.Count)
            {
                _nextRequirement = Math.Max(
                    _nextRequirement,
                    completedRequirement + _stages[_stageIndex].RequiredPoints);
            }

            RefreshState();
            return true;
        }

        public bool CancelChoice()
        {
            if (State != AuthorizationState.Choosing)
            {
                return false;
            }

            _candidates.Clear();
            State = AuthorizationState.Available;
            return true;
        }

        private void RefreshState()
        {
            if (ProgressCompletedStages())
            {
                RefreshState();
                return;
            }

            if (_stageIndex >= _stages.Count)
            {
                _nextRequirement = int.MaxValue;
                _candidates.Clear();
                State = AuthorizationState.None;
                return;
            }

            if (State == AuthorizationState.Choosing && _candidates.Count > 0)
            {
                return;
            }

            _candidates.Clear();
            State = Points >= _nextRequirement
                ? AuthorizationState.Available
                : AuthorizationState.WaitingForNextRequirement;
        }

        private bool ProgressCompletedStages()
        {
            var advanced = false;
            while (_stageIndex < _stages.Count &&
                   _stages[_stageIndex].Items.All(item => ContainsId(_deployList, item.Id)))
            {
                _stageIndex++;
                _currentStageRequestIndex = 0;
                _candidates.Clear();
                advanced = true;
            }

            if (_stageIndex >= _stages.Count)
            {
                _nextRequirement = int.MaxValue;
            }
            else if (advanced)
            {
                _nextRequirement = Math.Max(_nextRequirement, _stages[_stageIndex].RequiredPoints);
            }

            return advanced;
        }

        private static bool ContainsId(IEnumerable<string> ids, string id)
        {
            return ids.Any(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetCandidateCountForStage(int stageIndex)
        {
            if (stageIndex <= 0)
            {
                return 4;
            }

            return stageIndex == 1 ? 3 : 2;
        }
    }
}
