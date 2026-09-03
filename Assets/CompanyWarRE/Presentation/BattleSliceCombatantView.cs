using System;
using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceCombatantView : MonoBehaviour
    {
        public const float MovingUnitVisualFootprint = 0.95f;
        public const float BuildingVisualFootprint = 2.65f;

        private static readonly Color AllyColor = new Color(0.12f, 0.75f, 0.9f);
        private static readonly Color EnemyColor = new Color(0.9f, 0.2f, 0.18f);
        private static readonly Color EnemyBuildingColor = new Color(0.7f, 0.12f, 0.45f);
        private static readonly Color CurseColor = new Color(0.72f, 0.16f, 0.92f);
        private static readonly Color HealColor = new Color(0.3f, 1f, 0.45f);
        private static readonly Color ExecutionColor = new Color(1f, 0.15f, 0.75f);
        private static readonly Color StealthColor = new Color(0.38f, 0.48f, 1f);
        private static readonly Color ConversionColor = new Color(0.15f, 1f, 0.85f);
        private static readonly Color PushbackColor = new Color(1f, 0.55f, 0.12f);
        private static readonly Color HeavyStrikeColor = new Color(1f, 0.82f, 0.15f);
        private static readonly Color DamageColor = new Color(1f, 0.8f, 0.25f);
        private static readonly Color HealthColor = new Color(0.25f, 0.95f, 0.35f);
        private static readonly Color HealthBackgroundColor = new Color(0.08f, 0.08f, 0.08f);

        private Material _bodyMaterial;
        private Material _healthMaterial;
        private Material _healthBackgroundMaterial;
        private Material _abilityMaterial;
        private Transform _unitBody;
        private Transform _buildingBody;
        private Transform _healthFill;
        private Transform _healthBackground;
        private Transform _abilityIndicator;
        private Transform _importedBody;
        private Renderer[] _importedRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock _importedPropertyBlock;
        private BattleSliceVisualCatalog _visualCatalog;
        private string _resolvedTemplateId;
        private Quaternion _importedBaseRotation = Quaternion.identity;
        private Vector3 _importedIntendedAnchor = Vector3.zero;
        private Team _importedFacingTeam;
        private bool _hasImportedFacingTeam;
        private float _importedVerticalGroundOffset;
        private float _importedVisualTop;
        private TextMesh _statusLabel;
        private double _previousHitPoints = double.NaN;
        private float _healPulseRemaining;
        private float _damagePulseRemaining;
        private bool _curseDamage;
        private Vector3 _targetLocalPosition;
        private float _uniformMovementSpeed;
        private bool _hasPresentationPosition;
        private bool _isBuildingPresentation;
        private float _visualScale = 1f;

        public string ActorId { get; private set; }

        public void Initialize(string actorId, BattleSliceVisualCatalog visualCatalog = null)
        {
            ActorId = actorId;
            _visualCatalog = visualCatalog;
            _previousHitPoints = double.NaN;
            _healPulseRemaining = 0f;
            _damagePulseRemaining = 0f;
            _curseDamage = false;
            _targetLocalPosition = Vector3.zero;
            _uniformMovementSpeed = 0f;
            _hasPresentationPosition = false;
            _isBuildingPresentation = false;
            _visualScale = 1f;
            if (_unitBody != null)
            {
                return;
            }

            _bodyMaterial = CreateMaterial(Color.white);

            var unitBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitBody.name = "UnitBody";
            unitBody.transform.SetParent(transform, false);
            DisableCollider(unitBody);
            unitBody.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            _unitBody = unitBody.transform;

            var buildingBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buildingBody.name = "BuildingBody";
            buildingBody.transform.SetParent(transform, false);
            DisableCollider(buildingBody);
            buildingBody.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;
            _buildingBody = buildingBody.transform;

            var healthBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healthBackground.name = "HealthBackground";
            healthBackground.transform.SetParent(transform, false);
            DisableCollider(healthBackground);
            _healthBackground = healthBackground.transform;
            _healthBackgroundMaterial = CreateMaterial(HealthBackgroundColor);
            healthBackground.GetComponent<Renderer>().sharedMaterial = _healthBackgroundMaterial;

            var health = GameObject.CreatePrimitive(PrimitiveType.Cube);
            health.name = "HealthFill";
            health.transform.SetParent(transform, false);
            DisableCollider(health);
            _healthFill = health.transform;
            _healthMaterial = CreateMaterial(HealthColor);
            health.GetComponent<Renderer>().sharedMaterial = _healthMaterial;

            var ability = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ability.name = "AbilityIndicator";
            ability.transform.SetParent(transform, false);
            DisableCollider(ability);
            _abilityIndicator = ability.transform;
            _abilityMaterial = CreateMaterial(Color.clear);
            ability.GetComponent<Renderer>().sharedMaterial = _abilityMaterial;

            var labelObject = new GameObject("StatusLabel");
            labelObject.transform.SetParent(transform, false);
            _statusLabel = labelObject.AddComponent<TextMesh>();
            _statusLabel.anchor = TextAnchor.MiddleCenter;
            _statusLabel.alignment = TextAlignment.Center;
            _statusLabel.fontSize = 38;
            _statusLabel.characterSize = 0.065f;
            _statusLabel.color = Color.white;
        }

        public void ConfigureUniformMovement(float worldUnitsPerSecond)
        {
            _uniformMovementSpeed = Mathf.Max(0f, worldUnitsPerSecond);
        }

        public void ConfigureVisualScale(float scale)
        {
            _visualScale = Mathf.Max(0.01f, scale);
            transform.localScale = Vector3.one * _visualScale;
        }

        public void Render(BattleSliceCombatantSnapshot snapshot, bool curseDamage = false)
        {
            Render(
                snapshot,
                BattleSliceController.GetCombatantWorldPosition(snapshot),
                curseDamage);
        }

        public void Render(
            BattleSliceCombatantSnapshot snapshot,
            Vector3 localPosition,
            bool curseDamage = false)
        {
            if (snapshot == null || snapshot.ActorId != ActorId)
            {
                return;
            }

            if (!double.IsNaN(_previousHitPoints))
            {
                if (snapshot.HitPoints > _previousHitPoints)
                {
                    _healPulseRemaining = 0.75f;
                }
                else if (snapshot.HitPoints < _previousHitPoints)
                {
                    _damagePulseRemaining = 0.5f;
                    _curseDamage = curseDamage;
                }
            }

            _previousHitPoints = snapshot.HitPoints;
            gameObject.SetActive(snapshot.IsAlive);
            if (!snapshot.IsAlive)
            {
                return;
            }

            var hasImportedBody = EnsureImportedBody(snapshot.TemplateId, snapshot.IsBuilding);
            if (hasImportedBody &&
                (!_hasImportedFacingTeam || _importedFacingTeam != snapshot.Team))
            {
                _importedBody.localRotation = GetTeamFacingRotation(
                    _importedBaseRotation,
                    snapshot.Team);
                AlignImportedBodyToAnchor(snapshot.IsBuilding);
                _importedFacingTeam = snapshot.Team;
                _hasImportedFacingTeam = true;
            }
            if (hasImportedBody && !snapshot.IsBuilding)
            {
                localPosition.y += _importedVerticalGroundOffset;
            }
            _isBuildingPresentation = snapshot.IsBuilding;
            _targetLocalPosition = localPosition;
            if (!_hasPresentationPosition || snapshot.IsBuilding || _uniformMovementSpeed <= 0f)
            {
                transform.localPosition = localPosition;
                _hasPresentationPosition = true;
            }
            _unitBody.gameObject.SetActive(!hasImportedBody && !snapshot.IsBuilding);
            _buildingBody.gameObject.SetActive(!hasImportedBody && snapshot.IsBuilding);

            var ratio = snapshot.MaximumHitPoints <= 0d
                ? 0f
                : Mathf.Clamp01((float)(snapshot.HitPoints / snapshot.MaximumHitPoints));
            var barWidth = snapshot.IsBuilding ? 2.5f : 0.76f;
            var barHeight = hasImportedBody
                ? _importedVisualTop + 0.18f
                : snapshot.IsBuilding ? 2.05f : 1.62f;
            _healthBackground.localScale = new Vector3(barWidth + 0.12f, 0.11f, 0.12f);
            _healthBackground.localPosition = new Vector3(0f, barHeight, 0f);
            _healthFill.localScale = new Vector3(barWidth * ratio, 0.075f, 0.08f);
            _healthFill.localPosition = new Vector3(-barWidth * (1f - ratio) * 0.5f, barHeight, -0.03f);

            if (snapshot.IsBuilding)
            {
                _buildingBody.localPosition = new Vector3(0f, 0.72f, 0f);
                _buildingBody.localScale = new Vector3(2.65f, 1.3f, 2.65f);
            }
            else
            {
                _unitBody.localPosition = new Vector3(0f, 0.75f, 0f);
                _unitBody.localScale = new Vector3(0.42f, 0.55f, 0.42f);
            }

            var baseColor = snapshot.Team == Team.Ally ? AllyColor : EnemyColor;
            if (snapshot.IsBuilding)
            {
                baseColor = GetBuildingColor(snapshot.TemplateId);
            }

            _healPulseRemaining = Mathf.Max(0f, _healPulseRemaining - Time.unscaledDeltaTime);
            _damagePulseRemaining = Mathf.Max(0f, _damagePulseRemaining - Time.unscaledDeltaTime);
            var feedbackColor = _healPulseRemaining > 0f
                ? HealColor
                : _damagePulseRemaining > 0f
                    ? (_curseDamage ? CurseColor : DamageColor)
                    : snapshot.IsStealth
                        ? Color.Lerp(
                            baseColor,
                            StealthColor,
                            0.42f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.12f)
                        : baseColor;
            _bodyMaterial.color = feedbackColor;
            RenderImportedFeedback(
                snapshot.IsStealth || _healPulseRemaining > 0f || _damagePulseRemaining > 0f,
                feedbackColor);

            RenderAbilityIndicator(snapshot);
            RenderStatusLabel(snapshot);
        }

        private void RenderAbilityIndicator(BattleSliceCombatantSnapshot snapshot)
        {
            var abilityColor = Color.clear;
            if (string.Equals(snapshot.TemplateId, "E13", StringComparison.OrdinalIgnoreCase))
            {
                abilityColor = CurseColor;
            }
            else if (string.Equals(snapshot.TemplateId, "E14", StringComparison.OrdinalIgnoreCase))
            {
                abilityColor = HealColor;
            }
            else if (string.Equals(snapshot.TemplateId, "E15", StringComparison.OrdinalIgnoreCase))
            {
                abilityColor = ExecutionColor;
            }
            else if (snapshot.IsStealth)
            {
                abilityColor = StealthColor;
            }
            else if (snapshot.HasHealingAction)
            {
                abilityColor = HealColor;
            }
            else if (snapshot.HasConversionAction)
            {
                abilityColor = ConversionColor;
            }
            else if (snapshot.HasAuthorityPushback)
            {
                abilityColor = PushbackColor;
            }
            else if (snapshot.HasHeavyStrike)
            {
                abilityColor = HeavyStrikeColor;
            }

            var hasAbilityIndicator = abilityColor.a > 0f;
            _abilityIndicator.gameObject.SetActive(hasAbilityIndicator);
            if (!hasAbilityIndicator)
            {
                return;
            }

            var pulse = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.08f;
            _abilityIndicator.localPosition = new Vector3(0f, 0.16f, 0f);
            _abilityIndicator.localScale = new Vector3(1.55f * pulse, 0.025f, 1.55f * pulse);
            _abilityMaterial.color = abilityColor * (0.72f + Mathf.Sin(Time.unscaledTime * 3f) * 0.18f);
        }

        private void RenderStatusLabel(BattleSliceCombatantSnapshot snapshot)
        {
            var ability = string.Empty;
            if (string.Equals(snapshot.TemplateId, "E13", StringComparison.OrdinalIgnoreCase))
            {
                ability = "  CURSE";
            }
            else if (string.Equals(snapshot.TemplateId, "E14", StringComparison.OrdinalIgnoreCase))
            {
                ability = "  KILL HEAL";
            }
            else if (string.Equals(snapshot.TemplateId, "E15", StringComparison.OrdinalIgnoreCase))
            {
                var executeRemaining = Mathf.Max(0f, 12f - (float)snapshot.AttackProgress);
                ability = $"  EXEC {executeRemaining:0.0}s";
            }
            else if (snapshot.IsStealth)
            {
                ability = "  STEALTH";
            }
            else if (snapshot.HasHealingAction)
            {
                ability = "  HEAL";
            }
            else if (snapshot.HasConversionAction)
            {
                ability = "  CONVERT";
            }
            else if (snapshot.HasAuthorityPushback)
            {
                ability = "  PUSH";
            }
            else if (snapshot.HasHeavyStrike)
            {
                ability = "  HEAVY";
            }

            _statusLabel.text =
                $"{snapshot.TemplateId}  {snapshot.HitPoints:0.#}/{snapshot.MaximumHitPoints:0.#}{ability}";
            var labelHeight = _importedBody != null
                ? _importedVisualTop + 0.48f
                : snapshot.IsBuilding ? 2.42f : 1.92f;
            _statusLabel.transform.localPosition = new Vector3(0f, labelHeight, 0f);
        }

        private static Color GetBuildingColor(string templateId)
        {
            if (string.Equals(templateId, "E13", StringComparison.OrdinalIgnoreCase))
            {
                return CurseColor;
            }

            if (string.Equals(templateId, "E14", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.82f, 0.42f, 0.12f);
            }

            return string.Equals(templateId, "E15", StringComparison.OrdinalIgnoreCase)
                ? ExecutionColor
                : EnemyBuildingColor;
        }

        private bool EnsureImportedBody(string templateId, bool isBuilding)
        {
            if (_importedBody != null && string.Equals(
                    _resolvedTemplateId,
                    templateId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_visualCatalog == null || !_visualCatalog.TryResolve(templateId, out var resolved))
            {
                return false;
            }

            if (_importedBody != null)
            {
                Destroy(_importedBody.gameObject);
            }

            var instance = Instantiate(resolved.Prefab, transform, false);
            instance.name = "ImportedVisual_" + templateId;
            instance.transform.localPosition = resolved.LocalPosition;
            instance.transform.localRotation = resolved.LocalRotation;
            instance.transform.localScale = resolved.LocalScale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
            }
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }

            _importedBody = instance.transform;
            _importedBaseRotation = resolved.LocalRotation;
            _importedIntendedAnchor = resolved.LocalPosition;
            _hasImportedFacingTeam = false;
            _importedRenderers = instance.GetComponentsInChildren<Renderer>(true);
            if (resolved.MaterialOverride != null)
            {
                foreach (var renderer in _importedRenderers)
                {
                    var materials = renderer.sharedMaterials;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        materials[index] = resolved.MaterialOverride;
                    }
                    renderer.sharedMaterials = materials;
                }
            }

            FitImportedBodyToFootprint(isBuilding);

            _resolvedTemplateId = templateId;
            return true;
        }

        private void FitImportedBodyToFootprint(bool isBuilding)
        {
            if (_importedBody == null || _importedRenderers.Length == 0 ||
                !TryCalculateRendererBounds(_importedRenderers, out var bounds))
            {
                return;
            }

            var scale = CalculateFootprintScale(bounds.size, isBuilding) * _visualScale;
            _importedBody.localScale *= scale;

            AlignImportedBodyToAnchor(isBuilding);
        }

        private void AlignImportedBodyToAnchor(bool isBuilding)
        {
            if (_importedBody == null || _importedRenderers.Length == 0 ||
                !TryCalculateRendererBounds(_importedRenderers, out var bounds))
            {
                return;
            }

            var anchorInWorldSpace = isBuilding
                ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
                : bounds.center;
            var boundsAnchorInViewSpace = transform.InverseTransformPoint(anchorInWorldSpace);
            _importedBody.localPosition = CalculateAnchoredPosition(
                _importedBody.localPosition,
                _importedIntendedAnchor,
                boundsAnchorInViewSpace);

            if (!TryCalculateRendererBounds(_importedRenderers, out bounds))
            {
                return;
            }

            var bottomInViewSpace = transform.InverseTransformPoint(new Vector3(
                bounds.center.x,
                bounds.min.y,
                bounds.center.z));
            var topInViewSpace = transform.InverseTransformPoint(new Vector3(
                bounds.center.x,
                bounds.max.y,
                bounds.center.z));
            _importedVerticalGroundOffset = isBuilding ? 0f : -bottomInViewSpace.y;
            _importedVisualTop = topInViewSpace.y;
        }

        public static Vector3 CalculateAnchoredPosition(
            Vector3 currentLocalPosition,
            Vector3 intendedAnchor,
            Vector3 currentBoundsAnchorInViewSpace)
        {
            return currentLocalPosition + intendedAnchor - currentBoundsAnchorInViewSpace;
        }

        public static float CalculateFootprintScale(Vector3 rendererBoundsSize, bool isBuilding)
        {
            var horizontalSize = Mathf.Max(
                Mathf.Abs(rendererBoundsSize.x),
                Mathf.Abs(rendererBoundsSize.z));
            if (horizontalSize <= 0.0001f)
            {
                return 1f;
            }

            var targetSize = isBuilding ? BuildingVisualFootprint : MovingUnitVisualFootprint;
            return targetSize / horizontalSize;
        }

        public static Quaternion GetTeamFacingRotation(Quaternion baseRotation, Team team)
        {
            return baseRotation * Quaternion.Euler(0f, team == Team.Enemy ? 180f : 0f, 0f);
        }

        private static bool TryCalculateRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private void RenderImportedFeedback(bool active, Color color)
        {
            if (_importedRenderers.Length == 0)
            {
                return;
            }

            if (_importedPropertyBlock == null)
            {
                _importedPropertyBlock = new MaterialPropertyBlock();
            }
            _importedPropertyBlock.Clear();
            if (active)
            {
                _importedPropertyBlock.SetColor("_BaseColor", color);
                _importedPropertyBlock.SetColor("_Color", color);
            }

            foreach (var renderer in _importedRenderers)
            {
                renderer.SetPropertyBlock(_importedPropertyBlock);
            }
        }

        private static void DisableCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            return new Material(shader) { color = color };
        }

        private void LateUpdate()
        {
            if (_hasPresentationPosition &&
                !_isBuildingPresentation &&
                _uniformMovementSpeed > 0f)
            {
                transform.localPosition = AdvanceUniformPosition(
                    transform.localPosition,
                    _targetLocalPosition,
                    _uniformMovementSpeed,
                    Time.deltaTime);
            }

            var camera = Camera.main;
            if (_statusLabel != null && camera != null)
            {
                _statusLabel.transform.rotation = Quaternion.LookRotation(
                    _statusLabel.transform.position - camera.transform.position);
            }
        }

        public static Vector3 AdvanceUniformPosition(
            Vector3 current,
            Vector3 target,
            float worldUnitsPerSecond,
            float deltaSeconds)
        {
            return Vector3.MoveTowards(
                current,
                target,
                Mathf.Max(0f, worldUnitsPerSecond) * Mathf.Max(0f, deltaSeconds));
        }

        private void OnDestroy()
        {
            DestroyMaterial(_bodyMaterial);
            DestroyMaterial(_healthMaterial);
            DestroyMaterial(_healthBackgroundMaterial);
            DestroyMaterial(_abilityMaterial);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
