using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanyWarRE.Domain
{
    public static class SaveSchema
    {
        public const int CowCompatibleVersion = 1;
        public const int GrowthVersion = 2;
        public const int CurrentVersion = 3;
    }

    public enum SaveIssueSeverity
    {
        Warning,
        Error
    }

    public enum SaveIssueCode
    {
        UnsupportedVersion,
        MissingSectionDefaulted,
        VersionUpgraded,
        MissingLevel,
        DuplicateLevel,
        InvalidActiveLevel,
        InvalidStars,
        CompletedLevelLocked,
        InvalidAuthorizationPoints,
        InvalidDeploymentId,
        InvalidResources,
        InvalidScore,
        InvalidAudioLayer,
        InvalidVolume
    }

    public sealed class SaveIssue
    {
        public SaveIssue(SaveIssueCode code, SaveIssueSeverity severity, string path, string message)
        {
            Code = code;
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public SaveIssueCode Code { get; }
        public SaveIssueSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class SaveValidationReport
    {
        private readonly List<SaveIssue> _issues = new List<SaveIssue>();

        public IReadOnlyList<SaveIssue> Issues => _issues;
        public bool IsValid => _issues.All(issue => issue.Severity != SaveIssueSeverity.Error);

        public void Add(SaveIssue issue)
        {
            if (issue != null)
            {
                _issues.Add(issue);
            }
        }

        public void AddRange(IEnumerable<SaveIssue> issues)
        {
            foreach (var issue in issues ?? Array.Empty<SaveIssue>())
            {
                Add(issue);
            }
        }
    }

    public sealed class LevelSaveProgress
    {
        public LevelSaveProgress(string levelId, bool unlocked, bool completed, int stars)
        {
            LevelId = levelId;
            Unlocked = unlocked;
            Completed = completed;
            Stars = stars;
        }

        public string LevelId { get; }
        public bool Unlocked { get; }
        public bool Completed { get; }
        public int Stars { get; }
    }

    public sealed class CampaignSaveProgress
    {
        public CampaignSaveProgress(string activeLevelId, IEnumerable<LevelSaveProgress> levels)
        {
            ActiveLevelId = activeLevelId;
            Levels = (levels ?? Array.Empty<LevelSaveProgress>()).Where(level => level != null).ToArray();
        }

        public string ActiveLevelId { get; }
        public IReadOnlyList<LevelSaveProgress> Levels { get; }

        public LevelSaveProgress FindLevel(string levelId)
        {
            return Levels.FirstOrDefault(level =>
                string.Equals(level.LevelId, levelId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class GrowthSaveProgress
    {
        public GrowthSaveProgress(int authorizationPoints, IEnumerable<string> deploymentUnitIds)
        {
            AuthorizationPoints = authorizationPoints;
            DeploymentUnitIds = (deploymentUnitIds ?? Array.Empty<string>()).ToArray();
        }

        public int AuthorizationPoints { get; }
        public IReadOnlyList<string> DeploymentUnitIds { get; }

        public static GrowthSaveProgress Default => new GrowthSaveProgress(0, Array.Empty<string>());
    }

    public sealed class EconomySaveProgress
    {
        public EconomySaveProgress(int resources, int score)
        {
            Resources = resources;
            Score = score;
        }

        public int Resources { get; }
        public int Score { get; }

        public static EconomySaveProgress Default => new EconomySaveProgress(0, 0);
    }

    public sealed class AudioLayerSaveSettings
    {
        public AudioLayerSaveSettings(string layer, double volume, bool muted)
        {
            Layer = layer;
            Volume = volume;
            Muted = muted;
        }

        public string Layer { get; }
        public double Volume { get; }
        public bool Muted { get; }
    }

    public sealed class GameSettingsSave
    {
        public static readonly string[] CowAudioLayers = { "Master", "BGM", "SFX", "UI", "Voice" };

        public GameSettingsSave(IEnumerable<AudioLayerSaveSettings> audioLayers)
        {
            AudioLayers = (audioLayers ?? Array.Empty<AudioLayerSaveSettings>())
                .Where(layer => layer != null)
                .ToArray();
        }

        public IReadOnlyList<AudioLayerSaveSettings> AudioLayers { get; }

        public AudioLayerSaveSettings FindAudioLayer(string layer)
        {
            return AudioLayers.FirstOrDefault(item =>
                string.Equals(item.Layer, layer, StringComparison.OrdinalIgnoreCase));
        }

        public static GameSettingsSave Default => new GameSettingsSave(
            CowAudioLayers.Select(layer => new AudioLayerSaveSettings(layer, 1d, false)));
    }

    public sealed class SaveGame
    {
        public SaveGame(
            CampaignSaveProgress campaign,
            GrowthSaveProgress growth,
            EconomySaveProgress economy,
            GameSettingsSave settings)
        {
            Campaign = campaign;
            Growth = growth;
            Economy = economy;
            Settings = settings;
        }

        public int Version => SaveSchema.CurrentVersion;
        public CampaignSaveProgress Campaign { get; }
        public GrowthSaveProgress Growth { get; }
        public EconomySaveProgress Economy { get; }
        public GameSettingsSave Settings { get; }

        public SaveValidationReport Validate()
        {
            var report = new SaveValidationReport();
            ValidateCampaign(report);
            ValidateGrowth(report);
            ValidateEconomy(report);
            ValidateSettings(report);
            return report;
        }

        private void ValidateCampaign(SaveValidationReport report)
        {
            if (Campaign == null || Campaign.Levels.Count == 0)
            {
                report.Add(new SaveIssue(
                    SaveIssueCode.MissingLevel,
                    SaveIssueSeverity.Error,
                    "Campaign.Levels",
                    "At least one level is required."));
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var level in Campaign.Levels)
            {
                var path = "Campaign.Levels";
                if (string.IsNullOrWhiteSpace(level.LevelId))
                {
                    report.Add(new SaveIssue(SaveIssueCode.MissingLevel, SaveIssueSeverity.Error, path, "Level id is required."));
                    continue;
                }

                if (!seen.Add(level.LevelId))
                {
                    report.Add(new SaveIssue(SaveIssueCode.DuplicateLevel, SaveIssueSeverity.Error, path, level.LevelId));
                }

                if (level.Stars < 0 || level.Stars > 3)
                {
                    report.Add(new SaveIssue(SaveIssueCode.InvalidStars, SaveIssueSeverity.Error, path + ".Stars", level.LevelId));
                }

                if (level.Completed && !level.Unlocked)
                {
                    report.Add(new SaveIssue(
                        SaveIssueCode.CompletedLevelLocked,
                        SaveIssueSeverity.Error,
                        path,
                        level.LevelId));
                }
            }

            var active = Campaign.FindLevel(Campaign.ActiveLevelId);
            if (active == null || !active.Unlocked)
            {
                report.Add(new SaveIssue(
                    SaveIssueCode.InvalidActiveLevel,
                    SaveIssueSeverity.Error,
                    "Campaign.ActiveLevelId",
                    Campaign.ActiveLevelId));
            }
        }

        private void ValidateGrowth(SaveValidationReport report)
        {
            if (Growth == null || Growth.AuthorizationPoints < 0)
            {
                report.Add(new SaveIssue(
                    SaveIssueCode.InvalidAuthorizationPoints,
                    SaveIssueSeverity.Error,
                    "Growth.AuthorizationPoints",
                    "Authorization points cannot be negative."));
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in Growth.DeploymentUnitIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    report.Add(new SaveIssue(
                        SaveIssueCode.InvalidDeploymentId,
                        SaveIssueSeverity.Error,
                        "Growth.DeploymentUnitIds",
                        id ?? string.Empty));
                }
            }
        }

        private void ValidateEconomy(SaveValidationReport report)
        {
            if (Economy == null || Economy.Resources < 0)
            {
                report.Add(new SaveIssue(SaveIssueCode.InvalidResources, SaveIssueSeverity.Error, "Economy.Resources", "Resources cannot be negative."));
            }

            if (Economy == null || Economy.Score < 0)
            {
                report.Add(new SaveIssue(SaveIssueCode.InvalidScore, SaveIssueSeverity.Error, "Economy.Score", "Score cannot be negative."));
            }
        }

        private void ValidateSettings(SaveValidationReport report)
        {
            if (Settings == null)
            {
                report.Add(new SaveIssue(SaveIssueCode.InvalidAudioLayer, SaveIssueSeverity.Error, "Settings", "Settings are required."));
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in Settings.AudioLayers)
            {
                if (string.IsNullOrWhiteSpace(layer.Layer) || !seen.Add(layer.Layer))
                {
                    report.Add(new SaveIssue(SaveIssueCode.InvalidAudioLayer, SaveIssueSeverity.Error, "Settings.AudioLayers", layer.Layer));
                }

                if (double.IsNaN(layer.Volume) || double.IsInfinity(layer.Volume) || layer.Volume < 0d || layer.Volume > 1d)
                {
                    report.Add(new SaveIssue(SaveIssueCode.InvalidVolume, SaveIssueSeverity.Error, "Settings.AudioLayers.Volume", layer.Layer));
                }
            }
        }
    }

    public sealed class SaveGameDraft
    {
        public SaveGameDraft(
            int version,
            CampaignSaveProgress campaign,
            GrowthSaveProgress growth,
            EconomySaveProgress economy,
            GameSettingsSave settings)
        {
            Version = version;
            Campaign = campaign;
            Growth = growth;
            Economy = economy;
            Settings = settings;
        }

        public int Version { get; }
        public CampaignSaveProgress Campaign { get; }
        public GrowthSaveProgress Growth { get; }
        public EconomySaveProgress Economy { get; }
        public GameSettingsSave Settings { get; }
    }

    public sealed class SaveUpgradeResult
    {
        public SaveUpgradeResult(SaveGame value, SaveValidationReport report)
        {
            Value = value;
            Report = report ?? new SaveValidationReport();
        }

        public SaveGame Value { get; }
        public SaveValidationReport Report { get; }
        public bool Succeeded => Value != null && Report.IsValid;
    }

    public sealed class SaveGameUpgrader
    {
        public SaveUpgradeResult Upgrade(SaveGameDraft draft)
        {
            var report = new SaveValidationReport();
            if (draft == null || draft.Version < SaveSchema.CowCompatibleVersion || draft.Version > SaveSchema.CurrentVersion)
            {
                report.Add(new SaveIssue(
                    SaveIssueCode.UnsupportedVersion,
                    SaveIssueSeverity.Error,
                    "Version",
                    draft == null ? "Missing save document." : draft.Version.ToString()));
                return new SaveUpgradeResult(null, report);
            }

            var campaign = draft.Campaign ?? DefaultCampaign(report);
            var settings = draft.Settings ?? DefaultSettings(report);
            var growth = draft.Growth;
            var economy = draft.Economy;

            if (draft.Version < SaveSchema.GrowthVersion)
            {
                growth = GrowthSaveProgress.Default;
                AddUpgrade(report, SaveSchema.CowCompatibleVersion, SaveSchema.GrowthVersion);
            }
            else if (growth == null)
            {
                growth = GrowthSaveProgress.Default;
                AddMissing(report, "Growth");
            }

            if (draft.Version < SaveSchema.CurrentVersion)
            {
                economy = EconomySaveProgress.Default;
                AddUpgrade(report, SaveSchema.GrowthVersion, SaveSchema.CurrentVersion);
            }
            else if (economy == null)
            {
                economy = EconomySaveProgress.Default;
                AddMissing(report, "Economy");
            }

            var value = new SaveGame(campaign, growth, economy, settings);
            report.AddRange(value.Validate().Issues);
            return new SaveUpgradeResult(report.IsValid ? value : null, report);
        }

        private static CampaignSaveProgress DefaultCampaign(SaveValidationReport report)
        {
            AddMissing(report, "Campaign");
            return new CampaignSaveProgress("L00", new[] { new LevelSaveProgress("L00", true, false, 0) });
        }

        private static GameSettingsSave DefaultSettings(SaveValidationReport report)
        {
            AddMissing(report, "Settings");
            return GameSettingsSave.Default;
        }

        private static void AddMissing(SaveValidationReport report, string path)
        {
            report.Add(new SaveIssue(
                SaveIssueCode.MissingSectionDefaulted,
                SaveIssueSeverity.Warning,
                path,
                "Missing section was initialized with safe defaults."));
        }

        private static void AddUpgrade(SaveValidationReport report, int from, int to)
        {
            report.Add(new SaveIssue(
                SaveIssueCode.VersionUpgraded,
                SaveIssueSeverity.Warning,
                "Version",
                from + " -> " + to));
        }
    }
}
