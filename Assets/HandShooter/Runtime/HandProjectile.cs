using System.Collections.Generic;
using UnityEngine;

namespace HandShooter
{
    public sealed class HandProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float damage, speed, radius, remainingLife, remainingRange;
        private int hitMask;
        private HandWeapon owner;
        private int shotId;
        private bool initialized;
        private ExplosionStats explosion;
        private Material effectMaterial;
        private Color effectColor;
        private float effectDuration;
        private sealed class TargetMotion
        {
            public Vector3 Position;
        }
        private readonly Dictionary<Collider, TargetMotion> targetMotion = new Dictionary<Collider, TargetMotion>();

        private struct Impact
        {
            public Collider Collider;
            public Vector3 Point, Normal;
            public float Fraction;
        }

        public void Initialize(Vector3 flightDirection, float shotDamage, ShooterSettings settings, HandWeapon weapon, int sourceShotId = -1)
        {
            shotId = sourceShotId;
            direction = flightDirection.normalized;
            damage = shotDamage;
            speed = settings.EvaluateProjectileSpeed(shotDamage);
            radius = Mathf.Max(0.01f, settings.projectileRadius);
            remainingLife = settings.projectileLifetime;
            remainingRange = settings.range;
            hitMask = settings.hitMask.value & ~4;
            owner = weapon;
            // Snapshot the shot: changing the hand or settings does not change a projectile already in flight.
            explosion = settings.EvaluateExplosion(shotDamage);
            effectMaterial = settings.beamMaterial;
            effectColor = settings.explosionColor;
            effectDuration = settings.explosionVisualDuration;
            transform.localScale = Vector3.one * radius * 2f;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", settings.projectileColor);
            GetComponent<Renderer>().SetPropertyBlock(properties);
            Physics.SyncTransforms();
            foreach (var target in FindObjectsByType<DamageTarget>(FindObjectsSortMode.None))
                foreach (var collider in target.GetComponentsInChildren<Collider>())
                    if (!collider.isTrigger && (hitMask & (1 << collider.gameObject.layer)) != 0)
                        targetMotion[collider] = new TargetMotion { Position = collider.transform.position };
            initialized = true;
            if (FindImpact(Vector3.zero, out var impact)) Detonate(impact.Collider, impact.Point, impact.Normal);
        }
        private void Update() { if (initialized) Simulate(Time.deltaTime); }
        public void Simulate(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;
            float distance = Mathf.Min(speed * deltaTime, remainingRange, speed * Mathf.Max(0, remainingLife));
            // NavMesh agents move outside physics steps. Queries must see their latest collider poses.
            Physics.SyncTransforms();
            if (FindImpact(direction * distance, out var hit))
            {
                transform.position += direction * (distance * hit.Fraction);
                Detonate(hit.Collider, hit.Point, hit.Normal);
                return;
            }
            transform.position += direction * distance;
            remainingRange -= distance;
            remainingLife -= deltaTime;
            if (remainingLife <= 0f || remainingRange <= 0f) { initialized = false; Destroy(gameObject); }
        }

