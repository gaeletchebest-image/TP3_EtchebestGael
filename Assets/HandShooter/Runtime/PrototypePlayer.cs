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
        public ShooterSettings settings;
        public Camera viewCamera;
        public HandWeapon weapon;
        public GameObject deathScreen;
        public Text deathHint;
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
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float previousTimeScale = 1f;
        private EnemyInspection inspection;
        private DamageTarget pinnedEnemy;
        private bool waitForControlRelease;
        private PauseMenu pauseMenu;
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
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
                if (deathHint) deathHint.enabled = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.6f;
                if (Time.frameCount != deathFrame && WasAnyKeyPressed(keyboard))
                {
                    if (pauseMenu && pauseMenu.TryLoadLatest()) Time.timeScale = 1f;
                    else RestartScene();
                }
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
            viewCamera.fieldOfView = settings.fieldOfView;
            if (IsMenuOpen || Cursor.lockState != CursorLockMode.Locked) return;
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - Time.deltaTime);
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
            Vector3 horizontalMovement;
            if (dashRemaining > 0f)
            {
                horizontalMovement = dashDirection * Mathf.Max(0.1f, settings.dashSpeed);
                dashRemaining = Mathf.Max(0f, dashRemaining - Time.deltaTime);
            }
            else
            {
                horizontalMovement = (transform.right * move.x + transform.forward * move.y) * settings.moveSpeed;
            }
            controller.Move((horizontalMovement + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void StartDash(Vector2 move)
        {
            if (!DashUnlocked || dashRemaining > 0f || dashCooldownRemaining > 0f || move.sqrMagnitude <= 0f) return;
            dashDirection = (transform.right * move.x + transform.forward * move.y).normalized;
            dashRemaining = Mathf.Max(0.01f, settings.dashDuration);
            dashCooldownRemaining = Mathf.Max(0f, settings.dashCooldown);
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
                        pinnedEnemy = target;

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

        internal void UnlockDash() => DashUnlocked = true;

        internal SavedGame.Player CaptureState() => new SavedGame.Player
        {
            position = transform.position, rotation = transform.rotation, pitch = pitch,
            verticalVelocity = verticalVelocity, dashDirection = dashDirection, dashRemaining = dashRemaining,
            dashCooldown = dashCooldownRemaining, dashLocked = !DashUnlocked, weapon = weapon.CaptureState()
        };

        internal void RestoreState(SavedGame.Player state)
        {
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
            if (open && !IsMenuOpen) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            if (!open && IsMenuOpen) Time.timeScale = previousTimeScale;
            IsMenuOpen = open;
            IsPauseMenuOpen = open && pause;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (weapon) weapon.RequireTriggerRelease();
        }
        public void Die(string cause = "Enemigo")
        {
            if (IsDead || IsLoadingSave) return;
            IsDead = true;
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
            if (deathScreen) deathScreen.SetActive(true);
            else Debug.LogError("Player death screen is not assigned in the scene.", this);
        }
        private void RestartScene()
        {
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
