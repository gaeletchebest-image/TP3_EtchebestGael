using NUnit.Framework;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class DefenseRulesTests
    {
        [TestCase(0, 1f)]
        [TestCase(1, 0.5f)]
        [TestCase(2, 0.25f)]
        [TestCase(3, 0.125f)]
        public void EveryPointHalvesIncomingDamage(int points, float multiplier)
        {
            var defense = new EnemyDefense(points, 3);
            Assert.That(defense.DamageMultiplier, Is.EqualTo(multiplier));
        }

        [Test] public void BarsRefillBetweenPointsAndStayEmptyAfterLastPoint()
        {
            var defense = new EnemyDefense(2, 2);
            defense.RegisterPlayerHit();
            Assert.That(defense.Points, Is.EqualTo(2));
            Assert.That(defense.Bar, Is.EqualTo(1));
            defense.RegisterPlayerHit();
            Assert.That(defense.Points, Is.EqualTo(1));
            Assert.That(defense.Bar, Is.EqualTo(2));
            defense.RegisterPlayerHit();
            defense.RegisterPlayerHit();
            defense.RegisterPlayerHit();
            Assert.That(defense.Points, Is.Zero);
            Assert.That(defense.Bar, Is.Zero);
            Assert.That(defense.DamageMultiplier, Is.EqualTo(1f));
        }

        [Test] public void InvalidInitialSettingsCannotCreateNegativePointsOrAnUnbreakableBar()
        {
            var absent = new EnemyDefense(-2, 0);
            Assert.That(absent.Points, Is.Zero);
            Assert.That(absent.Bar, Is.Zero);
            var defense = new EnemyDefense(1, -5);
            Assert.That(defense.Capacity, Is.EqualTo(1));
            defense.RegisterPlayerHit();
            Assert.That(defense.Points, Is.Zero);
        }

        [TestCase(4f, 1, 3, 8)]
        [TestCase(4f, 2, 2, 9)]
        [TestCase(80f, 3, 3, 3)]
        [TestCase(100f, 1, 3, 1)]
        public void KillEstimateAccountsForBreakingDefenseAndDyingBeforeItBreaks(float damage, int points, int capacity, int shots)
        {
            Assert.That(BalanceCalculator.ShotsToKill(24f, damage, points, capacity), Is.EqualTo(shots));
        }

        [Test] public void CalculatorIncludesDefenseInDamageFuelAndTimeWithoutChangingShotCost()
        {
            var settings = ScriptableObject.CreateInstance<ShooterSettings>();
            try
            {
                var fuel = new WeaponBalance();
                var estimate = new BalanceEstimate(settings, fuel, new HandConfiguration(3, true), 24f, 0f, 1, 1, 3);
                Assert.That(estimate.DirectDamage, Is.EqualTo(2f));
                Assert.That(estimate.ShotsToKill, Is.EqualTo(8));
                Assert.That(estimate.FuelPerShot, Is.EqualTo(4f));
                Assert.That(estimate.FuelPerKill, Is.EqualTo(32f));
                Assert.That(estimate.TimeToKill, Is.EqualTo(7 * settings.Evaluate(new HandConfiguration(3, true)).Interval));
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
