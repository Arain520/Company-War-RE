using System.Collections.Generic;
using UnityEngine;
using CompanyWarRE.Domain;

namespace CompanyWarRE.Presentation
{
    /// <summary>Generated-map geometry used by both playback and the editor's path display.</summary>
    public sealed class BattleIntroSequence
    {
        public readonly BattleBoardCoordinateMapper Board;
        public readonly Matrix4x4 BoardToWorld;
        public readonly Quaternion BoardRotation;
        public readonly Vector3 EnemyFocus;
        private readonly List<Bounds> _obstacles = new List<Bounds>();
        private List<Vector3> _cloudPath;
        private Vector3 _cachedEntry;
        private int _cachedGap = -1;
        public string RouteMessage { get; private set; }

        public BattleIntroSequence(BattleBoardCoordinateMapper board, Matrix4x4 boardToWorld,
            Quaternion boardRotation, Vector3 enemyFocus, Transform environment = null)
        {
            Board = board;
            BoardToWorld = boardToWorld;
            BoardRotation = boardRotation;
            EnemyFocus = enemyFocus;
            if (environment == null) return;
            var inverse = boardToWorld.inverse;
            foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>())
            {
                var cloud = false;
                for (var node = renderer.transform; node != null && node != environment; node = node.parent)
                    if (node.name.Contains("Cloud")) { cloud = true; break; }
                if (cloud || !renderer.enabled) continue;
                var world = renderer.bounds;
                var local = new Bounds(inverse.MultiplyPoint3x4(world.min), Vector3.zero);
                for (var x = 0; x < 2; x++) for (var y = 0; y < 2; y++) for (var z = 0; z < 2; z++)
                    local.Encapsulate(inverse.MultiplyPoint3x4(new Vector3(x == 0 ? world.min.x : world.max.x,
                        y == 0 ? world.min.y : world.max.y, z == 0 ? world.min.z : world.max.z)));
                local.Expand(1.2f);
                _obstacles.Add(local);
            }
        }

        public static int ShotAt(BattleIntroPathSettings settings, float progress, out float localTime)
        {
            var weights = settings.shotDurations;
            var sum = 0f;
            for (var i = 0; i < 4; i++) sum += Mathf.Max(0.1f, weights[i]);
            var time = Mathf.Clamp01(progress) * sum;
            for (var i = 0; i < 4; i++)
            {
                var duration = Mathf.Max(0.1f, weights[i]);
                if (time < duration || i == 3) { localTime = Mathf.Clamp01(time / duration); return i; }
                time -= duration;
            }
            localTime = 1f;
            return 3;
        }

        public void GetTunnel(BattleIntroPathSettings settings, out Vector3 start, out Vector3 end)
        {
            var height = Mathf.Max(Board.PillarBaseY + 1f,
                Board.MinimumPillarTopY - Board.PillarWidth * Mathf.Clamp(settings.tunnelDepth, 0.3f, 2f));
            if (Board.ControlBlockColumns >= 2 && Board.PillarGap > 0.2f)
            {
                var gap = Mathf.Clamp(settings.tunnelGap == 0 ? Board.ControlBlockColumns / 2 : settings.tunnelGap,
                    1, Board.ControlBlockColumns - 1);
                var x = Board.GetControlBlockCenterLocalPosition(new GridPosition(gap, 1)).x + Board.ControlBlockPitch * 0.5f;
                start = new Vector3(x, height, -Board.VisualLength * 0.5f + Board.PillarWidth * 0.5f);
                end = new Vector3(x, height, Board.VisualLength * 0.5f - Board.PillarWidth * 0.5f);
            }
            else
            {
                // A one-column/zero-gap board cannot contain an internal longitudinal passage.
                start = new Vector3(-Board.VisualWidth * 0.5f - Board.PillarWidth, height, -Board.VisualLength * 0.5f);
                end = new Vector3(start.x, height, Board.VisualLength * 0.5f);
            }
        }

