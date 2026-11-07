using UnityEngine;

namespace SpaceAI2
{
    public enum WeaponKind { Cannon, Flak, Laser }

    [CreateAssetMenu(menuName = "SpaceAI2/Weapon Profile", fileName = "WeaponProfile")]
    public class WeaponProfile : ScriptableObject
    {
        public WeaponKind Kind = WeaponKind.Cannon;

        [Header("Damage")]
        [Min(0f)] public float Damage = 6f;
        [Tooltip("Flak only: blast radius around the detonation point.")]
        [Min(0f)] public float BlastRadius = 20f;

        [Header("Fire")]
        [Tooltip("Shots per second.")]
        [Min(0.01f)] public float FireRate = 8f;
        [Tooltip("Projectile speed relative to the shooter, m/s. Ignored by lasers.")]
        [Min(1f)] public float MuzzleSpeed = 700f;
        [Min(1f)] public float Range = 900f;
        [Tooltip("Random cone half-angle, degrees.")]
        [Range(0f, 10f)] public float Spread = 0.15f;

        [Header("Visual")]
        public Color TracerColor = new Color(1f, 0.75f, 0.3f, 1f);
        [Min(0.01f)] public float TracerLength = 12f;
        [Min(0.01f)] public float TracerWidth = 0.6f;
        [Tooltip("Laser only: how long the beam stays visible, seconds.")]
        [Min(0.01f)] public float BeamTime = 0.06f;

        public bool IsInstant => Kind == WeaponKind.Laser;
        public float Lifetime => Range / MuzzleSpeed;
    }
}
