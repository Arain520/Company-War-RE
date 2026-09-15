using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        [SerializeField] private Button eradicateButton;
        [SerializeField] private Image eradicateBackground;
        [SerializeField] private TMP_Text eradicateLabel;
        [SerializeField] private Button pauseButton;
        [Header("Pause Menu")]
        [SerializeField] private RectTransform pausePanel;
        [SerializeField] private Button pauseContinueButton;
        [SerializeField] private Button pauseMainMenuButton;
        [SerializeField] private Button pauseMailButton;
        [SerializeField] private GameObject pauseMailPanel;
        [SerializeField] private Button pauseMailCloseButton;
        [Header("Character Portrait")]
        [SerializeField] private RectTransform characterPortraitViewport;
        [SerializeField] private Image characterPortraitImage;
        [SerializeField, Range(1, 4)] private int characterPortraitPreviewLevel = 1;
        [Tooltip("依次对应 CH_1 到 CH_4；留空时自动从 Resources/Character 加载")]
        [SerializeField] private Sprite[] characterPortraitSprites = new Sprite[4];
        [Tooltip("四级立绘共用的位置")]
        [SerializeField] private Vector2 characterPortraitPosition = new Vector2(0f, 120f);
        [Tooltip("四级立绘共用的等比缩放")]
        [SerializeField, Range(0.25f, 4f)] private float characterPortraitScale = 1f;
        [Tooltip("遮罩相对边框内沿的距离")]
        [SerializeField, Min(0f)] private float characterPortraitMaskInset = 3f;
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
        [SerializeField] private Image authorizationModalBackdrop;
        [SerializeField] private Button authorizationCloseButton;
        [SerializeField] private CanvasGroup authorizationChoiceCanvasGroup;
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
        private Vector2 _dragScreenPosition;
        private string _selectedUnitId;
        private bool _infoVisible;
        private bool _authorizationAutoOpenedForCurrentOffer;
        private int _eradicationRevision = -1;
        private int _displayedPortraitLevel;
        private Image _unitModelImage;
        private string _displayedUnitModelId;

        private static readonly Color Navy = new Color(0.025f, 0.075f, 0.15f, 0.98f);
        private static readonly Color Gold = new Color(0.72f, 0.48f, 0.16f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.80f, 0.98f);
        private const string BootRouteKey = "CompanyWar.BootRoute";

        public void Configure(
            BattleSliceController controller, TMP_FontAsset mainFont,
            TMP_Text assault, TMP_Text resource, TMP_Text phase, Button eradicate, Button pause,
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
            eradicateButton = eradicate;
            eradicateBackground = eradicate == null ? null : eradicate.targetGraphic as Image;
            eradicateLabel = eradicate == null ? null : eradicate.GetComponentInChildren<TMP_Text>(true);
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

        public void ConfigureCharacterPortrait(
            RectTransform viewport, Image portrait, IReadOnlyList<Sprite> stageSprites)
        {
            characterPortraitViewport = viewport;
            characterPortraitImage = portrait;
            EnsurePortraitSprites();
            if (stageSprites == null) return;
            for (var index = 0; index < characterPortraitSprites.Length && index < stageSprites.Count; index++)
            {
                if (stageSprites[index] != null) characterPortraitSprites[index] = stageSprites[index];
            }
        }

        public void ConfigurePauseMenu(
            RectTransform panel, Button continueButton, Button mainMenuButton,
            Button mailButton, GameObject mailPanel, Button mailCloseButton)
        {
            pausePanel = panel;
            pauseContinueButton = continueButton;
            pauseMainMenuButton = mainMenuButton;
            pauseMailButton = mailButton;
            pauseMailPanel = mailPanel;
            pauseMailCloseButton = mailCloseButton;
        }

        private void Awake()
        {
            if (battle == null) battle = FindObjectOfType<BattleSliceController>();
            ResolveCharacterPortraitReferences();
            ResolveEradicationReferences();
            ResolveAuthorizationModalReferences();
            battle?.SetRuntimeHudVisible(false);
            NormalizeAuthorizationSlot();
            eradicateButton?.onClick.AddListener(ToggleEradication);
            pauseButton?.onClick.AddListener(TogglePauseMenu);
            pauseContinueButton?.onClick.AddListener(ContinueGame);
            pauseMainMenuButton?.onClick.AddListener(ReturnToMainMenu);
            pauseMailButton?.onClick.AddListener(TogglePauseMail);
            pauseMailCloseButton?.onClick.AddListener(TogglePauseMail);
            authorizationButton?.onClick.AddListener(RequestAuthorization);
            authorizationCloseButton?.onClick.AddListener(CloseAuthorizationChoice);
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            if (authorizationChoicePanel != null)
            {
                authorizationChoicePanel.anchoredPosition = Vector2.zero;
                authorizationChoicePanel.localScale = Vector3.one * 0.9f;
                authorizationChoicePanel.gameObject.SetActive(false);
            }
            if (authorizationModalBackdrop != null) authorizationModalBackdrop.gameObject.SetActive(false);
            if (pausePanel != null) pausePanel.gameObject.SetActive(false);
            if (pauseMailPanel != null) pauseMailPanel.SetActive(false);
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
            if (battle != null && battle.IsBattleIntroPlaying) return;
            if (!string.IsNullOrWhiteSpace(_dragUnitId))
            {
                if (battle == null || !battle.CanDragDeploy) CancelUnitDrag();
                else ContinueUnitDrag(_dragScreenPosition);
            }
            var snapshot = battle?.CurrentSnapshot;
            if (snapshot == null) return;

            if (clockText != null) clockText.text = DateTime.Now.ToString("HH:mm");
            if (assaultText != null)
                assaultText.text = $"攻坚积分  {snapshot.AssaultScore:000}/{snapshot.RequiredAssaultScore:000}";
            if (resourceText != null) resourceText.text = $"物资  {snapshot.Resources:000}";
            if (phaseText != null)
                phaseText.text = "当前阶段：" + (string.IsNullOrWhiteSpace(snapshot.CurrentWaveStage)
                    ? snapshot.WaveIndex.ToString() : snapshot.CurrentWaveStage);

            RefreshCharacterPortrait(snapshot.AcceptedAuthorizationCount);
            RefreshPauseMenu();
            RefreshAuthorizationProgress(snapshot);
            RefreshEradication(snapshot);
            RefreshDeploymentCards(snapshot);
            RefreshAuthorizationChoices(snapshot);
            AnimatePanels(snapshot);
        }

        private void Update()
        {
            if (battle == null) return;
            if (battle.IsBattleIntroPlaying) return;
            battle.SetRuntimeHudVisible(false);
            if (battle.IsAuthorizationUiPaused)
            {
                battle.SetRuntimeUiPointerBlocked(true);
                if (Input.GetKeyDown(KeyCode.Escape)) CloseAuthorizationChoice();
                return;
            }
            if (!string.IsNullOrWhiteSpace(_dragUnitId))
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) CancelUnitDrag();
                return;
            }
            battle.SetRuntimeUiPointerBlocked(
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        private void OnDisable()
        {
            CancelUnitDrag();
            battle?.CancelEradication();
            battle?.SetAuthorizationUiPaused(false);
            battle?.SetRuntimeUiPointerBlocked(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelUnitDrag();
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
            var onBattleScreen = battle?.CurrentFlow == null ||
                                 battle.CurrentFlow.Screen == FormalFlowScreen.Battle;
            if (available && onBattleScreen && !_authorizationAutoOpenedForCurrentOffer)
            {
                _authorizationAutoOpenedForCurrentOffer = true;
                RequestAuthorization();
            }
            else if (snapshot.AuthorizationState != AuthorizationState.Available &&
                     snapshot.AuthorizationState != AuthorizationState.Choosing)
            {
                _authorizationAutoOpenedForCurrentOffer = false;
            }
        }

        private void ToggleEradication()
        {
            if (battle == null) return;
            battle.ToggleEradicationMode();
            PublishEradicationMessage();
        }

        private void TogglePauseMenu()
        {
            if (battle == null || battle.IsAuthorizationUiPaused) return;
            battle.ToggleFormalPause();
            RefreshPauseMenu();
        }

        private void ContinueGame()
        {
            if (battle?.CurrentFlow?.Screen != FormalFlowScreen.Paused) return;
            if (pauseMailPanel != null) pauseMailPanel.SetActive(false);
            battle.ToggleFormalPause();
            RefreshPauseMenu();
        }

        private void ReturnToMainMenu()
        {
            PlayerPrefs.DeleteKey(BootRouteKey);
            PlayerPrefs.Save();
            SceneLoadingPanel.LoadScene("Boot", LoadSceneMode.Single);
        }

        private void TogglePauseMail()
        {
            if (pauseMailPanel == null) return;
            pauseMailPanel.SetActive(!pauseMailPanel.activeSelf);
        }

        private void RefreshPauseMenu()
        {
            if (pausePanel == null) return;
            var visible = battle?.CurrentFlow?.Screen == FormalFlowScreen.Paused;
            if (pausePanel.gameObject.activeSelf != visible) pausePanel.gameObject.SetActive(visible);
            if (!visible && pauseMailPanel != null && pauseMailPanel.activeSelf)
                pauseMailPanel.SetActive(false);
        }

        private void RefreshEradication(BattleSliceSnapshot snapshot)
        {
            if (eradicateButton == null) return;
            eradicateButton.interactable = snapshot.BattleState == BattleState.Running;

            if (eradicateBackground != null)
            {
                eradicateBackground.color = battle != null && battle.IsEradicationMode
                    ? Color.Lerp(
                        new Color(0.58f, 0.25f, 0.04f, 1f),
                        new Color(0.95f, 0.58f, 0.08f, 1f),
                        0.5f + Mathf.Sin(Time.unscaledTime * 5f) * 0.5f)
                    : battle != null && battle.HasErasableBuildings
                        ? new Color(0.52f, 0.08f, 0.06f, 1f)
                        : new Color(0.20f, 0.22f, 0.23f, 0.92f);
            }

            if (eradicateLabel != null) eradicateLabel.gameObject.SetActive(false);

            PublishEradicationMessage();
        }

        private void PublishEradicationMessage()
        {
            if (battle == null || battle.EradicationRevision == _eradicationRevision) return;
            _eradicationRevision = battle.EradicationRevision;
            if (feedbackText != null) feedbackText.text = battle.EradicationMessage;
        }

        private void ResolveEradicationReferences()
        {
            if (eradicateButton == null)
                eradicateButton = GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => string.Equals(
                        button.gameObject.name, "Eradicate", StringComparison.Ordinal));
            if (eradicateButton == null) return;
            if (eradicateBackground == null) eradicateBackground = eradicateButton.targetGraphic as Image;
            if (eradicateLabel == null) eradicateLabel = eradicateButton.GetComponentInChildren<TMP_Text>(true);
            if (eradicateLabel != null) eradicateLabel.gameObject.SetActive(false);
            EnsureShovelIcon(eradicateButton.transform);
        }

        private void ResolveCharacterPortraitReferences()
        {
            EnsurePortraitSprites();
            var characterPanel = GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => string.Equals(
                    rect.gameObject.name, "CharacterPlaceholder", StringComparison.Ordinal));
            if (characterPanel == null) return;

            var frameImage = characterPanel.GetComponent<Image>();
            var misplacedPortrait = frameImage != null && IsCharacterPortrait(frameImage.sprite)
                ? frameImage.sprite
                : null;
            if (misplacedPortrait != null) frameImage.sprite = null;

            var oldLabel = characterPanel.Find("CharacterLabel");
            if (oldLabel != null) oldLabel.gameObject.SetActive(false);

            if (characterPortraitViewport == null)
            {
                var existingViewport = characterPanel.Find("CharacterPortraitViewport") as RectTransform;
                if (existingViewport != null) characterPortraitViewport = existingViewport;
            }
            if (characterPortraitViewport == null)
            {
                var viewportObject = new GameObject(
                    "CharacterPortraitViewport", typeof(RectTransform), typeof(RectMask2D));
                viewportObject.transform.SetParent(characterPanel, false);
                characterPortraitViewport = (RectTransform)viewportObject.transform;
                characterPortraitViewport.anchorMin = Vector2.zero;
                characterPortraitViewport.anchorMax = Vector2.one;
            }
            else if (characterPortraitViewport.GetComponent<RectMask2D>() == null)
            {
                characterPortraitViewport.gameObject.AddComponent<RectMask2D>();
            }

            if (characterPortraitImage == null)
            {
                var existingImage = characterPortraitViewport.Find("CharacterPortrait");
                characterPortraitImage = existingImage == null ? null : existingImage.GetComponent<Image>();
            }
            if (characterPortraitImage == null)
            {
                var imageObject = new GameObject(
                    "CharacterPortrait", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
                imageObject.transform.SetParent(characterPortraitViewport, false);
                characterPortraitImage = imageObject.GetComponent<Image>();
                var rect = characterPortraitImage.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            characterPortraitViewport.anchorMin = Vector2.zero;
            characterPortraitViewport.anchorMax = Vector2.one;
            characterPortraitViewport.offsetMin = Vector2.one * characterPortraitMaskInset;
            characterPortraitViewport.offsetMax = Vector2.one * -characterPortraitMaskInset;
            characterPortraitViewport.SetAsLastSibling();
            if (characterPortraitImage.sprite == null && misplacedPortrait != null)
                characterPortraitImage.sprite = misplacedPortrait;
            characterPortraitImage.color = Color.white;
            characterPortraitImage.preserveAspect = true;
            characterPortraitImage.raycastTarget = false;
            var fitter = characterPortraitImage.GetComponent<AspectRatioFitter>() ??
                         characterPortraitImage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private static bool IsCharacterPortrait(Sprite sprite)
        {
            return sprite != null && sprite.name.StartsWith("CH_", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsurePortraitSprites()
        {
            if (characterPortraitSprites != null && characterPortraitSprites.Length == 4) return;
            var previous = characterPortraitSprites;
            characterPortraitSprites = new Sprite[4];
            if (previous == null) return;
            for (var index = 0; index < characterPortraitSprites.Length && index < previous.Length; index++)
                characterPortraitSprites[index] = previous[index];
        }

        private void RefreshCharacterPortrait(int acceptedAuthorizationCount)
        {
            var level = Mathf.Clamp(acceptedAuthorizationCount + 1, 1, 4);
            ApplyCharacterPortraitLevel(level);
        }

        private void ApplyCharacterPortraitLevel(int level)
        {
            if (characterPortraitImage == null) return;
            EnsurePortraitSprites();
            level = Mathf.Clamp(level, 1, 4);
            var sprite = characterPortraitSprites[level - 1] ?? Resources.Load<Sprite>($"Character/CH_{level}");
            if (sprite == null) return;

            if (_displayedPortraitLevel != level || characterPortraitImage.sprite != sprite)
            {
                _displayedPortraitLevel = level;
                characterPortraitImage.sprite = sprite;
                var fitter = characterPortraitImage.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
            characterPortraitImage.rectTransform.anchoredPosition = characterPortraitPosition;
            characterPortraitImage.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, characterPortraitScale);
        }

        private void OnValidate()
        {
            if (!UnityEngine.Application.isPlaying)
                ApplyCharacterPortraitLevel(characterPortraitPreviewLevel);
        }

        private void ResolveAuthorizationModalReferences()
        {
            if (authorizationChoicePanel == null) return;
            authorizationChoicePanel.anchorMin = new Vector2(0.25f, 0.20f);
            authorizationChoicePanel.anchorMax = new Vector2(0.75f, 0.80f);
            authorizationChoicePanel.offsetMin = Vector2.zero;
            authorizationChoicePanel.offsetMax = Vector2.zero;
            authorizationChoicePanel.anchoredPosition = Vector2.zero;
            authorizationChoiceCanvasGroup = authorizationChoicePanel.GetComponent<CanvasGroup>() ??
                                             authorizationChoicePanel.gameObject.AddComponent<CanvasGroup>();

            var parent = authorizationChoicePanel.parent;
            var backdropTransform = parent == null ? null : parent.Find("AuthorizationModalBackdrop");
            if (authorizationModalBackdrop == null && backdropTransform != null)
                authorizationModalBackdrop = backdropTransform.GetComponent<Image>();
            if (authorizationModalBackdrop == null && parent != null)
            {
                var backdrop = new GameObject("AuthorizationModalBackdrop", typeof(RectTransform), typeof(Image));
                backdrop.transform.SetParent(parent, false);
                authorizationModalBackdrop = backdrop.GetComponent<Image>();
                authorizationModalBackdrop.color = new Color(0.005f, 0.015f, 0.035f, 0.72f);
                authorizationModalBackdrop.raycastTarget = true;
                var rect = authorizationModalBackdrop.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            if (authorizationModalBackdrop != null)
                authorizationModalBackdrop.transform.SetSiblingIndex(authorizationChoicePanel.GetSiblingIndex());

            if (authorizationCloseButton == null)
            {
                var close = authorizationChoicePanel.Find("Close");
                authorizationCloseButton = close == null ? null : close.GetComponent<Button>();
            }
            if (authorizationCloseButton == null)
            {
                var closeObject = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
                closeObject.transform.SetParent(authorizationChoicePanel, false);
                var closeRect = (RectTransform)closeObject.transform;
                closeRect.anchorMin = new Vector2(0.88f, 0.87f);
                closeRect.anchorMax = new Vector2(0.96f, 0.96f);
                closeRect.offsetMin = Vector2.zero;
                closeRect.offsetMax = Vector2.zero;
                var image = closeObject.GetComponent<Image>();
                image.color = Navy;
                authorizationCloseButton = closeObject.GetComponent<Button>();
                authorizationCloseButton.targetGraphic = image;
                var label = CreateText(closeObject.transform, "Label", "X", 24f,
                    TextAlignmentOptions.Center, Color.white);
                Stretch(label.rectTransform, 2f);
            }
        }

        private static void EnsureShovelIcon(Transform buttonRoot)
        {
            if (buttonRoot == null || buttonRoot.Find("ShovelIcon") != null) return;
            var root = new GameObject("ShovelIcon", typeof(RectTransform));
            root.transform.SetParent(buttonRoot, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(54f, 54f);
            rect.anchoredPosition = Vector2.zero;

            CreateShovelPart(root.transform, "Handle", new Vector2(7f, 34f), new Vector2(2f, 5f), -35f);
            CreateShovelPart(root.transform, "Grip", new Vector2(20f, 6f), new Vector2(-9f, 18f), -35f);
            CreateShovelPart(root.transform, "Blade", new Vector2(23f, 18f), new Vector2(13f, -13f), -35f);
        }

        private static void CreateShovelPart(
            Transform parent, string name, Vector2 size, Vector2 position, float rotation)
        {
            var part = new GameObject(name, typeof(RectTransform), typeof(Image));
            part.transform.SetParent(parent, false);
            var rect = (RectTransform)part.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var image = part.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void AnimatePanels(BattleSliceSnapshot snapshot)
        {
            var available = snapshot.AuthorizationState == AuthorizationState.Available;
            var choosing = snapshot.AuthorizationState == AuthorizationState.Choosing;
            if (authorizationProgressDisplay != null)
                authorizationProgressDisplay.gameObject.SetActive(!available && !choosing);
            if (authorizationButtonPanel != null)
                authorizationButtonPanel.gameObject.SetActive(available);
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
            {
                authorizationChoicePanel.gameObject.SetActive(true);
                authorizationChoicePanel.localScale = Vector3.one * 0.9f;
                if (authorizationChoiceCanvasGroup != null) authorizationChoiceCanvasGroup.alpha = 0f;
                if (authorizationModalBackdrop != null) authorizationModalBackdrop.gameObject.SetActive(true);
                authorizationChoicePanel.SetAsLastSibling();
            }
            if (!authorizationChoicePanel.gameObject.activeSelf) return;

            var targetScale = visible ? Vector3.one : Vector3.one * 0.9f;
            authorizationChoicePanel.localScale = Vector3.Lerp(
                authorizationChoicePanel.localScale, targetScale, Time.unscaledDeltaTime * 16f);
            if (authorizationChoiceCanvasGroup != null)
                authorizationChoiceCanvasGroup.alpha = Mathf.MoveTowards(
                    authorizationChoiceCanvasGroup.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (!visible && (authorizationChoiceCanvasGroup == null || authorizationChoiceCanvasGroup.alpha <= 0.01f))
            {
                authorizationChoicePanel.gameObject.SetActive(false);
                if (authorizationModalBackdrop != null) authorizationModalBackdrop.gameObject.SetActive(false);
            }
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
                    battle.SetAuthorizationUiPaused(false);
                    SelectUnit(unitId);
                    if (feedbackText != null) feedbackText.text = "已获得部署授权：" + (option?.Name ?? unitId);
                }
            });
        }

        private void RequestAuthorization()
        {
            if (battle == null || !battle.RequestAuthorization()) return;
            _authorizationAutoOpenedForCurrentOffer = true;
            battle.SetAuthorizationUiPaused(true);
            if (feedbackText != null) feedbackText.text = "授权选择已开启，战斗进程暂停";
        }

        private void CloseAuthorizationChoice()
        {
            if (battle == null || !battle.CancelAuthorizationChoice()) return;
            battle.SetAuthorizationUiPaused(false);
            if (feedbackText != null) feedbackText.text = "已关闭授权选择，可点击部署权限按钮重新打开";
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
            RefreshUnitModelImage(option.Id);
            if (unitNameText != null) unitNameText.text = option.Name;
            if (unitTypeText != null) unitTypeText.text = DescribeMode(option.DeploymentMode);
            if (unitStatsText != null)
                unitStatsText.text = $"部署费用    {option.ResourceCost}\n部署冷却    {option.DeploymentCooldownSeconds:0.0}s\n剩余冷却    {option.RemainingCooldownSeconds:0.0}s";
            if (unitEffectText != null)
                unitEffectText.text = string.IsNullOrWhiteSpace(option.Effect) ? "无特殊效果" : option.Effect;
        }

        private void RefreshUnitModelImage(string unitId)
        {
            if (unitInfoPanel == null) return;
            if (_unitModelImage == null)
            {
                var preview = unitInfoPanel.Find("UnitPreview");
                if (preview == null) return;
                var imageObject = new GameObject("ModelImage", typeof(RectTransform), typeof(Image));
                imageObject.transform.SetParent(preview, false);
                _unitModelImage = imageObject.GetComponent<Image>();
                _unitModelImage.preserveAspect = true;
                _unitModelImage.raycastTarget = false;
                Stretch(_unitModelImage.rectTransform, 8f);
            }
            if (_displayedUnitModelId == unitId) return;
            _displayedUnitModelId = unitId;
            _unitModelImage.sprite = Resources.Load<Sprite>($"UnitPortraits/ModelPreviews/{unitId}");
            _unitModelImage.enabled = _unitModelImage.sprite != null;
            var placeholder = _unitModelImage.transform.parent.Find("PreviewPlaceholder");
            if (placeholder != null) placeholder.gameObject.SetActive(_unitModelImage.sprite == null);
        }

        public void BeginUnitDrag(string unitId, Vector2 screenPosition)
        {
            CancelUnitDrag();
            if (battle == null || !battle.BeginDeploymentPreview(unitId)) return;
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
            _dragScreenPosition = screenPosition;
            if (dragGhost != null) dragGhost.position = screenPosition;
            var blocked = IsPointerOverBlockingUi(screenPosition);
            if (blocked) battle?.HideDeploymentPreview();
            var validTarget = !blocked && battle != null && battle.PreviewDeploymentAtScreenPoint(screenPosition);
            if (dragGhostText != null) dragGhostText.text = _dragUnitId + "\n" +
                (battle != null && battle.HasDeploymentPreviewTarget ? battle.DeploymentPreviewMessage : "拖到战场以部署");
            if (dragGhost != null)
            {
                dragGhost.gameObject.SetActive(battle == null || !battle.HasDeploymentPreviewTarget);
                var ghostImage = dragGhost.GetComponent<Image>();
                if (ghostImage != null) ghostImage.color = validTarget
                    ? new Color(0.05f, 0.55f, 0.95f, 0.84f)
                    : new Color(0.75f, 0.12f, 0.10f, 0.84f);
            }
            if (feedbackText != null && battle != null && battle.HasDeploymentPreviewTarget)
                feedbackText.text = battle.DeploymentPreviewMessage;
        }

        public void EndUnitDrag(Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(_dragUnitId)) return;
            var unitId = _dragUnitId;
            var response = IsPointerOverBlockingUi(screenPosition)
                ? null
                : battle?.DeploySelectedAtScreenPoint(screenPosition);
            CancelUnitDrag();
            if (feedbackText != null)
                feedbackText.text = response != null && response.Succeeded
                    ? "部署完成：" + unitId
                    : "无法部署到该位置" + (response == null ? string.Empty : " · " + response.Failure);
        }

        public void CancelUnitDrag()
        {
            if (!string.IsNullOrWhiteSpace(_dragUnitId) && feedbackText != null) feedbackText.text = "已取消部署";
            _dragUnitId = null;
            battle?.EndDeploymentPreview();
            battle?.SetRuntimeUiPointerBlocked(false);
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        private bool IsPointerOverBlockingUi(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            return hits.Any(hit => hit.module is GraphicRaycaster && (dragGhost == null ||
                                   (hit.gameObject != dragGhost.gameObject &&
                                    !hit.gameObject.transform.IsChildOf(dragGhost))));
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
