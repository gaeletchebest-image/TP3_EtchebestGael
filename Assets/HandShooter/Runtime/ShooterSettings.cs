using UnityEngine;
using UnityEngine.InputSystem;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Prototype Settings", fileName = "ShooterSettings")]
    public sealed class ShooterSettings : ScriptableObject
    {
        [Header("Daño de los cuatro dedos")]
        [Min(0.01f)] public float baseDamage = 1f;
        [Min(1f)] public float additionalFingerMultiplier = 2f;
        [Tooltip("Segundos entre disparos con 0, 1, 2 y 3 dedos inactivos, respectivamente.")]
        public float[] cooldownByInactiveFingers = { 1f, 0.65f, 0.4f, 0.2f };
        [Header("Pulgar inactivo / proyectil (activo usa valores base)")]
        [Min(0.01f)] public float projectileDamageMultiplier = 2f;
        [Min(0.01f)] public float projectileCooldownMultiplier = 2f;
        [Tooltip("Descuento de combustible del proyectil respecto al rayo. 0.5 = consume 50 % menos para el mismo daño base.")]
        [Range(0f, 1f)] public float projectileFuelEfficiency = 0.5f;
        [Min(0.1f)] public float projectileSpeed = 12f;
        [Tooltip("Cuánto se reduce la velocidad por daño extra. 0 = sin reducción; 0.5 = la velocidad escala con la raíz inversa del daño relativo al daño base.")]
        [Min(0f)] public float projectileSpeedDamageExponent = 0.5f;
        [Min(0.01f)] public float projectileRadius = 0.09f;
        [Tooltip("Distancia máxima en metros antes de detonar.")]
        [Min(0.1f)] public float projectileRange = 30f;
        public HandProjectile projectilePrefab;
        [Header("Explosión del proyectil")]
        [Tooltip("Fracción del daño DIRECTO. 0.5 = 50 %. El blanco impactado recibe directo + explosión.")]
        [Range(0f, 1f)] public float explosionDamageFraction = 0.5f;
        [Tooltip("Radio en metros cuando el daño directo coincide con Reference Damage.")]
        [Min(0f)] public float explosionBaseRadius = 1.25f;
        [Min(0.01f)] public float explosionReferenceDamage = 2f;
        [Tooltip("Radio = Base Radius × (daño directo / Reference Damage) ^ Radius Exponent.")]
        [Range(0f, 1f)] public float explosionRadiusExponent = 0.5f;
        [Min(0f)] public float explosionMaxRadius = 4f;
        [Min(0.05f)] public float explosionVisualDuration = 0.35f;
        public Color explosionColor = new Color(1f, 0.55f, 0.15f, 1f);
        [Header("Disparo")]
        [Tooltip("Alcance del rayo en metros.")]
        [Min(1f)] public float range = 100f;
        public LayerMask hitMask = ~4;
        public bool automaticFire = true;
        [Min(0.01f)] public float beamDuration = 0.07f;
        public Color rayColor = new Color(0.2f, 1f, 0.85f);
        public Color projectileColor = new Color(1f, 0.5f, 0.15f);
        public Material beamMaterial;
        [Header("Configuración inicial")]
        [Range(1, 4)] public int initialActiveFingers = 1;
        public bool initialThumbActive = true;
        [Header("Movimiento y cámara")]
        [Min(0.1f)] public float moveSpeed = 6f;
        [Min(0.1f)] public float dashSpeed = 18f;
        [Min(0.01f)] public float dashDuration = 0.2f;
        [Min(0f)] public float dashCooldown = 1f;
        public Key dashKey = Key.LeftShift;
        [Min(0.001f)] public float mouseSensitivity = 0.12f;
        [Range(30f, 89f)] public float pitchLimit = 85f;
        [Range(40f, 110f)] public float fieldOfView = 75f;
        public float gravity = -22f;
        public Key forwardKey = Key.W;
        public Key backwardKey = Key.S;
        public Key leftKey = Key.A;
        public Key rightKey = Key.D;
        public Key menuKey = Key.Tab;
        [Header("Blancos de prueba")]
        [Min(0.01f)] public float targetHealth = 32f;
        [Min(0.1f)] public float targetRespawnDelay = 2f;

        public ShotStats Evaluate(HandConfiguration hand)
        {
            int inactive = 4 - hand.ActiveCount;
            float interval = cooldownByInactiveFingers != null && cooldownByInactiveFingers.Length > inactive
                ? cooldownByInactiveFingers[inactive] : 1f / (inactive + 1);
            float damage = Mathf.Max(0.01f, baseDamage) * Mathf.Pow(Mathf.Max(1f, additionalFingerMultiplier), hand.ActiveCount - 1);
            if (!hand.ThumbActive)
            {
                damage *= Mathf.Max(0.01f, projectileDamageMultiplier);
                interval *= Mathf.Max(0.01f, projectileCooldownMultiplier);
            }
            return new ShotStats(damage, Mathf.Max(0.02f, interval), hand.ThumbActive);
        }
        public float EvaluateFuelCost(ShotStats shot, float fuelPerDamage)
        {
            float costDamage = shot.Damage;
            if (!shot.Hitscan)
            {
                costDamage /= Mathf.Max(0.01f, projectileDamageMultiplier);
                costDamage *= 1f - Mathf.Clamp01(projectileFuelEfficiency);
            }
            return costDamage * Mathf.Max(0.01f, fuelPerDamage);
        }
        public float EvaluateProjectileSpeed(float shotDamage)
        {
            float damageRatio = Mathf.Max(1f, shotDamage / Mathf.Max(0.01f, baseDamage));
            float slowdown = Mathf.Pow(damageRatio, Mathf.Max(0f, projectileSpeedDamageExponent));
            return Mathf.Max(0.1f, projectileSpeed) / slowdown;
        }
        public ExplosionStats EvaluateExplosion(float directDamage)
        {
            float damage = Mathf.Max(0f, directDamage) * Mathf.Clamp01(explosionDamageFraction);
            float radius = Mathf.Max(0f, explosionBaseRadius) * Mathf.Pow(
                Mathf.Max(0f, directDamage) / Mathf.Max(0.01f, explosionReferenceDamage),
                Mathf.Clamp01(explosionRadiusExponent));
            return new ExplosionStats(damage, Mathf.Min(Mathf.Max(0f, explosionMaxRadius), radius));
        }
        private void OnValidate()
        {
            if (cooldownByInactiveFingers == null || cooldownByInactiveFingers.Length != 4)
                System.Array.Resize(ref cooldownByInactiveFingers, 4);
            for (int i = 0; i < 4; i++)
                cooldownByInactiveFingers[i] = Mathf.Clamp(cooldownByInactiveFingers[i], 0.02f, i == 0 ? float.MaxValue : cooldownByInactiveFingers[i - 1]);
            initialActiveFingers = Mathf.Clamp(initialActiveFingers, 1, 4);
            gravity = Mathf.Min(-0.1f, gravity);
        }
    }
    public readonly struct ExplosionStats
    {
        public readonly float Damage;
        public readonly float Radius;
        public ExplosionStats(float damage, float radius) { Damage = damage; Radius = radius; }
    }
    public readonly struct ShotStats
    {
        public readonly float Damage;
        public readonly float Interval;
        public readonly bool Hitscan;
        public ShotStats(float damage, float interval, bool hitscan)
        { Damage = damage; Interval = interval; Hitscan = hitscan; }
    }
}

