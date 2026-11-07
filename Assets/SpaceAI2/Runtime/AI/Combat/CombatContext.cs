using UnityEngine;

namespace SpaceAI2
{
    /// <summary>What a combat brain knows about the fight, refreshed every tick and read by the behaviors.</summary>
    public class CombatContext
    {
        public CombatBrain Brain;
        public AIPilot Pilot;
        public SpaceShip Ship;
        public PilotProfile Profile;

        public SpaceShip Target;
        /// <summary>Part of a large target to shoot at; null aims at the hull.</summary>
        public Subsystem Subsystem;
        public Vector3 ToTarget;
        public float Distance;
        /// <summary>Angle between my nose and the line of sight to the target.</summary>
        public float AngleOff;
        /// <summary>Angle between the target's tail and me: 0 = right behind it, 180 = in front of it.</summary>
        public float Aspect;
        /// <summary>Angle between the target's nose and me.</summary>
        public float TargetAngleOff;
        /// <summary>Rate the distance to the target shrinks, m/s.</summary>
        public float Closure;

        public SpaceShip Threat;
        public float ThreatLevel;
        public Vector3 ToThreat;
        public float ThreatDistance;

        /// <summary>The enemy a fleeing pilot runs from; null unless the brain's engagement is Flee.</summary>
        public SpaceShip Danger;
        public Vector3 ToDanger;
        public float DangerDistance;

        /// <summary>The noticed missile closest to impact, valid while <see cref="HasMissile"/>.</summary>
        public MissileTrack Missile;
        public bool HasMissile;
        /// <summary>Seconds until the missile reaches me at the current closing speed.</summary>
        public float MissileTimeToGo;

        public float HullShare = 1f;
        public float WeaponRange;
        public float TurnRadius;
        public float TimeInBehavior;
        public float TimeSinceFiringChance;
        public float LastMerge = float.NegativeInfinity;

        public bool HasTarget => Target != null;
        public Vector3 TargetPoint => Subsystem != null ? Subsystem.AimPoint : Target.Position;
    }
}
