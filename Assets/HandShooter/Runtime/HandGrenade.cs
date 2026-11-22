using UnityEngine;

namespace HandShooter
{
    // Swept ballistic flight, followed by a stationary, fixed fuse on the ground.
    public sealed class HandGrenade : MonoBehaviour
    {
        private Vector3 velocity;
        private float gravity, radius, fuseDuration, fuseRemaining, flightAge;
        private int hitMask, shotId;
        private bool active;
        private HandWeapon owner;
        private ExplosionStats explosion;
        private Material effectMaterial;
        private Color color;
        private float effectDuration;
        private Renderer body;
        private MaterialPropertyBlock properties;
        private LineRenderer warning;
        public bool HasLanded { get; private set; }
        public float FuseRemaining => fuseRemaining;

        public static HandGrenade Spawn(Vector3 position, Vector3 direction, float damage,
            ShooterSettings settings, HandWeapon owner, int shotId = -1)
        {
            var go = new GameObject("Timed grenade", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = 2;
            go.transform.position = position;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var grenade = go.AddComponent<HandGrenade>();
            grenade.Initialize(direction, damage, settings, owner, shotId);
            return grenade;
        }

        private void Initialize(Vector3 direction, float damage, ShooterSettings settings, HandWeapon weapon, int sourceShotId)
        {
            owner = weapon;
            shotId = sourceShotId;
            // Everything is captured at launch, including fuse and blast; later mode changes are harmless.
            explosion = settings.EvaluateGrenadeExplosion(damage);
            velocity = direction.normalized * Mathf.Max(0.1f, settings.grenadeLaunchSpeed) +
                Vector3.up * Mathf.Max(0f, settings.grenadeUpwardSpeed);
            gravity = Mathf.Max(0.1f, settings.grenadeGravity);
            radius = Mathf.Max(0.01f, settings.grenadeRadius);
            fuseDuration = fuseRemaining = Mathf.Max(0.05f, settings.grenadeFuseDuration);
            hitMask = settings.hitMask.value & ~4;
            effectMaterial = settings.beamMaterial;
            color = settings.grenadeColor;
            effectDuration = settings.explosionVisualDuration;
            body = GetComponent<Renderer>();
            body.sharedMaterial = effectMaterial;
            properties = new MaterialPropertyBlock();
            SetColor(color);
            transform.localScale = Vector3.one * (radius * 2f);
            active = true;
        }

        private void Update()
        {
            if (Time.timeScale > 0f) Simulate(Time.deltaTime);
        }

        public void Simulate(float deltaTime)
        {
            if (!active || deltaTime <= 0f || Time.timeScale <= 0f) return;
            Physics.SyncTransforms();
            while (deltaTime > 0f && active)
            {
                float step = Mathf.Min(0.02f, deltaTime);
                deltaTime -= step;
                if (HasLanded)
                {
                    fuseRemaining = Mathf.Max(0f, fuseRemaining - step);
                    float progress = 1f - fuseRemaining / fuseDuration;
                    ShooterAudio.Loop(transform, SoundCue.GrenadeFuse, true, 1f, 0.8f + progress * 1.2f);
                    SetColor(Color.Lerp(color, Color.white, progress * (0.5f + 0.5f * Mathf.Sin(progress * 50f))));
                    if (fuseRemaining <= 0.00001f) Detonate();
                    continue;
                }
                flightAge += step;
                // Misses into a void are discarded without an airborne explosion.
                if (flightAge > 30f) { active = false; Destroy(gameObject); break; }
                velocity += Vector3.down * (gravity * step);
                Vector3 displacement = velocity * step;
                float distance = displacement.magnitude;
                if (distance <= 0f) continue;
                if (!Physics.SphereCast(transform.position, radius, displacement / distance, out var hit,
                    distance, hitMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position += displacement;
                    continue;
                }
                transform.position = hit.point + hit.normal * (radius + 0.005f);
                if (hit.normal.y >= 0.5f && !hit.collider.GetComponentInParent<DamageTarget>()) Land();
                else
                {
                    // Walls and enemies stop forward flight but do not arm or detonate the grenade.
                    float inward = Vector3.Dot(velocity, hit.normal);
                    if (inward < 0f) velocity -= hit.normal * inward;
                }
            }
        }

        private void Land()
        {
            HasLanded = true;
            ShooterAudio.Play(SoundCue.GrenadeLand, transform.position);
            ShooterAudio.Loop(transform, SoundCue.GrenadeFuse, true);
            velocity = Vector3.zero;
            warning = new GameObject("Grenade blast warning").AddComponent<LineRenderer>();
            warning.gameObject.layer = 2;
            warning.transform.SetParent(transform, false);
            warning.useWorldSpace = true;
            warning.loop = true;
            warning.positionCount = 64;
            warning.sharedMaterial = effectMaterial;
            warning.startWidth = warning.endWidth = 0.025f;
            warning.startColor = warning.endColor = color;
            Vector3 center = transform.position + Vector3.down * (radius - 0.02f);
            for (int i = 0; i < warning.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / warning.positionCount;
                warning.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * explosion.Radius);
            }
        }

        private void SetColor(Color value)
        {
            properties.SetColor("_BaseColor", value);
            body.SetPropertyBlock(properties);
        }

        private void OnDisable() => ShooterAudio.Loop(transform, SoundCue.GrenadeFuse, false);

        private void Detonate()
        {
            if (!active) return;
            active = false;
            ShooterAudio.Loop(transform, SoundCue.GrenadeFuse, false);
            ShooterAudio.Play(SoundCue.GrenadeExplosion, transform.position);
            Vector3 origin = transform.position;
            foreach (var target in HandProjectile.CollectBlastTargets(origin, explosion, hitMask))
            {
                if (owner) owner.Damage(target, explosion.Damage, shotId);
                else target.TakePlayerShotDamage(explosion.Damage);
            }
            ExplosionEffect.Spawn(origin, explosion.Radius, effectDuration, effectMaterial, color);
            Destroy(gameObject);
        }
    }
}
