using System;
using NUnit.Framework;
using UnityEngine;

namespace HandShooter.Tests
{
    public sealed class HandPresetTests
    {
        private string prefix;
        private HandPresetStore presets;
        [SetUp] public void SetUp()
        {
            prefix = "HandShooter.Tests.Presets." + Guid.NewGuid() + ".";
            presets = new HandPresetStore(prefix);
        }
        [TearDown] public void TearDown()
        {
            for (int digit = 0; digit < HandPresetStore.SlotCount; digit++) PlayerPrefs.DeleteKey(prefix + digit);
            PlayerPrefs.Save();
        }
        [Test] public void EveryDigitStoresAnIndependentSnapshotAndSurvivesRecreation()
        {
            int[] masks = { 1, 2, 4, 8, 3, 6, 12, 7, 14, 15 };
            var hand = new HandConfiguration();
            for (int digit = 0; digit < masks.Length; digit++)
            {
                Assert.That(presets.Get(digit).IsSaved, Is.False);
                Assert.That(hand.TryApply(masks[digit], digit % 2 == 0), Is.True);
                Assert.That(presets.Save(digit, hand), Is.True);
            }
            hand.TryApply(1, false);
            var loaded = new HandPresetStore(prefix);
            for (int digit = 0; digit < masks.Length; digit++)
            {
                Assert.That(loaded.Get(digit).ActiveMask, Is.EqualTo(masks[digit]));
                Assert.That(loaded.Get(digit).ThumbActive, Is.EqualTo(digit % 2 == 0));
            }
        }
        [Test] public void OverwritingOneSlotLeavesOtherSlotsAlone()
        {
            presets.Save(1, new HandConfiguration(2, true));
            presets.Save(2, new HandConfiguration(3, true));
            presets.Save(1, new HandConfiguration(4, false));
            var loaded = new HandPresetStore(prefix);
            Assert.That(loaded.Get(1).Matches(new HandConfiguration(4, false)), Is.True);
            Assert.That(loaded.Get(2).Matches(new HandConfiguration(3, true)), Is.True);
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(5)] [TestCase(10)] [TestCase(16)] [TestCase(31 + 32)]
        public void CorruptSavedDataIsTreatedAsEmpty(int encoded)
        {
            PlayerPrefs.SetInt(prefix + 3, encoded);
            Assert.That(new HandPresetStore(prefix).Get(3).IsSaved, Is.False);
        }
        [TestCase(-1)] [TestCase(10)]
        public void InvalidSlotsAreIgnored(int digit)
        {
            Assert.That(presets.Save(digit, new HandConfiguration()), Is.False);
            Assert.That(presets.Get(digit).IsSaved, Is.False);
        }
        [TestCase(0)] [TestCase(5)] [TestCase(16)]
        public void InvalidConfigurationsCannotReplaceTheCurrentHand(int mask)
        {
            var hand = new HandConfiguration(2, true);
            Assert.That(hand.TryApply(mask, false), Is.False);
            Assert.That(hand.ActiveMask, Is.EqualTo(3));
            Assert.That(hand.ThumbActive, Is.True);
        }
    }
}
