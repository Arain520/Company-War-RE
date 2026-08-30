using System.Linq;
using CompanyWarRE.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWar.UI
{
    public sealed class LevelSelectPanel : MonoBehaviour
    {
        public Button[] LevelButtons;
        public Button BackButton;
        public string[] LevelIds = { "L00", "L01", "L02", "L03", "L04", "L05" };

        private static readonly string[] FormalLevelIds =
            BattleSliceController.AvailableFormalLevelIds.ToArray();
        private BattleSliceController _controller;

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            if (BackButton == null && LevelButtons != null && LevelButtons.Length > 4)
            {
                BackButton = LevelButtons[4];
            }

            BuildCompleteLevelGrid();

            for (var index = 0; index < (LevelButtons?.Length ?? 0); index++)
            {
                var button = LevelButtons[index];
                if (button == null) continue;
                if (index >= FormalLevelIds.Length)
                {
                    button.gameObject.SetActive(button == BackButton);
                    continue;
                }

                var levelId = FormalLevelIds[index];
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _controller?.StartFormalLevel(levelId));
                var label = button.GetComponentInChildren<TMP_Text>(true);
                CowUiTypography.SetText(label, levelId);
            }

            if (BackButton != null)
            {
                BackButton.onClick.RemoveAllListeners();
                BackButton.onClick.AddListener(() => _controller?.ReturnFormalMainMenu());
                SetLabel(BackButton, "返回主菜单");
            }
        }

        private void BuildCompleteLevelGrid()
        {
            var template = LevelButtons?.FirstOrDefault(button => button != null && button != BackButton);
            if (template == null)
            {
                return;
            }

            foreach (var oldButton in LevelButtons)
            {
                if (oldButton != null && oldButton != template && oldButton != BackButton)
                {
                    oldButton.gameObject.SetActive(false);
                }
            }

            var gridObject = new GameObject(
                "CompleteLevelGrid",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            var gridRect = (RectTransform)gridObject.transform;
            gridRect.SetParent(transform, false);
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -10f);
            gridRect.sizeDelta = new Vector2(1120f, 560f);
            var layout = gridObject.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(170f, 58f);
            layout.spacing = new Vector2(16f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.MiddleCenter;

            template.transform.SetParent(gridRect, false);
            template.gameObject.SetActive(true);
            var buttons = new Button[FormalLevelIds.Length];
            buttons[0] = template;
            for (var index = 1; index < buttons.Length; index++)
            {
                buttons[index] = Instantiate(template, gridRect, false);
                buttons[index].gameObject.name = "LevelButton_" + FormalLevelIds[index];
            }

            LevelButtons = buttons;
            LevelIds = FormalLevelIds.ToArray();
            if (BackButton != null && BackButton.transform is RectTransform backRect)
            {
                BackButton.gameObject.SetActive(true);
                backRect.SetAsLastSibling();
                backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0f);
                backRect.pivot = new Vector2(0.5f, 0f);
                backRect.anchoredPosition = new Vector2(0f, 28f);
            }
        }

        private void OnEnable()
        {
            var flow = _controller?.CurrentFlow;
            for (var index = 0; index < (LevelButtons?.Length ?? 0) && index < FormalLevelIds.Length; index++)
            {
                if (LevelButtons[index] != null)
                {
                    var levelId = FormalLevelIds[index];
                    var unlocked = flow != null && flow.IsUnlocked(levelId);
                    LevelButtons[index].interactable = unlocked;
                    SetLabel(
                        LevelButtons[index],
                        levelId + (flow != null && flow.IsCompleted(levelId)
                            ? "  已完成"
                            : unlocked ? "  可挑战" : "  未解锁"));
                }
            }
        }

        private static void SetLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                CowUiTypography.SetText(label, value);
            }
        }
    }
}
