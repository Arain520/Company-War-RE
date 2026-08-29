using System;
using System.Collections.Generic;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    /// <summary>
    /// Presentation-only adapter for Cow combat visuals. The legacy serialized field names are
    /// intentionally retained so an editor migration can transfer VisualMapping data losslessly.
    /// </summary>
    public sealed class BattleSliceVisualCatalog : MonoBehaviour
    {
        [SerializeField] private List<UnitVisualMapping> unitMappings = new List<UnitVisualMapping>();
        [SerializeField] private List<EnemyVisualMapping> enemyMappings = new List<EnemyVisualMapping>();

        public bool TryResolve(string templateId, out ResolvedVisual resolved)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                resolved = default;
                return false;
            }

            foreach (var mapping in unitMappings)
            {
                if (mapping != null && string.Equals(
                        mapping.unitId,
                        templateId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return TryResolve(
                        mapping.prefab,
                        mapping.model,
                        mapping.material,
                        mapping.localPosition,
                        mapping.localEulerAngles,
                        mapping.localScale,
                        out resolved);
                }
            }

            foreach (var mapping in enemyMappings)
            {
                if (mapping != null && string.Equals(
                        mapping.enemyId,
                        templateId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return TryResolve(
                        mapping.prefab,
                        mapping.model,
                        mapping.material,
                        mapping.localPosition,
                        mapping.localEulerAngles,
                        mapping.localScale,
                        out resolved);
                }
            }

            if (TryResolveMigratedCowPrefab(templateId, out var prefab))
            {
                resolved = new ResolvedVisual(
                    prefab,
                    null,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                return true;
            }

            resolved = default;
            return false;
        }

        private static bool TryResolveMigratedCowPrefab(string templateId, out GameObject prefab)
        {
            prefab = null;
            if (templateId.Length != 3 ||
                !int.TryParse(templateId.Substring(1), out var number))
            {
                return false;
            }

            string relativePath;
            if (char.ToUpperInvariant(templateId[0]) == 'U')
            {
                if (number < 1 || number > 36)
                {
                    return false;
                }

                if (number >= 25)
                {
                    var modelName = number == 31 ? "u31fbx" : $"u{number:00}";
                    prefab = Resources.Load<GameObject>($"CowLegacy/_Game/Resources/{modelName}");
                    return prefab != null;
                }

                var folder = (number >= 8 && number <= 11) || number == 21
                    ? "Buildings"
                    : "Units";
                relativePath = $"CowLegacy/_Game/Art/Prefabs/{folder}/PF_U{number:00}";
            }
            else if (char.ToUpperInvariant(templateId[0]) == 'E')
            {
                if (number < 1 || number > 15)
                {
                    return false;
                }

                var folder = number == 6 || number == 7 || number >= 12
                    ? "Buildings"
                    : "Enemies";
                relativePath = $"CowLegacy/_Game/Art/Prefabs/{folder}/PF_E{number:00}";
            }
            else
            {
                return false;
            }

            prefab = Resources.Load<GameObject>(relativePath);
            return prefab != null;
        }

        private static bool TryResolve(
            GameObject prefab,
            GameObject model,
            Material material,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            out ResolvedVisual resolved)
        {
            var source = prefab != null ? prefab : model;
            if (source == null)
            {
                resolved = default;
                return false;
            }

            resolved = new ResolvedVisual(
                source,
                material,
                localPosition,
                Quaternion.Euler(localEulerAngles),
                NormalizeScale(localScale));
            return true;
        }

        private static Vector3 NormalizeScale(Vector3 scale)
        {
            return Mathf.Approximately(scale.sqrMagnitude, 0f) ? Vector3.one : scale;
        }

        [Serializable]
        private sealed class UnitVisualMapping
        {
            // Names match CompanyWar.Configs.UnitVisualMapping for serialized transfer.
            public string unitId = string.Empty;
            public GameObject prefab = null;
            public Material material = null;
            public GameObject model = null;
            public Vector3 localPosition = Vector3.zero;
            public Vector3 localEulerAngles = Vector3.zero;
            public Vector3 localScale = Vector3.one;
        }

        [Serializable]
        private sealed class EnemyVisualMapping
        {
            // Names match CompanyWar.Configs.EnemyVisualMapping for serialized transfer.
            public string enemyId = string.Empty;
            public GameObject prefab = null;
            public Material material = null;
            public GameObject model = null;
            public Vector3 localPosition = Vector3.zero;
            public Vector3 localEulerAngles = Vector3.zero;
            public Vector3 localScale = Vector3.one;
        }

        public readonly struct ResolvedVisual
        {
            public ResolvedVisual(
                GameObject prefab,
                Material materialOverride,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Prefab = prefab;
                MaterialOverride = materialOverride;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            public GameObject Prefab { get; }
            public Material MaterialOverride { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }
    }
}
