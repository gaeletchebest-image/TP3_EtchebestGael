using UnityEngine;

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
        internal float MaximumHealth => hunterSettings ? hunterSettings.initialHealth : sniperSettings ? sniperSettings.initialHealth : kamikazeSettings ? kamikazeSettings.initialHealth :
            turretSettings ? turretSettings.initialHealth : settings.targetHealth;
        private int InitialDefensePoints => hunterSettings ? hunterSettings.initialDefensePoints : sniperSettings ? sniperSettings.initialDefensePoints :
            kamikazeSettings ? kamikazeSettings.initialDefensePoints : turretSettings ? turretSettings.initialDefensePoints : 0;
        private int InitialDefenseCapacity => hunterSettings ? hunterSettings.defenseBarCapacity : sniperSettings ? sniperSettings.defenseBarCapacity :
            kamikazeSettings ? kamikazeSettings.defenseBarCapacity : turretSettings ? turretSettings.defenseBarCapacity : 1;
        public TextMesh label;
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
        internal bool IsEnemy => turretSettings || kamikazeSettings || sniperSettings || hunterSettings;

        private void Awake()
        {
            body = GetComponent<Renderer>();
            hitCollider = GetComponent<Collider>();
            originalColor = body.sharedMaterial.GetColor("_BaseColor");
            properties = new MaterialPropertyBlock();
            if (IsEnemy)
            {
                if (TryGetComponent<HunterEnemy>(out var hunter)) statusLabel = hunter.statusLabel;
                if (TryGetComponent<SniperEnemy>(out var sniper)) statusLabel = sniper.statusLabel;
                vitals = new EnemyVitalsDisplay(this, statusLabel);
            }
            ResetTarget();
        }

        private void OnDisable() => vitals?.SetVisible(false);
        private void OnDestroy() => vitals?.Destroy();

        private void LateUpdate()
        {
            if (vitals == null || Health <= 0f) return;
            Bounds bounds = body.bounds;
            Vector3 anchor = new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z);
            if (label) anchor.y = Mathf.Max(anchor.y, label.transform.position.y);
            vitals.Place(anchor, Camera.main, statusLabel);
        }

        public bool TakeDamage(float amount) => ApplyDamage(amount, false);
        public bool TakePlayerShotDamage(float amount) => ApplyDamage(amount, true);

        private bool ApplyDamage(float amount, bool playerShot)
        {
            if (respawnAt >= 0f || Health <= 0f || amount <= 0f) return false;
            amount *= defense.DamageMultiplier;
            if (playerShot) defense.RegisterPlayerHit();
            LastDamage = amount;
            Health = Mathf.Max(0, Health - amount);
            flashUntil = Time.time + 0.12f;
            Damaged?.Invoke(amount);
            if (Health <= 0)
            {
                DestroyedCount++;
                if (IsEnemy)
                {
                    vitals?.SetVisible(false);
                    body.enabled = hitCollider.enabled = false;
                    Destroy(gameObject);
                    return true;
                }
                respawnAt = Time.time + settings.targetRespawnDelay;
                body.enabled = hitCollider.enabled = false;
            }
            RefreshLabel();
            return true;
        }
        public void ResetTarget()
        {
            Health = MaximumHealth;
            defense = new EnemyDefense(InitialDefensePoints, InitialDefenseCapacity);
            LastDamage = 0;
            respawnAt = -1f;
            if (body) body.enabled = true;
            if (hitCollider) hitCollider.enabled = true;
            RefreshLabel();
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
