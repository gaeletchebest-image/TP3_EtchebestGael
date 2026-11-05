using System;
using System.Collections;
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
    public sealed class HandPresetPlayTests
    {
        private PrototypePlayer player;
        private HandWeapon weapon;
        private HandMenu menu;
        private Keyboard keyboard;
        private string prefix;
        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            weapon = player.weapon;
            menu = Object.FindFirstObjectByType<HandMenu>();
            player.SetMenuOpen(false);
            player.enabled = false;
            weapon.enabled = false; // Input is advanced explicitly, independent of OS focus.
            prefix = "HandShooter.Tests.Presets." + Guid.NewGuid() + ".";
            typeof(HandWeapon).GetProperty(nameof(HandWeapon.Presets)).SetValue(weapon, new HandPresetStore(prefix));
            keyboard = InputSystem.AddDevice<Keyboard>();
        }
        [TearDown] public void Cleanup()
        {
            if (keyboard?.added == true) InputSystem.RemoveDevice(keyboard);
            if (prefix != null)
                for (int digit = 0; digit < HandPresetStore.SlotCount; digit++) PlayerPrefs.DeleteKey(prefix + digit);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        private void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            keyboard.MakeCurrent();
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True, "Synthetic key must reach the game input state.");
            weapon.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
        [UnityTest] public IEnumerator MenuButtonsSaveTheExactHandAndShowFeedbackWhilePaused()
        {
            player.SetMenuOpen(true);
            weapon.Hand.TryApply(12, false);
            yield return null;
            var button = menu.MenuRoot.transform.Find("Hand Menu Panel/Preset Button 7").GetComponent<Button>();
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            button.onClick.Invoke();
            yield return null;
            Assert.That(weapon.Presets.Get(7).ActiveMask, Is.EqualTo(12));
            Assert.That(weapon.Presets.Get(7).ThumbActive, Is.False);
            Assert.That(button.GetComponentInChildren<Text>().text, Does.Contain("34 · PROY."));
            Assert.That(menu.MenuRoot.transform.Find("Hand Menu Panel/Menu Footer").GetComponent<Text>().text,
                Does.Contain("GUARDADO EN [7]"));
            weapon.Hand.TryApply(1, true);
            Assert.That(weapon.Presets.Get(7).ActiveMask, Is.EqualTo(12));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(weapon.TryEquipPreset(7), Is.False);
        }
        [TestCase(Key.Digit1, 1)] [TestCase(Key.Digit9, 9)] [TestCase(Key.Digit0, 0)]
        [TestCase(Key.Numpad1, 1)] [TestCase(Key.Numpad9, 9)] [TestCase(Key.Numpad0, 0)]
        public void NumericKeysSaveInMenuAndEquipDuringGameplay(Key key, int digit)
        {
            player.SetMenuOpen(true);
            weapon.Hand.TryApply(6, false);
            Press(key);
            Assert.That(weapon.Presets.Get(digit).ActiveMask, Is.EqualTo(6));
            weapon.Hand.TryApply(1, true);
            player.SetMenuOpen(false);
            Press(key);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(6));
            Assert.That(weapon.Hand.ThumbActive, Is.False);
            Assert.That(weapon.Stats.Hitscan, Is.False);
        }
        [Test] public void EquippingPreservesFuelAndCooldownAndEmptySlotsDoNothing()
        {
            player.SetMenuOpen(true);
            weapon.Hand.TryApply(15, false);
            weapon.SavePreset(4);
            weapon.Hand.TryApply(1, true);
            player.SetMenuOpen(false);
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(), Is.True);
            float fuel = weapon.Fuel;
            float progress = weapon.CooldownRemaining / weapon.Stats.Interval;
            Assert.That(weapon.TryEquipPreset(4), Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(fuel));
            Assert.That(weapon.CooldownRemaining / weapon.Stats.Interval, Is.EqualTo(progress).Within(0.0001f));
            Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.TryEquipPreset(8), Is.False);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(15));
            Assert.That(weapon.Hand.ThumbActive, Is.False);
        }
        [Test] public void EquippingDuringReloadKeepsTheReloadInProgress()
        {
            player.SetMenuOpen(true);
            weapon.Hand.TryApply(3, false);
            weapon.SavePreset(5);
            weapon.Hand.TryApply(1, true);
            player.SetMenuOpen(false);
            Assert.That(weapon.TryFire(), Is.True);
            weapon.SendMessage("BeginReloadFuel");
            Assert.That(weapon.IsReloading, Is.True);
            float fuel = weapon.Fuel;
            int tanks = weapon.SpareTankCount;
            Assert.That(weapon.TryEquipPreset(5), Is.True);
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(fuel));
            Assert.That(weapon.SpareTankCount, Is.EqualTo(tanks));
        }
        [Test] public void UnlockedCursorPauseAndDeathBlockHotkeys()
        {
            player.SetMenuOpen(true);
            weapon.Hand.TryApply(7, false);
            weapon.SavePreset(2);
            weapon.Hand.TryApply(1, true);
            player.SetMenuOpen(false);
            Cursor.lockState = CursorLockMode.None;
            Press(Key.Digit2);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(1));
            Cursor.lockState = CursorLockMode.Locked;
            Time.timeScale = 0f;
            Press(Key.Digit2);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(1));
            Time.timeScale = 1f;
            player.Die();
            Press(Key.Digit2);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(1));
            Assert.That(weapon.SavePreset(2), Is.False);
        }
    }
}
