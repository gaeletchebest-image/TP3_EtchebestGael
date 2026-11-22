using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class AudioAssetTests
    {
        [Test]
        public void EveryCueHasAnAudibleClipIncludedInBuildResources()
        {
            var clips = Resources.LoadAll<AudioClip>("HandShooterAudio").ToDictionary(clip => clip.name);
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                Assert.That(clips.ContainsKey(cue.ToString()), Is.True, "Missing clip: " + cue);
                var clip = clips[cue.ToString()];
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True, "Unreadable clip: " + cue);
                Assert.That(samples.Max(value => Mathf.Abs(value)), Is.InRange(0.01f, 0.85f), cue + " headroom");
                Assert.That(samples.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True, cue.ToString());
            }
            Assert.That(Resources.Load<ShooterAudioSettings>("ShooterAudioSettings"), Is.Not.Null);
        }

        [Test]
        public void ContinuousTracksHaveMatchingEndpointsAndMusicLayersHaveMatchingLength()
        {
            string[] loops = { "IndustrialAmbience", "MusicAmbient", "MusicCombat", "GrenadeFuse", "LiftMotor", "SniperCharge" };
            foreach (string name in loops)
            {
                var clip = Resources.Load<AudioClip>("HandShooterAudio/" + name);
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                Assert.That(Mathf.Abs(samples[0] - samples[samples.Length - 1]), Is.LessThan(0.005f), name);
            }
            Assert.That(Resources.Load<AudioClip>("HandShooterAudio/MusicAmbient").samples,
                Is.EqualTo(Resources.Load<AudioClip>("HandShooterAudio/MusicCombat").samples));
        }
    }
}
