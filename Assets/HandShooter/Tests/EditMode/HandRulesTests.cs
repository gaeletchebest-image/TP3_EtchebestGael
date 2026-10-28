using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace HandShooter.Tests
{
    public sealed class HandRulesTests
    {
        private ShooterSettings settings;
        [SetUp] public void SetUp() { settings = ScriptableObject.CreateInstance<ShooterSettings>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(settings); }
        [TestCase(1, true, 1f, 0.2f)]
        [TestCase(2, true, 2f, 0.4f)]
        [TestCase(3, true, 4f, 0.65f)]
        [TestCase(4, true, 8f, 1f)]
        [TestCase(1, false, 2f, 0.4f)]
        [TestCase(2, false, 4f, 0.8f)]
        [TestCase(3, false, 8f, 1.3f)]
        [TestCase(4, false, 16f, 2f)]
        public void DefaultCombinations(int count, bool thumb, float damage, float interval)
        {
            ShotStats shot = settings.Evaluate(new HandConfiguration(count, thumb));
            Assert.That(shot.Damage, Is.EqualTo(damage).Within(0.0001f));
            Assert.That(shot.Interval, Is.EqualTo(interval).Within(0.0001f));
            Assert.That(shot.Hitscan, Is.EqualTo(thumb));
        }
        [Test] public void InspectorValuesDriveTheShot()
        {
            settings.baseDamage = 3; settings.additionalFingerMultiplier = 3;
            settings.cooldownByInactiveFingers[1] = 0.7f;
            settings.projectileDamageMultiplier = 4; settings.projectileCooldownMultiplier = 3;
            var shot = settings.Evaluate(new HandConfiguration(3, false));
            Assert.That(shot.Damage, Is.EqualTo(108f));
            Assert.That(shot.Interval, Is.EqualTo(2.1f).Within(0.0001f));
        }
        [Test] public void AllContiguousGroupsAreReachableAndEveryMovePreservesRules()
        {
            var reached = new HashSet<int>();
            Explore(new List<int>(), reached);
            Assert.That(reached, Is.EquivalentTo(new[] { 1, 2, 4, 8, 3, 6, 12, 7, 14, 15 }));
        }
        private static void Explore(List<int> path, HashSet<int> reached)
        {
            var hand = new HandConfiguration();
            foreach (int index in path) Assert.That(hand.TryToggle(index), Is.True);
            if (!reached.Add(hand.ActiveMask)) return;
            for (int i = -1; i <= 4; i++)
            {
                var candidate = new HandConfiguration();
                foreach (int index in path) candidate.TryToggle(index);
                int before = candidate.ActiveMask;
                bool allowed = candidate.CanToggle(i);
                Assert.That(candidate.TryToggle(i), Is.EqualTo(allowed));
                Assert.That(candidate.ActiveCount, Is.InRange(1, 4));
                Assert.That(HandConfiguration.IsValidMask(candidate.ActiveMask), Is.True);
                if (!allowed) Assert.That(candidate.ActiveMask, Is.EqualTo(before));
                else { var next = new List<int>(path) { i }; Explore(next, reached); }
            }
        }
    }
}
