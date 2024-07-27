namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Ship;
    using SpaceAI.ShipSystems;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    [Serializable]
    public class SA_WeaponController : SA_IShipSystem
    {
        private int CurrentWeapon = 0;

        public IReadOnlyList<SA_IWeapon> WeaponLists { get; }

        public SA_WeaponController() { }

        public SA_WeaponController(SA_IShip ownerShip)
        {
            WeaponLists = ownerShip.CurrentShipTransform.GetComponentsInChildren<SA_IWeapon>(true).ToList();

            int id = -1;

            foreach (var weapon in WeaponLists)
            {
                id++;
                weapon.SetOwner(ownerShip);
                weapon.SetFireShells(ownerShip.ShipConfiguration.Items.ShellPrefab, id);
            }
        }

        public SA_IShipSystem Init(SA_IShip ship, GameObject gameObject)
        {
            return new SA_WeaponController(ship);
        }

        public SA_IWeapon GetCurrentWeapon()
        {
            if (CurrentWeapon < WeaponLists.Count && WeaponLists[CurrentWeapon] != null)
            {
                return WeaponLists[CurrentWeapon];
            }

            return null;
        }

        public void LaunchWeapon(int index)
        {
            CurrentWeapon = index;

            if (CurrentWeapon < WeaponLists.Count && WeaponLists[index] != null)
            {
                WeaponLists[index].Shoot();
            }
        }

        public void LaunchWeapon(int index, Transform[] outShell,  Vector3 aimPoint)
        {
            CurrentWeapon = index;

            if (CurrentWeapon < WeaponLists.Count && WeaponLists[index] != null)
            {
                WeaponLists[index].Shoot(outShell, aimPoint);
            }
        }

        public void SwitchWeapon()
        {
            CurrentWeapon = CurrentWeapon++ % WeaponLists.Count;
        }

        public void LaunchWeapons()
        {
            if (WeaponLists == null) { return; }

            foreach (var wpn in WeaponLists)
            {
                wpn.Shoot();
            }
        }

        public void LaunchWeapons(Transform[] outShell)
        {
            if (WeaponLists == null) { return; }

            foreach (var wpn in WeaponLists)
            {
                wpn.Shoot(outShell);
            }
        }

        public void ResetTurrets()
        {
            if (WeaponLists != null && WeaponLists.Count > 0)
            {
                foreach (var weapon in WeaponLists)
                {
                    if (weapon is SA_Turret turret)
                    {
                        turret.SetIdle(true);
                    }
                }
            }
        }

        public void TurretsControl(Vector3 target)
        {
            if (WeaponLists != null && WeaponLists.Count > 0)
            {
                foreach (var weapon in WeaponLists)
                {
                    if (weapon is SA_Turret turret)
                    {
                        turret.SetAimpoint(target);
                    }
                }

                LaunchWeapons();
            }
        }

        public void TurretsControl(Transform target, Vector3 aimPoint)
        {
            if (WeaponLists != null && WeaponLists.Count > 0)
            {
                foreach (var weapon in WeaponLists)
                {
                    if (weapon is SA_Turret turret)
                    {
                        //turret.Target = target.gameObject;
                        turret.independent = true;
                    }
                }
            }
        }

        public void ShipSystemEvent(Collision collision)
        {
        }
    }
}