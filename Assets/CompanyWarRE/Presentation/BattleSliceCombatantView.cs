using CompanyWarRE.Application;
using CompanyWarRE.Domain;
using UnityEngine;

namespace CompanyWarRE.Presentation
{
    public sealed class BattleSliceCombatantView : MonoBehaviour
    {
        private static readonly Color AllyColor = new Color(0.12f, 0.75f, 0.9f);
        private static readonly Color EnemyColor = new Color(0.9f, 0.2f, 0.18f);
        private static readonly Color EnemyBuildingColor = new Color(0.7f, 0.12f, 0.45f);
        private static readonly Color HealthColor = new Color(0.25f, 0.95f, 0.35f);
        private Material _bodyMaterial;
        private Material _healthMaterial;
        private Transform _healthFill;

        public string ActorId { get; private set; }

        public void Initialize(string actorId)
        {
            ActorId = actorId;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.55f, 0.42f);
            _bodyMaterial = CreateMaterial(Color.white);
            body.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;

            var health = GameObject.CreatePrimitive(PrimitiveType.Cube);
            health.name = "Health";
            health.transform.SetParent(transform, false);
            health.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            health.transform.localScale = new Vector3(0.7f, 0.08f, 0.08f);
            _healthFill = health.transform;
            _healthMaterial = CreateMaterial(HealthColor);
            health.GetComponent<Renderer>().sharedMaterial = _healthMaterial;
        }

        public void Render(BattleSliceCombatantSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ActorId != ActorId)
            {
                return;
            }

            gameObject.SetActive(snapshot.IsAlive);
            if (!snapshot.IsAlive)
            {
                return;
            }

            transform.localPosition = new Vector3(
                snapshot.Column - 1,
                0.12f,
                (float)snapshot.LanePosition - 1f);
            _bodyMaterial.color = snapshot.Team == Team.Ally ? AllyColor : EnemyColor;
            if (snapshot.IsBuilding)
            {
                _bodyMaterial.color = EnemyBuildingColor;
                transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
            }
            else
            {
                transform.localScale = Vector3.one;
            }
            var ratio = snapshot.MaximumHitPoints <= 0d
                ? 0f
                : Mathf.Clamp01((float)(snapshot.HitPoints / snapshot.MaximumHitPoints));
            _healthFill.localScale = new Vector3(0.7f * ratio, 0.08f, 0.08f);
            _healthFill.localPosition = new Vector3(-0.35f * (1f - ratio), 1.55f, 0f);
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default");
            return new Material(shader) { color = color };
        }

        private void OnDestroy()
        {
            if (_bodyMaterial != null)
            {
                Destroy(_bodyMaterial);
            }

            if (_healthMaterial != null)
            {
                Destroy(_healthMaterial);
            }
        }
    }
}
