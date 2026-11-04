using UnityEditor;
using UnityEngine;

namespace HandShooter
{
    [CustomEditor(typeof(SlidingDoor))]
    [CanEditMultipleObjects]
    internal sealed class SlidingDoorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (!DrawDefaultInspector() || EditorApplication.isPlayingOrWillChangePlaymode) return;

            foreach (SlidingDoor door in targets)
                UpdatePanelPreview(door);

            SceneView.RepaintAll();
        }

        private static void UpdatePanelPreview(SlidingDoor door)
        {
            var properties = new SerializedObject(door);
            var panel = properties.FindProperty("panel").objectReferenceValue as Rigidbody;
            if (!panel) return;

            bool startOpen = properties.FindProperty("startOpen").boolValue;
            Vector3 position = properties.FindProperty(startOpen ? "openPosition" : "closedPosition").vector3Value;
            Vector3 size = properties.FindProperty("size").vector3Value;
            Transform panelTransform = panel.transform;
            if (panelTransform.localPosition == position && panelTransform.localScale == size) return;

            Undo.RecordObject(panelTransform, "Update door preview");
            panelTransform.localPosition = position;
            panelTransform.localScale = size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(panelTransform);
        }
    }
}
