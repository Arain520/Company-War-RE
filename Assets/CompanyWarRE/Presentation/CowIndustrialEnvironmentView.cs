using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Editable map-environment module rebuilt from Cow's formal industrial surround.
    /// It never owns battle state and all generated or imported content is non-interactive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CowIndustrialEnvironmentView : MonoBehaviour
    {
        private const int DecorationLayer = FormalBattleMapView.EnvironmentLayer;

        [Header("URP")]
        [SerializeField] private Material urpMaterialTemplate;

        [Header("Migrated optional Cow models")]
        [SerializeField] private GameObject railingPrefab;
        [SerializeField] private GameObject signalBasePrefab;
        [SerializeField] private GameObject serverTerminalPrefab;
        [SerializeField] private GameObject storageTankPrefab;
        [SerializeField] private GameObject cargoUnit01Prefab;
        [SerializeField] private GameObject cargoUnit02Prefab;
        [SerializeField] private GameObject boardPartPrefab;

        [Header("Environment groups")]
        [SerializeField] private bool includeRailings = true;
        [SerializeField] private bool includePipes = true;
        [SerializeField] private bool includeStairs = true;
        [SerializeField] private bool includeSkyline = true;
        [SerializeField] private bool includeOptionalModels = true;
        [SerializeField, Range(16, 320)] private int skylineBlockCount = 96;
        [SerializeField, Range(4f, 20f)] private float environmentExtentMultiplier = 8f;
        [SerializeField] private int randomSeed = 1001;
        [SerializeField] private List<string> excludedGeneratedObjectNames = new List<string>();

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly Dictionary<Material, Material> _convertedImportedMaterials =
            new Dictionary<Material, Material>();
        private Transform _generatedRoot;
        private int _builtColumns;
        private int _builtRows;

        public bool IncludesRailings => includeRailings;
        public bool IncludesPipes => includePipes;
        public bool IncludesStairs => includeStairs;
        public bool IncludesSkyline => includeSkyline;
        public bool IncludesOptionalModels => includeOptionalModels;
        public bool HasUrpMaterialTemplate => urpMaterialTemplate != null;
        public float EnvironmentExtentMultiplier => environmentExtentMultiplier;
        public int ConfiguredOptionalModelCount =>
            CountConfiguredModels();
        public IReadOnlyList<string> ExcludedGeneratedObjectNames => excludedGeneratedObjectNames;

        public bool ExcludeGeneratedObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            var normalized = objectName.Trim();
            if (excludedGeneratedObjectNames.Contains(normalized))
            {
                return false;
            }

            excludedGeneratedObjectNames.Add(normalized);
            return true;
        }

        public bool RestoreGeneratedObject(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName) &&
                   excludedGeneratedObjectNames.Remove(objectName.Trim());
        }

        public void ConfigureEnvironmentExtent(float multiplier, int skylineCount)
        {
            environmentExtentMultiplier = Mathf.Clamp(multiplier, 4f, 20f);
            skylineBlockCount = Mathf.Clamp(skylineCount, 16, 320);
        }

        public void RefreshManualImportedMaterials()
        {
            var editable = transform.Find("EditableEnvironmentModelFill");
            if (editable == null)
            {
                return;
            }

            for (var index = 0; index < editable.childCount; index++)
            {
                var manual = editable.GetChild(index);
                var source = ResolveImportedSource(manual.name);
                if (source != null)
                {
                    RestoreRendererMaterialsFromSource(manual, source.transform);
                }
            }
        }

        public void Configure(
            Material materialTemplate,
            GameObject railing,
            GameObject signalBase,
            GameObject serverTerminal,
            GameObject storageTank,
            GameObject cargoUnit01,
            GameObject cargoUnit02,
            GameObject boardPart)
        {
            urpMaterialTemplate = materialTemplate;
            railingPrefab = railing;
            signalBasePrefab = signalBase;
            serverTerminalPrefab = serverTerminal;
            storageTankPrefab = storageTank;
            cargoUnit01Prefab = cargoUnit01;
            cargoUnit02Prefab = cargoUnit02;
            boardPartPrefab = boardPart;
        }

        public void Build(int columns, int rows)
        {
            var safeColumns = Mathf.Max(1, columns);
            var safeRows = Mathf.Max(1, rows);
            if (_generatedRoot != null &&
                _generatedRoot.childCount > 0 &&
                _builtColumns == safeColumns &&
                _builtRows == safeRows)
            {
                Sanitize(_generatedRoot.gameObject);
                return;
            }

            ReplaceGeneratedRoot();
            _builtColumns = safeColumns;
            _builtRows = safeRows;

            var neutral = CreateMaterial(new Color(0.62f, 0.65f, 0.65f), 0.18f);
            var dark = CreateMaterial(new Color(0.08f, 0.10f, 0.12f), 0.12f);
            var metal = CreateMaterial(new Color(0.48f, 0.52f, 0.53f), 0.34f);
            var warning = CreateMaterial(new Color(0.95f, 0.78f, 0.05f), 0.16f);
            var distant = CreateMaterial(new Color(0.58f, 0.63f, 0.65f), 0.08f);

            if (includeRailings)
            {
                BuildRailings(safeColumns, safeRows, warning);
            }
            if (includePipes)
            {
                BuildPipes(safeColumns, safeRows, metal, dark);
            }
            if (includeStairs)
            {
                BuildStairs(safeColumns, safeRows, neutral, warning);
            }
            if (includeSkyline)
            {
                BuildSkyline(safeColumns, safeRows, distant, dark);
            }
            if (includeOptionalModels)
            {
                BuildOptionalModels(safeColumns, safeRows, neutral);
            }

            ApplyGenerationExclusions();
            Sanitize(_generatedRoot.gameObject);
        }

        private void ApplyGenerationExclusions()
        {
            if (excludedGeneratedObjectNames == null || excludedGeneratedObjectNames.Count == 0)
            {
                return;
            }

            var excluded = new HashSet<string>(excludedGeneratedObjectNames, StringComparer.Ordinal);
            var remove = new List<GameObject>();
            foreach (var child in _generatedRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child != _generatedRoot && excluded.Contains(child.name))
                {
                    remove.Add(child.gameObject);
                }
            }

            foreach (var target in remove)
            {
                if (target != null)
                {
                    DestroySafe(target);
                }
            }
        }

        private void BuildRailings(int columns, int rows, Material material)
        {
            var halfX = columns * 0.5f;
            var halfZ = rows * 0.5f;
            var placements = new[]
            {
                new Placement(new Vector3(-halfX - 2.0f, 0f, halfZ + 1.2f), 0f, 2.8f),
                new Placement(new Vector3(halfX + 2.0f, 0f, halfZ + 1.2f), 0f, 2.8f),
                new Placement(new Vector3(-halfX - 2.1f, 0f, -halfZ - 1.0f), 0f, 2.5f),
                new Placement(new Vector3(halfX + 2.1f, 0f, -halfZ - 1.0f), 0f, 2.5f),
                new Placement(new Vector3(-halfX - 2.2f, 0f, 0f), 90f, 3.2f),
                new Placement(new Vector3(halfX + 2.2f, 0f, 0f), 90f, 3.2f)
            };

            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                if (railingPrefab != null)
                {
                    CreateImported(
                        railingPrefab,
                        $"ENV_Railing_{index:00}",
                        placement.Position,
                        placement.RotationY,
                        new Vector3(placement.Size, 1.2f, 0.65f),
                        material);
                }
                else
                {
                    CreateProceduralRailing(
                        $"ENV_Railing_{index:00}",
                        placement.Position,
                        placement.RotationY,
                        placement.Size,
                        material);
                }
            }
        }

        private void BuildPipes(int columns, int rows, Material pipeMaterial, Material supportMaterial)
        {
            var halfX = columns * 0.5f;
            var halfZ = rows * 0.5f;
            CreatePipe("Pipe_North_A", new Vector3(-halfX * 0.35f, 0.48f, halfZ + 3.1f), 0f, 7f, pipeMaterial);
            CreatePipe("Pipe_North_B", new Vector3(halfX * 0.38f, 0.65f, halfZ + 3.8f), 0f, 5.5f, pipeMaterial);
            CreatePipe("Pipe_West", new Vector3(-halfX - 3.4f, 0.42f, -halfZ * 0.2f), 90f, 8f, pipeMaterial);
            CreatePipe("Pipe_East", new Vector3(halfX + 3.5f, 0.55f, halfZ * 0.15f), 90f, 7f, pipeMaterial);

            for (var index = -1; index <= 1; index++)
            {
                CreateBlock(
                    $"PipeSupport_N_{index}",
                    new Vector3(index * 2.4f, 0.18f, halfZ + 3.1f),
                    Quaternion.identity,
                    new Vector3(0.18f, 0.36f, 0.55f),
                    supportMaterial);
            }
        }

        private void BuildStairs(int columns, int rows, Material stepMaterial, Material railMaterial)
        {
            var halfX = columns * 0.5f;
            var halfZ = rows * 0.5f;
            CreateStaircase("Stairs_SouthWest", new Vector3(-halfX - 2.6f, -0.12f, -halfZ - 2.5f), 0f, stepMaterial, railMaterial);
            CreateStaircase("Stairs_NorthEast", new Vector3(halfX + 2.6f, -0.12f, halfZ + 2.5f), 180f, stepMaterial, railMaterial);
        }

        private void BuildSkyline(int columns, int rows, Material buildingMaterial, Material towerMaterial)
        {
            var skylineRoot = new GameObject("Skyline").transform;
            skylineRoot.SetParent(_generatedRoot, false);
            skylineRoot.gameObject.layer = DecorationLayer;
            var random = new System.Random(randomSeed);
            var halfExtent = Mathf.Max(columns, rows) *
                             Mathf.Max(2f, environmentExtentMultiplier) * 0.5f;
            var safeHalfX = columns * 0.5f + 10f;
            var safeHalfZ = rows * 0.5f + 10f;
            for (var index = 0; index < Mathf.Clamp(skylineBlockCount, 16, 320); index++)
            {
                var position = Vector3.zero;
                for (var attempt = 0; attempt < 16; attempt++)
                {
                    position = new Vector3(
                        Mathf.Lerp(-halfExtent + 5f, halfExtent - 5f,
                            (float)random.NextDouble()),
                        0f,
                        Mathf.Lerp(-halfExtent + 5f, halfExtent - 5f,
                            (float)random.NextDouble()));
                    if (Mathf.Abs(position.x) > safeHalfX || Mathf.Abs(position.z) > safeHalfZ)
                    {
                        break;
                    }
                }
                var width = Mathf.Lerp(2.4f, 6.5f, (float)random.NextDouble());
                var depth = Mathf.Lerp(2.2f, 5.5f, (float)random.NextDouble());
                var height = Mathf.Lerp(5f, 18f, (float)random.NextDouble());
                CreateBlock(
                    $"Skyline_Block_{index:00}",
                    position + Vector3.up * (height * 0.5f - 0.3f),
                    Quaternion.Euler(0f, random.Next(0, 4) * 90f, 0f),
                    new Vector3(width, height, depth),
                    buildingMaterial,
                    skylineRoot);

                if (random.NextDouble() < 0.28)
                {
                    CreateCylinder(
                        $"Skyline_Tower_{index:00}",
                        position + new Vector3(width * 0.3f, height + 2.5f, depth * 0.25f),
                        Quaternion.identity,
                        new Vector3(0.22f, 5f, 0.22f),
                        towerMaterial,
                        skylineRoot);
                }
            }
        }

        private void BuildOptionalModels(int columns, int rows, Material material)
        {
            var halfX = columns * 0.5f;
            var halfZ = rows * 0.5f;
            var squareExtent = Mathf.Max(columns, rows) *
                               Mathf.Max(2f, environmentExtentMultiplier) * 0.5f;
            var extentX = squareExtent;
            var extentZ = squareExtent;
            var slots = new GameObject("SeededEnvironmentModelFill").transform;
            slots.SetParent(_generatedRoot, false);
            slots.gameObject.layer = DecorationLayer;

            CreateImported(signalBasePrefab, "ENV_SignalBase", new Vector3(-halfX - 4f, 0f, halfZ + 2f), 15f, new Vector3(3f, 4.5f, 3f), material, slots);
            CreateImported(serverTerminalPrefab, "ENV_ServerTerminal", new Vector3(halfX + 3.4f, 0f, halfZ + 1.5f), 180f, new Vector3(3f, 3.2f, 3f), material, slots);
            CreateImported(storageTankPrefab, "ENV_StorageTank", new Vector3(-halfX - 3.5f, 0f, -halfZ - 2f), 35f, new Vector3(3.2f, 3.5f, 3.2f), material, slots);
            CreateImported(cargoUnit01Prefab, "ENV_CargoUnit01", new Vector3(-halfX - 1f, 0f, -halfZ - 3f), 0f, new Vector3(4.2f, 2.8f, 3.5f), material, slots);
            CreateImported(cargoUnit02Prefab, "ENV_CargoUnit02", new Vector3(halfX + 3f, 0f, -halfZ - 2.5f), -25f, new Vector3(4.2f, 2.8f, 3.5f), material, slots);
            CreateImported(boardPartPrefab, "ENV_BoardPart", new Vector3(0f, 0f, halfZ + 3f), 0f, new Vector3(5f, 2.5f, 3f), material, slots);

            CreateImported(cargoUnit01Prefab, "ENV_Fill_Cargo_SW", new Vector3(-extentX * 0.62f, 0f, -extentZ * 0.58f), 18f, new Vector3(5.5f, 3.2f, 4.2f), material, slots);
            CreateImported(cargoUnit02Prefab, "ENV_Fill_Cargo_NE", new Vector3(extentX * 0.58f, 0f, extentZ * 0.62f), -12f, new Vector3(5.5f, 3.2f, 4.2f), material, slots);
            CreateImported(storageTankPrefab, "ENV_Fill_Tank_NW", new Vector3(-extentX * 0.68f, 0f, extentZ * 0.52f), 25f, new Vector3(4.5f, 5f, 4.5f), material, slots);
            CreateImported(storageTankPrefab, "ENV_Fill_Tank_SE", new Vector3(extentX * 0.66f, 0f, -extentZ * 0.54f), -20f, new Vector3(4.5f, 5f, 4.5f), material, slots);
            CreateImported(serverTerminalPrefab, "ENV_Fill_Server_W", new Vector3(-extentX * 0.72f, 0f, 0f), 90f, new Vector3(4f, 4f, 4f), material, slots);
            CreateImported(signalBasePrefab, "ENV_Fill_Signal_E", new Vector3(extentX * 0.72f, 0f, extentZ * 0.08f), -90f, new Vector3(4f, 6f, 4f), material, slots);
        }

        private void CreateProceduralRailing(string objectName, Vector3 position, float rotationY, float length, Material material)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(_generatedRoot, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            root.gameObject.layer = DecorationLayer;
            CreateBlock("RailTop", new Vector3(0f, 0.6f, 0f), Quaternion.identity, new Vector3(length, 0.1f, 0.1f), material, root);
            CreateBlock("RailPostL", new Vector3(-length * 0.45f, 0.28f, 0f), Quaternion.identity, new Vector3(0.12f, 0.65f, 0.12f), material, root);
            CreateBlock("RailPostR", new Vector3(length * 0.45f, 0.28f, 0f), Quaternion.identity, new Vector3(0.12f, 0.65f, 0.12f), material, root);
        }

        private void CreateStaircase(string objectName, Vector3 position, float rotationY, Material stepMaterial, Material railMaterial)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(_generatedRoot, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            root.gameObject.layer = DecorationLayer;
            for (var index = 0; index < 6; index++)
            {
                CreateBlock(
                    $"Step_{index}",
                    new Vector3(0f, index * 0.12f, index * 0.34f),
                    Quaternion.identity,
                    new Vector3(2.4f, 0.22f, 0.38f),
                    stepMaterial,
                    root);
            }
            CreateProceduralRailing("LeftRail", position + new Vector3(-1.3f, 0.2f, 0.9f), rotationY + 90f, 2.2f, railMaterial);
            CreateProceduralRailing("RightRail", position + new Vector3(1.3f, 0.2f, 0.9f), rotationY + 90f, 2.2f, railMaterial);
        }

        private void CreatePipe(string objectName, Vector3 position, float rotationY, float length, Material material)
        {
            var rotation = rotationY == 0f
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.Euler(90f, 0f, 0f);
            CreateCylinder(objectName, position, rotation, new Vector3(0.18f, length * 0.5f, 0.18f), material);
        }

        private void CreateImported(
            GameObject prefab,
            string objectName,
            Vector3 localPosition,
            float rotationY,
            Vector3 targetSize,
            Material material,
            Transform parent = null)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = Instantiate(prefab, parent != null ? parent : _generatedRoot, false);
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = Vector3.one;
            ConvertImportedRenderers(instance, material);
            FitImportedModel(instance, localPosition.y, targetSize);
            Sanitize(instance);
        }

        private void FitImportedModel(GameObject instance, float groundY, Vector3 targetSize)
        {
            if (!TryGetRendererBounds(instance, out var bounds))
            {
                return;
            }

            var source = bounds.size;
            var scale = Mathf.Min(
                targetSize.x / Mathf.Max(0.001f, source.x),
                targetSize.z / Mathf.Max(0.001f, source.z));
            scale = Mathf.Min(scale, targetSize.y / Mathf.Max(0.001f, source.y));
            instance.transform.localScale *= Mathf.Clamp(scale, 0.001f, 100f);
            if (TryGetRendererBounds(instance, out bounds))
            {
                var localBottom = instance.transform.parent.InverseTransformPoint(bounds.min).y;
                instance.transform.localPosition += Vector3.up * (groundY - localBottom);
            }
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            if (renderers.Length == 0)
            {
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return true;
        }

        private void CreateBlock(string objectName, Vector3 position, Quaternion rotation, Vector3 scale, Material material, Transform parent = null)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent != null ? parent : _generatedRoot, false);
            block.transform.localPosition = position;
            block.transform.localRotation = rotation;
            block.transform.localScale = scale;
            ApplyPrimitive(block, material);
        }

        private void CreateCylinder(string objectName, Vector3 position, Quaternion rotation, Vector3 scale, Material material, Transform parent = null)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.SetParent(parent != null ? parent : _generatedRoot, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localRotation = rotation;
            cylinder.transform.localScale = scale;
            ApplyPrimitive(cylinder, material);
        }

        private static void ApplyPrimitive(GameObject target, Material material)
        {
            target.layer = DecorationLayer;
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                DestroySafe(collider);
            }
            var renderer = target.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void ConvertImportedRenderers(GameObject target, Material fallback)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = ConvertImportedMaterials(renderer.sharedMaterials, fallback);
            }
        }

        private Material[] ConvertImportedMaterials(Material[] sourceMaterials, Material fallback)
        {
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                return new[] { fallback };
            }

            var converted = new Material[sourceMaterials.Length];
            for (var index = 0; index < sourceMaterials.Length; index++)
            {
                converted[index] = ConvertImportedMaterial(sourceMaterials[index], fallback);
            }
            return converted;
        }

        private Material ConvertImportedMaterial(Material source, Material fallback)
        {
            if (source == null || urpMaterialTemplate == null)
            {
                return fallback;
            }

            if (_convertedImportedMaterials.TryGetValue(source, out var cached))
            {
                return cached;
            }

            var converted = new Material(urpMaterialTemplate)
            {
                name = "CowImported_" + source.name
            };
            CopyColor(source, converted, "_BaseColor", "_Color", Color.white);
            CopyFloat(source, converted, "_Metallic");
            CopyFloat(source, converted, "_Smoothness", "_Glossiness");
            CopyFloat(source, converted, "_BumpScale");
            CopyFloat(source, converted, "_OcclusionStrength");
            CopyTexture(source, converted, "_BaseMap", "_MainTex");
            CopyTexture(source, converted, "_BumpMap");
            CopyTexture(source, converted, "_MetallicGlossMap");
            CopyTexture(source, converted, "_OcclusionMap");
            CopyTexture(source, converted, "_EmissionMap");
            CopyColor(source, converted, "_EmissionColor", "_EmissionColor", Color.black);

            if (converted.GetTexture("_BumpMap") != null)
            {
                converted.EnableKeyword("_NORMALMAP");
            }
            if (converted.GetTexture("_EmissionMap") != null ||
                converted.GetColor("_EmissionColor").maxColorComponent > 0.001f)
            {
                converted.EnableKeyword("_EMISSION");
                converted.globalIlluminationFlags = source.globalIlluminationFlags;
            }

            _convertedImportedMaterials.Add(source, converted);
            _runtimeMaterials.Add(converted);
            return converted;
        }

        private void RestoreRendererMaterialsFromSource(Transform target, Transform source)
        {
            var sourceRenderers = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                sourceRenderers[GetSiblingPath(renderer.transform, source)] = renderer;
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (sourceRenderers.TryGetValue(
                        GetSiblingPath(renderer.transform, target),
                        out var sourceRenderer))
                {
                    renderer.sharedMaterials = ConvertImportedMaterials(
                        sourceRenderer.sharedMaterials,
                        renderer.sharedMaterial);
                }
            }
        }

        private GameObject ResolveImportedSource(string objectName)
        {
            if (objectName.StartsWith("ENV_Railing_", StringComparison.Ordinal)) return railingPrefab;
            if (objectName.Contains("CargoUnit02", StringComparison.Ordinal) ||
                objectName.Contains("Fill_Cargo_NE", StringComparison.Ordinal)) return cargoUnit02Prefab;
            if (objectName.Contains("CargoUnit01", StringComparison.Ordinal) ||
                objectName.Contains("Fill_Cargo_SW", StringComparison.Ordinal)) return cargoUnit01Prefab;
            if (objectName.Contains("Signal", StringComparison.Ordinal)) return signalBasePrefab;
            if (objectName.Contains("Server", StringComparison.Ordinal)) return serverTerminalPrefab;
            if (objectName.Contains("Tank", StringComparison.Ordinal)) return storageTankPrefab;
            if (objectName.Contains("BoardPart", StringComparison.Ordinal)) return boardPartPrefab;
            return null;
        }

        private static string GetSiblingPath(Transform current, Transform root)
        {
            if (current == root)
            {
                return string.Empty;
            }

            var indices = new Stack<int>();
            while (current != null && current != root)
            {
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }
            return string.Join("/", indices);
        }

        private static void CopyColor(
            Material source,
            Material target,
            string targetProperty,
            string alternateSourceProperty,
            Color fallback)
        {
            if (!target.HasProperty(targetProperty)) return;
            var color = source.HasProperty(targetProperty)
                ? source.GetColor(targetProperty)
                : source.HasProperty(alternateSourceProperty)
                    ? source.GetColor(alternateSourceProperty)
                    : fallback;
            target.SetColor(targetProperty, color);
            if (targetProperty == "_BaseColor" && target.HasProperty("_Color"))
            {
                target.SetColor("_Color", color);
            }
        }

        private static void CopyFloat(
            Material source,
            Material target,
            string targetProperty,
            string alternateSourceProperty = null)
        {
            if (!target.HasProperty(targetProperty)) return;
            if (source.HasProperty(targetProperty))
            {
                target.SetFloat(targetProperty, source.GetFloat(targetProperty));
            }
            else if (!string.IsNullOrEmpty(alternateSourceProperty) &&
                     source.HasProperty(alternateSourceProperty))
            {
                target.SetFloat(targetProperty, source.GetFloat(alternateSourceProperty));
            }
        }

        private static void CopyTexture(
            Material source,
            Material target,
            string targetProperty,
            string alternateSourceProperty = null)
        {
            if (!target.HasProperty(targetProperty)) return;
            var sourceProperty = source.HasProperty(targetProperty)
                ? targetProperty
                : !string.IsNullOrEmpty(alternateSourceProperty) &&
                  source.HasProperty(alternateSourceProperty)
                    ? alternateSourceProperty
                    : null;
            if (sourceProperty == null) return;
            target.SetTexture(targetProperty, source.GetTexture(sourceProperty));
            target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
            target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
        }

        private void ReplaceGeneratedRoot()
        {
            var existing = transform.Find("GeneratedCowEnvironment");
            if (existing == null)
            {
                existing = transform.Find("BakedCowEnvironment");
            }
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                DestroySafe(existing.gameObject);
            }
            ReleaseMaterials();
            _generatedRoot = new GameObject("GeneratedCowEnvironment").transform;
            _generatedRoot.SetParent(transform, false);
            _generatedRoot.gameObject.layer = DecorationLayer;
        }

        private Material CreateMaterial(Color color, float smoothness)
        {
            if (urpMaterialTemplate == null)
            {
                Debug.LogError("Cow industrial environment requires an explicit URP material template.", this);
                return null;
            }

            var material = new Material(urpMaterialTemplate)
            {
                name = "CowEnvironment_Runtime"
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            _runtimeMaterials.Add(material);
            return material;
        }

        private static void Sanitize(GameObject target)
        {
            foreach (var child in target.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = DecorationLayer;
            }
            foreach (var collider in target.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private void ReleaseMaterials()
        {
            foreach (var material in _runtimeMaterials)
            {
                DestroySafe(material);
            }
            _runtimeMaterials.Clear();
            _convertedImportedMaterials.Clear();
        }

        private void OnDestroy()
        {
            ReleaseMaterials();
        }

        private int CountConfiguredModels()
        {
            var count = 0;
            if (railingPrefab != null) count++;
            if (signalBasePrefab != null) count++;
            if (serverTerminalPrefab != null) count++;
            if (storageTankPrefab != null) count++;
            if (cargoUnit01Prefab != null) count++;
            if (cargoUnit02Prefab != null) count++;
            if (boardPartPrefab != null) count++;
            return count;
        }

        private static void DestroySafe(UnityEngine.Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private readonly struct Placement
        {
            public Placement(Vector3 position, float rotationY, float size)
            {
                Position = position;
                RotationY = rotationY;
                Size = size;
            }

            public Vector3 Position { get; }
            public float RotationY { get; }
            public float Size { get; }
        }
    }
}
