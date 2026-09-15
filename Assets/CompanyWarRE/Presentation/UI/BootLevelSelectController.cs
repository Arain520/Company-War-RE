using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    [Serializable]
    public sealed class BootLevelSelectEntry
    {
        public string levelId;
        public string title;
        public string shortDescription;
        [TextArea] public string recordText;
        public string chapterName;
        public string phaseLabel;
        public int columns;
        public int rows;
        public int stageCount;
        public int requiredAssaultScore;
        public int medalScore;
        public int recommendedPower;
        [TextArea] public string rewardsText;
        public Button button;
        public Image cardImage;
        public TMP_Text cardLabel;
    }

    public sealed class BootLevelSelectController : MonoBehaviour
    {
        private const string LastLevelKey = "CompanyWar.LastLevel";

        [SerializeField] private GameObject mainMenuCanvas;
        [SerializeField] private RectTransform descriptionPanel;
        [SerializeField] private TMP_Text descriptionTitle;
        [SerializeField] private TMP_Text descriptionBody;
        [SerializeField] private TMP_Text sideLevelId;
        [SerializeField] private TMP_Text sideTitle;
        [SerializeField] private TMP_Text sideSummary;
        [SerializeField] private TMP_Text sideMeta;
        [SerializeField] private TMP_Text sideRewards;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect levelScroll;
        [SerializeField] private string battleSceneName = "FormalBattle";
        [SerializeField] private BootLevelSelectEntry[] levels = Array.Empty<BootLevelSelectEntry>();
        [SerializeField] private Vector2 descriptionHiddenPosition = new Vector2(0f, 190f);
        [SerializeField] private Vector2 descriptionShownPosition = Vector2.zero;
        [SerializeField] private float descriptionAnimationSeconds = 0.28f;

        private int _selectedIndex = -1;
        private Coroutine _descriptionAnimation;

        public void Configure(
            GameObject mainMenu,
            RectTransform description,
            TMP_Text descriptionHeading,
            TMP_Text descriptionCopy,
            TMP_Text levelId,
            TMP_Text title,
            TMP_Text summary,
            TMP_Text meta,
            TMP_Text rewards,
            Button enter,
            Button back,
            ScrollRect scroll,
            BootLevelSelectEntry[] entries,
            string battleScene)
        {
            mainMenuCanvas = mainMenu;
            descriptionPanel = description;
            descriptionTitle = descriptionHeading;
            descriptionBody = descriptionCopy;
            sideLevelId = levelId;
            sideTitle = title;
            sideSummary = summary;
            sideMeta = meta;
            sideRewards = rewards;
            enterButton = enter;
            backButton = back;
            levelScroll = scroll;
            levels = entries ?? Array.Empty<BootLevelSelectEntry>();
            battleSceneName = string.IsNullOrWhiteSpace(battleScene) ? "FormalBattle" : battleScene;
        }

        private void Awake()
        {
            for (var index = 0; index < levels.Length; index++)
            {
                var capturedIndex = index;
                if (levels[index]?.button == null) continue;
                levels[index].button.onClick.RemoveAllListeners();
                levels[index].button.onClick.AddListener(() => SelectLevel(capturedIndex, true));
            }

            if (enterButton != null)
            {
                enterButton.onClick.RemoveAllListeners();
                enterButton.onClick.AddListener(EnterSelectedLevel);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void OnEnable()
        {
            if (descriptionPanel != null) descriptionPanel.anchoredPosition = descriptionHiddenPosition;
            var savedId = PlayerPrefs.GetString(LastLevelKey, levels.Length > 0 ? levels[0].levelId : string.Empty);
            var index = Array.FindIndex(levels,
                level => level != null && string.Equals(level.levelId, savedId, StringComparison.OrdinalIgnoreCase));
            SelectLevel(index >= 0 ? index : 0, false);
        }

        public void SelectLevel(int index, bool animateDescription)
        {
            if (index < 0 || index >= levels.Length || levels[index] == null) return;
            _selectedIndex = index;
            var selected = levels[index];

            for (var cardIndex = 0; cardIndex < levels.Length; cardIndex++)
            {
                var entry = levels[cardIndex];
                if (entry?.cardImage == null) continue;
                entry.cardImage.color = cardIndex == index
                    ? new Color(0.08f, 0.28f, 0.96f, 1f)
                    : new Color(0.035f, 0.07f, 0.28f, 1f);
            }

            Set(descriptionTitle, $"{selected.levelId} · 行动记录：{selected.title}");
            Set(descriptionBody, selected.recordText);
            Set(sideLevelId, selected.levelId);
            Set(sideTitle, selected.title);
            Set(sideSummary, selected.shortDescription);
            Set(sideMeta,
                $"关卡名称：{selected.title}\n地图尺寸：{selected.columns} × {selected.rows}\n" +
                $"预计阶段：{selected.stageCount}\n攻坚目标：{selected.requiredAssaultScore}\n" +
                $"勋章参考：{selected.medalScore}\n\n难度：{DifficultyLabel(selected.recommendedPower)}\n" +
                $"推荐战力：{selected.recommendedPower:N0}");
            Set(sideRewards, "奖励：\n" + selected.rewardsText);

            if (animateDescription) AnimateDescription();
        }

        public void EnterSelectedLevel()
        {
            if (_selectedIndex < 0 || _selectedIndex >= levels.Length) return;
            var level = levels[_selectedIndex];
            PlayerPrefs.SetString(LastLevelKey, level.levelId);
            PlayerPrefs.Save();
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(battleSceneName))
            {
                Set(sideSummary, $"场景 {battleSceneName} 未加入 Build Settings。");
                return;
            }

            if (!SceneLoadingPanel.LoadScene(battleSceneName, LoadSceneMode.Single))
            {
                Set(sideSummary, $"无法载入场景 {battleSceneName}，请检查 Build Settings。");
            }
        }

        public void ReturnToMainMenu()
        {
            if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
            gameObject.SetActive(false);
        }

        private void AnimateDescription()
        {
            if (descriptionPanel == null) return;
            if (_descriptionAnimation != null) StopCoroutine(_descriptionAnimation);
            _descriptionAnimation = StartCoroutine(SlideDescription());
        }

        private IEnumerator SlideDescription()
        {
            descriptionPanel.anchoredPosition = descriptionHiddenPosition;
            var elapsed = 0f;
            while (elapsed < descriptionAnimationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, descriptionAnimationSeconds));
                t = 1f - Mathf.Pow(1f - t, 3f);
                descriptionPanel.anchoredPosition = Vector2.LerpUnclamped(
                    descriptionHiddenPosition, descriptionShownPosition, t);
                yield return null;
            }

            descriptionPanel.anchoredPosition = descriptionShownPosition;
            _descriptionAnimation = null;
        }

        private static string DifficultyLabel(int power)
        {
            if (power < 8000) return "普通";
            if (power < 24000) return "困难";
            if (power < 40000) return "高危";
            return "极限";
        }

        private static void Set(TMP_Text target, string value)
        {
            if (target != null) target.text = value ?? string.Empty;
        }
    }
}
