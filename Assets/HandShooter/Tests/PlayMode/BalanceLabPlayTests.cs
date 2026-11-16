using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class BalanceLabPlayTests
    {
        private BalanceLabSession session;
        [UnitySetUp] public IEnumerator LoadLab()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("BalanceLab");
            yield return null;
            yield return null;
            session = Object.FindFirstObjectByType<BalanceLabSession>();
            session.publishResults = false;
            session.player.enabled = false;
            session.player.SetMenuOpen(false);
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest] public IEnumerator TrialsUseIndependentCopiesAndRestartRestoresResources()
        {
            Assert.That(session.RuntimeProfile.shooter, Is.Not.SameAs(session.profile.shooter));
            Assert.That(Object.FindObjectsByType<FuelTankPickup>(FindObjectsSortMode.None), Is.Empty);
            float sourceDamage = session.profile.shooter.baseDamage;
            session.RuntimeProfile.shooter.baseDamage = sourceDamage + 99f;
            Assert.That(session.profile.shooter.baseDamage, Is.EqualTo(sourceDamage));
            var weapon = session.player.weapon;
            // This test changes damage to verify isolation; keep its shot affordable for any saved capacity.
            weapon.fuelPerDamage = weapon.tankCapacity * 0.1f / weapon.Stats.Damage;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.Fuel, Is.LessThan(weapon.tankCapacity));
            session.RestartTrial();
            yield return null;
            yield return null;
            yield return null;
            session = Object.FindFirstObjectByType<BalanceLabSession>();
            session.publishResults = false;
            Assert.That(session.RuntimeProfile.shooter.baseDamage, Is.EqualTo(sourceDamage));
            Assert.That(session.player.weapon.Fuel, Is.EqualTo(session.player.weapon.tankCapacity));
            Assert.That(session.player.weapon.CooldownRemaining, Is.Zero);
            Assert.That(session.Trial.Result.shots, Is.Zero);
        }

        [UnityTest] public IEnumerator ActualShotRecordsDamageAndDeathCapturesCauseAndSnapshot()
        {
            var target = Object.FindObjectsByType<DamageTarget>(FindObjectsSortMode.None).OrderBy(t => Mathf.Abs(t.transform.position.x)).First();
            Vector3 initialPosition = target.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.That(target.GetComponentInParent<Animator>(), Is.Null);
            Assert.That(target.transform.position, Is.EqualTo(initialPosition));
            Assert.That(target.transform.position.z, Is.EqualTo(session.distance).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(session.RuntimeProfile.shooter.targetHealth));
            session.player.viewCamera.transform.LookAt(target.transform.position);
            Assert.That(session.player.weapon.TryFire(), Is.True);
            Assert.That(session.Trial.Result.shots, Is.EqualTo(1));
            Assert.That(session.Trial.Result.hittingShots, Is.EqualTo(1));
            Assert.That(session.Trial.Result.effectiveDamage, Is.GreaterThan(0f));
            session.player.Die("Sniper");
            Assert.That(session.Trial.Finished, Is.True);
            Assert.That(session.Trial.Result.deathCause, Is.EqualTo("Sniper"));
            Assert.That(session.Trial.Result.outcome, Is.EqualTo("Muerte"));
            Assert.That(session.Trial.Result.balanceSnapshot, Does.Contain("baseDamage"));
            Assert.That(session.Trial.Result.finalBalanceSnapshot, Is.Not.Empty);
            yield return null;
        }

        [UnityTest] public IEnumerator ClearingEncounterFinishesAndPausesWorld()
        {
            foreach (var target in Object.FindObjectsByType<DamageTarget>(FindObjectsSortMode.None)) target.TakeDamage(target.Health);
            yield return null;
            Assert.That(session.Trial.Finished, Is.True);
            Assert.That(session.Trial.Result.outcome, Is.EqualTo("Superado"));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(session.player.IsMenuOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }

        [UnityTest] public IEnumerator EveryScenarioSpawnsWithRuntimeSettingsAndOptionalCover()
        {
            foreach (int count in new[] { 1, 3, 8 })
            foreach (BalanceScenario kind in System.Enum.GetValues(typeof(BalanceScenario)))
            {
                Time.timeScale = 1f;
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> configure = (scene, mode) =>
                {
                    var lab = Object.FindFirstObjectByType<BalanceLabSession>();
                    lab.scenario = kind; lab.enemyCount = count; lab.cover = true; lab.publishResults = false;
                };
                SceneManager.sceneLoaded += configure;
                yield return SceneManager.LoadSceneAsync("BalanceLab");
                SceneManager.sceneLoaded -= configure;
                yield return null;
                yield return null;
                session = Object.FindFirstObjectByType<BalanceLabSession>();
                session.player.enabled = false;
                session.player.SetMenuOpen(true);
                var targets = Object.FindObjectsByType<DamageTarget>(FindObjectsSortMode.None);
                if (kind == BalanceScenario.EstacionDeLlenado)
                {
                    Assert.That(targets, Is.Empty);
                    var station = Object.FindFirstObjectByType<RefuelingStation>();
                    Assert.That(station, Is.Not.Null);
                    Assert.That(station.settings, Is.SameAs(session.RuntimeProfile.refuelingStation));
                    Assert.That(station.settings, Is.Not.SameAs(session.profile.refuelingStation));
                    Assert.That(session.player.weapon.FuelFraction, Is.EqualTo(session.stationStartingFuel).Within(0.001f));
                    continue;
                }
                Assert.That(targets.Length, Is.EqualTo(count), $"{kind}: {count} enemigos");
                Assert.That(GameObject.Find("Cobertura"), Is.Not.Null);
                foreach (var target in targets)
                {
                    Assert.That(target.Health, Is.GreaterThan(0f));
                    Assert.That(target.settings, Is.SameAs(session.RuntimeProfile.shooter));
                    if (target.hunterSettings) Assert.That(target.hunterSettings, Is.SameAs(session.RuntimeProfile.hunter));
                    if (target.sniperSettings) Assert.That(target.sniperSettings, Is.SameAs(session.RuntimeProfile.sniper));
                    if (target.kamikazeSettings) Assert.That(target.kamikazeSettings, Is.SameAs(session.RuntimeProfile.kamikaze));
                    if (target.turretSettings) Assert.That(target.turretSettings, Is.SameAs(session.RuntimeProfile.turret));
                }
            }
        }
    }
}
