using UnityEngine;

namespace HandShooter
{
    // TextMesh uses a dynamic font atlas. Keep its texture on the world-space material
    // without modifying the built-in font material or creating material instances.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TextMesh))]
    public sealed class WorldTextTexture : MonoBehaviour
    {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private TextMesh text;
        private Renderer textRenderer;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            text = GetComponent<TextMesh>();
            textRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            Font.textureRebuilt += OnFontTextureRebuilt;
            RefreshTexture();
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (text && text.font == font) RefreshTexture();
        }

        private void OnWillRenderObject() => RefreshTexture();

        private void RefreshTexture()
        {
            if (!text || !text.font || !textRenderer || properties == null) return;
            textRenderer.GetPropertyBlock(properties);
            properties.SetTexture(MainTex, text.font.material.mainTexture);
            textRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable() => Font.textureRebuilt -= OnFontTextureRebuilt;
    }
}
