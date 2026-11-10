using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HandShooter.CRT.Editor
{
    public static class CRTSetup
    {
        public const string ProfilePath = "Assets/HandShooter/CRT/ModerateCRT.asset";

        [MenuItem("Tools/Hand Shooter/CRT/Configure loaded cameras and renderers")]
        public static void Configure()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CRTProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<CRTProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            InstallRenderer("Assets/Settings/PC_Renderer.asset");
            InstallRenderer("Assets/Settings/Mobile_Renderer.asset");
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!camera.gameObject.scene.IsValid() || !camera.CompareTag("MainCamera")) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data.renderType != CameraRenderType.Base) continue;
                var effect = camera.GetComponent<CRTCameraEffect>();
                if (!effect) effect = Undo.AddComponent<CRTCameraEffect>(camera.gameObject);
                Undo.RecordObject(effect, "Configure CRT camera");
                if (!effect.profile) effect.profile = profile;
                EditorUtility.SetDirty(effect);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
        }

        private static void InstallRenderer(string path)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!renderer) return;
            var feature = renderer.rendererFeatures.Find(f => f is CRTRendererFeature) as CRTRendererFeature;
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<CRTRendererFeature>();
                feature.name = "Moderate CRT";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("crtShader").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/HandShooter/CRT/Shaders/CRT.shader");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Match URP's renderer-feature recovery map as well as the visible list.
            var rendererObject = new SerializedObject(renderer);
            var map = rendererObject.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id))
                    map.GetArrayElementAtIndex(i).longValue = id;
            }
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();
            EditorUtility.SetDirty(feature);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
    }
}
