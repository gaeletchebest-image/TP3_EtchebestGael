using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HandShooter.CRT
{
    public sealed class CRTRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("Referencia explícita para incluir el shader en el build.")]
        private Shader crtShader;
        private Material material;
        private CRTPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = crtShader ? CoreUtils.CreateEngineMaterial(crtShader) : null;
            pass = new CRTPass(material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var data = renderingData.cameraData;
            // Only the last camera resolves the completed world + first-person hand stack.
            if (!material || data.cameraType != CameraType.Game || !data.resolveFinalTarget ||
                data.camera.targetTexture || data.camera.stereoEnabled)
                return;
            var baseCamera = data.baseCamera ? data.baseCamera : data.camera;
            if (!baseCamera.TryGetComponent<CRTCameraEffect>(out var effect) || !effect.IsActive)
                return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class CRTPass : ScriptableRenderPass
        {
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private static readonly int Effects = Shader.PropertyToID("_CRTEffects");
            private static readonly int Scanlines = Shader.PropertyToID("_CRTScanlines");
            private static readonly int Noise = Shader.PropertyToID("_CRTNoise");
            private static readonly int Vignette = Shader.PropertyToID("_CRTVignette");
            private static readonly int Screen = Shader.PropertyToID("_CRTScreen");

            public CRTPass(Material material) { this.material = material; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer) return;
                var baseCamera = camera.baseCamera ? camera.baseCamera : camera.camera;
                if (!baseCamera.TryGetComponent<CRTCameraEffect>(out var effect) || !effect.IsActive) return;

                var profile = effect.profile;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                effect.LastInternalResolution = new Vector2Int(desc.width, desc.height);
                desc.name = "CRT Color";
                desc.clearBuffer = false;
                desc.filterMode = profile.upscaling == CRTUpscaling.Point ? FilterMode.Point : FilterMode.Bilinear;
                var destination = graph.CreateTexture(desc);
                float intensity = Mathf.Clamp01(profile.intensity);

                properties.Clear();
                properties.SetVector(Effects, new Vector4(
                    profile.curvature ? profile.curvatureAmount * intensity : 0f,
                    profile.roundedCorners ? profile.cornerRadius * intensity : 0f,
                    profile.chromaticAberration ? profile.chromaticOffsetPixels * intensity : 0f, 0f));
                properties.SetVector(Scanlines, new Vector4(
                    profile.scanlines ? profile.scanlineIntensity * intensity : 0f,
                    Mathf.Min(profile.scanlineCount, desc.height / 3f), profile.scanlineThinness, 0f));
                properties.SetVector(Noise, new Vector4(
                    profile.analogNoise ? profile.noiseAmount * intensity : 0f,
                    Mathf.Floor(Time.unscaledTime * profile.noiseFramesPerSecond),
                    Mathf.Max(0.5f, profile.noiseGrainSize), 0f));
                properties.SetVector(Vignette, new Vector4(
                    profile.vignette ? profile.vignetteIntensity * intensity : 0f,
                    profile.vignetteStart, profile.vignetteSoftness, 0f));
                properties.SetVector(Screen, new Vector4(baseCamera.pixelWidth, baseCamera.pixelHeight, desc.width, desc.height));

                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0,
                    properties, destinationSlice: 0, destinationMip: 0,
                    geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
                graph.AddBlitPass(parameters, passName: "CRT (world + hand, before overlay UI)");
                // Swap rather than blit back: no redundant fullscreen color copy.
                resources.cameraColor = destination;
            }
        }
    }
}
