using UnityEngine;

namespace CompanyWarRE.Presentation
{
    [CreateAssetMenu(menuName = "Company War RE/战前运镜配置", fileName = "BattleIntroPath")]
    public sealed class BattleIntroPathSettings : ScriptableObject
    {
        public const string ResourcePath = "CompanyWarRE/Presentation/BattleIntroPath";
        [Header("分镜：云海进入 / 柱间穿行 / 敌后俯瞰 / 旋转归位")]
        public bool useSequence = false;
        public Vector4 shotDurations = new Vector4(3f, 3f, 2f, 3.5f);
        [Range(40f, 85f)] public float flightFieldOfView = 65f;
        [Tooltip("X/Z 相对于地图宽/长；Y 相对于柱宽，原点为战场中心柱顶。")]
        public Vector3 cloudEntryOffset = new Vector3(-0.9f, -6f, -1.35f);
        [Range(0.3f, 2f)] public float tunnelDepth = 0.8f;
        [Tooltip("0 自动选择中央柱间通道，其他值从左到右选择间隙。")]
        [Min(0)] public int tunnelGap;
        [Tooltip("相对战场尺寸的敌后机位偏移，Y 为俯瞰高度。")]
        public Vector3 enemyOverviewOffset = new Vector3(0f, 0.65f, 0.65f);
        [Range(0.4f, 1.5f)] public float enemyOverviewSize = 0.72f;
        [Header("旧版单镜头（关闭 useSequence 时使用）")]
        [Range(-120f, 120f)] public float openingYaw = -32f;
        [Range(0.2f, 1.5f)] public float openingHeight = 0.65f;
        [Range(0.5f, 2f)] public float openingDistance = 1.15f;
        [Header("路径偏移（相对战斗镜头距离，随地图缩放）")]
        public Vector3 startOffset;
        public Vector3 firstControlOffset = new Vector3(0.18f, 0f, 0f);
        public Vector3 secondControlOffset = new Vector3(-0.16f, 0.08f, 0f);
        public Vector3 lookAtOffset;
        [Header("取景和节奏")]
        [Range(0.3f, 2f)] public float openingSize = 0.78f;
        [Range(0f, 0.95f)] public float returnRotationStart = 0.7f;
        public AnimationCurve motion = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public void GetControlPoints(Vector3 home, Quaternion rotation, Vector3 center,
            out Vector3 start, out Vector3 first, out Vector3 second, out Vector3 lookAt)
        {
            var distance = Mathf.Max(10f, Vector3.Distance(home, center));
            var offset = home - center;
            var opening = Quaternion.AngleAxis(openingYaw, Vector3.up) * offset;
            opening.y = Mathf.Max(distance * 0.4f, offset.y * openingHeight);
            start = center + opening * openingDistance + rotation * startOffset * distance;
            first = start + rotation * firstControlOffset * distance;
            second = home + rotation * secondControlOffset * distance;
            lookAt = center + rotation * lookAtOffset * distance;
        }

        public void Evaluate(Vector3 home, Quaternion rotation, Vector3 center, float progress,
            out Vector3 position, out Quaternion orientation, out float sizeMultiplier)
        {
            GetControlPoints(home, rotation, center, out var start, out var first, out var second, out var target);
            var p = Mathf.Clamp01(progress);
            // Pin the endpoints even if the artist moves the curve's first or last key.
            var t = p <= 0f ? 0f : p >= 1f ? 1f : Mathf.Clamp01(motion != null ? motion.Evaluate(p) : p);
            var u = 1f - t;
            position = u * u * u * start + 3f * u * u * t * first + 3f * u * t * t * second + t * t * t * home;
            var direction = target - position;
            var look = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : rotation;
            orientation = Quaternion.Slerp(look, rotation,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(returnRotationStart, 1f, t)));
            sizeMultiplier = Mathf.Lerp(openingSize, 1f, t);
        }
    }
}
