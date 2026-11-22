using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class PrototypePlayTests
    {
        private PrototypePlayer player;
        private HandWeapon weapon;
        private DamageTarget target;
        [UnitySetUp] public IEnumerator LoadRange()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            weapon = player.weapon;
            player.SetMenuOpen(false);
            player.enabled = false; // Tests supply aim directly, without depending on OS focus.
            target = GameObject.Find("Target 1").GetComponent<DamageTarget>();
            Physics.SyncTransforms();
        }
        [TearDown] public void RestoreClock() { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        [UnityTest] public IEnumerator HitscanAppliesDamageAndRespectsCooldown()
        {
            float before = target.Health;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(target.Health, Is.EqualTo(before - weapon.Stats.Damage));
            Assert.That(weapon.TryFire(), Is.False);
            yield return new WaitForSeconds(weapon.Stats.Interval + 0.05f);
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(target.Health, Is.EqualTo(before - weapon.Stats.Damage * 2));
        }
        [UnityTest] public IEnumerator ProjectileTravelsBeforeApplyingDirectAndExplosionDamage()
        {
            weapon.Hand.ThumbActive = false;
            float before = target.Health;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(target.Health, Is.EqualTo(before));
            Assert.That(Object.FindFirstObjectByType<HandProjectile>(), Is.Not.Null);
            // Travel time follows the current balance asset, including damage-based slowdown.
            float flightTime = Vector3.Distance(weapon.muzzle.position, target.transform.position) /
                weapon.settings.EvaluateProjectileSpeed(weapon.Stats.Damage);
            yield return new WaitForSeconds(flightTime + 0.1f);
            Assert.That(target.Health, Is.EqualTo(before - 1.5f));
        }
        [UnityTest] public IEnumerator MenuPausesWorldBlocksShootingAndButtonsChangeConfiguration()
        {
            var menu = Object.FindFirstObjectByType<HandMenu>();
            player.SetMenuOpen(true);
            yield return null;
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(weapon.TryFire(), Is.False);
            var panel = menu.MenuRoot.transform.Find("Hand Menu Panel");
            panel.Find("Mode Button 2").GetComponent<Button>().onClick.Invoke();
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Projectile));
            panel.Find("Power Segment 2").GetComponent<Button>().onClick.Invoke();
            Assert.That(weapon.PowerLevel, Is.EqualTo(2));
            yield return null;
            Assert.That(panel.Find("Power Label").GetComponent<Text>().text, Does.Contain("2 / 4"));
            panel.Find("Power Segment 1").GetComponent<Button>().onClick.Invoke();
            Assert.That(weapon.PowerLevel, Is.EqualTo(1));
            player.SetMenuOpen(false);
            Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ProjectileSweepCannotSkipThinWalls()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1.65f, 3);
            wall.transform.localScale = new Vector3(4, 4, 0.02f);
            Physics.SyncTransforms();
            var projectile = Object.Instantiate(weapon.settings.projectilePrefab, new Vector3(0, 1.65f, 1), Quaternion.identity);
            projectile.Initialize(Vector3.forward, 16, weapon.settings, weapon);
            projectile.Simulate(1f);
            yield return null;
            Assert.That(projectile == null, Is.True);
            Assert.That(target.Health, Is.EqualTo(weapon.settings.targetHealth));
            Object.Destroy(wall);
        }
        [UnityTest] public IEnumerator DestroyedTargetRespawnsWithFullHealth()
        {
            target.TakeDamage(target.Health);
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(target.DestroyedCount, Is.EqualTo(1));
            yield return new WaitForSeconds(weapon.settings.targetRespawnDelay + 0.05f);
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(target.Health, Is.EqualTo(weapon.settings.targetHealth));
        }
    }
}

