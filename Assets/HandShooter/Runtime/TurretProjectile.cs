using UnityEngine;

namespace HandShooter
{
    public sealed class TurretProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed, radius, life;
        private int mask;
        private Transform owner;
        public void Initialize(Vector3 direction, TurretSettings settings, Transform owner)
        {
            this.direction = direction.normalized;
            this.owner = owner;
            speed = Mathf.Max(0.1f, settings.projectileSpeed);
            radius = Mathf.Max(0.01f, settings.projectileRadius);
            life = Mathf.Max(0.1f, settings.projectileLifetime);
            // The prototype player uses Unity's Ignore Raycast layer for its first-person visuals.
            // Include that layer so the CharacterController can still receive enemy hits.
            mask = settings.hitMask.value | (1 << 2);
        }
        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            float distance = speed * Mathf.Min(Time.deltaTime, life);
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, direction, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (owner && hit.transform.IsChildOf(owner)) continue;
                var player = hit.collider.GetComponentInParent<PrototypePlayer>();
                if (player) player.Die("Torreta");
                Destroy(gameObject);
                return;
            }
            transform.position += direction * distance;
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
