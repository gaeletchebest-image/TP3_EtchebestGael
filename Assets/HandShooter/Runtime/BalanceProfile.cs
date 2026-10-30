using System;
using UnityEngine;

namespace HandShooter
{
    [Serializable]
    public sealed class WeaponBalance
    {
        [Min(0.01f)] public float tankCapacity = 100f;
        [Min(0.01f)] public float fuelPerDamage = 1f;
        [Min(0)] public int startingSpareTanks = 2;
        [Min(0.1f)] public float reloadDuration = 1.25f;

        public void Apply(HandWeapon weapon)
        {
            weapon.tankCapacity = Mathf.Max(0.01f, tankCapacity);
            weapon.fuelPerDamage = Mathf.Max(0.01f, fuelPerDamage);
            weapon.startingSpareTanks = Mathf.Max(0, startingSpareTanks);
            weapon.reloadDuration = Mathf.Max(0.1f, reloadDuration);
        }

        public static WeaponBalance Capture(HandWeapon weapon) => new WeaponBalance
        {
            tankCapacity = weapon.tankCapacity, fuelPerDamage = weapon.fuelPerDamage,
            startingSpareTanks = weapon.startingSpareTanks, reloadDuration = weapon.reloadDuration
        };
    }

    // Each saved profile owns its five settings as sub-assets. Visual/prefab references stay shared.
    public sealed class BalanceProfile : ScriptableObject
    {
        public string title = "Nuevo balance";
        [TextArea] public string notes;
        public ShooterSettings shooter;
        public TurretSettings turret;
        public KamikazeSettings kamikaze;
        public SniperSettings sniper;
        public HunterSettings hunter;
        public WeaponBalance weapon = new WeaponBalance();
        public bool IsComplete => shooter && turret && kamikaze && sniper && hunter && weapon != null;

        public string Snapshot() => JsonUtility.ToJson(new BalanceSnapshot
        {
            title = title, notes = notes, shooter = JsonUtility.ToJson(shooter),
            turret = JsonUtility.ToJson(turret), kamikaze = JsonUtility.ToJson(kamikaze),
            sniper = JsonUtility.ToJson(sniper), hunter = JsonUtility.ToJson(hunter), weapon = weapon
        });
    }

    [Serializable]
    public sealed class BalanceSnapshot
    {
        public string title, notes, shooter, turret, kamikaze, sniper, hunter;
        public WeaponBalance weapon;
    }
}
