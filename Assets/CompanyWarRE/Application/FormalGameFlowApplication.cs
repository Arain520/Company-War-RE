using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Domain;
using QFramework;

namespace CompanyWarRE.Application
{
    public sealed class FormalGameFlowSnapshot
    {
        public FormalGameFlowSnapshot(FormalCampaignProgression progression)
        {
            Screen = progression.Screen;
            ActiveLevelId = progression.ActiveLevelId;
            LastResult = progression.LastResult;
            NextLevelId = progression.GetNextLevelId();
            LevelOrder = progression.LevelOrder.ToArray();
            UnlockedLevels = progression.UnlockedLevels.ToArray();
            CompletedLevels = progression.CompletedLevels.ToArray();
        }

        public FormalFlowScreen Screen { get; }
        public string ActiveLevelId { get; }
        public string NextLevelId { get; }
        public BattleState LastResult { get; }
        public IReadOnlyList<string> LevelOrder { get; }
        public IReadOnlyList<string> UnlockedLevels { get; }
        public IReadOnlyList<string> CompletedLevels { get; }

        public bool IsUnlocked(string levelId)
        {
            return UnlockedLevels.Any(id => string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsCompleted(string levelId)
        {
            return CompletedLevels.Any(id => string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class InitializeFormalGameFlowCommand : AbstractCommand
    {
        private readonly IReadOnlyList<string> _levelIds;

        public InitializeFormalGameFlowCommand(params string[] levelIds)
        {
            _levelIds = levelIds ?? Array.Empty<string>();
        }

        protected override void OnExecute()
        {
            this.GetModel<FormalGameFlowModel>().Initialize(_levelIds);
        }
    }

    public sealed class RestoreFormalGameFlowCommand : AbstractCommand
    {
        private readonly SaveGame _save;
        private readonly IReadOnlyList<string> _levelIds;

        public RestoreFormalGameFlowCommand(SaveGame save, params string[] levelIds)
        {
            _save = save;
            _levelIds = levelIds ?? Array.Empty<string>();
        }

        protected override void OnExecute()
        {
            var model = this.GetModel<FormalGameFlowModel>();
            model.Initialize(_levelIds);
            model.Progression.Restore(_save?.Campaign);
        }
    }

    public sealed class SetFormalFlowScreenCommand : AbstractCommand
    {
        private readonly FormalFlowScreen _screen;

        public SetFormalFlowScreenCommand(FormalFlowScreen screen)
        {
            _screen = screen;
        }

        protected override void OnExecute()
        {
            var model = this.GetModel<FormalGameFlowModel>();
            if (_screen == FormalFlowScreen.MainMenu)
            {
                model.Progression.ReturnToMainMenu();
            }
            else if (_screen == FormalFlowScreen.LevelSelect)
            {
                model.Progression.OpenLevelSelect();
            }
        }
    }

    public sealed class StartFormalLevelCommand : AbstractCommand<bool>
    {
        private readonly string _levelId;

        public StartFormalLevelCommand(string levelId)
        {
            _levelId = levelId;
        }

        protected override bool OnExecute()
        {
            return this.GetModel<FormalGameFlowModel>().Progression.TryStartLevel(_levelId);
        }
    }

    public sealed class ToggleFormalPauseCommand : AbstractCommand<bool>
    {
        protected override bool OnExecute()
        {
            return this.GetModel<FormalGameFlowModel>().Progression.TogglePause();
        }
    }

    public sealed class RecordFormalBattleResultCommand : AbstractCommand<bool>
    {
        private readonly string _levelId;
        private readonly BattleState _result;

        public RecordFormalBattleResultCommand(string levelId, BattleState result)
        {
            _levelId = levelId;
            _result = result;
        }

        protected override bool OnExecute()
        {
            return this.GetModel<FormalGameFlowModel>().Progression.RecordResult(_levelId, _result);
        }
    }

    public sealed class RestartFormalLevelCommand : AbstractCommand<bool>
    {
        protected override bool OnExecute()
        {
            return this.GetModel<FormalGameFlowModel>().Progression.RestartActiveLevel();
        }
    }

    public sealed class GetFormalGameFlowSnapshotQuery : AbstractQuery<FormalGameFlowSnapshot>
    {
        protected override FormalGameFlowSnapshot OnDo()
        {
            return this.GetModel<FormalGameFlowModel>().CreateSnapshot();
        }
    }

    public sealed class FormalGameFlowModel : AbstractModel
    {
        private FormalCampaignProgression _progression;

        public FormalCampaignProgression Progression =>
            _progression ?? throw new InvalidOperationException("Formal game flow has not been initialized.");

        protected override void OnInit()
        {
        }

        public void Initialize(IEnumerable<string> levelIds)
        {
            _progression = new FormalCampaignProgression(levelIds);
        }

        public FormalGameFlowSnapshot CreateSnapshot()
        {
            return new FormalGameFlowSnapshot(Progression);
        }
    }
}
