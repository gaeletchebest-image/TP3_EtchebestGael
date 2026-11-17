#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class EnemyDestructionPlayTests
    {
        private readonly List<GameObject> testObjects = new List<GameObject>();
        private static readonly Vector3 Origin = new Vector3(300f, 10f, 300f);

        [TearDown] public void TearDown()
        {
            foreach (var testObject in testObjects)
                if (testObject) Object.Destroy(testObject);
            testObjects.Clear();
        }

        [UnityTest] public IEnumerator DefeatedEnemiesEmitTheirOwnPaletteAndTheEffectExpires()
        {
            string[] enemies = { "EnemyTurret", "EnemyKamikaze", "EnemySniper", "EnemyHunter" };
            var effects = new List<ParticleSystem>();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = Spawn(enemies[i], Origin + Vector3.right * (i * 10f));
                var renderer = enemy.GetComponent<Renderer>();
                Color tint = renderer.sharedMaterial.GetColor("_BaseColor");
                Vector3 center = renderer.bounds.center;
                var flash = new MaterialPropertyBlock();
                flash.SetColor("_BaseColor", Color.white);
                renderer.SetPropertyBlock(flash);
                Assert.That(enemy.TakePlayerShotDamage(10000f), Is.True);
                Assert.That(enemy.TakePlayerShotDamage(10000f), Is.False);
                Assert.That(enemy.DestroyedCount, Is.EqualTo(1));
                Assert.That(renderer.enabled, Is.False);

                var effect = EffectAt(center);
                effects.Add(effect);
                var particles = new ParticleSystem.Particle[effect.particleCount];
                Assert.That(effect.GetParticles(particles), Is.GreaterThan(50), enemies[i]);
                Assert.That(System.Array.Exists(particles, particle => SimilarTint(particle.startColor, tint)),
                    Is.True, enemies[i]);
                Assert.That(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));

                if (i == 0)
                {
                    // A rotated, nonuniformly scaled turret still emits from the faces of its mesh.
                    int turretFaceParticles = System.Array.FindAll(particles, particle =>
                        SimilarTint(particle.startColor, tint) && OnUnitCube(enemy.transform.InverseTransformPoint(particle.position))).Length;
                    Assert.That(turretFaceParticles, Is.GreaterThan(50));
                    Color baseTint = enemy.transform.Find("Target base").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
                    Assert.That(System.Array.Exists(particles, particle => SimilarTint(particle.startColor, baseTint) &&
                        particle.position.y < center.y - 0.6f), Is.True);
                }
            }

            var firstParticles = new ParticleSystem.Particle[effects[0].particleCount];
            effects[0].GetParticles(firstParticles);
            Assert.That(effects[0].isPaused, Is.True);
            yield return new WaitForSeconds(0.1f);
            Assert.That(effects[0].isPlaying, Is.True);
            yield return null;
            var movingParticles = new ParticleSystem.Particle[effects[0].particleCount];
            Assert.That(effects[0].GetParticles(movingParticles), Is.GreaterThan(0));
            Assert.That((movingParticles[0].position - firstParticles[0].position).sqrMagnitude, Is.GreaterThan(0.001f));

            yield return new WaitForSeconds(1f);
            foreach (var effect in effects) Assert.That(effect == null, Is.True);
        }

        [UnityTest] public IEnumerator KamikazeDetonationSpawnsOneBurstAndKeepsItsBlast()
        {
            var enemy = Spawn("EnemyKamikaze", Origin);
            var kamikaze = enemy.GetComponent<KamikazeEnemy>();
            kamikaze.Detonate();
            kamikaze.Detonate();

            var effect = EffectAt(Origin);
            Assert.That(effect.particleCount, Is.GreaterThan(50));
            var blast = System.Array.Find(Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None),
                candidate => candidate.transform.position == Origin);
            Assert.That(blast, Is.Not.Null);
            testObjects.Add(blast.gameObject);
            yield return null;
            Assert.That(enemy == null, Is.True);
            Assert.That(effect != null, Is.True);
        }

        private DamageTarget Spawn(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/HandShooter/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab, position, Quaternion.Euler(0f, 37f, 0f));
            testObjects.Add(instance);
            return instance.GetComponent<DamageTarget>();
        }

        private ParticleSystem EffectAt(Vector3 position)
        {
            var effects = System.Array.FindAll(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None),
                effect => effect.gameObject.name == "Enemy destruction" && effect.transform.position == position);
            foreach (var effect in effects) testObjects.Add(effect.gameObject);
            Assert.That(effects.Length, Is.EqualTo(1));
            return effects[0];
        }

        private static bool OnUnitCube(Vector3 point) => Mathf.Abs(
            Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y), Mathf.Abs(point.z)) - 0.5f) < 0.001f;

        private static bool SimilarTint(Color actual, Color expected) => Mathf.Max(
            Mathf.Abs(actual.r - expected.r), Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b)) < 0.19f;
    }
}
#endif
