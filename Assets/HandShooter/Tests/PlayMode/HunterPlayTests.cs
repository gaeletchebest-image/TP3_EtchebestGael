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

        private IEnumerator Spawn(float distance = 12f, Transform patrolCenter = null)
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
            hunter.patrolCenter = patrolCenter;
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

        private HandProjectile LaunchProjectile(Vector3 position, Vector3 direction, float damage = 8f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Hunter test projectile";
            go.layer = 2;
            go.GetComponent<Collider>().enabled = false;
            go.GetComponent<Renderer>().sharedMaterial = hunter.GetComponent<Renderer>().sharedMaterial;
            go.transform.position = position;
            var projectile = go.AddComponent<HandProjectile>();
            projectile.enabled = false;
            projectile.Initialize(direction, damage, playerSettings, null);
            return projectile;
        }

        [UnityTest]
        public IEnumerator ProjectileHitsHunterEnteringItsVolumeAfterLaunch()
        {
            yield return Spawn(18f);
            var health = hunter.GetComponent<DamageTarget>();
            health.TakeDamage(1f); // Alert the hunter as a ray hit would.
            hunter.enabled = false;
            hunter.GetComponent<NavMeshAgent>().enabled = false;
            playerSettings.projectileSpeed = 0.1f;
            Vector3 start = hunter.GetComponent<Collider>().bounds.center - Vector3.forward * 2f;
            var projectile = LaunchProjectile(start, Vector3.forward);
            hunter.transform.position -= Vector3.forward * 2f;
            // Deliberately omit SyncTransforms: the projectile must handle NavMesh/Transform movement.
            projectile.Simulate(0.1f);
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth - 1f - 12f));
            projectile.Simulate(0.1f);
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth - 1f - 12f));
            yield return null;
            Assert.That(projectile == null, Is.True);
        }

        [UnityTest]
        public IEnumerator ProjectileHitsHunterCrossingCompletelyBetweenFrames()
        {
            yield return Spawn(18f);
            hunter.enabled = false;
            hunter.GetComponent<NavMeshAgent>().enabled = false;
            playerSettings.projectileSpeed = 0.1f;
            var health = hunter.GetComponent<DamageTarget>();
            var projectile = LaunchProjectile(hunter.GetComponent<Collider>().bounds.center - Vector3.forward * 2f,
                Vector3.forward);
            hunter.transform.position -= Vector3.forward * 4f;
            projectile.Simulate(0.2f);
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth - 12f));
            yield return null;
            Assert.That(projectile == null, Is.True);
        }

        [UnityTest]
        public IEnumerator CrossingTheFlightLineAtDifferentTimesDoesNotCauseAFalseHit()
        {
            yield return Spawn(18f);
            hunter.enabled = false;
            hunter.GetComponent<NavMeshAgent>().enabled = false;
            hunter.transform.position -= Vector3.right * 4f;
            playerSettings.projectileSpeed = 40f;
            playerSettings.projectileSpeedDamageExponent = 0f;
            var health = hunter.GetComponent<DamageTarget>();
            Vector3 start = hunter.transform.position + Vector3.right * 4f - Vector3.forward * 2f;
            var projectile = LaunchProjectile(start, Vector3.forward);
            hunter.transform.position += Vector3.right * 8f;
            projectile.Simulate(0.2f);
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth));
            Assert.That(Vector3.Distance(projectile.transform.position, start + Vector3.forward * 8f), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator EarlierWallImpactPreventsDirectHitOnMovingHunter()
        {
            yield return Spawn(18f);
            hunter.enabled = false;
            hunter.GetComponent<NavMeshAgent>().enabled = false;
            playerSettings.projectileSpeed = 40f;
            playerSettings.projectileSpeedDamageExponent = 0f;
            playerSettings.explosionDamageFraction = 0f;
            Wall(12f);
            var health = hunter.GetComponent<DamageTarget>();
            var projectile = LaunchProjectile(hunter.GetComponent<Collider>().bounds.center - Vector3.forward * 8f,
                Vector3.forward);
            hunter.transform.position -= Vector3.forward * 4f;
            projectile.Simulate(0.2f);
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth));
            yield return null;
            Assert.That(projectile == null, Is.True);
        }

        [UnityTest]
        public IEnumerator AlertedHunterRunningIntoSlowProjectileTakesDamageWithNormalUpdates()
        {
            settings.chaseSpeed = 20f;
            yield return Spawn(18f);
            var health = hunter.GetComponent<DamageTarget>();
            health.TakeDamage(1f);
            playerSettings.projectileSpeed = 0.1f;
            var projectile = LaunchProjectile(new Vector3(ArenaOrigin.x, hunter.GetComponent<Collider>().bounds.center.y, 10f),
                Vector3.forward);
            projectile.enabled = true;
            Time.timeScale = 1f;
            float started = Time.realtimeSinceStartup;
            while (health.Health == settings.initialHealth - 1f && Time.realtimeSinceStartup - started < 3f)
                yield return null;
            Time.timeScale = 0f;
            Assert.That(health.Health, Is.EqualTo(settings.initialHealth - 1f - 12f));
            Assert.That(player.IsDead, Is.False);
            yield return null;
            Assert.That(projectile == null, Is.True);
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
        public IEnumerator UnalertedHunterPatrolsOutsideItsShortRange()
        {
            yield return Spawn(18f);
            Vector3 start = hunter.transform.position;
            hunter.Simulate(0.01f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
            Assert.That(hunter.IsAlerted, Is.False);
            Assert.That(agent.isStopped, Is.False);
            Assert.That(agent.speed, Is.EqualTo(settings.patrolSpeed));
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.3f);
            Time.timeScale = 0f;
            Assert.That(Vector3.Distance(hunter.transform.position, start), Is.GreaterThan(0.1f));
            Assert.That(hunter.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator PatrolWaitsWithinConfiguredBoundsBeforeChoosingAnotherPoint()
        {
            settings.patrolWaitMin = 0.5f;
            settings.patrolWaitMax = 1.2f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            for (int visit = 0; visit < 5; visit++)
            {
                Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
                Vector3 destination = agent.destination;
                Assert.That(agent.Warp(destination), Is.True);
                hunter.Simulate(0.001f);
                Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
                Assert.That(hunter.statusLabel.text, Is.EqualTo("ESPERANDO"));
                Assert.That(agent.isStopped, Is.True);
                Vector3 restingPosition = hunter.transform.position;
                hunter.Simulate(0.49f);
                Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
                Assert.That(hunter.transform.position, Is.EqualTo(restingPosition));
                hunter.Simulate(0.72f);
                Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
                Assert.That(agent.isStopped, Is.False);
                Vector3 step = agent.destination - destination;
                step.y = 0f;
                Assert.That(step.magnitude, Is.GreaterThanOrEqualTo(0.99f));
                Vector3 fromCenter = agent.destination - (ArenaOrigin + Vector3.forward * 18f);
                fromCenter.y = 0f;
                Assert.That(fromCenter.magnitude, Is.LessThanOrEqualTo(settings.patrolRadius + 0.01f));
            }
            Assert.That(player.IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator PatrolUsesAssignedCenterAndRejectsUnreachableDestinations()
        {
            settings.patrolRadius = 2f;
            var center = new GameObject("Assigned patrol center").transform;
            center.position = ArenaOrigin + new Vector3(70f, 0f, 18f);
            // This island has a NavMesh, but no complete route from the hunter's floor.
            var island = GameObject.CreatePrimitive(PrimitiveType.Cube);
            island.transform.position = center.position + Vector3.down * 0.25f;
            island.transform.localScale = new Vector3(10f, 0.5f, 10f);
            yield return Spawn(18f, center);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(NavMesh.SamplePosition(center.position, out _, 2f, agent.areaMask), Is.True);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            Assert.That(agent.isStopped, Is.True);
            Assert.That(agent.hasPath, Is.False);

            center.position = ArenaOrigin + new Vector3(10f, 0f, 18f);
            hunter.Simulate(settings.patrolWaitMax + 0.1f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
            Vector3 fromCenter = agent.destination - center.position;
            fromCenter.y = 0f;
            Assert.That(fromCenter.magnitude, Is.LessThanOrEqualTo(settings.patrolRadius + 0.01f));
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [UnityTest]
        public IEnumerator PatrolNaturallyArrivesAndWaitsAtItsDestination()
        {
            settings.patrolRadius = 2f;
            settings.patrolSpeed = 4f;
            settings.patrolWaitMin = settings.patrolWaitMax = 2f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Vector3 destination = agent.destination;
            Time.timeScale = 1f;
            float started = Time.realtimeSinceStartup;
            while (hunter.State != HunterEnemy.BehaviourState.Waiting &&
                Time.realtimeSinceStartup - started < 4f) yield return null;
            Time.timeScale = 0f;
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            Vector3 remaining = destination - hunter.transform.position;
            remaining.y = 0f;
            Assert.That(remaining.magnitude, Is.LessThanOrEqualTo(agent.stoppingDistance + 0.1f));
            Assert.That(agent.isStopped, Is.True);
            hunter.Simulate(2.1f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
        }

        [UnityTest]
        public IEnumerator DamageInterruptsPatrolWaitingImmediately()
        {
            settings.patrolWaitMin = settings.patrolWaitMax = 30f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(agent.destination), Is.True);
            hunter.Simulate(0.01f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            hunter.GetComponent<DamageTarget>().TakeDamage(1f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Chasing));
            Assert.That(agent.speed, Is.EqualTo(settings.chaseSpeed));
            Assert.That(agent.isStopped, Is.False);
            MoveHunter(3f);
            hunter.Simulate(0.001f);
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator WaitingHunterStillDetectsPlayerAndFiresImmediately()
        {
            settings.patrolWaitMin = settings.patrolWaitMax = 30f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(agent.destination), Is.True);
            hunter.Simulate(0.01f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            MoveHunter(3f);
            hunter.Simulate(0.001f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Firing));
            Assert.That(hunter.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
            Assert.That(agent.isStopped, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseFreezesPatrolWaitAndDisablingStopsMovement()
        {
            settings.patrolWaitMin = settings.patrolWaitMax = 1f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(agent.destination), Is.True);
            hunter.Simulate(0.001f);
            hunter.Simulate(0f);
            hunter.Simulate(0.9f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            hunter.Simulate(0.11f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
            hunter.enabled = false;
            Assert.That(agent.isStopped, Is.True);
            Assert.That(agent.hasPath, Is.False);
            hunter.enabled = true;
            hunter.Simulate(0.01f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
            Assert.That(agent.isStopped, Is.False);
        }

        [UnityTest]
        public IEnumerator ZeroPatrolRadiusKeepsHunterIdleAndInvalidWaitBoundsAreSafe()
        {
            settings.patrolRadius = 0f;
            settings.patrolWaitMin = 0.5f;
            settings.patrolWaitMax = -1f;
            yield return Spawn(18f);
            var agent = hunter.GetComponent<NavMeshAgent>();
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Normal));
            Assert.That(agent.isStopped, Is.True);
            settings.patrolRadius = 2f;
            hunter.Simulate(0.01f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
            Assert.That(agent.Warp(agent.destination), Is.True);
            hunter.Simulate(0.001f);
            hunter.Simulate(0.49f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Waiting));
            hunter.Simulate(0.02f);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling));
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
            Assert.That(hunter.IsAlerted, Is.False);
            Assert.That(hunter.State, Is.EqualTo(HunterEnemy.BehaviourState.Patrolling)
                .Or.EqualTo(HunterEnemy.BehaviourState.Waiting));
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
