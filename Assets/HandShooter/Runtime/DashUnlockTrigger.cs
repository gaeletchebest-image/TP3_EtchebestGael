using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class DashUnlockTrigger : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isActiveAndEnabled) return;
            var player = other.GetComponentInParent<PrototypePlayer>();
            if (player && !player.DashUnlocked && !player.IsDead && !player.IsLoadingSave)
                player.UnlockDash();
        }
    }
}
