using UnityEngine;

namespace HandShooter
{
    internal sealed class ProjectileRangeWarning
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly Color HotColor = new Color(1f, 0.95f, 0.8f);
        private readonly Renderer renderer;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Color flightColor;
        private readonly float warningDistance;

        public ProjectileRangeWarning(Renderer renderer, Color flightColor, float range)
        {
            this.renderer = renderer;
            this.flightColor = flightColor;
            warningDistance = range * 0.25f;
            if (!renderer) return;
            renderer.GetPropertyBlock(properties);
            SetColor(flightColor);
        }

        public void Update(float remainingRange)
        {
            if (!renderer || remainingRange >= warningDistance) return;
            float progress = 1f - Mathf.Clamp01(remainingRange / warningDistance);
            // Three pulses with progressively less distance between them.
            float phase = progress * (1f + 2f * progress) * (2f * Mathf.PI);
            float pulse = 0.5f - 0.5f * Mathf.Cos(phase);
            Color color = Color.Lerp(flightColor, HotColor, progress * Mathf.Lerp(0.65f * pulse, 1f, progress));
            color.a = flightColor.a;
            SetColor(color);
        }

        private void SetColor(Color color)
        {
            properties.SetColor(BaseColor, color);
            properties.SetColor(LegacyColor, color);
            renderer.SetPropertyBlock(properties);
        }
    }
}
