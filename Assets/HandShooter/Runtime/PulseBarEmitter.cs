using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    [DisallowMultipleComponent]
    public sealed class PulseBarEmitter : MonoBehaviour
    {
        private const float FaceDepth = 0.39f;

        [Header("Ráfaga")]
        [SerializeField, Min(0.05f)] private float fireInterval = 2.2f;
        [Tooltip("Segundos adicionales antes del primer disparo. Los siguientes mantienen Fire Interval.")]
        [SerializeField, Min(0f)] private float fireOffset;
        [SerializeField, Min(0.1f)] private float speed = 7f;
        [SerializeField, Min(0.3f)] private float barWidth = 2.2f;
        [SerializeField, Min(0.03f)] private float barHeight = 0.2f;
        [SerializeField, Min(0.03f)] private float barDepth = 0.12f;
        [SerializeField] private LayerMask solidLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private Material pulseMaterial;
        [SerializeField] private Mesh pulseMesh;

        [Header("Carcasa")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform upperHousing, lowerHousing, recess;
        [SerializeField] private Transform leftCap, rightCap;
        [SerializeField] private Transform leftStand, rightStand;

        private readonly List<(Transform bar, float speed)> pulses = new List<(Transform, float)>();
        private readonly Collider[] overlaps = new Collider[16];
        private double nextShot;
        private double FireInterval => Mathf.Max(0.05f, fireInterval);

        private Vector3 BarSize
        {
            get
            {
                float width = Mathf.Max(0.3f, barWidth);
                return new Vector3(width, Mathf.Clamp(barHeight, 0.03f, width), Mathf.Max(0.03f, barDepth));
            }
        }

        private void Awake()
        {
            ResizeHousing();
            if (!head || !pulseMaterial || !pulseMesh)
            {
                Debug.LogError("PulseBarEmitter needs a head, pulse mesh and pulse material.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable() => nextShot = Time.timeAsDouble + FireInterval + Mathf.Max(0f, fireOffset);
        private void OnValidate() => ResizeHousing();

        private void ResizeHousing()
        {
            if (!head || !upperHousing || !lowerHousing || !recess || !leftCap || !rightCap || !leftStand || !rightStand)
                return;

            Vector3 size = BarSize;
            float opening = size.y + 0.06f;
            float span = size.x + 0.18f;
            float sides = span * 0.5f + 0.11f;
            head.localPosition = Vector3.up * (0.81f + opening * 0.5f);
            head.localScale = new Vector3(1f, 1f, (0.66f + size.z) / 0.78f);
            upperHousing.localPosition = Vector3.up * (opening * 0.5f);
            lowerHousing.localPosition = -upperHousing.localPosition;
            upperHousing.localScale = lowerHousing.localScale = new Vector3(span, 1f, 1f);
            recess.localScale = new Vector3(span, opening, 1f);
            leftCap.localPosition = Vector3.left * sides;
            rightCap.localPosition = Vector3.right * sides;
            leftCap.localScale = rightCap.localScale = new Vector3(1f, opening + 0.54f, 1f);
            leftStand.localPosition = Vector3.left * sides;
            rightStand.localPosition = Vector3.right * sides;
        }

        private void LateUpdate()
        {
            if (Time.deltaTime <= 0f) return;
            double now = Time.timeAsDouble;
            bool fire = now >= nextShot;
            if (!fire && pulses.Count == 0) return;

            Physics.SyncTransforms();
            if (fire)
            {
                Emit();
                // Keep the firing phase even when a frame is late.
                double interval = FireInterval;
                nextShot += (System.Math.Floor((now - nextShot) / interval) + 1d) * interval;
            }

            // Include the player's CharacterController on Ignore Raycast, as with turret shots.
            int mask = solidLayers.value | (1 << 2);
            for (int index = pulses.Count - 1; index >= 0; index--)
            {
                var (bar, travelSpeed) = pulses[index];
                if (bar && Advance(bar, travelSpeed * Time.deltaTime, mask)) continue;
                if (bar) Destroy(bar.gameObject);
                pulses.RemoveAt(index);
            }
        }

        private void Emit()
        {
            Vector3 scale = transform.lossyScale;
            Vector3 size = Vector3.Scale(BarSize,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 forward = head.forward;
            var shot = new GameObject("Red Pulse Bar", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(shot, gameObject.scene);
            shot.GetComponent<MeshFilter>().sharedMesh = pulseMesh;
            var renderer = shot.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = pulseMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shot.transform.SetPositionAndRotation(
                head.TransformPoint(Vector3.forward * FaceDepth) + forward * (size.z * 0.5f + 0.01f),
                Quaternion.LookRotation(forward, Vector3.up));
            shot.transform.localScale = size;
            pulses.Add((shot.transform, Mathf.Max(0.1f, speed)));
        }

        private bool Advance(Transform bar, float distance, int mask)
        {
            Vector3 halfSize = bar.localScale * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(bar.position, halfSize, overlaps, bar.rotation,
                mask, QueryTriggerInteraction.Ignore);
            bool overlappingSolid = false;
            for (int index = 0; index < count; index++)
            {
                var contact = overlaps[index];
                if (contact.transform.IsChildOf(transform)) continue;
                if (HitPlayer(contact)) return false;
                overlappingSolid = true;
            }
            if (overlappingSolid) return false;

            var hit = FindSweepContact(bar, halfSize, distance, mask);
            if (hit)
            {
                HitPlayer(hit);
                return false;
            }

            bar.position += bar.forward * distance;
            return true;
        }

        private Collider FindSweepContact(Transform bar, Vector3 halfSize, float distance, int mask)
        {
            if (!Physics.BoxCast(bar.position, halfSize, bar.forward, out RaycastHit hit,
                bar.rotation, distance, mask, QueryTriggerInteraction.Ignore)) return null;
            if (!hit.collider.transform.IsChildOf(transform)) return hit.collider;

            // A collider on the emitter can hide another contact farther along the same sweep.
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var candidate in Physics.BoxCastAll(bar.position, halfSize, bar.forward,
                bar.rotation, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (candidate.distance >= nearestDistance || candidate.collider.transform.IsChildOf(transform)) continue;
                nearest = candidate.collider;
                nearestDistance = candidate.distance;
            }
            return nearest;
        }

        private static bool HitPlayer(Collider contact)
        {
            var player = contact.GetComponentInParent<PrototypePlayer>();
            if (!player) return false;
            player.Die("Barrera de pulsos");
            return true;
        }

        private void OnDisable()
        {
            foreach (var (bar, _) in pulses)
                if (bar) Destroy(bar.gameObject);
            pulses.Clear();
        }
    }
}
