using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HandShooter
{
    public sealed class HandWeapon : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public ShooterSettings settings;
        public PrototypePlayer player;
        public Camera viewCamera;
        public Transform muzzle;
        public Transform[] fingerVisuals;
        public Transform thumbVisual;
        [SerializeField] private bool startsUnequipped;
        [SerializeField] private bool startsWithoutThumb;
        public bool IsEquipped => gameObject.activeSelf;
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
        internal bool HasUnlimitedFuel => Hand != null && !Hand.ThumbUnlocked;
        public float FuelFraction => HasUnlimitedFuel ? 1f : tankCapacity <= 0f ? 0f : Fuel / tankCapacity;
        private bool CanReload => !HasUnlimitedFuel && !IsReloading && SpareTankCount > 0 && Fuel < tankCapacity;
        internal float GetFuelCost(ShotStats stats) => HasUnlimitedFuel ? 0f : settings.EvaluateFuelCost(stats, fuelPerDamage);
        public event Action<float> Hit;
        public event Action FuelChanged;
        public event Action<int> PresetSaved;
        public event Action<int, float> ShotFired;
        public event Action<int, DamageTarget, float, bool> DamageDealt;
        public event Action Reloaded;
        private int shotSequence;
        private float cooldownFraction;
        private bool requireRelease;
        private float recoil;
        private float reloadElapsed;
        private Vector3 handRestPosition;
        private Quaternion handRestRotation;
        private Camera handCamera;
        private List<Camera> cameraStack;
        private readonly List<GameObject> handMeshes = new List<GameObject>();
        private int excludedHandLayer;
        private void Awake()
        {
            Hand = new HandConfiguration(settings.initialActiveFingers, settings.initialThumbActive)
            {
                ThumbUnlocked = !startsWithoutThumb
            };
            Presets = new HandPresetStore();
            Fuel = tankCapacity;
            SpareTankCount = startingSpareTanks;
            handRestPosition = transform.localPosition;
            handRestRotation = transform.localRotation;
            if (startsUnequipped) gameObject.SetActive(false);
        }
        private void Start()
        {
            int handLayer = LayerMask.NameToLayer("FirstPersonHand");
            if (!viewCamera || handLayer < 0 ||
                !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset) ||
                (viewCamera.cullingMask & Physics.IgnoreRaycastLayer) == 0)
                return;

            foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mesh.gameObject.layer == IgnoreRaycastLayer && !mesh.GetComponent<Collider>())
                    handMeshes.Add(mesh.gameObject);
            }
            if (handMeshes.Count == 0) return;

            var viewData = viewCamera.GetUniversalAdditionalCameraData();
            cameraStack = viewData.cameraStack;
            if (cameraStack == null) return;

            // Overlay cameras clear depth by default, so only the hand's own depth affects its meshes.
            handCamera = new GameObject("Hand Camera").AddComponent<Camera>();
            handCamera.transform.SetParent(viewCamera.transform, false);
            handCamera.CopyFrom(viewCamera);
            handCamera.cullingMask = 1 << handLayer;
            handCamera.useOcclusionCulling = false;
            var handData = handCamera.GetUniversalAdditionalCameraData();
            handData.renderType = CameraRenderType.Overlay;
            handData.renderShadows = false;

            foreach (var mesh in handMeshes) mesh.layer = handLayer;
            excludedHandLayer = viewCamera.cullingMask & handCamera.cullingMask;
            viewCamera.cullingMask &= ~handCamera.cullingMask;
            cameraStack.Add(handCamera);
        }
        private void LateUpdate()
        {
            if (handCamera && viewCamera) handCamera.fieldOfView = viewCamera.fieldOfView;
        }
        private void OnDestroy()
        {
            if (cameraStack == null) return;

            cameraStack.Remove(handCamera);
            foreach (var mesh in handMeshes)
            {
                if (mesh) mesh.layer = IgnoreRaycastLayer;
            }
            if (viewCamera) viewCamera.cullingMask |= excludedHandLayer;
            if (handCamera) Destroy(handCamera.gameObject);
        }
        public void RequireTriggerRelease() { requireRelease = true; }
        internal void Equip()
        {
            UpdateHandVisuals();
            RequireTriggerRelease();
            gameObject.SetActive(true);
        }
        internal void AttachThumb()
        {
            Hand.ThumbUnlocked = true;
            Hand.ThumbActive = true;
            UpdateHandVisuals();
        }
        public bool SavePreset(int digit)
        {
            if (player.IsDead || !player.IsMenuOpen || player.IsPauseMenuOpen || !Presets.Save(digit, Hand)) return false;
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
            if (!IsEquipped || cooldownFraction > 0f || IsReloading || player.IsMenuOpen || Time.timeScale == 0f) return false;
            ShotStats stats = Stats;
            float fuelCost = GetFuelCost(stats);
            if (Fuel + 0.0001f < fuelCost) return false;
            Fuel = Mathf.Max(0f, Fuel - fuelCost);
            FuelChanged?.Invoke();
            int shotId = ++shotSequence;
            ShotFired?.Invoke(shotId, fuelCost);
            cooldownFraction = 1f;
            recoil = 1f;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int mask = settings.hitMask.value & ~4;
            float range = Mathf.Max(0f, settings.range);
            Vector3 aimPoint = ray.GetPoint(range);
            if (Physics.Raycast(ray, out RaycastHit aimHit, range, mask, QueryTriggerInteraction.Ignore)) aimPoint = aimHit.point;
            Vector3 start = muzzle.position;
            Vector3 direction = (aimPoint - start).normalized;
            if (stats.Hitscan)
            {
                Vector3 muzzleOffset = start - ray.origin;
                if (FindBeamObstruction(ray.origin, muzzleOffset.normalized, muzzleOffset.magnitude, mask,
                    out RaycastHit beamObstruction))
                    FireBeam(ray.origin, muzzleOffset.normalized, Mathf.Min(range, beamObstruction.distance), mask, stats.Damage, shotId);
                else
                    FireBeam(start, direction, range, mask, stats.Damage, shotId);
                return true;
            }
            // A muzzle poking through a wall must not shoot through it.
            if (Physics.Linecast(ray.origin, start, out RaycastHit obstruction, mask, QueryTriggerInteraction.Ignore))
            {
                var blockedProjectile = Instantiate(settings.projectilePrefab, ray.origin, Quaternion.LookRotation(direction));
                blockedProjectile.Initialize(direction, stats.Damage, settings, this, shotId);
                blockedProjectile.Detonate(obstruction.collider, obstruction.point, obstruction.normal);
                return true;
            }

            HandProjectile projectile = Instantiate(settings.projectilePrefab, start, Quaternion.LookRotation(direction));
            projectile.Initialize(direction, stats.Damage, settings, this, shotId);
            return true;
        }
        private static bool FindBeamObstruction(Vector3 origin, Vector3 direction, float distance, int mask,
            out RaycastHit obstruction)
        {
            obstruction = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.GetComponentInParent<DamageTarget>() && (!found || hit.distance < obstruction.distance))
                {
                    obstruction = hit;
                    found = true;
                }
            }
            return found;
        }
        private void FireBeam(Vector3 start, Vector3 direction, float range, int mask, float damage, int shotId)
        {
            Vector3 end = start + direction * range;
            var hits = Physics.RaycastAll(start, direction, range, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            var damaged = new HashSet<DamageTarget>();
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<DamageTarget>();
                if (!target)
                {
                    end = hit.point;
                    break;
                }
                if (damaged.Add(target)) Damage(target, damage, shotId);
            }
            DrawBeam(start, end, damage);
        }
        public bool ReloadFuel()
        {
            if (!CanReload) return false;
            SpareTankCount--;
            Fuel = tankCapacity;
            FuelChanged?.Invoke();
            Reloaded?.Invoke();
            return true;
        }
        private bool BeginReloadFuel()
        {
            if (!CanReload) return false;
            StartCoroutine(ReloadFuelAnimation(0f));
            return true;
        }
        private System.Collections.IEnumerator ReloadFuelAnimation(float elapsed)
        {
            IsReloading = true;
            float duration = Mathf.Max(0.1f, reloadDuration);
            reloadElapsed = elapsed;
            while (reloadElapsed < duration)
            {
                float dip = Mathf.Sin((reloadElapsed / duration) * Mathf.PI);
                transform.localPosition = handRestPosition + Vector3.down * (reloadHandDip * dip);
                transform.localRotation = handRestRotation * Quaternion.Euler(reloadHandTilt * dip, 0f, -5f * dip);
                yield return null;
                if (!player.IsMenuOpen) reloadElapsed += Time.deltaTime;
            }
            transform.localPosition = handRestPosition;
            transform.localRotation = handRestRotation;
            IsReloading = false;
            ReloadFuel();
        }

        internal SavedGame.Weapon CaptureState() => new SavedGame.Weapon
        {
            fuel = Fuel, spareTanks = SpareTankCount, mask = Hand.ActiveMask, thumb = Hand.ThumbActive,
            cooldown = cooldownFraction, reloading = IsReloading, reloadElapsed = reloadElapsed,
            unequipped = !IsEquipped, thumbUnlocked = Hand.ThumbUnlocked
        };

        internal void RestoreState(SavedGame.Weapon state)
        {
            Hand.ThumbUnlocked = !startsWithoutThumb || state.thumbUnlocked;
            Hand.TryApply(state.mask, state.thumb);
            Fuel = Mathf.Clamp(state.fuel, 0f, tankCapacity);
            SpareTankCount = Mathf.Max(0, state.spareTanks);
            cooldownFraction = Mathf.Clamp01(state.cooldown);
            UpdateHandVisuals();
            gameObject.SetActive(!state.unequipped);
            if (IsEquipped && state.reloading && !HasUnlimitedFuel) StartCoroutine(ReloadFuelAnimation(state.reloadElapsed));
            RequireTriggerRelease();
            FuelChanged?.Invoke();
        }
        public void AddFuelTank(int count = 1)
        {
            if (count <= 0) return;
            SpareTankCount += count;
            FuelChanged?.Invoke();
        }
        public void Damage(Collider collider, float damage, int shotId = -1)
        {
            Damage(collider.GetComponentInParent<DamageTarget>(), damage, shotId);
        }
        public void Damage(DamageTarget target, float damage, int shotId = -1)
        {
            float before = target ? target.Health : 0f;
            if (target && target.TakePlayerShotDamage(damage))
            {
                float receivedDamage = target.LastDamage;
                LastHitDamage = receivedDamage;
                LastHitTime = Time.time;
                Hit?.Invoke(receivedDamage);
                DamageDealt?.Invoke(shotId, target, Mathf.Min(before, receivedDamage), before > 0f && target.Health <= 0f);
            }
        }
        private void DrawBeam(Vector3 start, Vector3 end, float damage)
        {
            Color color = settings.rayColor;
            var beam = new GameObject("Shot beam").AddComponent<LineRenderer>();
            beam.gameObject.layer = IgnoreRaycastLayer;
            beam.sharedMaterial = settings.beamMaterial;
            beam.startColor = beam.endColor = color;
            beam.startWidth = 0.025f;
            beam.endWidth = 0.008f;
            beam.positionCount = 2;
            beam.SetPosition(0, start);
            beam.SetPosition(1, end);

            var endpoint = new GameObject("Beam endpoint", typeof(MeshFilter), typeof(MeshRenderer));
            endpoint.layer = IgnoreRaycastLayer;
            endpoint.transform.SetParent(beam.transform, false);
            endpoint.transform.position = end;
            // Compensate for perspective so distant hits still read as a small dot.
            float diameter = Mathf.Clamp(Vector3.Distance(viewCamera.transform.position, end) * 0.01f, 0.055f, 1f);
            // Scale volume with shot damage so the diameter grows more gradually.
            float damageScale = Mathf.Pow(damage, 1f / 3f);
            endpoint.transform.localScale = Vector3.one * (diameter * damageScale);
            endpoint.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var renderer = endpoint.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = settings.beamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(BaseColor, Color.Lerp(color, Color.white, 0.4f));
            renderer.SetPropertyBlock(properties);

            Destroy(beam, settings.beamDuration);
            Destroy(beam.gameObject, Mathf.Max(settings.beamDuration, 0.15f));
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
            if (thumbVisual)
            {
                thumbVisual.localRotation = Quaternion.Euler(Hand.ThumbActive ? -30 : 35, -35, -20);
                thumbVisual.gameObject.SetActive(Hand.ThumbUnlocked);
            }
        }
    }
}


