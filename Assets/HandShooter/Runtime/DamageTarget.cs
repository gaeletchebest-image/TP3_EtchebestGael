using UnityEngine;
using UnityEngine.AI;

namespace HandShooter
{
    public sealed class DamageTarget : MonoBehaviour
    {
        public ShooterSettings settings;
        public TurretSettings turretSettings;
        public KamikazeSettings kamikazeSettings;
        public SniperSettings sniperSettings;
        public HunterSettings hunterSettings;
        public event System.Action<float> Damaged;
        internal event System.Action Respawned;
        internal float MaximumHealth => hunterSettings ? hunterSettings.initialHealth : sniperSettings ? sniperSettings.initialHealth : kamikazeSettings ? kamikazeSettings.initialHealth :
            turretSettings ? turretSettings.initialHealth : PracticeMaximumHealth;
        private float PracticeMaximumHealth => practiceHealthOverride > 0f ? practiceHealthOverride : settings.targetHealth;
        private int InitialDefensePoints => hunterSettings ? hunterSettings.initialDefensePoints : sniperSettings ? sniperSettings.initialDefensePoints :
            kamikazeSettings ? kamikazeSettings.initialDefensePoints : turretSettings ? turretSettings.initialDefensePoints : practiceDefensePoints;
        private int InitialDefenseCapacity => hunterSettings ? hunterSettings.defenseBarCapacity : sniperSettings ? sniperSettings.defenseBarCapacity :
            kamikazeSettings ? kamikazeSettings.defenseBarCapacity : turretSettings ? turretSettings.defenseBarCapacity : practiceDefenseBarCapacity;
        public TextMesh label;
        [SerializeField] private bool showVitals;
        [Header("Blanco de práctica")]
        [SerializeField, Min(0f), Tooltip("Vida máxima de este blanco. Con 0 usa la vida de Shooter Settings.")]
        private float practiceHealthOverride;
        [SerializeField, Min(0), Tooltip("Puntos de defensa iniciales. Con 0 el blanco no tiene defensa.")]
        private int practiceDefensePoints;
        [SerializeField, Min(1), Tooltip("Impactos necesarios para romper cada punto de defensa.")]
        private int practiceDefenseBarCapacity = 1;
        public float Health { get; private set; }
        internal Vector3 RangeCenter => new Vector3(transform.position.x, body.bounds.min.y + 0.07f, transform.position.z);
        public int DefensePoints => defense.Points;
        public int DefenseBar => defense.Bar;
        public int DefenseBarCapacity => defense.Capacity;
        public float DamageMultiplier => defense.DamageMultiplier;
        public float LastDamage { get; private set; }
        public int DestroyedCount { get; private set; }
        private Renderer body;
        private Collider hitCollider;
        private float respawnAt = -1f, flashUntil;
        private Color originalColor;
        private MaterialPropertyBlock properties;
        private EnemyDefense defense;
        private EnemyVitalsDisplay vitals;
        private TextMesh statusLabel;
        private bool destroyingEnemy;
        internal bool IsEnemy => turretSettings || kamikazeSettings || sniperSettings || hunterSettings;
        internal float RespawnRemaining => respawnAt < 0f ? -1f : Mathf.Max(0f, respawnAt - Time.time);
        internal void SetRespawnDelay(float seconds) => respawnAt = seconds < 0f ? -1f : Time.time + seconds;

        private void Awake()
        {
            body = GetComponent<Renderer>();
            hitCollider = GetComponent<Collider>();
            originalColor = body.sharedMaterial.GetColor("_BaseColor");
            properties = new MaterialPropertyBlock();
            if (IsEnemy || showVitals || InitialDefensePoints > 0)
            {
                if (TryGetComponent<HunterEnemy>(out var hunter)) statusLabel = hunter.statusLabel;
                if (TryGetComponent<SniperEnemy>(out var sniper)) statusLabel = sniper.statusLabel;
                vitals = new EnemyVitalsDisplay(this, statusLabel);
            }
            ResetTarget();
            if (IsEnemy && !turretSettings) gameObject.AddComponent<RobotMotionAudio>();
        }

        private void OnDisable() => vitals?.SetVisible(false);
        private void OnDestroy() => vitals?.Destroy();

