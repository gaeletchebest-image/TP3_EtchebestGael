using UnityEngine;

namespace HandShooter
{
    public sealed class TurretProjectile : MonoBehaviour
    {
        private Vector3 direction, launchPosition;
        private float speed, radius, remainingRange;
        private int mask;
        private Transform owner;
        private ProjectileRangeWarning rangeWarning;
        private Material explosionMaterial;
        private Color explosionColor;
        private bool initialized;

        public void Initialize(Vector3 direction, TurretSettings settings, Transform owner)
        {
            this.direction = direction.normalized;
            launchPosition = transform.position;
            this.owner = owner;
            speed = Mathf.Max(0.1f, settings.projectileSpeed);
            radius = Mathf.Max(0.01f, settings.projectileRadius);
            remainingRange = Mathf.Max(0.1f, settings.projectileRange);
            explosionMaterial = settings.explosionMaterial;
            explosionColor = settings.projectileColor;
            rangeWarning = new ProjectileRangeWarning(GetComponent<Renderer>(), settings.projectileColor, remainingRange);
            // The prototype player uses Unity's Ignore Raycast layer for its first-person visuals.
            // Include that layer so the CharacterController can still receive enemy hits.
            mask = settings.hitMask.value | (1 << 2);
            initialized = true;
            ShooterAudio.Loop(transform, SoundCue.ProjectileFlight, true);
        }

        private void Update()
        {
            if (!initialized || Time.deltaTime <= 0f) return;
            float distance = Mathf.Min(speed * Time.deltaTime, remainingRange);
            Physics.SyncTransforms();
            RaycastHit nearest = default;
            float hitDistance = float.PositiveInfinity;
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, direction, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (owner && hit.transform.IsChildOf(owner)) continue;
                if (hit.distance >= hitDistance) continue;
                nearest = hit;
                hitDistance = hit.distance;
            }
            if (nearest.collider)
            {
                var player = nearest.collider.GetComponentInParent<PrototypePlayer>();
                if (player) player.Die("Torreta", owner, owner ? owner.position : launchPosition);
                // Overlapping casts can report an empty contact point.
                Vector3 origin = hitDistance > 0f ? nearest.point + nearest.normal * 0.015f : transform.position;
                Detonate(origin);
                return;
            }
            transform.position += direction * distance;
            remainingRange -= distance;
            if (remainingRange <= 0f) Detonate(transform.position);
            else rangeWarning.Update(remainingRange);
        }

        private void OnDisable() => ShooterAudio.Loop(transform, SoundCue.ProjectileFlight, false);

        private void Detonate(Vector3 position)
        {
            initialized = false;
            ShooterAudio.Loop(transform, SoundCue.ProjectileFlight, false);
            ShooterAudio.Play(SoundCue.EnemyImpact, position);
            ExplosionEffect.Spawn(position, radius * 3f, 0.25f, explosionMaterial, explosionColor);
            Destroy(gameObject);
        }
    }
}
