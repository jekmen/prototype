using SpaceAI.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    public enum ShellType
    {
        Laser,
        Missiles
    }

    public interface IWeapon
    {
        void Shoot(Transform[] outShell = null);

        SA_WeaponLunchManager.Settings Settings { get; }

        void SetOwner(IShip ownerShip);

        void SetFireShells(SA_DamageSandler[] shellPrefab);
    }
}