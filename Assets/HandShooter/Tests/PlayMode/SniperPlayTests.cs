using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class SniperPlayTests
    {
        private Scene arena, previousScene;
        private SniperSettings settings;
        private ShooterSettings playerSettings;
        private Material material;
        private PrototypePlayer player;
        private SniperEnemy sniper;
        private static readonly Vector3 ArenaOrigin = new Vector3(200f, 0f, 0f);
        private readonly List<GameObject> suspendedObjects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            // Other fixtures load the playable scene. Suspend it so its enemies cannot
            // end the game while this isolated arena is running.
            var prototype = SceneManager.GetSceneByName("HandShooterPrototype");
            if (prototype.IsValid() && prototype.isLoaded)
                foreach (var root in prototype.GetRootGameObjects())
                    if (root.activeSelf) { suspendedObjects.Add(root); root.SetActive(false); }
            arena = SceneManager.CreateScene("Sniper test arena");
            SceneManager.SetActiveScene(arena);
            settings = ScriptableObject.CreateInstance<SniperSettings>();
            settings.minimumAttackRange = 6f;
            settings.maximumAttackRange = 14f;
            settings.detectionRange = 40f;
            settings.settleDuration = 0.2f;
            settings.chargeDuration = 1f;
            settings.aimLockDuration = 0.25f;
            settings.shotCooldown = 0.6f;
            playerSettings = ScriptableObject.CreateInstance<ShooterSettings>();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = ArenaOrigin + new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(80f, 0.5f, 80f);
            var go = new GameObject("Sniper test player");
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
            player.viewCamera = new GameObject("Test camera").AddComponent<Camera>();
            player.viewCamera.transform.SetParent(go.transform, false);
            player.viewCamera.transform.localPosition = Vector3.up * 1.65f;
            player.deathScreen = new GameObject("Test death screen");
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
            Object.Destroy(material);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator Spawn(Vector3 position, bool usePrefab = false)
        {
            Time.timeScale = 0f; // Start initializes navigation/visuals without advancing the attack.
            GameObject go;
            var holder = new GameObject("Sniper spawn");
            holder.SetActive(false);
#if UNITY_EDITOR
            if (usePrefab)
                go = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/HandShooter/Prefabs/EnemySniper.prefab"), holder.transform);
            else
#endif
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.SetActive(false);
            go.transform.SetParent(holder.transform, false);
            go.transform.position = ArenaOrigin + position;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var health = go.GetComponent<DamageTarget>();
            if (!health) health = go.AddComponent<DamageTarget>();
            health.sniperSettings = settings;
            if (!go.GetComponent<NavMeshAgent>()) go.AddComponent<NavMeshAgent>();
            go.GetComponent<NavMeshAgent>().enabled = false;
            sniper = go.GetComponent<SniperEnemy>();
            if (!sniper) sniper = go.AddComponent<SniperEnemy>();
            sniper.settings = settings;
            sniper.target = player;
            if (!sniper.chargeOrb)
            {
                sniper.chargeOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                sniper.chargeOrb.SetParent(go.transform, false);
                Object.Destroy(sniper.chargeOrb.GetComponent<Collider>());
            }
            go.SetActive(true);
            holder.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            yield return null; // Allow the agent to bind to the newly built navigation mesh.
            sniper.enabled = false; // Advance combat explicitly; navigation still runs in Unity.
            Time.timeScale = 1f;
            yield return null; // Navigation only binds the agent after an unpaused engine update.
            Physics.SyncTransforms();
            Assert.That(sniper.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
        }

        private void BeginCharge()
        {
            sniper.Simulate(0.21f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Charging));
        }

        [UnityTest]
        public IEnumerator PlayerHitOutsideDetectionRangeStartsPersistentPursuit()
        {
            settings.detectionRange = 5f;
            settings.initialDefensePoints = 1;
            settings.chargeDuration = 30f;
            yield return Spawn(new Vector3(0f, 1f, 36f), true);
            sniper.enabled = true;
            Vector3 start = sniper.transform.position;
            var health = sniper.GetComponent<DamageTarget>();
            Assert.That(health.TakePlayerShotDamage(0f), Is.False);
            Assert.That(health.TakePlayerShotDamage(-1f), Is.False);
            yield return new WaitForSeconds(0.2f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Searching));
            Assert.That(Vector3.Distance(start, sniper.transform.position), Is.LessThan(0.1f));

            Assert.That(health.TakePlayerShotDamage(1f), Is.True);
            Assert.That(health.DefensePoints, Is.EqualTo(1), "An armored hit must also reveal the player.");
            yield return new WaitForSeconds(0.5f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Repositioning));
            Assert.That(start.z - sniper.transform.position.z, Is.GreaterThan(1f));
            Assert.That(Vector3.Distance(sniper.transform.position, player.transform.position),
                Is.GreaterThan(settings.detectionRange));
            Assert.That(sniper.ShotsFired, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HitOutsideDetectionRangePursuesAroundCoverBeforeCharging()
        {
            settings.detectionRange = 5f;
            settings.chargeDuration = 30f;
            Wall(new Vector3(0f, 1.5f, 5f));
            yield return Spawn(new Vector3(0f, 1f, 18f), true);
            sniper.enabled = true;
            sniper.Simulate(0.1f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Searching));
            sniper.GetComponent<DamageTarget>().TakePlayerShotDamage(1f);
            sniper.Simulate(0.1f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Repositioning));
            Assert.That(sniper.ShotsFired, Is.Zero);

            Vector3 start = sniper.transform.position;
            float started = Time.realtimeSinceStartup;
            while (sniper.State != SniperEnemy.AttackState.Charging && Time.realtimeSinceStartup - started < 9f)
                yield return null;
            Assert.That(Vector3.Distance(start, sniper.transform.position), Is.GreaterThan(1f));
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Charging));
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator HitDuringChargePreservesAttackProgress()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            Time.timeScale = 0f;
            sniper.enabled = true;
            BeginCharge();
            sniper.Simulate(0.8f);
            float progress = sniper.ChargeProgress;
            Assert.That(sniper.IsAimLocked, Is.True);
            sniper.GetComponent<DamageTarget>().TakePlayerShotDamage(1f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Charging));
            Assert.That(sniper.ChargeProgress, Is.EqualTo(progress));
            Assert.That(sniper.IsAimLocked, Is.True);
            sniper.Simulate(0.21f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator RealPrefabKillsPlayerUsingNormalFrameUpdates()
        {
            yield return Spawn(new Vector3(6f, 1f, 8f), true);
            sniper.enabled = true;
            float started = Time.realtimeSinceStartup;
            while (!player.IsDead && Time.realtimeSinceStartup - started < 4f) yield return null;
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
            Assert.That(player.deathScreen.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator RealPrefabPreviewTargetsPlayerImmediatelyAfterTurning()
        {
            yield return Spawn(new Vector3(6f, 1f, 8f), true);
            BeginCharge();
            var line = sniper.transform.Find("Sniper aim trajectory").GetComponent<LineRenderer>();
            Vector3 origin = line.GetPosition(0);
            Vector3 end = line.GetPosition(1);
            RaycastHit closest = default;
            float distance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, (end - origin).normalized, Vector3.Distance(origin, end) + 0.1f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(sniper.transform) || hit.distance >= distance) continue;
                closest = hit;
                distance = hit.distance;
            }
            Assert.That(closest.collider, Is.Not.Null, $"Trajectory from {origin} to {end} misses the player.");
            Assert.That(closest.collider.GetComponentInParent<PrototypePlayer>(), Is.EqualTo(player));
        }

        [UnityTest]
        public IEnumerator RealPrefabHitsPlayerBodyWhenCameraIsOutsideTheHitbox()
        {
            player.viewCamera.transform.localPosition = new Vector3(0.8f, 2.4f, 0f);
            Physics.SyncTransforms();
            yield return Spawn(new Vector3(6f, 1f, 8f), true);
            float elapsed = 0f;
            while (sniper.ShotsFired == 0 && elapsed < 3f)
            {
                sniper.Simulate(0.02f);
                elapsed += 0.02f;
            }
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator RealPrefabAimsAtAndKillsStationaryPlayer()
        {
            yield return Spawn(new Vector3(6f, 1f, 8f), true);
            Vector3 position = sniper.transform.position;
            float elapsed = 0f;
            while (sniper.ShotsFired == 0 && elapsed < 3f)
            {
                sniper.Simulate(0.02f);
                elapsed += 0.02f;
            }
            var line = sniper.transform.Find("Sniper hitscan flash").GetComponent<LineRenderer>();
            string shot = $"State={sniper.State}, position={sniper.transform.position}, target={player.viewCamera.transform.position}, " +
                $"start={line.GetPosition(0)}, end={line.GetPosition(1)}, stopped={sniper.GetComponent<NavMeshAgent>().isStopped}";
            Assert.That(sniper.ShotsFired, Is.EqualTo(1), shot);
            Assert.That(player.IsDead, Is.True, shot);
            Assert.That(sniper.transform.position, Is.EqualTo(position));
        }

        [UnityTest]
        public IEnumerator StopsTelegraphsAndHitsPlayerInstantlyAfterFullCharge()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            BeginCharge();
            Vector3 position = sniper.transform.position;
            sniper.Simulate(0.5f);
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            Assert.That(sniper.ChargeProgress, Is.InRange(0.49f, 0.51f));
            Assert.That(sniper.chargeOrb.gameObject.activeSelf, Is.True);
            Assert.That(sniper.transform.Find("Sniper aim trajectory").GetComponent<LineRenderer>().enabled, Is.True);
            Assert.That(sniper.transform.Find("Sniper charge progress").GetComponent<LineRenderer>().enabled, Is.True);
            Assert.That(sniper.GetComponent<NavMeshAgent>().isStopped, Is.True);
            Assert.That(sniper.transform.position, Is.EqualTo(position));
            sniper.Simulate(0.3f);
            Assert.That(sniper.IsAimLocked, Is.True);
            sniper.Simulate(0.21f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.True);
            Assert.That(sniper.transform.Find("Sniper hitscan flash").GetComponent<LineRenderer>().enabled, Is.True);
            Assert.That(Object.FindFirstObjectByType<TurretProjectile>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<HandProjectile>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator LockedShotCanBeDodgedAndCooldownPreventsImmediateRepeat()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            BeginCharge();
            sniper.Simulate(0.8f);
            Assert.That(sniper.IsAimLocked, Is.True);
            player.transform.position += Vector3.right * 2f;
            Physics.SyncTransforms();
            sniper.Simulate(0.21f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(player.IsDead, Is.False);
            sniper.Simulate(0.3f);
            Assert.That(sniper.ShotsFired, Is.EqualTo(1));
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Recovering));
        }

        [UnityTest]
        public IEnumerator BothRangeLimitsCancelChargeAndRequireFreshPreparation()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            BeginCharge();
            sniper.Simulate(0.6f);
            player.transform.position = ArenaOrigin + new Vector3(0f, 0.05f, -8f);
            Physics.SyncTransforms();
            sniper.Simulate(0.5f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Repositioning));
            Assert.That(sniper.ChargeProgress, Is.Zero);
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(sniper.chargeOrb.gameObject.activeSelf, Is.False);
            player.transform.position = ArenaOrigin + new Vector3(0f, 0.05f, 8f);
            Physics.SyncTransforms();
            sniper.Simulate(1f);
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Repositioning));
            player.transform.position = ArenaOrigin + new Vector3(0f, 0.05f, 0f);
            Physics.SyncTransforms();
            BeginCharge();
            sniper.Simulate(0.5f);
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(sniper.ChargeProgress, Is.InRange(0.49f, 0.51f));
        }

        [UnityTest]
        public IEnumerator CoverInterruptsChargeAndCannotBeShotThrough()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            BeginCharge();
            sniper.Simulate(0.8f);
            Wall(new Vector3(0f, 1.5f, 5f));
            Physics.SyncTransforms();
            sniper.Simulate(0.3f);
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Repositioning));
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(player.IsDead, Is.False);
            Assert.That(sniper.transform.Find("Sniper aim trajectory").GetComponent<LineRenderer>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator NavigatesAroundCoverToAVisibleFiringPosition()
        {
            Wall(new Vector3(0f, 1.5f, 5f));
            yield return Spawn(new Vector3(0f, 1f, 10f));
            settings.chargeDuration = 30f;
            Vector3 start = sniper.transform.position;
            float started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < 9f && sniper.State != SniperEnemy.AttackState.Charging)
            {
                sniper.Simulate(Time.deltaTime);
                yield return null;
            }
            Assert.That(Vector3.Distance(start, sniper.transform.position), Is.GreaterThan(1f));
            Assert.That(sniper.State, Is.EqualTo(SniperEnemy.AttackState.Charging));
            float distance = Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(sniper.transform.position.x, sniper.transform.position.z));
            Assert.That(distance, Is.InRange(settings.minimumAttackRange, settings.maximumAttackRange));
            Assert.That(player.IsDead, Is.False);
        }

        [UnityTest]
        public IEnumerator ApproachesWhenFarAndRetreatsWhenTooClose()
        {
            yield return Spawn(new Vector3(0f, 1f, 36f));
            settings.chargeDuration = 30f;
            Vector3 start = sniper.transform.position;
            Vector3 previous = start;
            float left = 0f, right = 0f;
            float elapsed = 0f;
            while (elapsed < 1f)
            {
                sniper.Simulate(Time.deltaTime);
                yield return null;
                elapsed += Time.deltaTime;
                Vector3 current = sniper.transform.position;
                left += Mathf.Max(0f, previous.x - current.x);
                right += Mathf.Max(0f, current.x - previous.x);
                previous = current;
            }
            Assert.That(start.z - sniper.transform.position.z, Is.GreaterThan(14.4f));
            Assert.That(left, Is.GreaterThan(0.5f));
            Assert.That(right, Is.GreaterThan(0.5f));

            player.transform.position = sniper.transform.position + Vector3.back * 2f - Vector3.up * 0.95f;
            Physics.SyncTransforms();
            start = sniper.transform.position;
            elapsed = 0f;
            while (elapsed < 0.5f)
            {
                sniper.Simulate(Time.deltaTime);
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.That(sniper.transform.position.z, Is.GreaterThan(start.z + 3f));
        }

        [UnityTest]
        public IEnumerator PauseAndDeathPreventFiringAndRemoveWarning()
        {
            yield return Spawn(new Vector3(0f, 1f, 10f));
            BeginCharge();
            sniper.Simulate(0.5f);
            sniper.Simulate(0f);
            Assert.That(sniper.ChargeProgress, Is.InRange(0.49f, 0.51f));
            sniper.GetComponent<DamageTarget>().TakeDamage(settings.initialHealth);
            sniper.Simulate(1f);
            Assert.That(sniper.ShotsFired, Is.Zero);
            Assert.That(sniper.chargeOrb.gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(sniper == null, Is.True);
        }

        private static void Wall(Vector3 position)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sniper test cover";
            wall.transform.position = ArenaOrigin + position;
            wall.transform.localScale = new Vector3(5f, 3f, 0.5f);
        }
    }
}
