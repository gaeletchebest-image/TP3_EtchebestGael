using UnityEngine;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Kamikaze Settings", fileName = "KamikazeSettings")]
    public sealed class KamikazeSettings : ScriptableObject
    {
        [Min(0.01f)] public float initialHealth = 18f;
        [Header("Defensa")]
        [Tooltip("Cada punto divide por 2 el daño recibido. Cero desactiva la defensa.")]
        [Min(0)] public int initialDefensePoints;
        [Tooltip("Disparos del jugador necesarios para perder un punto de defensa.")]
        [Min(1)] public int defenseBarCapacity = 3;
        [Header("Movimiento y explosión")]
        [Min(0f)] public float detectionRange = 18f;
        [Min(0.1f)] public float moveSpeed = 5f;
        [Min(0.1f)] public float detonationRange = 1.8f;
        [Min(0f)] public float blastRadius = 4f;
        [Min(0f)] public float blastDamage = 32f;
        [Min(0.05f)] public float explosionVisualDuration = 0.45f;
        public Color explosionColor = new Color(1f, 0.28f, 0.06f, 1f);
        public Material explosionMaterial;
        public LayerMask hitMask = ~4;
    }
}
