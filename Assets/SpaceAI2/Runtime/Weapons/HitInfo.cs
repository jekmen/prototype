using UnityEngine;

namespace SpaceAI2
{
    public struct HitInfo
    {
        public SpaceShip Source;
        /// <summary>Null when the hit landed on something that is not a ship.</summary>
        public SpaceShip Ship;
        public Collider Collider;
        /// <summary>The gun weapon, or null for missile hits.</summary>
        public WeaponProfile Weapon;
        public MissileProfile Missile;
        public Vector3 Point;
        public Vector3 Normal;
        public float Damage;
    }
}
