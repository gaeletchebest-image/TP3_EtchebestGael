using UnityEngine;

namespace HandShooter
{
    public sealed class DamageTarget : MonoBehaviour
    {
        public ShooterSettings settings;
        public TurretSettings turretSettings;
        public KamikazeSettings kamikazeSettings;
        public SniperSettings sniperSettings;
        private float InitialHealth => sniperSettings ? sniperSettings.initialHealth : kamikazeSettings ? kamikazeSettings.initialHealth :
            turretSettings ? turretSettings.initialHealth : settings.targetHealth;
        public TextMesh label;
        public float Health { get; private set; }
        public float LastDamage { get; private set; }
        public int DestroyedCount { get; private set; }
        private Renderer body;
        private Collider hitCollider;
        private float respawnAt = -1f, flashUntil;
        private Color originalColor;
        private MaterialPropertyBlock properties;
        private void Awake()
        {
            body = GetComponent<Renderer>();
            hitCollider = GetComponent<Collider>();
            originalColor = body.sharedMaterial.GetColor("_BaseColor");
            properties = new MaterialPropertyBlock();
            ResetTarget();
        }
        public bool TakeDamage(float amount)
        {
            if (respawnAt >= 0f || amount <= 0f) return false;
            LastDamage = amount;
            Health = Mathf.Max(0, Health - amount);
            flashUntil = Time.time + 0.12f;
            if (Health <= 0)
            {
                DestroyedCount++;
                if (turretSettings || kamikazeSettings || sniperSettings) { body.enabled = hitCollider.enabled = false; Destroy(gameObject); return true; }
                respawnAt = Time.time + settings.targetRespawnDelay;
                body.enabled = hitCollider.enabled = false;
            }
            RefreshLabel();
            return true;
        }
        public void ResetTarget()
        {
            Health = InitialHealth;
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
            if (label) label.text = Health <= 0 ? "DESTRUIDO" : $"{Health:0.#} / {InitialHealth:0.#}" + (LastDamage > 0 ? $"\n-{LastDamage:0.#}" : "");
        }
    }
}
