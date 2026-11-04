using UnityEngine;

namespace HandShooter
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class DoorPanelTiling : MonoBehaviour
    {
        private const float UnitsPerTile = 3f;
        private const int WideFacesSlot = 0;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");

        private MeshRenderer panel;
        private MaterialPropertyBlock properties;
        private Vector4 appliedTiling;

        private void OnEnable()
        {
            panel = GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
            appliedTiling = Vector4.zero;
            Update();
        }

        private void Update()
        {
            Material material = panel.sharedMaterial;
            if (!material || !material.HasProperty(BaseMap)) return;

            Vector2 tiling = material.GetTextureScale(BaseMap);
            Vector2 offset = material.GetTextureOffset(BaseMap);
            Vector3 size = transform.lossyScale;
            var faceTiling = new Vector4(
                tiling.x * Mathf.Abs(size.x) / UnitsPerTile,
                tiling.y * Mathf.Abs(size.y) / UnitsPerTile,
                offset.x,
                offset.y);
            if (faceTiling == appliedTiling) return;

            panel.GetPropertyBlock(properties, WideFacesSlot);
            properties.SetVector(BaseMapTransform, faceTiling);
            panel.SetPropertyBlock(properties, WideFacesSlot);
            appliedTiling = faceTiling;
        }

        private void OnDisable()
        {
            if (panel) panel.SetPropertyBlock(null, WideFacesSlot);
        }
    }
}
