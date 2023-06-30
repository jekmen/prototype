using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public enum ShellType
    {
        Laser,
        Missiles
    }

    public interface SA_IWeapon
    {
        void Shoot(Transform[] outShell = null);

        SA_WeaponLunchManager.Settings Settings { get; }

        void SetOwner(SA_IShip ownerShip);

        void SetFireShells(SA_DamageSandler[] shellPrefab);
    }
}