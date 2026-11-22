using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class KamikazePlayTests
    {
        private Scene arena, previousScene;
        private KamikazeSettings settings;
        private ShooterSettings playerSettings;
        private Material material;
        private PrototypePlayer player;
        private KamikazeEnemy kamikaze;
        private DamageTarget health;
        private static readonly Vector3 Origin = new Vector3(400f, 10f, 400f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            arena = SceneManager.CreateScene("Kamikaze test arena");
            SceneManager.SetActiveScene(arena);
            settings = ScriptableObject.CreateInstance<KamikazeSettings>();
            settings.detectionRange = 5f;
            settings.moveSpeed = 5f;
            settings.initialDefensePoints = 1;
            playerSettings = ScriptableObject.CreateInstance<ShooterSettings>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            var go = new GameObject("Kamikaze test player");
            go.SetActive(false);
            go.layer = 2;
            go.transform.position = Origin;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            player = go.AddComponent<PrototypePlayer>();
            player.settings = playerSettings;
            player.enabled = false;
            go.SetActive(true);

            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.SetActive(false);
            enemy.transform.position = Origin + Vector3.forward * 20f;
            enemy.GetComponent<Renderer>().sharedMaterial = material;
            health = enemy.AddComponent<DamageTarget>();
            health.kamikazeSettings = settings;
            kamikaze = enemy.AddComponent<KamikazeEnemy>();
            kamikaze.settings = settings;
            kamikaze.target = player;
            enemy.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            // Scene startup can pause the clock while the test arena is initializing.
            player.SetMenuOpen(false);
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(arena);
            Object.Destroy(settings);
            Object.Destroy(playerSettings);
            Object.Destroy(material);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator ArmoredHitRevealsPlayerOutsideRangeAndBehindCover()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Origin + new Vector3(0f, 1f, 10f);
            wall.transform.localScale = new Vector3(5f, 4f, 0.5f);
            Physics.SyncTransforms();
            Vector3 start = kamikaze.transform.position;
            yield return new WaitForSeconds(0.1f);
            Assert.That(kamikaze.transform.position, Is.EqualTo(start));

            Assert.That(health.TakePlayerShotDamage(1f), Is.True);
            Assert.That(health.DefensePoints, Is.EqualTo(1));
            yield return new WaitForSeconds(0.2f);
            Assert.That(kamikaze.transform.position.z, Is.LessThan(start.z - 0.5f));
            Assert.That(player.IsDead, Is.False);

            // Once alerted, moving even farther away does not clear detection.
            player.transform.position -= Vector3.forward * 10f;
            start = kamikaze.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(kamikaze.transform.position.z, Is.LessThan(start.z - 0.5f));
        }

        [UnityTest]
        public IEnumerator InvalidHitsAndDisabledOrDefeatedEnemyDoNotAlert()
        {
            Vector3 start = kamikaze.transform.position;
            Assert.That(health.TakePlayerShotDamage(0f), Is.False);
            Assert.That(health.TakePlayerShotDamage(-1f), Is.False);
            yield return new WaitForSeconds(0.1f);
            Assert.That(kamikaze.transform.position, Is.EqualTo(start));
            kamikaze.enabled = false;
            health.TakePlayerShotDamage(1f);
            kamikaze.enabled = true;
            yield return new WaitForSeconds(0.1f);
            Assert.That(kamikaze.transform.position, Is.EqualTo(start));
            health.TakePlayerShotDamage(10000f);
            yield return null;
            Assert.That(kamikaze == null, Is.True);
            Assert.That(player.IsDead, Is.False);
        }
    }
}
