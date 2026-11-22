using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HandShooter.Tests
{
    public sealed class HandControlsPlayTests
    {
        private PrototypePlayer player;
        private HandWeapon weapon;
        private HandMenu menu;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
#endif
        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/HandShooterPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
#endif
            yield return null;
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            weapon = player.weapon;
            menu = Object.FindFirstObjectByType<HandMenu>();
            player.enabled = false;
            weapon.enabled = false; // Advance input explicitly, independent of OS focus.
            player.SetMenuOpen(false);
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            foreach (var key in keyboard.allKeys) _ = key.wasPressedThisFrame;
            InputSystem.Update();
        }
        [TearDown] public void Cleanup()
        {
            if (keyboard?.added == true) InputSystem.RemoveDevice(keyboard);
            if (mouse?.added == true) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        private void Input(Key key = Key.None, float scroll = 0f)
        {
            InputSystem.QueueStateEvent(keyboard, key == Key.None ? new KeyboardState() : new KeyboardState(key));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, scroll) });
            InputSystem.Update();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            if (key != Key.None)
                Assert.That(keyboard[key].wasPressedThisFrame, Is.True, "Synthetic key must reach the input state.");
            typeof(HandWeapon).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
        }
        [TestCase(Key.Digit1, WeaponMode.Ray)]
        [TestCase(Key.Digit2, WeaponMode.Projectile)]
        [TestCase(Key.Digit3, WeaponMode.Grenade)]
        [TestCase(Key.Numpad1, WeaponMode.Ray)]
        [TestCase(Key.Numpad2, WeaponMode.Projectile)]
        [TestCase(Key.Numpad3, WeaponMode.Grenade)]
        public void NumericKeysSelectExactlyOneModeWithoutChangingPower(Key key, WeaponMode mode)
        {
            weapon.Hand.TryApply(7, mode == WeaponMode.Ray ? WeaponMode.Grenade : WeaponMode.Ray);
            Input(key);
            Assert.That(weapon.Stats.Mode, Is.EqualTo(mode));
            Assert.That(weapon.PowerLevel, Is.EqualTo(3));
            Input(key);
            Assert.That(weapon.Stats.Mode, Is.EqualTo(mode), "Selecting the current mode must not cycle it.");
        }
        [Test] public void ScrollTraversesExistingLevelsAndClampsAtBothEnds()
        {
            weapon.Hand.TryApply(1, WeaponMode.Projectile);
            for (int level = 2; level <= 4; level++)
            {
                Input(scroll: 120f);
                Assert.That(weapon.PowerLevel, Is.EqualTo(level));
                Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Projectile));
                Assert.That(weapon.Stats.Damage, Is.EqualTo(weapon.settings.Evaluate(new HandConfiguration(level, false)).Damage));
            }
            Input(scroll: 120f);
            Assert.That(weapon.PowerLevel, Is.EqualTo(4));
            for (int level = 3; level >= 1; level--)
            {
                Input(scroll: -120f);
                Assert.That(weapon.PowerLevel, Is.EqualTo(level));
            }
            Input(scroll: -120f);
            Assert.That(weapon.PowerLevel, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator MenuHasPowerMeterExclusiveModesAndNoPresets()
        {
            player.SetMenuOpen(true);
            yield return null;
            Transform panel = menu.MenuRoot.transform.Find("Hand Menu Panel");
            Assert.That(panel.Find("Preset Button 1"), Is.Null);
            Assert.That(panel.Find("Finger Button 1"), Is.Null);
            panel.Find("Power Segment 4").GetComponent<Button>().onClick.Invoke();
            Assert.That(weapon.PowerLevel, Is.EqualTo(4));
            for (int mode = 0; mode < 3; mode++)
            {
                var button = panel.Find($"Mode Button {mode + 1}").GetComponent<Button>();
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
                button.onClick.Invoke();
                yield return null;
                int active = 0;
                for (int i = 1; i <= 3; i++)
                    if (panel.Find($"Mode Button {i}").GetComponentInChildren<Text>().text.EndsWith("\nACTIVO")) active++;
                Assert.That(active, Is.EqualTo(1));
                Assert.That(weapon.Stats.Mode, Is.EqualTo((WeaponMode)mode));
            }
            Assert.That(panel.Find("Power Label").GetComponent<Text>().text, Does.Contain("4 / 4"));
            Assert.That(Time.timeScale, Is.Zero);
            Input(Key.Digit1, -120f);
            Assert.That(weapon.PowerLevel, Is.EqualTo(3));
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
            Assert.That(weapon.TryFire(), Is.False);
            player.SetMenuOpen(false);
            yield return null;
            Transform hud = menu.canvas.transform.Find("Weapon Status");
            Assert.That(hud.gameObject.activeSelf, Is.True);
            Assert.That(hud.Find("Power Label").GetComponent<Text>().text, Does.Contain("3/4"));
        }
        [Test] public void ConfigurationChangesPreserveFuelCooldownAndReload()
        {
            Assert.That(weapon.TryFire(), Is.True);
            float fuel = weapon.Fuel;
            float cooldown = weapon.CooldownRemaining;
            Assert.That(weapon.SetPowerLevel(4), Is.True);
            Assert.That(weapon.SelectMode(WeaponMode.Grenade), Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(fuel));
            Assert.That(weapon.CooldownRemaining, Is.EqualTo(cooldown));
            Assert.That(weapon.TryFire(), Is.False);
            weapon.AddFuelTank();
            weapon.SendMessage("BeginReloadFuel");
            Assert.That(weapon.IsReloading, Is.True);
            int tanks = weapon.SpareTankCount;
            weapon.SetPowerLevel(2);
            weapon.SelectMode(WeaponMode.Projectile);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(fuel));
            Assert.That(weapon.SpareTankCount, Is.EqualTo(tanks));
        }
        [Test] public void UnlockedCursorPauseDeathAndUnequippedWeaponBlockInput()
        {
            Cursor.lockState = CursorLockMode.None;
            Input(Key.Digit2, 120f);
            AssertUnchanged();
            Cursor.lockState = CursorLockMode.Locked;
            Time.timeScale = 0f;
            Input(Key.Digit2, 120f);
            AssertUnchanged();
            Time.timeScale = 1f;
            player.SetMenuOpen(true);
            typeof(PrototypePlayer).GetProperty("IsPauseMenuOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, true);
            Input(Key.Digit2, 120f);
            AssertUnchanged();
            player.SetMenuOpen(false);
            weapon.gameObject.SetActive(false);
            Input(Key.Digit2, 120f);
            AssertUnchanged();
            weapon.gameObject.SetActive(true);
            player.Die();
            Input(Key.Digit2, 120f);
            AssertUnchanged();
        }
        private void AssertUnchanged()
        {
            Assert.That(weapon.PowerLevel, Is.EqualTo(1));
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
        }
        [UnityTest] public IEnumerator LockedModesStayVisibleAndCannotBeSelected()
        {
            player.SetMenuOpen(true);
            typeof(HandConfiguration).GetProperty("ThumbUnlocked").SetValue(weapon.Hand, false);
            Input(Key.Digit2);
            Input(Key.Digit3);
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
            yield return null;
            var panel = menu.MenuRoot.transform.Find("Hand Menu Panel");
            Assert.That(panel.Find("Mode Button 2").GetComponent<Button>().interactable, Is.False);
            Assert.That(panel.Find("Mode Button 3").GetComponent<Button>().interactable, Is.False);
            typeof(HandConfiguration).GetProperty("ThumbUnlocked").SetValue(weapon.Hand, true);
            var settings = Object.Instantiate(weapon.settings);
            weapon.settings = settings;
            settings.grenadeEnabled = false;
            try
            {
                Input(Key.Digit3);
                yield return null;
                Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
                Assert.That(panel.Find("Mode Button 2").GetComponent<Button>().interactable, Is.True);
                Assert.That(panel.Find("Mode Button 3").GetComponent<Button>().interactable, Is.False);
                Input(Key.Digit2);
                Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Projectile));
            }
            finally { Object.Destroy(settings); }
        }
        [Test] public void FormerPresetKeysDoNotModifyConfiguration()
        {
            foreach (Key key in new[] { Key.Digit4, Key.Digit7, Key.Digit9, Key.Digit0, Key.Numpad4, Key.Numpad0 })
            {
                Input(key, 0f);
                AssertUnchanged();
            }
        }
    }
}
