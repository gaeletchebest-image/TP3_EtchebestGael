using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HandShooter.Tests
{
    public sealed class RefuelingStationPlayTests
    {
        private PrototypePlayer player;
        private HandWeapon weapon;
        private RefuelingStation station;
        private RefuelingStationSettings settings;
        private float initialFuel;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("BalanceLab");
            yield return null;
            yield return null;
            var session = Object.FindFirstObjectByType<BalanceLabSession>();
            session.publishResults = false;
            player = session.player;
            player.enabled = false;
            player.SetMenuOpen(false);
            weapon = player.weapon;
            player.transform.position = new Vector3(1000f, 20f, 990f);
            weapon.fuelPerDamage = weapon.tankCapacity * 0.7f / weapon.Stats.Damage;
            Assert.That(weapon.TryFire(), Is.True);
            initialFuel = weapon.Fuel;
            settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RefuelingStationSettings>("Assets/HandShooter/Settings/RefuelingStationSettings.asset"));
            settings.fillDuration = 1f;
            settings.interruptedFillFraction = 0.5f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandShooter/Prefabs/RefuelingStation/RefuelingStation.prefab");
            var staging = new GameObject("Station staging");
            staging.SetActive(false);
            station = Object.Instantiate(prefab, staging.transform).GetComponent<RefuelingStation>();
            station.settings = settings;
            station.transform.SetPositionAndRotation(new Vector3(1000f, 20f, 1000f), Quaternion.Euler(0f, 35f, 0f));
            station.transform.SetParent(null, true);
            Object.Destroy(staging);
            MoveToDeck();
            yield return Until(() => station.IsFilling);
        }

        private void MoveToDeck(Vector3 localOffset = default)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            var foot = station.deck.transform.TransformPoint(station.deck.center + Vector3.up * (station.deck.size.y * 0.5f));
            player.transform.position = foot + Vector3.up * 0.025f + station.transform.TransformVector(localOffset) -
                Vector3.up * (controller.center.y - controller.height * 0.5f);
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"La estación no alcanzó el estado esperado. " +
                $"time={Time.timeScale}, standing={station.IsStandingOnDeck(player.GetComponent<CharacterController>())}, " +
                $"active={station.isActiveAndEnabled}, player={player.transform.position}, deck={station.deck.bounds}, " +
                $"fuel={weapon.Fuel}, dead={player.IsDead}, menu={player.IsMenuOpen}, layer={player.gameObject.layer}");
        }

        [TearDown]
        public void Cleanup()
        {
            if (station) Object.DestroyImmediate(station.gameObject);
            if (settings) Object.DestroyImmediate(settings);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator FillTimeIsProportionalToMissingFuel()
        {
            settings.fillDuration = 2f;
            foreach (float fraction in new[] { 0f, 0.5f, 0.9f })
            {
                station.enabled = false;
                weapon.AddFuel(weapon.tankCapacity);
                yield return Until(() => weapon.CooldownRemaining <= 0f);
                weapon.fuelPerDamage = weapon.tankCapacity * (1f - fraction) / weapon.Stats.Damage;
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(weapon.FuelFraction, Is.EqualTo(fraction).Within(0.001f));
                station.enabled = true;
                yield return Until(() => station.IsFilling);
                float started = Time.time;
                yield return Until(() => weapon.Fuel >= weapon.tankCapacity);
                Assert.That(Time.time - started, Is.EqualTo(2f * (1f - fraction))
                    .Within(Time.maximumDeltaTime + 0.02f), $"Combustible inicial: {fraction:P0}");
            }
        }

        [UnityTest]
        public IEnumerator InterruptionKeepsHalfOfFuelAccumulatedAtTheFullTankRate()
        {
            station.enabled = false;
            settings.fillDuration = 2f;
            weapon.AddFuel(weapon.tankCapacity);
            yield return Until(() => weapon.CooldownRemaining <= 0f);
            weapon.fuelPerDamage = weapon.tankCapacity * 0.5f / weapon.Stats.Damage;
            Assert.That(weapon.TryFire(), Is.True);
            station.enabled = true;
            yield return Until(() => station.IsFilling);
            float started = Time.time;
            yield return Until(() => station.Progress >= 0.3f);
            float accumulated = weapon.tankCapacity * (Time.time - started) / 2f;
            Assert.That(station.PendingFuel, Is.EqualTo(accumulated).Within(0.01f));
            MoveToDeck(Vector3.right * 4f);
            yield return null;
            Assert.That(weapon.Fuel, Is.EqualTo(weapon.tankCapacity * 0.5f + accumulated * 0.5f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator CompletionDefersFuelAndDisplaysDarkerPreviewWithoutChangingTanks()
        {
            int tanks = weapon.SpareTankCount;
            float capacity = weapon.tankCapacity;
            yield return Until(() => station.Progress >= 0.25f);
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel));
            var menu = Object.FindFirstObjectByType<HandMenu>();
            Assert.That(menu.refuelPreview.enabled, Is.True);
            Assert.That(menu.refuelPreview.transform.GetSiblingIndex(), Is.LessThan(menu.fuelFill.transform.GetSiblingIndex()));
            Assert.That(menu.refuelPreview.rectTransform.anchorMax.x, Is.GreaterThan(weapon.FuelFraction));
            Assert.That(menu.refuelPreview.color.g, Is.LessThan(menu.fuelFill.color.g));
            Assert.That(weapon.ReloadFuel(), Is.False);
            yield return Until(() => weapon.Fuel == capacity);
            Assert.That(weapon.SpareTankCount, Is.EqualTo(tanks));
            Assert.That(weapon.tankCapacity, Is.EqualTo(capacity));
            Assert.That(weapon.ActiveRefuelingStation, Is.Null);
            yield return null;
            Assert.That(menu.refuelPreview.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator LeavingGrantsHalfAccruedFuelAndReentryStartsANewCycle()
        {
            yield return Until(() => station.Progress >= 0.3f);
            float expected = initialFuel + station.PendingFuel * 0.5f;
            MoveToDeck(Vector3.right * 4f);
            yield return null;
            Assert.That(weapon.Fuel, Is.EqualTo(expected).Within(0.001f));
            Assert.That(station.Progress, Is.Zero);
            Assert.That(weapon.ActiveRefuelingStation, Is.Null);
            MoveToDeck();
            yield return Until(() => station.IsFilling);
            Assert.That(station.Progress, Is.Zero);
            yield return Until(() => weapon.Fuel == weapon.tankCapacity);
        }

        [UnityTest]
        public IEnumerator PausePreservesProgressAndOnlyStandingOnDeckStartsACharge()
        {
            yield return Until(() => station.Progress >= 0.2f);
            player.SetMenuOpen(true);
            float progress = station.Progress;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(station.Progress, Is.EqualTo(progress));
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel));
            player.SetMenuOpen(false);
            MoveToDeck(Vector3.up * 1f);
            yield return null;
            Assert.That(station.IsFilling, Is.False);
            yield return null;
            Assert.That(station.IsFilling, Is.False);
            MoveToDeck();
            yield return Until(() => station.IsFilling);
        }

        [UnityTest]
        public IEnumerator DeathDiscardsPendingFuelEvenWhenTheClockStops()
        {
            yield return Until(() => station.Progress >= 0.2f);
            player.Die("Prueba de estación");
            yield return null;
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel));
            Assert.That(weapon.ActiveRefuelingStation, Is.Null);
            Assert.That(station.IsFilling, Is.False);
        }

        [UnityTest]
        public IEnumerator ConfiguredPenaltyAndStationDisableSettleExactlyOnce()
        {
            settings.interruptedFillFraction = 0.2f;
            yield return Until(() => station.Progress >= 0.3f);
            float expected = initialFuel + station.PendingFuel * 0.2f;
            station.enabled = false;
            Assert.That(weapon.Fuel, Is.EqualTo(expected).Within(0.001f));
            Assert.That(weapon.ActiveRefuelingStation, Is.Null);
            yield return null;
            Assert.That(weapon.Fuel, Is.EqualTo(expected).Within(0.001f));
        }
    }
}
