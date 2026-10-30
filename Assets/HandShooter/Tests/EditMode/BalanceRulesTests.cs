using NUnit.Framework;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class BalanceRulesTests
    {
        private ShooterSettings settings;
        private WeaponBalance fuel;
        [SetUp] public void SetUp() { settings = ScriptableObject.CreateInstance<ShooterSettings>(); fuel = new WeaponBalance(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(settings); }
        private BalanceEstimate Estimate(int fingers, bool ray, float health, float distance = 0f, int group = 1) =>
            new BalanceEstimate(settings, fuel, new HandConfiguration(fingers, ray), health, distance, group);

        [Test] public void FirstShotIsImmediateAndOverkillCostsAWholeShot()
        {
            var one = Estimate(4, true, 7f);
            Assert.That(one.TimeToKill, Is.Zero);
            Assert.That(one.ShotsToKill, Is.EqualTo(1));
            var breakpoint = Estimate(4, true, 9f);
            Assert.That(breakpoint.TimeToKill, Is.EqualTo(1f));
            Assert.That(breakpoint.FuelPerKill, Is.EqualTo(16f));
        }
        [Test] public void ProjectileIncludesTravelSplashAndDiscountButDoesNotMultiplySingleTargetDpsByGroup()
        {
            var estimate = Estimate(4, false, 24f, 6f, 3);
            Assert.That(estimate.DirectDamage, Is.EqualTo(16f));
            Assert.That(estimate.SplashDamage, Is.EqualTo(8f));
            Assert.That(estimate.ShotsToKill, Is.EqualTo(1));
            Assert.That(estimate.FuelPerShot, Is.EqualTo(4f));
            Assert.That(estimate.TimeToKill, Is.EqualTo(2f).Within(0.001f));
            Assert.That(estimate.DamagePerSecond, Is.EqualTo(12f));
            Assert.That(estimate.GroupDamage, Is.EqualTo(40f));
        }
        [Test] public void ReloadWaitOverlapsShotCooldownAndReservesLimitFeasibility()
        {
            fuel.tankCapacity = 2f; fuel.reloadDuration = 1.25f; fuel.startingSpareTanks = 1;
            var estimate = Estimate(1, true, 3f);
            Assert.That(estimate.Reloads, Is.EqualTo(1));
            Assert.That(estimate.TimeToKill, Is.EqualTo(1.45f).Within(0.001f));
            fuel.startingSpareTanks = 0;
            Assert.That(Estimate(1, true, 3f).TimeToKill, Is.EqualTo(float.PositiveInfinity));
        }
        [Test] public void SlowReloadDoesNotDoubleCountCooldown()
        {
            fuel.tankCapacity = 16f; fuel.reloadDuration = 0.5f;
            var estimate = Estimate(4, false, 120f);
            Assert.That(estimate.Reloads, Is.EqualTo(1));
            Assert.That(estimate.TimeToKill, Is.EqualTo(8f).Within(0.001f));
        }
        [Test] public void FuelFreeProjectilesHaveUnlimitedShotsWithoutReloads()
        {
            settings.projectileFuelEfficiency = 1f;
            var estimate = Estimate(1, false, 3000f);
            Assert.That(estimate.ShotsPerTank, Is.EqualTo(int.MaxValue));
            Assert.That(estimate.Reloads, Is.Zero);
            Assert.That(estimate.SustainedDps, Is.EqualTo(estimate.DamagePerSecond));
        }
        [Test] public void ProjectileLifetimeAndTankCapacityCanMakeAKillImpossible()
        {
            settings.projectileLifetime = 0.1f;
            Assert.That(Estimate(4, false, 24f, 10f).Reachable, Is.False);
            Assert.That(Estimate(4, false, 24f, 10f).TimeToKill, Is.EqualTo(float.PositiveInfinity));
            fuel.tankCapacity = 0.5f;
            Assert.That(Estimate(1, true, 1f).EnoughFuel, Is.False);
        }
        [Test] public void ZeroRadiusDoesNotCountSplashDamage()
        {
            settings.explosionMaxRadius = 0f;
            var estimate = Estimate(4, false, 24f);
            Assert.That(estimate.SplashDamage, Is.Zero);
            Assert.That(estimate.ShotsToKill, Is.EqualTo(2));
        }
        [Test] public void ExplosionAccuracyCountsShotsRatherThanVictimsAndFinishIsIdempotent()
        {
            var trial = new BalanceTrial(new BalanceTrialResult());
            trial.Shot(1, 4f); trial.Shot(2, 4f);
            trial.Damage(1, 8f, true); trial.Damage(1, 5f, true); trial.Damage(1, 3f, false);
            trial.Damage(-1, 100f, true);
            Assert.That(trial.Result.Accuracy, Is.EqualTo(0.5f));
            Assert.That(trial.Result.kills, Is.EqualTo(2));
            Assert.That(trial.Result.effectiveDamage, Is.EqualTo(16f));
            Assert.That(trial.Result.fuelSpent, Is.EqualTo(8f));
            Assert.That(trial.Finish("Muerte", 2f, "Sniper"), Is.True);
            Assert.That(trial.Finish("Superado", 3f), Is.False);
            trial.Shot(3, 4f); trial.Damage(2, 10f, true);
            Assert.That(trial.Result.shots, Is.EqualTo(2));
            Assert.That(trial.Result.deathCause, Is.EqualTo("Sniper"));
        }
    }
}
