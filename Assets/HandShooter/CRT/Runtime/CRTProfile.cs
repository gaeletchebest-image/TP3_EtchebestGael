using UnityEngine;

namespace HandShooter.CRT
{
    public enum CRTUpscaling { Bilinear, Point }

    [CreateAssetMenu(menuName = "Hand Shooter/CRT Profile", fileName = "CRTProfile")]
    public sealed class CRTProfile : ScriptableObject
    {
        [Header("General")]
        public bool effectEnabled = true;
        [Range(0f, 1f), Tooltip("Intensidad visual global. Cero también desactiva la reducción de resolución.")]
        public float intensity = 1f;

        [Header("Resolución interna real (antes de dibujar la escena)")]
        public bool reducedResolution = true;
        [Range(0.25f, 1f), Tooltip("Escala por eje. 0.67 usa aproximadamente el 45 % de los píxeles de pantalla.")]
        public float resolutionScale = 0.67f;
        public CRTUpscaling upscaling = CRTUpscaling.Bilinear;

        [Header("Scanlines estáticas")]
        public bool scanlines = true;
        [Range(0f, 0.5f)] public float scanlineIntensity = 0.16f;
        [Range(64, 600), Tooltip("Se limita automáticamente según la resolución interna para evitar aliasing.")]
        public int scanlineCount = 240;
        [Range(1f, 8f)] public float scanlineThinness = 3f;

        [Header("Curvatura sin zoom ni recorte")]
        public bool curvature = true;
        [Range(0f, 0.15f)] public float curvatureAmount = 0.025f;
        public bool roundedCorners = true;
        [Range(0f, 0.08f), Tooltip("Radio relativo al lado menor de la imagen.")]
        public float cornerRadius = 0.012f;

        [Header("Ruido analógico fino")]
        public bool analogNoise = true;
        [Range(0f, 0.05f)] public float noiseAmount = 0.009f;
        [Range(1f, 60f)] public float noiseFramesPerSecond = 24f;
        [Range(0.5f, 3f), Tooltip("Tamaño del grano en píxeles de resolución interna.")]
        public float noiseGrainSize = 1f;

        [Header("Aberración cromática en los bordes")]
        public bool chromaticAberration = true;
        [Range(0f, 2f), Tooltip("Separación por canal, medida en píxeles de pantalla.")]
        public float chromaticOffsetPixels = 0.4f;

        [Header("Viñeteado")]
        public bool vignette = true;
        [Range(0f, 0.6f)] public float vignetteIntensity = 0.12f;
        [Range(0.1f, 0.9f)] public float vignetteStart = 0.4f;
        [Range(0.05f, 1f)] public float vignetteSoftness = 0.6f;

        public bool IsActive => effectEnabled && intensity > 0f;
    }
}
