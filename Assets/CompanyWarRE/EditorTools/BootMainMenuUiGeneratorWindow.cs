using System;
using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Presentation.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyWarRE.EditorTools
{
    public sealed class BootMainMenuUiGeneratorWindow : EditorWindow
    {
        public const string DefaultBootScenePath = "Assets/CompanyWarRE/Scenes/Boot.unity";
        public const string DefaultBackgroundPath =
            "Assets/CompanyWarRE/Content/UI/MainMenu_Background.png";
        private const string MainMenuFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/MSYH SDF.asset";
        [SerializeField] private SceneAsset bootScene;
        [SerializeField] private Texture2D background;
        [SerializeField] private bool putBootFirstInBuildSettings = true;

        [MenuItem("工具/Company War/主界面 UI 生成器", priority = 120)]
        private static void OpenWindow()
        {
            var window = GetWindow<BootMainMenuUiGeneratorWindow>(true, "主界面 UI 生成器");
            window.minSize = new Vector2(470f, 250f);
            window.Show();
        }

        private void OnEnable()
        {
            if (bootScene == null)
            {
                bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultBootScenePath);
            }

            if (background == null)
            {
                background = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultBackgroundPath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("公司战争 · Boot 主界面", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "按照参考图生成 1920×1080 响应式 UGUI。工具只替换 Boot 场景中的 " +
                "[Generated] MainMenuUI，不会删除相机、灯光或手工制作的选关 UI。",
                MessageType.Info);
            EditorGUILayout.Space(6f);

            bootScene = (SceneAsset)EditorGUILayout.ObjectField(
                "Boot 场景", bootScene, typeof(SceneAsset), false);
            background = (Texture2D)EditorGUILayout.ObjectField(
                "背景图", background, typeof(Texture2D), false);
            putBootFirstInBuildSettings = EditorGUILayout.ToggleLeft(
                "将 Boot 放到 Build Settings 第一项", putBootFirstInBuildSettings);

            EditorGUILayout.Space(12f);
            var isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            if (isPlaying)
            {
                EditorGUILayout.HelpBox("请先退出 Play 模式再生成；播放期间的场景修改不会可靠保存。", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(bootScene == null || background == null || isPlaying))
            {
                if (GUILayout.Button("生成 / 更新 Boot 主界面", GUILayout.Height(42f)))
                {
                    Generate(bootScene, background, putBootFirstInBuildSettings, true);
                }
                if (GUILayout.Button("仅刷新邮件面板（保留当前主界面）", GUILayout.Height(34f)))
                {
                    UpdateMailPanelOnly(bootScene, true);
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "开始游戏会切换到 Boot 中名为 LevelSelectCanvas 的对象；未创建时会给出提示。",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>Command-line friendly entry point using the project's standard assets.</summary>
        public static void GenerateDefaultBootMainMenu()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultBootScenePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultBackgroundPath);
            Generate(scene, texture, true, false);
        }

        private static void UpdateMailPanelOnly(SceneAsset sceneAsset, bool askToSaveCurrentScenes)
        {
            if (sceneAsset == null) throw new ArgumentNullException(nameof(sceneAsset));
            if (askToSaveCurrentScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var font = LoadMainMenuFont();
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sceneAsset), OpenSceneMode.Single);
            var root = FindSceneTransform(scene, "[Generated] MainMenuUI");
            var controller = root == null ? null : root.GetComponent<BootMainMenuController>();
            var backdrop = root == null ? null : FindChild(root, "Backdrop");
            if (root == null || controller == null || backdrop == null)
                throw new InvalidOperationException("Boot 场景缺少现有主界面、控制器或 Backdrop。");

            var oldMail = FindChild(root, "MailPanel");
            if (oldMail != null) Undo.DestroyObjectImmediate(oldMail.gameObject);
            var mail = BuildSharedMailPanel(backdrop, font, out var closeButton);
            controller.ConfigureMailPanel(mail, closeButton);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = mail;
            Debug.Log("Boot 邮件面板已刷新为可滚动版本；主界面其余内容未重建。", mail);
        }

        public static void Generate(
            SceneAsset sceneAsset,
            Texture2D backgroundTexture,
            bool updateBuildSettings,
            bool askToSaveCurrentScenes)
        {
            if (sceneAsset == null) throw new ArgumentNullException(nameof(sceneAsset));
            if (backgroundTexture == null) throw new ArgumentNullException(nameof(backgroundTexture));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请先退出 Play 模式，再生成 Boot 主界面。");
            }

            if (askToSaveCurrentScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            var backgroundPath = AssetDatabase.GetAssetPath(backgroundTexture);
            ConfigureBackgroundImporter(backgroundPath);
            var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundPath);
            if (backgroundSprite == null)
            {
                throw new InvalidOperationException($"无法以 Sprite 加载背景图：{backgroundPath}");
            }

            var font = LoadMainMenuFont();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var oldGeneratedRoot = FindSceneTransform(scene, "[Generated] MainMenuUI");
            var levelSelectRoot = FindSceneTransform(scene, "LevelSelectCanvas")?.gameObject;
            if (oldGeneratedRoot != null)
            {
                Undo.DestroyObjectImmediate(oldGeneratedRoot.gameObject);
            }

            EnsureEventSystem(scene);
            var generatedRoot = new GameObject("[Generated] MainMenuUI");
            SceneManager.MoveGameObjectToScene(generatedRoot, scene);
            var controller = generatedRoot.AddComponent<BootMainMenuController>();
            var canvas = CreateCanvas(generatedRoot.transform);
            var backdrop = CreateImage("Backdrop", canvas.transform, Color.white);
            Stretch(backdrop.rectTransform);
            backdrop.sprite = backgroundSprite;
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            var warmVeil = CreateImage(
                "WarmVeil", backdrop.transform, new Color(1f, 0.965f, 0.86f, 0.08f));
            Stretch(warmVeil.rectTransform);
            warmVeil.raycastTarget = false;

            BuildDecorativeFrame(backdrop.transform);
            BuildTitle(backdrop.transform, font);
            BuildRightSlogan(backdrop.transform, font);
            var buttons = BuildWingButtons(backdrop.transform, font);
            var footer = BuildFooter(backdrop.transform, font);
            var mail = BuildSharedMailPanel(backdrop.transform, font, out var closeMailButton);
            var hint = CreateText(
                "ContinueHint", backdrop.transform, string.Empty, font, 18f,
                TextAlignmentOptions.Center, new Color(0.18f, 0.17f, 0.15f, 0.9f));
            SetRect(hint.rectTransform, new Vector2(0.30f, 0.145f), new Vector2(0.70f, 0.18f));

            controller.Configure(
                canvas.gameObject,
                levelSelectRoot,
                mail,
                buttons.Start,
                buttons.Continue,
                buttons.Mail,
                buttons.Quit,
                closeMailButton,
                hint,
                "FormalBattle");

            var clock = generatedRoot.AddComponent<BootMainMenuClock>();
            clock.Configure(footer.Clock, footer.Date);

            EnforceGeneratedFont(generatedRoot, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (updateBuildSettings)
            {
                PutSceneFirstInBuildSettings(scenePath);
            }

            Selection.activeGameObject = generatedRoot;
            EditorGUIUtility.PingObject(generatedRoot);
            Debug.Log($"Boot 主界面已生成：{scenePath}", generatedRoot);
        }

        private static TMP_FontAsset LoadMainMenuFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainMenuFontPath);
            if (font == null)
            {
                throw new InvalidOperationException($"找不到主界面字体资源：{MainMenuFontPath}");
            }

            const string requiredCharacters =
                "策略竞争征服公司战争更高效的世界由更好的决策构建查询邮件退出开始游戏继续即时要闻" +
                "系统时间星期日一二三四五六工业节点授权变动持续扩大多家公司正在争抢稀缺资源带内部通信维护";
            if (!font.TryAddCharacters(requiredCharacters, out var missingCharacters) &&
                !string.IsNullOrEmpty(missingCharacters))
            {
                throw new InvalidOperationException(
                    $"MSYH SDF 缺少主界面所需字符：{missingCharacters}");
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        private static void EnforceGeneratedFont(GameObject generatedRoot, TMP_FontAsset font)
        {
            var texts = generatedRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                text.font = font;
                EditorUtility.SetDirty(text);
            }

            Debug.Log($"主界面字体校验通过：{texts.Length} 个 TMP 文本均使用 MSYH SDF。", generatedRoot);
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var gameObject = new GameObject(
                "MainMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            gameObject.transform.SetParent(parent, false);
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void BuildDecorativeFrame(Transform parent)
        {
            var gold = new Color(0.67f, 0.45f, 0.19f, 0.72f);
            CreateLine("FrameTop", parent, gold, new Vector2(0.012f, 0.978f), new Vector2(0.988f, 0.981f));
            CreateLine("FrameBottom", parent, gold, new Vector2(0.012f, 0.137f), new Vector2(0.988f, 0.140f));
            CreateLine("FrameLeft", parent, gold, new Vector2(0.011f, 0.137f), new Vector2(0.013f, 0.981f));
            CreateLine("FrameRight", parent, gold, new Vector2(0.987f, 0.137f), new Vector2(0.989f, 0.981f));

            var diamond = CreateImage("CenterDiamondOuter", parent, new Color(0.73f, 0.48f, 0.16f, 0.95f));
            SetRect(diamond.rectTransform, new Vector2(0.5f, 0.493f), new Vector2(0.5f, 0.493f), new Vector2(48f, 48f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.raycastTarget = false;
            var inner = CreateImage("CenterDiamondInner", diamond.transform, new Color(0.96f, 0.91f, 0.78f, 1f));
            SetRect(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(25f, 25f));
            inner.raycastTarget = false;

            CreateDecorLine("LeftDecor01", parent, new Vector2(0.428f, 0.493f), new Vector2(165f, 2f), 16f);
            CreateDecorLine("LeftDecor02", parent, new Vector2(0.416f, 0.474f), new Vector2(118f, 2f), 10f);
            CreateDecorLine("RightDecor01", parent, new Vector2(0.572f, 0.493f), new Vector2(165f, 2f), -16f);
            CreateDecorLine("RightDecor02", parent, new Vector2(0.584f, 0.474f), new Vector2(118f, 2f), -10f);
        }

        private static void BuildTitle(Transform parent, TMP_FontAsset font)
        {
            var dark = new Color(0.075f, 0.07f, 0.06f, 1f);
            var slogan = CreateText("Slogan", parent, "策略  ·  竞争  ·  征服", font, 38f,
                TextAlignmentOptions.Center, new Color(0.20f, 0.18f, 0.15f, 0.92f));
            SetRect(slogan.rectTransform, new Vector2(0.30f, 0.755f), new Vector2(0.70f, 0.815f));

            var title = CreateText("TitleCn", parent, "公司战争", font, 132f,
                TextAlignmentOptions.Center, dark, FontStyles.Bold);
            title.characterSpacing = 4f;
            SetRect(title.rectTransform, new Vector2(0.22f, 0.62f), new Vector2(0.78f, 0.77f));

            var subtitle = CreateText("TitleEn", parent, "CORPORATE WARFARE", font, 38f,
                TextAlignmentOptions.Center, new Color(0.52f, 0.32f, 0.12f, 0.95f));
            subtitle.characterSpacing = 9f;
            SetRect(subtitle.rectTransform, new Vector2(0.34f, 0.57f), new Vector2(0.66f, 0.625f));
            CreateDecorLine("TitleLineLeft", parent, new Vector2(0.322f, 0.598f), new Vector2(100f, 2f), 0f);
            CreateDecorLine("TitleLineRight", parent, new Vector2(0.678f, 0.598f), new Vector2(100f, 2f), 0f);
        }

        private static void BuildRightSlogan(Transform parent, TMP_FontAsset font)
        {
            var panel = CreateImage("RightSloganPanel", parent, new Color(0.97f, 0.94f, 0.84f, 0.74f));
            SetRect(panel.rectTransform, new Vector2(0.872f, 0.18f), new Vector2(0.968f, 0.39f));
            panel.raycastTarget = false;
            var line = CreateLine("SloganAccent", panel.transform, new Color(0.56f, 0.34f, 0.12f, 0.9f),
                new Vector2(0.20f, 0.77f), new Vector2(0.40f, 0.79f));
            line.raycastTarget = false;
            var copy = CreateText("Copy", panel.transform, "更高效的世界\n由更好的决策构建", font, 19f,
                TextAlignmentOptions.Center, new Color(0.27f, 0.25f, 0.21f, 0.9f));
            copy.lineSpacing = 18f;
            SetRect(copy.rectTransform, new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.66f));
        }

        private static MenuButtons BuildWingButtons(Transform parent, TMP_FontAsset font)
        {
            return new MenuButtons
            {
                Mail = CreateWingButton("MailButton", parent, "[MAIL]   查询邮件", font,
                    new Vector2(0.30f, 0.295f), new Vector2(410f, 86f), -7f, false, false),
                Quit = CreateWingButton("QuitButton", parent, "[X]   退出", font,
                    new Vector2(0.31f, 0.225f), new Vector2(315f, 62f), -6f, false, false),
                Start = CreateWingButton("StartButton", parent, "开始游戏   >", font,
                    new Vector2(0.70f, 0.295f), new Vector2(410f, 86f), 7f, true, true),
                Continue = CreateWingButton("ContinueButton", parent, "[SAVE]   继续游戏   >", font,
                    new Vector2(0.69f, 0.225f), new Vector2(315f, 62f), 6f, true, false)
            };
        }

        private static FooterTexts BuildFooter(Transform parent, TMP_FontAsset font)
        {
            var footer = CreateImage("NewsFooter", parent, new Color(0.0f, 0.045f, 0.075f, 0.985f));
            SetRect(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.13f));
            var border = CreateLine("FooterBorder", footer.transform, new Color(0.73f, 0.51f, 0.25f, 0.9f),
                new Vector2(0.009f, 0.88f), new Vector2(0.991f, 0.90f));
            border.raycastTarget = false;

            var clock = CreateText("Clock", footer.transform, "14:14", font, 52f,
                TextAlignmentOptions.Center, Color.white);
            SetRect(clock.rectTransform, new Vector2(0.022f, 0.18f), new Vector2(0.115f, 0.82f));
            CreateFooterSeparator("Separator01", footer.transform, 0.12f);

            var system = CreateText("SystemLabel", footer.transform, "系统时间", font, 19f,
                TextAlignmentOptions.Left, Color.white);
            SetRect(system.rectTransform, new Vector2(0.136f, 0.48f), new Vector2(0.23f, 0.78f));
            var date = CreateText("Date", footer.transform, "2026/09/06  星期日", font, 15f,
                TextAlignmentOptions.Left, new Color(0.72f, 0.76f, 0.80f, 1f));
            SetRect(date.rectTransform, new Vector2(0.136f, 0.18f), new Vector2(0.24f, 0.48f));
            CreateFooterSeparator("Separator02", footer.transform, 0.245f);

            var iconBox = CreateImage("NewsIconBox", footer.transform, new Color(0.46f, 0.51f, 0.55f, 1f));
            SetRect(iconBox.rectTransform, new Vector2(0.26f, 0.23f), new Vector2(0.292f, 0.77f));
            var outline = iconBox.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.09f, 0.10f, 0.7f);
            outline.effectDistance = new Vector2(3f, -3f);
            var icon = CreateText("NewsIcon", iconBox.transform, "=", font, 30f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(icon.rectTransform);

            var label = CreateText("NewsLabel", footer.transform, "即时要闻", font, 23f,
                TextAlignmentOptions.Center, Color.white);
            SetRect(label.rectTransform, new Vector2(0.298f, 0.18f), new Vector2(0.39f, 0.82f));
            CreateFooterSeparator("Separator03", footer.transform, 0.397f);

            var track = CreateRect("NewsTrack", footer.transform);
            SetRect(track, new Vector2(0.42f, 0.18f), new Vector2(0.985f, 0.82f));
            track.gameObject.AddComponent<RectMask2D>();
            var ticker = CreateText(
                "Ticker", track,
                "[NEWS] 工业节点授权变动持续扩大    ·    [NEWS] 多家公司正在争抢稀缺资源带    ·    " +
                "[NEWS] 内部通信：周五进行系统维护    ·    [NEWS] 前线供应链完成新一轮重组",
                font, 18f, TextAlignmentOptions.MidlineLeft,
                new Color(0.73f, 0.76f, 0.80f, 1f));
            ticker.enableWordWrapping = false;
            ticker.overflowMode = TextOverflowModes.Overflow;
            ticker.rectTransform.anchorMin = new Vector2(0f, 0f);
            ticker.rectTransform.anchorMax = new Vector2(0f, 1f);
            ticker.rectTransform.pivot = new Vector2(0f, 0.5f);
            ticker.rectTransform.sizeDelta = new Vector2(1900f, 0f);
            ticker.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            ticker.gameObject.AddComponent<BootMainMenuNewsTicker>().Configure(90f, 100f);
            return new FooterTexts { Clock = clock, Date = date };
        }

        internal static GameObject BuildSharedMailPanel(
            Transform parent,
            TMP_FontAsset font,
            out Button closeButton)
        {
            var blocker = CreateImage("MailPanel", parent, new Color(0.0f, 0.025f, 0.045f, 0.72f));
            Stretch(blocker.rectTransform);
            var card = CreateImage("MailCard", blocker.transform, new Color(0.95f, 0.91f, 0.80f, 0.99f));
            SetRect(card.rectTransform, new Vector2(0.25f, 0.20f), new Vector2(0.75f, 0.80f));
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.63f, 0.42f, 0.18f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            var title = CreateText("MailTitle", card.transform, "入职邮件 · 战略部", font, 40f,
                TextAlignmentOptions.Center, new Color(0.10f, 0.09f, 0.07f, 1f), FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f));

            var scrollRoot = CreateImage("MailScroll", card.transform, new Color(0.08f, 0.07f, 0.05f, 0.035f));
            SetRect(scrollRoot.rectTransform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.76f));
            var viewport = CreateImage("Viewport", scrollRoot.transform, Color.clear);
            SetRect(viewport.rectTransform, new Vector2(0f, 0f), new Vector2(0.955f, 1f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = CreateText(
                "MailBody", viewport.transform,
                "欢迎加入公司战争项目。\n\n市场从不等待迟疑者。调配资本、部署员工，夺取关键工业节点，" +
                "并在竞争对手建立优势前完成征服。\n\n—— 市场战略部",
                font, 28f, TextAlignmentOptions.TopLeft,
                new Color(0.19f, 0.17f, 0.14f, 1f));
            body.lineSpacing = 16f;
            body.enableWordWrapping = true;
            body.overflowMode = TextOverflowModes.Overflow;
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.rectTransform.sizeDelta = new Vector2(0f, 620f);

            var scrollbarBack = CreateImage("Scrollbar", scrollRoot.transform,
                new Color(0.12f, 0.10f, 0.07f, 0.20f));
            SetRect(scrollbarBack.rectTransform, new Vector2(0.97f, 0.02f), new Vector2(0.995f, 0.98f));
            var handle = CreateImage("Handle", scrollbarBack.transform,
                new Color(0.63f, 0.42f, 0.18f, 0.95f));
            Stretch(handle.rectTransform);
            var scrollbar = scrollbarBack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = body.rectTransform;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 45f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalNormalizedPosition = 1f;
            closeButton = CreateRectButton("CloseMailButton", card.transform, "关闭", font,
                new Vector2(0.40f, 0.07f), new Vector2(0.60f, 0.19f));
            blocker.gameObject.SetActive(false);
            return blocker.gameObject;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static Button CreateWingButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset font,
            Vector2 anchor,
            Vector2 size,
            float rotation,
            bool pointRight,
            bool accent)
        {
            var root = CreateRect(name, parent);
            SetRect(root, anchor, anchor, size);
            root.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            var shadowObject = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer));
            shadowObject.transform.SetParent(root, false);
            var shadowRect = (RectTransform)shadowObject.transform;
            Stretch(shadowRect);
            shadowRect.offsetMin += new Vector2(9f, -10f);
            shadowRect.offsetMax += new Vector2(9f, -10f);
            var shadow = shadowObject.AddComponent<BootMenuWingGraphic>();
            shadow.color = new Color(0f, 0f, 0f, 0.32f);
            shadow.raycastTarget = false;
            shadow.Configure(pointRight);

            var goldObject = new GameObject("GoldOuter", typeof(RectTransform), typeof(CanvasRenderer));
            goldObject.transform.SetParent(root, false);
            var goldRect = (RectTransform)goldObject.transform;
            Stretch(goldRect);
            var gold = goldObject.AddComponent<BootMenuWingGraphic>();
            gold.color = new Color(0.70f, 0.46f, 0.18f, 1f);
            gold.raycastTarget = false;
            gold.Configure(pointRight);

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
            fillObject.transform.SetParent(goldRect, false);
            var fillRect = (RectTransform)fillObject.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(9f, 9f);
            fillRect.offsetMax = new Vector2(-9f, -9f);
            var fill = fillObject.AddComponent<BootMenuWingGraphic>();
            fill.color = accent
                ? new Color(0.035f, 0.14f, 0.80f, 0.98f)
                : new Color(0.025f, 0.024f, 0.022f, 0.97f);
            fill.raycastTarget = false;
            fill.Configure(pointRight);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = accent ? new Color(1.16f, 1.16f, 1.16f, 1f) : new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var highlight = CreateImage("Highlight", fillRect, new Color(1f, 1f, 1f, accent ? 0.16f : 0.06f));
            SetRect(highlight.rectTransform, new Vector2(0.10f, 0.68f), new Vector2(0.87f, 0.73f));
            highlight.raycastTarget = false;
            var text = CreateText("Label", fillRect, label, font, size.y > 70f ? 30f : 25f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            SetRect(text.rectTransform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));
            return button;
        }

        private static Button CreateRectButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset font,
            Vector2 min,
            Vector2 max)
        {
            var image = CreateImage(name, parent, new Color(0.04f, 0.10f, 0.16f, 0.98f));
            SetRect(image.rectTransform, min, max);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", image.transform, label, font, 26f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(text.rectTransform, 8f, 4f);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment,
            Color color,
            FontStyles style = FontStyles.Normal)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * 0.58f);
            text.fontSizeMax = size;
            text.extraPadding = true;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static Image CreateLine(
            string name,
            Transform parent,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            var line = CreateImage(name, parent, color);
            SetRect(line.rectTransform, min, max);
            line.raycastTarget = false;
            return line;
        }

        private static void CreateDecorLine(
            string name,
            Transform parent,
            Vector2 anchor,
            Vector2 size,
            float rotation)
        {
            var line = CreateImage(name, parent, new Color(0.68f, 0.45f, 0.18f, 0.62f));
            SetRect(line.rectTransform, anchor, anchor, size);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            line.raycastTarget = false;
        }

        private static void CreateFooterSeparator(string name, Transform parent, float x)
        {
            CreateLine(name, parent, new Color(1f, 1f, 1f, 0.55f),
                new Vector2(x, 0.18f), new Vector2(x + 0.0012f, 0.82f));
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
            rect.localScale = Vector3.one;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureBackgroundImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                !importer.mipmapEnabled && importer.maxTextureSize >= 2048)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Any())
            {
                return;
            }

            var eventSystem = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static Transform FindSceneTransform(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName) return child;
                }
            }

            return null;
        }

        private static void PutSceneFirstInBuildSettings(string bootScenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(item => item.path == bootScenePath);
            scenes.RemoveAll(item => item.path == bootScenePath);
            scenes.Insert(0, existing ?? new EditorBuildSettingsScene(bootScenePath, true));
            scenes[0].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private sealed class MenuButtons
        {
            public Button Start;
            public Button Continue;
            public Button Mail;
            public Button Quit;
        }

        private sealed class FooterTexts
        {
            public TextMeshProUGUI Clock;
            public TextMeshProUGUI Date;
        }
    }
}
