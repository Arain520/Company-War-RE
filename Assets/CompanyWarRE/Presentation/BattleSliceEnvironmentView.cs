using System;
using CompanyWarRE.Infrastructure.Levels;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceEnvironmentLayout
    {
        public BattleSliceEnvironmentLayout(
            string environmentId,
            int decorRing,
            float battleWidth,
            float battleLength,
            float outerGroundSize,
            float skylineDistance,
            float skylineDensity,
            int seed)
        {
            EnvironmentId = environmentId ?? string.Empty;
            DecorRing = decorRing;
            BattleWidth = battleWidth;
            BattleLength = battleLength;
            OuterGroundSize = outerGroundSize;
            SkylineDistance = skylineDistance;
            SkylineDensity = skylineDensity;
            Seed = seed;
        }

        public string EnvironmentId { get; }
        public int DecorRing { get; }
        public float BattleWidth { get; }
        public float BattleLength { get; }
        public float OuterGroundSize { get; }
        public float SkylineDistance { get; }
        public float SkylineDensity { get; }
        public int Seed { get; }
    }

    [DisallowMultipleComponent]
    public sealed class BattleSliceEnvironmentView : MonoBehaviour
    {
        private Transform _runtimeRoot;

        public BattleSliceEnvironmentLayout Layout { get; private set; }

        public static BattleSliceEnvironmentLayout ResolveLayout(FormalLevelRuntimeMetadata level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var environment = level.Environment ??
                              throw new ArgumentException("Formal level environment is required.", nameof(level));
            var battleWidth = BattleSliceController.GetColumnWorldX(level.RuntimeColumns) + 1f;
            var battleLength = BattleSliceController.GetRowWorldZ(level.RuntimeRows) + 1f;
            var sourceExtent = Mathf.Max(level.SourceColumns, level.SourceRows);
            var outerGroundSize = environment.OuterGroundSize > 0f
                ? Mathf.Max(40f, environment.OuterGroundSize)
                : Mathf.Max(80f, sourceExtent + 60f);
            var skylineDistance = environment.SkylineDistance > 0f
                ? environment.SkylineDistance
                : outerGroundSize * 0.44f;
            return new BattleSliceEnvironmentLayout(
                environment.EnvironmentId,
                Mathf.Max(0, environment.DecorRing),
                battleWidth,
                battleLength,
                outerGroundSize,
                skylineDistance,
                Mathf.Clamp01(environment.SkylineDensity),
                environment.Seed == 0 ? 1001 : environment.Seed);
        }

        public void Build(FormalLevelRuntimeMetadata level)
        {
            Clear();
            Layout = ResolveLayout(level);
            _runtimeRoot = new GameObject("RuntimeEnvironment_" + Layout.EnvironmentId).transform;
            _runtimeRoot.SetParent(transform, false);

            var center = new Vector3(
                BattleSliceController.GetColumnWorldX(level.RuntimeColumns) * 0.5f,
                0f,
                BattleSliceController.GetRowWorldZ(level.RuntimeRows) * 0.5f);
            CreateBlock(
                "OuterGround",
                center + Vector3.down * 0.42f,
                new Vector3(Layout.OuterGroundSize, 0.18f, Layout.OuterGroundSize),
                new Color(0.16f, 0.18f, 0.20f));

            var ringPadding = Layout.DecorRing;
            CreateBlock(
                "IndustrialPlatform",
                center + Vector3.down * 0.24f,
                new Vector3(
                    Layout.BattleWidth + ringPadding * 2f,
                    0.22f,
                    Layout.BattleLength + ringPadding * 2f),
                new Color(0.30f, 0.32f, 0.34f));
            BuildBoundary(center, ringPadding);
            BuildSkyline(center);
            BuildConfiguredPropMarkers(level.Environment, center);
        }

        private void BuildBoundary(Vector3 center, float padding)
        {
            var width = Layout.BattleWidth + padding * 2f;
            var length = Layout.BattleLength + padding * 2f;
            var halfWidth = width * 0.5f;
            var halfLength = length * 0.5f;
            var color = new Color(0.90f, 0.62f, 0.04f);
            CreateBlock("BoundaryNorth", center + new Vector3(0f, -0.08f, halfLength),
                new Vector3(width, 0.12f, 0.16f), color);
            CreateBlock("BoundarySouth", center + new Vector3(0f, -0.08f, -halfLength),
                new Vector3(width, 0.12f, 0.16f), color);
            CreateBlock("BoundaryEast", center + new Vector3(halfWidth, -0.08f, 0f),
                new Vector3(0.16f, 0.12f, length), color);
            CreateBlock("BoundaryWest", center + new Vector3(-halfWidth, -0.08f, 0f),
                new Vector3(0.16f, 0.12f, length), color);
        }

        private void BuildSkyline(Vector3 center)
        {
            var random = new System.Random(Layout.Seed ^ 0x61B5);
            var count = Mathf.RoundToInt(Mathf.Lerp(12f, 36f, Layout.SkylineDensity));
            var half = Mathf.Max(Layout.SkylineDistance, Layout.OuterGroundSize * 0.42f);
            for (var index = 0; index < count; index++)
            {
                var side = index % 4;
                var along = Mathf.Lerp(-half, half, (float)random.NextDouble());
                var height = Mathf.Lerp(2.5f, 9f, (float)random.NextDouble());
                var width = Mathf.Lerp(1.2f, 3.4f, (float)random.NextDouble());
                Vector3 position;
                if (side < 2)
                {
                    position = center + new Vector3(along, height * 0.5f - 0.25f, side == 0 ? half : -half);
                }
                else
                {
                    position = center + new Vector3(side == 2 ? half : -half, height * 0.5f - 0.25f, along);
                }

                CreateBlock(
                    "Skyline_" + (index + 1).ToString("00"),
                    position,
                    new Vector3(width, height, width),
                    new Color(0.12f, 0.14f, 0.17f));
            }
        }

        private void BuildConfiguredPropMarkers(FormalLevelEnvironmentDto environment, Vector3 center)
        {
            if (environment.SceneProps == null)
            {
                return;
            }

            for (var index = 0; index < environment.SceneProps.Count; index++)
            {
                var prop = environment.SceneProps[index];
                if (prop == null)
                {
                    continue;
                }

                var scale = Mathf.Max(0.01f, prop.Scale);
                var marker = CreateBlock(
                    "ConfiguredProp_" + prop.PrefabId,
                    center + new Vector3(prop.OffsetX, scale * 0.5f, prop.OffsetZ),
                    Vector3.one * scale,
                    new Color(0.24f, 0.52f, 0.62f));
                marker.transform.rotation = Quaternion.Euler(0f, prop.RotationY, 0f);
            }
        }

        private GameObject CreateBlock(string objectName, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(_runtimeRoot, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            var renderer = block.GetComponent<Renderer>();
            renderer.material.color = color;
            return block;
        }

        private void Clear()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(_runtimeRoot.gameObject);
            }
            else
            {
                DestroyImmediate(_runtimeRoot.gameObject);
            }

            _runtimeRoot = null;
            Layout = null;
        }
    }
}
