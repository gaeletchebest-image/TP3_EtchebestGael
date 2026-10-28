using UnityEngine;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Sniper Settings", fileName = "SniperSettings")]
    public sealed class SniperSettings : ScriptableObject
    {
        [Header("Vida y movimiento")]
        [Min(0.01f)] public float initialHealth = 20f;
        [Min(0f)] public float detectionRange = 26f;
        [Min(0.1f)] public float moveSpeed = 2f;
        [Min(0.1f)] public float repathInterval = 0.5f;
        [Min(0.05f)] public float positioningTolerance = 0.65f;

        [Header("Rango de ataque (distancia horizontal)")]
        [Min(0f)] public float minimumAttackRange = 7f;
        [Min(0.1f)] public float maximumAttackRange = 19f;
        public float PreferredRange => (minimumAttackRange + maximumAttackRange) * 0.5f;

        [Header("Preparación y disparo")]
        [Min(0.01f)] public float settleDuration = 0.4f;
        [Min(0.1f)] public float chargeDuration = 1.8f;
        [Tooltip("Tiempo final con dirección fija: permite esquivar el rayo.")]
        [Min(0.01f)] public float aimLockDuration = 0.35f;
        [Min(0.1f)] public float shotCooldown = 1.8f;
        [Min(0.02f)] public float beamDuration = 0.18f;
        [Min(0.005f)] public float warningWidth = 0.035f;
        [Min(0.01f)] public float beamWidth = 0.16f;
        public Color warningColor = new Color(1f, 0.2f, 0.12f, 1f);
        public Color lockedColor = new Color(1f, 0.9f, 0.35f, 1f);
        public Material beamMaterial;
        public LayerMask hitMask = ~0;

        private void OnValidate()
        {
            minimumAttackRange = Mathf.Max(0f, minimumAttackRange);
            maximumAttackRange = Mathf.Max(minimumAttackRange + 0.5f, maximumAttackRange);
            aimLockDuration = Mathf.Clamp(aimLockDuration, 0.01f, Mathf.Max(0.1f, chargeDuration));
        }
    }
}
