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
    public sealed class BootLevelSelectUiGeneratorWindow : EditorWindow
    {
        private const string BootScenePath = "Assets/CompanyWarRE/Scenes/Boot.unity";
        private const string ArchivePath =
            "Assets/CompanyWarRE/Resources/CompanyWarRE/Configs/LevelArchives.json";
        private const string LevelFolder =
            "Assets/CompanyWarRE/Resources/CompanyWarRE/Configs/Levels/";
        private const string FontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/MSYH SDF.asset";

        [SerializeField] private SceneAsset bootScene;
        [SerializeField] private TextAsset levelArchives;

        [MenuItem("工具/Company War/选关界面 UI 生成器", priority = 121)]
        private static void OpenWindow()
        {
            var window = GetWindow<BootLevelSelectUiGeneratorWindow>(true, "选关界面 UI 生成器");
            window.minSize = new Vector2(480f, 250f);
            window.Show();
        }

        private void OnEnable()
        {
            if (bootScene == null) bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (levelArchives == null) levelArchives = AssetDatabase.LoadAssetAtPath<TextAsset>(ArchivePath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("公司战争 · Boot 选关界面", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "读取 LevelArchives.json 与 Configs/Levels 下的关卡配置，生成可横向拖动的选关界面。" +
                "工具只替换 Boot 中的 LevelSelectCanvas，并自动回绑主界面的开始游戏按钮。",
                MessageType.Info);
            EditorGUILayout.Space(6f);
            bootScene = (SceneAsset)EditorGUILayout.ObjectField("Boot 场景", bootScene, typeof(SceneAsset), false);
            levelArchives = (TextAsset)EditorGUILayout.ObjectField(
                "关卡档案配置", levelArchives, typeof(TextAsset), false);

            var playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing)
            {
                EditorGUILayout.HelpBox("请先退出 Play 模式。", MessageType.Warning);
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(playing || bootScene == null || levelArchives == null))
            {
                if (GUILayout.Button("生成 / 更新 Boot 选关界面", GUILayout.Height(44f)))
                {
                    Generate(bootScene, levelArchives, true);
                }
            }
        }

        public static void GenerateDefault()
        {
            Generate(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath),
                AssetDatabase.LoadAssetAtPath<TextAsset>(ArchivePath),
                false);
        }

        public static void Generate(SceneAsset sceneAsset, TextAsset archiveAsset, bool askToSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play 模式，再生成选关界面。");
            if (sceneAsset == null || archiveAsset == null)
                throw new InvalidOperationException("Boot 场景或关卡档案配置为空。");
            if (askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var archive = JsonUtility.FromJson<ArchiveRoot>(archiveAsset.text);
            if (archive?.Levels == null || archive.Levels.Length == 0)
                throw new InvalidOperationException("LevelArchives.json 中没有可用关卡。");

            var source = BuildEntries(archive.Levels);
            var font = LoadFont(source);
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sceneAsset), OpenSceneMode.Single);
            var oldRoot = FindSceneObject(scene, "LevelSelectCanvas");
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
            EnsureEventSystem(scene);

            var root = new GameObject(
                "LevelSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(BootLevelSelectController));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var background = Image("Background", root.transform, new Color(0.018f, 0.055f, 0.10f, 1f));
            Stretch(background.rectTransform);
            var left = Panel("LevelWorkspace", background.transform,
                new Vector2(0.032f, 0.145f), new Vector2(0.716f, 0.94f),
                new Color(0.95f, 0.91f, 0.80f, 1f), new Color(0.68f, 0.46f, 0.18f, 1f));
            var right = Panel("LevelDetails", background.transform,
                new Vector2(0.73f, 0.145f), new Vector2(0.968f, 0.94f),
                new Color(0.96f, 0.93f, 0.84f, 1f), new Color(0.68f, 0.46f, 0.18f, 1f));

            var heading = Text("Heading", left.transform, "关卡选择", font, 62f,
                TextAlignmentOptions.Left, new Color(0.05f, 0.055f, 0.06f, 1f));
            Rect(heading.rectTransform, new Vector2(0.04f, 0.81f), new Vector2(0.62f, 0.96f));
            var phase = Text("Phase", left.transform, "工业区行动 · CORPORATE OPERATIONS", font, 22f,
                TextAlignmentOptions.Left, new Color(0.45f, 0.43f, 0.38f, 0.7f));
            Rect(phase.rectTransform, new Vector2(0.04f, 0.755f), new Vector2(0.72f, 0.83f));
            Line("HeaderLine", left.transform, new Vector2(0f, 0.745f), new Vector2(1f, 0.748f));

            var descriptionViewport = Image("DescriptionViewport", left.transform, Color.clear);
            Rect(descriptionViewport.rectTransform, new Vector2(0.012f, 0.50f), new Vector2(0.988f, 0.72f));
            descriptionViewport.gameObject.AddComponent<RectMask2D>();
            var description = Panel("DescriptionDropPanel", descriptionViewport.transform,
                Vector2.zero, Vector2.one, new Color(0.04f, 0.08f, 0.24f, 0.95f),
                new Color(0.72f, 0.52f, 0.22f, 1f));
            var descriptionTitle = Text("RecordTitle", description.transform, "选择关卡以查看行动记录", font,
                28f, TextAlignmentOptions.Left, Color.white);
            Rect(descriptionTitle.rectTransform, new Vector2(0.035f, 0.60f), new Vector2(0.96f, 0.94f));
            var descriptionBody = Text("RecordBody", description.transform, string.Empty, font, 17f,
                TextAlignmentOptions.TopLeft, new Color(0.82f, 0.84f, 0.91f, 1f));
            Rect(descriptionBody.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.60f));

            var scrollViewport = Image("CardViewport", left.transform, Color.clear);
            Rect(scrollViewport.rectTransform, new Vector2(0.035f, 0.205f), new Vector2(0.965f, 0.47f));
            scrollViewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            content.transform.SetParent(scrollViewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(Mathf.Max(1, source.Count) * 132f + 30f, 0f);
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 24, 24);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            var scroll = scrollViewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollViewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.10f;
            scroll.scrollSensitivity = 45f;

            var runtimeEntries = new BootLevelSelectEntry[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                runtimeEntries[index] = CreateCard(content.transform, font, source[index], index);
            }

            var dragHint = Text("DragHint", left.transform, "左右拖动查看全部关卡", font, 18f,
                TextAlignmentOptions.Center, new Color(0.42f, 0.40f, 0.35f, 0.58f));
            Rect(dragHint.rectTransform, new Vector2(0.32f, 0.13f), new Vector2(0.68f, 0.19f));
            var legend = Text("Legend", left.transform, "■ 已解锁     ■ 当前选择     ■ 特殊行动", font, 17f,
                TextAlignmentOptions.Left, new Color(0.33f, 0.32f, 0.29f, 0.7f));
            Rect(legend.rectTransform, new Vector2(0.045f, 0.035f), new Vector2(0.65f, 0.105f));

            var sideLevelId = Text("LevelId", right.transform, "L00", font, 20f,
                TextAlignmentOptions.Left, new Color(0.38f, 0.37f, 0.34f, 0.62f));
            Rect(sideLevelId.rectTransform, new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.975f));
            var sideTitle = Text("Title", right.transform, "入职考核", font, 40f,
                TextAlignmentOptions.Left, new Color(0.16f, 0.15f, 0.14f, 0.92f));
            Rect(sideTitle.rectTransform, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.91f));
            var sideSummary = Text("Summary", right.transform, string.Empty, font, 18f,
                TextAlignmentOptions.TopLeft, new Color(0.33f, 0.32f, 0.29f, 0.75f));
            Rect(sideSummary.rectTransform, new Vector2(0.08f, 0.61f), new Vector2(0.92f, 0.79f));
            Line("DetailLine1", right.transform, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.593f));
            var sideMeta = Text("Meta", right.transform, string.Empty, font, 18f,
                TextAlignmentOptions.TopLeft, new Color(0.22f, 0.21f, 0.19f, 0.85f));
            Rect(sideMeta.rectTransform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.57f));
            Line("DetailLine2", right.transform, new Vector2(0.08f, 0.285f), new Vector2(0.92f, 0.288f));
            var sideRewards = Text("Rewards", right.transform, string.Empty, font, 16f,
                TextAlignmentOptions.TopLeft, new Color(0.37f, 0.35f, 0.31f, 0.72f));
            Rect(sideRewards.rectTransform, new Vector2(0.08f, 0.10f), new Vector2(0.52f, 0.27f));
            var enter = Button("EnterLevel", right.transform, "进入关卡", font,
                new Vector2(0.48f, 0.035f), new Vector2(0.96f, 0.145f),
                new Color(0.12f, 0.23f, 0.80f, 1f));
            var back = Button("Back", right.transform, "返回", font,
                new Vector2(0.08f, 0.035f), new Vector2(0.43f, 0.145f),
                new Color(0.11f, 0.12f, 0.15f, 1f));

            BuildFooter(background.transform, font);

            var mainMenu = FindSceneObject(scene, "[Generated] MainMenuUI");
            var mainCanvas = mainMenu != null ? FindChild(mainMenu.transform, "MainMenuCanvas")?.gameObject : null;
            var controller = root.GetComponent<BootLevelSelectController>();
            controller.Configure(
                mainCanvas, description.rectTransform, descriptionTitle, descriptionBody,
                sideLevelId, sideTitle, sideSummary, sideMeta, sideRewards,
                enter, back, scroll, runtimeEntries, "FormalBattle");
            BindMainMenu(scene, root);
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log($"Boot 选关界面已生成：{runtimeEntries.Length} 个关卡。", root);
        }

        private static List<LevelSource> BuildEntries(IEnumerable<ArchiveEntry> archives)
        {
            var result = new List<LevelSource>();
            foreach (var archive in archives.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id)))
            {
                var config = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelFolder + archive.Id + ".json");
                if (config == null)
                {
                    Debug.LogWarning("跳过缺少关卡配置的档案：" + archive.Id);
                    continue;
                }

                var level = JsonUtility.FromJson<LevelConfig>(config.text) ?? new LevelConfig();
                result.Add(new LevelSource
                {
                    Archive = archive,
                    Config = level,
                    Rewards = archive.Rewards == null
                        ? string.Empty
                        : string.Join("\n", archive.Rewards.Select(reward => $"{reward.Label} {reward.Value}"))
                });
            }

            return result;
        }

        private static TMP_FontAsset LoadFont(IEnumerable<LevelSource> levels)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("找不到 MSYH SDF：" + FontPath);
            font.isMultiAtlasTexturesEnabled = true;
            var characters = "关卡选择工业区行动左右拖动查看全部已解锁当前特殊进入返回地图尺寸预计阶段攻坚目标勋章参考难度推荐战力奖励" +
                             string.Concat(levels.Select(level =>
                                 level.Archive.Id + level.Archive.Title + level.Archive.ShortDescription +
                                 level.Archive.RecordText + level.Archive.ChapterName + level.Archive.PhaseLabel +
                                 level.Rewards));
            if (!font.TryAddCharacters(characters, out var missing) && !string.IsNullOrEmpty(missing))
                Debug.LogWarning("MSYH SDF 仍缺少字符：" + missing);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        private static BootLevelSelectEntry CreateCard(
            Transform parent, TMP_FontAsset font, LevelSource source, int index)
        {
            var cardObject = new GameObject(
                "LevelCard_" + source.Archive.Id, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            cardObject.transform.SetParent(parent, false);
            var layout = cardObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 116f;
            layout.minWidth = 116f;
            var image = cardObject.GetComponent<Image>();
            image.color = index == 0
                ? new Color(0.08f, 0.28f, 0.96f, 1f)
                : new Color(0.035f, 0.07f, 0.28f, 1f);
            var outline = cardObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.52f, 0.20f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
            var button = cardObject.GetComponent<Button>();
            button.targetGraphic = image;
            var label = Text("Label", cardObject.transform,
                source.Archive.Id + "\n\n" + (source.Archive.IsHighRisk ? "◆ HIGH RISK" : "◆ ◆ ◆"),
                font, 22f, TextAlignmentOptions.Center, Color.white);
            Stretch(label.rectTransform, 5f, 5f);
            return new BootLevelSelectEntry
            {
                levelId = source.Archive.Id,
                title = string.IsNullOrWhiteSpace(source.Archive.ShortTitle)
                    ? source.Archive.Title : source.Archive.ShortTitle,
                shortDescription = source.Archive.ShortDescription,
                recordText = source.Archive.RecordText,
                chapterName = source.Archive.ChapterName,
                phaseLabel = source.Archive.PhaseLabel,
                columns = source.Config.Columns,
                rows = source.Config.Rows,
                stageCount = source.Config.Stages?.Length ?? 0,
                requiredAssaultScore = source.Config.RequiredAssaultScore,
                medalScore = source.Config.MedalScore,
                recommendedPower = source.Archive.RecommendedPower,
                rewardsText = source.Rewards,
                button = button,
                cardImage = image,
                cardLabel = label
            };
        }

        private static void BuildFooter(Transform parent, TMP_FontAsset font)
        {
            var footer = Panel("Footer", parent, new Vector2(0.032f, 0.025f), new Vector2(0.968f, 0.12f),
                new Color(0.01f, 0.20f, 0.34f, 0.98f), new Color(0.10f, 0.55f, 0.78f, 0.9f));
            var clock = Text("Clock", footer.transform, "14:14", font, 40f,
                TextAlignmentOptions.Center, Color.white);
            Rect(clock.rectTransform, new Vector2(0.02f, 0.08f), new Vector2(0.14f, 0.92f));
            var date = Text("Date", footer.transform, "2026/09/06  星期日", font, 15f,
                TextAlignmentOptions.Left, new Color(0.72f, 0.80f, 0.88f, 1f));
            Rect(date.rectTransform, new Vector2(0.15f, 0.12f), new Vector2(0.32f, 0.48f));
            var system = Text("System", footer.transform, "系统时间", font, 19f,
                TextAlignmentOptions.Left, Color.white);
            Rect(system.rectTransform, new Vector2(0.15f, 0.48f), new Vector2(0.30f, 0.88f));
            var ticker = Text("Ticker", footer.transform,
                "[NEWS] 工业节点授权波动持续扩大   ·   [NEWS] 多家公司正在争抢稀缺资源   ·   [NEWS] 内部通信：周五进行系统维护",
                font, 16f, TextAlignmentOptions.MidlineLeft, new Color(0.72f, 0.78f, 0.86f, 1f));
            Rect(ticker.rectTransform, new Vector2(0.40f, 0.12f), new Vector2(0.98f, 0.88f));
            var clockRuntime = footer.gameObject.AddComponent<BootMainMenuClock>();
            clockRuntime.Configure(clock, date);
        }

        private static void BindMainMenu(Scene scene, GameObject levelSelect)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var controller in root.GetComponentsInChildren<BootMainMenuController>(true))
            {
                var serialized = new SerializedObject(controller);
                var property = serialized.FindProperty("levelSelectRoot");
                if (property == null) continue;
                property.objectReferenceValue = levelSelect;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var child = FindChild(root.transform, name);
                if (child != null) return child.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<EventSystem>(true) != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static Image Panel(
            string name, Transform parent, Vector2 min, Vector2 max, Color color, Color border)
        {
            var image = Image(name, parent, color);
            Rect(image.rectTransform, min, max);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2f, -2f);
            return image;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Text(
            string name, Transform parent, string value, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(11f, size * 0.58f);
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Button Button(
            string name, Transform parent, string label, TMP_FontAsset font,
            Vector2 min, Vector2 max, Color color)
        {
            var image = Image(name, parent, color);
            Rect(image.rectTransform, min, max);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text("Label", image.transform, label, font, 25f,
                TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, 5f, 5f);
            return button;
        }

        private static void Line(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var line = Image(name, parent, new Color(0.69f, 0.50f, 0.22f, 0.65f));
            Rect(line.rectTransform, min, max);
            line.raycastTarget = false;
        }

        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
            rect.localScale = Vector3.one;
        }

        [Serializable] private sealed class ArchiveRoot { public ArchiveEntry[] Levels; }
        [Serializable] private sealed class ArchiveEntry
        {
            public string Id;
            public string Title;
            public string ShortTitle;
            public string ShortDescription;
            public string RecordText;
            public string ChapterName;
            public string PhaseLabel;
            public bool IsHighRisk;
            public int RecommendedPower;
            public Reward[] Rewards;
        }
        [Serializable] private sealed class Reward { public string Label; public string Value; }
        [Serializable] private sealed class LevelConfig
        {
            public int Columns;
            public int Rows;
            public int RequiredAssaultScore;
            public int MedalScore;
            public string[] Stages;
        }
        private sealed class LevelSource
        {
            public ArchiveEntry Archive;
            public LevelConfig Config;
            public string Rewards;
        }
    }
}
