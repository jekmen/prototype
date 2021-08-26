using System;
using UnityEngine;

namespace SpaceAI.WeaponSystem
{
    [Serializable]
    public class SA_WeaponController
    {
        private int CurrentWeapon = 0;

        public SA_WeaponController(Component ownerShip)
        {
            // find all attached weapons.
            if (ownerShip.transform.GetComponentsInChildren(typeof(SA_WeaponLunchManager)).Length > 0)
            {
                var weas = ownerShip.transform.GetComponentsInChildren(typeof(SA_WeaponLunchManager));

                WeaponLists = new SA_WeaponLunchManager[weas.Length];

                for (int i = 0; i < weas.Length; i++)
                {
                    WeaponLists[i] = weas[i].GetComponent<SA_WeaponLunchManager>();
                }
            }

            if (ownerShip.transform.GetComponentsInChildren(typeof(SA_TurretRotation)).Length > 0)
            {
                var tur = ownerShip.transform.GetComponentsInChildren(typeof(SA_TurretRotation));

                turretList = new SA_TurretRotation[tur.Length];

                for (int i = 0; i < tur.Length; i++)
                {
                    turretList[i] = tur[i].GetComponent<SA_TurretRotation>();
                }
            }
        }

        public SA_TurretRotation[] turretList { get; set; }
        public SA_WeaponLunchManager[] WeaponLists { get; set; }

        public SA_WeaponLunchManager GetCurrentWeapon()
        {
            if (CurrentWeapon < WeaponLists.Length && WeaponLists[CurrentWeapon] != null)
            {
                return WeaponLists[CurrentWeapon];
            }

            return null;
        }

        private void UpdateComponent()
        {
            for (int i = 0; i < WeaponLists.Length; i++)
            {
                if (WeaponLists[i] != null)
                {
                    WeaponLists[i].settings.OnActive = false;
                }
            }
            if (CurrentWeapon < WeaponLists.Length && WeaponLists[CurrentWeapon] != null)
            {
                WeaponLists[CurrentWeapon].settings.OnActive = true;
            }
        }

        public void LaunchWeapon(int index)
        {
            UpdateComponent();

            CurrentWeapon = index;
            if (CurrentWeapon < WeaponLists.Length && WeaponLists[index] != null)
            {
                WeaponLists[index].Shoot();
            }
        }

        public void SwitchWeapon()
        {
            CurrentWeapon += 1;
            if (CurrentWeapon >= WeaponLists.Length)
            {
                CurrentWeapon = 0;
            }
        }

        public void LaunchWeapon()
        {
            UpdateComponent();

            if (WeaponLists == null) { return; }

            if (WeaponLists[CurrentWeapon] != null && CurrentWeapon < WeaponLists.Length)
            {
                WeaponLists[CurrentWeapon].Shoot();
            }
        }

        public void LaunchWeapon(Transform[] outShell)
        {
            UpdateComponent();

            if (WeaponLists == null) { return; }

            if (WeaponLists[CurrentWeapon] != null && CurrentWeapon < WeaponLists.Length)
            {
                WeaponLists[CurrentWeapon].Shoot(outShell);
            }
        }

        public void LaunchTuretsWeapon()
        {
            UpdateComponent();

            for (int i = 0; i < WeaponLists.Length; i++)
            {
                if (WeaponLists[i] != null)
                {
                    WeaponLists[i].settings.OnActive = true;
                    WeaponLists[i].Shoot();
                }
            }
        }

        public void ResetTurrets()
        {
            if (turretList != null && turretList.Length > 0)
            {
                foreach (SA_TurretRotation tur in turretList)
                {
                    tur.SetIdle(true);
                }
            }
        }

        public void TurretControl(Vector3 target)
        {
            if (turretList != null && turretList.Length > 0)
            {
                foreach (SA_TurretRotation tur in turretList)
                {
                    tur.SetAimpoint(target);
                    LaunchTuretsWeapon();
                }
            }
        }

        public void TurretControl(Transform target, Vector3 aimPoint)
        {
            if (turretList != null && turretList.Length > 0)
            {
                foreach (SA_TurretRotation tur in turretList)
                {
                    tur.target = target;
                    tur.independent = true;
                    tur.aimPoint = aimPoint;
                }
            }
        }
    }
}