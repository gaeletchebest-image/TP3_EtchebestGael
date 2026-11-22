using System.Collections.Generic;
using UnityEngine;

namespace HandShooter
{
    [RequireComponent(typeof(DamageTarget))]
    public sealed class KamikazeEnemy : MonoBehaviour
    {
        public KamikazeSettings settings;
        public PrototypePlayer target;

        private DamageTarget damageTarget;
        private bool hasAcquiredTarget;
        private bool hasDetonated;
        internal bool HasAcquiredTarget { get => hasAcquiredTarget; set => hasAcquiredTarget = value; }
        internal bool HasDetonated => hasDetonated;

        private void Awake()
        {
            damageTarget = GetComponent<DamageTarget>();
        }

        private void OnEnable() => damageTarget.Damaged += OnDamaged;

        private void OnDamaged(float amount)
        {
            if (!settings || hasDetonated || damageTarget.Health <= 0f) return;
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
            if (!hasAcquiredTarget) ShooterAudio.Play(SoundCue.EnemyAlert, transform.position);
            hasAcquiredTarget = true;
        }

        private void Start()
        {
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || !settings || !target || target.IsDead || hasDetonated || damageTarget.Health <= 0f) return;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (!hasAcquiredTarget)
            {
                if (distance > settings.detectionRange || !CanSeeTarget(distance)) return;
                hasAcquiredTarget = true;
                ShooterAudio.Play(SoundCue.EnemyAlert, transform.position);
            }

            ShooterAudio.Loop(transform, SoundCue.KamikazeChase, true, 0.8f,
                Mathf.Lerp(1.7f, 0.8f, Mathf.Clamp01(distance / Mathf.Max(1f, settings.detectionRange))));
            ShooterAudio.Threat();
            if (distance <= settings.detonationRange)
            {
                Detonate();
                return;
            }

            Vector3 direction = toTarget / distance;
            transform.position += direction * Mathf.Max(0f, settings.moveSpeed) * Time.deltaTime;
            if (direction.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void OnDisable()
        {
            damageTarget.Damaged -= OnDamaged;
            ShooterAudio.Loop(transform, SoundCue.KamikazeChase, false);
        }

        private bool CanSeeTarget(float distance)
        {
            if (distance <= 0.001f) return true;
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            Vector3 destination = target.transform.position + Vector3.up * 0.8f;
            Vector3 direction = destination - origin;
            float sightDistance = direction.magnitude;
            int mask = settings.hitMask.value | (1 << 2);
            return Physics.Raycast(origin, direction / sightDistance, out RaycastHit hit, sightDistance, mask,
                QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PrototypePlayer>() == target;
        }

        public void Detonate()
        {
            if (hasDetonated || !settings || !damageTarget || damageTarget.Health <= 0f) return;
            hasDetonated = true;
            ShooterAudio.Loop(transform, SoundCue.KamikazeChase, false);
            ShooterAudio.Play(SoundCue.KamikazeExplosion, transform.position);

            Vector3 origin = transform.position;
            float radius = Mathf.Max(0f, settings.blastRadius);
            int mask = settings.hitMask.value | (1 << 2);
            var damagedTargets = new HashSet<DamageTarget>();
            var players = new HashSet<PrototypePlayer>();

            if (radius > 0f)
            {
                foreach (var candidate in Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Ignore))
                {
                    var player = candidate.GetComponentInParent<PrototypePlayer>();
                    var damageable = candidate.GetComponentInParent<DamageTarget>();
                    if (player && !players.Contains(player) && HasLineOfSight(origin, candidate, mask))
                        players.Add(player);
                    else if (damageable && damageable != damageTarget && !damagedTargets.Contains(damageable) &&
                        HasLineOfSight(origin, candidate, mask))
                        damagedTargets.Add(damageable);
                }
            }

            ExplosionEffect.Spawn(origin, radius, settings.explosionVisualDuration,
                settings.explosionMaterial, settings.explosionColor);
            foreach (var player in players) player.Die("Kamikaze", transform);
            foreach (var damageable in damagedTargets) damageable.TakeDamage(settings.blastDamage);

            damageTarget.enabled = false;
            damageTarget.DestroyEnemy();
        }

        private static bool HasLineOfSight(Vector3 origin, Collider targetCollider, int mask)
        {
            Vector3 closestPoint = targetCollider.ClosestPoint(origin);
            Vector3 offset = closestPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f) return true;

            foreach (var hit in Physics.RaycastAll(origin, offset / distance, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == targetCollider || hit.collider.GetComponentInParent<DamageTarget>() ||
                    hit.collider.GetComponentInParent<PrototypePlayer>()) continue;
                return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = new Color(1f, 0.3f, 0.08f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, settings.detectionRange));
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, settings.blastRadius));
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, settings.detonationRange));
        }
    }
}
