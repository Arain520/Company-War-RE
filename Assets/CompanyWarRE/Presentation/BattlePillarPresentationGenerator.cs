using System.Collections.Generic;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Builds a pillar instance for every logical control block and owns their lookup table.
    /// </summary>
    public sealed class BattlePillarPresentationGenerator : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, BattlePillarView> _pillars =
            new Dictionary<GridPosition, BattlePillarView>();

        private Material _bodyMaterial;
        private Material _topMaterial;

        public IReadOnlyDictionary<GridPosition, BattlePillarView> Pillars => _pillars;

        public void Build(
            BattleBoardCoordinateMapper mapper,
            Color bodyColor,
            Color topColor,
            float topThickness,
            GameObject pillarModel = null)
        {
            if (mapper == null)
            {
                return;
            }

            foreach (var pillar in _pillars.Values)
            {
                if (pillar == null) continue;
                pillar.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(pillar.gameObject);
                else DestroyImmediate(pillar.gameObject);
            }
            ReleaseMaterials();
            _pillars.Clear();
            _bodyMaterial = CreateBodyMaterial(bodyColor);
            _topMaterial = CreateMaterial(topColor, 0.32f);

            for (var column = 1; column <= mapper.ControlBlockColumns; column++)
            {
                for (var row = 1; row <= mapper.ControlBlockRows; row++)
                {
                    var blockPosition = new GridPosition(column, row);
                    var pillarObject = new GameObject($"Pillar_{column}_{row}");
                    pillarObject.transform.SetParent(transform, false);
                    pillarObject.transform.localPosition =
                        mapper.GetControlBlockCenterLocalPosition(blockPosition);
                    var view = pillarObject.AddComponent<BattlePillarView>();
                    view.Initialize(
                        blockPosition,
                        mapper.PillarWidth,
                        mapper.GetPillarHeight(blockPosition),
                        _bodyMaterial,
                        _topMaterial,
                        topThickness,
                        pillarModel);
                    _pillars.Add(blockPosition, view);
                }
            }
        }

        public void ConfigureCloudAbyss(
            float pillarBottomY,
            float worldFogTopY,
            float worldFogBottomY,
            Color fogColor,
            float fogStrength,
            float fadeCurve)
        {
            foreach (var pillar in _pillars.Values)
            {
                pillar.ExtendBodyToBottom(pillarBottomY);
            }

            if (_bodyMaterial == null)
            {
                return;
            }

            SetFloat(_bodyMaterial, "_FogTopY", worldFogTopY);
            SetFloat(_bodyMaterial, "_FogBottomY", worldFogBottomY);
            SetColor(_bodyMaterial, "_FogColor", fogColor);
            SetFloat(_bodyMaterial, "_FogStrength", Mathf.Clamp01(fogStrength));
            SetFloat(_bodyMaterial, "_FadeCurve", Mathf.Max(0.25f, fadeCurve));
        }

        public bool TryGetPillar(GridPosition controlBlockPosition, out BattlePillarView pillar)
        {
            return _pillars.TryGetValue(controlBlockPosition, out pillar);
        }

        public bool TryGetPillarForCell(
            GridPosition cellPosition,
            BattleBoardCoordinateMapper mapper,
            out BattlePillarView pillar)
        {
            pillar = null;
            return mapper != null &&
                   TryGetPillar(mapper.GetControlBlockForCell(cellPosition), out pillar);
        }

        private static Material CreateMaterial(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            return material;
        }

        private static Material CreateBodyMaterial(Color color)
        {
            var shader = Shader.Find("CompanyWarRE/PillarHeightFogURP");
            if (shader == null)
            {
                return CreateMaterial(color, 0.18f);
            }

            var material = new Material(shader)
            {
                name = "MAT_RuntimePillarHeightFog"
            };
            SetColor(material, "_BaseColor", color);
            return material;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private void OnDestroy()
        {
            ReleaseMaterials();
        }

        private void ReleaseMaterials()
        {
            ReleaseMaterial(_bodyMaterial);
            ReleaseMaterial(_topMaterial);
            _bodyMaterial = null;
            _topMaterial = null;
        }

        private static void ReleaseMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
