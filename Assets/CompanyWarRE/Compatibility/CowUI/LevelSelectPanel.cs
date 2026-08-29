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

        private static readonly string[] FormalLevelIds = { "L02", "L03", "L04", "L05" };
        private BattleSliceController _controller;

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            if (BackButton == null && LevelButtons != null && LevelButtons.Length > FormalLevelIds.Length)
            {
                BackButton = LevelButtons[FormalLevelIds.Length];
            }

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
