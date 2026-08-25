using UnityEngine;

namespace CompanyWarRE.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class BattleSliceCameraRig : MonoBehaviour
    {
        private const float MinimumZoom = 5f;
        private Camera _camera;
        private Vector3 _homeFocus;
        private Vector3 _focus;
        private float _homeSize;
        private float _pitch = 58f;

        public void Configure(int columns, int rows)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 250f;

            var width = BattleSliceController.GetColumnWorldX(columns) + 1f;
            var length = BattleSliceController.GetRowWorldZ(rows) + 1f;
            _homeFocus = new Vector3((width - 1f) * 0.5f, 0f, (length - 1f) * 0.5f);
            var aspect = Mathf.Max(0.5f, _camera.aspect);
            _homeSize = CalculateOrthographicSize(width, length, aspect);
            Refit();
        }

        public void Refit()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            _focus = _homeFocus;
            _camera.orthographicSize = Mathf.Max(MinimumZoom, _homeSize);
            ApplyPose();
        }

        public static float CalculateOrthographicSize(float width, float length, float aspect)
        {
            var safeAspect = Mathf.Max(0.5f, aspect);
            var verticalRequirement = Mathf.Max(1f, length) * 0.62f;
            var horizontalRequirement = Mathf.Max(1f, width) / safeAspect * 0.62f;
            return Mathf.Max(verticalRequirement, horizontalRequirement) + 1.5f;
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

            var zoom = Input.mouseScrollDelta.y;
            if (Mathf.Abs(zoom) > 0.001f)
            {
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize * (1f - zoom * 0.1f),
                    MinimumZoom,
                    Mathf.Max(MinimumZoom, _homeSize * 1.8f));
            }

            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            if (Input.GetMouseButton(2))
            {
                horizontal -= Input.GetAxis("Mouse X") * 1.8f;
                vertical -= Input.GetAxis("Mouse Y") * 1.8f;
            }

            if (Mathf.Abs(horizontal) > 0.001f || Mathf.Abs(vertical) > 0.001f)
            {
                var speed = Mathf.Max(5f, _camera.orthographicSize * 0.65f);
                _focus += new Vector3(horizontal, 0f, vertical) * speed * Time.unscaledDeltaTime;
                ApplyPose();
            }
        }

        private void ApplyPose()
        {
            var distance = Mathf.Max(12f, _camera.orthographicSize * 1.75f);
            var pitchRadians = _pitch * Mathf.Deg2Rad;
            var offset = new Vector3(
                0f,
                Mathf.Sin(pitchRadians) * distance,
                -Mathf.Cos(pitchRadians) * distance);
            transform.position = _focus + offset;
            transform.LookAt(_focus);
        }
    }
}
