using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Rebuilds the procedural industrial board surround used by Cow's formal Battle scene.
    /// It is Presentation-only and deliberately owns no Domain state or interaction colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CowBoardVisualRenderer : MonoBehaviour
    {
        private const int DecorationLayer = 2;
        private const string MaterialResourcePath =
            "CompanyWarRE/Maps/MAT_BaseFormalMapGround";

        [SerializeField] private CowBoardVisualTheme theme;
        [SerializeField] private Material urpMaterialTemplate;

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private Transform _visualRoot;

        public void Build(int columns, int rows)
        {
            var safeColumns = Mathf.Max(1, columns);
            var safeRows = Mathf.Max(1, rows);
            EnsureRoot();
            ClearVisuals();
            ReleaseMaterials();

            var source = theme;
            var decorRing = source != null ? source.DecorRing : 2;
            var platformThickness = source != null ? source.PlatformThickness : 0.35f;
            var plateThickness = source != null ? source.BattlePlateThickness : 0.065f;
            var wallHeight = source != null ? source.OuterWallHeight : 0.95f;
            var wallThickness = source != null ? source.OuterWallThickness : 0.55f;

            var platform = CreateMaterial(
                source != null ? source.PlatformColor : new Color(0.48f, 0.50f, 0.50f),
                Color.black,
                0.32f);
            var tileA = CreateMaterial(
                source != null ? source.TileColorA : new Color(0.62f, 0.63f, 0.62f),
                Color.black,
                0.32f);
            var tileB = CreateMaterial(
                source != null ? source.TileColorB : new Color(0.54f, 0.55f, 0.54f),
                Color.black,
                0.26f);
            var blue = source != null ? source.BlueEmission : new Color(0.04f, 0.34f, 0.72f);
            var plate = CreateMaterial(
                source != null ? source.BattlePlateColor : new Color(0.035f, 0.055f, 0.075f),
                blue * 0.55f,
                0.36f);
            var border = CreateMaterial(
                source != null ? source.BorderColor : new Color(0.07f, 0.08f, 0.08f),
                blue * 0.15f,
                0.25f);
            var warningColor = source != null
                ? source.WarningColor
                : new Color(0.86f, 0.66f, 0.06f);
            var warningEmission = source != null
                ? source.WarningEmission
                : new Color(0.85f, 0.55f, 0.02f);
            var warning = CreateMaterial(warningColor, warningEmission * 0.22f, 0.18f);
            var wall = CreateMaterial(
                source != null ? source.WallColor : new Color(0.38f, 0.39f, 0.39f),
                Color.black,
                0.22f);

            BuildIndustrialBase(
                safeColumns,
                safeRows,
                decorRing,
                platformThickness,
                plateThickness,
                wallHeight,
                wallThickness,
                platform,
                tileA,
                tileB,
                plate,
                border,
                warning,
                wall);
        }

        private void BuildIndustrialBase(
            int columns,
            int rows,
            int decorRing,
            float platformThickness,
            float plateThickness,
            float wallHeight,
            float wallThickness,
            Material platform,
            Material tileA,
            Material tileB,
            Material plate,
            Material border,
            Material warning,
            Material wall)
        {
            var visualColumns = columns + decorRing * 2;
            var visualRows = rows + decorRing * 2;
            var platformWidth = visualColumns;
            var platformLength = visualRows;

            CreateBlock(
                "IndustrialPlatformBase",
                new Vector3(0f, -0.26f, 0f),
                new Vector3(platformWidth + 0.9f, platformThickness, platformLength + 0.9f),
                platform);
            CreateBlock(
                "BattleZonePlate",
                new Vector3(0f, -0.095f, 0f),
                new Vector3(columns + 0.28f, plateThickness, rows + 0.28f),
                plate);

            var startX = -(visualColumns - 1f) * 0.5f;
            var startZ = -(visualRows - 1f) * 0.5f;
            var battleHalfX = columns * 0.5f;
            var battleHalfZ = rows * 0.5f;
            for (var x = 0; x < visualColumns; x++)
            {
                for (var z = 0; z < visualRows; z++)
                {
                    var offsetX = startX + x;
                    var offsetZ = startZ + z;
                    if (Mathf.Abs(offsetX) < battleHalfX &&
                        Mathf.Abs(offsetZ) < battleHalfZ)
                    {
                        continue;
                    }

                    CreateBlock(
                        $"IndustrialTile_{x}_{z}",
                        new Vector3(offsetX, -0.055f, offsetZ),
                        new Vector3(0.96f, 0.035f, 0.96f),
                        ((x + z) & 1) == 0 ? tileA : tileB);
                }
            }

            BuildBattleBorder(columns, rows, border, warning);

            var topZ = platformLength * 0.5f + 0.2f;
            var sideX = platformWidth * 0.5f + 0.2f;
            CreateBlock(
                "OuterWallTop",
                new Vector3(0f, 0.32f, topZ),
                new Vector3(platformWidth + 1f, wallHeight, wallThickness),
                wall);
            CreateBlock(
                "OuterWallLeft",
                new Vector3(-sideX, 0.32f, 0f),
                new Vector3(wallThickness, wallHeight * 0.86f, platformLength + 1f),
                wall);
            CreateBlock(
                "OuterWallRight",
                new Vector3(sideX, 0.32f, 0f),
                new Vector3(wallThickness, wallHeight * 0.86f, platformLength + 1f),
                wall);
        }

        private void BuildBattleBorder(
            int columns,
            int rows,
            Material border,
            Material warning)
        {
            const float borderY = 0.045f;
            var halfX = columns * 0.5f;
            var halfZ = rows * 0.5f;
            CreateBlock("BattleBorderTop", new Vector3(0f, borderY, halfZ + 0.11f),
                new Vector3(columns + 0.38f, 0.055f, 0.1f), border);
            CreateBlock("BattleBorderBottom", new Vector3(0f, borderY, -halfZ - 0.11f),
                new Vector3(columns + 0.38f, 0.055f, 0.1f), border);
            CreateBlock("BattleBorderLeft", new Vector3(-halfX - 0.11f, borderY, 0f),
                new Vector3(0.1f, 0.055f, rows + 0.38f), border);
            CreateBlock("BattleBorderRight", new Vector3(halfX + 0.11f, borderY, 0f),
                new Vector3(0.1f, 0.055f, rows + 0.38f), border);

            CreateWarningSegments("WarnTop", new Vector3(0f, borderY + 0.035f, halfZ + 0.22f), columns, true, warning);
            CreateWarningSegments("WarnBottom", new Vector3(0f, borderY + 0.035f, -halfZ - 0.22f), columns, true, warning);
            CreateWarningSegments("WarnLeft", new Vector3(-halfX - 0.22f, borderY + 0.035f, 0f), rows, false, warning);
            CreateWarningSegments("WarnRight", new Vector3(halfX + 0.22f, borderY + 0.035f, 0f), rows, false, warning);
        }

        private void CreateWarningSegments(
            string prefix,
            Vector3 center,
            float span,
            bool horizontal,
            Material material)
        {
            var count = Mathf.Max(2, Mathf.FloorToInt(span / 0.9f));
            var step = span / count;
            for (var index = 0; index < count; index += 2)
            {
                var offset = -span * 0.5f + step * (index + 0.5f);
                var position = center + (horizontal
                    ? new Vector3(offset, 0f, 0f)
                    : new Vector3(0f, 0f, offset));
                var scale = horizontal
                    ? new Vector3(step * 0.72f, 0.025f, 0.035f)
                    : new Vector3(0.035f, 0.025f, step * 0.72f);
                CreateBlock($"{prefix}_{index}", position, scale, material);
            }
        }

        private void EnsureRoot()
        {
            _visualRoot = transform.Find("CowBoardDecoration");
            if (_visualRoot != null)
            {
                return;
            }

            _visualRoot = new GameObject("CowBoardDecoration").transform;
            _visualRoot.SetParent(transform, false);
            _visualRoot.SetAsFirstSibling();
            _visualRoot.gameObject.layer = DecorationLayer;
        }

        private void CreateBlock(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.layer = DecorationLayer;
            block.transform.SetParent(_visualRoot, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            var collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                DestroySafe(collider);
            }

            var renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private Material CreateMaterial(Color baseColor, Color emissionColor, float smoothness)
        {
            var template = urpMaterialTemplate != null
                ? urpMaterialTemplate
                : Resources.Load<Material>(MaterialResourcePath);
            if (template == null)
            {
                Debug.LogError(
                    $"Cow board URP material template is missing at Resources/{MaterialResourcePath}.",
                    this);
                return null;
            }

            var material = new Material(template)
            {
                name = $"CowBoard_{baseColor}_Runtime"
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
                if (emissionColor.maxColorComponent > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                }
            }

            _runtimeMaterials.Add(material);
            return material;
        }

        private void ClearVisuals()
        {
            for (var index = _visualRoot.childCount - 1; index >= 0; index--)
            {
                DestroySafe(_visualRoot.GetChild(index).gameObject);
            }
        }

        private void ReleaseMaterials()
        {
            for (var index = 0; index < _runtimeMaterials.Count; index++)
            {
                if (_runtimeMaterials[index] != null)
                {
                    DestroySafe(_runtimeMaterials[index]);
                }
            }
            _runtimeMaterials.Clear();
        }

        private void OnDestroy()
        {
            ReleaseMaterials();
        }

        private static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
