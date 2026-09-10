using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    [DefaultExecutionOrder(-50)]
    public sealed class FormalBattleHudController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private BattleSliceController battle;
        [SerializeField] private TMP_FontAsset font;
        [Header("Top")]
        [SerializeField] private TMP_Text assaultText;
        [SerializeField] private TMP_Text resourceText;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private Button pauseButton;
        [Header("Deployment")]
        [SerializeField] private RectTransform deploymentContent;
        [SerializeField] private RectTransform unitInfoPanel;
        [SerializeField] private TMP_Text unitNameText;
        [SerializeField] private TMP_Text unitTypeText;
        [SerializeField] private TMP_Text unitStatsText;
        [SerializeField] private TMP_Text unitEffectText;
        [Header("Authorization")]
        [SerializeField] private RectTransform authorizationProgressDisplay;
        [SerializeField] private Image authorizationFill;
        [SerializeField] private TMP_Text authorizationProgressText;
        [SerializeField] private RectTransform authorizationButtonPanel;
        [SerializeField] private Button authorizationButton;
        [SerializeField] private RectTransform authorizationChoicePanel;
        [SerializeField] private RectTransform authorizationChoiceContent;
        [Header("Footer")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text feedbackText;
        [Header("Drag")]
        [SerializeField] private RectTransform dragGhost;
        [SerializeField] private TMP_Text dragGhostText;

        private readonly Dictionary<string, Image> _deploymentCards =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private string _deploymentSignature = string.Empty;
        private string _authorizationSignature = string.Empty;
        private string _dragUnitId;
        private string _selectedUnitId;
        private bool _infoVisible;
        private bool _authorizationWasAvailable;

        private static readonly Color Navy = new Color(0.025f, 0.075f, 0.15f, 0.98f);
        private static readonly Color Gold = new Color(0.72f, 0.48f, 0.16f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.80f, 0.98f);

        public void Configure(
            BattleSliceController controller, TMP_FontAsset mainFont,
            TMP_Text assault, TMP_Text resource, TMP_Text phase, Button pause,
            RectTransform deployContent, RectTransform infoPanel, TMP_Text unitName,
            TMP_Text unitType, TMP_Text unitStats, TMP_Text unitEffect,
            RectTransform authProgressDisplay, Image authFill, TMP_Text authProgress, RectTransform authButtonPanel,
            Button authButton, RectTransform authChoicePanel, RectTransform authChoiceContent,
            TMP_Text clock, TMP_Text feedback, RectTransform ghost, TMP_Text ghostText)
        {
            battle = controller;
            font = mainFont;
            assaultText = assault;
            resourceText = resource;
            phaseText = phase;
            pauseButton = pause;
            deploymentContent = deployContent;
            unitInfoPanel = infoPanel;
            unitNameText = unitName;
            unitTypeText = unitType;
            unitStatsText = unitStats;
            unitEffectText = unitEffect;
            authorizationProgressDisplay = authProgressDisplay;
            authorizationFill = authFill;
            authorizationProgressText = authProgress;
            authorizationButtonPanel = authButtonPanel;
            authorizationButton = authButton;
            authorizationChoicePanel = authChoicePanel;
            authorizationChoiceContent = authChoiceContent;
            clockText = clock;
            feedbackText = feedback;
            dragGhost = ghost;
            dragGhostText = ghostText;
        }

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleSliceController>();
            NormalizeAuthorizationSlot();
            pauseButton?.onClick.AddListener(() => battle?.ToggleFormalPause());
            authorizationButton?.onClick.AddListener(RequestAuthorization);
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            if (authorizationChoicePanel != null)
            {
                authorizationChoicePanel.anchoredPosition =
                    new Vector2(authorizationChoicePanel.rect.width + 28f, 0f);
                authorizationChoicePanel.gameObject.SetActive(false);
            }
            if (unitInfoPanel != null) unitInfoPanel.anchoredPosition = InfoHiddenPosition();
            if (authorizationButtonPanel != null) authorizationButtonPanel.gameObject.SetActive(false);
        }

        private void NormalizeAuthorizationSlot()
        {
            if (authorizationFill == null || authorizationButtonPanel == null) return;
            var slot = authorizationProgressDisplay != null
                ? authorizationProgressDisplay.parent as RectTransform
                : authorizationFill.transform.parent != null
                    ? authorizationFill.transform.parent.parent as RectTransform
                    : null;
            if (slot == null) return;

            if (authorizationProgressDisplay == null)
            {
                var displayObject = new GameObject("ProgressDisplay", typeof(RectTransform));
                authorizationProgressDisplay = (RectTransform)displayObject.transform;
                authorizationProgressDisplay.SetParent(slot, false);
                authorizationProgressDisplay.anchorMin = Vector2.zero;
                authorizationProgressDisplay.anchorMax = Vector2.one;
                authorizationProgressDisplay.offsetMin = Vector2.zero;
                authorizationProgressDisplay.offsetMax = Vector2.zero;

                var children = new List<Transform>();
                for (var index = 0; index < slot.childCount; index++)
                {
                    var child = slot.GetChild(index);
                    if (child != authorizationProgressDisplay && child != authorizationButtonPanel)
                        children.Add(child);
                }
                foreach (var child in children) child.SetParent(authorizationProgressDisplay, false);
            }

            if (authorizationButtonPanel.parent != slot)
                authorizationButtonPanel.SetParent(slot, false);
            authorizationButtonPanel.anchorMin = Vector2.zero;
            authorizationButtonPanel.anchorMax = Vector2.one;
            authorizationButtonPanel.offsetMin = Vector2.zero;
            authorizationButtonPanel.offsetMax = Vector2.zero;
            authorizationButtonPanel.anchoredPosition = Vector2.zero;
        }

        private void LateUpdate()
        {
            var snapshot = battle?.CurrentSnapshot;
            if (snapshot == null) return;

            if (clockText != null) clockText.text = DateTime.Now.ToString("HH:mm");
            if (assaultText != null)
                assaultText.text = $"攻坚积分  {snapshot.AssaultScore:000}/{snapshot.RequiredAssaultScore:000}";
            if (resourceText != null) resourceText.text = $"物资  {snapshot.Resources:000}";
            if (phaseText != null)
                phaseText.text = "当前阶段：" + (string.IsNullOrWhiteSpace(snapshot.CurrentWaveStage)
                    ? snapshot.WaveIndex.ToString() : snapshot.CurrentWaveStage);

            RefreshAuthorizationProgress(snapshot);
            RefreshDeploymentCards(snapshot);
            RefreshAuthorizationChoices(snapshot);
            AnimatePanels(snapshot);
        }

        private void Update()
        {
            if (battle == null || !string.IsNullOrWhiteSpace(_dragUnitId)) return;
            battle.SetRuntimeUiPointerBlocked(
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        private void OnDisable()
        {
            battle?.SetRuntimeUiPointerBlocked(false);
        }

        private void RefreshAuthorizationProgress(BattleSliceSnapshot snapshot)
        {
            var requirement = snapshot.NextAuthorizationRequirement;
            var complete = requirement == int.MaxValue || requirement <= 0;
            var ratio = complete ? 1f : Mathf.Clamp01((float)snapshot.AuthorizationPoints / requirement);
            if (authorizationFill != null) authorizationFill.fillAmount = ratio;
            if (authorizationProgressText != null)
                authorizationProgressText.text = complete
                    ? "授权申请  已完成"
                    : $"授权申请  {snapshot.AuthorizationPoints:000}/{requirement:000}";

            var available = snapshot.AuthorizationState == AuthorizationState.Available;
            if (available && !_authorizationWasAvailable && feedbackText != null)
                feedbackText.text = "授权积分已满足，可申请新的部署单位";
            _authorizationWasAvailable = available;
        }

        private void AnimatePanels(BattleSliceSnapshot snapshot)
        {
            var available = snapshot.AuthorizationState == AuthorizationState.Available;
            var choosing = snapshot.AuthorizationState == AuthorizationState.Choosing;
            if (authorizationProgressDisplay != null)
                authorizationProgressDisplay.gameObject.SetActive(!available && !choosing);
            if (authorizationButtonPanel != null)
                authorizationButtonPanel.gameObject.SetActive(available || choosing);
            if (authorizationButton != null) authorizationButton.interactable = available;

            if (unitInfoPanel != null)
            {
                var target = _infoVisible ? Vector2.zero : InfoHiddenPosition();
                unitInfoPanel.anchoredPosition = Vector2.Lerp(
                    unitInfoPanel.anchoredPosition, target, Time.unscaledDeltaTime * 14f);
            }

            AnimateChoicePanel(choosing);
        }

        private Vector2 InfoHiddenPosition()
        {
            return new Vector2(-(unitInfoPanel == null ? 360f : unitInfoPanel.rect.width + 24f), 0f);
        }

        private void AnimateChoicePanel(bool visible)
        {
            if (authorizationChoicePanel == null) return;
            if (visible && !authorizationChoicePanel.gameObject.activeSelf)
                authorizationChoicePanel.gameObject.SetActive(true);
            if (!authorizationChoicePanel.gameObject.activeSelf) return;

            var hidden = new Vector2(authorizationChoicePanel.rect.width + 28f, 0f);
            var target = visible ? Vector2.zero : hidden;
            authorizationChoicePanel.anchoredPosition = Vector2.Lerp(
                authorizationChoicePanel.anchoredPosition, target, Time.unscaledDeltaTime * 14f);
            if (!visible && Vector2.Distance(authorizationChoicePanel.anchoredPosition, hidden) < 1f)
                authorizationChoicePanel.gameObject.SetActive(false);
        }

        private void RefreshDeploymentCards(BattleSliceSnapshot snapshot)
        {
            var signature = string.Join("|", snapshot.DeployList);
            if (signature != _deploymentSignature)
            {
                _deploymentSignature = signature;
                ClearChildren(deploymentContent);
                _deploymentCards.Clear();
                foreach (var unitId in snapshot.DeployList)
                {
                    var option = FindOption(snapshot, unitId);
                    CreateDeploymentCard(unitId, option);
                }
            }

            foreach (var pair in _deploymentCards)
            {
                var option = FindOption(snapshot, pair.Key);
                pair.Value.color = string.Equals(pair.Key, battle.SelectedUnitId, StringComparison.OrdinalIgnoreCase)
                    ? new Color(0.10f, 0.42f, 0.85f, 1f)
                    : option != null && !option.CanDeploy
                        ? new Color(0.28f, 0.16f, 0.13f, 1f)
                        : new Color(0.20f, 0.16f, 0.10f, 1f);
            }

            if (!string.IsNullOrWhiteSpace(_selectedUnitId))
                RefreshInfo(FindOption(snapshot, _selectedUnitId));
        }

        private void CreateDeploymentCard(string unitId, BattleSliceUnitOptionSnapshot option)
        {
            if (deploymentContent == null) return;
            var card = new GameObject("Deploy_" + unitId, typeof(RectTransform), typeof(Image),
                typeof(LayoutElement), typeof(FormalBattleDeploymentDragItem));
            card.transform.SetParent(deploymentContent, false);
            var layout = card.GetComponent<LayoutElement>();
            layout.preferredWidth = 168f;
            layout.preferredHeight = 104f;
            var image = card.GetComponent<Image>();
            image.color = new Color(0.20f, 0.16f, 0.10f, 1f);
            image.raycastTarget = true;
            var label = CreateText(card.transform, "Label",
                $"{unitId}  {(option?.Name ?? "部署单位")}\n费用  {(option?.ResourceCost ?? 0)}\n拖入战场部署",
                17f, TextAlignmentOptions.TopLeft, Color.white);
            Stretch(label.rectTransform, 10f);
            card.GetComponent<FormalBattleDeploymentDragItem>().Configure(this, unitId);
            _deploymentCards[unitId] = image;
        }

        private void RefreshAuthorizationChoices(BattleSliceSnapshot snapshot)
        {
            var signature = snapshot.AuthorizationState + "|" + string.Join("|", snapshot.AuthorizationCandidates);
            if (signature == _authorizationSignature) return;
            _authorizationSignature = signature;
            ClearChildren(authorizationChoiceContent);
            foreach (var candidate in snapshot.AuthorizationCandidates)
            {
                var option = FindOption(snapshot, candidate);
                CreateChoiceButton(candidate, option);
            }
        }

        private void CreateChoiceButton(string unitId, BattleSliceUnitOptionSnapshot option)
        {
            if (authorizationChoiceContent == null) return;
            var item = new GameObject("Choice_" + unitId, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            item.transform.SetParent(authorizationChoiceContent, false);
            item.GetComponent<Image>().color = Navy;
            item.GetComponent<LayoutElement>().preferredHeight = 92f;
            var label = CreateText(item.transform, "Label",
                $"{option?.Name ?? unitId}  [{unitId}]\n{DescribeMode(option?.DeploymentMode ?? DeploymentMode.StandardUnit)} · 费用 {option?.ResourceCost ?? 0}",
                19f, TextAlignmentOptions.Left, Color.white);
            Stretch(label.rectTransform, 18f);
            item.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (battle != null && battle.AcceptAuthorization(unitId))
                {
                    SelectUnit(unitId);
                    if (feedbackText != null) feedbackText.text = "已获得部署授权：" + (option?.Name ?? unitId);
                }
            });
        }

        private void RequestAuthorization()
        {
            if (battle != null && battle.RequestAuthorization() && feedbackText != null)
                feedbackText.text = "请选择一个单位；战斗仍在继续";
        }

        public void SelectUnit(string unitId)
        {
            if (_infoVisible && string.Equals(_selectedUnitId, unitId, StringComparison.OrdinalIgnoreCase))
            {
                _infoVisible = false;
                return;
            }

            EnsureUnitSelected(unitId);
        }

        private void EnsureUnitSelected(string unitId)
        {
            var snapshot = battle?.CurrentSnapshot;
            if (snapshot == null || !battle.SelectDeploymentUnit(unitId)) return;
            _selectedUnitId = unitId;
            _infoVisible = true;
            RefreshInfo(FindOption(snapshot, unitId));
        }

        private void RefreshInfo(BattleSliceUnitOptionSnapshot option)
        {
            if (option == null) return;
            if (unitNameText != null) unitNameText.text = option.Name;
            if (unitTypeText != null) unitTypeText.text = DescribeMode(option.DeploymentMode);
            if (unitStatsText != null)
                unitStatsText.text = $"部署费用    {option.ResourceCost}\n部署冷却    {option.DeploymentCooldownSeconds:0.0}s\n剩余冷却    {option.RemainingCooldownSeconds:0.0}s";
            if (unitEffectText != null)
                unitEffectText.text = string.IsNullOrWhiteSpace(option.Effect) ? "无特殊效果" : option.Effect;
        }

        public void BeginUnitDrag(string unitId, Vector2 screenPosition)
        {
            EnsureUnitSelected(unitId);
            _dragUnitId = unitId;
            battle?.SetRuntimeUiPointerBlocked(true);
            if (dragGhost != null) dragGhost.gameObject.SetActive(true);
            if (dragGhostText != null) dragGhostText.text = unitId + "\n释放以部署";
            ContinueUnitDrag(screenPosition);
        }

        public void ContinueUnitDrag(Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(_dragUnitId)) return;
            if (dragGhost != null) dragGhost.position = screenPosition;
            var validTarget = !IsPointerOverBlockingUi(screenPosition) && battle != null &&
                              battle.PreviewDeploymentAtScreenPoint(screenPosition);
            if (dragGhost != null)
                dragGhost.GetComponent<Image>().color = validTarget
                    ? new Color(0.05f, 0.55f, 0.95f, 0.84f)
                    : new Color(0.75f, 0.12f, 0.10f, 0.84f);
        }

        public void EndUnitDrag(Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(_dragUnitId)) return;
            var response = IsPointerOverBlockingUi(screenPosition)
                ? null
                : battle?.DeploySelectedAtScreenPoint(screenPosition);
            if (feedbackText != null)
                feedbackText.text = response != null && response.Succeeded
                    ? "部署完成：" + _dragUnitId
                    : "无法部署到该位置" + (response == null ? string.Empty : " · " + response.Failure);
            _dragUnitId = null;
            battle?.SetRuntimeUiPointerBlocked(false);
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        private bool IsPointerOverBlockingUi(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            return hits.Any(hit => dragGhost == null ||
                                   (hit.gameObject != dragGhost.gameObject &&
                                    !hit.gameObject.transform.IsChildOf(dragGhost)));
        }

        private TMP_Text CreateText(Transform parent, string objectName, string value, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static BattleSliceUnitOptionSnapshot FindOption(BattleSliceSnapshot snapshot, string id)
        {
            return snapshot?.UnitOptions?.FirstOrDefault(option =>
                string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static string DescribeMode(DeploymentMode mode)
        {
            switch (mode)
            {
                case DeploymentMode.Building: return "建筑单位";
                case DeploymentMode.SupportEffect: return "支援效果";
                case DeploymentMode.TerrainBuild: return "地形建造";
                case DeploymentMode.OuterRing: return "外围部署";
                default: return "作战单位";
            }
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (var index = root.childCount - 1; index >= 0; index--)
                Destroy(root.GetChild(index).gameObject);
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
