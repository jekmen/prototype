namespace SpaceAI.WeaponSystem
{
    using SpaceAI.Ship;
    using UnityEngine;

    public enum ShellType
    {
        Laser,
        Missiles
    }

    public interface SA_IWeapon
    {
        void Shoot(Transform[] outShell = null, Vector3 aimPoint = new Vector3());

        SA_WeaponLaunchManager.Settings WeaponLaunchManagerSettings { get; }

        int WeaponId { get; }

        float BulletSpeed { get; }

        Vector3 BulletInitPos { get; }

        void SetOwner(SA_IShip ownerShip);

        void SetFireShells(SA_DamageSandler[] shellPrefab, int id);
    }
}