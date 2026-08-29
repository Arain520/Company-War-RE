using CompanyWarRE.Domain;
using CompanyWarRE.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWar.UI
{
    public sealed class MenuPanel : MonoBehaviour
    {
        public Button startButton;
        public Button levelSelectButton;
        public Button CloseButton;

        private BattleSliceController _controller;
        private Button _restartButton;
        private FormalFlowScreen _renderedScreen = (FormalFlowScreen)(-1);

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            EnsureCowStyledButtons();
            startButton?.onClick.RemoveAllListeners();
            levelSelectButton?.onClick.RemoveAllListeners();
            _restartButton?.onClick.RemoveAllListeners();
            CloseButton?.onClick.RemoveAllListeners();
            startButton?.onClick.AddListener(StartOrResume);
            levelSelectButton?.onClick.AddListener(OpenLevelSelect);
            _restartButton?.onClick.AddListener(() => _controller?.RestartFormalLevel());
            CloseButton?.onClick.AddListener(ReturnToMainMenu);
        }

        private void OnEnable()
        {
            _renderedScreen = (FormalFlowScreen)(-1);
            RefreshState();
        }

        private void Update()
        {
            RefreshState();
        }

        private void StartOrResume()
        {
            var flow = _controller?.CurrentFlow;
            if (flow == null)
            {
                return;
            }

            if (flow.Screen == FormalFlowScreen.Paused)
            {
                _controller.ToggleFormalPause();
            }
            else
            {
                _controller.StartFormalLevel(flow.ActiveLevelId);
            }
        }

        private void OpenLevelSelect()
        {
            _controller?.OpenFormalLevelSelect();
        }

        private void ReturnToMainMenu()
        {
            _controller?.ReturnFormalMainMenu();
        }

        private void RefreshState()
        {
            var flow = _controller?.CurrentFlow;
            if (flow == null || flow.Screen == _renderedScreen)
            {
                return;
            }

            _renderedScreen = flow.Screen;
            var paused = flow.Screen == FormalFlowScreen.Paused;
            SetLabel(startButton, paused ? "继续战斗" : "开始 / 继续 " + flow.ActiveLevelId);
            SetLabel(levelSelectButton, "关卡选择");
            SetLabel(_restartButton, "重新开始");
            SetLabel(CloseButton, "返回主菜单");
            if (_restartButton != null) _restartButton.gameObject.SetActive(paused);
            if (CloseButton != null) CloseButton.gameObject.SetActive(paused);
        }

        private void EnsureCowStyledButtons()
        {
            if (CloseButton == null)
            {
                CloseButton = GetComponentInChildren<Button>(true);
            }

            if (CloseButton == null)
            {
                return;
            }

            startButton = startButton ?? CloneButton(CloseButton, "StartButton", 78f);
            levelSelectButton = levelSelectButton ?? CloneButton(CloseButton, "LevelSelectButton", 18f);
            _restartButton = CloneButton(CloseButton, "RestartButton", -42f);
            LayoutButton(CloseButton, -102f);
        }

        private static Button CloneButton(Button source, string name, float y)
        {
            var clone = Instantiate(source, source.transform.parent);
            clone.name = name;
            clone.onClick.RemoveAllListeners();
            LayoutButton(clone, y);
            return clone;
        }

        private static void LayoutButton(Button button, float y)
        {
            if (!(button.transform is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(280f, 48f);
            rect.localScale = Vector3.one;
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
