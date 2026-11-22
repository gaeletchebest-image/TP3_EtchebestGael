using UnityEngine;

namespace HandShooter
{
    [RequireComponent(typeof(DamageTarget))]
    public sealed class TurretEnemy : MonoBehaviour
    {
        public TurretSettings settings;
        public Material projectileMaterial;
        public Transform muzzle;
        public PrototypePlayer target;
        private float nextShot;
        internal float ShotDelay
        {
            get => Mathf.Max(0f, nextShot - Time.time);
            set => nextShot = Time.time + value;
        }
        private void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = new Color(1f, 0.35f, 0.15f, 1f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, settings.detectionRange));
        }

        private void Start()
        {
            if (!target) target = FindFirstObjectByType<PrototypePlayer>();
        }
        private void Update()
        {
            if (Time.timeScale <= 0f || !settings || !target || target.IsDead || GetComponent<DamageTarget>().Health <= 0f) return;
            Vector3 aim = target.viewCamera ? target.viewCamera.transform.position : target.transform.position + Vector3.up;
            if ((aim - transform.position).sqrMagnitude > settings.detectionRange * settings.detectionRange) return;
            Vector3 origin = muzzle ? muzzle.position : transform.position;
            Vector3 direction = (aim - origin).normalized;
            if (direction == Vector3.zero || Time.time < nextShot) return;
            nextShot = Time.time + Mathf.Max(0.02f, settings.fireInterval);
            ShooterAudio.Play(SoundCue.TurretFire, origin);
            ShooterAudio.Threat();
            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = "Turret Projectile";
            shot.layer = 2;
            Destroy(shot.GetComponent<Collider>());
            shot.transform.position = origin;
            shot.transform.localScale = Vector3.one * Mathf.Max(0.01f, settings.projectileRadius) * 2f;
            if (projectileMaterial) shot.GetComponent<Renderer>().sharedMaterial = projectileMaterial;
            shot.AddComponent<TurretProjectile>().Initialize(direction, settings, transform);
        }
    }
}
