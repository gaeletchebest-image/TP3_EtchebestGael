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
            yield return new WaitForSeconds(0.85f);
            Assert.That(target.Health, Is.EqualTo(before - 3));
        }
        [UnityTest] public IEnumerator MenuPausesWorldBlocksShootingAndButtonsChangeConfiguration()
        {
            var menu = Object.FindFirstObjectByType<HandMenu>();
            player.SetMenuOpen(true);
            yield return null;
            Assert.That(menu.MenuRoot.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(weapon.TryFire(), Is.False);
            var middle = menu.MenuRoot.transform.Find("Panel/MEDIO").GetComponent<Button>();
            Assert.That(middle.interactable, Is.True);
            middle.onClick.Invoke();
            Assert.That(weapon.Hand.ActiveCount, Is.EqualTo(2));
            yield return null;
            var index = menu.MenuRoot.transform.Find("Panel/ÍNDICE").GetComponent<Button>();
            index.onClick.Invoke();
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(2));
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

