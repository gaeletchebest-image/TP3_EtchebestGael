using System;
using UnityEngine;

namespace HandShooter
{
    [Serializable]
    public sealed class HandConfiguration
    {
        [SerializeField] private int activeMask = 1;
        [SerializeField] private bool thumbActive = true;
        public bool ThumbActive { get => thumbActive; set => thumbActive = value; }
        public int ActiveMask => IsValidMask(activeMask) ? activeMask : 1;
        public int ActiveCount
        {
            get { int count = 0; for (int i = 0; i < 4; i++) if (IsActive(i)) count++; return count; }
        }
        public HandConfiguration(int count = 1, bool thumb = true)
        { activeMask = (1 << Mathf.Clamp(count, 1, 4)) - 1; thumbActive = thumb; }
        public bool IsActive(int index) => index >= 0 && index < 4 && (ActiveMask & (1 << index)) != 0;
        public bool CanToggle(int index) => index >= 0 && index < 4 && IsValidMask(ActiveMask ^ (1 << index));
        public bool TryToggle(int index)
        {
            if (!CanToggle(index)) return false;
            activeMask = ActiveMask ^ (1 << index);
            return true;
        }
        public bool TryApply(int mask, bool thumb)
        {
            if (!IsValidMask(mask)) return false;
            activeMask = mask;
            thumbActive = thumb;
            return true;
        }
        public static bool IsValidMask(int mask)
        {
            if (mask < 1 || mask > 15) return false;
            while ((mask & 1) == 0) mask >>= 1;
            return (mask & (mask + 1)) == 0;
        }
    }
}
