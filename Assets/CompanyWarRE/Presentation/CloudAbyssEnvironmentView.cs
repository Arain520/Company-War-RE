using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            [Min(0f)] public float depthFadeDistance = 14f;
            public Color lightColor = new Color(0.82f, 0.88f, 0.91f, 1f);
            public Color shadowColor = new Color(0.39f, 0.50f, 0.59f, 1f);
        }

        [Header("Cloud Sea")]
        [SerializeField] private Transform bakedCloudRoot;
        [SerializeField] private bool cloudSeaEnabled = true;
        [SerializeField] private bool hideLegacyEnvironmentRenderers = true;
        [SerializeField, Min(200f)] private float minimumCloudDiameter = 2400f;
        [SerializeField, Range(3f, 16f)] private float coverageMultiplier = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float edgeFade = 0.16f;
        [SerializeField] private Texture2D authoredCloudTexture;
        [SerializeField] private Texture2D authoredFlowMap;
        [SerializeField, Range(0f, 0.25f)] private float flowStrength = 0.045f;
        [SerializeField, Range(0f, 1f)] private float flowSpeed = 0.08f;
        [SerializeField, Min(0.01f)] private float flowTiling = 1f;
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

        [Header("Horizon Cloud Ring")]
        [SerializeField] private bool horizonCloudRingEnabled = true;
        [SerializeField, Range(8, 32)] private int horizonCloudCardCount = 16;
        [SerializeField, Min(100f)] private float horizonCloudRingRadius = 520f;
        [SerializeField] private float horizonCloudCenterY = 45f;
        [SerializeField] private Vector2 horizonCloudCardSize = new Vector2(280f, 170f);
        [SerializeField, Range(0f, 1f)] private float horizonCloudAlpha = 0.36f;
        [SerializeField] private int horizonCloudSeed = 9031;

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

        [Header("Unified Sky / Main Light")]
        [SerializeField] private bool unifiedScenePaletteEnabled = true;
        [SerializeField] private Color skyColor = new Color(0.48f, 0.60f, 0.69f, 1f);
        [SerializeField] private Color ambientSkyColor = new Color(0.58f, 0.68f, 0.74f, 1f);
        [SerializeField] private Color ambientEquatorColor = new Color(0.38f, 0.49f, 0.57f, 1f);
        [SerializeField] private Color ambientGroundColor = new Color(0.18f, 0.25f, 0.32f, 1f);
        [SerializeField] private Color mainLightColor = new Color(1f, 0.92f, 0.80f, 1f);
        [SerializeField, Min(0f)] private float mainLightIntensity = 1.15f;
        [SerializeField] private Vector3 mainLightEulerAngles = new Vector3(48f, -32f, 0f);

        [Header("URP Color Grading")]
        [SerializeField] private bool colorGradingEnabled = true;
        [SerializeField, Range(-2f, 2f)] private float postExposure = -0.08f;
        [SerializeField, Range(-100f, 100f)] private float contrast = 8f;
        [SerializeField, Range(-100f, 100f)] private float saturation = -6f;
        [SerializeField] private Color colorFilter = new Color(0.96f, 0.99f, 1f, 1f);
        [SerializeField, Range(-100f, 100f)] private float whiteBalanceTemperature = -4f;
        [SerializeField, Range(-100f, 100f)] private float whiteBalanceTint = 1f;

        [Header("Camera Zoom Response")]
        [SerializeField] private bool zoomResponsiveFog = true;
        [SerializeField, Range(1f, 4f)] private float closeViewFogDistanceScale = 2f;
        [SerializeField, Range(0.1f, 1f)] private float farViewFogStartScale = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float farViewFogEndScale = 0.75f;
        [SerializeField, Range(0.1f, 1f)] private float closeViewCloudOpacityScale = 0.4f;
        [SerializeField, Range(1f, 2f)] private float farViewCloudOpacityScale = 1.2f;
        [SerializeField, Min(0.1f)] private float zoomResponseSpeed = 6f;

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
        private Texture2D _fallbackFlowTexture;
        private VolumeProfile _runtimeVolumeProfile;
        private UniversalAdditionalCameraData _cameraData;
        private bool _cameraDataCreated;
        private bool _originalPostProcessing;
        private bool _originalCameraRequiresDepthTexture;
        private LayerMask _originalVolumeLayerMask;
        private Light _mainLight;
        private Color _originalMainLightColor;
        private float _originalMainLightIntensity;
        private Quaternion _originalMainLightRotation;
        private bool _capturedRenderSettings;
        private bool _originalFogEnabled;
        private FogMode _originalFogMode;
        private Color _originalFogColor;
        private float _originalFogStart;
        private float _originalFogEnd;
        private AmbientMode _originalAmbientMode;
        private Color _originalAmbientSkyColor;
        private Color _originalAmbientEquatorColor;
        private Color _originalAmbientGroundColor;
        private Light _originalSun;
        private Camera _atmosphereCamera;
        private CameraClearFlags _originalCameraClearFlags;
        private Color _originalCameraBackground;
        private float _originalCameraFarClip;
        private float _builtWidth = -1f;
        private float _builtLength = -1f;
        private BattleSliceCameraRig _cameraRig;
        private float _currentViewDistance;
        private bool _viewDistanceInitialized;

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
            if (bakedCloudRoot != null)
            {
                _runtimeRoot = bakedCloudRoot;
                ApplyAtmosphere(boardAnchor);
                return;
            }
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

        private void LateUpdate()
        {
            if (_runtimeRoot == null)
            {
                return;
            }

            var targetViewDistance = ResolveViewDistance();
            if (!_viewDistanceInitialized)
            {
                _currentViewDistance = targetViewDistance;
                _viewDistanceInitialized = true;
            }
            else
            {
                var response = 1f - Mathf.Exp(
                    -Mathf.Max(0.1f, zoomResponseSpeed) * Time.unscaledDeltaTime);
                _currentViewDistance = Mathf.Lerp(
                    _currentViewDistance,
                    targetViewDistance,
                    response);
            }

            ApplyZoomResponsiveAtmosphere(_currentViewDistance);
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
            if (authoredCloudTexture == null)
            {
                _noiseTexture = CreateNoiseTexture(
                    Mathf.Clamp(proceduralNoiseResolution, 32, 512),
                    proceduralNoiseSeed);
            }
            if (authoredFlowMap == null)
            {
                _fallbackFlowTexture = CreateNeutralFlowTexture();
            }
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

            var fogController = CreateGroup("FogController", _runtimeRoot);
            CreateColorGradingVolume(fogController);
            var abyssEnvironment = CreateGroup("AbyssEnvironment", _runtimeRoot);
            if (horizonCloudRingEnabled)
            {
                CreateHorizonCloudRing(abyssEnvironment);
            }
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
                settings.depthFadeDistance,
                ResolveCloudTexture());
            layer.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateHorizonCloudRing(Transform parent)
        {
            var ring = CreateGroup("HorizonCloudRing", parent);
            var material = CreateCloudMaterial(
                new Color(0.82f, 0.89f, 0.93f, 1f),
                new Color(0.38f, 0.49f, 0.58f, 1f),
                1.15f,
                new Vector2(0.17f, 0.41f),
                new Vector2(-0.0012f, 0.0008f),
                horizonCloudAlpha,
                0.60f,
                0.20f,
                0.30f,
                20f,
                ResolveCloudTexture());
            var random = new System.Random(horizonCloudSeed);
            var count = Mathf.Clamp(horizonCloudCardCount, 8, 32);
            var radius = Mathf.Max(100f, horizonCloudRingRadius);
            var baseWidth = Mathf.Max(20f, horizonCloudCardSize.x);
            var baseHeight = Mathf.Max(20f, horizonCloudCardSize.y);
            for (var index = 0; index < count; index++)
            {
                var normalized = index / (float)count;
                var angle = normalized * Mathf.PI * 2f + NextRange(random, -0.07f, 0.07f);
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"HorizonCloudCard_{index + 1:00}";
                card.transform.SetParent(ring, false);
                card.transform.localPosition = radial * radius + new Vector3(
                    0f,
                    horizonCloudCenterY + NextRange(random, -28f, 28f),
                    0f);
                card.transform.localRotation = Quaternion.LookRotation(-radial, Vector3.up);
                card.transform.localScale = new Vector3(
                    baseWidth * NextRange(random, 0.82f, 1.22f),
                    baseHeight * NextRange(random, 0.78f, 1.18f),
                    1f);
                DisableAndRemoveCollider(card);
                var renderer = card.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void CreateColorGradingVolume(Transform parent)
        {
            if (!colorGradingEnabled)
            {
                return;
            }

            var volumeObject = new GameObject("UnifiedColorGrading");
            volumeObject.transform.SetParent(parent, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            _runtimeVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeVolumeProfile.name = "VP_RuntimeCloudAbyss";
            _runtimeVolumeProfile.hideFlags = HideFlags.DontSave;
            volume.sharedProfile = _runtimeVolumeProfile;

            var adjustments = _runtimeVolumeProfile.Add<ColorAdjustments>(true);
            adjustments.postExposure.Override(postExposure);
            adjustments.contrast.Override(contrast);
            adjustments.saturation.Override(saturation);
            adjustments.colorFilter.Override(colorFilter);
            var whiteBalance = _runtimeVolumeProfile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(whiteBalanceTemperature);
            whiteBalance.tint.Override(whiteBalanceTint);
        }

        private Texture ResolveCloudTexture()
        {
            return authoredCloudTexture != null ? authoredCloudTexture : _noiseTexture;
        }

        private Texture ResolveFlowTexture()
        {
            return authoredFlowMap != null ? authoredFlowMap : _fallbackFlowTexture;
        }

        private static float NextRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
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
                0f,
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
            float depthFadeDistance,
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
            SetTexture(material, "_FlowMap", ResolveFlowTexture());
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
            SetFloat(material, "_FlowTiling", Mathf.Max(0.01f, flowTiling));
            SetFloat(material, "_FlowStrength", Mathf.Clamp(flowStrength, 0f, 0.25f));
            SetFloat(material, "_FlowSpeed", Mathf.Max(0f, flowSpeed));
            SetFloat(material, "_DepthFadeDistance", Mathf.Max(0f, depthFadeDistance));
            SetFloat(material, "_UseVertexColor", texture == _particleTexture ? 1f : 0f);
            _runtimeMaterials.Add(material);
            return material;
        }

        private void ApplyAtmosphere(Transform boardAnchor)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = distanceFogColor;

            var camera = Camera.main;
            if (camera != null)
            {
                CaptureCamera(camera);
                _cameraRig = camera.GetComponent<BattleSliceCameraRig>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = unifiedScenePaletteEnabled
                    ? skyColor
                    : abyssBackgroundColor;
                camera.farClipPlane = Mathf.Max(
                    camera.farClipPlane,
                    Mathf.Max(distanceFogEnd + 40f, minimumCloudDiameter * 0.75f));
                if (_cameraData == null)
                {
                    var hadCameraData = camera.TryGetComponent<UniversalAdditionalCameraData>(
                        out _cameraData);
                    if (!hadCameraData)
                    {
                        _cameraData = camera.GetUniversalAdditionalCameraData();
                        _cameraDataCreated = true;
                    }
                    _originalPostProcessing = _cameraData.renderPostProcessing;
                    _originalCameraRequiresDepthTexture = _cameraData.requiresDepthTexture;
                    _originalVolumeLayerMask = _cameraData.volumeLayerMask;
                }
                _cameraData.renderPostProcessing = colorGradingEnabled;
                _cameraData.requiresDepthTexture = true;
                _cameraData.volumeLayerMask |= 1 << FormalBattleMapView.EnvironmentLayer;
            }

            ApplyUnifiedScenePalette();

            Shader.SetGlobalFloat(
                "_CompanyWarHeightFogTopY",
                GetWorldHeight(boardAnchor, heightFogTopY));
            Shader.SetGlobalFloat(
                "_CompanyWarHeightFogBottomY",
                GetWorldHeight(boardAnchor, HeightFogBottomY));
            Shader.SetGlobalColor("_CompanyWarHeightFogColor", heightFogColor);
            Shader.SetGlobalFloat("_CompanyWarHeightFogStrength", heightFogStrength);
            Shader.SetGlobalFloat("_CompanyWarHeightFogCurve", heightFogCurve);

            _currentViewDistance = ResolveViewDistance();
            _viewDistanceInitialized = true;
            ApplyZoomResponsiveAtmosphere(_currentViewDistance);
        }

        private void ApplyUnifiedScenePalette()
        {
            if (!unifiedScenePaletteEnabled)
            {
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;

            if (_mainLight == null)
            {
                _mainLight = ResolveMainDirectionalLight();
                if (_mainLight != null)
                {
                    _originalMainLightColor = _mainLight.color;
                    _originalMainLightIntensity = _mainLight.intensity;
                    _originalMainLightRotation = _mainLight.transform.rotation;
                }
            }
            if (_mainLight == null)
            {
                return;
            }

            _mainLight.color = mainLightColor;
            _mainLight.intensity = mainLightIntensity;
            _mainLight.transform.rotation = Quaternion.Euler(mainLightEulerAngles);
            RenderSettings.sun = _mainLight;
        }

        private static Light ResolveMainDirectionalLight()
        {
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            {
                return RenderSettings.sun;
            }

            foreach (var light in FindObjectsOfType<Light>(true))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }
            return null;
        }

        private float ResolveViewDistance()
        {
            if (!zoomResponsiveFog)
            {
                return 1f;
            }

            if (_cameraRig == null)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    _cameraRig = camera.GetComponent<BattleSliceCameraRig>();
                }
            }

            return _cameraRig != null ? _cameraRig.NormalizedViewDistance : 1f;
        }

        private void ApplyZoomResponsiveAtmosphere(float viewDistance)
        {
            var normalized = Mathf.Clamp01(viewDistance);
            RenderSettings.fog = distanceFogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = distanceFogColor;

            if (zoomResponsiveFog)
            {
                var startScale = Mathf.Lerp(
                    closeViewFogDistanceScale,
                    farViewFogStartScale,
                    normalized);
                var endScale = Mathf.Lerp(
                    closeViewFogDistanceScale,
                    farViewFogEndScale,
                    normalized);
                RenderSettings.fogStartDistance = Mathf.Max(0f, distanceFogStart * startScale);
                RenderSettings.fogEndDistance = Mathf.Max(
                    RenderSettings.fogStartDistance + 1f,
                    distanceFogEnd * endScale);
                Shader.SetGlobalFloat(
                    "_CompanyWarCloudOpacityScale",
                    Mathf.Lerp(
                        closeViewCloudOpacityScale,
                        farViewCloudOpacityScale,
                        normalized));
                return;
            }

            RenderSettings.fogStartDistance = Mathf.Max(0f, distanceFogStart);
            RenderSettings.fogEndDistance = Mathf.Max(distanceFogStart + 1f, distanceFogEnd);
            Shader.SetGlobalFloat("_CompanyWarCloudOpacityScale", 1f);
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
            _originalAmbientMode = RenderSettings.ambientMode;
            _originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            _originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            _originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            _originalSun = RenderSettings.sun;
        }

        private void CaptureCamera(Camera camera)
        {
            if (_atmosphereCamera != null || camera == null)
            {
                return;
            }

            _atmosphereCamera = camera;
            _originalCameraClearFlags = camera.clearFlags;
            _originalCameraBackground = camera.backgroundColor;
            _originalCameraFarClip = camera.farClipPlane;
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
            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientSkyColor = _originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _originalAmbientGroundColor;
            RenderSettings.sun = _originalSun;
            if (_mainLight != null)
            {
                _mainLight.color = _originalMainLightColor;
                _mainLight.intensity = _originalMainLightIntensity;
                _mainLight.transform.rotation = _originalMainLightRotation;
            }
            if (_atmosphereCamera != null)
            {
                _atmosphereCamera.clearFlags = _originalCameraClearFlags;
                _atmosphereCamera.backgroundColor = _originalCameraBackground;
                _atmosphereCamera.farClipPlane = _originalCameraFarClip;
            }
            if (_cameraData != null)
            {
                _cameraData.renderPostProcessing = _originalPostProcessing;
                _cameraData.requiresDepthTexture = _originalCameraRequiresDepthTexture;
                _cameraData.volumeLayerMask = _originalVolumeLayerMask;
                if (_cameraDataCreated)
                {
                    DestroySafe(_cameraData);
                }
            }
            _cameraData = null;
            _cameraDataCreated = false;
            _atmosphereCamera = null;
            _mainLight = null;
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

        private static Texture2D CreateNeutralFlowTexture()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
            {
                name = "T_RuntimeNeutralFlow",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixel(0, 0, new Color(0.5f, 0.5f, 0f, 1f));
            texture.Apply(false, true);
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
            if (_runtimeRoot != bakedCloudRoot)
                DestroySafe(_runtimeRoot != null ? _runtimeRoot.gameObject : null);
            _runtimeRoot = null;
            foreach (var material in _runtimeMaterials)
            {
                DestroySafe(material);
            }
            _runtimeMaterials.Clear();
            DestroySafe(_runtimeVolumeProfile);
            DestroySafe(_noiseTexture);
            DestroySafe(_particleTexture);
            DestroySafe(_fallbackFlowTexture);
            _runtimeVolumeProfile = null;
            _noiseTexture = null;
            _particleTexture = null;
            _fallbackFlowTexture = null;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeVisuals();
            RestoreLegacyEnvironmentVisuals();
            RestoreRenderSettings();
            Shader.SetGlobalFloat("_CompanyWarCloudOpacityScale", 1f);
        }

        private static void DestroySafe(UnityEngine.Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
