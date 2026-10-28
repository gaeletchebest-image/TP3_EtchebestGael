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
            mask = settings.hitMask.value & ~4;
        }
        private void Update()
        {
            float distance = speed * Mathf.Min(Time.deltaTime, life);
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, direction, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (owner && hit.transform.IsChildOf(owner)) continue;
                // Enemy shots collide but intentionally do not apply damage yet.
                Destroy(gameObject);
                return;
            }
            transform.position += direction * distance;
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
