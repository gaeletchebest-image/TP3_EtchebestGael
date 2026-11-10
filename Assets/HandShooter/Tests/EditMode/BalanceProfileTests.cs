using System.Linq;
using HandShooter.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class BalanceProfileTests
    {
        private BalanceProfile source, saved, destination;
        private string path;

        [SetUp] public void Setup()
        {
            source = ScriptableObject.CreateInstance<BalanceProfile>();
            source.shooter = ScriptableObject.CreateInstance<ShooterSettings>();
            source.turret = ScriptableObject.CreateInstance<TurretSettings>();
            source.kamikaze = ScriptableObject.CreateInstance<KamikazeSettings>();
            source.sniper = ScriptableObject.CreateInstance<SniperSettings>();
            source.hunter = ScriptableObject.CreateInstance<HunterSettings>();
            source.refuelingStation = ScriptableObject.CreateInstance<RefuelingStationSettings>();
            path = AssetDatabase.GenerateUniqueAssetPath("Assets/HandShooter/Tests/BalanceTest.asset");
        }

        [TearDown] public void Cleanup()
        {
            if (saved) AssetDatabase.DeleteAsset(path);
            if (destination)
            {
                Object.DestroyImmediate(destination.shooter); Object.DestroyImmediate(destination.turret);
                Object.DestroyImmediate(destination.kamikaze); Object.DestroyImmediate(destination.sniper);
                Object.DestroyImmediate(destination.hunter); Object.DestroyImmediate(destination.refuelingStation);
                Object.DestroyImmediate(destination);
            }
            Object.DestroyImmediate(source.shooter); Object.DestroyImmediate(source.turret);
            Object.DestroyImmediate(source.kamikaze); Object.DestroyImmediate(source.sniper);
            Object.DestroyImmediate(source.hunter); Object.DestroyImmediate(source.refuelingStation);
            Object.DestroyImmediate(source);
        }

        [Test] public void SavedProfilesOwnIndependentSettingsAndReloadIntoDraftWithoutSharingArrays()
        {
            source.shooter.baseDamage = 3f; source.weapon.tankCapacity = 42f;
            saved = BalanceLabAssets.CreateCopy(source, path);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Length, Is.EqualTo(7));
            saved.refuelingStation.fillDuration = 3.5f;
            Assert.That(source.refuelingStation.fillDuration, Is.EqualTo(2f));
            saved.shooter.cooldownByInactiveFingers[0] = 5f;
            saved.weapon.tankCapacity = 200f;
            Assert.That(source.shooter.cooldownByInactiveFingers[0], Is.EqualTo(1f));
            Assert.That(source.weapon.tankCapacity, Is.EqualTo(42f));
            destination = Object.Instantiate(source);
            destination.shooter = Object.Instantiate(source.shooter); destination.turret = Object.Instantiate(source.turret);
            destination.kamikaze = Object.Instantiate(source.kamikaze); destination.sniper = Object.Instantiate(source.sniper);
            destination.hunter = Object.Instantiate(source.hunter);
            destination.refuelingStation = Object.Instantiate(source.refuelingStation);
            BalanceLabAssets.LoadInto(saved, destination);
            destination.shooter.cooldownByInactiveFingers[0] = 7f;
            Assert.That(saved.shooter.cooldownByInactiveFingers[0], Is.EqualTo(5f));
            Assert.That(destination.weapon.tankCapacity, Is.EqualTo(200f));
            Assert.That(destination.refuelingStation.fillDuration, Is.EqualTo(3.5f));
            Assert.That(BalanceLabWindow.Differences(saved, destination).Any(s => s.Contains("cooldownByInactiveFingers")), Is.True);
        }
    }
}
