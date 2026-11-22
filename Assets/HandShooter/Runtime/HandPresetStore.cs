using UnityEngine;

namespace HandShooter
{
    public readonly struct HandPreset
    {
        public readonly int ActiveMask;
        public readonly WeaponMode Mode;
        public bool ThumbActive => Mode == WeaponMode.Ray;
        public bool IsSaved => HandConfiguration.IsValidMask(ActiveMask);
        public HandPreset(int activeMask, bool thumbActive)
            : this(activeMask, thumbActive ? WeaponMode.Ray : WeaponMode.Projectile) { }
        public HandPreset(int activeMask, WeaponMode mode)
        { ActiveMask = activeMask; Mode = mode; }
        public bool Matches(HandConfiguration hand) => IsSaved && hand != null &&
            hand.ActiveMask == ActiveMask && (!hand.ThumbUnlocked || hand.Mode == Mode);
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
                if ((encoded & ~63) == 0 && (encoded & 48) != 48 && HandConfiguration.IsValidMask(mask))
                    slots[digit] = new HandPreset(mask, (encoded & 32) != 0 ? WeaponMode.Grenade :
                        (encoded & 16) != 0 ? WeaponMode.Ray : WeaponMode.Projectile);
            }
        }

        public HandPreset Get(int digit) => digit >= 0 && digit < SlotCount ? slots[digit] : default;

        public bool Save(int digit, HandConfiguration hand)
        {
            if (digit < 0 || digit >= SlotCount || hand == null) return false;
            slots[digit] = new HandPreset(hand.ActiveMask, hand.Mode);
            PlayerPrefs.SetInt(keyPrefix + digit, hand.ActiveMask | (hand.Mode == WeaponMode.Grenade ? 32 : hand.ThumbActive ? 16 : 0));
            PlayerPrefs.Save();
            return true;
        }
    }
}
