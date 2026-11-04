using UnityEngine;

namespace HandShooter
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class DoorPanelVisibility : MonoBehaviour
    {
        private SlidingDoor door;
        private MeshRenderer panelRenderer;
        private Mesh croppedMesh;
        private Vector3[] positions;
        private Vector2[] uvs;
        private Vector2[] fullUvs;
        private Vector2[] lowerUvs;
        private bool[] upperVertices;
        private float previousFraction;

        private void OnEnable()
        {
            door = GetComponentInParent<SlidingDoor>();
            panelRenderer = GetComponent<MeshRenderer>();
            previousFraction = -1f;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (!door) return;
            float fraction = door.VisiblePanelFraction;
            if (fraction == previousFraction) return;

            previousFraction = fraction;
            panelRenderer.forceRenderingOff = fraction <= 0f;
            if (fraction <= 0f || fraction >= 1f)
            {
                if (croppedMesh && panelRenderer.additionalVertexStreams == croppedMesh)
                    panelRenderer.additionalVertexStreams = null;
                return;
            }

            if (!croppedMesh) CreateCroppedMesh();
            for (int i = 0; i < positions.Length; i++)
            {
                if (!upperVertices[i]) continue;
                positions[i].y = fraction - 0.5f;
                uvs[i] = Vector2.Lerp(lowerUvs[i], fullUvs[i], fraction);
            }

            croppedMesh.vertices = positions;
            croppedMesh.uv = uvs;
            panelRenderer.additionalVertexStreams = croppedMesh;
        }

        private void CreateCroppedMesh()
        {
            Mesh source = GetComponent<MeshFilter>().sharedMesh;
            positions = source.vertices;
            fullUvs = source.uv;
            uvs = (Vector2[])fullUvs.Clone();
            lowerUvs = (Vector2[])fullUvs.Clone();
            upperVertices = new bool[positions.Length];
            Vector3[] normals = source.normals;
            for (int upper = 0; upper < positions.Length; upper++)
            {
                if (positions[upper].y <= 0f) continue;
                upperVertices[upper] = true;
                // The top cap keeps its UVs. Vertical faces keep the UVs of the surviving portion.
                if (Mathf.Abs(normals[upper].y) > 0.5f) continue;

                for (int lower = 0; lower < positions.Length; lower++)
                {
                    if (positions[lower].y >= 0f || normals[lower] != normals[upper] ||
                        positions[lower].x != positions[upper].x || positions[lower].z != positions[upper].z)
                        continue;

                    lowerUvs[upper] = fullUvs[lower];
                    break;
                }
            }

            croppedMesh = new Mesh { name = "Door panel crop", hideFlags = HideFlags.HideAndDontSave };
            croppedMesh.MarkDynamic();
        }

        private void OnDisable()
        {
            if (panelRenderer) panelRenderer.forceRenderingOff = false;
            if (!croppedMesh) return;
            if (panelRenderer && panelRenderer.additionalVertexStreams == croppedMesh)
                panelRenderer.additionalVertexStreams = null;
            if (Application.IsPlaying(gameObject)) Destroy(croppedMesh);
            else DestroyImmediate(croppedMesh);
            croppedMesh = null;
        }
    }
}
