using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class GrenadePlayTests
    {
        private ShooterSettings settings;
        private HandWeapon weapon;
        private PrototypePlayer player;
        private Material material;
        private readonly Vector3 origin = new Vector3(80f, 20f, 80f);

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            // The prototype can stay excluded from the shipping build profile.
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/HandShooterPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("HandShooterPrototype");
#endif
            yield return null;
            player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false); player.enabled = false;
            weapon = player.weapon; weapon.enabled = false;
            material = GameObject.Find("Target 1").GetComponent<Renderer>().sharedMaterial;
            settings = Object.Instantiate(weapon.settings);
            settings.grenadeEnabled = true;
            settings.grenadeUpwardSpeed = 0f;
            settings.grenadeLaunchSpeed = 0.1f;
            settings.grenadeFuseDuration = 0.5f;
            weapon.settings = settings;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Grenade test floor";
            floor.transform.position = origin + Vector3.down * 1.1f;
            floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
            Physics.SyncTransforms();
        }

        [TearDown] public void TearDown()
        {
            Object.Destroy(settings);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private DamageTarget Target(Vector3 offset)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            go.transform.position = origin + offset;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var target = go.AddComponent<DamageTarget>(); target.settings = settings;
            go.SetActive(true);
            return target;
        }

        private HandGrenade Launch(float damage = 3f)
        {
            return HandGrenade.Spawn(origin, Vector3.down, damage, settings, weapon);
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        private static IEnumerator Land(HandGrenade grenade)
        {
            for (int i = 0; i < 250 && grenade && !grenade.HasLanded; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(grenade, Is.Not.Null);
            Assert.That(grenade.HasLanded, Is.True);
        }

        [UnityTest] public IEnumerator DifferentPowersShareTheSameFlightAndGroundFuse()
        {
            var weak = Launch(3f);
            var strong = Launch(24f);
            yield return Steps(5);
            Assert.That(strong.transform.position, Is.EqualTo(weak.transform.position));
            yield return Land(weak);
            Assert.That(strong.HasLanded, Is.True);
            yield return Steps(20);
            Assert.That(strong.FuseRemaining, Is.EqualTo(weak.FuseRemaining).Within(0.0001f));
            Assert.That(strong.FuseRemaining, Is.GreaterThan(0f));
            yield return Steps(7);
            yield return null;
            Assert.That(weak == null && strong == null, Is.True);
        }

        [UnityTest] public IEnumerator FuseStartsOnLandingPausesAndBlastDamagesEachTargetOnce()
        {
            var near = Target(new Vector3(2f, -0.5f, 0f));
            var outside = Target(new Vector3(4f, -0.5f, 0f));
            new GameObject("Extra collider", typeof(BoxCollider)).transform.SetParent(near.transform, false);
            float before = near.Health;
            var grenade = Launch();
            yield return Steps(5);
            Assert.That(grenade.HasLanded, Is.False);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            Assert.That(near.Health, Is.EqualTo(before));
            yield return Land(grenade);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            Vector3 landed = grenade.transform.position;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            Time.timeScale = 1f;
            yield return Steps(20);
            Assert.That(near.Health, Is.EqualTo(before));
            Assert.That(grenade.transform.position, Is.EqualTo(landed));
            yield return Steps(7);
            Assert.That(near.Health, Is.EqualTo(before - 3f));
            Assert.That(outside.Health, Is.EqualTo(before));
            yield return null;
            Assert.That(grenade == null, Is.True);
        }

        [UnityTest] public IEnumerator CoverBlocksBlastAndLaunchSettingsRemainFixed()
        {
            var exposed = Target(new Vector3(-2f, -0.5f, 0f));
            var covered = Target(new Vector3(2f, -0.5f, 0f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + new Vector3(1f, 0f, 0f);
            wall.transform.localScale = new Vector3(0.1f, 4f, 4f);
            var grenade = Launch();
            settings.grenadeDamageMultiplier = 100f;
            settings.grenadeRadiusMultiplier = 0f;
            settings.grenadeFuseDuration = 100f;
            yield return Land(grenade);
            yield return Steps(27);
            Assert.That(exposed.Health, Is.EqualTo(settings.targetHealth - 3f));
            Assert.That(covered.Health, Is.EqualTo(settings.targetHealth));
            yield return null;
        }

        [UnityTest] public IEnumerator WallImpactDoesNotExplodeOrStartFuse()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = origin + Vector3.forward;
            wall.transform.localScale = new Vector3(4f, 8f, 0.02f);
            settings.grenadeLaunchSpeed = 30f;
            var grenade = HandGrenade.Spawn(origin, Vector3.forward, 3f, settings, weapon);
            yield return Steps(3);
            Assert.That(grenade.transform.position.z, Is.LessThan(wall.transform.position.z));
            Assert.That(grenade.HasLanded, Is.False);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            yield return Land(grenade);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            yield return null;
        }

        [UnityTest] public IEnumerator RigidbodyFlightUsesLaunchGravityAndPausesInMidair()
        {
            float launchGravity = settings.grenadeGravity;
            var grenade = Launch();
            var rigidbody = grenade.GetComponent<Rigidbody>();
            var collider = grenade.GetComponent<SphereCollider>();
            Assert.That(rigidbody, Is.Not.Null);
            Assert.That(rigidbody.isKinematic, Is.False);
            Assert.That(collider && !collider.isTrigger, Is.True);
            settings.grenadeGravity = 100f;
            float started = Time.fixedTime;
            yield return Steps(5);
            Assert.That(rigidbody.position.y, Is.LessThan(origin.y));
            Assert.That(rigidbody.linearVelocity.y,
                Is.EqualTo(-0.1f - launchGravity * (Time.fixedTime - started)).Within(0.01f));
            Assert.That(grenade.HasLanded, Is.False);
            Vector3 position = rigidbody.position, velocity = rigidbody.linearVelocity;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(rigidbody.position, Is.EqualTo(position));
            Assert.That(rigidbody.linearVelocity, Is.EqualTo(velocity));
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            Time.timeScale = 1f;
            yield return Land(grenade);
            Assert.That(rigidbody.isKinematic, Is.True);
        }

        [UnityTest] public IEnumerator LandingOnEnemyDoesNotArmOrApplyContactDamage()
        {
            var enemy = Target(new Vector3(0f, -0.5f, 0f));
            var grenade = Launch();
            yield return Steps(15);
            Assert.That(grenade.GetComponent<Rigidbody>().position.y, Is.GreaterThan(origin.y - 0.4f));
            Assert.That(grenade.HasLanded, Is.False);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
            Assert.That(enemy.Health, Is.EqualTo(settings.targetHealth));
            enemy.transform.position += Vector3.right * 3f;
            Physics.SyncTransforms();
            yield return Land(grenade);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
        }

        [UnityTest] public IEnumerator PhysicalFlightRespectsHitMaskAndIgnoresPlayerLayerAndTriggers()
        {
            var excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.layer = 8;
            excluded.transform.position = origin + Vector3.forward;
            excluded.transform.localScale = new Vector3(4f, 8f, 0.02f);
            settings.hitMask &= ~(1 << 8);
            var playerLayer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerLayer.layer = 2;
            playerLayer.transform.position = origin + Vector3.forward * 0.5f;
            playerLayer.transform.localScale = excluded.transform.localScale;
            var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.transform.position = origin + Vector3.forward * 0.75f;
            trigger.transform.localScale = excluded.transform.localScale;
            trigger.GetComponent<Collider>().isTrigger = true;
            settings.grenadeLaunchSpeed = 30f;
            var grenade = HandGrenade.Spawn(origin, Vector3.forward, 3f, settings, weapon);
            yield return Steps(3);
            Assert.That(grenade.GetComponent<Rigidbody>().position.z, Is.GreaterThan(origin.z + 1f));
            Assert.That(grenade.HasLanded, Is.False);
            Assert.That(grenade.FuseRemaining, Is.EqualTo(0.5f));
        }

        [UnityTest] public IEnumerator ModeButtonsSelectAndDisablingRemovesGrenadeWithoutResettingCooldown()
        {
            var menu = Object.FindFirstObjectByType<HandMenu>();
            player.SetMenuOpen(true);
            yield return null;
            var panel = menu.MenuRoot.transform.Find("Hand Menu Panel");
            panel.Find("Mode Button 2").GetComponent<Button>().onClick.Invoke();
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Projectile));
            var button = panel.Find("Mode Button 3").GetComponent<Button>();
            button.onClick.Invoke(); Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Grenade));
            yield return null;
            Assert.That(button.GetComponentInChildren<Text>().text, Does.Contain("GRANADA"));
            player.SetMenuOpen(false);
            float before = weapon.Fuel;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(before - 3f));
            Assert.That(Object.FindFirstObjectByType<HandGrenade>(), Is.Not.Null);
            float cooldown = weapon.CooldownRemaining;
            settings.grenadeEnabled = false;
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
            weapon.CycleMode(); Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Projectile));
            weapon.CycleMode(); Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
            Assert.That(weapon.CooldownRemaining, Is.EqualTo(cooldown));
            Assert.That(weapon.TryFire(), Is.False);
        }

        [UnityTest] public IEnumerator SavedWeaponRestoresGrenadeAndDisabledSavesRestoreRay()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo capture = typeof(HandWeapon).GetMethod("CaptureState", flags);
            MethodInfo restore = typeof(HandWeapon).GetMethod("RestoreState", flags);
            weapon.Hand.SetMode(WeaponMode.Grenade);
            object saved = capture.Invoke(weapon, null);
            weapon.Hand.SetMode(WeaponMode.Projectile);
            restore.Invoke(weapon, new[] { saved });
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Grenade));
            settings.grenadeEnabled = false;
            restore.Invoke(weapon, new[] { saved });
            Assert.That(weapon.Stats.Mode, Is.EqualTo(WeaponMode.Ray));
            weapon.Hand.SetMode(WeaponMode.Grenade);
            saved = capture.Invoke(weapon, null);
            Assert.That(saved.GetType().GetField("thumb").GetValue(saved), Is.True);
            Assert.That(saved.GetType().GetField("grenade").GetValue(saved), Is.False);
            yield return null;
        }
    }
}
