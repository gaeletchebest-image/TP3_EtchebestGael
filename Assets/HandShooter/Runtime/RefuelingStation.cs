using UnityEngine;

namespace HandShooter
{
    [DisallowMultipleComponent]
    public sealed class RefuelingStation : MonoBehaviour
    {
        public RefuelingStationSettings settings;
        public BoxCollider deck;
        public TextMesh statusLabel;
        public Renderer[] progressLights;
        public Material idleLight, chargedLight;
        public float Progress { get; private set; }
        public bool IsFilling => recipient != null;
        public float PendingFuel => weapon ? Mathf.Clamp(pendingFuel, 0f,
            Mathf.Max(0f, weapon.tankCapacity - weapon.Fuel)) : 0f;
        private PrototypePlayer recipient;
        private HandWeapon weapon;
        private float pendingFuel;
        private readonly Collider[] nearby = new Collider[16];

        private void Awake()
        {
            if (settings && deck) return;
            Debug.LogError("RefuelingStation needs settings and a solid deck collider.", this);
            enabled = false;
        }

        private void Update()
        {
            // Death and save restoration can pause the clock on the same frame.
            if (recipient && (!CanFill(recipient) || weapon.ActiveRefuelingStation != this))
            {
                CancelFill(false);
                UpdateDisplay();
            }
            if (Time.deltaTime <= 0f) return;
            if (recipient)
            {
                if (!IsStandingOnDeck(recipient.GetComponent<CharacterController>()))
                    CancelFill(true);
                else if (!recipient.IsMenuOpen)
                {
                    // fillDuration is the time for an empty tank. Accumulate fuel at
                    // a constant rate, including when the player fires during a charge.
                    pendingFuel += Mathf.Max(0f, weapon.tankCapacity) * Time.deltaTime /
                        Mathf.Max(0.01f, settings.fillDuration);
                    float missingFuel = Mathf.Max(0f, weapon.tankCapacity - weapon.Fuel);
                    Progress = missingFuel <= 0f ? 1f : Mathf.Clamp01(pendingFuel / missingFuel);
                    if (Progress >= 1f)
                    {
                        weapon.AddFuel(weapon.tankCapacity);
                        CancelFill(false);
                    }
                }
            }
            else FindRecipient();
            UpdateDisplay();
        }

        private void FindRecipient()
        {
            Bounds bounds = deck.bounds;
            // The player uses Ignore Raycast so its own weapon cannot hit its controller.
            int count = Physics.OverlapBoxNonAlloc(bounds.center + Vector3.up * 0.65f,
                new Vector3(bounds.extents.x, 0.8f, bounds.extents.z), nearby,
                Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!(nearby[i] is CharacterController controller)) continue;
                var player = controller.GetComponent<PrototypePlayer>();
                if (!CanFill(player) || player.IsMenuOpen || !IsStandingOnDeck(controller) ||
                    player.weapon.ActiveRefuelingStation || player.weapon.Fuel >= player.weapon.tankCapacity) continue;
                recipient = player;
                weapon = player.weapon;
                weapon.ActiveRefuelingStation = this;
                pendingFuel = Progress = 0f;
                break;
            }
        }

        private static bool CanFill(PrototypePlayer player) => player && !player.IsDead &&
            !player.IsLoadingSave && player.weapon && player.weapon.isActiveAndEnabled &&
            player.weapon.Hand != null && !player.weapon.HasUnlimitedFuel && !player.weapon.IsReloading;

        public bool IsStandingOnDeck(CharacterController controller)
        {
            if (!controller || !controller.enabled || !deck || !deck.enabled) return false;
            Vector3 foot = controller.bounds.center - Vector3.up * controller.bounds.extents.y;
            Vector3 local = deck.transform.InverseTransformPoint(foot) - deck.center;
            Vector3 half = deck.size * 0.5f;
            float height = local.y - half.y;
            return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.z &&
                height >= -0.08f && height <= 0.25f;
        }

        internal void CancelFill(bool grantPartial)
        {
            var previous = weapon;
            float amount = PendingFuel * (settings ? Mathf.Clamp01(settings.interruptedFillFraction) : 0f);
            if (previous && previous.ActiveRefuelingStation == this) previous.ActiveRefuelingStation = null;
            recipient = null;
            weapon = null;
            pendingFuel = Progress = 0f;
            if (grantPartial && previous) previous.AddFuel(amount);
        }

        private void UpdateDisplay()
        {
            if (statusLabel) statusLabel.text = IsFilling ? $"CARGANDO  {Progress:P0}" : "REPOSICIÓN\nPERMANECÉ EN LA BASE";
            if (progressLights == null) return;
            for (int i = 0; i < progressLights.Length; i++)
                if (progressLights[i]) progressLights[i].sharedMaterial =
                    IsFilling && Progress >= (i + 1f) / progressLights.Length ? chargedLight : idleLight;
        }

        private void OnDisable()
        {
            CancelFill(recipient && !recipient.IsDead && weapon && weapon.isActiveAndEnabled);
            UpdateDisplay();
        }
    }
}
