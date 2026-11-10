#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class PulseBarEmitterPlayTests
    {
        [UnityTest]
        public IEnumerator ConfiguredBarResizesTheEmitterStopsAtAWallAndKillsThePlayer()
        {
            const float speed = 22f;
            const float interval = 0.20f;
            const float offset = 0.15f;
            var size = new Vector3(3.4f, 0.28f, 0.14f);
            var scene = SceneManager.CreateScene("Pulse bar physics test");
            float previousMaxDelta = Time.maximumDeltaTime;
            Time.maximumDeltaTime = 0.05f;
            Time.timeScale = 1f;
            try
            {
                var stage = new GameObject("Test stage");
                stage.SetActive(false);
                stage.transform.position = new Vector3(400f, 0f, 400f);
                SceneManager.MoveGameObjectToScene(stage, scene);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/HandShooter/Prefabs/PulseBarEmitter.prefab");
                var emitter = Object.Instantiate(prefab, stage.transform).GetComponent<PulseBarEmitter>();
                emitter.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var settings = new SerializedObject(emitter);
                settings.FindProperty("fireInterval").floatValue = interval;
                settings.FindProperty("fireOffset").floatValue = offset;
                settings.FindProperty("speed").floatValue = speed;
                settings.FindProperty("barWidth").floatValue = size.x;
                settings.FindProperty("barHeight").floatValue = size.y;
                settings.FindProperty("barDepth").floatValue = size.z;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var housing = emitter.GetComponent<BoxCollider>();
                if (!housing) housing = emitter.gameObject.AddComponent<BoxCollider>();
                housing.center = new Vector3(0f, 0.90f, 0.1f);
                housing.size = new Vector3(4f, 1.5f, 1.4f);

                Vector3 direction = emitter.transform.forward;
                var wall = new GameObject("Thin wall", typeof(BoxCollider));
                wall.transform.SetParent(stage.transform, false);
                wall.transform.SetPositionAndRotation(stage.transform.position + direction * 8f + Vector3.up,
                    emitter.transform.rotation);
                wall.GetComponent<BoxCollider>().size = new Vector3(5f, 3f, 0.03f);

                var playerObject = new GameObject("Test player", typeof(CharacterController));
                playerObject.transform.SetParent(stage.transform, false);
                playerObject.transform.position = stage.transform.position - direction * 3f;
                playerObject.layer = 2;
                var controller = playerObject.GetComponent<CharacterController>();
                controller.height = 1.8f;
                controller.center = Vector3.up * 0.9f;
                var player = playerObject.AddComponent<PrototypePlayer>();
                player.enabled = false;
                player.spawnDefaultFuelPickups = false;
                player.deathScreen = new GameObject("Death screen");
                player.deathScreen.transform.SetParent(stage.transform, false);
                player.deathScreen.SetActive(false);

                float enabledAt = Time.time;
                stage.SetActive(true);
                var upper = emitter.transform.Find("Emitter head/Upper housing");
                Assert.That(upper.localScale.x, Is.GreaterThan(size.x));
                float deadline = Time.realtimeSinceStartup + 5f;
                GameObject bar;
                while (!(bar = FindBar(scene)) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(bar, Is.Not.Null);
                Assert.That(Vector3.Distance(bar.transform.localScale, size), Is.LessThan(0.001f));
                Assert.That(Vector3.Dot(bar.transform.forward, direction), Is.GreaterThan(0.999f));
                Assert.That(bar.transform.right.y, Is.Zero.Within(0.001f));
                var turret = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandShooter/Prefabs/EnemyTurret.prefab");
                var turretWeapon = turret.GetComponent<TurretEnemy>();
                var renderer = bar.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial, Is.SameAs(turretWeapon.projectileMaterial));
                Assert.That(renderer.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(turretWeapon.settings.projectileColor));
                var handProjectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandShooter/Prefabs/HandProjectile.prefab");
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(handProjectile.GetComponent<Renderer>().sharedMaterial));

                Vector3 start = bar.transform.position;
                float startedAt = Time.time;
                Assert.That(startedAt - enabledAt, Is.InRange(interval + offset - 0.002f, interval + offset + 0.11f));
                yield return new WaitForSeconds(0.12f);
                Assert.That(bar, Is.Not.Null);
                Assert.That(Vector3.Dot(bar.transform.position - start, direction),
                    Is.EqualTo(speed * (Time.time - startedAt)).Within(1.15f));

                GameObject second;
                while (!(second = FindBar(scene, bar)) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(second, Is.Not.Null);
                Assert.That(bar != null, Is.True, "The next shot should fire while the first is still travelling.");
                Assert.That(Time.time - startedAt, Is.EqualTo(interval).Within(0.06f));
                while (bar && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(bar == null, Is.True, "A fast bar should stop at the thin wall.");

                player.transform.position = stage.transform.position + direction * 3f;
                deadline = Time.realtimeSinceStartup + 5f;
                while (!player.IsDead && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.IsDead, Is.True);
                Assert.That(player.deathScreen.activeSelf, Is.True);
            }
            finally
            {
                Time.maximumDeltaTime = previousMaxDelta;
                Time.timeScale = 1f;
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static GameObject FindBar(Scene scene, GameObject except = null)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root != except && root.name == "Red Pulse Bar") return root;
            return null;
        }
    }
}
#endif