        internal SavedGame.Enemy CaptureState(string id)
        {
            if (!body) return null; // Awake has not run on inactive scene objects.
            var state = new SavedGame.Enemy
            {
                id = id, defeated = Health <= 0f, position = transform.position, rotation = transform.rotation,
                health = Health, defensePoints = DefensePoints, defenseCapacity = DefenseBarCapacity,
                defenseBar = DefenseBar, lastDamage = LastDamage, destroyedCount = DestroyedCount
            };
            if (TryGetComponent<TurretEnemy>(out var turret)) state.shotDelay = turret.ShotDelay;
            if (TryGetComponent<KamikazeEnemy>(out var kamikaze))
            {
                state.acquiredTarget = kamikaze.HasAcquiredTarget;
                state.defeated |= kamikaze.HasDetonated;
            }
            if (TryGetComponent<HunterEnemy>(out var hunter)) state.hunter = hunter.CaptureState();
            if (TryGetComponent<SniperEnemy>(out var sniper)) state.sniper = sniper.CaptureState();
            if (TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
            {
                state.navigation = new SavedGame.Navigation
                {
                    hasDestination = agent.hasPath || agent.pathPending, destination = agent.destination,
                    stopped = agent.isStopped, speed = agent.speed, acceleration = agent.acceleration,
                    autoBraking = agent.autoBraking
                };
            }
            return state;
        }

        internal void RestoreState(SavedGame.Enemy state)
        {
            Health = IsEnemy ? state.health : Mathf.Clamp(state.health, 0f, MaximumHealth);
            LastDamage = state.lastDamage;
            DestroyedCount = state.destroyedCount;
            defense = new EnemyDefense(state.defensePoints, state.defenseCapacity, state.defenseBar);

            if (TryGetComponent<NavMeshAgent>(out var agent) && agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(state.position - Vector3.up * agent.baseOffset);
                if (state.navigation != null)
                {
                    var nav = state.navigation;
                    agent.speed = nav.speed;
                    agent.acceleration = nav.acceleration;
                    agent.autoBraking = nav.autoBraking;
                    agent.isStopped = nav.stopped;
                    if (nav.hasDestination) agent.SetDestination(nav.destination);
                }
            }
            else transform.position = state.position;
            transform.rotation = state.rotation;

            if (TryGetComponent<TurretEnemy>(out var turret)) turret.ShotDelay = state.shotDelay;
            if (TryGetComponent<KamikazeEnemy>(out var kamikaze)) kamikaze.HasAcquiredTarget = state.acquiredTarget;
            if (state.hunter != null && TryGetComponent<HunterEnemy>(out var hunter)) hunter.RestoreState(state.hunter);
            if (state.sniper != null && TryGetComponent<SniperEnemy>(out var sniper)) sniper.RestoreState(state.sniper);
            if (!IsEnemy) body.enabled = hitCollider.enabled = Health > 0f;
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (vitals == null) return;
            Camera camera = Camera.main;
            if (Health <= 0f || !camera || !HasLineOfSight(camera))
            {
                vitals.SetVisible(false);
                return;
            }

            Bounds bounds = body.bounds;
            Vector3 anchor = new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z);
            if (label) anchor.y = Mathf.Max(anchor.y, label.transform.position.y);
            vitals.Place(anchor, camera, statusLabel);
        }

        // Test the body so the floating indicators cannot give away a target behind cover.
        internal bool HasLineOfSight(Camera camera) =>
            !Physics.Linecast(camera.transform.position, hitCollider.bounds.center, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
            hit.collider.GetComponentInParent<DamageTarget>() == this;

        public bool TakeDamage(float amount) => ApplyDamage(amount, false);
        public bool TakePlayerShotDamage(float amount) => ApplyDamage(amount, true);

        private bool ApplyDamage(float amount, bool playerShot)
        {
            if (respawnAt >= 0f || Health <= 0f || amount <= 0f) return false;
            int oldDefense = defense.Points;
            amount *= defense.DamageMultiplier;
            if (playerShot) defense.RegisterPlayerHit();
            ShooterAudio.Play(oldDefense > defense.Points ? SoundCue.ShieldBreak :
                oldDefense > 0 ? SoundCue.ShieldHit : SoundCue.TargetHit, transform.position);
            LastDamage = amount;
            Health = Mathf.Max(0, Health - amount);
            flashUntil = Time.time + 0.12f;
            Damaged?.Invoke(amount);
            if (Health <= 0)
            {
                DestroyedCount++;
                vitals?.SetVisible(false);
                if (IsEnemy)
                {
                    DestroyEnemy();
                    return true;
                }
                SetRespawnDelay(settings.targetRespawnDelay);
                body.enabled = hitCollider.enabled = false;
            }
            RefreshLabel();
            return true;
        }

        internal void DestroyEnemy()
        {
            if (destroyingEnemy) return;
            destroyingEnemy = true;
            ShooterAudio.Play(SoundCue.EnemyDestroy, transform.position);
            EnemyDestructionEffect.Spawn(body, originalColor);
            body.enabled = hitCollider.enabled = false;
            Destroy(gameObject);
        }

        public void ResetTarget()
        {
            if (DestroyedCount > 0 && Health <= 0f && !IsEnemy) ShooterAudio.Play(SoundCue.TargetRespawn, transform.position);
            Health = MaximumHealth;
            defense = new EnemyDefense(InitialDefensePoints, InitialDefenseCapacity);
            LastDamage = 0;
            respawnAt = -1f;
            if (body) body.enabled = true;
            if (hitCollider) hitCollider.enabled = true;
            RefreshLabel();
            Respawned?.Invoke();
        }
        private void Update()
        {
            if (respawnAt >= 0f && Time.time >= respawnAt) ResetTarget();
            properties.SetColor("_BaseColor", Time.time < flashUntil ? Color.white : originalColor);
            body.SetPropertyBlock(properties);
        }
        private void RefreshLabel()
        {
            if (label) label.text = Health <= 0 ? "DESTRUIDO" : $"{Health:0.#} / {MaximumHealth:0.#}" + (LastDamage > 0 ? $"\n-{LastDamage:0.#}" : "");
            vitals?.Refresh(Health, MaximumHealth, LastDamage, defense);
        }
    }
}
