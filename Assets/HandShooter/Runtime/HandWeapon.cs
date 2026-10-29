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
        public HandPresetStore Presets { get; private set; }
        public ShotStats Stats => settings.Evaluate(Hand);
        public float CooldownRemaining => cooldownFraction * Stats.Interval;
        public float LastHitDamage { get; private set; }
        public float LastHitTime { get; private set; } = -100f;
        [Header("Combustible")]
        [Min(0.01f)] public float tankCapacity = 100f;
        [Min(0.01f)] public float fuelPerDamage = 1f;
        [Min(0)] public int startingSpareTanks = 2;
        [Header("Animación de recarga")]
        [Min(0.1f)] public float reloadDuration = 1.25f;
        [Min(0f)] public float reloadHandDip = 0.16f;
        [Min(0f)] public float reloadHandTilt = 32f;
        public float Fuel { get; private set; }
        public int SpareTankCount { get; private set; }
        public bool IsReloading { get; private set; }
        public float FuelFraction => tankCapacity <= 0f ? 0f : Fuel / tankCapacity;
        public event Action<float> Hit;
        public event Action FuelChanged;
        public event Action<int> PresetSaved;
        private float cooldownFraction;
        private bool requireRelease;
        private float recoil;
        private Vector3 handRestPosition;
        private Quaternion handRestRotation;
        private void Awake()
        {
            Hand = new HandConfiguration(settings.initialActiveFingers, settings.initialThumbActive);
            Presets = new HandPresetStore();
            Fuel = tankCapacity;
            SpareTankCount = startingSpareTanks;
            handRestPosition = transform.localPosition;
            handRestRotation = transform.localRotation;
        }
        public void RequireTriggerRelease() { requireRelease = true; }
        public bool SavePreset(int digit)
        {
            if (player.IsDead || !player.IsMenuOpen || !Presets.Save(digit, Hand)) return false;
            PresetSaved?.Invoke(digit);
            return true;
        }
        public bool TryEquipPreset(int digit)
        {
            if (player.IsDead || player.IsMenuOpen || Time.timeScale <= 0f ||
                Cursor.lockState != CursorLockMode.Locked) return false;
            HandPreset preset = Presets.Get(digit);
            // Only the hand changes: fuel, reload and cooldown progress are preserved.
            return preset.IsSaved && Hand.TryApply(preset.ActiveMask, preset.ThumbActive);
        }
        private void UpdatePresetInput(Keyboard keyboard)
        {
            if (keyboard == null || player.IsDead) return;
            for (int digit = 0; digit < HandPresetStore.SlotCount; digit++)
            {
                Key topRow = digit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + digit - 1);
                Key numpad = (Key)((int)Key.Numpad0 + digit);
                if (!keyboard[topRow].wasPressedThisFrame && !keyboard[numpad].wasPressedThisFrame) continue;
                if (player.IsMenuOpen) SavePreset(digit);
                else TryEquipPreset(digit);
            }
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            UpdatePresetInput(keyboard);
            cooldownFraction = Mathf.Max(0f, cooldownFraction - Time.deltaTime / Stats.Interval);
            UpdateHandVisuals();
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && !player.IsMenuOpen && Cursor.lockState == CursorLockMode.Locked)
                BeginReloadFuel();
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) requireRelease = false;
            if (player.IsMenuOpen || Cursor.lockState != CursorLockMode.Locked || requireRelease) return;
            if (settings.automaticFire ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame) TryFire();
        }
        public bool TryFire()
        {
            if (cooldownFraction > 0f || IsReloading || player.IsMenuOpen || Time.timeScale == 0f) return false;
            ShotStats stats = Stats;
            float fuelCost = settings.EvaluateFuelCost(stats, fuelPerDamage);
            if (Fuel + 0.0001f < fuelCost) return false;
            Fuel = Mathf.Max(0f, Fuel - fuelCost);
            FuelChanged?.Invoke();
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
        public bool ReloadFuel()
        {
            if (IsReloading || SpareTankCount <= 0 || Fuel >= tankCapacity) return false;
            SpareTankCount--;
            Fuel = tankCapacity;
            FuelChanged?.Invoke();
            return true;
        }
        private bool BeginReloadFuel()
        {
            if (IsReloading || SpareTankCount <= 0 || Fuel >= tankCapacity) return false;
            StartCoroutine(ReloadFuelAnimation());
            return true;
        }
        private System.Collections.IEnumerator ReloadFuelAnimation()
        {
            IsReloading = true;
            float duration = Mathf.Max(0.1f, reloadDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                float dip = Mathf.Sin((elapsed / duration) * Mathf.PI);
                transform.localPosition = handRestPosition + Vector3.down * (reloadHandDip * dip);
                transform.localRotation = handRestRotation * Quaternion.Euler(reloadHandTilt * dip, 0f, -5f * dip);
                yield return null;
            }
            transform.localPosition = handRestPosition;
            transform.localRotation = handRestRotation;
            IsReloading = false;
            ReloadFuel();
        }
        public void AddFuelTank(int count = 1)
        {
            if (count <= 0) return;
            SpareTankCount += count;
            FuelChanged?.Invoke();
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


