using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HandShooter.CRT.Tests
{
    public sealed class CRTShaderTests
    {
        private Material material;
        private Texture2D source;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a GPU.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/HandShooter/CRT/Shaders/CRT.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "CRT shader must compile on the current graphics API.");
            material = new Material(shader);
            source = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
            source.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            source.Apply();
            properties.Clear();
            properties.SetTexture("_BlitTexture", source);
            properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
            properties.SetVector("_CRTEffects", Vector4.zero);
            properties.SetVector("_CRTScanlines", new Vector4(0, 48, 3, 0));
            properties.SetVector("_CRTNoise", new Vector4(0, 0, 1, 0));
            properties.SetVector("_CRTVignette", new Vector4(0, 0.4f, 0.6f, 0));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(source);
        }

        [TestCase(240, 180)]
        [TestCase(320, 180)]
        [TestCase(420, 180)]
        [TestCase(180, 320)]
        public void DisabledEffectsPreserveEntireImage(int width, int height)
        {
            var pixels = Render(width, height);
            foreach (var pixel in pixels) Assert.That(pixel.r, Is.EqualTo(1f).Within(0.003f));
        }

        [TestCase(240, 180)]
        [TestCase(320, 180)]
        [TestCase(420, 180)]
        [TestCase(180, 320)]
        public void CurvatureKeepsCenterAndCreatesNarrowBorder(int width, int height)
        {
            properties.SetVector("_CRTEffects", new Vector4(0.025f, 0.012f, 0, 0));
            var pixels = Render(width, height);
            Assert.That(pixels[(height / 2) * width + width / 2].r, Is.GreaterThan(0.99f));
            Assert.That(pixels[0].r, Is.LessThan(0.05f));
            int visible = 0;
            foreach (var pixel in pixels) if (pixel.r > 0.9f) visible++;
            Assert.That((float)visible / pixels.Length, Is.GreaterThan(0.90f), "Moderate curvature must retain most of the display area.");
        }

        [Test]
        public void ScanlinesChangeRowsWithoutIntroducingColumnNoise()
        {
            properties.SetVector("_CRTScanlines", new Vector4(0.16f, 48, 3, 0));
            var pixels = Render(320, 180);
            float min = 1f, max = 0f;
            for (int y = 0; y < 180; y++)
            {
                float value = pixels[y * 320 + 100].r;
                Assert.That(pixels[y * 320 + 200].r, Is.EqualTo(value).Within(0.002f));
                min = Mathf.Min(min, value); max = Mathf.Max(max, value);
            }
            Assert.That(max - min, Is.GreaterThan(0.10f));
            Assert.That(min, Is.GreaterThan(0.82f));
        }

        [Test]
        public void NoiseIsAnimatedFineGrainWithLowAmplitude()
        {
            // Gray avoids clamping at white when noise adds positive values.
            source.SetPixels(new[] { Color.gray, Color.gray, Color.gray, Color.gray }); source.Apply();
            properties.SetVector("_CRTNoise", new Vector4(0.009f, 1, 1, 0));
            var first = Render(320, 180);
            properties.SetVector("_CRTNoise", new Vector4(0.009f, 2, 1, 0));
            var second = Render(320, 180);
            float change = 0f, mean = 0f;
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(Mathf.Abs(first[i].r - 0.5f), Is.LessThan(0.005f));
                change += Mathf.Abs(first[i].r - second[i].r);
                mean += first[i].r;
            }
            Assert.That(change / first.Length, Is.GreaterThan(0.001f));
            Assert.That(mean / first.Length, Is.EqualTo(0.5f).Within(0.0005f));
        }

        [Test]
        public void VignettePreservesCenterBrightness()
        {
            properties.SetVector("_CRTVignette", new Vector4(0.12f, 0.4f, 0.6f, 0));
            var pixels = Render(320, 180);
            Assert.That(pixels[90 * 320 + 160].r, Is.GreaterThan(0.99f));
            Assert.That(pixels[0].r, Is.InRange(0.87f, 0.90f));
        }

        private Color[] Render(int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var cmd = new CommandBuffer { name = "CRT GPU verification" };
            try
            {
                properties.SetVector("_CRTScreen", new Vector4(width, height, width, height));
                cmd.SetRenderTarget(target);
                cmd.ClearRenderTarget(false, true, Color.magenta);
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, properties);
                Graphics.ExecuteCommandBuffer(cmd);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                return image.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                cmd.Release();
                Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
