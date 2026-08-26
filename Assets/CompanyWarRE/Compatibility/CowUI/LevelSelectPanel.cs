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
            for (var index = 0; index < (LevelButtons?.Length ?? 0); index++)
            {
                var button = LevelButtons[index];
                if (button == null) continue;
                if (index >= FormalLevelIds.Length)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                var levelId = FormalLevelIds[index];
                button.onClick.AddListener(() => _controller?.StartFormalLevel(levelId));
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = levelId;
            }

            if (BackButton != null) BackButton.onClick.AddListener(() => _controller?.ReturnFormalMainMenu());
        }

        private void OnEnable()
        {
            var flow = _controller?.CurrentFlow;
            for (var index = 0; index < (LevelButtons?.Length ?? 0) && index < FormalLevelIds.Length; index++)
            {
                if (LevelButtons[index] != null)
                {
                    LevelButtons[index].interactable = flow != null && flow.IsUnlocked(FormalLevelIds[index]);
                }
            }
        }
    }
}
