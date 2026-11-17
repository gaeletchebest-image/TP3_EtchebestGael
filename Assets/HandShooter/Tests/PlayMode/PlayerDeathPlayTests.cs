using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class PlayerDeathPlayTests
    {
        private TurretSettings turret;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1f;
            turret = ScriptableObject.CreateInstance<TurretSettings>();
            turret.projectileSpeed = 60f;
        }

        [TearDown] public void TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(turret);
        }

        [UnityTest] public IEnumerator OffscreenProjectileAutomaticallyFramesTheAttacker() => RunTurretDeath(false);

        [UnityTest] public IEnumerator CameraStaysOnTheNearSideOfAWallBehindTheAttacker() => RunTurretDeath(true);

        [UnityTest] public IEnumerator TimeoutAutomaticallyMovesToAThirdPersonViewOfTheArea()
        {
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            var player = PlacePlayer();
            var camera = player.viewCamera;
            Vector3 firstPosition = camera.transform.position;
            player.Die("Tiempo agotado");

            yield return WaitForSummary();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Details(), Is.EqualTo("Se agotó el tiempo."));
            Assert.That(camera.transform.position.z, Is.LessThan(firstPosition.z - 2.5f));
        }

        private IEnumerator RunTurretDeath(bool wallBehindAttacker)
        {
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            var player = PlacePlayer();
            var camera = player.viewCamera;
            Vector3 firstPosition = camera.transform.position;
            Vector3 strike = player.GetComponent<CharacterController>().bounds.center;
            var attacker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            attacker.transform.position = strike + new Vector3(2.7f, 0f, -4f);
            Vector3 fireDirection = (strike - attacker.transform.position).normalized;
            Vector3 wallPosition = attacker.transform.position - fireDirection * 1.7f + Vector3.up;
            if (wallBehindAttacker)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetPositionAndRotation(wallPosition, Quaternion.LookRotation(fireDirection));
                wall.transform.localScale = new Vector3(10f, 5f, 0.2f);
            }

            var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.GetComponent<Collider>().enabled = false;
            shot.transform.position = attacker.transform.position;
            shot.AddComponent<TurretProjectile>().Initialize(fireDirection, turret, attacker.transform);
            yield return WaitFor(() => player.IsDead);
            yield return WaitForSummary();

            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Details(), Is.EqualTo("Ataque desde atrás a la derecha."));
            Assert.That(GameObject.Find("Player Death Review/Death Summary/Cause").GetComponent<Text>().text,
                Does.Contain("TORRETA"));
            Vector3 finalPosition = camera.transform.position;
            Assert.That(Vector3.Distance(firstPosition, finalPosition), Is.GreaterThan(3f));
            float distanceBehindAttacker = -Vector3.Dot(finalPosition - attacker.transform.position, fireDirection);
            Assert.That(distanceBehindAttacker, Is.GreaterThan(wallBehindAttacker ? 0.5f : 2.5f));
            if (wallBehindAttacker)
                Assert.That(Vector3.Dot(finalPosition - wallPosition, fireDirection), Is.GreaterThan(0.18f));
            Vector3 onScreen = camera.WorldToViewportPoint(attacker.transform.position);
            Assert.That(onScreen.x, Is.InRange(0.05f, 0.95f));
            Assert.That(onScreen.y, Is.InRange(0.05f, 0.95f));
            Assert.That(onScreen.z, Is.GreaterThan(0f));
            yield return null;
            Assert.That(camera.transform.position, Is.EqualTo(finalPosition));
        }

        private static PrototypePlayer PlacePlayer()
        {
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.SetMenuOpen(false);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(300f, 10f, 300f), Quaternion.identity);
            controller.enabled = true;
            player.viewCamera.transform.localRotation = Quaternion.identity;
            return player;
        }

        private static string Details() => GameObject.Find("Player Death Review/Death Summary/Attack Details")
            .GetComponent<Text>().text;

        private static IEnumerator WaitForSummary() =>
            WaitFor(() => GameObject.Find("Player Death Review/Death Summary/Attack Details"));

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True);
        }
    }
}
