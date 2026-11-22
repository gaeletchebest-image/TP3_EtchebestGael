using HandShooter.CRT;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandShooter
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        private const float DashFieldOfViewBoost = 9f;
        private const float DashViewAttack = 0.045f;
        private const float DashViewRelease = 0.16f;

        public ShooterSettings settings;
        public Camera viewCamera;
        public HandWeapon weapon;
        public GameObject deathScreen;
        public Text deathHint;
        [SerializeField, Tooltip("Transición del CRT al morir.")]
        private CRTDeathTransition deathTransition;
        public bool IsMenuOpen { get; private set; }
        internal bool IsPauseMenuOpen { get; private set; }
        internal bool IsLoadingSave => pauseMenu && pauseMenu.IsLoading;
        public bool IsDead { get; private set; }
        public bool spawnDefaultFuelPickups = true;
        [SerializeField] private bool startsWithoutDash;
        public bool DashUnlocked { get; private set; }
        public event System.Action<string> Died;
        public float DashCooldownFraction => settings == null || settings.dashCooldown <= 0f
            ? 1f : Mathf.Clamp01(1f - dashCooldownRemaining / settings.dashCooldown);
        private CharacterController controller;
        private int deathFrame;
        private float pitch, verticalVelocity;
        private float footstepDistance;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float dashViewStrength, dashStreakTravel;
        private float previousTimeScale = 1f;
        private EnemyInspection inspection;
        private DamageTarget pinnedEnemy;
        private bool waitForControlRelease;
        private PauseMenu pauseMenu;
        private PlayerDeathPresentation deathPresentation;
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            ShooterAudio.Initialize(this);
            DashUnlocked = !startsWithoutDash;
            if (GameSaveSession.CanSaveScene(gameObject.scene))
                pauseMenu = gameObject.AddComponent<PauseMenu>();
        }
        private void Start()
        {
            bool restoring = GameSaveSession.IsPendingFor(gameObject.scene);
            SetMenu(restoring, pause: restoring);
            if (spawnDefaultFuelPickups) FuelTankPickup.SpawnDefaultPickups(transform);
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (IsLoadingSave) return;
            if (IsDead)
            {
                if (deathPresentation != null && !deathPresentation.Tick()) return;
                if (deathPresentation == null && deathHint) deathHint.enabled = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.6f;
                if (Time.frameCount != deathFrame && WasAnyKeyPressed(keyboard))
                {
                    if (pauseMenu && pauseMenu.TryLoadLatest()) Time.timeScale = 1f;
                    else RestartScene();
                }
                return;
            }
            if (pauseMenu && pauseMenu.IsAudioOpen)
            {
                if (keyboard?.escapeKey.wasPressedThisFrame == true) pauseMenu.CloseAudio();
                return;
            }
            if (pauseMenu && pauseMenu.IsConfirmingDelete)
            {
                if (keyboard?.escapeKey.wasPressedThisFrame == true) pauseMenu.CancelDelete();
                return;
            }
            if (pauseMenu && keyboard?.f9Key.wasPressedThisFrame == true)
            {
                if (pauseMenu.TryLoadLatest()) SetMenu(true, pause: true);
                return;
            }
            if (IsControlHeld(keyboard) && keyboard.rKey.wasPressedThisFrame)
            {
                RestartScene();
                return;
            }
            if (pauseMenu && keyboard?.f5Key.wasPressedThisFrame == true)
            {
                pauseMenu.Save();
                return;
            }
            if (!settings || !viewCamera) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && pauseMenu)
                SetMenu(!IsPauseMenuOpen, pause: true);
            else if (keyboard != null && (keyboard[settings.menuKey].wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                SetMenuOpen(!IsMenuOpen || IsPauseMenuOpen);
            if (IsMenuOpen || Cursor.lockState != CursorLockMode.Locked)
            {
                SetDashView(0f);
                return;
            }
            float previousCooldown = dashCooldownRemaining;
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - Time.deltaTime);
            if (previousCooldown > 0f && dashCooldownRemaining == 0f) ShooterAudio.Local(SoundCue.DashReady);
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * settings.mouseSensitivity;
                transform.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -settings.pitchLimit, settings.pitchLimit);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                move.x = (keyboard[settings.rightKey].isPressed ? 1 : 0) - (keyboard[settings.leftKey].isPressed ? 1 : 0);
                move.y = (keyboard[settings.forwardKey].isPressed ? 1 : 0) - (keyboard[settings.backwardKey].isPressed ? 1 : 0);
            }
            move = Vector2.ClampMagnitude(move, 1f);
            if (keyboard != null && keyboard[settings.dashKey].wasPressedThisFrame) StartDash(move);
            verticalVelocity = controller.isGrounded && verticalVelocity < 0 ? -2f : verticalVelocity + settings.gravity * Time.deltaTime;
            bool dashing = dashRemaining > 0f;
            float rampDuration = dashing ? DashViewAttack : DashViewRelease;
            SetDashView(Mathf.MoveTowards(dashViewStrength, dashing ? 1f : 0f, Time.deltaTime / rampDuration));
            Vector3 horizontalMovement;
            if (dashing)
            {
                horizontalMovement = dashDirection * Mathf.Max(0.1f, settings.dashSpeed);
                dashRemaining = Mathf.Max(0f, dashRemaining - Time.deltaTime);
            }
            else
            {
                horizontalMovement = (transform.right * move.x + transform.forward * move.y) * settings.moveSpeed;
            }
            Vector3 beforeMove = transform.position;
            bool wasGrounded = controller.isGrounded;
            float fallSpeed = verticalVelocity;
            controller.Move((horizontalMovement + Vector3.up * verticalVelocity) * Time.deltaTime);
            if (!wasGrounded && controller.isGrounded && fallSpeed < -4f) ShooterAudio.Local(SoundCue.Land);
            Vector3 walked = transform.position - beforeMove;
            walked.y = 0f;
            if (controller.isGrounded && !dashing && move.sqrMagnitude > 0f)
            {
                footstepDistance += walked.magnitude;
                if (footstepDistance >= 1.65f)
                {
                    footstepDistance %= 1.65f;
                    bool metal = Physics.Raycast(transform.position + Vector3.up * 0.15f, Vector3.down,
                        out var floor, 1.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        floor.collider.GetComponentInParent<CargoLift>();
                    ShooterAudio.Local(metal ? SoundCue.StepMetal : SoundCue.StepSolid);
                }
            }
            else footstepDistance = 0f;
        }

        private void StartDash(Vector2 move)
        {
            if (!DashUnlocked || dashRemaining > 0f || dashCooldownRemaining > 0f || move.sqrMagnitude <= 0f) return;
            ShooterAudio.Local(SoundCue.Dash);
            dashDirection = (transform.right * move.x + transform.forward * move.y).normalized;
            dashRemaining = Mathf.Max(0.01f, settings.dashDuration);
            dashCooldownRemaining = Mathf.Max(0f, settings.dashCooldown);
        }

        private void SetDashView(float strength)
        {
            float eased = Mathf.SmoothStep(0f, 1f, strength);
            if (viewCamera)
            {
                if (settings) viewCamera.fieldOfView = settings.fieldOfView + DashFieldOfViewBoost * eased;
                if ((strength > 0f || dashViewStrength > 0f) && viewCamera.TryGetComponent<CRTCameraEffect>(out var crt))
                {
                    Vector3 direction = viewCamera.transform.InverseTransformDirection(dashDirection);
                    dashStreakTravel += Time.deltaTime * (direction.z < -0.25f ? -2.7f : 2.7f);
                    crt.SetDashMotion(eased, direction.x * 0.44f, dashStreakTravel);
                }
            }
            dashViewStrength = strength;
            if (strength <= 0f) dashStreakTravel = 0f;
        }

        private static bool IsControlHeld(Keyboard keyboard) =>
            keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

        private static bool WasAnyKeyPressed(Keyboard keyboard)
        {
            if (keyboard == null) return false;
            foreach (var key in keyboard.allKeys)
                if (key.wasPressedThisFrame) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (IsDead || IsMenuOpen || Cursor.lockState != CursorLockMode.Locked || !settings || !viewCamera)
            {
                inspection?.Hide();
                return;
            }

            var keyboard = Keyboard.current;
            bool controlHeld = IsControlHeld(keyboard);
            if (!controlHeld) waitForControlRelease = false;
            if (CanInspect(pinnedEnemy))
            {
                if (keyboard != null && (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame))
                {
                    pinnedEnemy = null;
                    ShooterAudio.Ui(SoundCue.UiCancel);
                    waitForControlRelease = controlHeld;
                    inspection.Hide();
                    return;
                }

                if (pinnedEnemy.HasLineOfSight(viewCamera)) inspection.Show(pinnedEnemy, true);
                else inspection.Hide();
                return;
            }
            pinnedEnemy = null;

            if (controlHeld && !waitForControlRelease)
            {
                var target = GetAimedEnemy();
                if (CanInspect(target))
                {
                    if (Mouse.current?.rightButton.wasPressedThisFrame == true)
                        { pinnedEnemy = target; ShooterAudio.Ui(SoundCue.Pin); }

                    inspection ??= new EnemyInspection(transform);
                    inspection.Show(target, pinnedEnemy != null);
                    return;
                }
            }

            inspection?.Hide();
        }

        private DamageTarget GetAimedEnemy()
        {
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            return Physics.Raycast(ray, out RaycastHit hit, viewCamera.farClipPlane,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<DamageTarget>()
                : null;
        }

        private static bool CanInspect(DamageTarget target) =>
            target && target.isActiveAndEnabled && target.IsEnemy && target.Health > 0f;

        internal void UnlockDash()
        {
            if (DashUnlocked) return;
            DashUnlocked = true;
            ShooterAudio.Local(SoundCue.Unlock);
        }

        internal SavedGame.Player CaptureState() => new SavedGame.Player
        {
            position = transform.position, rotation = transform.rotation, pitch = pitch,
            verticalVelocity = verticalVelocity, dashDirection = dashDirection, dashRemaining = dashRemaining,
            dashCooldown = dashCooldownRemaining, dashLocked = !DashUnlocked, weapon = weapon.CaptureState()
        };

        internal void RestoreState(SavedGame.Player state)
        {
            SetDashView(0f);
            controller.enabled = false;
            transform.SetPositionAndRotation(state.position, state.rotation);
            controller.enabled = true;
            pitch = state.pitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            verticalVelocity = state.verticalVelocity;
            dashDirection = state.dashDirection;
            DashUnlocked = !startsWithoutDash || !state.dashLocked;
            dashRemaining = DashUnlocked ? state.dashRemaining : 0f;
            dashCooldownRemaining = DashUnlocked ? state.dashCooldown : 0f;
            weapon.RestoreState(state.weapon);
        }

        public void SetMenuOpen(bool open)
        {
            if (!open || (weapon && weapon.IsEquipped)) SetMenu(open, pause: false);
        }

        private void SetMenu(bool open, bool pause)
        {
            if (IsDead) return;
            if (open != IsMenuOpen) ShooterAudio.Ui(open ? SoundCue.MenuOpen : SoundCue.MenuClose);
            if (open && !IsMenuOpen) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            if (!open && IsMenuOpen) Time.timeScale = previousTimeScale;
            IsMenuOpen = open;
            IsPauseMenuOpen = open && pause;
            if (open) SetDashView(0f);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (weapon) weapon.RequireTriggerRelease();
        }
        public void Die(string cause = "Enemigo") => Die(cause, null);

        internal void Die(string cause, Transform source, Vector3? origin = null)
        {
            if (IsDead || IsLoadingSave) return;
            if (!origin.HasValue && source) origin = source.position;
            string detail = PlayerDeathPresentation.Describe(cause, viewCamera, origin);
            IsDead = true;
            ShooterAudio.PlayerDied(cause);
            SetDashView(0f);
            deathFrame = Time.frameCount;
            pinnedEnemy = null;
            inspection?.Hide();
            Died?.Invoke(cause);
            IsMenuOpen = false;
            IsPauseMenuOpen = false;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (weapon) weapon.RequireTriggerRelease();
            if (deathScreen)
            {
                var subtitle = deathScreen.transform.Find("Death Subtitle");
                if (subtitle && subtitle.TryGetComponent<Text>(out var label)) label.text = detail;
                deathScreen.SetActive(true);
            }

            if (isActiveAndEnabled && viewCamera)
                deathPresentation = new PlayerDeathPresentation(this, cause, detail, source, origin, deathTransition);
            else if (!deathScreen) Debug.LogError("Player death presentation needs a view camera.", this);
        }
        private void RestartScene()
        {
            ShooterAudio.OnNextScene(SoundCue.Restart);
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Application.isPlaying && !IsDead && !IsMenuOpen)
                SetMenu(true, pause: pauseMenu != null);
        }
        private void OnDisable()
        {
            SetDashView(0f);
            deathPresentation?.Dispose();
            deathPresentation = null;
            pinnedEnemy = null;
            waitForControlRelease = false;
            inspection?.Hide();
            if (IsMenuOpen) Time.timeScale = previousTimeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        private void OnDestroy() => inspection?.Destroy();
    }
}
