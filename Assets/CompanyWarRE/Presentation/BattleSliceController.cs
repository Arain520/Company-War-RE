using System.Collections.Generic;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using QFramework;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceController : MonoBehaviour, IController
    {
        private readonly Dictionary<GridPosition, BattleSliceCellView> _cellViews =
            new Dictionary<GridPosition, BattleSliceCellView>();

        private IArchitecture _architecture;
        private BattleSliceSnapshot _snapshot;
        private GridPosition _selected = new GridPosition(1, 1);
        private int _actorSequence = 1;
        private string _lastAction = "Ready";

        public IArchitecture GetArchitecture()
        {
            return BattleSliceArchitecture.Interface;
        }

        private void Awake()
        {
            _architecture = GetArchitecture();
            EnsureSceneInfrastructure();
            BuildGrid();
            ResetSlice();
        }

        private void Update()
        {
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

            for (var column = 1; column <= BattleSliceModel.Columns; column++)
            {
                for (var row = 1; row <= BattleSliceModel.Rows; row++)
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
        }

        private static void EnsureSceneInfrastructure()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                camera.transform.position = new Vector3(2.5f, 8.5f, -5.5f);
                camera.transform.LookAt(new Vector3(2.5f, 0f, 2.5f));
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
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 190f), GUI.skin.box);
            GUILayout.Label("Company War-RE | Domain vertical slice");
            GUILayout.Label($"Resources: {_snapshot.Resources}    Time: {_snapshot.ElapsedSeconds:0.0}s");
            GUILayout.Label($"U01 cooldown: {_snapshot.RemainingCooldown:0.0}s    Selected: {_selected}");
            GUILayout.Space(6f);
            GUILayout.Label("Left click: select | Right click / D / Space: deploy U01");
            GUILayout.Label("P: toggle 3x3 pollution block | R: reset");
            GUILayout.Label("Green owned | Gray unowned | Purple polluted | Cyan unit");
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
                default:
                    return failure.ToString();
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
