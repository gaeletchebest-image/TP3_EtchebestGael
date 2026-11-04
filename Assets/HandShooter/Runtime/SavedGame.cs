using System;
using System.Collections.Generic;
using UnityEngine;

namespace HandShooter
{
    [Serializable]
    internal sealed class SavedGame
    {
        internal const int CurrentVersion = 1;
        public int version;
        public long savedAtUtc;
        public string scene;
        public Player player;
        public List<Enemy> enemies;
        public List<Door> doors;
        public List<Arena> arenas;
        public List<string> consumedPickups;

        [Serializable]
        internal sealed class Player
        {
            public Vector3 position, dashDirection;
            public Quaternion rotation;
            public float pitch, verticalVelocity, dashRemaining, dashCooldown;
            public Weapon weapon;
        }

        [Serializable]
        internal sealed class Weapon
        {
            public float fuel, cooldown, reloadElapsed;
            public int spareTanks, mask;
            public bool thumb, reloading;
        }

        [Serializable]
        internal sealed class Enemy
        {
            public string id;
            public bool defeated, acquiredTarget;
            public Vector3 position;
            public Quaternion rotation;
            public float health, lastDamage, shotDelay;
            public int defensePoints, defenseBar, defenseCapacity, destroyedCount;
            public Navigation navigation;
            public Hunter hunter;
            public Sniper sniper;
        }

        [Serializable]
        internal sealed class Navigation
        {
            public bool hasDestination, stopped, autoBraking;
            public Vector3 destination;
            public float speed, acceleration;
        }

        [Serializable]
        internal sealed class Hunter
        {
            public HunterEnemy.BehaviourState state;
            public bool alerted, hasPatrolDestination;
            public int shotsFired;
            public float repathRemaining, patrolWaitRemaining;
        }

        [Serializable]
        internal sealed class Sniper
        {
            public SniperEnemy.AttackState state;
            public float chargeProgress, stateElapsed, cooldownRemaining, repathRemaining, zigzagSide;
            public bool aimLocked, acquiredTarget, hasPosition, followingZigzag, retreating;
            public int shotsFired;
            public Vector3 aimDirection, plannedPosition, zigzagWaypoint;
        }

        [Serializable]
        internal sealed class Door
        {
            public string id;
            public bool open;
            public float openness, startOpenness, progress;
        }

        [Serializable]
        internal sealed class Arena
        {
            public string id;
            public ArenaEncounter.EncounterState state;
        }
    }
}
