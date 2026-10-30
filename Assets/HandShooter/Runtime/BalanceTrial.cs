using System;
using System.Collections.Generic;

namespace HandShooter
{
    public enum BalanceScenario { Blanco, Hunter, Sniper, Kamikazes, Torreta, GrupoMixto }

    [Serializable]
    public sealed class BalanceTrialResult
    {
        public string id, utc, profile, scenario, setup, balanceSnapshot, finalBalanceSnapshot, outcome, deathCause;
        public float seconds, fuelSpent, effectiveDamage;
        public int shots, hittingShots, kills, reloads;
        public bool settingsChanged;
        public float Accuracy => shots == 0 ? 0f : (float)hittingShots / shots;
    }

    // Tracks unique shot IDs: one explosion hitting three enemies is still one accurate shot.
    public sealed class BalanceTrial
    {
        public BalanceTrialResult Result { get; }
        private readonly HashSet<int> fired = new HashSet<int>();
        private readonly HashSet<int> hits = new HashSet<int>();
        public bool Finished { get; private set; }

        public BalanceTrial(BalanceTrialResult result) { Result = result; }
        public void Shot(int id, float fuel)
        {
            if (Finished || !fired.Add(id)) return;
            Result.shots++;
            Result.fuelSpent += fuel;
        }
        public void Damage(int id, float effective, bool killed)
        {
            if (Finished || !fired.Contains(id)) return;
            if (hits.Add(id)) Result.hittingShots++;
            Result.effectiveDamage += effective;
            if (killed) Result.kills++;
        }
        public void Reload() { if (!Finished) Result.reloads++; }
        public bool Finish(string outcome, float seconds, string cause = "")
        {
            if (Finished) return false;
            Finished = true;
            Result.outcome = outcome;
            Result.seconds = seconds;
            Result.deathCause = cause;
            return true;
        }
    }
}
