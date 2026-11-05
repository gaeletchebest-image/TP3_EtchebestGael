using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public sealed class CargoLift : MonoBehaviour
    {
        private enum Operation { External, Arrival, Departure }

        [Header("Paradas")]
        [Tooltip("Arrival baja al iniciar. Departure espera al jugador antes de bajar.")]
        [SerializeField] private Operation operation;
        [Tooltip("Recorrido vertical en metros desde la parada visible en el editor.")]
        [SerializeField, Min(0.1f)] private float travelHeight = 4f;
        [Tooltip("Velocidad de la plataforma en metros por segundo.")]
        [SerializeField, Min(0.1f)] private float travelSpeed = 1.4285715f;
        [SerializeField] private bool startAtUpperStop;

        [Header("Plataforma")]
        [SerializeField] private Transform carriage;
        [SerializeField] private BoxCollider deck;

        [Header("Reja")]
        [SerializeField] private AnimationClip openClip;
        [SerializeField] private AnimationClip closeClip;
        [Tooltip("Volumen de referencia para detectar al jugador bajo la reja.")]
        [SerializeField] private BoxCollider gateCollider;
        [SerializeField] private bool startOpen;
        [SerializeField] private Renderer[] gateIndicators;
        [SerializeField] private Material lockedIndicator;
        [SerializeField] private Material openIndicator;

        private readonly Collider[] nearby = new Collider[32];
        private Vector3 authoredStop;
        private Vector3 travelOffset;
        private float liftPosition;
        private float gatePosition;
        private bool upperStop;
        private bool gateRequestedOpen;
        private bool travelQueued;
        private bool travelling;
        private bool destinationUpper;

        internal float DescentProgress => 1f - liftPosition;
        internal bool IsGateOpen => gatePosition == 1f;

        private void Awake()
        {
            if (!carriage || !deck || !openClip || !closeClip || !gateCollider)
            {
                Debug.LogError("CargoLift needs a carriage, deck collider, gate collider and both animation clips.", this);
                enabled = false;
                return;
            }

            authoredStop = carriage.localPosition;
            travelOffset = transform.InverseTransformVector(Vector3.up * travelHeight);
            upperStop = operation != Operation.External || startAtUpperStop;
            liftPosition = upperStop ? 1f : 0f;
            gateRequestedOpen = operation == Operation.Departure || (operation == Operation.External && startOpen);
            gatePosition = gateRequestedOpen ? 1f : 0f;
            if (operation == Operation.Arrival && !GameSaveSession.IsPendingFor(gameObject.scene))
                RaiseArrivalRiders();
            ApplyPose();
            Physics.SyncTransforms();
        }

        private void RaiseArrivalRiders()
        {
            Physics.SyncTransforms();
            int count = FindAboveDeck();
            Vector3 rise = Vector3.up * travelHeight;
            for (int i = 0; i < count; i++)
                if (nearby[i] is CharacterController rider && IsStandingOnDeck(rider))
                    rider.transform.position += rise;
        }

        private void Start()
        {
            if (operation == Operation.Arrival) GoDown();
        }

        public void GoUp() => RequestStop(true);
        public void GoDown() => RequestStop(false);

        public void OpenGate()
        {
            if (travelling) return;
            travelQueued = false;
            gateRequestedOpen = true;
        }

        public void CloseGate() => gateRequestedOpen = false;

        private void RequestStop(bool upper)
        {
            if (travelling) return;
            if (upper == upperStop)
            {
                OpenGate();
                return;
            }

            destinationUpper = upper;
            travelQueued = true;
            CloseGate();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;

            if (travelling)
            {
                MoveCarriage();
                return;
            }

            if (operation == Operation.Departure && upperStop)
            {
                bool playerAboard = IsPlayerAboard();
                if (travelQueued && !playerAboard) OpenGate();
                else if (!travelQueued && playerAboard && gatePosition == 1f) GoDown();
            }

            float target = gateRequestedOpen ? 1f : 0f;
            if (gatePosition != target)
            {
                if (!gateRequestedOpen && EntranceOccupied())
                {
                    OpenGate();
                    return;
                }

                float duration = gateRequestedOpen ? openClip.length : closeClip.length;
                gatePosition = Mathf.MoveTowards(gatePosition, target, Time.deltaTime / duration);
                SampleGate();
                Physics.SyncTransforms();
                UpdateIndicators();
            }

            if (travelQueued && gatePosition == 0f)
            {
                travelQueued = false;
                travelling = true;
            }
        }

        private void SampleGate()
        {
            if (gateRequestedOpen)
                openClip.SampleAnimation(carriage.gameObject, gatePosition * openClip.length);
            else
                closeClip.SampleAnimation(carriage.gameObject, (1f - gatePosition) * closeClip.length);
        }

        private void UpdateIndicators()
        {
            Material material = gatePosition == 1f ? openIndicator : lockedIndicator;
            foreach (Renderer indicator in gateIndicators)
                if (indicator && indicator.sharedMaterial != material)
                    indicator.sharedMaterial = material;
        }

        private bool EntranceOccupied()
        {
            Transform gate = gateCollider.transform;
            // Check the standing area below the raised gate before lowering it.
            Vector3 center = carriage.InverseTransformPoint(gate.TransformPoint(gateCollider.center));
            center.y = 1f;
            Vector3 halfSize = new Vector3(gateCollider.size.x * 0.5f, 1f, 0.24f);
            int count = FindNearby(carriage, center, halfSize);
            for (int i = 0; i < count; i++)
                if (nearby[i] is CharacterController) return true;
            return false;
        }

        private void MoveCarriage()
        {
            float destination = destinationUpper ? 1f : 0f;
            liftPosition = Mathf.MoveTowards(liftPosition, destination, travelSpeed * Time.deltaTime / travelHeight);
            Vector3 next = CarriagePosition;
            Vector3 delta = transform.TransformVector(next - carriage.localPosition);

            int count = FindAboveDeck();
            for (int i = 0; i < count; i++)
            {
                if (nearby[i] is CharacterController rider && IsStandingOnDeck(rider))
                {
                    // Move riders ahead of an ascending floor. A descending floor must move first.
                    if (destinationUpper) rider.Move(delta);
                }
                else
                {
                    nearby[i] = null;
                }
            }

            carriage.localPosition = next;
            Physics.SyncTransforms();

            if (!destinationUpper)
                for (int i = 0; i < count; i++)
                    if (nearby[i] is CharacterController rider) rider.Move(delta);

            if (liftPosition != destination) return;
            upperStop = destinationUpper;
            travelling = false;
            if (operation != Operation.Departure || upperStop) OpenGate();
        }

        private Vector3 CarriagePosition =>
            authoredStop + travelOffset * (operation == Operation.Departure ? liftPosition - 1f : liftPosition);

        private void ApplyPose()
        {
            carriage.localPosition = CarriagePosition;
            SampleGate();
            UpdateIndicators();
        }

        internal bool IsPlayerAboard()
        {
            if (!isActiveAndEnabled) return false;

            int count = FindAboveDeck();
            for (int i = 0; i < count; i++)
            {
                if (nearby[i] is not CharacterController rider ||
                    !rider.TryGetComponent(out PrototypePlayer player) || player.IsDead) continue;

                // Give the player room to clear the front edge before closing the gate.
                Vector3 local = deck.transform.InverseTransformPoint(rider.bounds.center) - deck.center;
                if (local.z > -deck.size.z * 0.5f + 1f && IsStandingOnDeck(rider)) return true;
            }
            return false;
        }

        private int FindAboveDeck() =>
            FindNearby(deck.transform, deck.center + Vector3.up * (deck.size.y * 0.5f + 0.15f),
                new Vector3(deck.size.x * 0.5f, 0.25f, deck.size.z * 0.5f));

        internal SavedGame.Lift CaptureState(string id) => new SavedGame.Lift
        {
            id = id, liftPosition = liftPosition, gatePosition = gatePosition, upperStop = upperStop,
            gateRequestedOpen = gateRequestedOpen, travelQueued = travelQueued, travelling = travelling,
            destinationUpper = destinationUpper, linearTravel = true
        };

        internal void RestoreState(SavedGame.Lift state)
        {
            if (!isActiveAndEnabled) return;
            if (state == null)
            {
                if (operation != Operation.Arrival) return;
                // Older saves predate the arrival sequence and already place the player in the level.
                state = new SavedGame.Lift { gatePosition = 1f, gateRequestedOpen = true };
            }

            liftPosition = Mathf.Clamp01(state.liftPosition);
            if (!state.linearTravel)
                liftPosition = Mathf.SmoothStep(0f, 1f, liftPosition);
            gatePosition = Mathf.Clamp01(state.gatePosition);
            upperStop = state.upperStop;
            gateRequestedOpen = state.gateRequestedOpen;
            travelQueued = state.travelQueued;
            travelling = state.travelling;
            destinationUpper = state.destinationUpper;
            ApplyPose();
        }

        private bool IsStandingOnDeck(CharacterController rider)
        {
            Bounds bounds = rider.bounds;
            Vector3 feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return deck.Raycast(new Ray(feet + Vector3.up * 0.2f, Vector3.down), out _, 0.35f);
        }

        private int FindNearby(Transform space, Vector3 center, Vector3 halfSize)
        {
            return Physics.OverlapBoxNonAlloc(space.TransformPoint(center),
                Vector3.Scale(halfSize, space.lossyScale), nearby, space.rotation,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
