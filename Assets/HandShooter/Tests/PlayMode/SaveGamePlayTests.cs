using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class SaveGamePlayTests
    {
        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<string> createdSaves = new List<string>();

        [SetUp]
        public void SetupInput()
        {
            Time.timeScale = 1f;
            // Keep native devices registered even if Play Mode is interrupted by a reload.
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (mouse?.added == true) InputSystem.RemoveDevice(mouse);
            if (keyboard?.added == true) InputSystem.RemoveDevice(keyboard);
            Time.timeScale = 1f;
            foreach (var save in createdSaves)
            {
                File.Delete(Path.ChangeExtension(save, ".jpg"));
                File.Delete(save);
            }
            createdSaves.Clear();
        }

        [UnityTest]
        public IEnumerator EscapeSaveCanRestoreAChangedLevelFromAnotherScene()
        {
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            yield return null;
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            OpenPauseMenu(player);
            var saveObject = GameObject.Find("Guardar partida");
            Assert.That(saveObject, Is.Not.Null, "Escape must show the save menu.");
            var button = saveObject.GetComponent<Button>();
            Assert.That(button.interactable, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            const float savedSeconds = 97.5f;
            Object.FindFirstObjectByType<LevelLiftTransition>().SendMessage("RestoreCountdown", savedSeconds);

            var liftCarriage = GameObject.Find("Arrival Lift").transform.Find("Carriage");
            Vector3 savedCarriagePosition = liftCarriage.position;
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

            string savePath = RecordSave(() => Click(button));
            var savedRow = GameObject.Find("Partida guardada 1");
            var labels = savedRow.GetComponentsInChildren<Text>().Select(label => label.text).ToArray();
            Assert.That(labels, Does.Contain("LevelStarter"));
            Assert.That(labels, Does.Contain("01:37"));
            Assert.That(labels.Any(label => System.Text.RegularExpressions.Regex.IsMatch(label,
                @"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}$")), Is.True);
            string thumbnailPath = Path.ChangeExtension(savePath, ".jpg");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.That(File.Exists(thumbnailPath), Is.True);
                var thumbnail = savedRow.GetComponentInChildren<RawImage>().texture;
                Assert.That(thumbnail, Is.Not.Null);
                Assert.That(thumbnail.width, Is.EqualTo(320));
                Assert.That(thumbnail.height, Is.EqualTo(180));
                File.Delete(thumbnailPath); // Exercise the row for a save created without a thumbnail.
            }

            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            OpenPauseMenu(Object.FindFirstObjectByType<PrototypePlayer>());
            var row = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .OrderBy(candidate => candidate.transform.GetSiblingIndex())
                .First(candidate => candidate.GetComponentInChildren<Text>()?.text == "LevelStarter");
            var preview = row.GetComponentInChildren<RawImage>();
            Assert.That(preview.texture, Is.Null);
            Assert.That(preview.GetComponentInChildren<Text>().enabled, Is.True);
            Click(row);
            for (int frame = 0; frame < 600 && SceneManager.GetActiveScene().name != "LevelStarter"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("LevelStarter"));
            yield return null;

            var restored = Object.FindFirstObjectByType<PrototypePlayer>();
            var transition = Object.FindFirstObjectByType<LevelLiftTransition>();
            var remainingTime = typeof(LevelLiftTransition).GetProperty("SecondsRemaining",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(remainingTime.GetValue(transition), Is.EqualTo(savedSeconds));
            Assert.That(Vector3.Distance(restored.transform.position, savedPosition), Is.LessThan(0.05f));
            var restoredCarriage = GameObject.Find("Arrival Lift").transform.Find("Carriage");
            Assert.That(Vector3.Distance(restoredCarriage.position, savedCarriagePosition), Is.LessThan(0.05f));
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

        [UnityTest]
        public IEnumerator QuickLoadAndDeathRestoreTheLatestSave()
        {
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            yield return null;
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false);
            Assert.That(player.IsMenuOpen, Is.False);
            Assert.That(player.weapon.Hand.TryApply(3, true), Is.True);
            string quickSave = RecordSave(() => PressKey(player, Key.F5));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.That(File.Exists(Path.ChangeExtension(quickSave, ".jpg")), Is.True);
            Assert.That(player.weapon.Hand.TryApply(12, false), Is.True);
            string newerQuickSave = RecordSave(() => PressKey(player, Key.F5));

            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false);
            PressKey(player, Key.F9);
            yield return WaitForReload(player);

            player = Object.FindFirstObjectByType<PrototypePlayer>();
            Assert.That(player.weapon.Hand.ActiveMask, Is.EqualTo(12));
            Assert.That(player.weapon.Hand.ThumbActive, Is.False);
            Assert.That(player.IsMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            OpenPauseMenu(player);
            Assert.That(player.weapon.Hand.TryApply(6, true), Is.True);
            var button = GameObject.Find("Guardar partida").GetComponent<Button>();
            string manualSave = RecordSave(() => Click(button));
            player.SetMenuOpen(false);
            Assert.That(player.weapon.Hand.TryApply(1, false), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            keyboard.MakeCurrent();
            player.Die();
            player.SendMessage("Update");
            Assert.That(player.deathScreen.activeInHierarchy, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            yield return null;
            yield return null;
            Assert.That(player == null, Is.False, "The player must wait for a new key press.");
            Assert.That(player.deathScreen.activeInHierarchy, Is.True);
            yield return new WaitForSecondsRealtime(1f);
            PressKey(player, Key.F5);
            yield return WaitForReload(player);

            player = Object.FindFirstObjectByType<PrototypePlayer>();
            Assert.That(player.IsDead, Is.False);
            Assert.That(player.weapon.Hand.ActiveMask, Is.EqualTo(6));
            Assert.That(player.weapon.Hand.ThumbActive, Is.True);
            Assert.That(player.IsMenuOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            OpenPauseMenu(player);
            var delete = GameObject.Find("Fila de guardado 1/Eliminar").GetComponent<Button>();
            Click(delete);
            Click(GameObject.Find("Cancelar").GetComponent<Button>());
            Assert.That(File.Exists(manualSave), Is.True, "Cancel must preserve the selected save.");
            Click(delete);
            var pause = player.GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "PauseMenu");
            var target = pause.GetType().GetField("deleteTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
            Assert.That(target.GetType().GetField("filePath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target),
                Is.EqualTo(manualSave), "Only delete a save created by this test.");
            Click(GameObject.Find("Eliminar partida").GetComponent<Button>());
            Assert.That(File.Exists(manualSave), Is.False);
            Assert.That(File.Exists(Path.ChangeExtension(manualSave, ".jpg")), Is.False);
            Assert.That(File.Exists(newerQuickSave), Is.True);

            PressKey(player, Key.F9);
            yield return WaitForReload(player);
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            Assert.That(player.weapon.Hand.ActiveMask, Is.EqualTo(12));
        }

        private static IEnumerator WaitForReload(PrototypePlayer previous)
        {
            for (int frame = 0; frame < 600 && previous; frame++) yield return null;
            Assert.That(previous == null, Is.True, "The save must reload the level.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("LevelStarter"));
            yield return null;
        }

        private string RecordSave(System.Action save)
        {
            var before = Directory.Exists(SaveDirectory)
                ? new HashSet<string>(Directory.GetFiles(SaveDirectory, "*.json"))
                : new HashSet<string>();
            int count = createdSaves.Count;
            save();
            createdSaves.AddRange(Directory.GetFiles(SaveDirectory, "*.json").Where(path => !before.Contains(path)));
            Assert.That(createdSaves.Count, Is.EqualTo(count + 1));
            return createdSaves[count];
        }

        private void OpenPauseMenu(PrototypePlayer player)
        {
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponents<MonoBehaviour>().Any(component => component.GetType().Name == "PauseMenu"),
                Is.True, $"A save menu must be installed in {player.gameObject.scene.path}.");
            player.SetMenuOpen(false);
            PressKey(player, Key.Escape);
            Assert.That(player.IsMenuOpen, Is.True, "Escape must pause the player.");
            player.SendMessage("LateUpdate");
        }

        private void PressKey(PrototypePlayer player, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            keyboard.MakeCurrent();
            Assert.That(keyboard[key].wasPressedThisFrame, Is.True);
            player.SendMessage("Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var module = EventSystem.current.currentInputModule;
            Assert.That(module, Is.Not.Null);
            var state = new MouseState { position = position };
            InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left));
            InputSystem.Update();
            module.Process();
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            module.Process();
        }
    }
}
