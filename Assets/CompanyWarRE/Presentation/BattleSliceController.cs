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
        [SerializeField] private TextAsset legacyUnitsJson;
        [SerializeField] private TextAsset legacyEnemiesJson;
        [SerializeField] private TextAsset sliceSettingsJson;
        [SerializeField] private TextAsset legacySpawnSchedulesJson;
        [SerializeField] private TextAsset legacyLevelJson;

        private readonly Dictionary<GridPosition, BattleSliceCellView> _cellViews =
            new Dictionary<GridPosition, BattleSliceCellView>();
        private readonly Dictionary<string, BattleSliceCombatantView> _combatantViews =
            new Dictionary<string, BattleSliceCombatantView>(System.StringComparer.Ordinal);

        private IArchitecture _architecture;
        private BattleSliceSnapshot _snapshot;
        private GridPosition _selected = new GridPosition(1, 1);
        private int _actorSequence = 1;
        private string _lastAction = "Ready";
        private string _configurationError;
        private bool _isReady;
        private Transform _combatantRoot;

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
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
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
            if (Input.GetMouseButtonDown(1))
            {
                DeploySelected();
            }
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

            for (var column = 1; column <= _snapshot.Columns; column++)
            {
                for (var row = 1; row <= _snapshot.Rows; row++)
                {
                    var position = new GridPosition(column, row);
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cell_{column}_{row}";
                    cell.transform.SetParent(root, false);
                    cell.transform.localPosition = new Vector3(column - 1, 0f, row - 1);
                    cell.transform.localScale = new Vector3(0.9f, 0.18f, 0.9f);
                    var view = cell.AddComponent<BattleSliceCellView>();
                    view.Initialize(position);
                    _cellViews.Add(position, view);
                }
            }
        }

        private void RefreshView()
        {
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
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

                view.Render(combatant);
            }

            foreach (var pair in _combatantViews)
            {
                if (!activeActorIds.Contains(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }

        private static void EnsureSceneInfrastructure(int columns, int rows)
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                var center = new Vector3((columns - 1) * 0.5f, 0f, (rows - 1) * 0.5f);
                var extent = Mathf.Max(columns, rows);
                camera.transform.position = new Vector3(center.x, extent * 1.3f, center.z - extent * 1.05f);
                camera.transform.LookAt(center);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
            }

            if (FindObjectOfType<Light>() == null)
            {
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
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

            GUILayout.BeginArea(new Rect(16f, 16f, 540f, 270f), GUI.skin.box);
            GUILayout.Label("Company War-RE | U01 vs E01 combat slice");
            GUILayout.Label($"Resources: {_snapshot.Resources}    Time: {_snapshot.ElapsedSeconds:0.0}s");
            GUILayout.Label(
                $"{_snapshot.UnitId} cost: {_snapshot.UnitResourceCost}    " +
                $"cooldown: {_snapshot.RemainingCooldown:0.0}s    Selected: {_selected}");
            GUILayout.Space(6f);
            GUILayout.Label($"Left click: select | Right click / D / Space: deploy {_snapshot.UnitId}");
            GUILayout.Label("P: toggle 3x3 pollution block | R: reset");
            GUILayout.Label("Green owned | Gray unowned | Purple polluted | Cyan ally | Red enemy");
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
                $"Wave: {_snapshot.WaveIndex} / {_snapshot.CurrentWaveStage}    " +
                $"Alive: ally {aliveAllies} / enemy {aliveEnemies}    Spawned: {_snapshot.EnemySpawns.Count}");
            GUILayout.Label(
                $"State: {_snapshot.BattleState}    Buildings: {_snapshot.EnemyBuildingCount}    " +
                $"Assault: {_snapshot.AssaultScore}/{_snapshot.RequiredAssaultScore}    " +
                $"Spawn columns: {_snapshot.ValidSpawnPointCount}");
            if (_snapshot.CombatEvents.Count > 0)
            {
                GUILayout.Label(Describe(_snapshot.CombatEvents[_snapshot.CombatEvents.Count - 1]));
            }
            GUILayout.Space(6f);
            GUILayout.Label(_lastAction);
            GUILayout.EndArea();
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
