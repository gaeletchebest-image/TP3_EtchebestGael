using System.Collections.Generic;
using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DamageTarget))]
    public sealed class DummyDoorChallenge : MonoBehaviour
    {
        [SerializeField] private HandWeapon weapon;
        [SerializeField] private SlidingDoor door;
        [SerializeField, Min(0), Tooltip("0 admite cualquier cantidad. Un valor positivo exige esa cantidad exacta de tiros que dañen a este blanco.")]
        private int requiredShots;

        private readonly HashSet<int> damagingShots = new HashSet<int>();
        private DamageTarget target;
        private int shotCount;
        private bool completed;

        private void Awake()
        {
            target = GetComponent<DamageTarget>();
            if (weapon && door && !target.IsEnemy) return;
            Debug.LogError("DummyDoorChallenge needs a practice target, the player's weapon and an exit door.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            weapon.DamageDealt += OnDamageDealt;
            target.Respawned += ResetAttempt;
        }

        private void OnDisable()
        {
            if (weapon) weapon.DamageDealt -= OnDamageDealt;
            if (target) target.Respawned -= ResetAttempt;
        }

        private void OnDamageDealt(int shotId, DamageTarget damaged, float damage, bool killed)
        {
            if (completed || damaged != target || shotId <= 0) return;
            if (damagingShots.Add(shotId)) shotCount++;
            if (!killed) return;

            if (requiredShots > 0 && shotCount != requiredShots)
            {
                target.SetRespawnDelay(1f);
                return;
            }

            completed = true;
            target.SetRespawnDelay(-1f);
            door.SetOpen(true);
        }

        private void ResetAttempt()
        {
            shotCount = 0;
            damagingShots.Clear();
        }

        internal SavedGame.DummyChallenge CaptureState(string id)
        {
            var targetState = target ? target.CaptureState(id) : null;
            if (targetState == null) return null;
            return new SavedGame.DummyChallenge
            {
                id = id, target = targetState, shots = shotCount, completed = completed,
                respawnRemaining = target.RespawnRemaining
            };
        }

        internal void RestoreState(SavedGame.DummyChallenge state)
        {
            if (!target || state.target == null) return;
            target.RestoreState(state.target);
            target.SetRespawnDelay(state.respawnRemaining);
            damagingShots.Clear();
            shotCount = state.shots;
            completed = state.completed;
        }
    }
}
