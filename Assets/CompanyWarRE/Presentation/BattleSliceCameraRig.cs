using UnityEngine;
using UnityEngine.EventSystems;

namespace CompanyWarRE.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BattleSliceCameraRig : MonoBehaviour
    {
        private const float MinimumZoom = 5f;

        [SerializeField] private float rotationSpeed = 3f;
        [SerializeField] private float panSpeed = 10f;
        [SerializeField] private float fastMoveMultiplier = 2.5f;
        [SerializeField] private float zoomRatioPerStep = 0.1f;
        [SerializeField] private float zoomFocusLerp = 0.22f;
        [SerializeField] private float minPitch = 25f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float smoothTime = 0.12f;

        private Camera _camera;
        private Vector3 _homeFocus;
        private Vector3 _currentFocus;
        private Vector3 _targetFocus;
        private Vector3 _focusVelocity;
        private Vector2 _minimumFocus;
        private Vector2 _maximumFocus;
        private float _homeSize;
        private float _targetSize;
        private float _sizeVelocity;
        private float _currentYaw;
        private float _targetYaw;
        private float _yawVelocity;
        private float _currentPitch = 58f;
        private float _targetPitch = 58f;
        private float _pitchVelocity;
        private float _orbitDistance;
        private bool _isRotating;
        private BattleSliceController _battleSliceController;
        private Transform _boardSpace;
        private float _boardScale = 1f;

        public void Configure(int columns, int rows)
        {
            Configure(columns, rows, null);
        }

        public void Configure(int columns, int rows, Transform boardSpace)
        {
            _camera = GetComponent<Camera>();
            _battleSliceController = FindObjectOfType<BattleSliceController>();
            _boardSpace = boardSpace;
            _boardScale = boardSpace != null
                ? Mathf.Max(0.0001f, Mathf.Abs(boardSpace.lossyScale.x))
                : 1f;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 250f;

            var safeColumns = Mathf.Max(1, columns);
            var safeRows = Mathf.Max(1, rows);
            var halfX = (safeColumns - 1f) * 0.5f;
            var halfZ = (safeRows - 1f) * 0.5f;
            _minimumFocus = new Vector2(-halfX, -halfZ);
            _maximumFocus = new Vector2(halfX, halfZ);
            _homeFocus = Vector3.zero;
            _homeSize = CalculateOrthographicSize(
                safeColumns * _boardScale,
                safeRows * _boardScale,
                Mathf.Max(0.5f, _camera.aspect));
            _orbitDistance = Mathf.Max(12f, _homeSize * 1.75f);
            Refit();
        }

        public void Refit()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            _targetFocus = _homeFocus;
            _currentFocus = _homeFocus;
            _targetYaw = 0f;
            _currentYaw = 0f;
            _targetPitch = 58f;
            _currentPitch = 58f;
            _targetSize = Mathf.Max(MinimumZoom, _homeSize);
            _camera.orthographicSize = _targetSize;
            _focusVelocity = Vector3.zero;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _sizeVelocity = 0f;
            ApplyPose();
        }

        public static float CalculateOrthographicSize(float width, float length, float aspect)
        {
            var safeAspect = Mathf.Max(0.5f, aspect);
            var verticalRequirement = Mathf.Max(1f, length) * 0.62f;
            var horizontalRequirement = Mathf.Max(1f, width) / safeAspect * 0.62f;
            return Mathf.Max(verticalRequirement, horizontalRequirement) + 1.5f;
        }

        public static float CalculateZoomSize(
            float currentSize,
            float wheelDelta,
            float minimumSize,
            float maximumSize,
            float ratioPerStep)
        {
            var safeMinimum = Mathf.Max(0.01f, minimumSize);
            var safeMaximum = Mathf.Max(safeMinimum, maximumSize);
            var ratio = Mathf.Clamp(ratioPerStep, 0.01f, 0.9f);
            return Mathf.Clamp(currentSize * (1f - wheelDelta * ratio), safeMinimum, safeMaximum);
        }

        public static Vector3 ClampFocusToBounds(Vector3 focus, Vector2 minimum, Vector2 maximum)
        {
            var minimumX = Mathf.Min(minimum.x, maximum.x);
            var maximumX = Mathf.Max(minimum.x, maximum.x);
            var minimumZ = Mathf.Min(minimum.y, maximum.y);
            var maximumZ = Mathf.Max(minimum.y, maximum.y);
            return new Vector3(
                Mathf.Clamp(focus.x, minimumX, maximumX),
                0f,
                Mathf.Clamp(focus.z, minimumZ, maximumZ));
        }

        public static float ClampOrbitPitch(float pitch, float minimumPitch, float maximumPitch)
        {
            var minimum = Mathf.Min(minimumPitch, maximumPitch);
            var maximum = Mathf.Max(minimumPitch, maximumPitch);
            return Mathf.Clamp(pitch, minimum, maximum);
        }

        private void Update()
        {
            if (_camera == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Home))
            {
                Refit();
                return;
            }

            var pointerOverUi =
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) ||
                (_battleSliceController != null &&
                 _battleSliceController.IsPointerOverRuntimeHud(Input.mousePosition));
            UpdateRotation(pointerOverUi);
            UpdateZoom(pointerOverUi);
            UpdatePan(pointerOverUi);
            _targetFocus = ClampFocusToBounds(_targetFocus, _minimumFocus, _maximumFocus);
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            var duration = Mathf.Max(0.0001f, smoothTime);
            var deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _currentFocus = Vector3.SmoothDamp(
                _currentFocus,
                _targetFocus,
                ref _focusVelocity,
                duration,
                Mathf.Infinity,
                deltaTime);
            _currentYaw = Mathf.SmoothDampAngle(
                _currentYaw,
                _targetYaw,
                ref _yawVelocity,
                duration,
                Mathf.Infinity,
                deltaTime);
            _currentPitch = Mathf.SmoothDampAngle(
                _currentPitch,
                _targetPitch,
                ref _pitchVelocity,
                duration,
                Mathf.Infinity,
                deltaTime);
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                _targetSize,
                ref _sizeVelocity,
                duration,
                Mathf.Infinity,
                deltaTime);
            ApplyPose();
        }

        private void UpdateRotation(bool pointerOverUi)
        {
            if (!pointerOverUi && Input.GetMouseButtonDown(1))
            {
                _isRotating = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                _isRotating = false;
            }

            if (!_isRotating || pointerOverUi)
            {
                return;
            }

            _targetYaw += Input.GetAxis("Mouse X") * rotationSpeed;
            _targetPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            _targetPitch = ClampOrbitPitch(_targetPitch, minPitch, maxPitch);
        }

        private void UpdateZoom(bool pointerOverUi)
        {
            if (pointerOverUi)
            {
                return;
            }

            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) <= Mathf.Epsilon)
            {
                return;
            }

            _targetSize = CalculateZoomSize(
                _targetSize,
                wheel,
                MinimumZoom,
                Mathf.Max(MinimumZoom, _homeSize * 1.8f),
                zoomRatioPerStep);

            if (wheel > 0f && TryGetBattlePlaneHit(_camera.ScreenPointToRay(Input.mousePosition), out var hit))
            {
                var amount = Mathf.Clamp01(Mathf.Abs(wheel) * zoomFocusLerp);
                _targetFocus = Vector3.Lerp(_targetFocus, hit, amount);
            }
        }

        private void UpdatePan(bool pointerOverUi)
        {
            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            var middleMousePan = !pointerOverUi && Input.GetMouseButton(2);
            if (middleMousePan)
            {
                horizontal -= Input.GetAxis("Mouse X") * 1.8f;
                vertical -= Input.GetAxis("Mouse Y") * 1.8f;
            }

            if (Mathf.Abs(horizontal) <= Mathf.Epsilon && Mathf.Abs(vertical) <= Mathf.Epsilon)
            {
                return;
            }

            var movement = Quaternion.Euler(0f, _currentYaw, 0f) *
                           new Vector3(horizontal, 0f, vertical);
            if (movement.sqrMagnitude > 1f)
            {
                movement.Normalize();
            }

            var speed = Mathf.Max(panSpeed, _camera.orthographicSize * 0.65f);
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed *= fastMoveMultiplier;
            }

            _targetFocus += movement * (speed / _boardScale * Time.unscaledDeltaTime);
            _targetFocus.y = 0f;
        }

        private bool TryGetBattlePlaneHit(Ray ray, out Vector3 hitPoint)
        {
            var planeNormal = _boardSpace != null ? _boardSpace.up : Vector3.up;
            var planePoint = _boardSpace != null ? _boardSpace.position : Vector3.zero;
            var plane = new Plane(planeNormal, planePoint);
            if (plane.Raycast(ray, out var distance))
            {
                var worldPoint = ray.GetPoint(distance);
                hitPoint = _boardSpace != null
                    ? _boardSpace.InverseTransformPoint(worldPoint)
                    : worldPoint;
                hitPoint.y = 0f;
                return true;
            }

            hitPoint = default;
            return false;
        }

        private void ApplyPose()
        {
            var localRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            var worldRotation = _boardSpace != null
                ? _boardSpace.rotation * localRotation
                : localRotation;
            var worldFocus = _boardSpace != null
                ? _boardSpace.TransformPoint(_currentFocus)
                : _currentFocus;
            transform.rotation = worldRotation;
            transform.position = worldFocus - worldRotation * Vector3.forward * _orbitDistance;
        }
    }
}
