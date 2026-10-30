using UnityEngine;

namespace HandShooter
{
    public readonly struct BalanceEstimate
    {
        public readonly float DirectDamage, SplashDamage, Radius, Interval, FuelPerShot;
        public readonly float DamagePerSecond, SustainedDps, FuelPerKill, TimeToKill, FlightTime, GroupDamage;
        public readonly int ShotsToKill, ShotsPerTank, Reloads;
        public readonly bool Reachable, EnoughFuel;

        public BalanceEstimate(ShooterSettings settings, WeaponBalance fuel, HandConfiguration hand,
            float health, float distance, int nearbyTargets, int defensePoints = 0, int defenseBarCapacity = 1)
        {
            ShotStats shot = settings.Evaluate(hand);
            ExplosionStats blast = shot.Hitscan ? new ExplosionStats(0f, 0f) : settings.EvaluateExplosion(shot.Damage);
            float multiplier = new EnemyDefense(defensePoints, defenseBarCapacity).DamageMultiplier;
            float rawSplash = blast.Radius > 0f ? blast.Damage : 0f;
            DirectDamage = shot.Damage * multiplier;
            SplashDamage = rawSplash * multiplier;
            Radius = blast.Radius;
            Interval = shot.Interval;
            float effectiveDamage = DirectDamage + SplashDamage;
            FuelPerShot = settings.EvaluateFuelCost(shot, fuel.fuelPerDamage);
            ShotsToKill = BalanceCalculator.ShotsToKill(health, shot.Damage + rawSplash, defensePoints, defenseBarCapacity);
            ShotsPerTank = FuelPerShot <= 0f ? int.MaxValue :
                Mathf.Max(0, Mathf.FloorToInt((Mathf.Max(0.01f, fuel.tankCapacity) + 0.00001f) / FuelPerShot));
            Reloads = ShotsPerTank == 0 ? 0 : (ShotsToKill - 1) / ShotsPerTank;
            FuelPerKill = ShotsToKill * FuelPerShot;
            FlightTime = shot.Hitscan ? 0f : Mathf.Max(0f, distance) / settings.EvaluateProjectileSpeed(shot.Damage);
            Reachable = distance <= settings.range && (shot.Hitscan || FlightTime <= settings.projectileLifetime);
            EnoughFuel = ShotsPerTank > 0 && Reloads <= Mathf.Max(0, fuel.startingSpareTanks);
            // First shot is ready. Cooldown continues during reload, so the two waits overlap.
            TimeToKill = Reachable && EnoughFuel ? (ShotsToKill - 1) * Interval + FlightTime +
                Reloads * Mathf.Max(0f, Mathf.Max(0.1f, fuel.reloadDuration) - Interval) : float.PositiveInfinity;
            DamagePerSecond = effectiveDamage / Interval;
            SustainedDps = ShotsPerTank == 0 ? 0f : FuelPerShot <= 0f ? DamagePerSecond :
                ShotsPerTank * effectiveDamage / ((ShotsPerTank - 1f) * Interval + Mathf.Max(Interval, Mathf.Max(0.1f, fuel.reloadDuration)));
            GroupDamage = DirectDamage + SplashDamage * Mathf.Max(1, nearbyTargets);
        }
    }

    public static class BalanceCalculator
    {
        public static BalanceEstimate Evaluate(BalanceProfile profile, int fingers, bool ray, float health,
            float distance, int nearbyTargets = 1, int defensePoints = 0, int defenseBarCapacity = 1) =>
            new BalanceEstimate(profile.shooter, profile.weapon, new HandConfiguration(fingers, ray),
                health, distance, nearbyTargets, defensePoints, defenseBarCapacity);

        public static int ShotsToKill(float health, float damage, int defensePoints, int defenseBarCapacity)
        {
            double remaining = Mathf.Max(0.01f, health);
            long shots = 0;
            int capacity = Mathf.Max(1, defenseBarCapacity);
            for (int points = Mathf.Max(0, defensePoints); points > 0; points--)
            {
                double reducedDamage = damage * System.Math.Pow(0.5, points);
                int hits = (int)System.Math.Min(capacity, System.Math.Ceiling(remaining / reducedDamage));
                shots += hits;
                remaining -= hits * reducedDamage;
                if (shots >= int.MaxValue) return int.MaxValue;
                if (remaining <= 0) return (int)shots;
            }
            return (int)System.Math.Min(int.MaxValue, shots + System.Math.Ceiling(remaining / damage));
        }
    }
}
