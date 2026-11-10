using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SlidingDoor))]
    public sealed class WeaponUnlockDoor : MonoBehaviour
    {
        [SerializeField] private HandWeapon weapon;
        [SerializeField, Min(0f)] private float openingDelay = 1f;
        private SlidingDoor door;
        internal float RemainingDelay { get; set; }

        private void Awake()
        {
            door = GetComponent<SlidingDoor>();
            RemainingDelay = openingDelay;
        }

        private void Update()
        {
            if (!weapon || !weapon.IsEquipped || weapon.player.IsLoadingSave) return;
            RemainingDelay = Mathf.Max(0f, RemainingDelay - Time.deltaTime);
            if (RemainingDelay > 0f) return;
            door.SetOpen(true);
            enabled = false;
        }
    }
}
