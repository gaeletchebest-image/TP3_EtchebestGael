using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HandShooter.Tests
{
    public sealed class GrenadeRulesTests
    {
        private ShooterSettings settings;
        [SetUp] public void SetUp() => settings = ScriptableObject.CreateInstance<ShooterSettings>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        [TestCase(1, 3f, 0.8f)] [TestCase(2, 6f, 1.6f)]
        [TestCase(3, 12f, 2.6f)] [TestCase(4, 24f, 4f)]
        public void PowerScalesDamageCostRadiusAndInterval(int power, float damage, float interval)
        {
            var hand = new HandConfiguration(power);
            ShotStats ray = settings.Evaluate(hand);
            hand.SetMode(WeaponMode.Projectile);
            ShotStats projectile = settings.Evaluate(hand);
            hand.SetMode(WeaponMode.Grenade);
            ShotStats grenade = settings.Evaluate(hand);
            var blast = settings.EvaluateGrenadeExplosion(grenade.Damage);
            Assert.That(grenade.Mode, Is.EqualTo(WeaponMode.Grenade));
            Assert.That(grenade.Damage, Is.EqualTo(damage));
            Assert.That(grenade.Interval, Is.EqualTo(interval).Within(0.0001f));
            Assert.That(blast.Damage, Is.EqualTo(damage));
            Assert.That(blast.Radius, Is.EqualTo(settings.EvaluateExplosion(projectile.Damage).Radius * 3f).Within(0.0001f));
            Assert.That(settings.EvaluateFuelCost(grenade, 1f), Is.EqualTo(settings.EvaluateFuelCost(ray, 1f) * 3f));
            Assert.That(settings.grenadeFuseDuration, Is.EqualTo(2f));
        }

        [Test] public void SelectorCyclesThreeModesAndDisabledModeFallsBackToRay()
        {
            var hand = new HandConfiguration();
            hand.CycleMode(true); Assert.That(hand.Mode, Is.EqualTo(WeaponMode.Projectile));
            hand.CycleMode(true); Assert.That(hand.Mode, Is.EqualTo(WeaponMode.Grenade));
            hand.CycleMode(true); Assert.That(hand.Mode, Is.EqualTo(WeaponMode.Ray));
            hand.CycleMode(false); hand.CycleMode(false);
            Assert.That(hand.Mode, Is.EqualTo(WeaponMode.Ray));
            hand.SetMode(WeaponMode.Grenade);
            settings.grenadeEnabled = false;
            Assert.That(settings.Evaluate(hand).Mode, Is.EqualTo(WeaponMode.Ray));
        }

        [Test] public void GrenadePresetSurvivesReloadAndLegacyPresetStillLoads()
        {
            string prefix = "HandShooter.Tests.Grenade." + Guid.NewGuid() + ".";
            try
            {
                var hand = new HandConfiguration(3);
                hand.SetMode(WeaponMode.Grenade);
                var presets = new HandPresetStore(prefix);
                presets.Save(2, hand);
                PlayerPrefs.SetInt(prefix + 3, 19); // Original ray encoding, mask 3.
                var loaded = new HandPresetStore(prefix);
                Assert.That(loaded.Get(2).Mode, Is.EqualTo(WeaponMode.Grenade));
                Assert.That(loaded.Get(2).Matches(hand), Is.True);
                Assert.That(loaded.Get(3).Mode, Is.EqualTo(WeaponMode.Ray));
                Assert.That(loaded.Get(3).ActiveMask, Is.EqualTo(3));
            }
            finally
            {
                PlayerPrefs.DeleteKey(prefix + 2); PlayerPrefs.DeleteKey(prefix + 3); PlayerPrefs.Save();
            }
        }
    }
}
