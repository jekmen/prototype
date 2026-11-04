using System;
using UnityEngine;

namespace SpaceAI2
{
    public enum SubsystemKind { Turret, Engine, ShieldGenerator, Hangar, Other }

    /// <summary>
    /// A separately targetable part of a large ship with its own hitbox and hit points. Hits on its colliders damage it
    /// and pass a share to the hull; once destroyed, further hits go to the hull in full.
    /// </summary>
    [DisallowMultipleComponent]
    public class Subsystem : MonoBehaviour
    {
        [SerializeField] private SubsystemKind kind = SubsystemKind.Turret;
        [SerializeField, Min(1f)] private float maxHealth = 200f;
        [Tooltip("Share of the damage this subsystem takes that also reaches the hull.")]
        [SerializeField, Range(0f, 1f)] private float hullShare = 0.25f;

        private Collider[] colliders;

        public event Action<Subsystem, HitInfo> Damaged;
        public event Action<Subsystem, HitInfo> Destroyed;

        public SubsystemKind Kind
        {
            get => kind;
            set => kind = value;
        }

        public float MaxHealth
        {
            get => maxHealth;
            set => maxHealth = Mathf.Max(1f, value);
        }

        public float HullShare
        {
            get => hullShare;
            set => hullShare = Mathf.Clamp01(value);
        }

        public SpaceShip Ship { get; private set; }
        public float Health { get; private set; }
        public bool IsDestroyed => Health <= 0f;
        public Collider[] Colliders => colliders ??= GetComponentsInChildren<Collider>(true);

        /// <summary>Where to shoot to hit it: the center of its hitbox.</summary>
        public Vector3 AimPoint
        {
            get
            {
                Collider[] hitboxes = Colliders;
                return hitboxes.Length > 0 ? hitboxes[0].bounds.center : transform.position;
            }
        }

        public void Restore() => Health = maxHealth;

        private void Awake()
        {
            Ship = GetComponentInParent<SpaceShip>();
            Restore();
        }

        /// <summary>Applies the hit and returns the damage that carries on to the hull.</summary>
        internal float Absorb(in HitInfo hit, float damage)
        {
            if (IsDestroyed) return damage;
            Health = Mathf.Max(0f, Health - damage);
            Damaged?.Invoke(this, hit);
            if (IsDestroyed) Destroyed?.Invoke(this, hit);
            return damage * hullShare;
        }
    }
}
