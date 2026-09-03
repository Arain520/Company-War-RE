using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Presentation-only cloud sea, atmospheric fog and sparse local cloud system.
    /// It deliberately owns no grid, occupation, deployment or combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudAbyssEnvironmentView : MonoBehaviour
    {
        [Serializable]
        private sealed class CloudLayerSettings
        {
            public string name = "CloudLayer";
            public float height = -10f;
            [Min(0.01f)] public float tiling = 3f;
            public Vector2 uvOffset = Vector2.zero;
            public Vector2 scrollSpeed = new Vector2(-0.004f, 0.002f);
            [Range(0f, 1f)] public float alpha = 0.5f;
            [Range(0f, 1f)] public float density = 0.58f;
            [Range(0.01f, 0.5f)] public float softness = 0.16f;
            public Color lightColor = new Color(0.82f, 0.88f, 0.91f, 1f);
            public Color shadowColor = new Color(0.39f, 0.50f, 0.59f, 1f);
        }

        [Header("Cloud Sea")]
        [SerializeField] private bool cloudSeaEnabled = true;
        [SerializeField] private bool hideLegacyEnvironmentRenderers = true;
        [SerializeField, Min(200f)] private float minimumCloudDiameter = 2400f;
        [SerializeField, Range(3f, 16f)] private float coverageMultiplier = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float edgeFade = 0.16f;
        [SerializeField, Range(32, 512)] private int proceduralNoiseResolution = 128;
        [SerializeField] private int proceduralNoiseSeed = 4219;
        [SerializeField] private CloudLayerSettings[] cloudLayers =
        {
            new CloudLayerSettings
            {
                name = "CloudLayer_High",
                height = -7f,
                tiling = 8f,
                scrollSpeed = new Vector2(0.010f, 0.003f),
                alpha = 0.28f,
                density = 0.46f,
                softness = 0.18f,
                lightColor = new Color(0.88f, 0.92f, 0.94f, 1f),
                shadowColor = new Color(0.50f, 0.61f, 0.69f, 1f)
            },
            new CloudLayerSettings
            {
                name = "CloudLayer_Main",
                height = -10f,
                tiling = 3f,
                scrollSpeed = new Vector2(-0.004f, 0.002f),
                alpha = 0.58f,
                density = 0.62f,
                softness = 0.17f,
                lightColor = new Color(0.82f, 0.88f, 0.91f, 1f),
                shadowColor = new Color(0.39f, 0.50f, 0.59f, 1f)
            },
            new CloudLayerSettings
            {
                name = "CloudLayer_Low",
                height = -13f,
                tiling = 1f,
                scrollSpeed = new Vector2(0.001f, -0.0015f),
                alpha = 0.78f,
                density = 0.72f,
                softness = 0.20f,
                lightColor = new Color(0.68f, 0.76f, 0.81f, 1f),
                shadowColor = new Color(0.25f, 0.34f, 0.43f, 1f)
            }
        };

        [Header("Pillar Height Fog")]
        [SerializeField] private float heightFogTopY = 8f;
        [SerializeField] private float heightFogBottomY = -24f;
        [SerializeField] private Color heightFogColor = new Color(0.56f, 0.66f, 0.72f, 1f);
        [SerializeField, Range(0f, 1f)] private float heightFogStrength = 0.96f;
        [SerializeField, Range(0.25f, 4f)] private float heightFogCurve = 1.35f;
        [SerializeField, Min(1f)] private float minimumBottomDepthBelowCloud = 20f;

        [Header("Distance Fog / Air Perspective")]
        [SerializeField] private bool distanceFogEnabled = true;
        [SerializeField] private Color distanceFogColor = new Color(0.48f, 0.59f, 0.66f, 1f);
        [SerializeField, Min(0f)] private float distanceFogStart = 180f;
        [SerializeField, Min(1f)] private float distanceFogEnd = 560f;
        [SerializeField] private Color abyssBackgroundColor = new Color(0.32f, 0.43f, 0.52f, 1f);

        [Header("Local Cloud Particles")]
        [SerializeField] private bool localCloudParticlesEnabled = true;
        [SerializeField, Range(0, 64)] private int particleCount = 20;
        [SerializeField] private Vector2 particleSize = new Vector2(18f, 34f);
        [SerializeField] private Vector2 particleLifetime = new Vector2(24f, 38f);
        [SerializeField, Min(0f)] private float particleDriftSpeed = 0.35f;
        [SerializeField] private float particleUpwardSpeed = 0.08f;
        [SerializeField, Range(0f, 1f)] private float particleAlpha = 0.16f;
        [SerializeField] private Vector3 particleSpawnArea = new Vector3(220f, 14f, 280f);
        [SerializeField] private float particleCenterY = -5f;

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly Dictionary<Renderer, bool> _legacyRendererStates =
            new Dictionary<Renderer, bool>();
        private Transform _runtimeRoot;
        private Texture2D _noiseTexture;
        private Texture2D _particleTexture;
        private bool _capturedRenderSettings;
        private bool _originalFogEnabled;
        private FogMode _originalFogMode;
        private Color _originalFogColor;
        private float _originalFogStart;
        private float _originalFogEnd;
        private float _builtWidth = -1f;
        private float _builtLength = -1f;

        public float HeightFogTopY => heightFogTopY;
        public float HeightFogBottomY => Mathf.Min(heightFogBottomY, heightFogTopY - 0.01f);
        public Color HeightFogColor => heightFogColor;
        public float HeightFogStrength => heightFogStrength;
        public float HeightFogCurve => heightFogCurve;
        public float LowestCloudHeight => ResolveLowestCloudHeight();
        public float MinimumPillarBottomY =>
            LowestCloudHeight - Mathf.Max(1f, minimumBottomDepthBelowCloud);

        public void Build(
            Transform environmentRoot,
            Transform boardAnchor,
            float boardWidth,
            float boardLength)
        {
            if (!cloudSeaEnabled || environmentRoot == null || boardAnchor == null)
            {
                return;
            }

            CaptureRenderSettings();
            if (hideLegacyEnvironmentRenderers)
            {
                HideLegacyEnvironmentVisuals(environmentRoot);
            }

            var safeWidth = Mathf.Max(1f, boardWidth);
            var safeLength = Mathf.Max(1f, boardLength);
            var requiresRebuild = _runtimeRoot == null ||
                                  Mathf.Abs(_builtWidth - safeWidth) > 0.01f ||
                                  Mathf.Abs(_builtLength - safeLength) > 0.01f;
            if (requiresRebuild)
            {
                RebuildVisuals(environmentRoot, boardAnchor, safeWidth, safeLength);
            }

            ApplyAtmosphere(boardAnchor);
            SetLayerRecursively(_runtimeRoot, FormalBattleMapView.EnvironmentLayer);
        }

        public float GetWorldHeight(Transform boardAnchor, float localHeight)
        {
            return boardAnchor != null
                ? boardAnchor.TransformPoint(new Vector3(0f, localHeight, 0f)).y
                : localHeight;
        }

        private void RebuildVisuals(
            Transform environmentRoot,
            Transform boardAnchor,
            float boardWidth,
            float boardLength)
        {
            ReleaseRuntimeVisuals();
            _builtWidth = boardWidth;
            _builtLength = boardLength;
            _noiseTexture = CreateNoiseTexture(
                Mathf.Clamp(proceduralNoiseResolution, 32, 512),
                proceduralNoiseSeed);
            _particleTexture = CreateParticleTexture(96, proceduralNoiseSeed ^ 0x33A1);

            _runtimeRoot = new GameObject("CloudAbyssEnvironment").transform;
            _runtimeRoot.SetParent(environmentRoot, false);
            _runtimeRoot.position = boardAnchor.position;
            _runtimeRoot.rotation = boardAnchor.rotation;

            var cloudSea = CreateGroup("CloudSea", _runtimeRoot);
            var diameter = Mathf.Max(
                minimumCloudDiameter,
                Mathf.Max(boardWidth, boardLength) * Mathf.Max(3f, coverageMultiplier));
            if (cloudLayers != null)
            {
                for (var index = 0; index < cloudLayers.Length; index++)
                {
                    CreateCloudLayer(cloudSea, cloudLayers[index], diameter, index);
                }
            }

            var particleRoot = CreateGroup("CloudParticles", _runtimeRoot);
            if (localCloudParticlesEnabled && particleCount > 0)
            {
                CreateCloudParticles(particleRoot, boardWidth, boardLength);
            }

            CreateGroup("FogController", _runtimeRoot);
            CreateGroup("AbyssEnvironment", _runtimeRoot);
        }

        private void CreateCloudLayer(
            Transform parent,
            CloudLayerSettings settings,
            float diameter,
            int index)
        {
            if (settings == null)
            {
                return;
            }

            var layer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            layer.name = string.IsNullOrWhiteSpace(settings.name)
                ? $"CloudLayer_{index + 1}"
                : settings.name.Trim();
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = new Vector3(0f, settings.height, 0f);
            layer.transform.localScale = new Vector3(diameter / 10f, 1f, diameter / 10f);
            DisableAndRemoveCollider(layer);

            var material = CreateCloudMaterial(
                settings.lightColor,
                settings.shadowColor,
                settings.tiling,
                settings.uvOffset,
                settings.scrollSpeed,
                settings.alpha,
                settings.density,
                settings.softness,
                edgeFade,
                _noiseTexture);
            layer.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateCloudParticles(Transform parent, float boardWidth, float boardLength)
        {
            var particleObject = new GameObject("SparseLargeClouds");
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = new Vector3(0f, particleCenterY, 0f);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, particleCount);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Max(1f, Mathf.Min(particleLifetime.x, particleLifetime.y)),
                Mathf.Max(1f, Mathf.Max(particleLifetime.x, particleLifetime.y)));
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(1f, Mathf.Min(particleSize.x, particleSize.y)),
                Mathf.Max(1f, Mathf.Max(particleSize.x, particleSize.y)));
            main.startColor = new Color(0.82f, 0.90f, 0.94f, particleAlpha);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0.05f, particleCount / Mathf.Max(2f, particleLifetime.y));

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                Mathf.Max(particleSpawnArea.x, boardWidth * 1.2f),
                Mathf.Max(1f, particleSpawnArea.y),
                Mathf.Max(particleSpawnArea.z, boardLength * 1.2f));

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-particleDriftSpeed, particleDriftSpeed);
            // Unity requires all three velocity axes to use the same MinMaxCurve mode.
            // Keep Y effectively constant by using identical bounds in TwoConstants mode.
            velocity.y = new ParticleSystem.MinMaxCurve(
                particleUpwardSpeed,
                particleUpwardSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-particleDriftSpeed, particleDriftSpeed);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.72f, 0.82f, 0.88f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.8f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = CreateCloudMaterial(
                new Color(0.90f, 0.94f, 0.96f, 1f),
                new Color(0.54f, 0.65f, 0.72f, 1f),
                1f,
                Vector2.zero,
                Vector2.zero,
                1f,
                0.48f,
                0.22f,
                0.35f,
                _particleTexture);
            particles.Play();
        }

        private Material CreateCloudMaterial(
            Color lightColor,
            Color shadowColor,
            float tiling,
            Vector2 uvOffset,
            Vector2 scrollSpeed,
            float alpha,
            float density,
            float softness,
            float materialEdgeFade,
            Texture texture)
        {
            var shader = Shader.Find("CompanyWarRE/CloudSeaURP") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = "MAT_RuntimeCloudAbyss",
                hideFlags = HideFlags.DontSave
            };
            SetTexture(material, "_NoiseTex", texture);
            SetTexture(material, "_BaseMap", texture);
            SetColor(material, "_LightColor", lightColor);
            SetColor(material, "_ShadowColor", shadowColor);
            SetColor(material, "_BaseColor", lightColor);
            SetFloat(material, "_Tiling", Mathf.Max(0.01f, tiling));
            if (material.HasProperty("_UvOffset"))
            {
                material.SetVector("_UvOffset", uvOffset);
            }
            if (material.HasProperty("_ScrollSpeed"))
            {
                material.SetVector("_ScrollSpeed", scrollSpeed);
            }
            SetFloat(material, "_Opacity", Mathf.Clamp01(alpha));
            SetFloat(material, "_Density", Mathf.Clamp01(density));
            SetFloat(material, "_Softness", Mathf.Clamp(softness, 0.01f, 0.5f));
            SetFloat(material, "_EdgeFade", Mathf.Clamp01(materialEdgeFade));
            SetFloat(material, "_UseVertexColor", texture == _particleTexture ? 1f : 0f);
            _runtimeMaterials.Add(material);
            return material;
        }

        private void ApplyAtmosphere(Transform boardAnchor)
        {
            RenderSettings.fog = distanceFogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = distanceFogColor;
            RenderSettings.fogStartDistance = Mathf.Max(0f, distanceFogStart);
            RenderSettings.fogEndDistance = Mathf.Max(distanceFogStart + 1f, distanceFogEnd);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = abyssBackgroundColor;
                camera.farClipPlane = Mathf.Max(
                    camera.farClipPlane,
                    Mathf.Max(distanceFogEnd + 40f, minimumCloudDiameter * 0.75f));
            }

            Shader.SetGlobalFloat(
                "_CompanyWarHeightFogTopY",
                GetWorldHeight(boardAnchor, heightFogTopY));
            Shader.SetGlobalFloat(
                "_CompanyWarHeightFogBottomY",
                GetWorldHeight(boardAnchor, HeightFogBottomY));
            Shader.SetGlobalColor("_CompanyWarHeightFogColor", heightFogColor);
            Shader.SetGlobalFloat("_CompanyWarHeightFogStrength", heightFogStrength);
            Shader.SetGlobalFloat("_CompanyWarHeightFogCurve", heightFogCurve);
        }

        private void HideLegacyEnvironmentVisuals(Transform environmentRoot)
        {
            foreach (var renderer in environmentRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (_runtimeRoot != null && renderer.transform.IsChildOf(_runtimeRoot))
                {
                    continue;
                }

                if (!_legacyRendererStates.ContainsKey(renderer))
                {
                    _legacyRendererStates.Add(renderer, renderer.enabled);
                }
                renderer.enabled = false;
            }
        }

        private void RestoreLegacyEnvironmentVisuals()
        {
            foreach (var pair in _legacyRendererStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = pair.Value;
                }
            }
            _legacyRendererStates.Clear();
        }

        private void CaptureRenderSettings()
        {
            if (_capturedRenderSettings)
            {
                return;
            }

            _capturedRenderSettings = true;
            _originalFogEnabled = RenderSettings.fog;
            _originalFogMode = RenderSettings.fogMode;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogStart = RenderSettings.fogStartDistance;
            _originalFogEnd = RenderSettings.fogEndDistance;
        }

        private void RestoreRenderSettings()
        {
            if (!_capturedRenderSettings)
            {
                return;
            }

            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogMode = _originalFogMode;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogStartDistance = _originalFogStart;
            RenderSettings.fogEndDistance = _originalFogEnd;
            _capturedRenderSettings = false;
        }

        private float ResolveLowestCloudHeight()
        {
            var lowest = float.MaxValue;
            if (cloudLayers != null)
            {
                foreach (var layer in cloudLayers)
                {
                    if (layer != null)
                    {
                        lowest = Mathf.Min(lowest, layer.height);
                    }
                }
            }
            return lowest < float.MaxValue ? lowest : -10f;
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static void DisableAndRemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }
            collider.enabled = false;
            DestroySafe(collider);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
            {
                return;
            }
            root.gameObject.layer = layer;
            for (var index = 0; index < root.childCount; index++)
            {
                SetLayerRecursively(root.GetChild(index), layer);
            }
        }

        private static Texture2D CreateNoiseTexture(int resolution, int seed)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = "T_RuntimeCloudNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[resolution * resolution];
            var seedX = Mathf.Abs(seed % 997) * 0.137f;
            var seedY = Mathf.Abs(seed % 617) * 0.193f;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var u = x / (float)Mathf.Max(1, resolution - 1);
                    var v = y / (float)Mathf.Max(1, resolution - 1);
                    var value = 0f;
                    var amplitude = 0.58f;
                    var frequency = 2f;
                    var total = 0f;
                    for (var octave = 0; octave < 4; octave++)
                    {
                        value += SampleTileablePerlin(
                            u,
                            v,
                            frequency,
                            seedX,
                            seedY) * amplitude;
                        total += amplitude;
                        amplitude *= 0.5f;
                        frequency *= 2.03f;
                    }
                    value = Mathf.Clamp01(value / Mathf.Max(0.001f, total));
                    pixels[y * resolution + x] = new Color(value, value, value, value);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static float SampleTileablePerlin(
            float u,
            float v,
            float frequency,
            float offsetX,
            float offsetY)
        {
            var x = u * frequency;
            var y = v * frequency;
            var lowerLeft = Mathf.PerlinNoise(offsetX + x, offsetY + y);
            var lowerRight = Mathf.PerlinNoise(offsetX + x - frequency, offsetY + y);
            var upperLeft = Mathf.PerlinNoise(offsetX + x, offsetY + y - frequency);
            var upperRight = Mathf.PerlinNoise(
                offsetX + x - frequency,
                offsetY + y - frequency);
            return Mathf.Lerp(
                Mathf.Lerp(lowerLeft, lowerRight, u),
                Mathf.Lerp(upperLeft, upperRight, u),
                v);
        }

        private static Texture2D CreateParticleTexture(int resolution, int seed)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
            {
                name = "T_RuntimeCloudParticle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[resolution * resolution];
            var offset = Mathf.Abs(seed % 701) * 0.11f;
            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var u = (x + 0.5f) / resolution;
                    var v = (y + 0.5f) / resolution;
                    var dx = u * 2f - 1f;
                    var dy = v * 2f - 1f;
                    var radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    radial = Mathf.SmoothStep(0f, 1f, radial);
                    var noise = Mathf.PerlinNoise(offset + u * 3.1f, offset + v * 3.1f);
                    var alpha = Mathf.Clamp01(radial * Mathf.Lerp(0.68f, 1f, noise));
                    pixels[y * resolution + x] = new Color(noise, noise, noise, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (material.HasProperty(property)) material.SetTexture(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private void ReleaseRuntimeVisuals()
        {
            DestroySafe(_runtimeRoot != null ? _runtimeRoot.gameObject : null);
            _runtimeRoot = null;
            foreach (var material in _runtimeMaterials)
            {
                DestroySafe(material);
            }
            _runtimeMaterials.Clear();
            DestroySafe(_noiseTexture);
            DestroySafe(_particleTexture);
            _noiseTexture = null;
            _particleTexture = null;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeVisuals();
            RestoreLegacyEnvironmentVisuals();
            RestoreRenderSettings();
        }

        private static void DestroySafe(UnityEngine.Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
