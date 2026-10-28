using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class ExplosionPlayTests
    {
        private ShooterSettings settings;
        private HandWeapon weapon;
        private Material targetMaterial;
        private readonly Vector3 origin = new Vector3(8, 2, 8);
        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
            yield return null;
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false); player.enabled = false;
            weapon = player.weapon; weapon.enabled = false;
            settings = Object.Instantiate(weapon.settings);
            targetMaterial = GameObject.Find("Target 1").GetComponent<Renderer>().sharedMaterial;
        }
        [TearDown] public void TearDown()
        {
            Object.Destroy(settings);
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private DamageTarget Target(Vector3 offset)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            go.name = "Explosion test target";
            go.transform.position = origin + offset;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().sharedMaterial = targetMaterial;
            var target = go.AddComponent<DamageTarget>(); target.settings = settings;
            go.SetActive(true);
            return target;
        }
        private HandProjectile Launch(float damage)
        {
            Physics.SyncTransforms();
            var projectile = Object.Instantiate(settings.projectilePrefab, origin, Quaternion.identity);
            projectile.Initialize(Vector3.forward, damage, settings, weapon);
            return projectile;
        }
        [UnityTest] public IEnumerator DirectHitGetsFullDamagePlusSplashAndNeighboursGetSplashOnce()
        {
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0, 1.3f));
            var outside = Target(new Vector3(3.2f, 0, 1));
            var child = new GameObject("Extra collider", typeof(BoxCollider));
            child.transform.SetParent(nearby.transform, false);
            child.transform.localScale = Vector3.one * 0.8f;
            var shot = Launch(8);
            shot.Simulate(0.2f);
            Assert.That(direct.Health, Is.EqualTo(20));
            Assert.That(nearby.Health, Is.EqualTo(28));
            Assert.That(outside.Health, Is.EqualTo(32));
            // Detonating the same projectile a second time cannot apply damage twice.
            shot.Detonate(direct.GetComponent<Collider>(), direct.transform.position, Vector3.back);
            Assert.That(nearby.Health, Is.EqualTo(28));
            yield return null;
            Assert.That(shot == null, Is.True);
        }
        [UnityTest] public IEnumerator CoverStopsSplash()
        {
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0, 1.3f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(0.45f, 0, 0.9f);
            wall.transform.localScale = new Vector3(0.08f, 2, 1);
            Launch(8).Simulate(0.2f);
            Assert.That(direct.Health, Is.EqualTo(20));
            Assert.That(nearby.Health, Is.EqualTo(32));
            yield return null;
        }
        [UnityTest] public IEnumerator MoreConcentratedShotReachesFurtherTargets()
        {
            var direct = Target(Vector3.forward);
            var distant = Target(new Vector3(2.2f, 0, 1));
            Launch(2).Simulate(0.2f);
            Assert.That(distant.Health, Is.EqualTo(32));
            direct.ResetTarget();
            Launch(16).Simulate(0.2f);
            Assert.That(direct.Health, Is.EqualTo(8));
            Assert.That(distant.Health, Is.EqualTo(24));
            yield return null;
        }
        [UnityTest] public IEnumerator ExplosionUsesLaunchValuesEvenIfSettingsChangeInFlight()
        {
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0, 1.3f));
            var shot = Launch(8);
            settings.explosionDamageFraction = 0;
            settings.explosionBaseRadius = 0;
            shot.Simulate(0.2f);
            Assert.That(direct.Health, Is.EqualTo(20));
            Assert.That(nearby.Health, Is.EqualTo(28));
            yield return null;
        }
        [UnityTest] public IEnumerator FractionZeroDisablesSplash()
        {
            settings.explosionDamageFraction = 0;
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0, 1.3f));
            Launch(8).Simulate(0.2f);
            Assert.That(direct.Health, Is.EqualTo(24));
            Assert.That(nearby.Health, Is.EqualTo(32));
            Assert.That(Object.FindFirstObjectByType<ExplosionEffect>(), Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator HittingSceneryAlsoExplodes()
        {
            var nearby = Target(new Vector3(0.9f, 0, 0.7f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + Vector3.forward;
            wall.transform.localScale = new Vector3(0.3f, 2, 0.2f);
            Launch(8).Simulate(0.2f);
            Assert.That(nearby.Health, Is.EqualTo(28));
            yield return null;
            Assert.That(Object.FindFirstObjectByType<ExplosionEffect>(), Is.Not.Null);
        }
    }
}



