using System.Collections.Generic;
using System.Linq;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using CompanyWarRE.Infrastructure.Configuration;
using CompanyWarRE.Infrastructure.Levels;
using QFramework;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceController : MonoBehaviour, IController
    {
        private static readonly string[] FormalLevelIds =
            Enumerable.Range(0, 21)
                .Select(index => $"L{index:00}")
                .Concat(new[] { "L_ENDLESS" })
                .ToArray();

        public static IReadOnlyList<string> AvailableFormalLevelIds => FormalLevelIds;

        internal const float CellVisualSize = 0.92f;
        internal const float ControlBlockBorderWidth = 0.0625f;
        internal const float ColumnGroupBorderWidth = 0.1f;

        [SerializeField] private TextAsset legacyUnitsJson;
        [SerializeField] private TextAsset legacyEnemiesJson;
        [SerializeField] private TextAsset sliceSettingsJson;
        [SerializeField] private TextAsset legacySpawnSchedulesJson;
        [SerializeField] private TextAsset legacyLevelJson;
        [SerializeField] private bool useFormalLevelConfiguration;
        [SerializeField] private string formalLevelId = "L02";
        [SerializeField] private TextAsset[] formalLevelJsonDocuments;
        [SerializeField] private BattleSliceVisualCatalog visualCatalog;
        [SerializeField] private FormalBattleBoardView battleBoardPrefab;
        [SerializeField] private FormalBattleMapView battleMapPrefab;

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
        private string _activeLevelId = "L01";
        private FormalLevelRuntimeMetadata _formalLevel;
        private Material _controlBlockBorderMaterial;
        private Material _columnGroupBorderMaterial;
        private Transform _runtimeGridRoot;
        private Transform _feedbackRoot;
        private Transform _runtimeBoardRoot;
        private FormalBattleMapView _runtimeMap;
        private Transform _boardAnchor;
        private FormalGameFlowSnapshot _flowSnapshot;
        private BattleState _reportedBattleState = BattleState.Running;
        private string _selectedUnitId;
        private bool _runtimeHudVisible = true;
        private bool _runtimeUiPointerBlocked;
        private FormalSaveSession _saveSession;
        private string _saveStatus = "Save not initialized";
        private FormalAudioService _audioService;
        private IProductionAssetProvider _assetProvider;
        private ProductionSceneLoader _sceneLoader;
        private ProductionComponentPool<BattleSliceCombatantView> _combatantViewPool;
        private Transform _poolRoot;
        private Material _cellSharedMaterial;
        private BattleRuntimePerformanceMonitor _performanceMonitor;

        public BattleSliceSnapshot CurrentSnapshot => _snapshot;
        public FormalGameFlowSnapshot CurrentFlow => _flowSnapshot;
        public string ActiveLevelId => _activeLevelId;
        public string SelectedUnitId => _selectedUnitId;
        public string SaveStatus => _saveStatus;
        public string SavePath => _saveSession?.SavePath ?? string.Empty;
        public bool IsSaveWritable => _saveSession?.State == FormalSaveSessionState.Ready;
        public BattlePerformanceSnapshot Performance =>
            _performanceMonitor != null ? _performanceMonitor.Latest : default;

        public IArchitecture GetArchitecture()
        {
            return BattleSliceArchitecture.Interface;
        }

        private void Awake()
        {
            _architecture = GetArchitecture();
            InitializeProductionInfrastructure();
            if (useFormalLevelConfiguration)
            {
                _architecture.SendCommand(new InitializeFormalGameFlowCommand(FormalLevelIds));
                _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
                InitializeFormalSave();
            }

            _audioService.Apply(_saveSession?.Current?.Settings ?? GameSettingsSave.Default);

            if (visualCatalog == null)
            {
                visualCatalog = GetComponent<BattleSliceVisualCatalog>();
            }

            if (!TryConfigureSlice())
            {
                return;
            }

            if (!EnsureBattleMap())
            {
                return;
            }
            EnsureSceneInfrastructure(_snapshot.Columns, _snapshot.Rows);
            BuildGrid();
            ApplyFormalEnvironment();
            _isReady = true;
            RefreshView();
            _selectedUnitId = _snapshot.UnitId;
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            if (useFormalLevelConfiguration)
            {
                _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ToggleFormalPause();
                }
            }

            var canRunBattle = !useFormalLevelConfiguration ||
                               _flowSnapshot.Screen == FormalFlowScreen.Battle;
            var choosingAuthorization = _snapshot.AuthorizationState == AuthorizationState.Choosing;
            if (canRunBattle && !choosingAuthorization)
            {
                _architecture.SendCommand(new AdvanceBattleSliceTimeCommand(Time.deltaTime));
                ProcessPointerInput();
                ProcessKeyboardInput();
            }

            RefreshView();

            if (useFormalLevelConfiguration &&
                _snapshot.BattleState != BattleState.Running &&
                _reportedBattleState == BattleState.Running)
            {
                _reportedBattleState = _snapshot.BattleState;
                var recorded = _architecture.SendCommand(
                    new RecordFormalBattleResultCommand(_activeLevelId, _snapshot.BattleState));
                _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
                if (recorded)
                {
                    RememberSaveResult(_saveSession?.SaveBattleResult(_flowSnapshot, _snapshot));
                }
            }
        }

        private void InitializeProductionInfrastructure()
        {
            _audioService = new FormalAudioService();
            _assetProvider = new ResKitWithResourcesFallbackProvider();
            _sceneLoader = new ProductionSceneLoader();
            _performanceMonitor = GetComponent<BattleRuntimePerformanceMonitor>() ??
                                  gameObject.AddComponent<BattleRuntimePerformanceMonitor>();
            var poolObject = new GameObject("RuntimePools");
            poolObject.transform.SetParent(transform, false);
            _poolRoot = poolObject.transform;
            _combatantViewPool = new ProductionComponentPool<BattleSliceCombatantView>(
                () =>
                {
                    var actorObject = new GameObject("PooledCombatant");
                    actorObject.transform.SetParent(_poolRoot, false);
                    return actorObject.AddComponent<BattleSliceCombatantView>();
                },
                _poolRoot,
                BattleRuntimePerformanceMonitor.RecommendedMaximumCombatantViews);
        }

        private void InitializeFormalSave()
        {
            var paths = UnitySavePathSet.ForCurrentPlatform();
            _saveSession = new FormalSaveSession(
                UnitySaveCompatibilityFactory.CreateForCurrentPlayerPrefs(FormalLevelIds),
                paths.SavePath,
                paths.BackupDirectory);
            var result = _saveSession.Start(_flowSnapshot);
            RememberSaveResult(result);
            if (!result.Succeeded)
            {
                Debug.LogWarning(
                    "Formal save entered read-only protection: " + result.Error + " - " + result.Message,
                    this);
                return;
            }

            _architecture.SendCommand(new RestoreFormalGameFlowCommand(result.Value, FormalLevelIds));
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
            formalLevelId = _flowSnapshot.ActiveLevelId;
        }

        private void ResetSlice()
        {
            _architecture.SendCommand(new ResetBattleSliceCommand());
            _selected = new GridPosition(1, 1);
            _actorSequence = 1;
            _processedCombatEventCount = 0;
            _reportedBattleState = BattleState.Running;
            _previousHitPoints.Clear();
            if (_feedbackLayer != null)
            {
                _feedbackLayer.Clear();
            }
            _lastAction = "Slice reset";
            RefreshView();
            if (string.IsNullOrWhiteSpace(_selectedUnitId) || !_snapshot.DeployList.Contains(_selectedUnitId))
            {
                _selectedUnitId = _snapshot.UnitId;
            }
        }

        public bool StartFormalLevel(string levelId)
        {
            if (!useFormalLevelConfiguration)
            {
                return false;
            }

            var resumeLoadedBattle = _flowSnapshot != null &&
                                     _flowSnapshot.Screen == FormalFlowScreen.MainMenu &&
                                     _snapshot != null &&
                                     _snapshot.BattleState == BattleState.Running &&
                                     string.Equals(
                                         _activeLevelId,
                                         levelId,
                                         System.StringComparison.OrdinalIgnoreCase);
            if (!_architecture.SendCommand(new StartFormalLevelCommand(levelId)))
            {
                return false;
            }

            if (resumeLoadedBattle)
            {
                _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
                _lastAction = "Resumed " + _activeLevelId;
                return true;
            }

            formalLevelId = levelId;
            if (!TryConfigureFormalLevel())
            {
                return false;
            }

            ClearRuntimePresentation();
            if (!EnsureBattleMap())
            {
                _lastAction = "Formal battle map prefab is missing";
                return false;
            }
            EnsureSceneInfrastructure(_snapshot.Columns, _snapshot.Rows);
            BuildGrid();
            ApplyFormalEnvironment();
            _selected = new GridPosition(1, 1);
            _selectedUnitId = _snapshot.DeployList.FirstOrDefault() ?? _snapshot.UnitId;
            _actorSequence = 1;
            _processedCombatEventCount = 0;
            _reportedBattleState = BattleState.Running;
            _previousHitPoints.Clear();
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
            _lastAction = "Started " + _activeLevelId;
            RememberSaveResult(_saveSession?.SaveCampaign(_flowSnapshot));
            RefreshView();
            return true;
        }

        public void OpenFormalLevelSelect()
        {
            if (!useFormalLevelConfiguration)
            {
                return;
            }

            _architecture.SendCommand(new SetFormalFlowScreenCommand(FormalFlowScreen.LevelSelect));
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
        }

        public void ReturnFormalMainMenu()
        {
            if (!useFormalLevelConfiguration)
            {
                return;
            }

            _architecture.SendCommand(new SetFormalFlowScreenCommand(FormalFlowScreen.MainMenu));
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
        }

        public void ToggleFormalPause()
        {
            if (!useFormalLevelConfiguration || _flowSnapshot == null)
            {
                return;
            }

            if (_flowSnapshot.Screen == FormalFlowScreen.LevelSelect)
            {
                ReturnFormalMainMenu();
                return;
            }

            _architecture.SendCommand(new ToggleFormalPauseCommand());
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
        }

        public void RestartFormalLevel()
        {
            if (!useFormalLevelConfiguration)
            {
                ResetSlice();
                return;
            }

            _architecture.SendCommand(new RestartFormalLevelCommand());
            _flowSnapshot = _architecture.SendQuery(new GetFormalGameFlowSnapshotQuery());
            ResetSlice();
        }

        public bool AcceptAuthorization(string unitId)
        {
            if (!_architecture.SendCommand(new AcceptBattleAuthorizationCommand(unitId)))
            {
                return false;
            }

            _selectedUnitId = unitId;
            RefreshView();
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            return true;
        }

        public bool RequestAuthorization()
        {
            if (_snapshot == null ||
                _snapshot.AuthorizationState != AuthorizationState.Available ||
                !_architecture.SendCommand(new BeginBattleAuthorizationChoiceCommand()))
            {
                return false;
            }

            _lastAction = "Authorization requested";
            RefreshView();
            return true;
        }

        public bool CancelAuthorizationChoice()
        {
            if (!_architecture.SendCommand(new CancelBattleAuthorizationChoiceCommand()))
            {
                return false;
            }

            _lastAction = "Authorization choice cancelled";
            RefreshView();
            return true;
        }

        public bool SaveAudioSetting(string layer, float volume, bool muted)
        {
            var result = _saveSession?.SaveAudioSetting(layer, volume, muted);
            RememberSaveResult(result);
            if (result != null && result.Succeeded)
            {
                _audioService?.Apply(result.Value.Settings);
            }
            return result != null && result.Succeeded;
        }

        public void LoadProductionAssetAsync<T>(ProductionAssetKey key,
            System.Action<ProductionAssetLoadResult<T>> completed) where T : UnityEngine.Object
        {
            _assetProvider.LoadAsync(this, key, completed);
        }

        public void LoadProductionSceneAsync(string sceneName, string bundleName = "")
        {
            _sceneLoader.LoadAsync(sceneName, bundleName, UnityEngine.SceneManagement.LoadSceneMode.Single, null);
        }

        public AudioLayerSaveSettings GetSavedAudioSetting(string layer)
        {
            return _saveSession?.Current?.Settings?.FindAudioLayer(layer);
        }

        private void RememberSaveResult(SaveOperationResult<SaveGame> result)
        {
            if (result == null)
            {
                return;
            }

            _saveStatus = result.Succeeded
                ? (string.IsNullOrEmpty(result.BackupPath) ? "Saved" : "Saved with backup")
                : result.Error + ": " + result.Message;
        }

        public bool SelectDeploymentUnit(string unitId)
        {
            if (_snapshot == null || string.IsNullOrWhiteSpace(unitId) ||
                !_snapshot.DeployList.Contains(unitId))
            {
                return false;
            }

            _selectedUnitId = unitId;
            _lastAction = "Selected deployment " + unitId;
            return true;
        }

        public void SetRuntimeHudVisible(bool visible)
        {
            _runtimeHudVisible = visible;
        }

        public void SetRuntimeUiPointerBlocked(bool blocked)
        {
            _runtimeUiPointerBlocked = blocked;
        }

        private void ClearRuntimePresentation()
        {
            ReleaseAllCombatantViews();
            if (_runtimeBoardRoot != null)
            {
                _runtimeBoardRoot.gameObject.SetActive(false);
                Destroy(_runtimeBoardRoot.gameObject);
            }

            _runtimeBoardRoot = null;
            _runtimeGridRoot = null;
            _combatantRoot = null;
            _feedbackRoot = null;
            _feedbackLayer = null;
            _cellViews.Clear();
            DestroyGridBorderMaterials();
            DestroyCellSharedMaterial();
        }

        private void DestroyGridBorderMaterials()
        {
            if (_controlBlockBorderMaterial != null)
            {
                Destroy(_controlBlockBorderMaterial);
                _controlBlockBorderMaterial = null;
            }

            if (_columnGroupBorderMaterial != null)
            {
                Destroy(_columnGroupBorderMaterial);
                _columnGroupBorderMaterial = null;
            }
        }

        private void DestroyCellSharedMaterial()
        {
            if (_cellSharedMaterial != null)
            {
                Destroy(_cellSharedMaterial);
                _cellSharedMaterial = null;
            }
        }

        private bool TryConfigureSlice()
        {
            return useFormalLevelConfiguration
                ? TryConfigureFormalLevel()
                : TryConfigureLegacySlice();
        }

        private bool TryConfigureFormalLevel()
        {
            var documents = BuildSharedDocuments();
            var selected = FindFormalLevelDocument(formalLevelId);
            if (selected != null)
            {
                documents["formal-level"] = selected.text;
            }

            var result = new FormalLevelConfigurationPipeline(
                    new DictionaryConfigurationTextSource(documents))
                .Load(
                    "legacy-units",
                    "legacy-enemies",
                    "legacy-spawn-schedules",
                    "formal-level");
            if (!result.Succeeded)
            {
                return RejectConfiguration("Formal level configuration failed", result.Issues);
            }

            if (!string.Equals(result.Level.LevelId, formalLevelId, System.StringComparison.OrdinalIgnoreCase))
            {
                _configurationError =
                    $"Selected level '{formalLevelId}' resolved document '{result.Level.LevelId}'.";
                _lastAction = "Configuration failed";
                Debug.LogError("Formal level selection failed:\n" + _configurationError, this);
                return false;
            }

            var configuration = _saveSession?.ApplyGrowth(result.Configuration) ?? result.Configuration;
            _architecture.SendCommand(new ConfigureBattleSliceCommand(configuration));
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            _formalLevel = result.Level;
            _activeLevelId = result.Level.LevelId;
            _configurationError = null;
            _lastAction = $"Loaded formal {_activeLevelId}";
            return true;
        }

        private bool TryConfigureLegacySlice()
        {
            var documents = BuildSharedDocuments();

            if (sliceSettingsJson != null)
            {
                documents["slice-settings"] = sliceSettingsJson.text;
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
                return RejectConfiguration("BattleSlice configuration failed", result.Issues);
            }

            _architecture.SendCommand(new ConfigureBattleSliceCommand(result.Configuration));
            _snapshot = _architecture.SendQuery(new GetBattleSliceSnapshotQuery());
            _formalLevel = null;
            _activeLevelId = "L01";
            _configurationError = null;
            _lastAction =
                $"Loaded legacy {result.Configuration.TestUnit.Id} vs {result.Configuration.EnemyCombatant.Id}";
            return true;
        }

        private Dictionary<string, string> BuildSharedDocuments()
        {
            var documents = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (legacyUnitsJson != null)
            {
                documents["legacy-units"] = legacyUnitsJson.text;
            }

            if (legacyEnemiesJson != null)
            {
                documents["legacy-enemies"] = legacyEnemiesJson.text;
            }

            if (legacySpawnSchedulesJson != null)
            {
                documents["legacy-spawn-schedules"] = legacySpawnSchedulesJson.text;
            }

            return documents;
        }

        private TextAsset FindFormalLevelDocument(string requestedLevelId)
        {
            if (string.IsNullOrWhiteSpace(requestedLevelId))
            {
                return null;
            }

            var normalizedId = requestedLevelId.Trim();
            var cowDocument = Resources.Load<TextAsset>(
                "CompanyWarRE/Configs/Levels/" + normalizedId);
            if (cowDocument != null)
            {
                return cowDocument;
            }

            if (formalLevelJsonDocuments == null)
            {
                return null;
            }

            var expectedName = "FormalLevel." + normalizedId;
            foreach (var document in formalLevelJsonDocuments)
            {
                if (document != null && string.Equals(
                        document.name,
                        expectedName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }

            return null;
        }

        private bool RejectConfiguration(
            string heading,
            System.Collections.Generic.IEnumerable<ConfigurationIssue> issues)
        {
            _configurationError = string.Join("\n", issues);
            _lastAction = "Configuration failed";
            Debug.LogError(heading + ":\n" + _configurationError, this);
            return false;
        }

        private void ApplyFormalEnvironment()
        {
            if (_formalLevel == null)
            {
                return;
            }

            if (_runtimeMap != null)
            {
                _runtimeMap.PrepareForBattle(_snapshot.Columns, _snapshot.Rows);
                return;
            }

            Debug.LogError(
                "Formal battle map prefab is missing. Runtime environment generation is disabled; " +
                "create or bake the map through the Formal Battle Map editor.",
                this);
        }

        private bool EnsureBattleMap()
        {
            if (!useFormalLevelConfiguration)
            {
                _boardAnchor = transform;
                return true;
            }

            if (battleMapPrefab == null)
            {
                _runtimeMap = null;
                _boardAnchor = null;
                _configurationError =
                    "Formal battle requires an explicitly assigned, editor-baked map prefab.";
                Debug.LogError(_configurationError, this);
                return false;
            }

            if (_runtimeMap == null)
            {
                _runtimeMap = Instantiate(battleMapPrefab, transform, false);
                _runtimeMap.gameObject.name = battleMapPrefab.gameObject.name;
            }

            _runtimeMap.PrepareForBattle(_snapshot.Columns, _snapshot.Rows);
            _boardAnchor = _runtimeMap.BattleBoardAnchor;
            return true;
        }

        private void DeploySelected()
        {
            var actorId = $"test-unit-{_actorSequence:00}";
            var response = _architecture.SendCommand(
                new DeployBattleSliceUnitCommand(_selected, actorId, _selectedUnitId));
            if (response.Succeeded)
            {
                _actorSequence++;
                _lastAction = $"Deployed {_selectedUnitId} as {actorId} at {_selected}";
                ShowDeploymentFeedback(_selectedUnitId, _selected);
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
            if (_runtimeUiPointerBlocked || !Input.GetMouseButtonDown(0))
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
            DestroyCellSharedMaterial();
            _cellSharedMaterial = CreateGridBorderMaterial(Color.white);
            var configuredBoardPrefab = battleBoardPrefab != null
                ? battleBoardPrefab
                : Resources.Load<FormalBattleBoardView>(
                    "CompanyWarRE/Battle/PF_FormalBattleBoard");
            var parent = _boardAnchor != null ? _boardAnchor : transform;
            var board = configuredBoardPrefab != null
                ? Instantiate(configuredBoardPrefab, parent, false)
                : new GameObject("FormalBattleBoardFallback")
                    .AddComponent<FormalBattleBoardView>();
            if (board.transform.parent == null)
            {
                board.transform.SetParent(parent, false);
            }
            board.transform.localPosition = Vector3.zero;
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = Vector3.one;
            board.Prepare(_snapshot.Columns, _snapshot.Rows);
            _runtimeBoardRoot = board.transform;
            _runtimeGridRoot = board.GridRoot;
            _combatantRoot = board.CombatantRoot;
            _feedbackRoot = board.FeedbackRoot;
            _feedbackLayer = _feedbackRoot.GetComponent<BattleSliceFeedbackLayer>() ??
                             _feedbackRoot.gameObject.AddComponent<BattleSliceFeedbackLayer>();

            for (var column = 1; column <= _snapshot.Columns; column++)
            {
                for (var row = 1; row <= _snapshot.Rows; row++)
                {
                    var position = new GridPosition(column, row);
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cell_{column}_{row}";
                    cell.transform.SetParent(_runtimeGridRoot, false);
                    cell.transform.localPosition = new Vector3(
                        GetCenteredColumnCoordinate(column, _snapshot.Columns),
                        board.SurfaceOffsetY,
                        GetCenteredRowCoordinate(row, _snapshot.Rows));
                    cell.transform.localScale = new Vector3(
                        board.CellVisualFill,
                        board.CellHeight,
                        board.CellVisualFill);
                    var view = cell.AddComponent<BattleSliceCellView>();
                    view.Initialize(position, _cellSharedMaterial);
                    view.ApplyCowBoardPalette(board);
                    _cellViews.Add(position, view);
                }
            }

            BuildGridBorders(board, _runtimeGridRoot, _snapshot.Columns, _snapshot.Rows);
        }

        private void BuildGridBorders(
            FormalBattleBoardView board,
            Transform gridRoot,
            int columns,
            int rows)
        {
            var borderRoot = new GameObject("ControlBlockBorders").transform;
            borderRoot.SetParent(gridRoot, false);
            _controlBlockBorderMaterial = CreateGridBorderMaterial(board.ControlBlockBorderColor);
            _columnGroupBorderMaterial = CreateGridBorderMaterial(
                Color.Lerp(board.ControlBlockBorderColor, Color.white, 0.18f));

            var borderY = board.SurfaceOffsetY + board.CellHeight * 0.9f;
            for (var boundary = 0; boundary <= columns; boundary += BattleGrid.ControlBlockSize)
            {
                CreateGridBorder(
                    borderRoot,
                    $"ColumnGroupBorder_{boundary}",
                    new Vector3(GetCenteredBoundaryCoordinate(boundary, columns), borderY, 0f),
                    new Vector3(board.ColumnGroupBorderWidth, board.CellHeight * 0.55f, rows),
                    _columnGroupBorderMaterial);
            }

            if (columns % BattleGrid.ControlBlockSize != 0)
            {
                CreateGridBorder(
                    borderRoot,
                    "ColumnGroupBorder_End",
                    new Vector3(columns * 0.5f, borderY, 0f),
                    new Vector3(board.ColumnGroupBorderWidth, board.CellHeight * 0.55f, rows),
                    _columnGroupBorderMaterial);
            }

            for (var boundary = 0; boundary <= rows; boundary += BattleGrid.ControlBlockSize)
            {
                CreateGridBorder(
                    borderRoot,
                    $"ControlBlockRowBorder_{boundary}",
                    new Vector3(0f, borderY, GetCenteredBoundaryCoordinate(boundary, rows)),
                    new Vector3(columns, board.CellHeight * 0.55f, board.ControlBlockBorderWidth),
                    _controlBlockBorderMaterial);
            }

            if (rows % BattleGrid.ControlBlockSize != 0)
            {
                CreateGridBorder(
                    borderRoot,
                    "ControlBlockRowBorder_End",
                    new Vector3(0f, borderY, rows * 0.5f),
                    new Vector3(columns, board.CellHeight * 0.55f, board.ControlBlockBorderWidth),
                    _controlBlockBorderMaterial);
            }
        }

        private static void CreateGridBorder(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = name;
            border.transform.SetParent(parent, false);
            border.transform.localPosition = localPosition;
            border.transform.localScale = localScale;
            var collider = border.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = border.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material CreateGridBorderMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            return new Material(shader) { color = color };
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
                if (!combatant.IsAlive)
                {
                    ReleaseCombatantView(combatant.ActorId);
                    _previousHitPoints.Remove(combatant.ActorId);
                    continue;
                }

                activeActorIds.Add(combatant.ActorId);
                if (!_combatantViews.TryGetValue(combatant.ActorId, out var view))
                {
                    view = _combatantViewPool.Rent(_combatantRoot);
                    view.gameObject.name = "Combatant_" + combatant.ActorId;
                    view.Initialize(combatant.ActorId, visualCatalog);
                    _combatantViews.Add(combatant.ActorId, view);
                }

                var lostHitPoints = _previousHitPoints.TryGetValue(combatant.ActorId, out var previousHitPoints) &&
                                    combatant.HitPoints < previousHitPoints;
                var curseDamage = hasLivingE13 &&
                                  combatant.Team == Team.Ally &&
                                  lostHitPoints &&
                                  !directAttackTargets.Contains(combatant.ActorId);
                view.Render(combatant, GetRuntimeCombatantLocalPosition(combatant), curseDamage);

                if (curseDamage)
                {
                    _feedbackLayer?.Show(
                        "CURSE",
                        GetRuntimeCombatantLocalPosition(combatant),
                        new Color(0.85f, 0.25f, 1f));
                }

                _previousHitPoints[combatant.ActorId] = combatant.HitPoints;
            }

            var inactiveActorIds = _combatantViews.Keys
                .Where(actorId => !activeActorIds.Contains(actorId))
                .ToArray();
            foreach (var actorId in inactiveActorIds)
            {
                ReleaseCombatantView(actorId);
            }

            _performanceMonitor?.ReportWorld(
                _cellViews.Count,
                _combatantViewPool?.ActiveCount ?? _combatantViews.Count,
                _combatantViewPool?.AvailableCount ?? 0);
        }

        private void ShowDeploymentFeedback(string unitId, GridPosition position)
        {
            var option = _snapshot?.UnitOptions.FirstOrDefault(item =>
                string.Equals(item.Id, unitId, System.StringComparison.OrdinalIgnoreCase));
            if (option == null ||
                (option.DeploymentMode != DeploymentMode.SupportEffect &&
                 option.DeploymentMode != DeploymentMode.TerrainBuild))
            {
                return;
            }

            var text = "SUPPORT";
            var color = new Color(0.25f, 0.8f, 1f);
            switch (option.Effect)
            {
                case "Strike":
                    text = "STRIKE";
                    color = new Color(1f, 0.65f, 0.15f);
                    break;
                case "Bomb3x3":
                    text = "BOMB 3x3";
                    color = new Color(1f, 0.3f, 0.15f);
                    break;
                case "Freeze":
                    text = "FREEZE";
                    color = new Color(0.3f, 0.85f, 1f);
                    break;
                case "TerrainBuild":
                    text = "TERRITORY";
                    color = new Color(0.25f, 1f, 0.45f);
                    break;
                case "Silence50":
                case "Silence80":
                    text = "SILENCE";
                    color = new Color(0.5f, 0.55f, 1f);
                    break;
                case "Lure3x3":
                case "Lure5x5":
                    text = "LURE";
                    color = new Color(0.95f, 0.25f, 0.9f);
                    break;
                case "Destroy5x5":
                    text = "CLEAR 5x5";
                    color = new Color(1f, 0.15f, 0.15f);
                    break;
            }

            _feedbackLayer?.Show(
                text,
                GetRuntimeCellLocalPosition(position.Column, position.Row, 0.25f),
                color,
                1.35f);
        }

        private void ReleaseCombatantView(string actorId)
        {
            if (string.IsNullOrEmpty(actorId) || !_combatantViews.TryGetValue(actorId, out var view))
            {
                return;
            }

            _combatantViews.Remove(actorId);
            _combatantViewPool?.Return(view);
        }

        private void ReleaseAllCombatantViews()
        {
            foreach (var view in _combatantViews.Values.ToArray())
            {
                _combatantViewPool?.Return(view);
            }

            _combatantViews.Clear();
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
                        combatants.TryGetValue(combatEvent.TargetActorId, out var target))
                    {
                        var attackColor = source.HasHeavyStrike
                            ? new Color(1f, 0.82f, 0.15f)
                            : source.Team == Team.Ally
                                ? new Color(0.2f, 0.8f, 1f)
                                : new Color(1f, 0.3f, 0.22f);
                        _feedbackLayer?.ShowTracer(
                            GetRuntimeCombatantLocalPosition(source) + Vector3.up * 0.55f,
                            GetRuntimeCombatantLocalPosition(target) + Vector3.up * 0.45f,
                            attackColor);

                        if (source.HasHeavyStrike)
                        {
                            _feedbackLayer?.Show(
                                "HEAVY",
                                GetRuntimeCombatantLocalPosition(target),
                                attackColor,
                                0.65f);
                        }

                        if (string.Equals(
                                source.TemplateId,
                                "E15",
                                System.StringComparison.OrdinalIgnoreCase))
                        {
                            _feedbackLayer?.Show(
                                "EXECUTE",
                                GetRuntimeCombatantLocalPosition(target),
                                new Color(1f, 0.15f, 0.75f),
                                1.4f);
                        }
                    }
                }
                else if (combatEvent.Type == CombatEventType.Pollution)
                {
                    _feedbackLayer?.Show(
                        "POLLUTED",
                        GetRuntimeCellLocalPosition(combatEvent.Column, combatEvent.Row, 0.25f),
                        new Color(0.75f, 0.2f, 0.9f));
                }
                else if (combatEvent.Type == CombatEventType.Heal &&
                         combatants.TryGetValue(combatEvent.TargetActorId, out var healed))
                {
                    _feedbackLayer?.Show(
                        $"+{combatEvent.Amount:0.#} HEAL",
                        GetRuntimeCombatantLocalPosition(healed),
                        new Color(0.3f, 1f, 0.45f));
                }
                else if (combatEvent.Type == CombatEventType.Conversion &&
                         combatants.TryGetValue(combatEvent.TargetActorId, out var converted))
                {
                    _feedbackLayer?.Show(
                        "CONVERT",
                        GetRuntimeCombatantLocalPosition(converted),
                        new Color(0.15f, 1f, 0.85f),
                        1.4f);
                }
                else if (combatEvent.Type == CombatEventType.Pushback &&
                         combatants.TryGetValue(combatEvent.TargetActorId, out var pushed))
                {
                    _feedbackLayer?.Show(
                        $"PUSH {combatEvent.Amount:0}",
                        GetRuntimeCombatantLocalPosition(pushed),
                        new Color(1f, 0.55f, 0.12f));
                }
                else if (combatEvent.Type == CombatEventType.Death &&
                         combatants.TryGetValue(combatEvent.ActorId, out var defeated))
                {
                    _feedbackLayer?.Show(
                        defeated.IsBuilding ? "BUILDING DOWN" : "DOWN",
                        GetRuntimeCombatantLocalPosition(defeated),
                        new Color(1f, 0.35f, 0.25f));
                }
            }

            _processedCombatEventCount = _snapshot.CombatEvents.Count;
            return directAttackTargets;
        }

        private void EnsureSceneInfrastructure(int columns, int rows)
        {
            Camera camera;
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.61f, 0.65f);
            }
            else
            {
                camera = Camera.main;
            }

            var cameraRig = camera.GetComponent<BattleSliceCameraRig>() ??
                            camera.gameObject.AddComponent<BattleSliceCameraRig>();
            cameraRig.Configure(columns, rows, _boardAnchor);

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
            return Mathf.Max(0, column - 1);
        }

        internal static float GetRowWorldZ(int row)
        {
            return Mathf.Max(0, row - 1);
        }

        internal static float GetLaneWorldZ(double lanePosition)
        {
            return (float)System.Math.Max(0d, lanePosition - 1d);
        }

        internal static float GetControlBlockBoundaryWorldCoordinate(int completedCells)
        {
            return Mathf.Max(0, completedCells) - 0.5f;
        }

        internal static float GetCenteredColumnCoordinate(int column, int columns)
        {
            return Mathf.Max(1, column) - 1f - (Mathf.Max(1, columns) - 1f) * 0.5f;
        }

        internal static float GetCenteredRowCoordinate(int row, int rows)
        {
            return Mathf.Max(1, row) - 1f - (Mathf.Max(1, rows) - 1f) * 0.5f;
        }

        internal static float GetCenteredBoundaryCoordinate(int completedCells, int cellCount)
        {
            return Mathf.Clamp(completedCells, 0, Mathf.Max(1, cellCount)) -
                   Mathf.Max(1, cellCount) * 0.5f;
        }

        private Vector3 GetRuntimeCellLocalPosition(int column, int row, float height)
        {
            return new Vector3(
                GetCenteredColumnCoordinate(column, _snapshot.Columns),
                height,
                GetCenteredRowCoordinate(row, _snapshot.Rows));
        }

        private Vector3 GetRuntimeCombatantLocalPosition(BattleSliceCombatantSnapshot combatant)
        {
            var localX = GetCenteredColumnCoordinate(combatant.Column, _snapshot.Columns);
            var localZ = (float)System.Math.Max(1d, combatant.LanePosition) - 1f -
                         (_snapshot.Rows - 1f) * 0.5f;
            if (combatant.IsBuilding)
            {
                localX = (GetCenteredColumnCoordinate(
                              combatant.FootprintStartColumn,
                              _snapshot.Columns) +
                          GetCenteredColumnCoordinate(
                              combatant.FootprintEndColumn,
                              _snapshot.Columns)) * 0.5f;
                localZ = (GetCenteredRowCoordinate(
                              combatant.FootprintStartRow,
                              _snapshot.Rows) +
                          GetCenteredRowCoordinate(
                              combatant.FootprintEndRow,
                              _snapshot.Rows)) * 0.5f;
            }

            return new Vector3(localX, 0.12f, localZ);
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
            if (_runtimeUiPointerBlocked)
            {
                return true;
            }

            if (useFormalLevelConfiguration && _flowSnapshot != null &&
                _flowSnapshot.Screen != FormalFlowScreen.Battle)
            {
                return true;
            }

            if (_snapshot != null && _snapshot.AuthorizationState == AuthorizationState.Choosing)
            {
                return true;
            }

            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (_runtimeHudVisible && new Rect(16f, 16f, 570f, 370f).Contains(guiPosition))
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

            if (!_runtimeHudVisible)
            {
                return;
            }

            if (useFormalLevelConfiguration && _flowSnapshot != null)
            {
                if (_flowSnapshot.Screen == FormalFlowScreen.MainMenu)
                {
                    DrawFormalMainMenu();
                    return;
                }

                if (_flowSnapshot.Screen == FormalFlowScreen.LevelSelect)
                {
                    DrawFormalLevelSelect();
                    return;
                }
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 570f, 370f), GUI.skin.box);
            GUILayout.Label(useFormalLevelConfiguration
                ? $"Company War-RE | {_activeLevelId} 正式关卡"
                : "Company War-RE | L01 可玩验证");
            GUILayout.Label($"资源: {_snapshot.Resources}    时间: {_snapshot.ElapsedSeconds:0.0}s");
            GUILayout.Label(
                $"已选: {_selectedUnitId}    基准 {_snapshot.UnitId} 消耗: {_snapshot.UnitResourceCost}    " +
                $"冷却: {_snapshot.RemainingCooldown:0.0}s    选中: {_selected}");
            GUILayout.Space(6f);
            GUILayout.Label($"左键选择 | D / 空格部署 {_selectedUnitId} | 右键拖动旋转镜头 | Esc 暂停");
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
            GUILayout.Label(
                $"授权: {_snapshot.AuthorizationPoints}    下一需求: " +
                $"{(_snapshot.NextAuthorizationRequirement == int.MaxValue ? "完成" : _snapshot.NextAuthorizationRequirement.ToString())}");
            if (_snapshot.AuthorizationState == AuthorizationState.Available &&
                GUILayout.Button("申请新单位授权", GUILayout.Width(180f)))
            {
                RequestAuthorization();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("部署列表:", GUILayout.Width(70f));
            foreach (var unitId in _snapshot.DeployList)
            {
                if (GUILayout.Toggle(
                        string.Equals(_selectedUnitId, unitId, System.StringComparison.OrdinalIgnoreCase),
                        unitId,
                        GUI.skin.button,
                        GUILayout.Width(54f)))
                {
                    _selectedUnitId = unitId;
                }
            }
            GUILayout.EndHorizontal();
            if (_snapshot.CombatEvents.Count > 0)
            {
                GUILayout.Label(Describe(_snapshot.CombatEvents[_snapshot.CombatEvents.Count - 1]));
            }
            GUILayout.Space(6f);
            GUILayout.Label(_lastAction);
            GUILayout.EndArea();

            if (useFormalLevelConfiguration && _flowSnapshot != null &&
                _flowSnapshot.Screen == FormalFlowScreen.Paused)
            {
                DrawFormalPause();
                return;
            }

            if (_snapshot.AuthorizationState == AuthorizationState.Choosing)
            {
                DrawAuthorizationChoice();
                return;
            }

            if (_snapshot.BattleState != BattleState.Running)
            {
                var width = 420f;
                var height = useFormalLevelConfiguration ? 285f : 170f;
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
                    _snapshot.BattleState == BattleState.Victory
                        ? _activeLevelId + " 胜利"
                        : _activeLevelId + " 失败",
                    resultStyle);
                GUILayout.Label(
                    $"突击分 {_snapshot.AssaultScore}    剩余建筑 {_snapshot.EnemyBuildingCount}    " +
                    $"耗时 {_snapshot.ElapsedSeconds:0.0}s");
                GUILayout.Space(12f);
                if (GUILayout.Button("重新开始 (R)", GUILayout.Height(36f)))
                {
                    RestartFormalLevel();
                }
                if (useFormalLevelConfiguration && _flowSnapshot != null)
                {
                    if (!string.IsNullOrWhiteSpace(_flowSnapshot.NextLevelId) &&
                        GUILayout.Button("下一关 " + _flowSnapshot.NextLevelId, GUILayout.Height(34f)))
                    {
                        StartFormalLevel(_flowSnapshot.NextLevelId);
                    }
                    if (GUILayout.Button("关卡选择", GUILayout.Height(32f)))
                    {
                        OpenFormalLevelSelect();
                    }
                    if (GUILayout.Button("返回主菜单", GUILayout.Height(32f)))
                    {
                        ReturnFormalMainMenu();
                    }
                }
                GUILayout.EndArea();
            }
        }

        private void DrawFormalMainMenu()
        {
            const float width = 460f;
            const float height = 300f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                GUI.skin.window);
            GUILayout.Space(22f);
            var title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label("COMPANY WAR-RE", title);
            GUILayout.Label("正式战役 L00–L20 / 无尽", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            });
            GUILayout.Space(28f);
            if (GUILayout.Button("开始 / 继续 " + _flowSnapshot.ActiveLevelId, GUILayout.Height(42f)))
            {
                StartFormalLevel(_flowSnapshot.ActiveLevelId);
            }
            if (GUILayout.Button("关卡选择", GUILayout.Height(42f)))
            {
                OpenFormalLevelSelect();
            }
            GUILayout.Space(18f);
            GUILayout.Label("胜利后自动解锁下一关；进度暂存于本次运行。", GUI.skin.label);
            GUILayout.EndArea();
        }

        private void DrawFormalLevelSelect()
        {
            const float width = 520f;
            const float height = 720f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                GUI.skin.window);
            GUILayout.Space(16f);
            GUILayout.Label("选择正式关卡", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold
            });
            GUILayout.Space(18f);
            var columns = 4;
            var index = 0;
            foreach (var levelId in _flowSnapshot.LevelOrder)
            {
                if (index % columns == 0)
                {
                    GUILayout.BeginHorizontal();
                }
                var unlocked = _flowSnapshot.IsUnlocked(levelId);
                var completed = _flowSnapshot.IsCompleted(levelId);
                GUI.enabled = unlocked;
                var suffix = completed ? "  已完成" : unlocked ? "  可挑战" : "  未解锁";
                if (GUILayout.Button(levelId + suffix, GUILayout.Height(48f)))
                {
                    StartFormalLevel(levelId);
                }
                index++;
                if (index % columns == 0)
                {
                    GUILayout.EndHorizontal();
                }
            }
            if (index % columns != 0)
            {
                GUILayout.EndHorizontal();
            }
            GUI.enabled = true;
            GUILayout.Space(12f);
            if (GUILayout.Button("返回主菜单", GUILayout.Height(38f)))
            {
                ReturnFormalMainMenu();
            }
            GUILayout.EndArea();
        }

        private void DrawFormalPause()
        {
            const float width = 400f;
            const float height = 285f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                GUI.skin.window);
            GUILayout.Space(18f);
            GUILayout.Label("已暂停", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            });
            if (GUILayout.Button("继续", GUILayout.Height(38f))) ToggleFormalPause();
            if (GUILayout.Button("重新开始", GUILayout.Height(38f))) RestartFormalLevel();
            if (GUILayout.Button("关卡选择", GUILayout.Height(38f))) OpenFormalLevelSelect();
            if (GUILayout.Button("主菜单", GUILayout.Height(38f))) ReturnFormalMainMenu();
            GUILayout.EndArea();
        }

        private void DrawAuthorizationChoice()
        {
            const float width = 480f;
            var height = 190f + _snapshot.AuthorizationCandidates.Count * 44f;
            GUILayout.BeginArea(
                new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height),
                GUI.skin.window);
            GUILayout.Space(14f);
            GUILayout.Label("授权成长", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold
            });
            GUILayout.Label("选择一个新单位加入部署列表");
            foreach (var candidate in _snapshot.AuthorizationCandidates)
            {
                var option = _snapshot.UnitOptions.FirstOrDefault(item => string.Equals(
                    item.Id,
                    candidate,
                    System.StringComparison.OrdinalIgnoreCase));
                var label = option == null
                    ? candidate
                    : $"{candidate}  {option.Name}　费用 {option.ResourceCost}";
                if (GUILayout.Button(label, GUILayout.Height(36f)))
                {
                    AcceptAuthorization(candidate);
                }
            }
            if (GUILayout.Button("暂不申请", GUILayout.Height(32f)))
            {
                CancelAuthorizationChoice();
            }
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
                case CombatEventType.Heal:
                    return $"Heal: {combatEvent.ActorId} -> {combatEvent.TargetActorId} +{combatEvent.Amount:0.#}";
                case CombatEventType.Conversion:
                    return $"Conversion: {combatEvent.ActorId} -> {combatEvent.TargetActorId}";
                case CombatEventType.Pushback:
                    return $"Pushback: {combatEvent.ActorId} -> {combatEvent.TargetActorId} {combatEvent.Amount:0} blocks";
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
            _assetProvider?.Dispose();
            _sceneLoader?.Dispose();
            _combatantViewPool?.Dispose();
            _assetProvider = null;
            _sceneLoader = null;
            _combatantViewPool = null;
            DestroyCellSharedMaterial();
            DestroyGridBorderMaterials();

            if (UnityEngine.Application.isPlaying && _architecture != null)
            {
                _architecture.Deinit();
            }
        }
    }
}
