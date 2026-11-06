using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class ShotRangePlayTests
    {
        private static readonly Vector3 Origin = new Vector3(500f, 2f, 0f);
        private Scene arena, previousScene;
        private ShooterSettings shooter;
        private TurretSettings turret;
        private Material material;

        [SetUp] public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            arena = SceneManager.CreateScene("Shot range test arena");
            SceneManager.SetActiveScene(arena);
            shooter = ScriptableObject.CreateInstance<ShooterSettings>();
            turret = ScriptableObject.CreateInstance<TurretSettings>();
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            shooter.beamMaterial = turret.explosionMaterial = material;
        }

        [UnityTearDown] public IEnumerator CleanUp()
        {
            Time.timeScale = 1f;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(arena);
            Object.Destroy(shooter);
            Object.Destroy(turret);
            Object.Destroy(material);
        }

        private GameObject Shot()
        {
            var go = new GameObject("Test projectile", typeof(MeshRenderer));
            go.transform.position = Origin;
            return go;
        }

        private ExplosionEffect Blast() => arena.GetRootGameObjects()
            .Select(root => root.GetComponent<ExplosionEffect>()).FirstOrDefault(effect => effect);

        private DamageTarget Target(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            go.transform.position = position;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var target = go.AddComponent<DamageTarget>();
            target.settings = shooter;
            go.SetActive(true);
            return target;
        }

        [TestCase(3f, 0f, true, 1, 0.055f)]
        [TestCase(3.75f, 3f, false, 4, 0.11f)]
        [TestCase(4.75f, 0f, false, 1, 0.055f)]
        [TestCase(4.75f, 0f, false, 4, 0.11f)]
        public void PlayerBeamMarksItsActualEndpoint(float targetDistance, float wallDistance, bool damagesTarget,
            int fingers, float expectedDiameter)
        {
            Time.timeScale = 1f;
            shooter.range = 5f;
            shooter.initialActiveFingers = fingers;
            var playerObject = new GameObject("Beam test player") { layer = 2 };
            playerObject.transform.position = Origin;
            var player = playerObject.AddComponent<PrototypePlayer>();
            player.enabled = false;
            var camera = playerObject.AddComponent<Camera>();
            camera.enabled = false;

            var weaponObject = new GameObject("Beam test weapon");
            weaponObject.SetActive(false);
            weaponObject.transform.position = Origin + new Vector3(0.5f, 0f, -1f);
            var weapon = weaponObject.AddComponent<HandWeapon>();
            weapon.enabled = false;
            weapon.settings = shooter;
            weapon.player = player;
            weapon.viewCamera = camera;
            weapon.muzzle = weaponObject.transform;
            weaponObject.SetActive(true);

            var target = Target(Origin + Vector3.forward * targetDistance);
            if (wallDistance > 0f)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = Origin + Vector3.forward * wallDistance;
            }
            Physics.SyncTransforms();

            Assert.That(weapon.TryFire(), Is.True);
            var beam = arena.GetRootGameObjects().Single(root => root.name == "Shot beam").GetComponent<LineRenderer>();
            Vector3 end = beam.GetPosition(1);
            var endpoint = beam.transform.Find("Beam endpoint");
            Assert.That(endpoint.position, Is.EqualTo(end));
            Assert.That(endpoint.GetComponent<Collider>(), Is.Null);
            Assert.That(endpoint.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(endpoint.localScale.x, Is.EqualTo(expectedDiameter).Within(0.001f));
            if (damagesTarget || wallDistance > 0f)
                Assert.That(end.z, Is.EqualTo(2.5f).Within(0.001f));
            else
                Assert.That(Vector3.Distance(weapon.muzzle.position, end), Is.EqualTo(shooter.range).Within(0.001f));
            float damage = damagesTarget ? weapon.Stats.Damage : 0f;
            Assert.That(target.Health, Is.EqualTo(shooter.targetHealth - damage));
        }

        [UnityTest] public IEnumerator SlowPlayerShotWarnsAndDealsSplashAtItsRange()
        {
            Time.timeScale = 0f;
            const float damage = 8f;
            shooter.projectileRange = 10f;
            shooter.projectileSpeed = 0.25f;
            var target = Target(Origin + new Vector3(1.2f, 0f, 10f));

            var shot = Shot().AddComponent<HandProjectile>();
            shot.Initialize(Vector3.forward, damage, shooter, null);
            var renderer = shot.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color launchColor = properties.GetColor("_BaseColor");
            Vector3 scale = shot.transform.localScale;
            float speed = shooter.EvaluateProjectileSpeed(damage);
            shot.Simulate(7f / speed);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(launchColor));

            shot.Simulate(2.5f / speed);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor").g, Is.GreaterThan(launchColor.g));
            Assert.That(shot.transform.localScale, Is.EqualTo(scale));
            Assert.That(Blast(), Is.Null);

            float initialHealth = target.Health;
            shot.Simulate(5f / speed);
            Assert.That(shot.transform.position.z, Is.EqualTo(10f).Within(0.001f));
            Assert.That(Blast().transform.position.z, Is.EqualTo(10f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(initialHealth - shooter.EvaluateExplosion(damage).Damage));
            yield return null;
            Assert.That(shot == null, Is.True);
        }

        [UnityTest] public IEnumerator TurretShotDetonatesAtRangeBeforeScenery()
        {
            Time.timeScale = 1f;
            turret.projectileRange = 2f;
            turret.projectileSpeed = 20f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Origin + Vector3.forward * 3f;
            var shot = Shot().AddComponent<TurretProjectile>();
            shot.Initialize(Vector3.forward, turret, null);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (shot && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(shot == null, Is.True);
            Assert.That(Blast().transform.position.z, Is.EqualTo(2f).Within(0.001f));
        }
    }
}
