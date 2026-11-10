using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    public sealed class SlidingDoor : MonoBehaviour
    {
        [SerializeField] private Rigidbody panel;
        [Tooltip("Center of the closed panel, relative to the door root.")]
        [SerializeField] private Vector3 closedPosition = new Vector3(0f, 1.5f, 0f);
        [Tooltip("Center of the open panel, relative to the door root.")]
        [SerializeField] private Vector3 openPosition = new Vector3(0f, 4.7f, 0f);
        [SerializeField] private Vector3 size = new Vector3(3f, 3f, 0.4f);
        [SerializeField, Min(0.01f)] private float travelTime = 0.45f;
        [SerializeField] private bool startOpen;

        [Header("Optional clearance before sliding")]
        [SerializeField] private Vector3 clearanceOffset;
        [SerializeField, Min(0f)] private float clearanceTravelTime;
        [SerializeField, Min(0f)] private float clearanceHoldTime;

        private bool openRequested;
        private float openness;
        private float startOpenness;
        private float progress = 1f;
        private bool HasClearance => clearanceTravelTime > 0f;
        private float ClearanceTime => HasClearance ? clearanceTravelTime + clearanceHoldTime : 0f;
        private Vector3 ClearancePosition => closedPosition + clearanceOffset;

        internal float VisiblePanelFraction =>
            panel ? Mathf.Clamp01(1f - (panel.transform.localPosition.y - closedPosition.y) / size.y) : 1f;

        private void Awake()
        {
            if (!panel)
            {
                Debug.LogError("SlidingDoor needs a panel Rigidbody.", this);
                enabled = false;
                return;
            }

            panel.transform.localScale = size;
            SetOpen(startOpen, immediate: true);
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            if (!panel || (!immediate && openRequested == open)) return;
            openRequested = open;
            if (immediate)
            {
                openness = open ? 1f : 0f;
                progress = 1f;
                panel.position = PanelPosition;
                return;
            }

            startOpenness = openness;
            progress = 0f;
        }

        private Vector3 PanelPosition
        {
            get
            {
                if (!HasClearance)
                    return transform.TransformPoint(Vector3.Lerp(closedPosition, openPosition, openness));

                float elapsed = openness * (ClearanceTime + travelTime);
                if (elapsed < clearanceTravelTime)
                {
                    float release = Mathf.SmoothStep(0f, 1f, elapsed / clearanceTravelTime);
                    return transform.TransformPoint(Vector3.Lerp(closedPosition, ClearancePosition, release));
                }

                float slide = Mathf.SmoothStep(0f, 1f, (elapsed - ClearanceTime) / travelTime);
                return transform.TransformPoint(Vector3.Lerp(ClearancePosition, openPosition, slide));
            }
        }

        internal SavedGame.Door CaptureState(string id) => new SavedGame.Door
        {
            id = id, open = openRequested, openness = openness, startOpenness = startOpenness, progress = progress
        };

        internal void RestoreState(SavedGame.Door state)
        {
            openRequested = state.open;
            openness = state.openness;
            startOpenness = state.startOpenness;
            progress = state.progress;
            if (!panel) return;
            // Keep the interpolated transform and its Rigidbody in sync before restoring physics.
            Vector3 position = PanelPosition;
            panel.transform.position = position;
            panel.position = position;
        }

        private void FixedUpdate()
        {
            if (progress >= 1f) return;
            float target = openRequested ? 1f : 0f;
            float duration = HasClearance
                ? (ClearanceTime + travelTime) * Mathf.Abs(target - startOpenness)
                : travelTime;
            progress = Mathf.Clamp01(progress + Time.fixedDeltaTime / Mathf.Max(0.01f, duration));
            openness = HasClearance
                ? Mathf.Lerp(startOpenness, target, progress)
                : Mathf.SmoothStep(startOpenness, target, progress);
            panel.MovePosition(PanelPosition);
        }

        private void OnValidate()
        {
            travelTime = Mathf.Max(0.01f, travelTime);
            size = Vector3.Max(Vector3.one * 0.01f, size);
        }

        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(closedPosition, size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(openPosition, size);
            if (HasClearance)
            {
                Gizmos.DrawWireCube(ClearancePosition, size);
                Gizmos.DrawLine(closedPosition, ClearancePosition);
                Gizmos.DrawLine(ClearancePosition, openPosition);
            }
            else Gizmos.DrawLine(closedPosition, openPosition);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
