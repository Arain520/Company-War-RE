using System;
using CompanyWarRE.Presentation;
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
    public sealed class FormalBattleHudGeneratorWindow : EditorWindow
    {
        private const string ScenePath = "Assets/CompanyWarRE/Scenes/FormalBattle.unity";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MSYH SDF.asset";
        private const string RootName = "[Generated] FormalBattleHUD";

        [SerializeField] private SceneAsset battleScene;
        [SerializeField] private TMP_FontAsset mainFont;

        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.80f, 0.98f);
        private static readonly Color Navy = new Color(0.018f, 0.065f, 0.13f, 0.98f);
        private static readonly Color DeepNavy = new Color(0.015f, 0.035f, 0.075f, 0.98f);
        private static readonly Color Gold = new Color(0.72f, 0.48f, 0.16f, 1f);
        private static readonly Color Ink = new Color(0.04f, 0.08f, 0.13f, 1f);
        private static readonly Color Blue = new Color(0.03f, 0.52f, 0.95f, 1f);

        [MenuItem("工具/Company War/战斗场景 UI 生成器", priority = 122)]
        private static void OpenWindow()
        {
            var window = GetWindow<FormalBattleHudGeneratorWindow>(true, "战斗场景 UI 生成器");
            window.minSize = new Vector2(500f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            if (battleScene == null) battleScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (mainFont == null) mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("公司战争 · 战斗 HUD", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "生成新战斗 HUD：底部授权进度、达标后滑入的方形申请按钮、不中断战斗的单位选择栏、" +
                "左侧单位信息栏，以及从底部卡片拖入战场的部署交互。顶部角色区域只保留占位框。",
                MessageType.Info);
            EditorGUILayout.Space(6f);
            battleScene = (SceneAsset)EditorGUILayout.ObjectField("战斗场景", battleScene, typeof(SceneAsset), false);
            mainFont = (TMP_FontAsset)EditorGUILayout.ObjectField("主字体", mainFont, typeof(TMP_FontAsset), false);
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("工具仅替换名称为 “" + RootName + "” 的生成内容。", MessageType.None);

            var blocked = EditorApplication.isPlayingOrWillChangePlaymode;
            using (new EditorGUI.DisabledScope(blocked || battleScene == null || mainFont == null))
            {
                if (GUILayout.Button("生成 / 更新 FormalBattle HUD", GUILayout.Height(46f)))
                    Generate(battleScene, mainFont, true);
            }
            if (blocked) EditorGUILayout.HelpBox("请先退出 Play 模式。", MessageType.Warning);
        }

        public static void GenerateDefault()
        {
            Generate(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), false);
        }

        public static void Generate(SceneAsset sceneAsset, TMP_FontAsset font, bool askToSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play 模式，再生成战斗 HUD。");
            if (sceneAsset == null) throw new InvalidOperationException("没有指定 FormalBattle 场景。");
            if (font == null) throw new InvalidOperationException("找不到 MSYH SDF 字体资源。");
            if (askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(sceneAsset), OpenSceneMode.Single);
            var oldRoot = FindSceneObject(scene, RootName);
            if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
            EnsureEventSystem(scene);

            var battle = FindInScene<BattleSliceController>(scene);
            if (battle == null)
                throw new InvalidOperationException("场景中没有 BattleSliceController，无法绑定战斗数据。");
            RemoveLegacyCowUiBootstrap(scene);

            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(FormalBattleHudController));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var safe = Panel("SafeFrame", root.transform, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.99f),
                Color.clear, Gold, 2f);
            safe.raycastTarget = false;

            var top = Panel("TopCommandBar", root.transform, new Vector2(0.015f, 0.865f), new Vector2(0.985f, 0.985f),
                Cream, Gold, 2f);
            var character = Panel("CharacterPlaceholder", top.transform, new Vector2(0.018f, 0.12f), new Vector2(0.29f, 0.90f),
                new Color(0.92f, 0.88f, 0.76f, 1f), Gold, 2f);
            var characterLabel = Text("CharacterLabel", character.transform,
                "CHARACTER\n角色立绘预留", font, 18f, TextAlignmentOptions.Center, new Color(0.35f, 0.32f, 0.27f, 0.55f));
            Stretch(characterLabel.rectTransform, 8f);

            var assault = StatBar("Assault", top.transform, "攻坚积分  000/006", font,
                new Vector2(0.31f, 0.62f), new Vector2(0.48f, 0.88f), new Color(0.55f, 0.025f, 0.035f, 1f));
            var resource = StatBar("Resource", top.transform, "物资  010", font,
                new Vector2(0.31f, 0.34f), new Vector2(0.48f, 0.58f), new Color(0.42f, 0.27f, 0.015f, 1f));
            var topNote = Text("NoTopAuthorization", top.transform, "授权积分已移至底部进度条", font, 13f,
                TextAlignmentOptions.Left, new Color(0.32f, 0.30f, 0.26f, 0.42f));
            Rect(topNote.rectTransform, new Vector2(0.31f, 0.10f), new Vector2(0.52f, 0.30f));

            var mail = SquareButton("Mail", top.transform, "✉", font,
                new Vector2(0.715f, 0.18f), new Vector2(0.755f, 0.82f), DeepNavy);
            var phase = Text("Phase", top.transform, "当前阶段：I", font, 18f,
                TextAlignmentOptions.Center, Ink);
            Rect(phase.rectTransform, new Vector2(0.765f, 0.22f), new Vector2(0.855f, 0.78f));
            Panel("PhaseFrame", top.transform, new Vector2(0.765f, 0.18f), new Vector2(0.855f, 0.82f), Color.clear, Gold, 1f)
                .raycastTarget = false;
            var eradicate = SquareButton("Eradicate", top.transform, "铲除", font,
                new Vector2(0.87f, 0.18f), new Vector2(0.915f, 0.82f), DeepNavy);
            var pause = SquareButton("Pause", top.transform, "Ⅱ\n暂停", font,
                new Vector2(0.928f, 0.18f), new Vector2(0.975f, 0.82f), DeepNavy);
            mail.interactable = false;
            eradicate.interactable = false;

            var deployBar = Panel("DeploymentBar", root.transform,
                new Vector2(0.025f, 0.075f), new Vector2(0.79f, 0.205f), Cream, Gold, 2f);
            var deployContent = new GameObject("DeploymentCards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            deployContent.transform.SetParent(deployBar.transform, false);
            Rect((RectTransform)deployContent.transform, new Vector2(0.012f, 0.08f), new Vector2(0.988f, 0.92f));
            var deployLayout = deployContent.GetComponent<HorizontalLayoutGroup>();
            deployLayout.spacing = 12f;
            deployLayout.childControlWidth = false;
            deployLayout.childControlHeight = true;
            deployLayout.childForceExpandWidth = false;
            deployLayout.childForceExpandHeight = true;
            deployLayout.childAlignment = TextAnchor.MiddleLeft;

            var authProgress = Panel("AuthorizationProgress", root.transform,
                new Vector2(0.795f, 0.075f), new Vector2(0.985f, 0.205f), Cream, Gold, 2f);
            var progressDisplayObject = new GameObject("ProgressDisplay", typeof(RectTransform));
            progressDisplayObject.transform.SetParent(authProgress.transform, false);
            var progressDisplay = (RectTransform)progressDisplayObject.transform;
            Stretch(progressDisplay);
            var authTitle = Text("Title", progressDisplay, "授权申请", font, 28f,
                TextAlignmentOptions.Center, Ink);
            Rect(authTitle.rectTransform, new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.92f));
            var progressBack = Panel("ProgressBack", progressDisplay,
                new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.51f), new Color(0.25f, 0.25f, 0.25f, 1f), Ink, 1f);
            var progressFill = Image("Fill", progressBack.transform, Blue);
            Stretch(progressFill.rectTransform, 2f);
            progressFill.type = UnityEngine.UI.Image.Type.Filled;
            progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0.66f;
            var authProgressText = Text("ProgressLabel", progressDisplay, "授权申请  004/006", font, 17f,
                TextAlignmentOptions.Center, Ink);
            Rect(authProgressText.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.30f));

            var footer = Panel("BattleNews", root.transform, new Vector2(0.025f, 0.015f), new Vector2(0.985f, 0.065f),
                new Color(0.005f, 0.22f, 0.38f, 0.96f), Blue, 1f);
            var clock = Text("Clock", footer.transform, "14:14", font, 35f, TextAlignmentOptions.Center, Color.white);
            Rect(clock.rectTransform, new Vector2(0.012f, 0.05f), new Vector2(0.105f, 0.95f));
            var system = Text("SystemTime", footer.transform, "系统时间", font, 16f,
                TextAlignmentOptions.Left, new Color(0.82f, 0.88f, 0.95f, 1f));
            Rect(system.rectTransform, new Vector2(0.11f, 0.05f), new Vector2(0.20f, 0.95f));
            var feedback = Text("Feedback", footer.transform, "[NEWS] 等待部署指令", font, 15f,
                TextAlignmentOptions.Right, new Color(0.72f, 0.80f, 0.90f, 1f));
            Rect(feedback.rectTransform, new Vector2(0.35f, 0.05f), new Vector2(0.975f, 0.95f));

            var infoPanel = BuildInfoPanel(root.transform, font, out var unitName, out var unitType,
                out var unitStats, out var unitEffect);
            var authButtonPanel = BuildAuthorizationButton(authProgress.transform, font, out var authButton);
            var choicePanel = BuildChoicePanel(root.transform, font, out var choiceContent);
            var ghost = Panel("DeploymentGhost", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Color(0.05f, 0.55f, 0.95f, 0.84f), Color.white, 2f);
            ghost.rectTransform.sizeDelta = new Vector2(142f, 88f);
            ghost.raycastTarget = false;
            var ghostText = Text("Label", ghost.transform, "U01\n释放以部署", font, 16f,
                TextAlignmentOptions.Center, Color.white);
            Stretch(ghostText.rectTransform, 5f);
            ghost.gameObject.SetActive(false);

            var controller = root.GetComponent<FormalBattleHudController>();
            controller.Configure(battle, font, assault, resource, phase, pause,
                (RectTransform)deployContent.transform, infoPanel, unitName, unitType, unitStats, unitEffect,
                progressDisplay, progressFill, authProgressText, authButtonPanel, authButton, choicePanel, choiceContent,
                clock, feedback, ghost.rectTransform, ghostText);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("FormalBattle HUD 已生成。授权选择不会暂停战斗，单位卡片可拖入场景部署。", root);
        }

        private static RectTransform BuildInfoPanel(Transform parent, TMP_FontAsset font,
            out TMP_Text unitName, out TMP_Text unitType, out TMP_Text unitStats, out TMP_Text unitEffect)
        {
            var panel = Panel("UnitInfoDrawer", parent, new Vector2(0.015f, 0.22f), new Vector2(0.19f, 0.85f),
                Cream, Gold, 2f);
            unitName = Text("UnitName", panel.transform, "攻击者 I 型", font, 30f, TextAlignmentOptions.Left, Ink);
            Rect(unitName.rectTransform, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.96f));
            unitType = Text("UnitType", panel.transform, "作战单位", font, 18f, TextAlignmentOptions.Left,
                new Color(0.15f, 0.18f, 0.22f, 0.78f));
            Rect(unitType.rectTransform, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.87f));
            var preview = Panel("UnitPreview", panel.transform, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.78f),
                new Color(0.88f, 0.85f, 0.76f, 1f), Gold, 1f);
            var previewLabel = Text("PreviewPlaceholder", preview.transform, "UNIT PREVIEW\n单位图片预留", font, 18f,
                TextAlignmentOptions.Center, new Color(0.25f, 0.28f, 0.31f, 0.50f));
            Stretch(previewLabel.rectTransform, 5f);
            var statsTitle = Text("StatsTitle", panel.transform, "单位属性", font, 20f,
                TextAlignmentOptions.Left, Ink);
            Rect(statsTitle.rectTransform, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.49f));
            unitStats = Text("UnitStats", panel.transform, "部署费用    --\n部署冷却    --\n剩余冷却    --", font, 17f,
                TextAlignmentOptions.TopLeft, Ink);
            Rect(unitStats.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.42f));
            var effectTitle = Text("EffectTitle", panel.transform, "特殊效果", font, 20f,
                TextAlignmentOptions.Left, Ink);
            Rect(effectTitle.rectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.21f));
            unitEffect = Text("UnitEffect", panel.transform, "无特殊效果", font, 15f,
                TextAlignmentOptions.TopLeft, Ink);
            Rect(unitEffect.rectTransform, new Vector2(0.08f, 0.035f), new Vector2(0.92f, 0.14f));
            return panel.rectTransform;
        }

        private static RectTransform BuildAuthorizationButton(Transform parent, TMP_FontAsset font, out Button button)
        {
            var panel = Panel("AuthorizationButtonDrawer", parent,
                Vector2.zero, Vector2.one, Cream, Gold, 1f);
            button = SquareButton("RequestAuthorization", panel.transform, "▰  部署权限", font,
                new Vector2(0.20f, 0.15f), new Vector2(0.80f, 0.85f), Navy);
            return panel.rectTransform;
        }

        private static RectTransform BuildChoicePanel(Transform parent, TMP_FontAsset font, out RectTransform content)
        {
            var panel = Panel("AuthorizationChoiceDrawer", parent,
                new Vector2(0.76f, 0.22f), new Vector2(0.985f, 0.84f), Cream, Gold, 2f);
            var title = Text("Title", panel.transform, "选择部署单位", font, 31f, TextAlignmentOptions.Left, Ink);
            Rect(title.rectTransform, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.97f));
            var subtitle = Text("Subtitle", panel.transform, "选择期间战斗继续进行", font, 15f,
                TextAlignmentOptions.Left, new Color(0.35f, 0.32f, 0.27f, 0.75f));
            Rect(subtitle.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.88f));
            var contentGo = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentGo.transform.SetParent(panel.transform, false);
            content = (RectTransform)contentGo.transform;
            Rect(content, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.79f));
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            panel.gameObject.SetActive(false);
            return panel.rectTransform;
        }

        private static TMP_Text StatBar(string name, Transform parent, string value, TMP_FontAsset font,
            Vector2 min, Vector2 max, Color color)
        {
            var panel = Panel(name, parent, min, max, color, new Color(0.92f, 0.78f, 0.32f, 1f), 1f);
            var text = Text("Label", panel.transform, value, font, 17f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, 4f);
            return text;
        }

        private static Button SquareButton(string name, Transform parent, string value, TMP_FontAsset font,
            Vector2 min, Vector2 max, Color color)
        {
            var image = Image(name, parent, color);
            Rect(image.rectTransform, min, max);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2f, -2f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text("Label", image.transform, value, font, value.Length <= 2 ? 30f : 18f,
                TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform, 3f);
            return button;
        }

        private static Image Panel(string name, Transform parent, Vector2 min, Vector2 max,
            Color fill, Color border, float borderWidth)
        {
            var image = Image(name, parent, fill);
            Rect(image.rectTransform, min, max);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(borderWidth, -borderWidth);
            return image;
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font,
            float size, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
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

        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindChild(root.transform, objectName);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindInScene<EventSystem>(scene) != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static void RemoveLegacyCowUiBootstrap(Scene scene)
        {
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                foreach (var behaviour in sceneRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null &&
                        behaviour.GetType().FullName == "CompanyWar.UI.FormalCowUiBootstrap")
                    {
                        Undo.DestroyObjectImmediate(behaviour);
                    }
                }
            }
        }
    }
}
