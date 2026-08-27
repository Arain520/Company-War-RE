using System;
using System.Collections.Generic;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        private GameObject _resultPanel;
        private GameObject _authorizationPanel;
        private GameObject _deploymentPanel;
        private RectTransform _authorizationButtons;
        private RectTransform _deploymentButtons;
        private TMP_Text _resultTitle;
        private TMP_Text _resultSummary;
        private Button _nextLevelButton;
        private string _authorizationSignature = string.Empty;
        private string _deploymentSignature = string.Empty;
        private readonly Dictionary<string, Button> _deploymentButtonMap =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            _controller = FindObjectOfType<BattleSliceController>();
            if (MenuButton != null) MenuButton.onClick.AddListener(() => _controller?.ToggleFormalPause());
            var background = GetComponent<Image>();
            if (background != null)
            {
                var color = background.color;
                color.a = 0f;
                background.color = color;
                background.raycastTarget = false;
            }

            BuildRuntimePanels();
        }

        private void LateUpdate()
        {
            var snapshot = _controller?.CurrentSnapshot;
            if (snapshot == null) return;
            var flow = _controller.CurrentFlow;
            if (resourceText != null) resourceText.text = "资源  " + snapshot.Resources;
            if (scoreText != null) scoreText.text = "授权  " + snapshot.AuthorizationPoints;
            if (Victory != null) Victory.SetActive(snapshot.BattleState == BattleState.Victory);
            if (Fail != null) Fail.SetActive(snapshot.BattleState == BattleState.Defeat);

            var resultVisible = flow != null && flow.Screen == FormalFlowScreen.Result;
            var choosing = snapshot.AuthorizationState == AuthorizationState.Choosing;
            _resultPanel.SetActive(resultVisible);
            _authorizationPanel.SetActive(choosing && !resultVisible);
            _deploymentPanel.SetActive(
                flow != null && flow.Screen == FormalFlowScreen.Battle &&
                snapshot.BattleState == BattleState.Running && !choosing);
            if (MenuButton != null)
            {
                MenuButton.gameObject.SetActive(
                    flow != null && flow.Screen == FormalFlowScreen.Battle && !choosing);
            }

            RefreshResult(snapshot, flow);
            RefreshAuthorization(snapshot);
            RefreshDeployments(snapshot);
        }

        private void BuildRuntimePanels()
        {
            _resultPanel = CreateModal("FormalResultPanel", new Vector2(450f, 380f));
            _resultTitle = CreateText(_resultPanel.transform, "战斗结算", 30f, 48f);
            _resultSummary = CreateText(_resultPanel.transform, string.Empty, 19f, 66f);
            CreateButton(_resultPanel.transform, "重新开始", () => _controller?.RestartFormalLevel());
            _nextLevelButton = CreateButton(_resultPanel.transform, "下一关", StartNextLevel);
            CreateButton(_resultPanel.transform, "关卡选择", () => _controller?.OpenFormalLevelSelect());
            CreateButton(_resultPanel.transform, "返回主菜单", () => _controller?.ReturnFormalMainMenu());
            _resultPanel.SetActive(false);

            _authorizationPanel = CreateModal("AuthorizationChoicePanel", new Vector2(460f, 430f));
            CreateText(_authorizationPanel.transform, "授权成长", 28f, 48f);
            CreateText(_authorizationPanel.transform, "选择一个新单位加入部署列表", 18f, 36f);
            _authorizationButtons = CreateLayoutRoot(
                _authorizationPanel.transform,
                "AuthorizationButtons",
                false);
            _authorizationPanel.SetActive(false);

            _deploymentPanel = CreateContainer(
                transform,
                "DeploymentBar",
                new Vector2(760f, 70f),
                new Vector2(0f, 48f),
                new Color(0.04f, 0.06f, 0.09f, 0.88f));
            var deployRect = (RectTransform)_deploymentPanel.transform;
            deployRect.anchorMin = deployRect.anchorMax = new Vector2(0.5f, 0f);
            _deploymentButtons = CreateLayoutRoot(_deploymentPanel.transform, "DeploymentButtons", true);
            _deploymentPanel.SetActive(false);
        }

        private void RefreshResult(BattleSliceSnapshot snapshot, FormalGameFlowSnapshot flow)
        {
            if (!_resultPanel.activeSelf)
            {
                return;
            }

            _resultTitle.text = snapshot.BattleState == BattleState.Victory
                ? _controller.ActiveLevelId + "  胜利"
                : _controller.ActiveLevelId + "  失败";
            _resultSummary.text =
                $"突击分 {snapshot.AssaultScore}/{snapshot.RequiredAssaultScore}\n" +
                $"剩余建筑 {snapshot.EnemyBuildingCount}    用时 {snapshot.ElapsedSeconds:0.0}s";
            var nextLevel = flow?.NextLevelId ?? string.Empty;
            _nextLevelButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(nextLevel));
            SetButtonLabel(_nextLevelButton, "下一关  " + nextLevel);
        }

        private void StartNextLevel()
        {
            var nextLevel = _controller?.CurrentFlow?.NextLevelId;
            if (!string.IsNullOrWhiteSpace(nextLevel))
            {
                _controller.StartFormalLevel(nextLevel);
            }
        }

        private void RefreshAuthorization(BattleSliceSnapshot snapshot)
        {
            var signature = string.Join("|", snapshot.AuthorizationCandidates);
            if (signature == _authorizationSignature)
            {
                return;
            }

            _authorizationSignature = signature;
            ClearChildren(_authorizationButtons);
            foreach (var candidate in snapshot.AuthorizationCandidates)
            {
                var captured = candidate;
                CreateButton(
                    _authorizationButtons,
                    candidate,
                    () => _controller?.AcceptAuthorization(captured));
            }
        }

        private void RefreshDeployments(BattleSliceSnapshot snapshot)
        {
            var signature = string.Join("|", snapshot.DeployList);
            if (signature != _deploymentSignature)
            {
                _deploymentSignature = signature;
                ClearChildren(_deploymentButtons);
                _deploymentButtonMap.Clear();
                foreach (var unitId in snapshot.DeployList)
                {
                    var captured = unitId;
                    _deploymentButtonMap[unitId] = CreateButton(
                        _deploymentButtons,
                        unitId,
                        () => _controller?.SelectDeploymentUnit(captured),
                        88f);
                }
            }

            foreach (var pair in _deploymentButtonMap)
            {
                var colors = pair.Value.colors;
                colors.normalColor = string.Equals(
                    pair.Key,
                    _controller.SelectedUnitId,
                    StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.15f, 0.62f, 0.72f, 1f)
                    : new Color(0.18f, 0.22f, 0.28f, 1f);
                pair.Value.colors = colors;
            }
        }

        private GameObject CreateModal(string name, Vector2 size)
        {
            var panel = CreateContainer(
                transform,
                name,
                size,
                Vector2.zero,
                new Color(0.035f, 0.05f, 0.075f, 0.96f));
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private static GameObject CreateContainer(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.layer = parent.gameObject.layer;
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = panel.GetComponent<Image>();
            image.color = color;
            return panel;
        }

        private static RectTransform CreateLayoutRoot(Transform parent, string name, bool horizontal)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = horizontal ? new Vector2(720f, 50f) : new Vector2(400f, 250f);
            var element = root.AddComponent<LayoutElement>();
            element.preferredWidth = rect.sizeDelta.x;
            element.preferredHeight = rect.sizeDelta.y;
            var layout = horizontal
                ? (HorizontalOrVerticalLayoutGroup)root.AddComponent<HorizontalLayoutGroup>()
                : root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = !horizontal;
            layout.childForceExpandWidth = !horizontal;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private TMP_Text CreateText(Transform parent, string value, float fontSize, float preferredHeight)
        {
            var item = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            item.layer = parent.gameObject.layer;
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            var sourceFont = resourceText != null ? resourceText.font : scoreText != null ? scoreText.font : null;
            if (sourceFont != null) text.font = sourceFont;
            var layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            return text;
        }

        private Button CreateButton(
            Transform parent,
            string label,
            UnityAction action,
            float preferredWidth = 320f)
        {
            var item = new GameObject("Button_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            item.layer = parent.gameObject.layer;
            item.transform.SetParent(parent, false);
            var image = item.GetComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            var button = item.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var layout = item.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = 44f;
            var text = CreateText(item.transform, label, 18f, 44f);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = value;
        }

        private static void ClearChildren(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                root.GetChild(index).gameObject.SetActive(false);
                Destroy(root.GetChild(index).gameObject);
            }
        }
    }
}
