using UnityEngine;
using UnityEngine.InputSystem;

namespace HandShooter
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class PrototypePlayer : MonoBehaviour
    {
        public ShooterSettings settings;
        public Camera viewCamera;
        public HandWeapon weapon;
        public bool IsMenuOpen { get; private set; }
        public float DashCooldownFraction => settings == null || settings.dashCooldown <= 0f
            ? 1f : Mathf.Clamp01(1f - dashCooldownRemaining / settings.dashCooldown);
        private CharacterController controller;
        private float pitch, verticalVelocity;
        private Vector3 dashDirection;
        private float dashRemaining;
        private float dashCooldownRemaining;
        private float previousTimeScale = 1f;
        private void Awake() { controller = GetComponent<CharacterController>(); }
        private void Start() { SetMenuOpen(false); FuelTankPickup.SpawnDefaultPickups(transform); }
        private void Update()
        {
            if (!settings || !viewCamera) return;
            var keyboard = Keyboard.current;
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
            if (open && !IsMenuOpen) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            if (!open && IsMenuOpen) Time.timeScale = previousTimeScale;
            IsMenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (weapon) weapon.RequireTriggerRelease();
        }
        private void OnApplicationFocus(bool focused) { if (!focused && Application.isPlaying) SetMenuOpen(true); }
        private void OnDisable()
        {
            if (IsMenuOpen) Time.timeScale = previousTimeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
