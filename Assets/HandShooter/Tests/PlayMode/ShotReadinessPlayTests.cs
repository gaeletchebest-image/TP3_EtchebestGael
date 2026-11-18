using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class ShotReadinessPlayTests
    {
#if UNITY_EDITOR
        [Test] public void PlayerPrefabContainsTheWiredFlatDisplay()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HandShooter/Prefabs/Player.prefab");
            var weapon = prefab.GetComponentInChildren<HandWeapon>(true);
            var display = weapon.GetComponentInChildren<HandChargeDisplay>(true);
            Assert.That(display, Is.Not.Null);
            var weaponFields = new UnityEditor.SerializedObject(weapon);
            Assert.That(weaponFields.FindProperty("chargeDisplay").objectReferenceValue, Is.EqualTo(display));
            var displayFields = new UnityEditor.SerializedObject(display);
            Assert.That(displayFields.FindProperty("fill").objectReferenceValue, Is.Not.Null);
            Assert.That(displayFields.FindProperty("track").objectReferenceValue, Is.Not.Null);
            Assert.That(Quaternion.Angle(display.transform.localRotation, weapon.transform.Find("Palm").localRotation),
                Is.LessThan(0.01f));
        }
#endif

        [UnityTest] public IEnumerator TheHandBarTracksCooldownReloadAndInsufficientFuel()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            var weapon = player.weapon;
            var menu = Object.FindFirstObjectByType<HandMenu>();
            var originalSettings = weapon.settings;
            var settings = Object.Instantiate(originalSettings);
            float originalFuelPerDamage = weapon.fuelPerDamage;
            float originalReloadDuration = weapon.reloadDuration;
            try
            {
                player.SetMenuOpen(false);
                weapon.settings = settings;
                settings.cooldownByInactiveFingers = new[] { 1f, 0.8f, 0.6f, 0.4f };
                weapon.Hand.TryApply(15, true);
                weapon.reloadDuration = 0.85f;
                yield return null;

                var chargeDisplay = weapon.GetComponentInChildren<HandChargeDisplay>();
                Assert.That(chargeDisplay, Is.Not.Null);
                Transform display = chargeDisplay.transform;
                var track = display.Find("Track").GetComponent<MeshRenderer>();
                var fill = display.Find("Fill").GetComponent<MeshRenderer>();
                float full = track.transform.localScale.x;
                Assert.That(fill.gameObject.layer, Is.EqualTo(weapon.transform.Find("Palm").gameObject.layer));
                Assert.That(menu.crosshair.transform.Find("Shot Cooldown"), Is.Null);
                Assert.That(menu.crosshair.transform.Find("Shot Status"), Is.Null);

                yield return WaitFor(() => IsGreen(Tint(fill)) && fill.enabled, "show the weapon is ready");
                Assert.That(fill.transform.localScale.x, Is.EqualTo(full).Within(0.001f));
                float originalInterval = weapon.Stats.Interval;
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(weapon.Hand.TryApply(1, true), Is.True);
                Assert.That(weapon.CooldownRemaining, Is.EqualTo(originalInterval));
                yield return WaitFor(() => fill.transform.localScale.x < full && IsRedOrAmber(Tint(fill)),
                    "reset the bar for the shot cooldown");
                Assert.That(weapon.TryFire(), Is.False);
                yield return WaitFor(() => weapon.CooldownRemaining <= 0f && IsGreen(Tint(fill)), "show readiness after the cooldown");

                // The tank can have fuel left while the current shot costs more than is available.
                Assert.That(weapon.Fuel, Is.GreaterThan(0f));
                weapon.fuelPerDamage = weapon.tankCapacity * 2f;
                yield return WaitFor(() => !fill.enabled && IsRedOrAmber(Tint(track)), "show the fuel block as an empty red bar");
                Assert.That(weapon.TryFire(), Is.False);

                weapon.AddFuelTank();
                weapon.SendMessage("BeginReloadFuel");
                Assert.That(weapon.IsReloading, Is.True);
                yield return WaitFor(() => fill.enabled && fill.transform.localScale.x > 0f &&
                    fill.transform.localScale.x < full && IsBlue(Tint(fill)), "fill the blue bar during reload");
                weapon.fuelPerDamage = originalFuelPerDamage;
                yield return WaitFor(() => !weapon.IsReloading && fill.enabled && IsGreen(Tint(fill)), "return to the ready state after reloading");
                Assert.That(fill.transform.localScale.x, Is.EqualTo(full).Within(0.001f));
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(weapon.CooldownRemaining, Is.EqualTo(weapon.Stats.Interval));
                Assert.That(menu.crosshair.text, Is.EqualTo("+"));
            }
            finally
            {
                if (weapon)
                {
                    weapon.settings = originalSettings;
                    weapon.fuelPerDamage = originalFuelPerDamage;
                    weapon.reloadDuration = originalReloadDuration;
                }
                Object.Destroy(settings);
                Time.timeScale = 1f;
            }
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();

        private Color Tint(Renderer renderer)
        {
            renderer.GetPropertyBlock(tint);
            return tint.GetColor(BaseColor);
        }

        private static bool IsGreen(Color color) => color.g > color.r && color.g > color.b;
        private static bool IsRedOrAmber(Color color) => color.r > color.g && color.r > color.b;
        private static bool IsBlue(Color color) => color.b > color.r && color.b > color.g;

        private static IEnumerator WaitFor(System.Func<bool> condition, string expected)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"Expected to {expected}.");
        }
    }
}
