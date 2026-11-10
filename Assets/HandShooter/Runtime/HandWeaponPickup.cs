using System.Collections;
using UnityEngine;

namespace HandShooter
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class HandWeaponPickup : MonoBehaviour
    {
        private enum PickupPart { Hand, Thumb }

        [SerializeField] private HandWeapon weapon;
        [SerializeField] private Transform display;
        [SerializeField] private PickupPart part;
        [SerializeField, Min(0.1f)] private float equipDuration = 0.48f;
        private SphereCollider proximity;
        private bool collecting;
        private bool IsThumb => part == PickupPart.Thumb;
        private bool Collected => IsThumb ? weapon.Hand.ThumbUnlocked : weapon.IsEquipped;
        private bool CanCollect => !Collected && (!IsThumb || weapon.IsEquipped) &&
            !weapon.player.IsDead && !weapon.player.IsLoadingSave;

        private void Awake()
        {
            if (!weapon || !weapon.player || !display || (IsThumb && !weapon.thumbVisual))
            {
                Debug.LogError("HandWeaponPickup needs the player's weapon and its display model.", this);
                enabled = false;
                return;
            }

            proximity = GetComponent<SphereCollider>();
            proximity.isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isActiveAndEnabled || collecting || !CanCollect) return;
            var player = weapon.player;
            if (player.IsMenuOpen || other.GetComponentInParent<PrototypePlayer>() != player) return;
            StartCoroutine(Equip());
        }

        private IEnumerator Equip()
        {
            collecting = true;
            Vector3 startPosition = display.position;
            Quaternion startRotation = display.rotation;
            Vector3 startScale = display.localScale;
            Transform destination = IsThumb ? weapon.thumbVisual : weapon.transform;
            Vector3 endScale = destination.lossyScale;
            if (display.parent)
            {
                Vector3 parentScale = display.parent.lossyScale;
                endScale = new Vector3(endScale.x / parentScale.x, endScale.y / parentScale.y, endScale.z / parentScale.z);
            }
            float liftDuration = equipDuration / 3f;
            for (float elapsed = 0f; elapsed < equipDuration && CanCollect; elapsed += Time.deltaTime)
            {
                float lift = Mathf.SmoothStep(0f, 1f, elapsed / liftDuration);
                float settle = Mathf.SmoothStep(0f, 1f, (elapsed - liftDuration) / (equipDuration - liftDuration));
                Vector3 raised = startPosition + Vector3.up * (0.24f * lift);
                display.SetPositionAndRotation(Vector3.Lerp(raised, destination.position, settle),
                    Quaternion.Slerp(startRotation, destination.rotation, settle));
                display.localScale = Vector3.Lerp(startScale, endScale, settle);
                yield return null;
            }

            if (CanCollect)
            {
                display.gameObject.SetActive(false);
                if (IsThumb) weapon.AttachThumb();
                else weapon.Equip();
            }
            else
            {
                display.SetPositionAndRotation(startPosition, startRotation);
                display.localScale = startScale;
            }
            collecting = false;
        }

        private void LateUpdate()
        {
            bool available = !Collected;
            display.gameObject.SetActive(available);
            proximity.enabled = available;
        }
    }
}
