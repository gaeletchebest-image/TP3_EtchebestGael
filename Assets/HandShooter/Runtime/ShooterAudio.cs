using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandShooter
{
    // Scene-owned: no authored scene/prefab changes, and no voices surviving a load/restart.
    [DefaultExecutionOrder(500)]
    public sealed class ShooterAudio : MonoBehaviour
    {
        private const int VoiceCount = 32;
        private const int LoopCount = 24;
        private static ShooterAudio instance;
        private static bool quitting;
        private static SoundCue? arrivalCue;
        private readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
        private readonly Dictionary<SoundCue, float> lastPlayed = new Dictionary<SoundCue, float>();
        private readonly List<Voice> loops = new List<Voice>();
        private readonly HashSet<ArenaEncounter> encounters = new HashSet<ArenaEncounter>();
        private readonly Voice[] voices = new Voice[VoiceCount];
        private readonly System.Random variation = new System.Random(7013);
        private readonly float[] levels = new float[4];
        private ShooterAudioSettings settings;
        private PrototypePlayer player;
        private bool paused;
        private float combatUntil, combatBlend;

        private sealed class Voice
        {
            public AudioSource source;
            public SoundCue cue;
            public AudioBus bus;
            public Transform owner;
            public bool ui, active;
            public float gain, remaining;
        }

        public static ShooterAudio Current => instance;
        public int ActiveOneShots
        {
            get { int count = 0; foreach (var voice in voices) if (voice != null && voice.active) count++; return count; }
        }
        public int ActiveLoops => loops.FindAll(voice => voice.active).Count;
        public int AllocatedLoops => loops.Count;
        public bool WorldPaused => paused;
        public bool HasClip(SoundCue cue) => clips.ContainsKey(cue);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; quitting = false; arrivalCue = null; }

        private static ShooterAudio Ensure()
        {
            if (!Application.isPlaying || quitting) return null;
            if (!instance)
            {
                var go = new GameObject("Shooter Audio");
                instance = go.AddComponent<ShooterAudio>();
            }
            return instance;
        }

        public static void Initialize(PrototypePlayer listener)
        {
            var audio = Ensure();
            if (!audio) return;
            audio.player = listener;
            if (listener && audio.gameObject.scene != listener.gameObject.scene)
                SceneManager.MoveGameObjectToScene(audio.gameObject, listener.gameObject.scene);
            if (listener && listener.viewCamera && !FindFirstObjectByType<AudioListener>())
                listener.viewCamera.gameObject.AddComponent<AudioListener>();
            Loop(audio.transform, SoundCue.IndustrialAmbience, true, 0.38f, 1f, true);
            Loop(audio.transform, SoundCue.MusicAmbient, true, 0.7f, 1f, true);
            Loop(audio.transform, SoundCue.MusicCombat, true, 0f, 1f, true);
            if (arrivalCue.HasValue) { Ui(arrivalCue.Value); arrivalCue = null; }
        }

        private void Awake()
        {
            instance = this;
            settings = Resources.Load<ShooterAudioSettings>("ShooterAudioSettings");
            foreach (var clip in Resources.LoadAll<AudioClip>("HandShooterAudio"))
                if (Enum.TryParse(clip.name, out SoundCue cue)) clips[cue] = clip;
            levels[0] = PlayerPrefs.GetFloat("Shooter.Audio.Master", settings ? settings.master : 0.8f);
            levels[1] = PlayerPrefs.GetFloat("Shooter.Audio.Effects", settings ? settings.effects : 0.8f);
            levels[2] = PlayerPrefs.GetFloat("Shooter.Audio.Ambience", settings ? settings.ambience : 0.35f);
            levels[3] = PlayerPrefs.GetFloat("Shooter.Audio.Music", settings ? settings.music : 0.25f);
            for (int i = 0; i < levels.Length; i++) levels[i] = Mathf.Clamp01(levels[i]);
            for (int i = 0; i < VoiceCount; i++) voices[i] = CreateVoice("Effect " + i);
        }

        private Voice CreateVoice(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = settings ? settings.audibleDistance : 42f;
            return new Voice { source = source };
        }

        public static float Volume(AudioBus bus) => Ensure() ? instance.levels[(int)bus] : 0f;
        public static void SetVolume(AudioBus bus, float value)
        {
            if (!Ensure()) return;
            instance.levels[(int)bus] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("Shooter.Audio." + bus, instance.levels[(int)bus]);
        }

        public static void Ui(SoundCue cue, float strength = 1f) => Play(cue, Vector3.zero, strength, true);
        public static void Local(SoundCue cue, float strength = 1f) => Play(cue, Vector3.zero, strength, false, true);
        public static void OnNextScene(SoundCue cue) => arrivalCue = cue;

        public static void PlayerDied(string cause)
        {
            var audio = Ensure();
            if (!audio) return;
            // The killing shot and CRT sting must remain audible when death freezes simulation.
            foreach (var voice in audio.voices)
                if (voice.active && (voice.cue == SoundCue.SniperFire || voice.cue == SoundCue.HunterFire ||
                    voice.cue == SoundCue.KamikazeExplosion || voice.cue == SoundCue.TurretFire)) voice.ui = true;
            Ui(SoundCue.Death);
            if (cause == "Tiempo agotado") Ui(SoundCue.Timeout);
        }

        public static void Play(SoundCue cue, Vector3 position, float strength = 1f, bool ui = false, bool flat = false)
        {
            var audio = Ensure();
            if (!audio || (!ui && Time.timeScale <= 0f) || !audio.clips.TryGetValue(cue, out var clip)) return;
            float now = Time.unscaledTime;
            float interval = cue == SoundCue.Empty ? 0.4f : cue == SoundCue.UiSelect ? 0.09f : 0.015f;
            if (audio.lastPlayed.TryGetValue(cue, out float last) && now - last < interval) return;
            float distance = audio.settings ? audio.settings.audibleDistance : 42f;
            if (!ui && !flat && audio.player && (position - audio.player.transform.position).sqrMagnitude > distance * distance) return;
            audio.lastPlayed[cue] = now;
            Voice chosen = null;
            foreach (var candidate in audio.voices)
            {
                if (!candidate.active) { chosen = candidate; break; }
                // Protect UI/death/alerts from world sound bursts.
                if (!candidate.ui && !IsWarning(candidate.cue) && (chosen == null || candidate.remaining < chosen.remaining)) chosen = candidate;
            }
            if (chosen == null && (cue == SoundCue.Death || cue == SoundCue.Timeout)) chosen = audio.voices[0];
            if (chosen == null) return;
            chosen.source.Stop();
            chosen.source.transform.position = position;
            chosen.cue = cue;
            chosen.bus = AudioBus.Effects;
            chosen.gain = Gain(cue) * Mathf.Clamp(strength, 0f, 1.6f);
            chosen.ui = ui;
            chosen.active = true;
            chosen.source.spatialBlend = ui || flat ? 0f : 1f;
            chosen.source.priority = ui || IsWarning(cue) ? 32 : 128;
            chosen.source.pitch = IsWarning(cue) || ui ? 1f : 0.94f + (float)audio.variation.NextDouble() * 0.12f;
            if (cue == SoundCue.RayFire || cue == SoundCue.ProjectileFire || cue == SoundCue.GrenadeThrow)
                chosen.source.pitch *= Mathf.Lerp(1.1f, 0.8f, Mathf.InverseLerp(0.8f, 1.4f, strength));
            chosen.source.clip = clip;
            chosen.source.loop = false;
            chosen.remaining = clip.length / chosen.source.pitch;
            chosen.source.volume = audio.EffectiveVolume(chosen);
            chosen.source.Play();
            if (!ui && audio.paused) chosen.source.Pause();
        }

        public static void Loop(Transform owner, SoundCue cue, bool active, float gain = 1f, float pitch = 1f, bool flat = false)
        {
            // Stopping a loop must never create a manager during teardown.
            var audio = active ? Ensure() : instance;
            if (!audio || !owner) return;
            Voice voice = audio.loops.Find(v => v.owner == owner && v.cue == cue);
            float distance = audio.settings ? audio.settings.audibleDistance : 42f;
            if (active && !flat && audio.player && (owner.position - audio.player.transform.position).sqrMagnitude > distance * distance) active = false;
            if (!active)
            {
                if (voice != null) { voice.source.Stop(); voice.active = false; }
                return;
            }
            if (!audio.clips.TryGetValue(cue, out var clip)) return;
            if (voice == null)
            {
                // Reuse stopped or orphaned loops; never grow with repeated projectiles/reloads.
                voice = audio.loops.Find(v => !v.active || !v.owner || !v.owner.gameObject.activeInHierarchy);
                if (voice == null)
                {
                    if (audio.loops.Count >= LoopCount)
                    {
                        if (!IsWarning(cue)) return;
                        voice = audio.loops.Find(v => v.cue == SoundCue.ProjectileFlight || v.cue == SoundCue.PulseFlight || v.cue == SoundCue.RobotMove);
                        if (voice == null) return;
                    }
                    else
                    {
                        voice = audio.CreateVoice("Loop " + cue);
                        audio.loops.Add(voice);
                    }
                }
                voice.source.Stop();
                voice.active = false;
                voice.owner = owner;
                voice.cue = cue;
                voice.source.clip = clip;
                voice.source.loop = true;
            }
            voice.bus = cue == SoundCue.MusicAmbient || cue == SoundCue.MusicCombat ? AudioBus.Music :
                cue == SoundCue.IndustrialAmbience || cue == SoundCue.StationHum ? AudioBus.Ambience : AudioBus.Effects;
            voice.ui = false;
            voice.gain = gain * Gain(cue);
            voice.source.spatialBlend = flat ? 0f : 1f;
            voice.source.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            voice.source.priority = IsWarning(cue) ? 40 : voice.bus == AudioBus.Effects ? 150 : 220;
            voice.source.transform.position = owner.position;
            voice.source.volume = audio.EffectiveVolume(voice);
            if (!voice.active)
            {
                voice.active = true;
                voice.source.Play();
                if (audio.paused || Time.timeScale <= 0f) voice.source.Pause();
            }
        }

        public static void Threat() { if (Ensure()) instance.combatUntil = Time.time + 5f; }
        public static void Encounter(ArenaEncounter arena, bool fighting)
        {
            if (!Ensure()) return;
            if (fighting) instance.encounters.Add(arena); else instance.encounters.Remove(arena);
        }

        private void LateUpdate()
        {
            bool freeze = Time.timeScale <= 0f;
            if (paused != freeze)
            {
                paused = freeze;
                foreach (var voice in voices) PauseVoice(voice, freeze);
                foreach (var voice in loops) PauseVoice(voice, freeze);
            }
            encounters.RemoveWhere(arena => !arena || !arena.isActiveAndEnabled);
            float target = encounters.Count > 0 || Time.time < combatUntil ? 1f : 0f;
            combatBlend = Mathf.MoveTowards(combatBlend, target, Time.unscaledDeltaTime * 0.65f);
            foreach (var voice in voices)
            {
                if (!voice.active) continue;
                voice.remaining -= voice.ui ? Time.unscaledDeltaTime : Time.deltaTime;
                if (voice.remaining <= 0f) { voice.source.Stop(); voice.active = false; }
                voice.source.volume = EffectiveVolume(voice);
            }
            foreach (var voice in loops)
            {
                if (!voice.active) continue;
                if (!voice.owner || !voice.owner.gameObject.activeInHierarchy)
                {
                    voice.source.Stop(); voice.active = false; continue;
                }
                voice.source.transform.position = voice.owner.position;
                if (voice.cue == SoundCue.MusicAmbient) voice.gain = 0.7f * (1f - combatBlend);
                if (voice.cue == SoundCue.MusicCombat) voice.gain = 0.65f * combatBlend;
                voice.source.volume = EffectiveVolume(voice);
            }
        }

        private static void PauseVoice(Voice voice, bool freeze)
        {
            if (voice == null || !voice.active || voice.ui) return;
            if (freeze) voice.source.Pause(); else voice.source.UnPause();
        }

        private float EffectiveVolume(Voice voice) => Mathf.Clamp01(voice.gain * levels[0] * levels[(int)voice.bus]);
        private static bool IsWarning(SoundCue cue) => cue == SoundCue.SniperCharge || cue == SoundCue.SniperLock ||
            cue == SoundCue.KamikazeChase || cue == SoundCue.TimerTick || cue == SoundCue.FuelLow;

        private static float Gain(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.StepSolid: case SoundCue.StepMetal: return 0.3f;
                case SoundCue.DashReady: case SoundCue.UiSelect: return 0.22f;
                case SoundCue.RobotMove: case SoundCue.ProjectileFlight: case SoundCue.PulseFlight: return 0.22f;
                case SoundCue.StationHum: case SoundCue.ReloadLoop: case SoundCue.RefuelLoop: return 0.35f;
                case SoundCue.SniperCharge: return 0.65f;
                case SoundCue.TargetHit: case SoundCue.WallHit: case SoundCue.ShieldHit: return 0.4f;
                case SoundCue.Death: case SoundCue.GrenadeExplosion: case SoundCue.KamikazeExplosion: return 0.85f;
                default: return 0.6f;
            }
        }

        private void OnApplicationQuit() => quitting = true;
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
