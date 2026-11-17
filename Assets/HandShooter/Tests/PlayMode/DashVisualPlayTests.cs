using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HandShooter.Tests
{
    public sealed class DashVisualPlayTests
    {
        [UnityTest] public IEnumerator DashWidensTheViewAndReleasesItBeforeTheNextDash()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            var original = player.settings;
            var settings = Object.Instantiate(original);
            player.settings = settings;
            settings.dashDuration = 0.5f;
            settings.dashCooldown = 0.1f;
            try
            {
                player.SetMenuOpen(false);
                var camera = player.viewCamera;
                float baseline = settings.fieldOfView;
                player.SendMessage("StartDash", Vector2.zero);
                yield return null;
                Assert.That(camera.fieldOfView, Is.EqualTo(baseline).Within(0.001f));

                player.SendMessage("StartDash", Vector2.right);
                yield return WaitFor(() => camera.fieldOfView > baseline + 5f);
                yield return WaitFor(() => Mathf.Approximately(camera.fieldOfView, baseline));

                player.SendMessage("StartDash", Vector2.up);
                yield return WaitFor(() => camera.fieldOfView > baseline + 5f);
                player.SetMenuOpen(true);
                Assert.That(camera.fieldOfView, Is.EqualTo(baseline).Within(0.001f));
                player.SetMenuOpen(false);
                yield return WaitFor(() => camera.fieldOfView > baseline + 0.1f);
                player.Die();
                Assert.That(camera.fieldOfView, Is.EqualTo(baseline).Within(0.001f));
            }
            finally
            {
                Time.timeScale = 1f;
                if (player) player.settings = original;
                Object.Destroy(settings);
            }
        }

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True);
        }
    }
}
