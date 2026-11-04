using System;
using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class ArenaEncounter : MonoBehaviour
    {
        [Tooltip("Doors the player passes before reaching this trigger.")]
        [SerializeField] private SlidingDoor[] entranceDoors = Array.Empty<SlidingDoor>();
        [Tooltip("Doors that unlock when the encounter is cleared.")]
        [SerializeField] private SlidingDoor[] exitDoors = Array.Empty<SlidingDoor>();
        [Tooltip("Scene enemies that must be defeated to clear this encounter.")]
        [SerializeField] private DamageTarget[] enemies = Array.Empty<DamageTarget>();

        internal enum EncounterState { Waiting, Fighting, Cleared }
        private EncounterState state;
        internal EncounterState State { get => state; set => state = value; }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void Start()
        {
            SetDoors(entranceDoors, open: true, immediate: true);
            SetDoors(exitDoors, open: false, immediate: true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || state != EncounterState.Waiting) return;
            var player = other.GetComponentInParent<PrototypePlayer>();
            if (!player || player.IsDead) return;

            state = EncounterState.Fighting;
            if (!TryClear()) SetDoors(entranceDoors, open: false);
        }

        private void Update()
        {
            if (Time.timeScale > 0f && state == EncounterState.Fighting) TryClear();
        }

        private bool TryClear()
        {
            foreach (var enemy in enemies)
                if (enemy && (enemy.Health > 0f || !enemy.gameObject.activeInHierarchy))
                    return false;

            state = EncounterState.Cleared;
            SetDoors(entranceDoors, open: true);
            SetDoors(exitDoors, open: true);
            return true;
        }

        private static void SetDoors(SlidingDoor[] doors, bool open, bool immediate = false)
        {
            foreach (var door in doors)
                if (door) door.SetOpen(open, immediate);
        }
    }
}
