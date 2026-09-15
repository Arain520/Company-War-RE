using CompanyWarRE.Presentation;
using UnityEditor;
using UnityEngine;

namespace CompanyWarRE.EditorTools
{
    public sealed class BattleIntroEditorWindow : EditorWindow
    {
        private BattleIntroPathSettings _settings;
        private Editor _settingsEditor;
        private BattleIntroDirector _preview;
        private Vector3 _home, _center;
        private Quaternion _rotation;
        private float _size, _progress;
        private int _columns = 18, _rows = 30;
        private Vector2 _scroll;
        private bool _showHandles = true;
        private BattleIntroSequence _sequence;
        private static readonly string[] ShotNames = { "云海进入", "柱间穿行", "敌后俯瞰", "旋转归位" };

        [MenuItem("Tools/CompanyWarRE/战前运镜编辑器")]
        public static void Open() => GetWindow<BattleIntroEditorWindow>("战前运镜");

        private void OnEnable()
        {
            _settings = Resources.Load<BattleIntroPathSettings>(BattleIntroPathSettings.ResourcePath);
            SceneView.duringSceneGui += DrawScene;
            RebuildReference();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScene;
            if (_preview != null) _preview.PreviewPaused = false;
            if (_settingsEditor != null) DestroyImmediate(_settingsEditor);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(_settings != null && _settings.useSequence ? "分镜展示：云海飞入 → 柱间穿行 → 敌后俯瞰 → 旋转归位。" : "第一版连续运镜：可拖动开场点、两个控制点和注视点。播放时长在场景 BattleSliceController 中设置。", MessageType.Info);
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("未找到 BattleIntroPath 配置资源。", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("运镜配置", _settings, typeof(BattleIntroPathSettings), false);
            _showHandles = EditorGUILayout.Toggle("显示 Scene 控制点", _showHandles);
            if (GUILayout.Button("选中配置资源（Inspector 编辑）")) Selection.activeObject = _settings;
            Editor.CreateCachedEditor(_settings, null, ref _settingsEditor);
            EditorGUI.BeginChangeCheck();
            _settingsEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            if (_settings.useSequence)
            {
                var selectedShot = BattleIntroSequence.ShotAt(_settings, _progress, out _);
                var chosen = GUILayout.Toolbar(selectedShot, ShotNames);
                if (chosen != selectedShot)
                {
                    _progress = ShotProgress(chosen, 0.5f);
                    if (_preview != null && _preview.IsPlaying) _preview.SeekPreview(_progress);
                    SceneView.RepaintAll();
                }
                EditorGUILayout.LabelField("当前分镜", ShotNames[BattleIntroSequence.ShotAt(_settings, _progress, out _)]);
                EditorGUILayout.HelpBox("Shot Durations 为四段时长比例，默认 3 / 3 / 2 / 3.5 秒，总时长由场景组件控制。前两段为透视飞行，敌后镜头切回战斗投影。", MessageType.None);
            }

            var controller = FindObjectOfType<BattleSliceController>();
            if (controller != null)
            {
                var serialized = new SerializedObject(controller);
                serialized.Update();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("playBattleIntro"), new GUIContent("启用战前运镜"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("battleIntroDuration"), new GUIContent("播放时长（秒，非运行模式修改）"));
                }
                serialized.ApplyModifiedProperties();
            }
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入正式战斗后点“开始预览”。预览期间 UI 隐藏、战斗暂停；拖动进度条查看画面。关闭窗口将继续播放。", MessageType.Info);
                using (new EditorGUI.DisabledScope(controller == null))
                    if (GUILayout.Button("开始 / 重新预览") && controller.StartIntroPreview())
                    {
                        _preview = controller.GetComponent<BattleIntroDirector>();
                        _progress = 0f;
                    }
                if (_preview != null && _preview.IsPlaying)
                {
                    CaptureRuntimeFrame();
                    if (!_preview.PreviewPaused) _progress = _preview.Progress;
                    EditorGUI.BeginChangeCheck();
                    _progress = EditorGUILayout.Slider("预览进度", _progress, 0f, 1f);
                    if (EditorGUI.EndChangeCheck()) _preview.SeekPreview(_progress);
                    if (GUILayout.Button("继续播放")) _preview.PreviewPaused = false;
                    if (GUILayout.Button("结束预览并恢复战斗")) { _preview.Cancel(); _preview = null; }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("编辑模式显示按地图尺寸计算的路径示意；精确画面请进入正式战斗后预览。", MessageType.None);
                _columns = Mathf.Max(1, EditorGUILayout.IntField("参考地图列数", _columns));
                _rows = Mathf.Max(1, EditorGUILayout.IntField("参考地图行数", _rows));
                if (GUILayout.Button("更新参考机位")) RebuildReference();
                _progress = EditorGUILayout.Slider("路径预览进度", _progress, 0f, 1f);
            }
            if (GUILayout.Button("Scene 视图对准预览镜头"))
            {
                Evaluate(_progress, out var position, out var rotation, out var size, out var perspective);
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.orthographic = !perspective;
                    SceneView.lastActiveSceneView.LookAtDirect(position + rotation * Vector3.forward * _size * size,
                        rotation, _size * size);
                }
            }
            if (GUILayout.Button("保存运镜配置")) AssetDatabase.SaveAssetIfDirty(_settings);
            EditorGUILayout.EndScrollView();
        }

