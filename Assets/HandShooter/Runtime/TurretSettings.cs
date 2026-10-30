using UnityEngine;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Turret Settings", fileName = "TurretSettings")]
    public sealed class TurretSettings : ScriptableObject
    {
        [Min(0.01f)] public float initialHealth = 24f;
        [Header("Defensa")]
        [Tooltip("Cada punto divide por 2 el daño recibido. Cero desactiva la defensa.")]
        [Min(0)] public int initialDefensePoints;
        [Tooltip("Disparos del jugador necesarios para perder un punto de defensa.")]
        [Min(1)] public int defenseBarCapacity = 3;
        [Header("Ataque")]
        [Min(0f)] public float detectionRange = 15f;
        [Min(0.02f)] public float fireInterval = 1f;
        [Min(0.1f)] public float projectileSpeed = 8f;
        [Min(0.01f)] public float projectileRadius = 0.12f;
        [Min(0.1f)] public float projectileLifetime = 5f;
        public Color projectileColor = Color.red;
        public LayerMask hitMask = ~4;
    }
}
