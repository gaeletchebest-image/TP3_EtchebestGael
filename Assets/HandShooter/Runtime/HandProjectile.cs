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
            initialized = true;
            // Sweeps alone cannot detect overlaps at the spawn position.
            var overlaps = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Ignore);
            if (overlaps.Length > 0)
            {
                Vector3 point = overlaps[0].ClosestPoint(transform.position);
                Vector3 normal = (transform.position - point).normalized;
                Detonate(overlaps[0], point, normal == Vector3.zero ? -direction : normal);
            }
        }
        private void Update() { if (initialized) Simulate(Time.deltaTime); }
        public void Simulate(float deltaTime)
        {
            if (!initialized || deltaTime <= 0f) return;
            float distance = Mathf.Min(speed * deltaTime, remainingRange, speed * Mathf.Max(0, remainingLife));
            if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
            {
                transform.position += direction * hit.distance;
                Detonate(hit.collider, hit.point, hit.normal);
                return;
            }
            transform.position += direction * distance;
            remainingRange -= distance;
            remainingLife -= deltaTime;
            if (remainingLife <= 0f || remainingRange <= 0f) { initialized = false; Destroy(gameObject); }
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

