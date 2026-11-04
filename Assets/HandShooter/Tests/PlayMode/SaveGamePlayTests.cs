using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class SaveGamePlayTests
    {
        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
        private InputTestFixture input;
        private Keyboard keyboard;
        private string createdSave;

        [TearDown]
        public void Cleanup()
        {
            if (createdSave != null) File.Delete(createdSave);
            input?.TearDown();
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator EscapeSaveCanRestoreAChangedLevelFromAnotherScene()
        {
            Time.timeScale = 1f;
            input = new InputTestFixture();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            yield return null;
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            OpenPauseMenu(player);
            var saveObject = GameObject.Find("Guardar partida");
            Assert.That(saveObject, Is.Not.Null, "Escape must show the save menu.");
            var button = saveObject.GetComponent<Button>();
            Assert.That(button.interactable, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            player.transform.position += Vector3.right * 0.25f;
            Vector3 savedPosition = player.transform.position;
            player.weapon.AddFuelTank(5);
            int tanks = player.weapon.SpareTankCount;
            Assert.That(player.weapon.Hand.TryApply(12, false), Is.True);
            var turrets = Object.FindObjectsByType<TurretEnemy>(FindObjectsSortMode.None);
            Assert.That(turrets.Length, Is.GreaterThanOrEqualTo(2));
            var damaged = turrets[0].GetComponent<DamageTarget>();
            damaged.TakePlayerShotDamage(1f);
            float savedHealth = damaged.Health;
            int savedDefenseBar = damaged.DefenseBar;
            Vector3 damagedPosition = damaged.transform.position;
            var defeated = turrets[1].GetComponent<DamageTarget>();
            Vector3 defeatedPosition = defeated.transform.position;
            defeated.TakeDamage(defeated.Health / defeated.DamageMultiplier);
            yield return null;

            var door = Object.FindFirstObjectByType<SlidingDoor>();
            var panel = door.GetComponentInChildren<Rigidbody>();
            Vector3 doorPosition = door.transform.position;
            door.SetOpen(true, immediate: true);
            Vector3 savedPanelPosition = panel.position;

            var before = Directory.Exists(SaveDirectory)
                ? new HashSet<string>(Directory.GetFiles(SaveDirectory, "*.json"))
                : new HashSet<string>();
            button.onClick.Invoke();
            createdSave = Directory.GetFiles(SaveDirectory, "*.json").Single(path => !before.Contains(path));

            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            OpenPauseMenu(Object.FindFirstObjectByType<PrototypePlayer>());
            var row = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(candidate => candidate.GetComponentInChildren<Text>()?.text.Contains("LevelStarter   ·") == true);
            row.onClick.Invoke();
            for (int frame = 0; frame < 600 && SceneManager.GetActiveScene().name != "LevelStarter"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("LevelStarter"));
            yield return null;

            var restored = Object.FindFirstObjectByType<PrototypePlayer>();
            Assert.That(Vector3.Distance(restored.transform.position, savedPosition), Is.LessThan(0.05f));
            Assert.That(restored.weapon.SpareTankCount, Is.EqualTo(tanks));
            Assert.That(restored.weapon.Hand.ActiveMask, Is.EqualTo(12));
            Assert.That(restored.weapon.Hand.ThumbActive, Is.False);
            var surviving = Object.FindObjectsByType<TurretEnemy>(FindObjectsSortMode.None);
            Assert.That(surviving.Length, Is.EqualTo(turrets.Length - 1));
            Assert.That(surviving.Any(turret => turret.transform.position == defeatedPosition), Is.False);
            var restoredEnemy = surviving.Single(turret => turret.transform.position == damagedPosition).GetComponent<DamageTarget>();
            Assert.That(restoredEnemy.Health, Is.EqualTo(savedHealth));
            Assert.That(restoredEnemy.DefenseBar, Is.EqualTo(savedDefenseBar));
            var restoredDoor = Object.FindObjectsByType<SlidingDoor>(FindObjectsSortMode.None)
                .Single(candidate => candidate.transform.position == doorPosition);
            Assert.That(restoredDoor.GetComponentInChildren<Rigidbody>().position, Is.EqualTo(savedPanelPosition));
        }

        private void OpenPauseMenu(PrototypePlayer player)
        {
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == "PauseMenu"),
                Is.True, $"A save menu must be installed in {player.gameObject.scene.path}.");
            player.SetMenuOpen(false);
            input.Release(keyboard.escapeKey);
            input.Press(keyboard.escapeKey);
            InputSystem.Update();
            keyboard.MakeCurrent();
            Assert.That(keyboard.escapeKey.wasPressedThisFrame, Is.True);
            player.SendMessage("Update");
            Assert.That(player.IsMenuOpen, Is.True, "Escape must pause the player.");
            player.SendMessage("LateUpdate");
            input.Release(keyboard.escapeKey);
            InputSystem.Update();
        }
    }
}
