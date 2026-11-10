using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class TutorialPlayTests
    {
        [UnityTest]
        public IEnumerator WeaponPickupAndFirstDummyOpenTheirOwnDoors()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Tutorial");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.enabled = false;
            player.SetMenuOpen(false);
            var weaponDoor = Object.FindFirstObjectByType<WeaponUnlockDoor>();
            var firstPanel = weaponDoor.GetComponentInChildren<Rigidbody>();
            var exitPanel = GameObject.Find("SlidingDoor (1)").GetComponentInChildren<Rigidbody>();
            float closedFirstY = firstPanel.position.y;
            float closedExitY = exitPanel.position.y;
            Assert.That(player.weapon.IsEquipped, Is.False);

            var pedestal = GameObject.Find("Hand Weapon Pedestal");
            player.transform.position = pedestal.transform.position;
            Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!player.weapon.IsEquipped && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.weapon.IsEquipped, Is.True);
            float equippedAt = Time.time;
            Assert.That(firstPanel.position.y, Is.EqualTo(closedFirstY));
            while (firstPanel.position.y <= closedFirstY && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(firstPanel.position.y, Is.GreaterThan(closedFirstY));
            Assert.That(Time.time - equippedAt, Is.GreaterThanOrEqualTo(0.9f));
            Assert.That(exitPanel.position.y, Is.EqualTo(closedExitY));

            var target = GameObject.Find("Dummy").GetComponentInChildren<DamageTarget>();
            yield return new WaitForSeconds(0.5f);
            Vector3 center = target.GetComponent<Collider>().bounds.center;
            player.viewCamera.transform.SetPositionAndRotation(center + Vector3.left * 4f, Quaternion.LookRotation(Vector3.right));
            Physics.SyncTransforms();
            target.TakeDamage(target.Health / target.DamageMultiplier - player.weapon.Stats.Damage);
            Assert.That(player.weapon.TryFire(), Is.True);
            Assert.That(target.Health, Is.Zero);
            yield return new WaitForSeconds(target.settings.targetRespawnDelay + 0.1f);
            Assert.That(exitPanel.position.y, Is.GreaterThan(closedExitY));
            Assert.That(target.Health, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ThumbPickupRequiresTheHandAndUnlocksProjectilesAfterAttaching()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Tutorial");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.enabled = false;
            player.SetMenuOpen(false);
            var handPedestal = GameObject.Find("Hand Weapon Pedestal");
            var thumbPedestal = GameObject.Find("Thumb Pedestal");
            var display = thumbPedestal.transform.Find("Display Thumb");
            var weapon = player.weapon;
            Vector3 rest = display.position;
            Assert.That(handPedestal.transform.Find("Display Hand/Thumb").gameObject.activeSelf, Is.False);

            player.transform.position = thumbPedestal.transform.position;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.8f);
            Assert.That(weapon.Hand.ThumbUnlocked, Is.False);
            Assert.That(display.position, Is.EqualTo(rest));

            player.transform.position = handPedestal.transform.position;
            Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!weapon.IsEquipped && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(weapon.IsEquipped, Is.True);
            Assert.That(weapon.thumbVisual.gameObject.activeSelf, Is.False);
            Assert.That(weapon.Hand.TryApply(3, false), Is.True);
            Assert.That(weapon.Hand.ActiveMask, Is.EqualTo(3));
            Assert.That(weapon.Stats.Hitscan, Is.True);
            float initialFuel = weapon.Fuel;
            float shotCost = -1f;
            weapon.ShotFired += (_, cost) => shotCost = cost;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel));
            Assert.That(shotCost, Is.Zero);

            var menu = Object.FindFirstObjectByType<HandMenu>();
            player.SetMenuOpen(true);
            yield return null;
            var button = menu.MenuRoot.transform.Find("Hand Menu Panel/Thumb Button").GetComponent<Button>();
            Assert.That(button.interactable, Is.False);
            Assert.That(menu.tankLabel.text, Does.Contain("INFINITO"));
            player.SetMenuOpen(false);

            player.transform.position = thumbPedestal.transform.position;
            Physics.SyncTransforms();
            deadline = Time.realtimeSinceStartup + 4f;
            while (display.position.y < rest.y + 0.1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(display.position.y, Is.GreaterThanOrEqualTo(rest.y + 0.1f));
            Assert.That(weapon.Hand.ThumbUnlocked, Is.False);
            while (!weapon.Hand.ThumbUnlocked && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(weapon.Hand.ThumbUnlocked, Is.True);
            Assert.That(display.gameObject.activeSelf, Is.False);
            Assert.That(weapon.thumbVisual.gameObject.activeSelf, Is.True);
            Assert.That(weapon.Stats.Hitscan, Is.True);
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel));
            while (weapon.CooldownRemaining > 0f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(shotCost, Is.GreaterThan(0f));
            Assert.That(weapon.Fuel, Is.EqualTo(initialFuel - shotCost));

            player.SetMenuOpen(true);
            yield return null;
            Assert.That(button.interactable, Is.True);
            Assert.That(menu.tankLabel.text, Does.Not.Contain("INFINITO"));
            button.onClick.Invoke();
            Assert.That(weapon.Stats.Hitscan, Is.False);
            player.SetMenuOpen(false);
        }

        [UnityTest]
        public IEnumerator DummyWithExactShotCountRetriesAndAcceptsTheNextCorrectAttempt()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Tutorial");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.enabled = false;
            player.SetMenuOpen(false);
            var challenge = GameObject.Find("Dummy").GetComponentInChildren<DummyDoorChallenge>();
            typeof(DummyDoorChallenge).GetField("requiredShots", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(challenge, 2);
            var target = challenge.GetComponent<DamageTarget>();
            var hitCollider = target.GetComponent<Collider>();
            var exitPanel = GameObject.Find("SlidingDoor (1)").GetComponentInChildren<Rigidbody>();
            float closedY = exitPanel.position.y;
            float health = target.Health;
            int defense = target.DefensePoints;
            int defenseBar = target.DefenseBar;

            player.weapon.Damage(target, health / (2f * target.DamageMultiplier), 1);
            player.weapon.Damage(target, target.Health / target.DamageMultiplier, 1);
            Assert.That(target.Health, Is.Zero);
            Assert.That(hitCollider.enabled, Is.False);
            float defeatedAt = Time.time;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (target.Health == 0f && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(exitPanel.position.y, Is.EqualTo(closedY));
                yield return null;
            }
            Assert.That(Time.time - defeatedAt, Is.InRange(1f, 1.4f));
            Assert.That(target.Health, Is.EqualTo(health));
            Assert.That(target.DefensePoints, Is.EqualTo(defense));
            Assert.That(target.DefenseBar, Is.EqualTo(defenseBar));
            Assert.That(hitCollider.enabled, Is.True);

            player.weapon.Damage(target, health / (2f * target.DamageMultiplier), 2);
            player.weapon.Damage(target, target.Health / target.DamageMultiplier, 3);
            Assert.That(target.Health, Is.Zero);
            yield return new WaitForSeconds(1.1f);
            Assert.That(target.Health, Is.Zero);
            Assert.That(exitPanel.position.y, Is.GreaterThan(closedY));
        }

        [UnityTest]
        public IEnumerator DummyVitalsFollowDamageAndDisappearBehindCover()
        {
            yield return SceneManager.LoadSceneAsync("Tutorial");
            var target = GameObject.Find("Dummy").GetComponentInChildren<DamageTarget>();
            const BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DamageTarget).GetField("practiceDefensePoints", instanceFields).SetValue(target, 1);
            typeof(DamageTarget).GetField("practiceDefenseBarCapacity", instanceFields).SetValue(target, 2);
            target.ResetTarget();
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            player.enabled = false;
            target.transform.parent.position += Vector3.right * 500f;
            Physics.SyncTransforms();
            Vector3 center = target.GetComponent<Collider>().bounds.center;
            player.viewCamera.transform.SetPositionAndRotation(center - Vector3.forward * 5f, Quaternion.identity);
            yield return null;

            var indicators = System.Array.Find(target.gameObject.scene.GetRootGameObjects(), root =>
                root.name == $"{target.name} Indicators" && root.activeSelf && Mathf.Abs(root.transform.position.x - center.x) < 0.01f);
            Assert.That(indicators, Is.Not.Null);
            var healthFill = indicators.transform.Find("Health Bar/Health Fill").GetComponent<RectTransform>();
            var healthText = indicators.transform.Find("Health Points").GetComponent<Text>();
            var damageText = indicators.transform.Find("Last Damage").GetComponent<Text>();
            var defenseDisplay = indicators.GetComponentInChildren<EnemyDefenseDisplay>();
            Assert.That(defenseDisplay, Is.Not.Null);
            var defenseText = defenseDisplay.transform.Find("Defense Points").GetComponent<Text>();
            var defenseFill = defenseDisplay.transform.Find("Defense Bar/Defense Fill").GetComponent<RectTransform>();
            float fullDefenseWidth = defenseFill.rect.width;
            Assert.That(defenseText.text, Is.EqualTo("DEF 1  ·  2/2"));
            float fullWidth = healthFill.rect.width;
            float fullHealth = target.Health;
            Assert.That(target.TakePlayerShotDamage(fullHealth), Is.True);
            Assert.That(healthFill.rect.width, Is.EqualTo(fullWidth / 2f).Within(0.001f));
            Assert.That(healthText.text, Is.EqualTo($"{target.Health:0.#} / {fullHealth:0.#}"));
            Assert.That(damageText.text, Is.EqualTo($"-{target.LastDamage:0.#}"));
            Assert.That(defenseText.text, Is.EqualTo("DEF 1  ·  1/2"));
            Assert.That(defenseFill.rect.width, Is.EqualTo(fullDefenseWidth / 2f).Within(0.001f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = center - Vector3.forward * 2.5f;
            wall.transform.localScale = new Vector3(3f, 1f, 0.1f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(indicators.activeSelf, Is.False, "Cover should hide the entire display even if the bar is above it.");

            wall.SetActive(false);
            Object.Destroy(wall);
            yield return null;
            Assert.That(indicators.activeSelf, Is.True);
            Assert.That(target.TakePlayerShotDamage(1f), Is.True);
            Assert.That(defenseDisplay.gameObject.activeSelf, Is.False);
            Assert.That(target.TakePlayerShotDamage(fullHealth), Is.True);
            Assert.That(indicators.activeSelf, Is.False);
            target.ResetTarget();
            yield return null;
            Assert.That(indicators.activeSelf, Is.True);
            Assert.That(healthFill.rect.width, Is.EqualTo(fullWidth).Within(0.001f));
            Assert.That(defenseText.text, Is.EqualTo("DEF 1  ·  2/2"));
            Assert.That(damageText.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PlayerCanWalkFromTheCapsuleToTheNextLevel()
        {
            float previousMaximumDeltaTime = Time.maximumDeltaTime;
            Time.maximumDeltaTime = 0.05f;
            Time.timeScale = 1f;
            try
            {
                yield return SceneManager.LoadSceneAsync("Tutorial");
                var capsule = Object.FindFirstObjectByType<ArrivalCapsule>();
                var player = Object.FindFirstObjectByType<PrototypePlayer>();
                var pedestal = GameObject.Find("Hand Weapon Pedestal");
                var display = pedestal.transform.Find("Display Hand");
                var hud = Object.FindFirstObjectByType<HandMenu>();
                var dashTrigger = Object.FindFirstObjectByType<DashUnlockTrigger>();
                var rider = player.GetComponent<CharacterController>();
                player.enabled = false;
                yield return null;
                var hatch = capsule.transform.Find("Right hatch/Hatch panel").GetComponent<BoxCollider>();
                var carriage = GameObject.Find("Departure Lift").transform.Find("Carriage");
                var fade = GameObject.Find("Level Fade").GetComponentInChildren<Image>();
                float threshold = hatch.bounds.center.z;
                float closedX = hatch.bounds.center.x;
                Assert.That(player.transform.position.z, Is.LessThan(threshold));
                Assert.That(fade.color.a, Is.Zero);
                Assert.That(SceneManager.GetActiveScene().buildIndex, Is.Zero);
                Assert.That(player.weapon.IsEquipped, Is.False);
                Assert.That(player.weapon.TryFire(), Is.False);
                Assert.That(hud.fuelLabel.gameObject.activeSelf, Is.False);
                Assert.That(display.gameObject.activeSelf, Is.True);
                Assert.That(player.DashUnlocked, Is.False);
                Assert.That(hud.dashCooldownFill.transform.parent.gameObject.activeSelf, Is.False);

                for (int step = 0; step < 20; step++)
                    rider.Move(new Vector3(0f, -0.03f, 0.12f));
                Assert.That(player.transform.position.z, Is.LessThan(threshold), "The closed hatch should block the rider.");

                float pushedOut = threshold + 0.16f;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (hatch.bounds.center.z < pushedOut - 0.005f && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(hatch.bounds.center.x, Is.EqualTo(closedX).Within(0.005f));
                    yield return null;
                }
                Assert.That(hatch.bounds.center.z, Is.EqualTo(pushedOut).Within(0.005f));
                Assert.That(hatch.bounds.center.x, Is.EqualTo(closedX).Within(0.005f));

                float holdStartedAt = Time.time;
                while (hatch.bounds.center.x < closedX + 0.005f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Time.time - holdStartedAt, Is.InRange(0.43f, 0.68f));

                float clearance = rider.bounds.max.x + 0.1f;
                while (hatch.bounds.min.x < clearance && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(hatch.bounds.min.x, Is.GreaterThanOrEqualTo(clearance));
                Assert.That(hatch.bounds.center.z, Is.EqualTo(pushedOut).Within(0.005f));
                for (int step = 0; step < 10; step++)
                    rider.Move(new Vector3(0f, -0.03f, 0.12f));
                Assert.That(player.transform.position.z, Is.GreaterThan(threshold + 0.5f));

                float restHeight = display.position.y;
                for (int step = 0; step < 30 && player.transform.position.z < pedestal.transform.position.z - 0.65f; step++)
                    rider.Move(new Vector3(0f, -0.03f, 0.12f));
                deadline = Time.realtimeSinceStartup + 4f;
                while (display.position.y < restHeight + 0.1f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(display.position.y, Is.GreaterThanOrEqualTo(restHeight + 0.1f));
                Assert.That(player.weapon.IsEquipped, Is.False, "The hand should rise before it equips.");

                while (!player.weapon.IsEquipped && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.weapon.IsEquipped, Is.True);
                Assert.That(display.gameObject.activeSelf, Is.False);
                yield return null;
                Assert.That(hud.fuelLabel.gameObject.activeSelf, Is.True);
                Assert.That(player.weapon.TryFire(), Is.True);

                player.SendMessage("StartDash", Vector2.up);
                Assert.That(player.DashCooldownFraction, Is.EqualTo(1f));

                for (int step = 0; step < 40 && player.transform.position.z < dashTrigger.transform.position.z; step++)
                    rider.Move(new Vector3(0f, -0.03f, 0.14f));
                deadline = Time.realtimeSinceStartup + 4f;
                while (!player.DashUnlocked && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(player.DashUnlocked, Is.True);
                yield return null;
                Assert.That(hud.dashCooldownFill.transform.parent.gameObject.activeSelf, Is.True);
                player.SendMessage("StartDash", Vector2.up);
                Assert.That(player.DashCooldownFraction, Is.LessThan(1f));

                for (int step = 0; step < 100 && player.transform.position.z < carriage.position.z; step++)
                    rider.Move(new Vector3(0f, -0.03f, 0.14f));
                Assert.That(player.transform.position.z, Is.EqualTo(carriage.position.z).Within(0.15f));
                Assert.That(player.transform.position.y, Is.InRange(0f, 0.2f));

                deadline = Time.realtimeSinceStartup + 8f;
                while (fade && fade.color.a < 1f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(fade.color.a, Is.EqualTo(1f));
                Assert.That(carriage.localPosition.y, Is.LessThan(0f));

                deadline = Time.realtimeSinceStartup + 5f;
                while (SceneManager.GetActiveScene().name == "Tutorial" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("LevelStarter"));
                var nextPlayer = Object.FindFirstObjectByType<PrototypePlayer>();
                Assert.That(nextPlayer.DashUnlocked, Is.True);
                Assert.That(nextPlayer.weapon.IsEquipped, Is.True);
            }
            finally
            {
                Time.maximumDeltaTime = previousMaximumDeltaTime;
            }
        }
    }
}
