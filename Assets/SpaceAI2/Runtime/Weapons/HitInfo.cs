using UnityEngine;

namespace SpaceAI2
{
    public struct HitInfo
    {
        public SpaceShip Source;
        /// <summary>Null when the hit landed on something that is not a ship.</summary>
        public SpaceShip Ship;
        public Collider Collider;
        /// <summary>The subsystem whose hitbox took the hit, if any.</summary>
        public Subsystem Subsystem;
        /// <summary>The gun weapon, or null for missile hits.</summary>
        public WeaponProfile Weapon;
        public MissileProfile Missile;
        public Vector3 Point;
        public Vector3 Normal;
        /// <summary>Direction of a shot's travel; zero for blasts.</summary>
        public Vector3 Direction;
        public float Damage;
        /// <summary>Blasts only: the radius, so subsystems near the point take their share.</summary>
        public float BlastRadius;
    }
}
