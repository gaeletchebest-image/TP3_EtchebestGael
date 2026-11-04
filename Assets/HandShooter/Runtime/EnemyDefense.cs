using UnityEngine;

namespace HandShooter
{
    // Shared by actual enemies and the balance calculator. Damage uses the defense
    // present at impact; breaking a point benefits the following shot.
    public struct EnemyDefense
    {
        public int Points { get; private set; }
        public int Bar { get; private set; }
        public int Capacity { get; private set; }
        public float DamageMultiplier => Mathf.Pow(0.5f, Points);

        public EnemyDefense(int points, int capacity)
            : this(points, capacity, capacity) { }

        internal EnemyDefense(int points, int capacity, int bar)
        {
            Points = Mathf.Max(0, points);
            Capacity = Mathf.Max(1, capacity);
            Bar = Points > 0 ? Mathf.Clamp(bar, 1, Capacity) : 0;
        }

        public void RegisterPlayerHit()
        {
            if (Points <= 0) return;
            if (--Bar > 0) return;
            Points--;
            Bar = Points > 0 ? Capacity : 0;
        }
    }
}
