using System;
using System.Collections.Generic;
using System.Linq;
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
        private GameObject _authorizationStatusPanel;
        private GameObject _deploymentPanel;
        private RectTransform _authorizationButtons;
        private RectTransform _deploymentButtons;
        private TMP_Text _authorizationStatusText;
        private TMP_Text _selectedUnitText;
        private Button _authorizationRequestButton;
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
            CowUiTypography.SetText(resourceText, "资源  " + snapshot.Resources);
            CowUiTypography.SetText(scoreText, "授权  " + snapshot.AuthorizationPoints);
            if (Victory != null) Victory.SetActive(snapshot.BattleState == BattleState.Victory);
            if (Fail != null) Fail.SetActive(snapshot.BattleState == BattleState.Defeat);

            var resultVisible = flow != null && flow.Screen == FormalFlowScreen.Result;
            var choosing = snapshot.AuthorizationState == AuthorizationState.Choosing;
            var battleVisible = flow != null && flow.Screen == FormalFlowScreen.Battle &&
                                snapshot.BattleState == BattleState.Running;
            _resultPanel.SetActive(resultVisible);
            _authorizationPanel.SetActive(choosing && !resultVisible);
            _authorizationStatusPanel.SetActive(battleVisible && !choosing);
            _deploymentPanel.SetActive(battleVisible && !choosing);
            if (MenuButton != null)
            {
                MenuButton.gameObject.SetActive(
                    flow != null && flow.Screen == FormalFlowScreen.Battle && !choosing);
            }

            RefreshResult(snapshot, flow);
            RefreshAuthorizationStatus(snapshot);
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

            _authorizationPanel = CreateModal("AuthorizationChoicePanel", new Vector2(560f, 520f));
            CreateText(_authorizationPanel.transform, "授权成长", 28f, 48f);
            CreateText(_authorizationPanel.transform, "选择一个新单位加入部署列表，战斗选择期间暂停", 18f, 42f);
            _authorizationButtons = CreateLayoutRoot(
                _authorizationPanel.transform,
                "AuthorizationButtons",
                false);
            CreateButton(
                _authorizationPanel.transform,
                "暂不申请",
                () => _controller?.CancelAuthorizationChoice());
            _authorizationPanel.SetActive(false);

            _authorizationStatusPanel = CreateContainer(
                transform,
                "AuthorizationStatus",
                new Vector2(330f, 112f),
                new Vector2(-28f, -28f),
                new Color(0.04f, 0.06f, 0.09f, 0.9f));
            var authorizationRect = (RectTransform)_authorizationStatusPanel.transform;
            authorizationRect.anchorMin = authorizationRect.anchorMax = new Vector2(1f, 1f);
            authorizationRect.pivot = new Vector2(1f, 1f);
            var authorizationLayout = _authorizationStatusPanel.AddComponent<VerticalLayoutGroup>();
            authorizationLayout.padding = new RectOffset(12, 12, 10, 10);
            authorizationLayout.spacing = 6f;
            authorizationLayout.childControlWidth = true;
            authorizationLayout.childForceExpandWidth = true;
            authorizationLayout.childForceExpandHeight = false;
            _authorizationStatusText = CreateText(
                _authorizationStatusPanel.transform,
                "授权进度",
                17f,
                32f);
            _authorizationRequestButton = CreateButton(
                _authorizationStatusPanel.transform,
                "申请授权",
                () => _controller?.RequestAuthorization(),
                300f);
            _authorizationStatusPanel.SetActive(false);

            _deploymentPanel = CreateContainer(
                transform,
                "DeploymentBar",
                new Vector2(1120f, 176f),
                new Vector2(0f, 84f),
                new Color(0.04f, 0.06f, 0.09f, 0.88f));
            var deployRect = (RectTransform)_deploymentPanel.transform;
            deployRect.anchorMin = deployRect.anchorMax = new Vector2(0.5f, 0f);
            deployRect.pivot = new Vector2(0.5f, 0f);
            var deploymentLayout = _deploymentPanel.AddComponent<VerticalLayoutGroup>();
            deploymentLayout.padding = new RectOffset(18, 18, 10, 10);
            deploymentLayout.spacing = 6f;
            deploymentLayout.childControlWidth = true;
            deploymentLayout.childForceExpandWidth = true;
            deploymentLayout.childForceExpandHeight = false;
            _selectedUnitText = CreateText(_deploymentPanel.transform, "选择部署单位", 17f, 32f);
            _deploymentButtons = CreateGridRoot(_deploymentPanel.transform, "DeploymentButtons");
            _deploymentPanel.SetActive(false);
        }

        private void RefreshAuthorizationStatus(BattleSliceSnapshot snapshot)
        {
            if (_authorizationStatusText == null || _authorizationRequestButton == null)
            {
                return;
            }

            var requirement = snapshot.NextAuthorizationRequirement;
            if (snapshot.AuthorizationState == AuthorizationState.Available)
            {
                CowUiTypography.SetText(
                    _authorizationStatusText,
                    $"授权点 {snapshot.AuthorizationPoints}  已达到申请条件");
                _authorizationRequestButton.gameObject.SetActive(true);
                _authorizationRequestButton.interactable = true;
                SetButtonLabel(_authorizationRequestButton, "申请新单位授权");
            }
            else if (snapshot.AuthorizationState == AuthorizationState.WaitingForNextRequirement)
            {
                CowUiTypography.SetText(
                    _authorizationStatusText,
                    $"授权点 {snapshot.AuthorizationPoints}/{requirement}  还需 {Math.Max(0, requirement - snapshot.AuthorizationPoints)}");
                _authorizationRequestButton.gameObject.SetActive(false);
            }
            else
            {
                CowUiTypography.SetText(_authorizationStatusText, "本关授权已全部完成");
                _authorizationRequestButton.gameObject.SetActive(false);
            }
        }

        private void RefreshResult(BattleSliceSnapshot snapshot, FormalGameFlowSnapshot flow)
        {
            if (!_resultPanel.activeSelf)
            {
                return;
            }

            CowUiTypography.SetText(_resultTitle, snapshot.BattleState == BattleState.Victory
                ? _controller.ActiveLevelId + "  胜利"
                : _controller.ActiveLevelId + "  失败");
            CowUiTypography.SetText(_resultSummary,
                $"突击分 {snapshot.AssaultScore}/{snapshot.RequiredAssaultScore}\n" +
                $"剩余建筑 {snapshot.EnemyBuildingCount}    用时 {snapshot.ElapsedSeconds:0.0}s");
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
            var signature = string.Join("|", snapshot.AuthorizationCandidates.Select(candidate =>
            {
                var option = FindOption(snapshot, candidate);
                return option == null ? candidate : candidate + ":" + option.Name;
            }));
            if (signature == _authorizationSignature)
            {
                return;
            }

            _authorizationSignature = signature;
            ClearChildren(_authorizationButtons);
            foreach (var candidate in snapshot.AuthorizationCandidates)
            {
                var captured = candidate;
                var option = FindOption(snapshot, candidate);
                CreateButton(
                    _authorizationButtons,
                    FormatAuthorizationLabel(option, candidate),
                    () => _controller?.AcceptAuthorization(captured));
            }
        }

        private void RefreshDeployments(BattleSliceSnapshot snapshot)
        {
            var signature = string.Join("|", snapshot.DeployList.Select(unitId =>
            {
                var option = FindOption(snapshot, unitId);
                return option == null ? unitId : unitId + ":" + option.Name;
            }));
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
                        168f);
                }
            }

            foreach (var pair in _deploymentButtonMap)
            {
                var option = FindOption(snapshot, pair.Key);
                SetButtonLabel(pair.Value, FormatDeploymentLabel(option, pair.Key));
                var colors = pair.Value.colors;
                var selected = string.Equals(
                    pair.Key,
                    _controller.SelectedUnitId,
                    StringComparison.OrdinalIgnoreCase);
                colors.normalColor = selected
                    ? new Color(0.15f, 0.62f, 0.72f, 1f)
                    : option != null && !option.CanDeploy
                        ? new Color(0.17f, 0.18f, 0.2f, 1f)
                        : new Color(0.18f, 0.22f, 0.28f, 1f);
                pair.Value.colors = colors;
            }

            var selectedOption = FindOption(snapshot, _controller.SelectedUnitId);
            if (_selectedUnitText != null)
            {
                CowUiTypography.SetText(_selectedUnitText, selectedOption == null
                    ? "选择部署单位"
                    : $"当前：{selectedOption.Id}  {selectedOption.Name}　费用 {selectedOption.ResourceCost}　{DescribeMode(selectedOption.DeploymentMode)}");
            }
        }

        private static BattleSliceUnitOptionSnapshot FindOption(BattleSliceSnapshot snapshot, string unitId)
        {
            return snapshot.UnitOptions.FirstOrDefault(option => string.Equals(
                option.Id,
                unitId,
                StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatDeploymentLabel(BattleSliceUnitOptionSnapshot option, string fallbackId)
        {
            if (option == null)
            {
                return fallbackId;
            }

            var state = option.RemainingCooldownSeconds > 0.05d
                ? $"冷却 {option.RemainingCooldownSeconds:0.0}s"
                : $"费用 {option.ResourceCost}";
            return $"{option.Id}  {option.Name}\n{state}";
        }

        private static string FormatAuthorizationLabel(BattleSliceUnitOptionSnapshot option, string fallbackId)
        {
            return option == null
                ? fallbackId
                : $"{option.Id}  {option.Name}　费用 {option.ResourceCost}　{DescribeMode(option.DeploymentMode)}";
        }

        private static string DescribeMode(DeploymentMode mode)
        {
            switch (mode)
            {
                case DeploymentMode.Building:
                    return "建筑";
                case DeploymentMode.SupportEffect:
                    return "支援";
                case DeploymentMode.TerrainBuild:
                    return "地形";
                default:
                    return "移动单位";
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

        private static RectTransform CreateGridRoot(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1080f, 112f);
            var element = root.AddComponent<LayoutElement>();
            element.preferredWidth = rect.sizeDelta.x;
            element.preferredHeight = rect.sizeDelta.y;
            var layout = root.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(168f, 50f);
            layout.spacing = new Vector2(8f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.MiddleCenter;
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
            var sourceFont = CowUiTypography.Font ??
                             (resourceText != null ? resourceText.font : scoreText != null ? scoreText.font : null);
            if (sourceFont != null) text.font = sourceFont;
            text.extraPadding = true;
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
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 18f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            CowUiTypography.SetText(label, value);
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
