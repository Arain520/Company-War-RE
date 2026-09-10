using CompanyWarRE.Domain;
using CompanyWarRE.Presentation;
using CompanyWarRE.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CompanyWar.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class FormalCowUiBootstrap : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanelPrefab;
        [SerializeField] private GameObject levelSelectPanelPrefab;
        [SerializeField] private GameObject battlePanelPrefab;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private bool hideImmediateModeDebugHud = true;

        private BattleSliceController _controller;
        private GameObject _menuPanel;
        private GameObject _levelSelectPanel;
        private GameObject _battlePanel;

        public bool IsReady =>
            _controller != null && _menuPanel != null &&
            _levelSelectPanel != null && _battlePanel != null;

        private void Start()
        {
            _controller = GetComponent<BattleSliceController>() ??
                          FindObjectOfType<BattleSliceController>();
            if (_controller == null)
            {
                Debug.LogError("Formal Cow UI requires BattleSliceController.", this);
                enabled = false;
                return;
            }

            if (FindObjectOfType<FormalBattleHudController>(true) != null)
            {
                _controller.SetRuntimeHudVisible(false);
                _controller.SetRuntimeUiPointerBlocked(false);
                enabled = false;
                return;
            }

            if (menuPanelPrefab == null || levelSelectPanelPrefab == null || battlePanelPrefab == null)
            {
                Debug.LogError("Formal Cow UI prefab references are incomplete.", this);
                enabled = false;
                return;
            }

            var canvasRoot = new GameObject(
                "FormalCowUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem(transform);
            _menuPanel = InstantiatePanel(menuPanelPrefab, canvasRoot.transform, false);
            _levelSelectPanel = InstantiatePanel(levelSelectPanelPrefab, canvasRoot.transform, true);
            _battlePanel = InstantiatePanel(battlePanelPrefab, canvasRoot.transform, true);
            CowUiTypography.ApplyTo(canvasRoot);
            _controller.SetRuntimeHudVisible(!hideImmediateModeDebugHud);
            RefreshVisibility();
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            _controller.SetRuntimeUiPointerBlocked(
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.SetRuntimeUiPointerBlocked(false);
                _controller.SetRuntimeHudVisible(true);
            }
        }

        private void RefreshVisibility()
        {
            var flow = _controller.CurrentFlow;
            if (flow == null)
            {
                return;
            }

            SetActive(_menuPanel,
                flow.Screen == FormalFlowScreen.MainMenu || flow.Screen == FormalFlowScreen.Paused);
            SetActive(_levelSelectPanel, flow.Screen == FormalFlowScreen.LevelSelect);
            SetActive(_battlePanel,
                flow.Screen == FormalFlowScreen.Battle ||
                flow.Screen == FormalFlowScreen.Paused ||
                flow.Screen == FormalFlowScreen.Result);
        }

        private static GameObject InstantiatePanel(
            GameObject prefab,
            Transform parent,
            bool stretch)
        {
            var instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            if (instance.transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                if (stretch)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
                else
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(448f, 359f);
                }
            }

            return instance;
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject(
                "FormalCowUIEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }

    internal static class CowUiTypography
    {
        private const string FontResourcePath = "CowLegacy/_Game/Art/Fonts/Default SDF";
        private const string FallbackFontResourcePath = "CowLegacy/Fonts/ChineseUIFont";
        private static TMP_FontAsset _font;
        private static TMP_FontAsset _fallbackFont;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<TMP_FontAsset>(FontResourcePath);
                    if (_font != null)
                    {
                        // Chinese unit names and status text can exceed a single 1024 atlas.
                        _font.isMultiAtlasTexturesEnabled = true;
                        var fallback = FallbackFont;
                        if (fallback != null && !_font.fallbackFontAssetTable.Contains(fallback))
                        {
                            _font.fallbackFontAssetTable.Add(fallback);
                        }
                    }
                }

                return _font;
            }
        }

        private static TMP_FontAsset FallbackFont
        {
            get
            {
                if (_fallbackFont != null)
                {
                    return _fallbackFont;
                }

                var source = Resources.Load<Font>(FallbackFontResourcePath);
                if (source == null)
                {
                    return null;
                }

                _fallbackFont = TMP_FontAsset.CreateFontAsset(
                    source,
                    64,
                    8,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                _fallbackFont.name = "CompanyWarRE Chinese UI Fallback";
                _fallbackFont.isMultiAtlasTexturesEnabled = true;
                return _fallbackFont;
            }
        }

        public static void ApplyTo(GameObject root)
        {
            if (root == null || Font == null)
            {
                return;
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                ApplyTo(text);
            }
        }

        public static void ApplyTo(TMP_Text text)
        {
            if (text == null || Font == null)
            {
                return;
            }

            text.font = Font;
            text.extraPadding = true;
            EnsureCharacters(text.text);
        }

        public static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            ApplyTo(text);
            EnsureCharacters(value);
            text.text = value ?? string.Empty;
        }

        private static void EnsureCharacters(string value)
        {
            if (Font == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            Font.TryAddCharacters(value, out var missingCharacters);
            if (!string.IsNullOrEmpty(missingCharacters) && FallbackFont != null)
            {
                FallbackFont.TryAddCharacters(missingCharacters, out _);
            }
        }
    }
}
