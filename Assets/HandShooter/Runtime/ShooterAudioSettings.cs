using UnityEngine;

namespace HandShooter
{
    public enum AudioBus { Master, Effects, Ambience, Music }

    public enum SoundCue
    {
        StepSolid, StepMetal, Land, Dash, DashReady,
        RayFire, ProjectileFire, GrenadeThrow, WallHit, TargetHit, ShieldHit, ShieldBreak,
        ProjectileExplosion, GrenadeLand, GrenadeFuse, GrenadeExplosion,
        Empty, FuelLow, ReloadStart, ReloadLoop, ReloadEnd, TankPickup,
        RefuelStart, RefuelLoop, RefuelCancel, RefuelEnd, StationHum,
        FingerToggle, ModeChange, Invalid, PresetSave, PresetEquip,
        HandEquip, ThumbEquip, Unlock,
        RobotMove, EnemyAlert, HunterFire, SniperCharge, SniperLock, SniperCancel, SniperFire,
        KamikazeChase, KamikazeExplosion, TurretFire, ProjectileFlight, EnemyImpact, EnemyDestroy,
        PulseEmit, PulseFlight, PulseImpact,
        DoorOpen, DoorClose, DoorStop, DoorUnlock, DoorLock,
        LiftStart, LiftMotor, LiftStop,
        CombatStart, CombatClear, ChallengeSuccess, ChallengeFail, TargetRespawn, TimerTick, Timeout, LevelExit,
        MenuOpen, MenuClose, UiSelect, UiConfirm, UiCancel, Save, Load, Delete, Error,
        Inspect, Pin, Death, Restart, IndustrialAmbience, MusicAmbient, MusicCombat
    }

    [CreateAssetMenu(menuName = "Hand Shooter/Audio Settings")]
    public sealed class ShooterAudioSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float master = 0.8f;
        [Range(0f, 1f)] public float effects = 0.8f;
        [Range(0f, 1f)] public float ambience = 0.35f;
        [Range(0f, 1f)] public float music = 0.25f;
        [Min(5f)] public float audibleDistance = 42f;
    }
}
