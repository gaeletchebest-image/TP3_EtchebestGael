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
        public bool IsDead { get; private set; }
        public bool spawnDefaultFuelPickups = true;
        public event System.Action<string> Died;
        public float DashCooldownFraction => settings == null || settings.dashCooldown <= 0f
            ? 1f : Mathf.Clamp01(1f - dashCooldownRemaining / settings.dashCooldown);
        private CharacterController controller;
        private float pitch, verticalVelocity;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float previousTimeScale = 1f;
        private void Awake() { controller = GetComponent<CharacterController>(); }
        private void Start() { SetMenuOpen(false); if (spawnDefaultFuelPickups) FuelTankPickup.SpawnDefaultPickups(transform); }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
                keyboard.rKey.wasPressedThisFrame)
            {
                RestartScene();
                return;
            }
            if (IsDead)
            {
                if (deathHint) deathHint.enabled = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.6f;
                if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) RestartScene();
                return;
            }
            if (!settings || !viewCamera) return;
            if (keyboard != null && (keyboard[settings.menuKey].wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                SetMenuOpen(!IsMenuOpen);
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
            if (dashRemaining <= 0f && dashCooldownRemaining <= 0f && move.sqrMagnitude > 0f &&
                keyboard != null && keyboard[settings.dashKey].wasPressedThisFrame)
            {
                dashDirection = (transform.right * move.x + transform.forward * move.y).normalized;
                dashRemaining = Mathf.Max(0.01f, settings.dashDuration);
                dashCooldownRemaining = Mathf.Max(0f, settings.dashCooldown);
            }
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
        public void SetMenuOpen(bool open)
        {
            if (IsDead) return;
            if (open && !IsMenuOpen) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            if (!open && IsMenuOpen) Time.timeScale = previousTimeScale;
            IsMenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (weapon) weapon.RequireTriggerRelease();
        }
        public void Die(string cause = "Enemigo")
        {
            if (IsDead) return;
            IsDead = true;
            Died?.Invoke(cause);
            IsMenuOpen = false;
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
        private void OnApplicationFocus(bool focused) { if (!focused && Application.isPlaying && !IsDead) SetMenuOpen(true); }
        private void OnDisable()
        {
            if (IsMenuOpen) Time.timeScale = previousTimeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
