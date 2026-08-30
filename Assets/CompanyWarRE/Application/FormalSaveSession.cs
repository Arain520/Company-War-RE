using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Domain;

namespace CompanyWarRE.Application
{
    public enum FormalSaveSessionState
    {
        Uninitialized,
        Ready,
        ReadOnlyFailure
    }

    public sealed class FormalSaveSession
    {
        private readonly SaveGameUseCases _useCases;
        private readonly string _savePath;
        private readonly string _backupDirectory;

        public FormalSaveSession(
            SaveGameUseCases useCases,
            string savePath,
            string backupDirectory)
        {
            _useCases = useCases ?? throw new ArgumentNullException(nameof(useCases));
            _savePath = savePath ?? throw new ArgumentNullException(nameof(savePath));
            _backupDirectory = backupDirectory ?? throw new ArgumentNullException(nameof(backupDirectory));
        }

        public FormalSaveSessionState State { get; private set; }
        public SaveGame Current { get; private set; }
        public SaveOperationResult<SaveGame> LastResult { get; private set; }
        public string SavePath => _savePath;

        public SaveOperationResult<SaveGame> Start(FormalGameFlowSnapshot initialFlow)
        {
            if (initialFlow == null)
            {
                throw new ArgumentNullException(nameof(initialFlow));
            }

            var result = _useCases.LoadOrImport(_savePath, _backupDirectory);
            if (result.Succeeded)
            {
                Accept(result);
                if (!ContainsPerBattleGrowth(result.Value))
                {
                    return result;
                }

                // Cow treats authorization choices as level-local battle state. Older
                // Company War-RE saves wrote that transient state into Growth; remove it
                // once, while preserving campaign, economy and settings compatibility.
                var sanitized = new SaveGame(
                    result.Value.Campaign,
                    GrowthSaveProgress.Default,
                    result.Value.Economy,
                    result.Value.Settings);
                var sanitizedResult = _useCases.Save(_savePath, _backupDirectory, sanitized);
                return sanitizedResult.Succeeded ? Accept(sanitizedResult) : Reject(sanitizedResult);
            }

            if (result.Error != SaveOperationError.NotFound &&
                result.Error != SaveOperationError.LegacySourceUnavailable)
            {
                State = FormalSaveSessionState.ReadOnlyFailure;
                LastResult = result;
                return result;
            }

            var fresh = Capture(initialFlow, null, GameSettingsSave.Default);
            result = _useCases.Save(_savePath, _backupDirectory, fresh);
            return result.Succeeded ? Accept(result) : Reject(result);
        }

        public SaveOperationResult<SaveGame> SaveCampaign(FormalGameFlowSnapshot flow)
        {
            return SaveCaptured(Capture(flow, null, Current?.Settings ?? GameSettingsSave.Default));
        }

        public SaveOperationResult<SaveGame> SaveBattleResult(
            FormalGameFlowSnapshot flow,
            BattleSliceSnapshot battle)
        {
            if (battle == null)
            {
                throw new ArgumentNullException(nameof(battle));
            }

            return SaveCaptured(Capture(flow, battle, Current?.Settings ?? GameSettingsSave.Default));
        }

        public SaveOperationResult<SaveGame> SaveAudioSetting(string layer, double volume, bool muted)
        {
            if (string.IsNullOrWhiteSpace(layer))
            {
                return RememberFailure(SaveOperationError.ValidationFailed, "Audio layer is required.");
            }

            var normalized = GameSettingsSave.CowAudioLayers.FirstOrDefault(item =>
                string.Equals(item, layer.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalized == null)
            {
                return RememberFailure(SaveOperationError.ValidationFailed, "Unknown audio layer: " + layer);
            }

            var source = Current?.Settings ?? GameSettingsSave.Default;
            var layers = GameSettingsSave.CowAudioLayers.Select(item =>
            {
                var existing = source.FindAudioLayer(item) ?? new AudioLayerSaveSettings(item, 1d, false);
                return string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)
                    ? new AudioLayerSaveSettings(item, volume, muted)
                    : existing;
            });
            var value = new SaveGame(
                Current?.Campaign,
                GrowthSaveProgress.Default,
                Current?.Economy ?? EconomySaveProgress.Default,
                new GameSettingsSave(layers));
            return SaveCaptured(value);
        }

        public BattleSliceConfiguration ApplyGrowth(BattleSliceConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            // Kept as an API compatibility seam. Authorization points and deployment
            // choices must be recreated from the level document for every level start.
            return configuration;
        }

        private SaveGame Capture(
            FormalGameFlowSnapshot flow,
            BattleSliceSnapshot battle,
            GameSettingsSave settings)
        {
            if (flow == null)
            {
                throw new ArgumentNullException(nameof(flow));
            }

            var previous = Current?.Campaign;
            var levels = flow.LevelOrder.Select(levelId =>
            {
                var old = previous?.FindLevel(levelId);
                var completed = flow.IsCompleted(levelId);
                var stars = completed ? Math.Max(1, old?.Stars ?? 0) : old?.Stars ?? 0;
                return new LevelSaveProgress(levelId, flow.IsUnlocked(levelId), completed, stars);
            });
            var growth = GrowthSaveProgress.Default;
            var economy = battle == null
                ? Current?.Economy ?? EconomySaveProgress.Default
                : new EconomySaveProgress(battle.Resources, battle.AssaultScore);
            return new SaveGame(
                new CampaignSaveProgress(flow.ActiveLevelId, levels),
                growth,
                economy,
                settings ?? GameSettingsSave.Default);
        }

        private static bool ContainsPerBattleGrowth(SaveGame save)
        {
            return save?.Growth != null &&
                   (save.Growth.AuthorizationPoints != 0 || save.Growth.DeploymentUnitIds.Count != 0);
        }

        private SaveOperationResult<SaveGame> SaveCaptured(SaveGame value)
        {
            if (State != FormalSaveSessionState.Ready)
            {
                return LastResult ?? RememberFailure(
                    SaveOperationError.WriteFailed,
                    "Formal save session is not writable.");
            }

            var result = _useCases.Save(_savePath, _backupDirectory, value);
            return result.Succeeded ? Accept(result) : Reject(result);
        }

        private SaveOperationResult<SaveGame> Accept(SaveOperationResult<SaveGame> result)
        {
            Current = result.Value;
            LastResult = result;
            State = FormalSaveSessionState.Ready;
            return result;
        }

        private SaveOperationResult<SaveGame> Reject(SaveOperationResult<SaveGame> result)
        {
            LastResult = result;
            if (result.Error != SaveOperationError.ValidationFailed)
            {
                State = FormalSaveSessionState.ReadOnlyFailure;
            }
            return result;
        }

        private SaveOperationResult<SaveGame> RememberFailure(SaveOperationError error, string message)
        {
            LastResult = SaveOperationResult<SaveGame>.Failure(error, message);
            return LastResult;
        }
    }
}
