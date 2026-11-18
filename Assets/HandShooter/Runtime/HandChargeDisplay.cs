using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    public sealed class HandChargeDisplay : MonoBehaviour
    {
        internal static readonly Color Ready = new Color(0.27f, 0.93f, 0.75f);
        internal static readonly Color Cooldown = new Color(1f, 0.59f, 0.22f);
        internal static readonly Color Reloading = new Color(0.24f, 0.66f, 1f);
        internal static readonly Color Empty = new Color(0.96f, 0.21f, 0.12f);
        private static readonly Color Housing = new Color(0.022f, 0.041f, 0.05f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MeshRenderer track, fill;
        private MaterialPropertyBlock properties;
        private Color lastTint;
        private bool lastBlocked;
        private float lastProgress = -1f;

        private void Awake() => properties = new MaterialPropertyBlock();

        internal void Show(float progress, Color tint, bool blocked = false)
        {
            if (!track || !fill) return;
            progress = Mathf.Clamp01(progress);
            if (lastProgress != progress)
            {
                fill.enabled = progress > 0f;
                float width = track.transform.localScale.x;
                var strip = fill.transform;
                Vector3 scale = strip.localScale;
                scale.x = width * progress;
                strip.localScale = scale;
                Vector3 center = strip.localPosition;
                center.x = track.transform.localPosition.x + (scale.x - width) * 0.5f;
                strip.localPosition = center;
                lastProgress = progress;
            }
            if (lastTint == tint && lastBlocked == blocked) return;

            Tint(track, Color.Lerp(Housing, tint, blocked ? 0.53f : 0.16f));
            Tint(fill, tint);
            lastTint = tint;
            lastBlocked = blocked;
        }

        private void Tint(Renderer renderer, Color color)
        {
            properties.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(properties);
        }
    }
}
