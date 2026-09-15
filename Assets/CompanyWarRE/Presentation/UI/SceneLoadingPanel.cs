using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyWarRE.Presentation.UI
{
    /// <summary>
    /// Full-screen, persistent loading transition shared by every formal scene change.
    /// The destination is only activated after Unity has finished loading it and the
    /// randomly selected presentation time has elapsed.
    /// </summary>
    public sealed class SceneLoadingPanel : MonoBehaviour
    {
        private const string BackgroundResourcePath = "CompanyWarRE/UI/SceneLoadingBackground";
        private const string FontResourcePath = "Fonts & Materials/MSYH SDF";
        private const string LastLevelKey = "CompanyWar.LastLevel";
        private const float MinimumDisplaySeconds = 2f;
        private const float MaximumDisplaySeconds = 3f;

        private static SceneLoadingPanel _instance;

        private CanvasGroup _canvasGroup;
        private TMP_Text _percentText;
        private TMP_Text _statusText;
        private TMP_Text _operationText;
        private TMP_Text[] _stepTexts;
        private Image[] _stepMarks;
        private SceneLoadingRingGraphic _ring;
        private bool _isLoading;

        public static bool IsLoading => _instance != null && _instance._isLoading;

        /// <summary>Starts a guarded asynchronous scene transition.</summary>
        public static bool LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || IsLoading)
            {
                return false;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene '{sceneName}' is not available in Build Settings.");
                return false;
            }

            EnsureInstance().StartCoroutine(_instance.LoadRoutine(sceneName, mode));
            return true;
        }

        private static SceneLoadingPanel EnsureInstance()
        {
            if (_instance != null) return _instance;

            var root = new GameObject("[Runtime] SceneLoadingPanel");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<SceneLoadingPanel>();
            _instance.BuildView();
            return _instance;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private IEnumerator LoadRoutine(string sceneName, LoadSceneMode mode)
        {
            _isLoading = true;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            var levelId = PlayerPrefs.GetString(LastLevelKey, "L01");
            var enteringBattle = !string.Equals(sceneName, "Boot", StringComparison.OrdinalIgnoreCase);
            _operationText.text = enteringBattle ? $"{levelId} 战区载入" : "指挥中心载入";
            _statusText.text = enteringBattle ? "正在部署战区数据…" : "正在返回战略终端…";
            SetProgress(0f);

            var displaySeconds = UnityEngine.Random.Range(MinimumDisplaySeconds, MaximumDisplaySeconds);
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, mode);
            }
            catch (Exception exception)
            {
                Fail(sceneName, exception.Message);
                yield break;
            }

            if (operation == null)
            {
                Fail(sceneName, "Unity did not create an AsyncOperation.");
                yield break;
            }

            operation.allowSceneActivation = false;
            var elapsed = 0f;
            while (operation.progress < 0.9f || elapsed < displaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var loadProgress = Mathf.Clamp01(operation.progress / 0.9f);
                var presentationProgress = Mathf.Clamp01(elapsed / displaySeconds);
                var visibleProgress = Mathf.Min(0.99f, Mathf.Max(loadProgress, presentationProgress * 0.94f));
                SetProgress(visibleProgress);
                yield return null;
            }

            SetProgress(1f);
            _statusText.text = "部署完成，正在进入战区…";
            yield return new WaitForSecondsRealtime(0.12f);
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;

            var fadeElapsed = 0f;
            const float fadeSeconds = 0.22f;
            while (fadeElapsed < fadeSeconds)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeSeconds);
                yield return null;
            }

            _isLoading = false;
            Destroy(gameObject);
        }

        private void Fail(string sceneName, string reason)
        {
            _isLoading = false;
            Debug.LogError($"Failed to load scene '{sceneName}': {reason}", this);
            Destroy(gameObject);
        }

        private void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            _ring.Progress = value;
            _percentText.text = $"{Mathf.RoundToInt(value * 100f)}%";

            var activeStep = value < 0.25f ? 0 : value < 0.5f ? 1 : value < 0.72f ? 2 : value < 0.94f ? 3 : 4;
            for (var i = 0; i < _stepTexts.Length; i++)
            {
                var active = i <= activeStep;
                _stepTexts[i].color = active ? Palette.Navy : Palette.Muted;
                _stepMarks[i].color = active ? Palette.Gold : Palette.MarkIdle;
            }
        }

        private void BuildView()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            var background = CreateUI<RawImage>("Background", transform);
            Stretch(background.rectTransform);
            background.color = Color.white;
            background.raycastTarget = true;
            background.texture = Resources.Load<Texture2D>(BackgroundResourcePath);
            if (background.texture == null)
            {
                background.color = new Color(0.12f, 0.17f, 0.22f, 1f);
                Debug.LogWarning($"Loading background not found at Resources/{BackgroundResourcePath}.", this);
            }
            else
            {
                var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)background.texture.width / background.texture.height;
            }

            var atmosphere = CreateImage("Atmosphere", transform, new Color(0.03f, 0.08f, 0.14f, 0.16f));
            Stretch(atmosphere.rectTransform);

            BuildOuterBranding(transform);
            var panel = CreateImage("CommandPanel", transform, Palette.Panel);
            SetRect(panel.rectTransform, new Vector2(0.19f, 0.10f), new Vector2(0.81f, 0.89f));
            AddOutline(panel.gameObject, new Color(0.74f, 0.52f, 0.20f, 0.92f), new Vector2(2f, -2f));
            BuildCornerBrackets(panel.transform);

            CreateText("SceneLoading", panel.transform, "///  SCENE LOADING", 15f,
                TextAlignmentOptions.Left, Palette.Gold, new Vector2(0.04f, 0.93f), new Vector2(0.33f, 0.98f), FontStyles.Normal);
            CreateText("System", panel.transform, "TACTICAL SYSTEM  ///", 14f,
                TextAlignmentOptions.Right, Palette.Gold, new Vector2(0.67f, 0.93f), new Vector2(0.96f, 0.98f), FontStyles.Normal);

            _operationText = CreateText("Operation", panel.transform, "L01 战区载入", 62f,
                TextAlignmentOptions.Center, Palette.Navy, new Vector2(0.20f, 0.80f), new Vector2(0.80f, 0.93f), FontStyles.Bold);
            CreateText("LoadingScene", panel.transform, "—  L O A D I N G   S C E N E  —", 15f,
                TextAlignmentOptions.Center, Palette.Gold, new Vector2(0.30f, 0.76f), new Vector2(0.70f, 0.82f), FontStyles.Normal);

            BuildSteps(panel.transform);
            BuildProgress(panel.transform);
            BuildTelemetry(panel.transform);
        }

        private void BuildOuterBranding(Transform parent)
        {
            CreateText("Brand", parent, "///   S.C.U.  TACTICAL SYSTEM", 14f, TextAlignmentOptions.Left,
                new Color(0.96f, 0.92f, 0.80f, 0.92f), new Vector2(0.022f, 0.945f), new Vector2(0.32f, 0.99f), FontStyles.Normal);
            CreateText("Motto", parent, "WORLD STABILITY\nOUR COMMON MISSION   ///", 12f,
                TextAlignmentOptions.Right, new Color(0.12f, 0.17f, 0.25f, 0.75f),
                new Vector2(0.73f, 0.935f), new Vector2(0.975f, 0.99f), FontStyles.Normal);
            CreateText("SideCopy", parent, "—\n以理性\n构筑更安全的未来。\n\nFOR A SAFER\nTOMORROW", 15f,
                TextAlignmentOptions.Left, new Color(0.97f, 0.94f, 0.86f, 0.92f),
                new Vector2(0.022f, 0.28f), new Vector2(0.16f, 0.57f), FontStyles.Normal);
        }

        private void BuildSteps(Transform parent)
        {
            var labels = new[] { "加载地图资源", "初始化单位数据", "构建战场环境", "同步战术系统", "完成最终检查" };
            _stepTexts = new TMP_Text[labels.Length];
            _stepMarks = new Image[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                var yMax = 0.61f - i * 0.065f;
                _stepMarks[i] = CreateImage("StepMark" + i, parent, Palette.MarkIdle);
                SetRect(_stepMarks[i].rectTransform, new Vector2(0.06f, yMax - 0.024f), new Vector2(0.072f, yMax));
                _stepTexts[i] = CreateText("Step" + i, parent, labels[i], 17f, TextAlignmentOptions.Left,
                    Palette.Muted, new Vector2(0.09f, yMax - 0.04f), new Vector2(0.31f, yMax + 0.014f), FontStyles.Normal);
            }
        }

        private void BuildProgress(Transform parent)
        {
            var ringObject = new GameObject(
                "ProgressRing",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(SceneLoadingRingGraphic));
            ringObject.transform.SetParent(parent, false);
            var ringRect = (RectTransform)ringObject.transform;
            SetRect(ringRect, new Vector2(0.35f, 0.27f), new Vector2(0.65f, 0.70f));
            _ring = ringObject.GetComponent<SceneLoadingRingGraphic>();
            _ring.raycastTarget = false;

            CreateText("Emblem", ringObject.transform, "◆", 72f, TextAlignmentOptions.Center,
                new Color(0.64f, 0.43f, 0.16f, 0.85f), new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), FontStyles.Bold);
            CreateText("LoadingTitle", parent, "场 景 加 载 中", 32f, TextAlignmentOptions.Center,
                Palette.Navy, new Vector2(0.31f, 0.18f), new Vector2(0.69f, 0.28f), FontStyles.Bold);
            _statusText = CreateText("Status", parent, "正在部署战区数据…", 18f, TextAlignmentOptions.Center,
                Palette.Navy, new Vector2(0.31f, 0.13f), new Vector2(0.69f, 0.20f), FontStyles.Normal);
            CreateText("Async", parent, "A S Y N C   L O A D I N G", 12f, TextAlignmentOptions.Center,
                Palette.Muted, new Vector2(0.34f, 0.10f), new Vector2(0.66f, 0.15f), FontStyles.Normal);
            _percentText = CreateText("Percent", parent, "0%", 24f, TextAlignmentOptions.Center,
                Palette.Navy, new Vector2(0.41f, 0.055f), new Vector2(0.59f, 0.12f), FontStyles.Bold);
        }

        private void BuildTelemetry(Transform parent)
        {
            CreateText("Telemetry", parent,
                "│ S.C.U. SYSTEM  ///\n\nMAP DATA\nUNIT DATA\nENVIRONMENT\nTACTICS INIT\nSYSTEM CHECK\n\n—\n\nPREPARING\nTHE NEXT\nOPERATION.",
                13f, TextAlignmentOptions.Left, Palette.Muted,
                new Vector2(0.81f, 0.20f), new Vector2(0.96f, 0.66f), FontStyles.Normal);
        }

        private static void BuildCornerBrackets(Transform parent)
        {
            var corners = new[]
            {
                ("TL-H", new Vector2(0.012f, 0.975f), new Vector2(0.07f, 0.98f)),
                ("TL-V", new Vector2(0.012f, 0.91f), new Vector2(0.016f, 0.98f)),
                ("TR-H", new Vector2(0.93f, 0.975f), new Vector2(0.988f, 0.98f)),
                ("TR-V", new Vector2(0.984f, 0.91f), new Vector2(0.988f, 0.98f)),
                ("BL-H", new Vector2(0.012f, 0.02f), new Vector2(0.07f, 0.025f)),
                ("BL-V", new Vector2(0.012f, 0.02f), new Vector2(0.016f, 0.09f)),
                ("BR-H", new Vector2(0.93f, 0.02f), new Vector2(0.988f, 0.025f)),
                ("BR-V", new Vector2(0.984f, 0.02f), new Vector2(0.988f, 0.09f))
            };
            foreach (var corner in corners)
            {
                var line = CreateImage(corner.Item1, parent, Palette.Gold);
                SetRect(line.rectTransform, corner.Item2, corner.Item3);
            }
        }

        private static T CreateUI<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateUI<Image>(name, parent);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = Resources.Load<TMP_FontAsset>(FontResourcePath) ?? TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            SetRect(text.rectTransform, anchorMin, anchorMax);
            return text;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static class Palette
        {
            public static readonly Color Navy = new Color(0.025f, 0.10f, 0.24f, 1f);
            public static readonly Color Gold = new Color(0.73f, 0.49f, 0.16f, 1f);
            public static readonly Color Muted = new Color(0.39f, 0.39f, 0.37f, 0.72f);
            public static readonly Color MarkIdle = new Color(0.74f, 0.66f, 0.50f, 0.32f);
            public static readonly Color Panel = new Color(1f, 0.975f, 0.86f, 0.91f);
        }
    }

    /// <summary>Procedural UGUI ring, avoiding a dependency on extra sprite assets.</summary>
    public sealed class SceneLoadingRingGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField] private float thickness = 16f;
        [SerializeField] private int segments = 96;

        public float Progress
        {
            get => progress;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(progress, clamped)) return;
                progress = clamped;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.45f;
            var center = rect.center;
            DrawArc(vertexHelper, center, radius, thickness, 0f, 1f,
                new Color(0.77f, 0.70f, 0.56f, 0.25f));
            DrawArc(vertexHelper, center, radius, thickness, 0f, progress,
                new Color(0.73f, 0.49f, 0.16f, 1f));
            DrawArc(vertexHelper, center, radius - thickness * 1.8f, 3f, 0f, 1f,
                new Color(0.16f, 0.20f, 0.25f, 0.18f));
        }

        private void DrawArc(VertexHelper vh, Vector2 center, float radius, float width,
            float start, float length, Color32 arcColor)
        {
            var count = Mathf.Clamp(Mathf.CeilToInt(segments * length), 1, segments);
            var inner = Mathf.Max(0f, radius - width);
            var baseIndex = vh.currentVertCount;
            for (var i = 0; i <= count; i++)
            {
                var t = start + length * i / count;
                var angle = (t * Mathf.PI * 2f) + Mathf.PI * 0.5f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, arcColor, Vector2.zero);
                vh.AddVert(center + direction * inner, arcColor, Vector2.zero);
            }

            for (var i = 0; i < count; i++)
            {
                var index = baseIndex + i * 2;
                vh.AddTriangle(index, index + 2, index + 1);
                vh.AddTriangle(index + 2, index + 3, index + 1);
            }
        }
    }
}
