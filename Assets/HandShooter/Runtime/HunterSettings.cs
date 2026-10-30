using UnityEngine;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Hunter Settings", fileName = "HunterSettings")]
    public sealed class HunterSettings : ScriptableObject
    {
        [Header("Vida y detección")]
        [Min(0.01f)] public float initialHealth = 24f;
        [Header("Defensa")]
        [Tooltip("Cada punto divide por 2 el daño recibido. Cero desactiva la defensa.")]
        [Min(0)] public int initialDefensePoints;
        [Tooltip("Disparos del jugador necesarios para perder un punto de defensa.")]
        [Min(1)] public int defenseBarCapacity = 3;
        [Header("Detección")]
        [Tooltip("Único alcance horizontal para ver y disparar. Requiere línea de visión.")]
        [Min(0.1f)] public float range = 5f;

        [Header("Patrulla sin detectar al jugador")]
        [Tooltip("Radio alrededor del centro de patrulla. Cero mantiene al hunter quieto.")]
        [Min(0f)] public float patrolRadius = 6f;
        [Min(0.1f)] public float patrolSpeed = 2f;
        [Tooltip("Espera mínima al llegar a cada destino, en segundos.")]
        [Min(0f)] public float patrolWaitMin = 1f;
        [Tooltip("Espera máxima al llegar a cada destino, en segundos.")]
        [Min(0f)] public float patrolWaitMax = 3f;

        [Header("Persecución al recibir daño")]
        [Min(0.1f)] public float chaseSpeed = 12f;
        [Min(0.1f)] public float acceleration = 60f;
        [Min(0.02f)] public float repathInterval = 0.1f;

        [Header("Rayo instantáneo (sin carga)")]
        [Min(0.02f)] public float beamDuration = 0.18f;
        [Min(0.01f)] public float beamWidth = 0.16f;
        public Color beamColor = new Color(1f, 0.9f, 0.35f, 1f);
        public Material beamMaterial;
        public LayerMask hitMask = ~0;

        [Header("Estado visual")]
        public Color normalColor = new Color(0.15f, 0.9f, 0.8f, 1f);
        public Color alertColor = new Color(1f, 0.12f, 0.05f, 1f);

        private void OnValidate()
        {
            patrolRadius = Mathf.Max(0f, patrolRadius);
            patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
            patrolWaitMin = Mathf.Max(0f, patrolWaitMin);
            patrolWaitMax = Mathf.Max(patrolWaitMin, patrolWaitMax);
        }
    }
}