        private void CaptureRuntimeFrame()
        {
            _home = _preview.HomePosition;
            _rotation = _preview.HomeRotation;
            _center = _preview.BattlefieldCenter;
            _size = _preview.HomeSize;
            _sequence = _preview.Sequence;
        }

        private void RebuildReference()
        {
            var map = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CompanyWarRE/Content/Maps/SkyBattlefield/PF_SkyBattlefield.prefab");
            var controller = FindObjectOfType<BattleSliceController>();
            if (controller != null)
            {
                var assigned = new SerializedObject(controller).FindProperty("battleMapPrefab").objectReferenceValue as FormalBattleMapView;
                if (assigned != null) map = assigned.gameObject;
            }
            var sky = map != null ? map.GetComponent<SkyBattlefieldSettings>() : null;
            if (sky == null) { _center = Vector3.zero; _home = new Vector3(0, 60, -60); _rotation = Quaternion.Euler(45, 0, 0); _size = 40; return; }
            var data = new SerializedObject(sky);
            var scale = data.FindProperty("visualScale").floatValue;
            var mapper = new BattleBoardCoordinateMapper(_columns, _rows,
                data.FindProperty("pillarWidth").floatValue * scale, data.FindProperty("gapRatio").floatValue, 0f,
                new ScaledBattlePillarHeightProvider(new SeededRandomBattlePillarHeightProvider(
                    data.FindProperty("minimumHeight").floatValue, data.FindProperty("maximumHeight").floatValue,
                    data.FindProperty("heightSeed").intValue), scale));
            var anchor = map.GetComponent<FormalBattleMapView>().BattleBoardAnchor;
            _sequence = new BattleIntroSequence(mapper, anchor.localToWorldMatrix, anchor.rotation,
                new Vector3(0f, mapper.MaximumPillarTopY, mapper.VisualLength * 0.3f),
                map.GetComponent<FormalBattleMapView>().EnvironmentRoot);
            var temporary = new GameObject("IntroReferenceCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = temporary.AddComponent<Camera>();
                camera.enabled = false;
                camera.aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
                var rig = temporary.AddComponent<BattleSliceCameraRig>();
                rig.ConfigureVisualSize(mapper.VisualWidth, mapper.VisualLength, mapper.AveragePillarTopY, anchor);
                _home = camera.transform.position;
                _rotation = camera.transform.rotation;
                _center = anchor.TransformPoint(new Vector3(0f, mapper.AveragePillarTopY, 0f));
                _size = camera.orthographicSize;
            }
            finally { DestroyImmediate(temporary); }
            SceneView.RepaintAll();
        }

        private void DrawScene(SceneView scene)
        {
            if (!_showHandles || _settings == null) return;
            if (_preview != null && _preview.IsPlaying) CaptureRuntimeFrame();
            if (_settings.useSequence && _sequence != null) { DrawSequence(); return; }
            _settings.GetControlPoints(_home, _rotation, _center, out var start, out var first, out var second, out var look);
            Handles.DrawBezier(start, _home, first, second, Color.cyan, null, 3f);
            Handles.color = Color.gray;
            Handles.DrawDottedLine(start, first, 5f);
            Handles.DrawDottedLine(second, _home, 5f);
            var scale = Mathf.Max(10f, Vector3.Distance(_home, _center));
            MovePoint("开场机位", start, ref _settings.startOffset, scale);
            MovePoint("控制点 A", first, ref _settings.firstControlOffset, scale);
            MovePoint("控制点 B", second, ref _settings.secondControlOffset, scale);
            MovePoint("注视目标", look, ref _settings.lookAtOffset, scale);
            Handles.Label(_home, "终点：正式战斗机位（自动）");
            _settings.Evaluate(_home, _rotation, _center, _progress, out var position, out var rotation, out _);
            Handles.color = Color.yellow;
            Handles.ArrowHandleCap(0, position, rotation, HandleUtility.GetHandleSize(position), EventType.Repaint);
        }

