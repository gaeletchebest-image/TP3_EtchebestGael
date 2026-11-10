using UnityEngine;

namespace HandShooter
{
    [CreateAssetMenu(menuName = "Hand Shooter/Refueling Station Settings")]
    public sealed class RefuelingStationSettings : ScriptableObject
    {
        [Tooltip("Tiempo para llenar de 0% a 100%. La espera es proporcional al combustible faltante: con 50% tarda la mitad.")]
        [Min(0.01f)] public float fillDuration = 2f;
        [Tooltip("Fracción del combustible acumulado que se entrega al abandonar la base antes de completar.")]
        [Range(0f, 1f)] public float interruptedFillFraction = 0.5f;
        [Tooltip("Oscurecimiento de la barra de carga respecto de la barra de combustible actual.")]
        [Range(0.1f, 0.9f)] public float previewBrightness = 0.55f;
    }
}
