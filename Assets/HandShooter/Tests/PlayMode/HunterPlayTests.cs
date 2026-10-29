using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class HunterPlayTests
    {
        private Scene arena, previousScene;
        private HunterSettings settings;
        private ShooterSettings playerSettings;
        private PrototypePlayer player;
        private HunterEnemy hunter;
        private readonly List<GameObject> suspendedObjects = new List<GameObject>();
        private static readonly Vector3 ArenaOrigin = new Vector3(300f, 0f, 0f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            var prototype = SceneManager.GetSceneByName("HandShooterPrototype");
            if (prototype.IsValid() && prototype.isLoaded)
                foreach (var root in prototype.GetRootGameObjects())
                    if (root.activeSelf) { suspendedObjects.Add(root); root.SetActive(false); }
            arena = SceneManager.CreateScene("Hunter test arena");
            SceneManager.SetActiveScene(arena);
            settings = ScriptableObject.CreateInstance<HunterSettings>();
            playerSettings = ScriptableObject.CreateInstance<ShooterSettings>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = ArenaOrigin + new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(80f, 0.5f, 80f);
            var go = new GameObject("Hunter test player");
            go.SetActive(false);
            go.layer = 2;
            go.transform.position = ArenaOrigin + new Vector3(0f, 0.05f, 0f);
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.radius = 0.3f;
            player = go.AddComponent<PrototypePlayer>();
            player.settings = playerSettings;
            player.enabled = false;
            player.viewCamera = new GameObject("Hunter test camera").AddComponent<Camera>();
            player.viewCamera.transform.SetParent(go.transform, false);
            player.viewCamera.transform.localPosition = Vector3.up * 1.65f;
            player.deathScreen = new GameObject("Hunter test death screen");
            player.deathScreen.SetActive(false);
            go.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SceneManager.SetActiveScene(previousScene);
            yield return SceneManager.UnloadSceneAsync(arena);
            foreach (var root in suspendedObjects) if (root) root.SetActive(true);
            suspendedObjects.Clear();
            Object.Destroy(settings);
            Object.Destroy(playerSettings);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator Spawn(float distance = 12f)
        {
            Time.timeScale = 0f;
            var holder = new GameObject("Hunter spawn");
            holder.SetActive(false);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/HandShooter/Prefabs/EnemyHunter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var go = Object.Instantiate(prefab, holder.transform);
#else
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.SetActive(false);
            go.transform.SetParent(holder.transform, false);
            go.AddComponent<DamageTarget>();
            go.AddComponent<NavMeshAgent>().enabled = false;
            go.AddComponent<HunterEnemy>();
#endif
            go.transform.position = ArenaOrigin + new Vector3(0f, 1f, distance);
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            go.GetComponent<DamageTarget>().hunterSettings = settings;
            hunter = go.GetComponent<HunterEnemy>();
            hunter.settings = settings;
            hunter.target = player;
            holder.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Time.timeScale = 1f;
            yield return null;
            Time.timeScale = 0f;
            Physics.SyncTransforms();
            Assert.That(hunter.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
            Assert.That(hunter.GetComponent<DamageTarget>().Health, Is.EqualTo(settings.initialHealth));
        }

        private void MoveHunter(float distance)
        {
            Assert.That(hunter.GetComponent<NavMeshAgent>().Warp(ArenaOrigin + Vector3.forward * distance), Is.True);
            Physics.SyncTransforms();
        }

        private static GameObject Wall(float z, float width = 5f)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Hunter test cover";
            wall.transform.position = ArenaOrigin + new Vector3(0f, 1.5f, z);
            wall.transform.localScale = new Vector3(width, 3f, 0.5f);
            Physics.SyncTransforms();
            return wall;
        }

        [UnityTest]
        public IEnumerator NormalStateStaysIdleOutsideItsShortRange()
        {
            yield return Spawn();
            Vector3 start = hunter.transform.position;
            hunter.Simulate(10f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Normal));
            Assert.That(hunter.IsAlerted, Is.False);
            Assert.That(hunter.GetComponent<NavMeshAgent>().isStopped, Is.True);
            Assert.That(hunter.transform.position, Is.EqualTo(start));
            Assert.That(hunter.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator SharedRangeFiresImmediatelyAtBoundaryButNotOutside()
        {
            yield return Spawn();
            MoveHunter(settings.range + 0.05f);
            hunter.Simulate(1f);
            Assert.That(hunter.ShotsFired, Is.Zero);
            MoveHunter(settings.range);
            hunter.Simulate(0.001f);
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
            Assert.That(player.deathScreen.activeSelf, Is.True);
            var beam = hunter.transform.Find("Hunter hitscan flash").GetComponent<LineRenderer>();
            Assert.That(beam.enabled, Is.True);
            Assert.That(beam.startWidth, Is.EqualTo(settings.beamWidth));
            Assert.That(Object.FindFirstObjectByType<TurretProjectile>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<HandProjectile>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator RealPrefabKillsWithNormalUpdatesAndCameraOutsideBody()
        {
            yield return Spawn();
            player.viewCamera.transform.localPosition = new Vector3(2f, 3f, 0f);
            MoveHunter(3f);
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator CoverBlocksDetectionAndShotUntilPlayerIsVisible()
        {
            yield return Spawn();
            MoveHunter(4f);
            var wall = Wall(2f);
            hunter.Simulate(1f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Normal));
            Assert.That(hunter.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            Object.Destroy(wall);
            yield return null;
            Physics.SyncTransforms();
            hunter.Simulate(0.001f);
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator DamageImmediatelyAlertsOutsideRangeAndDoesNotExtendAttackRange()
        {
            yield return Spawn();
            var health = hunter.GetComponent<DamageTarget>();
            Assert.That(health.TakeDamage(1f), Is.True);
            Assert.That(hunter.IsAlerted, Is.True);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Chasing));
            Assert.That(hunter.GetComponent<NavMeshAgent>().isStopped, Is.False);
            Assert.That(hunter.statusLabel.text, Is.EqualTo("¡ALERTA!"));
            hunter.Simulate(1f);
            Assert.That(hunter.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            MoveHunter(4.5f);
            hunter.Simulate(0.001f);
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator DamagedHunterRapidlyChasesMovingPlayerAndKillsOnEnteringRange()
        {
            yield return Spawn(18f);
            hunter.GetComponent<DamageTarget>().TakeDamage(1f);
            float startZ = hunter.transform.position.z;
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.5f);
            Assert.That(startZ - hunter.transform.position.z, Is.GreaterThan(3f));
            Assert.That(hunter.ShotsFired, Is.Zero);
            player.transform.position += Vector3.right * 5f;
            Physics.SyncTransforms();
            float started = Time.realtimeSinceStartup;
            while (!player.IsDead && Time.realtimeSinceStartup - started < 4f) yield return null;
            Assert.That(player.IsDead, Is.True);
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
            Vector3 offset = hunter.transform.position - player.transform.position;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(settings.range));
            Assert.That(hunter.GetComponent<NavMeshAgent>().isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator DamageDetectsThroughCoverAndNavigationGoesAroundWall()
        {
            Wall(3f, 6f);
            yield return Spawn();
            hunter.GetComponent<DamageTarget>().TakeDamage(1f);
            hunter.Simulate(0.1f);
            Assert.That(hunter.ShotsFired, Is.Zero);
            Time.timeScale = 1f;
            float furthestSide = 0f;
            float started = Time.realtimeSinceStartup;
            while (!player.IsDead && Time.realtimeSinceStartup - started < 6f)
            {
                furthestSide = Mathf.Max(furthestSide, Mathf.Abs(hunter.transform.position.x - ArenaOrigin.x));
                yield return null;
            }
            Assert.That(furthestSide, Is.GreaterThan(3f));
            Assert.That(player.IsDead, Is.True);
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ZeroDamageDoesNotAlertAndLethalDamagePreventsRetaliation()
        {
            yield return Spawn();
            var health = hunter.GetComponent<DamageTarget>();
            Assert.That(health.TakeDamage(0f), Is.False);
            Assert.That(health.TakeDamage(-1f), Is.False);
            Assert.That(hunter.IsAlerted, Is.False);
            MoveHunter(3f);
            health.TakeDamage(settings.initialHealth);
            hunter.Simulate(0.1f);
            Assert.That(hunter.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            yield return null;
            Assert.That(hunter == null, Is.True);
        }

        [UnityTest]
        public IEnumerator PausePreventsFiringAndDisabledEnemyCannotReactToDamage()
        {
            yield return Spawn();
            MoveHunter(3f);
            hunter.Simulate(0f);
            Assert.That(player.IsDead, Is.False);
            hunter.enabled = false;
            hunter.GetComponent<DamageTarget>().TakeDamage(1f);
            Assert.That(hunter.IsAlerted, Is.False);
            hunter.enabled = true;
            hunter.GetComponent<DamageTarget>().TakeDamage(1f);
            Assert.That(hunter.IsAlerted, Is.True);
            hunter.Simulate(0.001f);
            Assert.That(player.IsDead, Is.True);
        }
    }
}
