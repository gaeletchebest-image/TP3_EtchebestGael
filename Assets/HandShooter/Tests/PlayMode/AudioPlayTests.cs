using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HandShooter.Tests
{
    public sealed class AudioPlayTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly Dictionary<AudioBus, float> originalLevels = new Dictionary<AudioBus, float>();
        private readonly Dictionary<AudioBus, bool> hadPreferences = new Dictionary<AudioBus, bool>();
        private readonly List<GameObject> suspended = new List<GameObject>();
        private Scene testScene, previousScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            previousScene = SceneManager.GetActiveScene();
            foreach (var root in previousScene.GetRootGameObjects())
                if (root.activeSelf && root.name != "Code-based tests runner") { suspended.Add(root); root.SetActive(false); }
            testScene = SceneManager.CreateScene("Audio test arena");
            SceneManager.SetActiveScene(testScene);
            if (ShooterAudio.Current) Object.Destroy(ShooterAudio.Current.gameObject);
            yield return null;
            ShooterAudio.Ui(SoundCue.UiConfirm);
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                hadPreferences[bus] = PlayerPrefs.HasKey("Shooter.Audio." + bus);
                originalLevels[bus] = ShooterAudio.Volume(bus);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            foreach (var item in objects) if (item) Object.Destroy(item);
            objects.Clear();
            foreach (var entry in originalLevels)
            {
                ShooterAudio.SetVolume(entry.Key, entry.Value);
                if (!hadPreferences[entry.Key]) PlayerPrefs.DeleteKey("Shooter.Audio." + entry.Key);
            }
            if (ShooterAudio.Current) Object.Destroy(ShooterAudio.Current.gameObject);
            yield return null;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (testScene.IsValid() && testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(testScene);
            foreach (var root in suspended) if (root) root.SetActive(true);
            suspended.Clear();
        }

        private GameObject Owner()
        {
            var go = new GameObject("Audio test owner");
            objects.Add(go);
            return go;
        }

        private static AudioSource Source(SoundCue cue) => ShooterAudio.Current.GetComponentsInChildren<AudioSource>()
            .FirstOrDefault(source => source.clip && source.clip.name == cue.ToString());

        private static IEnumerator LoadPlayableScene()
        {
            yield return SceneManager.LoadSceneAsync("LevelStarter");
            yield return null; // Let Start initialize player/menu before opening it.
        }

        [UnityTest]
        public IEnumerator PauseFreezesWorldAndLocalEffectsWhileMenusAndDeathRemainAudible()
        {
            var owner = Owner();
            ShooterAudio.Loop(owner.transform, SoundCue.LiftMotor, true);
            ShooterAudio.Local(SoundCue.RayFire);
            yield return null;
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            Assert.That(ShooterAudio.Current.WorldPaused, Is.True);
            Assert.That(Source(SoundCue.LiftMotor).isPlaying, Is.False);
            Assert.That(Source(SoundCue.RayFire).isPlaying, Is.False);
            ShooterAudio.Ui(SoundCue.MenuOpen);
            ShooterAudio.PlayerDied("Sniper");
            yield return null;
            Assert.That(Source(SoundCue.MenuOpen).isPlaying, Is.True);
            Assert.That(Source(SoundCue.Death).isPlaying, Is.True);
            Time.timeScale = 1f;
            yield return null;
            Assert.That(Source(SoundCue.LiftMotor).isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator BurstLimitsRecycleLoopsAndPreserveSniperWarnings()
        {
            for (int i = 0; i < 60; i++) ShooterAudio.Loop(Owner().transform, SoundCue.ProjectileFlight, true);
            Assert.That(ShooterAudio.Current.AllocatedLoops, Is.LessThanOrEqualTo(24));
            var sniper = Owner();
            ShooterAudio.Loop(sniper.transform, SoundCue.SniperCharge, true);
            Assert.That(Source(SoundCue.SniperCharge), Is.Not.Null, "Threat warning must preempt projectile hum");
            Assert.That(Source(SoundCue.SniperCharge).isPlaying, Is.True);
            foreach (var go in objects) Object.Destroy(go);
            yield return null;
            yield return null;
            Assert.That(ShooterAudio.Current.ActiveLoops, Is.Zero);
            ShooterAudio.Loop(Owner().transform, SoundCue.RefuelLoop, true);
            Assert.That(ShooterAudio.Current.ActiveLoops, Is.EqualTo(1));
            Assert.That(ShooterAudio.Current.AllocatedLoops, Is.LessThanOrEqualTo(24));
        }

        [UnityTest]
        public IEnumerator EmptyTriggerIsThrottledAndDeathSurvivesVoiceSaturation()
        {
            for (int i = 0; i < 100; i++) ShooterAudio.Local(SoundCue.Empty);
            Assert.That(ShooterAudio.Current.GetComponentsInChildren<AudioSource>()
                .Count(source => source.clip && source.clip.name == "Empty"), Is.EqualTo(1));
            foreach (SoundCue cue in System.Enum.GetValues(typeof(SoundCue)))
                if (cue != SoundCue.Death) ShooterAudio.Ui(cue);
            ShooterAudio.PlayerDied("Hunter");
            Assert.That(ShooterAudio.Current.ActiveOneShots, Is.LessThanOrEqualTo(32));
            Assert.That(Source(SoundCue.Death), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VolumesApplyToLiveLoopsAndPersistForNextAudioSession()
        {
            ShooterAudio.SetVolume(AudioBus.Master, 1f);
            ShooterAudio.SetVolume(AudioBus.Effects, 0f);
            ShooterAudio.Loop(Owner().transform, SoundCue.LiftMotor, true);
            yield return null;
            Assert.That(Source(SoundCue.LiftMotor).volume, Is.Zero);
            ShooterAudio.SetVolume(AudioBus.Effects, 0.5f);
            yield return null;
            Assert.That(Source(SoundCue.LiftMotor).volume, Is.GreaterThan(0f));
            Object.Destroy(ShooterAudio.Current.gameObject);
            yield return null;
            Assert.That(ShooterAudio.Volume(AudioBus.Effects), Is.EqualTo(0.5f));
        }

        [UnityTest]
        public IEnumerator RealSceneFiringAndAudioMenuAreWiredAndSceneLoadsRemoveOldVoices()
        {
            yield return LoadPlayableScene();
            var player = Object.FindFirstObjectByType<PrototypePlayer>();
            foreach (var target in Object.FindObjectsByType<DamageTarget>(FindObjectsSortMode.None))
                if (target.GetComponent<TurretEnemy>() || target.GetComponent<SniperEnemy>() ||
                    target.GetComponent<HunterEnemy>() || target.GetComponent<KamikazeEnemy>()) target.gameObject.SetActive(false);
            player.SetMenuOpen(false);
            Time.timeScale = 1f;
            player.weapon.gameObject.SetActive(true);
            player.weapon.Hand.SetMode(WeaponMode.Ray);
            Assert.That(player.weapon.TryFire(), Is.True);
            Assert.That(Source(SoundCue.RayFire), Is.Not.Null);
            // Escape uses the separate pause menu. Reflection avoids depending on internals in production.
            typeof(PrototypePlayer).GetMethod("SetMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(player, new object[] { true, true });
            yield return null;
            yield return null;
            var audioButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(button => button.GetComponentInChildren<Text>()?.text == "Audio");
            audioButton.onClick.Invoke();
            yield return null;
            Assert.That(Object.FindObjectsByType<Slider>(FindObjectsSortMode.None).Length, Is.EqualTo(4));
            var oldAudio = ShooterAudio.Current;
            yield return LoadPlayableScene();
            Assert.That(oldAudio == null, Is.True);
            Assert.That(Object.FindObjectsByType<ShooterAudio>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }
    }
}
