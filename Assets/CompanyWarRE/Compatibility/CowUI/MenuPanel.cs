using CompanyWarRE.Presentation;
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

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (levelSelectButton != null) levelSelectButton.onClick.AddListener(OpenLevelSelect);
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
        }

        private void StartGame()
        {
            var flow = _controller?.CurrentFlow;
            if (flow != null) _controller.StartFormalLevel(flow.ActiveLevelId);
        }

        private void OpenLevelSelect()
        {
            _controller?.OpenFormalLevelSelect();
        }

        private void Close()
        {
            _controller?.ToggleFormalPause();
            gameObject.SetActive(false);
        }
    }
}
