using UnityEditor;
using UnityEngine;

namespace HandShooter.CRT.Editor
{
    [CustomEditor(typeof(CRTCameraEffect))]
    public sealed class CRTCameraEffectEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor profileEditor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var effect = (CRTCameraEffect)target;
            EditorGUILayout.HelpBox("La UI Screen Space - Overlay se conserva nítida. El efecto se aplica una vez al finalizar la pila de cámaras. Los ajustes del perfil son compartidos entre escenas.", MessageType.Info);
            if (effect.LastInternalResolution.x > 0)
                EditorGUILayout.LabelField("Último buffer CRT", effect.LastInternalResolution.ToString());
            if (!effect.profile) return;
            EditorGUILayout.Space();
            CreateCachedEditor(effect.profile, null, ref profileEditor);
            profileEditor.OnInspectorGUI();
        }

        private void OnDisable()
        {
            if (profileEditor) DestroyImmediate(profileEditor);
        }
    }
}
