using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CompanyWarRE.Presentation
{
    /// <summary>Owns the camera and suppresses scene UI only for the battle introduction.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class BattleIntroDirector : MonoBehaviour
    {
        private readonly Dictionary<Canvas, bool> _canvases = new Dictionary<Canvas, bool>();
        private readonly Dictionary<GraphicRaycaster, bool> _raycasters = new Dictionary<GraphicRaycaster, bool>();
        private readonly Dictionary<EventSystem, bool> _eventSystems = new Dictionary<EventSystem, bool>();
        private Camera _camera;
        private BattleSliceCameraRig _rig;
        private CinemachineBrain _brain;
        private CinemachineVirtualCamera _shot;
        private bool _ownsBrain, _brainEnabled, _rigEnabled;
        private CinemachineBrain.UpdateMethod _updateMethod;
        private CinemachineBrain.BrainUpdateMethod _blendUpdateMethod;
        private CinemachineBlendDefinition _blend;
        private LensSettings _homeLens;
        private Vector3 _homePosition, _center;
        private BattleIntroPathSettings _path;
        private bool _ownsPath;
        public bool PreviewPaused { get; set; }
        public Vector3 HomePosition => _homePosition;
        public Quaternion HomeRotation => _homeRotation;
        public Vector3 BattlefieldCenter => _center;
        public float HomeSize => _homeLens.OrthographicSize;
        public float Progress => Mathf.Clamp01(_elapsed / Mathf.Max(1f, _duration));
        private Quaternion _homeRotation;
        private float _duration, _elapsed, _skipElapsed, _skipSize;
        private Vector3 _skipPosition;
        private Quaternion _skipRotation;
        private bool _skipping;
        private Matrix4x4 _homeProjection, _skipProjection;
        private int _lastShot = -1;
        public BattleIntroSequence Sequence { get; private set; }
        public int CurrentShot => Sequence != null && _path != null && _path.useSequence
            ? BattleIntroSequence.ShotAt(_path, Progress, out _) : 0;
        public bool IsPlaying { get; private set; }

        public void Play(Camera camera, Vector3 battlefieldCenter, float duration, BattleIntroSequence sequence = null)
        {
            Cancel();
            if (camera == null || !isActiveAndEnabled) return;
            _camera = camera;
            _homePosition = camera.transform.position;
            _homeRotation = camera.transform.rotation;
            _homeLens = LensSettings.FromCamera(camera);
            _homeProjection = camera.projectionMatrix;
            Sequence = sequence;
            _lastShot = -1;
            _center = battlefieldCenter;
            _path = Resources.Load<BattleIntroPathSettings>(BattleIntroPathSettings.ResourcePath);
            _ownsPath = _path == null;
            if (_ownsPath) _path = ScriptableObject.CreateInstance<BattleIntroPathSettings>();
            PreviewPaused = false;
            _duration = Mathf.Max(1f, duration);
            _elapsed = _skipElapsed = 0f;
            _skipping = false;
            _rig = camera.GetComponent<BattleSliceCameraRig>();
            _rigEnabled = _rig != null && _rig.enabled;
            if (_rig != null) _rig.enabled = false;
            _brain = camera.GetComponent<CinemachineBrain>();
            _ownsBrain = _brain == null;
            if (_ownsBrain) _brain = camera.gameObject.AddComponent<CinemachineBrain>();
            _brainEnabled = _brain.enabled;
            _updateMethod = _brain.m_UpdateMethod;
            _blendUpdateMethod = _brain.m_BlendUpdateMethod;
            _blend = _brain.m_DefaultBlend;
            _brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
            _brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            _brain.enabled = true;

            var shotObject = new GameObject("BattleIntro_Cinemachine");
            shotObject.transform.SetParent(transform, false);
            _shot = shotObject.AddComponent<CinemachineVirtualCamera>();
            _shot.Priority = int.MaxValue;
            _shot.m_Lens = _homeLens;
            IsPlaying = true;
            HideUi();
            ApplyShot(0f);
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            if (PreviewPaused) { HideUi(); ApplyShot(Progress); }
            else Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (!IsPlaying) return;
            if (_camera == null || _brain == null || _shot == null) { Cancel(); return; }
            HideUi();
            if (_skipping)
            {
                _skipElapsed += Mathf.Max(0f, deltaTime);
                var t = Mathf.SmoothStep(0f, 1f, _skipElapsed / 0.45f);
                _shot.transform.SetPositionAndRotation(Vector3.Lerp(_skipPosition, _homePosition, t),
                    Quaternion.Slerp(_skipRotation, _homeRotation, t));
                var lens = _homeLens;
                lens.OrthographicSize = Mathf.Lerp(_skipSize, _homeLens.OrthographicSize, t);
                _shot.m_Lens = lens;
                _brain.ManualUpdate();
                var projection = new Matrix4x4();
                for (var i = 0; i < 16; i++) projection[i] = Mathf.Lerp(_skipProjection[i], _homeProjection[i], t);
                _camera.projectionMatrix = projection;
                if (_skipElapsed >= 0.45f) Cancel();
            }
            else
            {
                _elapsed += Mathf.Max(0f, deltaTime);
                ApplyShot(Mathf.Clamp01(_elapsed / _duration));
                if (_elapsed >= _duration) Cancel();
            }
        }

        private void ApplyShot(float progress)
        {
            Vector3 position;
            Quaternion rotation;
            float size;
            var perspective = false;
            var shot = 0;
            if (Sequence != null && _path.useSequence)
                Sequence.Evaluate(_path, _homePosition, _homeRotation, _center, progress,
                    out position, out rotation, out size, out perspective, out shot);
            else _path.Evaluate(_homePosition, _homeRotation, _center, progress, out position, out rotation, out size);
            if (shot != _lastShot) { _shot.PreviousStateIsValid = false; _lastShot = shot; }
            _shot.transform.SetPositionAndRotation(position, rotation);
            var lens = _homeLens;
            lens.OrthographicSize *= size;
            if (Sequence != null && _path.useSequence)
            {
                lens.ModeOverride = perspective ? LensSettings.OverrideModes.Perspective :
                    _homeLens.Orthographic ? LensSettings.OverrideModes.Orthographic : LensSettings.OverrideModes.Perspective;
                lens.FieldOfView = perspective ? _path.flightFieldOfView : _homeLens.FieldOfView;
                lens.NearClipPlane = Mathf.Min(0.1f, _homeLens.NearClipPlane);
                lens.FarClipPlane = Mathf.Max(_homeLens.FarClipPlane, Mathf.Max(Sequence.Board.VisualWidth, Sequence.Board.VisualLength) * 10f);
            }
            _shot.m_Lens = lens;
            _brain.ManualUpdate();
            _camera.ResetProjectionMatrix();
        }

        public void Skip()
        {
            PreviewPaused = false;
            if (!IsPlaying || _skipping || _shot == null) return;
            _skipping = true;
            _skipPosition = _shot.transform.position;
            _skipRotation = _shot.transform.rotation;
            _skipSize = _shot.m_Lens.OrthographicSize;
            _skipProjection = _camera.projectionMatrix;
        }

        public void SeekPreview(float progress)
        {
            if (!IsPlaying) return;
            PreviewPaused = true;
            _skipping = false;
            _elapsed = Mathf.Clamp01(progress) * _duration;
            ApplyShot(Progress);
        }

        private void HideUi()
        {
            var events = EventSystem.current;
            if (events != null)
            {
                if (!_eventSystems.ContainsKey(events)) _eventSystems.Add(events, events.sendNavigationEvents);
                events.sendNavigationEvents = false;
            }
            // Include inactive and newly created canvases, without disabling their controllers.
            foreach (var canvas in FindObjectsOfType<Canvas>(true))
            {
                if (canvas.gameObject.scene != gameObject.scene) continue;
                if (!_canvases.ContainsKey(canvas)) _canvases.Add(canvas, canvas.enabled);
                canvas.enabled = false;
            }
            foreach (var raycaster in FindObjectsOfType<GraphicRaycaster>(true))
            {
                if (raycaster.gameObject.scene != gameObject.scene) continue;
                if (!_raycasters.ContainsKey(raycaster)) _raycasters.Add(raycaster, raycaster.enabled);
                raycaster.enabled = false;
            }
        }

        public void Cancel()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            PreviewPaused = false;
            if (_ownsPath && _path != null) Release(_path);
            _path = null;
            if (_shot != null)
            {
                _shot.gameObject.SetActive(false);
                Release(_shot.gameObject);
            }
            if (_brain != null)
            {
                _brain.enabled = false;
                _brain.m_UpdateMethod = _updateMethod;
                _brain.m_BlendUpdateMethod = _blendUpdateMethod;
                _brain.m_DefaultBlend = _blend;
                // Retain the disabled runtime brain for a level started again this frame.
                if (!_ownsBrain) _brain.enabled = _brainEnabled;
            }
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_homePosition, _homeRotation);
                _camera.orthographicSize = _homeLens.OrthographicSize;
                _camera.fieldOfView = _homeLens.FieldOfView;
                _camera.nearClipPlane = _homeLens.NearClipPlane;
                _camera.farClipPlane = _homeLens.FarClipPlane;
                _camera.orthographic = _homeLens.Orthographic;
                _camera.ResetProjectionMatrix();
            }
            if (_rig != null) _rig.enabled = _rigEnabled;
            foreach (var entry in _canvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _raycasters) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in _eventSystems) if (entry.Key != null) entry.Key.sendNavigationEvents = entry.Value;
            _canvases.Clear();
            _raycasters.Clear();
            _eventSystems.Clear();
            _shot = null;
            _brain = null;
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
        private static void Release(Object value)
        {
            if (UnityEngine.Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