        private void MovePoint(string label, Vector3 position, ref Vector3 offset, float scale)
        {
            Handles.Label(position, label);
            EditorGUI.BeginChangeCheck();
            var changed = Handles.PositionHandle(position, _rotation);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(_settings, "调整战前运镜控制点");
            offset += Quaternion.Inverse(_rotation) * (changed - position) / scale;
            EditorUtility.SetDirty(_settings);
            Repaint();
        }

        private float ShotProgress(int shot, float t)
        {
            var total = 0f;
            var before = 0f;
            for (var i = 0; i < 4; i++)
            {
                var duration = Mathf.Max(0.1f, _settings.shotDurations[i]);
                total += duration;
                if (i < shot) before += duration;
            }
            return (before + Mathf.Max(0.1f, _settings.shotDurations[shot]) * t) / total;
        }

        private void Evaluate(float progress, out Vector3 position, out Quaternion rotation, out float size, out bool perspective)
        {
            perspective = false;
            if (_settings.useSequence && _sequence != null)
                _sequence.Evaluate(_settings, _home, _rotation, _center, progress, out position, out rotation, out size, out perspective, out _);
            else _settings.Evaluate(_home, _rotation, _center, progress, out position, out rotation, out size);
        }

        private void DrawSequence()
        {
            var colors = new[] { Color.cyan, Color.green, Color.yellow, new Color(1f, 0.5f, 0.2f) };
            for (var shot = 0; shot < 4; shot++)
            {
                var points = new Vector3[41];
                for (var i = 0; i < points.Length; i++)
                {
                    var p = ShotProgress(shot, i / 40f);
                    if (i == 40 && shot < 3) p -= 0.00001f;
                    Evaluate(p, out points[i], out _, out _, out _);
                }
                Handles.color = colors[shot];
                Handles.DrawAAPolyLine(3f, points);
                Handles.Label(points[0], ShotNames[shot]);
            }
            var board = _sequence.Board;
            var dimensions = new Vector3(board.VisualWidth, board.PillarWidth, board.VisualLength);
            var entryBase = new Vector3(0f, board.MinimumPillarTopY, 0f);
            MoveSequencePoint("云海入口", entryBase, dimensions, ref _settings.cloudEntryOffset);
            var span = Mathf.Max(board.VisualWidth, board.VisualLength);
            MoveSequencePoint("敌后机位", _sequence.EnemyFocus, Vector3.one * span, ref _settings.enemyOverviewOffset);
            _sequence.GetTunnel(_settings, out var tunnel, out _);
            var world = _sequence.BoardToWorld.MultiplyPoint3x4(tunnel);
            Handles.Label(world, "柱间高度（仅上下拖动；通道由 Tunnel Gap 选择）");
            EditorGUI.BeginChangeCheck();
            var changed = Handles.Slider(world, _sequence.BoardRotation * Vector3.up);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_settings, "调整柱间镜头高度");
                var delta = _sequence.BoardToWorld.inverse.MultiplyVector(changed - world);
                _settings.tunnelDepth = Mathf.Clamp(_settings.tunnelDepth - delta.y / board.PillarWidth, 0.3f, 2f);
                EditorUtility.SetDirty(_settings);
                Repaint();
            }
            Evaluate(_progress, out var position, out var rotation, out _, out _);
            Handles.ArrowHandleCap(0, position, rotation, HandleUtility.GetHandleSize(position), EventType.Repaint);
        }

        private void MoveSequencePoint(string label, Vector3 origin, Vector3 dimensions, ref Vector3 offset)
        {
            var world = _sequence.BoardToWorld.MultiplyPoint3x4(origin + Vector3.Scale(offset, dimensions));
            Handles.Label(world, label);
            EditorGUI.BeginChangeCheck();
            var changed = Handles.PositionHandle(world, _sequence.BoardRotation);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(_settings, "调整分镜机位");
            var delta = _sequence.BoardToWorld.inverse.MultiplyVector(changed - world);
            offset += new Vector3(delta.x / dimensions.x, delta.y / dimensions.y, delta.z / dimensions.z);
            EditorUtility.SetDirty(_settings);
            Repaint();
        }
    }
}
