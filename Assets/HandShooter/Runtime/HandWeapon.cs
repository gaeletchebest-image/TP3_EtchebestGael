using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HandShooter
{
    public sealed class HandWeapon : MonoBehaviour
    {
        public ShooterSettings settings;
        public PrototypePlayer player;
        public Camera viewCamera;
        public Transform muzzle;
        public Transform[] fingerVisuals;
        public Transform thumbVisual;
        public HandConfiguration Hand { get; private set; }
        public ShotStats Stats => settings.Evaluate(Hand);
        public float CooldownRemaining => cooldownFraction * Stats.Interval;
        public float LastHitDamage { get; private set; }
        public float LastHitTime { get; private set; } = -100f;
        public event Action<float> Hit;
        private float cooldownFraction;
        private bool requireRelease;
        private float recoil;
        private void Awake() { Hand = new HandConfiguration(settings.initialActiveFingers, settings.initialThumbActive); }
        public void RequireTriggerRelease() { requireRelease = true; }
        private void Update()
        {
            cooldownFraction = Mathf.Max(0f, cooldownFraction - Time.deltaTime / Stats.Interval);
            UpdateHandVisuals();
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) requireRelease = false;
            if (player.IsMenuOpen || Cursor.lockState != CursorLockMode.Locked || requireRelease) return;
            if (settings.automaticFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame) TryFire();
        }
        public bool TryFire()
        {
            if (cooldownFraction > 0f || player.IsMenuOpen || Time.timeScale == 0f) return false;
            ShotStats stats = Stats;
            cooldownFraction = 1f;
            recoil = 1f;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int mask = settings.hitMask.value & ~4;
            Vector3 end = ray.origin + ray.direction * settings.range;
            if (Physics.Raycast(ray, out RaycastHit aimHit, settings.range, mask, QueryTriggerInteraction.Ignore)) end = aimHit.point;
            Vector3 start = muzzle.position;
            Vector3 direction = (end - start).normalized;
            // A muzzle poking through a wall must not shoot through it.
            if (Physics.Linecast(ray.origin, start, out RaycastHit obstruction, mask, QueryTriggerInteraction.Ignore))
            {
                if (stats.Hitscan)
                {
                    Damage(obstruction.collider, stats.Damage);
                    DrawBeam(ray.origin, obstruction.point, settings.rayColor);
                }
                else
                {
                    var blockedProjectile = Instantiate(settings.projectilePrefab, ray.origin, Quaternion.LookRotation(direction));
                    blockedProjectile.Initialize(direction, stats.Damage, settings, this);
                    blockedProjectile.Detonate(obstruction.collider, obstruction.point, obstruction.normal);
                }
                return true;
            }
            if (stats.Hitscan)
            {
                if (Physics.Raycast(start, direction, out RaycastHit hit, Vector3.Distance(start, end) + 0.02f, mask, QueryTriggerInteraction.Ignore))
                { end = hit.point; Damage(hit.collider, stats.Damage); }
                DrawBeam(start, end, settings.rayColor);
            }
            else
            {
                HandProjectile projectile = Instantiate(settings.projectilePrefab, start, Quaternion.LookRotation(direction));
                projectile.Initialize(direction, stats.Damage, settings, this);
            }
            return true;
        }
        public void Damage(Collider collider, float damage)
        {
            Damage(collider.GetComponentInParent<DamageTarget>(), damage);
        }
        public void Damage(DamageTarget target, float damage)
        {
            if (target && target.TakeDamage(damage))
            {
                LastHitDamage = damage;
                LastHitTime = Time.time;
                Hit?.Invoke(damage);
            }
        }
        private void DrawBeam(Vector3 start, Vector3 end, Color color)
        {
            var beam = new GameObject("Shot beam").AddComponent<LineRenderer>();
            beam.gameObject.layer = 2;
            beam.sharedMaterial = settings.beamMaterial;
            beam.startColor = beam.endColor = color;
            beam.startWidth = 0.025f;
            beam.endWidth = 0.008f;
            beam.positionCount = 2;
            beam.SetPosition(0, start);
            beam.SetPosition(1, end);
            Destroy(beam.gameObject, settings.beamDuration);
        }
        private void UpdateHandVisuals()
        {
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
            int order = 0;
            if (fingerVisuals != null)
                for (int i = 0; i < fingerVisuals.Length; i++)
                {
                    bool active = Hand.IsActive(i);
                    if (!fingerVisuals[i]) continue;
                    fingerVisuals[i].localPosition = new Vector3(active ? -0.09f + order++ * 0.06f : -0.14f + i * 0.1f, active ? 0.04f : -0.04f, 0.18f - recoil * 0.04f);
                    fingerVisuals[i].localRotation = Quaternion.Euler(active ? 0 : 65, 0, 0);
                }
            if (thumbVisual) thumbVisual.localRotation = Quaternion.Euler(Hand.ThumbActive ? -30 : 35, -35, -20);
        }
    }
}


