using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HandShooter.CRT
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("Rendering/CRT Camera Effect")]
    public sealed class CRTCameraEffect : MonoBehaviour
    {
        [Tooltip("Perfil compartido de efectos, resolución interna y escalado.")]
        public CRTProfile profile;

        public Vector2Int LastInternalResolution { get; internal set; }
        public bool IsActive => isActiveAndEnabled && profile && profile.IsActive;

        private Camera viewCamera;
        private Camera finalCamera;
        private UniversalRenderPipelineAsset scopedPipeline;
        private float previousScale;
        private UpscalingFilterSelection previousFilter;

        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            RenderPipelineManager.endContextRendering += EndContext;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.endContextRendering -= EndContext;
            RestorePipeline();
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != viewCamera || !IsActive || camera.cameraType != CameraType.Game ||
                camera.targetTexture || camera.stereoEnabled)
                return;

            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) &&
                data.renderType != CameraRenderType.Base)
                return;

            // URP reads these properties after beginCameraRendering. Keep them through the
            // entire stack: overlay camera data is initialized before its own begin callback.
            RestorePipeline();
            scopedPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!scopedPipeline) return;
            previousScale = scopedPipeline.renderScale;
            previousFilter = scopedPipeline.upscalingFilter;
            scopedPipeline.renderScale = profile.reducedResolution ? Mathf.Clamp(profile.resolutionScale, 0.25f, 1f) : 1f;
            scopedPipeline.upscalingFilter = profile.upscaling == CRTUpscaling.Point
                ? UpscalingFilterSelection.Point : UpscalingFilterSelection.Linear;

            finalCamera = camera;
            if (data != null && data.cameraStack != null)
            {
                foreach (var overlay in data.cameraStack)
                    if (overlay && overlay.isActiveAndEnabled &&
                        overlay.TryGetComponent<UniversalAdditionalCameraData>(out var overlayData) &&
                        overlayData.renderType == CameraRenderType.Overlay)
                        finalCamera = overlay;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == finalCamera) RestorePipeline();
        }

        private void EndContext(ScriptableRenderContext context, List<Camera> cameras) => RestorePipeline();

        private void RestorePipeline()
        {
            if (scopedPipeline)
            {
                scopedPipeline.renderScale = previousScale;
                scopedPipeline.upscalingFilter = previousFilter;
            }
            scopedPipeline = null;
            finalCamera = null;
        }
    }
}