        public void Evaluate(BattleIntroPathSettings settings, Vector3 home, Quaternion homeRotation,
            Vector3 center, float progress, out Vector3 position, out Quaternion rotation,
            out float size, out bool perspective, out int shot)
        {
            shot = ShotAt(settings, progress, out var t);
            perspective = shot < 2;
            size = 1f;
            Vector3 local, target;
            var span = Mathf.Max(Board.VisualWidth, Board.VisualLength);
            if (shot == 0)
            {
                EnsureCloudPath(settings);
                local = SamplePath(_cloudPath, t);
                target = SamplePath(_cloudPath, Mathf.Min(1f, t + 0.06f));
                if ((target - local).sqrMagnitude < 0.01f) target = local + Vector3.forward * Board.PillarWidth;
                // Gradually reveal the battlefield as the flight reaches its outer edge.
                target = Vector3.Lerp(target, new Vector3(0f, Board.AveragePillarTopY, 0f), Mathf.SmoothStep(0f, 1f, t));
            }
            else if (shot == 1)
            {
                GetTunnel(settings, out var start, out var end);
                local = Vector3.Lerp(start, end, t);
                target = local + (end - start).normalized * Board.PillarWidth * 3f + Vector3.up * Board.PillarWidth * 0.25f;
            }
            else
            {
                var localHome = BoardToWorld.inverse.MultiplyPoint3x4(home);
                var localCenter = BoardToWorld.inverse.MultiplyPoint3x4(center);
                var offset = settings.enemyOverviewOffset * span;
                offset.y = Mathf.Max(Board.PillarWidth * 2f, offset.y);
                offset.z = Mathf.Max(Board.PillarWidth * 2f, offset.z);
                var overview = EnemyFocus + offset;
                overview.z = Mathf.Max(overview.z, Board.VisualLength * 0.5f + Board.PillarWidth);
                overview.y = Mathf.Max(overview.y, Board.MaximumPillarTopY + offset.y);
                overview = LiftOutsideBuildings(overview);
                if (shot == 2)
                {
                    local = overview;
                    target = EnemyFocus;
                    size = settings.enemyOverviewSize;
                }
                else
                {
                    var blend = Mathf.SmoothStep(0f, 1f, t);
                    target = Vector3.Lerp(EnemyFocus, localCenter, blend);
                    var a = overview - EnemyFocus;
                    var b = localHome - localCenter;
                    var angleA = Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg;
                    var angleB = Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg;
                    var angle = Mathf.LerpAngle(angleA, angleB, blend) * Mathf.Deg2Rad;
                    var radius = Mathf.Lerp(new Vector2(a.x, a.z).magnitude, new Vector2(b.x, b.z).magnitude, blend);
                    local = target + new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(a.y, b.y, blend), Mathf.Cos(angle) * radius);
                    // Keep the orbit above architecture instead of interpolating through it.
                    local.y += Mathf.Sin(blend * Mathf.PI) * OrbitClearance(overview, localHome, span);
                    size = Mathf.Lerp(settings.enemyOverviewSize, 1f, blend);
                }
            }
            position = BoardToWorld.MultiplyPoint3x4(local);
            var direction = BoardToWorld.MultiplyPoint3x4(target) - position;
            rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, BoardRotation * Vector3.up) : homeRotation;
            if (shot == 3)
            {
                rotation = Quaternion.Slerp(rotation, homeRotation, Mathf.SmoothStep(0f, 1f, t));
                if (progress >= 1f) { position = home; rotation = homeRotation; size = 1f; }
            }
        }

        private Vector3 LiftOutsideBuildings(Vector3 point)
        {
            foreach (var obstacle in _obstacles)
                if (obstacle.Contains(point)) point.y = obstacle.max.y + Board.PillarWidth;
            return point;
        }

        private float OrbitClearance(Vector3 start, Vector3 end, float span)
        {
            var height = Mathf.Max(start.y, end.y);
            foreach (var obstacle in _obstacles)
                if (Mathf.Abs(obstacle.center.x) < span && Mathf.Abs(obstacle.center.z) < span)
                    height = Mathf.Max(height, obstacle.max.y + Board.PillarWidth);
            return Mathf.Max(Board.PillarWidth, height - Mathf.Min(start.y, end.y));
        }

        private void EnsureCloudPath(BattleIntroPathSettings settings)
        {
            if (_cloudPath != null && _cachedEntry == settings.cloudEntryOffset && _cachedGap == settings.tunnelGap) return;
            _cachedEntry = settings.cloudEntryOffset;
            _cachedGap = settings.tunnelGap;
            var offset = settings.cloudEntryOffset;
            var start = new Vector3(offset.x * Board.VisualWidth,
                Board.MinimumPillarTopY + offset.y * Board.PillarWidth, offset.z * Board.VisualLength);
            GetTunnel(settings, out var tunnel, out _);
            var end = new Vector3(tunnel.x, Board.MinimumPillarTopY + Board.PillarWidth * 0.4f,
                -Board.VisualLength * 0.5f - Board.PillarWidth * 1.5f);
            _cloudPath = PlanFlight(start, end, _obstacles);
            RouteMessage = _cloudPath.Count > 2 ? "云海路线已绕开建筑" : "云海直达通道";
        }

        /// <summary>Visibility graph around inflated building bounds, independent of disabled scene colliders.</summary>
        public static List<Vector3> PlanFlight(Vector3 start, Vector3 end, IReadOnlyList<Bounds> obstacles)
        {
            var relevant = new List<Bounds>();
            foreach (var box in obstacles)
                if (box.max.y >= Mathf.Min(start.y, end.y) && box.min.y <= Mathf.Max(start.y, end.y)) relevant.Add(box);
            foreach (var box in relevant)
            {
                if (InsideXZ(start, box)) start.x = box.min.x - 0.5f;
                if (InsideXZ(end, box)) end.z = box.min.z - 0.5f;
            }
            var nodes = new List<Vector3> { start, end };
            foreach (var box in relevant)
                for (var x = 0; x < 2; x++) for (var z = 0; z < 2; z++)
                    nodes.Add(new Vector3(x == 0 ? box.min.x - 0.3f : box.max.x + 0.3f, start.y,
                        z == 0 ? box.min.z - 0.3f : box.max.z + 0.3f));
            var distance = new float[nodes.Count];
            var previous = new int[nodes.Count];
            var visited = new bool[nodes.Count];
            for (var i = 0; i < nodes.Count; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
            distance[0] = 0f;
            for (var step = 0; step < nodes.Count; step++)
            {
                var current = -1;
                for (var i = 0; i < nodes.Count; i++)
                    if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(distance[current]) || current == 1) break;
                visited[current] = true;
                for (var next = 0; next < nodes.Count; next++)
                {
                    if (visited[next] || !ClearXZ(nodes[current], nodes[next], relevant)) continue;
                    var cost = distance[current] + Vector2.Distance(new Vector2(nodes[current].x, nodes[current].z), new Vector2(nodes[next].x, nodes[next].z));
                    if (cost >= distance[next]) continue;
                    distance[next] = cost; previous[next] = current;
                }
            }
            var path = new List<Vector3>();
            if (previous[1] < 0)
            {
                // A fully enclosed endpoint is uncommon; use an overhead establishing shot safely.
                var height = Mathf.Max(start.y, end.y);
                foreach (var box in relevant) height = Mathf.Max(height, box.max.y + 2f);
                return new List<Vector3> { new Vector3(start.x, height, start.z), new Vector3(end.x, height, end.z) };
            }
            for (var node = 1; node >= 0; node = previous[node]) path.Add(nodes[node]);
            path.Reverse();
            var length = 0f;
            for (var i = 1; i < path.Count; i++) length += Vector2.Distance(new Vector2(path[i-1].x,path[i-1].z), new Vector2(path[i].x,path[i].z));
            var travelled = 0f;
            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0) travelled += Vector2.Distance(new Vector2(path[i-1].x,path[i-1].z), new Vector2(path[i].x,path[i].z));
                path[i] = new Vector3(path[i].x, Mathf.Lerp(start.y, end.y, travelled / Mathf.Max(0.001f, length)), path[i].z);
            }
            return path;
        }

        private static bool InsideXZ(Vector3 p, Bounds b) => p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        private static bool ClearXZ(Vector3 start, Vector3 end, List<Bounds> obstacles)
        {
            start.y = end.y = 0f;
            var direction = end - start;
            var length = direction.magnitude;
            if (length < 0.001f) return true;
            foreach (var b in obstacles)
            {
                var box = new Bounds(new Vector3(b.center.x, 0f, b.center.z), new Vector3(b.size.x, 2f, b.size.z));
                if (box.IntersectRay(new Ray(start, direction / length), out var hit) && hit <= length) return false;
            }
            return true;
        }
        private static Vector3 SamplePath(List<Vector3> path, float t)
        {
            var length = 0f;
            for (var i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            var remaining = length * Mathf.Clamp01(t);
            for (var i = 1; i < path.Count; i++)
            {
                var segment = Vector3.Distance(path[i - 1], path[i]);
                if (remaining <= segment) return Vector3.Lerp(path[i - 1], path[i], remaining / Mathf.Max(0.001f, segment));
                remaining -= segment;
            }
            return path[path.Count - 1];
        }
    }
}
