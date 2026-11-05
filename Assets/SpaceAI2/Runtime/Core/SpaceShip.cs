using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>A ship's state copied once per frame, so loops over many ships avoid native transform and body calls.</summary>
    public struct ShipSnapshot
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Velocity;
        public float Radius;
        public bool Active;
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipMotor))]
    public class SpaceShip : MonoBehaviour
    {
        [SerializeField] private int team;

        private ShipMotor motor;
        private ShipHealth health;
        private bool healthSearched;
        private ShipSnapshot snapshot;
        private Subsystem[] subsystems = Array.Empty<Subsystem>();
        private readonly Dictionary<int, Subsystem> subsystemColliders = new Dictionary<int, Subsystem>();

        public event Action<SpaceShip, Collision> Collided;
        public event Action<HitInfo> Hit;

        public int Team
        {
            get => team;
            set => team = value;
        }

        // Other components may ask for the motor from their own Awake, before ours has run.
        public ShipMotor Motor => motor != null ? motor : motor = GetComponent<ShipMotor>();
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Velocity => Motor.Velocity;
        public float Radius => Motor.Profile != null ? Motor.Profile.Radius : 1f;

        /// <summary>Null for ships without one.</summary>
        public ShipHealth Health
        {
            get
            {
                if (!healthSearched)
                {
                    health = GetComponent<ShipHealth>();
                    healthSearched = true;
                }
                return health;
            }
        }

        /// <summary>
        /// State as of the start of this frame's AI update (or the last origin shift). Use it in loops over many
        /// ships; use <see cref="Position"/> and <see cref="Velocity"/> where the exact current value matters.
        /// </summary>
        public ref readonly ShipSnapshot Snapshot => ref snapshot;

        internal void TakeSnapshot()
        {
            Transform t = transform;
            snapshot.Position = t.position;
            snapshot.Forward = t.forward;
            snapshot.Velocity = Motor.Velocity;
            snapshot.Radius = Radius;
            snapshot.Active = isActiveAndEnabled;
        }

        public IReadOnlyList<Subsystem> Subsystems => subsystems;

        public bool IsEnemy(SpaceShip other) => other != null && other.team != team;

        /// <summary>
        /// Routes a hit through the subsystem whose hitbox it struck; a blast damages every subsystem in its radius
        /// on top of the hull.
        /// </summary>
        public void ReceiveHit(in HitInfo hit)
        {
            HitInfo routed = hit;
            if (hit.Collider != null && subsystemColliders.TryGetValue(hit.Collider.GetInstanceID(), out Subsystem struck) && !struck.IsDestroyed)
            {
                routed.Subsystem = struck;
                routed.Damage = struck.Absorb(hit, hit.Damage);
            }
            else if (hit.BlastRadius > 0f)
            {
                foreach (Subsystem subsystem in subsystems)
                {
                    if (subsystem.IsDestroyed) continue;
                    float distance = Vector3.Distance(subsystem.AimPoint, hit.Point);
                    if (distance > hit.BlastRadius) continue;
                    subsystem.Absorb(hit, hit.Damage * (1f - distance / hit.BlastRadius));
                }
            }
            Hit?.Invoke(routed);
        }

        public void RestoreSubsystems()
        {
            foreach (Subsystem subsystem in subsystems) subsystem.Restore();
            UpdatePower();
        }

        private void Awake()
        {
            motor = GetComponent<ShipMotor>();
            subsystems = GetComponentsInChildren<Subsystem>(true);
            foreach (Subsystem subsystem in subsystems)
            {
                foreach (Collider collider in subsystem.Colliders) subsystemColliders[collider.GetInstanceID()] = subsystem;
                subsystem.Destroyed += (_, _) => UpdatePower();
            }
        }

        // Every lost engine takes its share of thrust and turn rate; with none left the ship drifts.
        private void UpdatePower()
        {
            int engines = 0, working = 0;
            foreach (Subsystem subsystem in subsystems)
            {
                if (subsystem.Kind != SubsystemKind.Engine) continue;
                engines++;
                if (!subsystem.IsDestroyed) working++;
            }
            if (engines > 0) Motor.Power = (float)working / engines;
        }

        private void OnEnable() => SpaceAIWorld.Instance.Registry.Add(this);

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Registry.Remove(this);
        }

        private void OnCollisionEnter(Collision collision) => Collided?.Invoke(this, collision);
    }
}
