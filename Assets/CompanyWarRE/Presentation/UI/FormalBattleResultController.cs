using System;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    public sealed class FormalBattleResultController : MonoBehaviour
    {
        private const string ArchiveResourcePath = "CompanyWarRE/Configs/LevelArchives";
        private const string BootRouteKey = "CompanyWar.BootRoute";
        private const string LevelSelectRoute = "LevelSelect";
        private const string VictoryImagePath = "CowLegacy/_Game/Art/08e8c90f-7c51-4457-aace-762b212d32c9";
        private const string DefeatImagePath = "CowLegacy/_Game/Art/8c3cdf86-bce5-4ea9-b5a9-b9fb8ca04a2e";

        [SerializeField] private BattleSliceController battle;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image accentImage;
        [SerializeField] private Image placeholderImage;
        [SerializeField] private Sprite victorySprite;
        [SerializeField] private Sprite defeatSprite;
        [SerializeField] private TMP_Text operationTitle;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSubtitle;
        [SerializeField] private TMP_Text resultDescription;
        [SerializeField] private TMP_Text scoreValue;
        [SerializeField] private TMP_Text buildingValue;
        [SerializeField] private TMP_Text elapsedValue;
        [SerializeField] private TMP_Text placeholderLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button mainMenuButton;

        private ArchiveRoot _archive;
        private BattleState _displayedState = BattleState.Running;
        private float _targetAlpha;

        public void Configure(
            BattleSliceController controller, CanvasGroup group, Image accent, Image placeholder,
            TMP_Text operation, TMP_Text title, TMP_Text subtitle, TMP_Text description,
            TMP_Text score, TMP_Text buildings, TMP_Text elapsed, TMP_Text placeholderCopy,
            Button restart, Button next, TMP_Text nextLabel, Button levelSelect, Button mainMenu)
        {
            battle = controller;
            canvasGroup = group;
            accentImage = accent;
            placeholderImage = placeholder;
            operationTitle = operation;
            resultTitle = title;
            resultSubtitle = subtitle;
            resultDescription = description;
            scoreValue = score;
            buildingValue = buildings;
            elapsedValue = elapsed;
            placeholderLabel = placeholderCopy;
            restartButton = restart;
            nextButton = next;
            nextButtonLabel = nextLabel;
            levelSelectButton = levelSelect;
            mainMenuButton = mainMenu;
            ApplyResultImage(true);
        }

        private void ApplyResultImage(bool victory)
        {
            if (victorySprite == null) victorySprite = Resources.Load<Sprite>(VictoryImagePath);
            if (defeatSprite == null) defeatSprite = Resources.Load<Sprite>(DefeatImagePath);
            var sprite = victory ? victorySprite : defeatSprite;
            if (placeholderImage != null)
            {
                placeholderImage.sprite = sprite;
                placeholderImage.type = Image.Type.Simple;
                placeholderImage.preserveAspect = true;
                placeholderImage.color = sprite != null ? Color.white : new Color(0.10f, 0.17f, 0.24f, 1f);
                placeholderImage.raycastTarget = false;
            }
            if (placeholderLabel != null) placeholderLabel.gameObject.SetActive(sprite == null);
        }

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleSliceController>();
            var archiveAsset = Resources.Load<TextAsset>(ArchiveResourcePath);
            _archive = archiveAsset == null
                ? new ArchiveRoot()
                : JsonUtility.FromJson<ArchiveRoot>(archiveAsset.text) ?? new ArchiveRoot();

            Bind(restartButton, RestartLevel);
            Bind(nextButton, StartNextLevel);
            Bind(levelSelectButton, ReturnToLevelSelect);
            Bind(mainMenuButton, ReturnToMainMenu);
            ApplyResultImage(true);
            SetVisible(false, true);
        }

        private void LateUpdate()
        {
            var snapshot = battle?.CurrentSnapshot;
            if (snapshot == null) return;

            if (snapshot.BattleState != BattleState.Running && snapshot.BattleState != _displayedState)
            {
                _displayedState = snapshot.BattleState;
                RefreshResult(snapshot);
                SetVisible(true, false);
            }
            else if (snapshot.BattleState == BattleState.Running && _displayedState != BattleState.Running)
            {
                _displayedState = BattleState.Running;
                SetVisible(false, false);
            }

            if (canvasGroup == null) return;
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * 5f);
        }

        private void RefreshResult(BattleSliceSnapshot snapshot)
        {
            var victory = snapshot.BattleState == BattleState.Victory;
            var levelId = battle.ActiveLevelId;
            var copy = FindCopy(levelId);
            var title = string.IsNullOrWhiteSpace(copy?.Title) ? levelId : copy.Title;
            var description = victory ? copy?.VictoryText : copy?.DefeatText;
            if (string.IsNullOrWhiteSpace(description))
                description = victory
                    ? "行动目标已经完成，区域控制权已确认。"
                    : "行动目标未完成，请调整部署方案后重新尝试。";

            Set(operationTitle, levelId + " · " + title);
            Set(resultTitle, levelId + (victory ? "  胜利" : "  失败"));
            Set(resultSubtitle, victory ? "任务完成  ·  MISSION CLEAR" : "任务未完成  ·  MISSION FAILED");
            Set(resultDescription, description);
            Set(scoreValue, $"{snapshot.AssaultScore} / {snapshot.RequiredAssaultScore}");
            Set(buildingValue, snapshot.EnemyBuildingCount.ToString());
            Set(elapsedValue, snapshot.ElapsedSeconds.ToString("0.0") + "s");
            Set(placeholderLabel, victory
                ? "OPERATION COMPLETE\n通用胜利图占位"
                : "MISSION INCOMPLETE\n通用失败图占位");

            var accent = victory
                ? new Color(0.72f, 0.46f, 0.10f, 1f)
                : new Color(0.55f, 0.045f, 0.04f, 1f);
            if (accentImage != null) accentImage.color = accent;
            ApplyResultImage(victory);

            var nextLevel = battle.CurrentFlow?.NextLevelId ?? string.Empty;
            var canContinue = victory && !string.IsNullOrWhiteSpace(nextLevel);
            if (nextButton != null) nextButton.interactable = canContinue;
            Set(nextButtonLabel, canContinue ? "下一关  " + nextLevel : "下一关");
        }

        private ArchiveEntry FindCopy(string levelId)
        {
            if (_archive?.Levels == null) return null;
            return Array.Find(_archive.Levels, entry => entry != null &&
                string.Equals(entry.Id, levelId, StringComparison.OrdinalIgnoreCase));
        }

        private void RestartLevel()
        {
            battle?.RestartFormalLevel();
            _displayedState = BattleState.Running;
            SetVisible(false, false);
        }

        private void StartNextLevel()
        {
            var nextLevel = battle?.CurrentFlow?.NextLevelId;
            if (!string.IsNullOrWhiteSpace(nextLevel) && battle.StartFormalLevel(nextLevel))
            {
                _displayedState = BattleState.Running;
                SetVisible(false, false);
            }
        }

        private static void ReturnToLevelSelect()
        {
            PlayerPrefs.SetString(BootRouteKey, LevelSelectRoute);
            PlayerPrefs.Save();
            SceneLoadingPanel.LoadScene("Boot", LoadSceneMode.Single);
        }

        private static void ReturnToMainMenu()
        {
            PlayerPrefs.DeleteKey(BootRouteKey);
            PlayerPrefs.Save();
            SceneLoadingPanel.LoadScene("Boot", LoadSceneMode.Single);
        }

        private void SetVisible(bool visible, bool immediate)
        {
            _targetAlpha = visible ? 1f : 0f;
            if (canvasGroup == null) return;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (immediate) canvasGroup.alpha = _targetAlpha;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void Set(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }

        [Serializable]
        private sealed class ArchiveRoot
        {
            public ArchiveEntry[] Levels = Array.Empty<ArchiveEntry>();
        }

        [Serializable]
        private sealed class ArchiveEntry
        {
            public string Id;
            public string Title;
            public string VictoryText;
            public string DefeatText;
        }
    }
}