        private bool FindImpact(Vector3 displacement, out Impact impact)
        {
            impact = new Impact { Fraction = float.PositiveInfinity };
            Vector3 start = transform.position;
            // Sphere casts omit colliders already overlapping their starting sphere.
            foreach (var collider in Physics.OverlapSphere(start, radius, hitMask, QueryTriggerInteraction.Ignore))
                if (!HasMoved(collider)) ConsiderOverlap(collider, start, ref impact);
            float distance = displacement.magnitude;
            if (distance > 0f)
                foreach (var hit in Physics.SphereCastAll(start, radius, displacement / distance, distance,
                    hitMask, QueryTriggerInteraction.Ignore))
                    if (!HasMoved(hit.collider)) ConsiderHit(hit, distance, ref impact);

            foreach (var entry in targetMotion)
            {
                Collider collider = entry.Key;
                if (!collider) continue;
                Vector3 position = collider.transform.position;
                Vector3 movement = position - entry.Value.Position;
                entry.Value.Position = position;
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger ||
                    (hitMask & (1 << collider.gameObject.layer)) == 0 || movement.sqrMagnitude < 0.00000001f) continue;

                // Freeze the target at its current pose and sweep the projectile's relative motion.
                // This also catches an enemy crossing completely through the shot between frames.
                Vector3 relativeStart = start + movement;
                Vector3 relativeDisplacement = displacement - movement;
                Vector3 closestPoint = collider.ClosestPoint(relativeStart);
                if ((closestPoint - relativeStart).sqrMagnitude <= radius * radius)
                {
                    ConsiderOverlap(collider, relativeStart, ref impact);
                    continue;
                }
                float relativeDistance = relativeDisplacement.magnitude;
                if (relativeDistance <= 0f) continue;
                foreach (var hit in Physics.SphereCastAll(relativeStart, radius, relativeDisplacement / relativeDistance,
                    relativeDistance, hitMask, QueryTriggerInteraction.Ignore))
                    if (hit.collider == collider) ConsiderHit(hit, relativeDistance, ref impact);
            }
            return impact.Collider;
        }

        private bool HasMoved(Collider collider) => targetMotion.TryGetValue(collider, out var motion) &&
            (collider.transform.position - motion.Position).sqrMagnitude >= 0.00000001f;

        private void ConsiderOverlap(Collider collider, Vector3 center, ref Impact impact)
        {
            if (impact.Fraction <= 0f) return;
            Vector3 point = collider.ClosestPoint(center);
            Vector3 normal = center - point;
            impact = new Impact { Collider = collider, Point = point,
                Normal = normal.sqrMagnitude > 0f ? normal.normalized : -direction, Fraction = 0f };
        }

        private static void ConsiderHit(RaycastHit hit, float distance, ref Impact impact)
        {
            float fraction = hit.distance / distance;
            if (fraction >= impact.Fraction) return;
            impact = new Impact { Collider = hit.collider, Point = hit.point, Normal = hit.normal, Fraction = fraction };
        }
        public void Detonate(Collider collider, Vector3 point, Vector3 normal)
        {
            if (!initialized) return;
            initialized = false;
            // Keep the origin just outside the struck surface so cover blocks the blast correctly.
            Vector3 origin = point + normal * 0.015f;
            var recipients = new HashSet<DamageTarget>();
            if (explosion.Damage > 0f && explosion.Radius > 0f)
            {
                foreach (var candidate in Physics.OverlapSphere(origin, explosion.Radius, hitMask, QueryTriggerInteraction.Ignore))
                {
                    var target = candidate.GetComponentInParent<DamageTarget>();
                    if (!target || recipients.Contains(target)) continue;
                    Vector3 toTarget = candidate.ClosestPoint(origin) - origin;
                    bool blocked = false;
                    // Bodies do not absorb the blast for their neighbours; only scenery provides cover.
                    foreach (var cover in Physics.RaycastAll(origin, toTarget.normalized, toTarget.magnitude, hitMask, QueryTriggerInteraction.Ignore))
                        if (!cover.collider.GetComponentInParent<DamageTarget>()) { blocked = true; break; }
                    if (!blocked) recipients.Add(target);
                }
                ExplosionEffect.Spawn(origin, explosion.Radius, effectDuration, effectMaterial, effectColor);
            }
            var directTarget = collider.GetComponentInParent<DamageTarget>();
            // Combine direct and splash into one damage event for a useful total in the hit indicator.
            bool directReceivesSplash = directTarget && recipients.Remove(directTarget);
            foreach (var target in recipients) ApplyDamage(target, explosion.Damage);
            if (directTarget) ApplyDamage(directTarget, damage + (directReceivesSplash ? explosion.Damage : 0f));
            Destroy(gameObject);
        }
        private void ApplyDamage(DamageTarget target, float amount)
        {
            if (owner) owner.Damage(target, amount, shotId);
            else target.TakePlayerShotDamage(amount);
        }
    }
}

