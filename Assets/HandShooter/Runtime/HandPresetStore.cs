using UnityEngine;

namespace HandShooter
{
    public readonly struct HandPreset
    {
        public readonly int ActiveMask;
        public readonly bool ThumbActive;
        public bool IsSaved => HandConfiguration.IsValidMask(ActiveMask);
        public HandPreset(int activeMask, bool thumbActive)
        { ActiveMask = activeMask; ThumbActive = thumbActive; }
        public bool Matches(HandConfiguration hand) => IsSaved && hand != null &&
            hand.ActiveMask == ActiveMask && hand.ThumbActive == ThumbActive;
    }

    public sealed class HandPresetStore
    {
        public const int SlotCount = 10;
        private readonly HandPreset[] slots = new HandPreset[SlotCount];
        private readonly string keyPrefix;

        public HandPresetStore(string keyPrefix = "HandShooter.WeaponPreset.v1.")
        {
            this.keyPrefix = keyPrefix;
            for (int digit = 0; digit < SlotCount; digit++)
            {
                int encoded = PlayerPrefs.GetInt(keyPrefix + digit, 0);
                int mask = encoded & 15;
                if ((encoded & ~31) == 0 && HandConfiguration.IsValidMask(mask))
                    slots[digit] = new HandPreset(mask, (encoded & 16) != 0);
            }
        }

        public HandPreset Get(int digit) => digit >= 0 && digit < SlotCount ? slots[digit] : default;

        public bool Save(int digit, HandConfiguration hand)
        {
            if (digit < 0 || digit >= SlotCount || hand == null) return false;
            slots[digit] = new HandPreset(hand.ActiveMask, hand.ThumbActive);
            PlayerPrefs.SetInt(keyPrefix + digit, hand.ActiveMask | (hand.ThumbActive ? 16 : 0));
            PlayerPrefs.Save();
            return true;
        }
    }
}
