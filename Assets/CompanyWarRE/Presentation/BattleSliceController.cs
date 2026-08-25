using System.Collections.Generic;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;
using QFramework;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceController : MonoBehaviour, IController
    {
        internal const float ColumnGroupGap = 0.7f;
        internal const float RowGroupGap = 0.7f;

        [SerializeField] private TextAsset legacyUnitsJson;
        [SerializeField] private TextAsset legacyEnemiesJson;
        [SerializeField] private TextAsset sliceSettingsJson;
        [SerializeField] private TextAsset legacySpawnSchedulesJson;
        [SerializeField] private TextAsset legacyLevelJson;

        private readonly Dictionary<GridPosition, BattleSliceCellView> _cellViews =
            new Dictionary<GridPosition, BattleSliceCellView>();
        private readonly Dictionary<string, BattleSliceCombatantView> _combatantViews =
            new Dictionary<string, BattleSliceCombatantView>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, double> _previousHitPoints =
            new Dictionary<string, double>(System.StringComparer.Ordinal);

        private IArchitecture _architecture;
        private BattleSliceSnapshot _snapshot;
        private GridPosition _selected = new GridPosition(1, 1);
        private int _actorSequence = 1;
        private string _lastAction = "Ready";
        private string _configurationError;
        private bool _isReady;
        private Transform _combatantRoot;
        private BattleSliceFeedbackLayer _feedbackLayer;
        private int _processedCombatEventCount;

        public IArchitecture GetArchitecture()
        {
            return BattleSliceArchitecture.Interface;
        }

        private void Awake()
        {
            _architecture = GetArchitecture();
            if (!TryConfigureSlice())
            {
                return;
            }

            EnsureSceneInfrastructure(_snapshot.Columns, _snapshot.Rows);
            BuildGrid();
            _isReady = true;
            RefreshView();
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(Time.deltaTime));
            ProcessPointerInput();
            ProcessKeyboardInput();
            RefreshView();
        }

        private void ResetSlice()
        {
            _architecture.SendCommand(new ResetBattleSliceCommand());
            _selected = new GridPosition(1, 1);
            _actorSequence = 1;
            _processedCombatEventCount = 0;
            _previousHitPoints.Clear();
            if (_feedbackLayer != null)
            {
                _feedbackLayer.Clear();
            }
            _lastAction = "Slice reset";
            RefreshView();
        }

        private bool TryConfigureSlice()
        {
            var documents = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (legacyUnitsJson != null)
            {
                documents["legacy-units"] = legacyUnitsJson.text;
            }

            if (sliceSettingsJson != null)
            {
                documents["slice-settings"] = sliceSettingsJson.text;
            }

            if (legacyEnemiesJson != null)
            {
                documents["legacy-enemies"] = legacyEnemiesJson.text;
            }

            if (legacySpawnSchedulesJson != null)
            {
                documents["legacy-spawn-schedules"] = legacySpawnSchedulesJson.text;
            }

            if (legacyLevelJson != null)
            {
                documents["legacy-level"] = legacyLevelJson.text;
            }

            var provider = new LegacyBattleSliceConfigurationProvider(
                new DictionaryConfigurationTextSource(documents));
            var result = provider.Load(
                "legacy-units",
                "legacy-enemies",
                "slice-settings",
                "legacy-spawn-schedules",
                "legacy-level");
            if (!result.Succeeded)
            {
                _configurationError = string.Join("\n", result.Issues);
                _lastAction = "Configuration failed";
                Debug.LogError("BattleSlice configuration failed:\n" + _configurationError, this);
                return false;
            }

            _architecture.SendCommand(new ConfigureBattleSliceCommand(result.Configuration));
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            _configurationError = null;
            _lastAction =
                $"Loaded legacy {result.Configuration.TestUnit.Id} vs {result.Configuration.EnemyCombatant.Id}";
            return true;
        }

        private void DeploySelected()
        {
            var actorId = $"test-unit-{_actorSequence:00}";
            var response = _architecture.SendCommand(
                new DeployBattleSliceUnitCommand(_selected, actorId));
            if (response.Succeeded)
            {
                _actorSequence++;
                _lastAction = $"Deployed {actorId} at {_selected}";
            }
            else
            {
                _lastAction = $"Rejected: {Describe(response.Failure)}";
            }
        }

        private void TogglePollution()
        {
            var changed = _architecture.SendCommand(new ToggleBattleSlicePollutionCommand(_selected));
            _lastAction = changed > 0
                ? $"Pollution toggled for {changed} cells"
                : "Pollution unchanged";
        }

        private void ProcessPointerInput()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition), out var hit))
            {
                return;
            }

            var cellView = hit.collider.GetComponentInParent<BattleSliceCellView>();
            if (cellView == null)
            {
                return;
            }

            _selected = cellView.Position;
            _lastAction = $"Selected {_selected}";
        }

        private void ProcessKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.D))
            {
                DeploySelected();
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                TogglePollution();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSlice();
            }
        }

        private void BuildGrid()
        {
            var root = new GameObject("RuntimeGrid").transform;
            root.SetParent(transform, false);
            _combatantRoot = new GameObject("RuntimeCombatants").transform;
            _combatantRoot.SetParent(transform, false);
            var feedbackRoot = new GameObject("RuntimeFeedback");
            feedbackRoot.transform.SetParent(transform, false);
            _feedbackLayer = feedbackRoot.AddComponent<BattleSliceFeedbackLayer>();

            for (var column = 1; column <= _snapshot.Columns; column++)
            {
                for (var row = 1; row <= _snapshot.Rows; row++)
                {
                    var position = new GridPosition(column, row);
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cell_{column}_{row}";
                    cell.transform.SetParent(root, false);
                    cell.transform.localPosition = new Vector3(
                        GetColumnWorldX(column),
                        0f,
                        GetRowWorldZ(row));
                    cell.transform.localScale = new Vector3(0.84f, 0.16f, 0.84f);
                    var view = cell.AddComponent<BattleSliceCellView>();
                    view.Initialize(position);
                    _cellViews.Add(position, view);
                }
            }
        }

        private void RefreshView()
        {
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            var directAttackTargets = ProcessNewCombatFeedback();
            var hasLivingE13 = false;
            foreach (var combatant in _snapshot.Combatants)
            {
                if (combatant.IsAlive &&
                    combatant.Team == Team.Enemy &&
                    string.Equals(combatant.TemplateId, "E13", System.StringComparison.OrdinalIgnoreCase))
                {
                    hasLivingE13 = true;
                    break;
                }
            }

            foreach (var cell in _snapshot.Cells)
            {
                if (_cellViews.TryGetValue(cell.Position, out var view))
                {
                    view.Render(cell, cell.Position.Equals(_selected));
                }
            }

            var activeActorIds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var combatant in _snapshot.Combatants)
            {
                activeActorIds.Add(combatant.ActorId);
                if (!_combatantViews.TryGetValue(combatant.ActorId, out var view))
                {
                    var actorObject = new GameObject("Combatant_" + combatant.ActorId);
                    actorObject.transform.SetParent(_combatantRoot, false);
                    view = actorObject.AddComponent<BattleSliceCombatantView>();
                    view.Initialize(combatant.ActorId);
                    _combatantViews.Add(combatant.ActorId, view);
                }

                var lostHitPoints = _previousHitPoints.TryGetValue(combatant.ActorId, out var previousHitPoints) &&
                                    combatant.HitPoints < previousHitPoints;
                var curseDamage = hasLivingE13 &&
                                  combatant.Team == Team.Ally &&
                                  lostHitPoints &&
                                  !directAttackTargets.Contains(combatant.ActorId);
                view.Render(combatant, curseDamage);

                if (_previousHitPoints.TryGetValue(combatant.ActorId, out previousHitPoints) &&
                    combatant.HitPoints > previousHitPoints &&
                    string.Equals(combatant.TemplateId, "E14", System.StringComparison.OrdinalIgnoreCase))
                {
                    _feedbackLayer?.Show(
                        "+1 HEAL",
                        GetCombatantWorldPosition(combatant),
                        new Color(0.3f, 1f, 0.45f));
                }

                if (curseDamage)
                {
                    _feedbackLayer?.Show(
                        "CURSE",
                        GetCombatantWorldPosition(combatant),
                        new Color(0.85f, 0.25f, 1f));
                }

                _previousHitPoints[combatant.ActorId] = combatant.HitPoints;
            }

            foreach (var pair in _combatantViews)
            {
                if (!activeActorIds.Contains(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }

        private HashSet<string> ProcessNewCombatFeedback()
        {
            var directAttackTargets = new HashSet<string>(System.StringComparer.Ordinal);
            if (_snapshot == null)
            {
                return directAttackTargets;
            }

            var combatants = new Dictionary<string, BattleSliceCombatantSnapshot>(System.StringComparer.Ordinal);
            foreach (var combatant in _snapshot.Combatants)
            {
                combatants[combatant.ActorId] = combatant;
            }

            var startIndex = Mathf.Clamp(_processedCombatEventCount, 0, _snapshot.CombatEvents.Count);
            for (var index = startIndex; index < _snapshot.CombatEvents.Count; index++)
            {
                var combatEvent = _snapshot.CombatEvents[index];
                if (combatEvent.Type == CombatEventType.Attack)
                {
                    directAttackTargets.Add(combatEvent.TargetActorId);
                    if (combatants.TryGetValue(combatEvent.ActorId, out var source) &&
                        combatants.TryGetValue(combatEvent.TargetActorId, out var target) &&
                        string.Equals(source.TemplateId, "E15", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _feedbackLayer?.Show(
                            "EXECUTE",
                            GetCombatantWorldPosition(target),
                            new Color(1f, 0.15f, 0.75f),
                            1.4f);
                    }
                }
                else if (combatEvent.Type == CombatEventType.Pollution)
                {
                    _feedbackLayer?.Show(
                        "POLLUTED",
                        new Vector3(
                            GetColumnWorldX(combatEvent.Column),
                            0.25f,
                            GetRowWorldZ(combatEvent.Row)),
                        new Color(0.75f, 0.2f, 0.9f));
                }
                else if (combatEvent.Type == CombatEventType.Death &&
                         combatants.TryGetValue(combatEvent.ActorId, out var defeated))
                {
                    _feedbackLayer?.Show(
                        defeated.IsBuilding ? "BUILDING DOWN" : "DOWN",
                        GetCombatantWorldPosition(defeated),
                        new Color(1f, 0.35f, 0.25f));
                }
            }

            _processedCombatEventCount = _snapshot.CombatEvents.Count;
            return directAttackTargets;
        }

        private static void EnsureSceneInfrastructure(int columns, int rows)
        {
            Camera camera;
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            }
            else
            {
                camera = Camera.main;
            }

            var cameraRig = camera.GetComponent<BattleSliceCameraRig>() ??
                            camera.gameObject.AddComponent<BattleSliceCameraRig>();
            cameraRig.Configure(columns, rows);

            if (FindObjectOfType<Light>() == null)
            {
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        internal static float GetColumnWorldX(int column)
        {
            var zeroBasedColumn = Mathf.Max(0, column - 1);
            var completedGroups = zeroBasedColumn / BattleGrid.ControlBlockSize;
            return zeroBasedColumn + completedGroups * ColumnGroupGap;
        }

        internal static float GetRowWorldZ(int row)
        {
            var zeroBasedRow = Mathf.Max(0, row - 1);
            var completedGroups = zeroBasedRow / BattleGrid.ControlBlockSize;
            return zeroBasedRow + completedGroups * RowGroupGap;
        }

        internal static float GetLaneWorldZ(double lanePosition)
        {
            var zeroBasedLane = System.Math.Max(0d, lanePosition - 1d);
            var completedGroups = (int)System.Math.Floor(zeroBasedLane / BattleGrid.ControlBlockSize);
            return (float)zeroBasedLane + completedGroups * RowGroupGap;
        }

        internal static Vector3 GetCombatantWorldPosition(BattleSliceCombatantSnapshot combatant)
        {
            var worldX = GetColumnWorldX(combatant.Column);
            var worldZ = GetLaneWorldZ(combatant.LanePosition);
            if (combatant.IsBuilding)
            {
                worldX = (GetColumnWorldX(combatant.FootprintStartColumn) +
                          GetColumnWorldX(combatant.FootprintEndColumn)) * 0.5f;
                worldZ = (GetRowWorldZ(combatant.FootprintStartRow) +
                          GetRowWorldZ(combatant.FootprintEndRow)) * 0.5f;
            }

            return new Vector3(worldX, 0.12f, worldZ);
        }

        internal bool IsPointerOverRuntimeHud(Vector2 screenPosition)
        {
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (new Rect(16f, 16f, 570f, 294f).Contains(guiPosition))
            {
                return true;
            }

            if (_snapshot == null || _snapshot.BattleState == BattleState.Running)
            {
                return false;
            }

            const float resultWidth = 420f;
            const float resultHeight = 170f;
            return new Rect(
                (Screen.width - resultWidth) * 0.5f,
                (Screen.height - resultHeight) * 0.5f,
                resultWidth,
                resultHeight).Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (_snapshot == null)
            {
                GUILayout.BeginArea(new Rect(16f, 16f, 520f, 180f), GUI.skin.box);
                GUILayout.Label("Company War-RE | Configuration compatibility slice");
                GUILayout.Label("Configuration load failed:");
                GUILayout.Label(string.IsNullOrWhiteSpace(_configurationError)
                    ? "No configuration was loaded."
                    : _configurationError);
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 570f, 294f), GUI.skin.box);
            GUILayout.Label("Company War-RE | L01 可玩验证");
            GUILayout.Label($"资源: {_snapshot.Resources}    时间: {_snapshot.ElapsedSeconds:0.0}s");
            GUILayout.Label(
                $"{_snapshot.UnitId} 消耗: {_snapshot.UnitResourceCost}    " +
                $"冷却: {_snapshot.RemainingCooldown:0.0}s    选中: {_selected}");
            GUILayout.Space(6f);
            GUILayout.Label($"左键选择 | D / 空格部署 {_snapshot.UnitId} | 右键拖动旋转镜头");
            GUILayout.Label("WASD/方向键平移 | Shift 加速 | 中键拖动 | 滚轮缩放 | F/Home 复位");
            GUILayout.Label("绿色我方 | 灰色敌方 | 紫色污染 | 深红为建筑占用 | 每3行/列分组");
            var aliveAllies = 0;
            var aliveEnemies = 0;
            foreach (var combatant in _snapshot.Combatants)
            {
                if (!combatant.IsAlive)
                {
                    continue;
                }

                if (combatant.Team == Team.Ally)
                {
                    aliveAllies++;
                }
                else
                {
                    aliveEnemies++;
                }
            }

            GUILayout.Label(
                $"波次: {_snapshot.WaveIndex} / {_snapshot.CurrentWaveStage}    " +
                $"存活: 我方 {aliveAllies} / 敌方 {aliveEnemies}    已生成: {_snapshot.EnemySpawns.Count}");
            GUILayout.Label(
                $"状态: {_snapshot.BattleState}    建筑: {_snapshot.EnemyBuildingCount}    " +
                $"突击分: {_snapshot.AssaultScore}/{_snapshot.RequiredAssaultScore}    " +
                $"有效生成列: {_snapshot.ValidSpawnPointCount}");
            if (_snapshot.CombatEvents.Count > 0)
            {
                GUILayout.Label(Describe(_snapshot.CombatEvents[_snapshot.CombatEvents.Count - 1]));
            }
            GUILayout.Space(6f);
            GUILayout.Label(_lastAction);
            GUILayout.EndArea();

            if (_snapshot.BattleState != BattleState.Running)
            {
                var width = 420f;
                var height = 170f;
                GUILayout.BeginArea(
                    new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                    GUI.skin.window);
                GUILayout.Space(18f);
                var resultStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 28,
                    fontStyle = FontStyle.Bold
                };
                GUILayout.Label(
                    _snapshot.BattleState == BattleState.Victory ? "L01 胜利" : "L01 失败",
                    resultStyle);
                GUILayout.Label(
                    $"突击分 {_snapshot.AssaultScore}    剩余建筑 {_snapshot.EnemyBuildingCount}    " +
                    $"耗时 {_snapshot.ElapsedSeconds:0.0}s");
                GUILayout.Space(12f);
                if (GUILayout.Button("重新开始 (R)", GUILayout.Height(36f)))
                {
                    ResetSlice();
                }
                GUILayout.EndArea();
            }
        }

        private static string Describe(DeploymentFailure failure)
        {
            switch (failure)
            {
                case DeploymentFailure.OutsideGrid:
                    return "outside grid";
                case DeploymentFailure.TerritoryNotOwned:
                    return "territory not owned";
                case DeploymentFailure.Polluted:
                    return "cell polluted";
                case DeploymentFailure.Occupied:
                    return "cell occupied";
                case DeploymentFailure.InsufficientResources:
                    return "insufficient resources";
                case DeploymentFailure.CooldownActive:
                    return "cooldown active";
                case DeploymentFailure.DuplicateActorId:
                    return "duplicate actor id";
                case DeploymentFailure.CombatRegistrationRejected:
                    return "combat registration rejected";
                case DeploymentFailure.BattleEnded:
                    return "battle ended";
                default:
                    return failure.ToString();
            }
        }

        private static string Describe(CombatEvent combatEvent)
        {
            switch (combatEvent.Type)
            {
                case CombatEventType.Attack:
                    return $"Attack: {combatEvent.ActorId} -> {combatEvent.TargetActorId}";
                case CombatEventType.MeleeBattlefieldStarted:
                    return $"Battlefield started: CB({combatEvent.ControlBlockColumn}, {combatEvent.ControlBlockRow})";
                case CombatEventType.MeleeBattlefieldEnded:
                    return $"Battlefield ended: CB({combatEvent.ControlBlockColumn}, {combatEvent.ControlBlockRow})";
                case CombatEventType.Pollution:
                    return $"Polluted: ({combatEvent.Column}, {combatEvent.Row})";
                case CombatEventType.Death:
                    return $"Death: {combatEvent.ActorId}";
                default:
                    return combatEvent.Type.ToString();
            }
        }

        private void OnDestroy()
        {
            if (UnityEngine.Application.isPlaying && _architecture != null)
            {
                _architecture.Deinit();
            }
        }
    }
}
