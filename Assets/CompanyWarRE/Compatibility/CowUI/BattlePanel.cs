using CompanyWarRE.Domain;
using CompanyWarRE.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyWar.UI
{
    public sealed class BattlePanel : MonoBehaviour
    {
        public TMP_Text resourceText;
        public TMP_Text scoreText;
        public GameObject Victory;
        public GameObject Fail;
        public Button MenuButton;
        public GameObject MenuPanelPrefab;

        private BattleSliceController _controller;

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            if (MenuButton != null) MenuButton.onClick.AddListener(() => _controller?.ToggleFormalPause());
        }

        private void LateUpdate()
        {
            var snapshot = _controller?.CurrentSnapshot;
            if (snapshot == null) return;
            if (resourceText != null) resourceText.text = snapshot.Resources.ToString();
            if (scoreText != null) scoreText.text = snapshot.AuthorizationPoints.ToString();
            if (Victory != null) Victory.SetActive(snapshot.BattleState == BattleState.Victory);
            if (Fail != null) Fail.SetActive(snapshot.BattleState == BattleState.Defeat);
        }
    }
}
