using System.Collections;
using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    public sealed class ArrivalCapsule : MonoBehaviour
    {
        [SerializeField] private SlidingDoor leftHatch;
        [SerializeField] private SlidingDoor rightHatch;
        [Tooltip("Pausa antes de abrir la cápsula, en segundos.")]
        [SerializeField, Min(0f)] private float openingDelay = 0.7f;

        private IEnumerator Start()
        {
            if (!leftHatch || !rightHatch)
            {
                Debug.LogError("ArrivalCapsule needs both sliding hatches.", this);
                yield break;
            }

            // Door state is restored by the save system. Repeating an open request
            // is safe and also handles saves taken before the opening starts.
            yield return new WaitForSeconds(openingDelay);
            leftHatch.SetOpen(true);
            rightHatch.SetOpen(true);
        }
    }
}
