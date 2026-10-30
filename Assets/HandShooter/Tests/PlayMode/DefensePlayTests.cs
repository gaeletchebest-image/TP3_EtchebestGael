using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class DefensePlayTests
    {
        private Scene arena, previousScene;
        private TurretSettings enemySettings;
        private ShooterSettings shooter;
        private Material material;
        private HandWeapon weapon;
        private static readonly Vector3 Origin = new Vector3(500f, 2f, 0f);

        [SetUp] public void Setup()
        {
            Time.timeScale = 0f;
            previousScene = SceneManager.GetActiveScene();
            arena = SceneManager.CreateScene("Defense test arena");
            SceneManager.SetActiveScene(arena);
            enemySettings = ScriptableObject.CreateInstance<TurretSettings>();
            enemySettings.initialHealth = 100f;
            enemySettings.initialDefensePoints = 2;
            enemySettings.defenseBarCapacity = 2;
            shooter = ScriptableObject.CreateInstance<ShooterSettings>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var go = new GameObject("Defense test weapon");
            go.SetActive(false);
            weapon = go.AddComponent<HandWeapon>();
            weapon.settings = shooter;
            weapon.enabled = false;
            go.SetActive(true);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(arena);
            Object.Destroy(enemySettings);
            Object.Destroy(shooter);
            Object.Destroy(material);
            Time.timeScale = 1f;
        }

        private DamageTarget Target(Vector3 offset, bool withLabel = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            go.transform.position = Origin + offset;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var target = go.AddComponent<DamageTarget>();
            target.turretSettings = enemySettings;
            if (withLabel)
            {
                var text = new GameObject("Health", typeof(TextMesh));
                text.transform.SetParent(go.transform, false);
                target.label = text.GetComponent<TextMesh>();
            }
            go.SetActive(true);
            return target;
        }

        private HandProjectile Projectile(float damage, bool owned = true)
        {
            Physics.SyncTransforms();
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.layer = 2;
            go.GetComponent<Collider>().enabled = false;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.position = Origin;
            var projectile = go.AddComponent<HandProjectile>();
            projectile.Initialize(Vector3.forward, damage, shooter, owned ? weapon : null, 1);
            return projectile;
        }

        [Test] public void PlayerDamageBreaksOneUnitPerShotAndBreakingHitStillUsesPreviousDefense()
        {
            var target = Target(Vector3.forward);
            weapon.Damage(target, 4f, 1);
            Assert.That(target.Health, Is.EqualTo(99f));
            Assert.That(target.DefenseBar, Is.EqualTo(1));
            weapon.Damage(target, 8f, 2);
            Assert.That(target.Health, Is.EqualTo(97f));
            Assert.That(target.DefensePoints, Is.EqualTo(1));
            Assert.That(target.DefenseBar, Is.EqualTo(2));
            weapon.Damage(target, 8f, 3);
            Assert.That(target.Health, Is.EqualTo(93f));
            weapon.Damage(target, 8f, 4);
            Assert.That(target.Health, Is.EqualTo(89f));
            Assert.That(target.DefensePoints, Is.Zero);
            Assert.That(target.DefenseBar, Is.Zero);
            weapon.Damage(target, 8f, 5);
            Assert.That(target.Health, Is.EqualTo(81f));
        }

        [Test] public void NonPlayerDamageIsReducedWithoutWearingDefenseAndInvalidDamageDoesNothing()
        {
            var target = Target(Vector3.forward);
            target.TakeDamage(16f);
            Assert.That(target.Health, Is.EqualTo(96f));
            Assert.That(target.DefenseBar, Is.EqualTo(2));
            Assert.That(target.TakePlayerShotDamage(0f), Is.False);
            Assert.That(target.TakePlayerShotDamage(-4f), Is.False);
            Assert.That(target.DefenseBar, Is.EqualTo(2));
            Assert.That(target.DefensePoints, Is.EqualTo(2));
        }

        [Test] public void HitFeedbackAndMetricsReportReducedDamageAndCapOverkill()
        {
            var target = Target(Vector3.forward);
            float indicator = 0f, effective = 0f;
            bool killed = false;
            weapon.Hit += value => indicator = value;
            weapon.DamageDealt += (id, victim, amount, death) => { effective = amount; killed = death; };
            weapon.Damage(target, 8f, 1);
            Assert.That(indicator, Is.EqualTo(2f));
            Assert.That(effective, Is.EqualTo(2f));
            Assert.That(weapon.LastHitDamage, Is.EqualTo(2f));
            weapon.Damage(target, 800f, 2);
            Assert.That(effective, Is.EqualTo(98f));
            Assert.That(killed, Is.True);
            Assert.That(target.TakePlayerShotDamage(8f), Is.False);
        }

        [Test] public void DefenseDisplayDisappearsAtZeroAndResetRestoresInitialState()
        {
            enemySettings.initialDefensePoints = 1;
            var target = Target(Vector3.forward, true);
            var display = target.GetComponentInChildren<EnemyDefenseDisplay>();
            Assert.That(display, Is.Not.Null);
            Assert.That(display.gameObject.activeSelf, Is.True);
            weapon.Damage(target, 1f);
            var fill = display.transform.Find("Defense Bar/Defense Fill").GetComponent<RectTransform>();
            Assert.That(fill.sizeDelta.x, Is.EqualTo(70f));
            weapon.Damage(target, 1f);
            Assert.That(display.gameObject.activeSelf, Is.False);
            Assert.That(target.label.text, Does.Not.Contain("DEF"));
            target.ResetTarget();
            Assert.That(target.DefensePoints, Is.EqualTo(1));
            Assert.That(target.DefenseBar, Is.EqualTo(2));
            Assert.That(target.Health, Is.EqualTo(100f));
            Assert.That(display.gameObject.activeSelf, Is.True);
            Assert.That(fill.sizeDelta.x, Is.EqualTo(140f));
        }

        [Test] public void ProjectileImpactAndSplashCountOncePerEnemyEvenWithMultipleColliders()
        {
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0f, 1.3f));
            var outside = Target(new Vector3(8f, 0f, 1f));
            var child = new GameObject("Extra collider", typeof(BoxCollider));
            child.transform.SetParent(nearby.transform, false);
            var projectile = Projectile(8f);
            projectile.Simulate(1.2f / shooter.EvaluateProjectileSpeed(8f));
            Assert.That(direct.Health, Is.EqualTo(97f)); // (8 direct + 4 area) / 4.
            Assert.That(nearby.Health, Is.EqualTo(99f));
            Assert.That(direct.DefenseBar, Is.EqualTo(1));
            Assert.That(nearby.DefenseBar, Is.EqualTo(1));
            Assert.That(outside.DefenseBar, Is.EqualTo(2));
            projectile.Detonate(direct.GetComponent<Collider>(), direct.transform.position, Vector3.back);
            Assert.That(direct.DefenseBar, Is.EqualTo(1));
            Assert.That(nearby.DefenseBar, Is.EqualTo(1));
        }

        [Test] public void ProjectileWithoutOwnerStillWearsDefense()
        {
            shooter.explosionDamageFraction = 0f;
            var target = Target(Vector3.forward);
            Projectile(8f, false).Simulate(1.2f / shooter.EvaluateProjectileSpeed(8f));
            Assert.That(target.Health, Is.EqualTo(98f));
            Assert.That(target.DefenseBar, Is.EqualTo(1));
        }

        [Test] public void CoverPreventsSplashFromWearingDefense()
        {
            var direct = Target(Vector3.forward);
            var nearby = Target(new Vector3(0.9f, 0f, 1.3f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Origin + new Vector3(0.45f, 0f, 0.9f);
            wall.transform.localScale = new Vector3(0.08f, 2f, 1f);
            Projectile(8f).Simulate(1.2f / shooter.EvaluateProjectileSpeed(8f));
            Assert.That(direct.DefenseBar, Is.EqualTo(1));
            Assert.That(nearby.Health, Is.EqualTo(100f));
            Assert.That(nearby.DefenseBar, Is.EqualTo(2));
        }
    }
}
