using NUnit.Framework;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class ExplosionRulesTests
    {
        private ShooterSettings settings;
        [SetUp] public void SetUp() { settings = ScriptableObject.CreateInstance<ShooterSettings>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(settings); }
        [TestCase(1, 0.5f, 0.8838835f)]
        [TestCase(2, 1f, 1.25f)]
        [TestCase(3, 2f, 1.767767f)]
        [TestCase(4, 4f, 2.5f)]
        public void ConcentrationIncreasesExplosionDamageAndRadius(int fingers, float damage, float radius)
        {
            var shot = settings.Evaluate(new HandConfiguration(fingers, false));
            var explosion = settings.EvaluateExplosion(shot.Damage);
            Assert.That(explosion.Damage, Is.EqualTo(damage).Within(0.0001f));
            Assert.That(explosion.Radius, Is.EqualTo(radius).Within(0.0001f));
        }
        [Test] public void FractionAndRadiusAreConfigurableAndCapped()
        {
            settings.explosionDamageFraction = 0.25f;
            settings.explosionBaseRadius = 2;
            settings.explosionReferenceDamage = 4;
            settings.explosionRadiusExponent = 1;
            settings.explosionMaxRadius = 3;
            var explosion = settings.EvaluateExplosion(16);
            Assert.That(explosion.Damage, Is.EqualTo(4));
            Assert.That(explosion.Radius, Is.EqualTo(3));
        }
    }
}
