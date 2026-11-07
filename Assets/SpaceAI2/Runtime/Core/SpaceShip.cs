using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    public enum ShipControl
    {
        None,
        AI,
        Player
    }

    /// <summary>
    /// The one component a ship needs. With a <see cref="Profile"/> assigned it adds and configures the rest itself
    /// (motor, collider, health, gun, missiles, pilot); without one, the components are set up by hand.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipMotor))]
    public class SpaceShip : MonoBehaviour
    {
        [Tooltip("Who the ship fights and protects. Empty falls back to Team.")]
        [SerializeField] private Faction faction;
        [Tooltip("Used only between ships without a faction: different teams fight.")]
        [SerializeField] private int team;

        [Tooltip("Assigning a profile turns on setup: the ship adds and configures the components below.")]
        [SerializeField] private ShipProfile profile;
        [SerializeField] private ShipControl control = ShipControl.AI;
        [Tooltip("Skill of the AI pilot. Empty uses a default pilot.")]
        [SerializeField] private PilotProfile pilot;
        [Tooltip("Forward gun. Empty leaves the ship without one.")]
        [SerializeField] private WeaponProfile weapon;
        [Tooltip("Missile launcher. Empty leaves the ship without one.")]
        [SerializeField] private MissileProfile missile;
        [Tooltip("Adds hull and shield from the profile; the ship is disabled when the hull runs out.")]
        [SerializeField] private bool destructible = true;
        [Tooltip("Adds engine exhaust, hit and death effects at runtime when the scene has CombatEffects.")]
        [SerializeField] private bool visualEffects = true;

        [SerializeField] private UnityEvent<SpaceShip> onDestroyed = new UnityEvent<SpaceShip>();
        [Tooltip("The ship and the damage dealt.")]
        [SerializeField] private UnityEvent<SpaceShip, float> onDamaged = new UnityEvent<SpaceShip, float>();
        [Tooltip("The ship and its new target, raised by the AI.")]
        [SerializeField] private UnityEvent<SpaceShip, SpaceShip> onTargetAcquired = new UnityEvent<SpaceShip, SpaceShip>();
        [Tooltip("The ship and the locked target, null for unguided missiles.")]
        [SerializeField] private UnityEvent<SpaceShip, SpaceShip> onMissileLaunched = new UnityEvent<SpaceShip, SpaceShip>();

        private ShipMotor motor;
        private ShipHealth health;
        private bool healthSearched;
        private ShipSnapshot snapshot;
        private Subsystem[] subsystems = Array.Empty<Subsystem>();
        private readonly Dictionary<int, Subsystem> subsystemColliders = new Dictionary<int, Subsystem>();

        private CombatBrain brain;
        private MissileLauncher launcher;

        public event Action<SpaceShip, Collision> Collided;
        public event Action<HitInfo> Hit;

        public UnityEvent<SpaceShip> OnDestroyed => onDestroyed;
        public UnityEvent<SpaceShip, float> OnDamaged => onDamaged;
        public UnityEvent<SpaceShip, SpaceShip> OnTargetAcquired => onTargetAcquired;
        public UnityEvent<SpaceShip, SpaceShip> OnMissileLaunched => onMissileLaunched;

        public ShipProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public ShipControl Control
        {
            get => control;
            set => control = value;
        }

        public PilotProfile Pilot
        {
            get => pilot;
            set => pilot = value;
        }

        public WeaponProfile Weapon
        {
            get => weapon;
            set => weapon = value;
        }

        public MissileProfile Missile
        {
            get => missile;
            set => missile = value;
        }

        public bool Destructible
        {
            get => destructible;
            set => destructible = value;
        }

        public Faction Faction
        {
            get => faction;
            set => faction = value;
        }

        public int Team
        {
            get => team;
            set => team = value;
        }

        /// <summary>Identifies the ship's side for bookkeeping: its faction, or its team without one.</summary>
        public int Side => faction != null ? faction.GetInstanceID() : team;

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

        public bool IsEnemy(SpaceShip other) => Factions.AreHostile(this, other);

        /// <summary>True for ships on this ship's side: its faction, friendly factions, or its team without factions.</summary>
        public bool IsAlly(SpaceShip other) => Factions.AreAllied(this, other);

        /// <summary>
        /// Routes a hit through the subsystem whose hitbox it struck; a blast damages every subsystem in its radius
        /// on top of the hull.
        /// </summary>
        public void ReceiveHit(in HitInfo hit)
        {
            HitInfo routed = hit;
            Subsystem struck = null;
            if (hit.Collider != null && !subsystemColliders.TryGetValue(hit.Collider.GetInstanceID(), out struck) && hit.Direction != Vector3.zero)
                struck = BuriedSubsystem(hit);
            if (struck != null && !struck.IsDestroyed)
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
            if (hit.Damage > 0f) Factions.ReportAttack(this, hit.Source);
            Hit?.Invoke(routed);
        }

        /// <summary>
        /// A hull collider that encloses the subsystems' hitboxes would take every shot meant for them, so a shot on
        /// the hull carries on inward and strikes the first intact hitbox on its path, no deeper than the hull's
        /// thinnest half so it never reaches the far side.
        /// </summary>
        private Subsystem BuriedSubsystem(in HitInfo hit)
        {
            Vector3 extents = hit.Collider.bounds.extents;
            float depth = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z));
            var ray = new Ray(hit.Point - hit.Direction * 0.05f, hit.Direction);
            Subsystem best = null;
            foreach (Subsystem subsystem in subsystems)
            {
                if (subsystem.IsDestroyed) continue;
                foreach (Collider hitbox in subsystem.Colliders)
                {
                    if (!hitbox.enabled) continue;
                    float distance;
                    // A ray that starts inside a collider misses it.
                    if ((hitbox.ClosestPoint(ray.origin) - ray.origin).sqrMagnitude < 1e-6f) distance = 0f;
                    else if (hitbox.Raycast(ray, out RaycastHit inner, depth)) distance = inner.distance;
                    else continue;
                    if (best != null && distance >= depth) continue;
                    depth = distance;
                    best = subsystem;
                }
            }
            return best;
        }

        public void RestoreSubsystems()
        {
            foreach (Subsystem subsystem in subsystems) subsystem.Restore();
            UpdatePower();
        }

        /// <summary>
        /// Adds the components the setup asks for and hands them their profiles. Does nothing without a
        /// <see cref="Profile"/>; never removes components. Runs in Awake and from the inspector.
        /// </summary>
        public void ApplySetup()
        {
            if (profile == null) return;
            Motor.Profile = profile;
            if (GetComponentInChildren<Collider>() == null) gameObject.AddComponent<SphereCollider>().radius = profile.Radius;
            if (destructible) Ensure<ShipHealth>();
            if (weapon != null) Ensure<ShipGun>().Weapon = weapon;
            if (missile != null) Ensure<MissileLauncher>().Missile = missile;
            switch (control)
            {
                case ShipControl.AI:
                    AIPilot ai = Ensure<AIPilot>();
                    if (pilot != null) ai.Profile = pilot;
                    Ensure<CombatBrain>();
                    break;
                case ShipControl.Player:
                    Ensure<PlayerPilot>();
                    break;
            }
            healthSearched = false;
        }

        private T Ensure<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void Awake()
        {
            ApplySetup();
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

        private void Start()
        {
            if (visualEffects && CombatEffects.TryGet(out _) && GetComponent<ShipEffects>() == null) gameObject.AddComponent<ShipEffects>();
        }

        private void OnEnable()
        {
            SpaceAIWorld.Instance.Registry.Add(this);
            brain = GetComponent<CombatBrain>();
            launcher = GetComponent<MissileLauncher>();
            if (Health != null)
            {
                Health.Damaged += OnHealthDamaged;
                Health.Destroyed += OnHealthDestroyed;
            }
            if (brain != null) brain.TargetChanged += OnTargetChanged;
            if (launcher != null) launcher.Launched += OnLaunched;
        }

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Registry.Remove(this);
            Factions.Forgive(this);
            if (Health != null)
            {
                Health.Damaged -= OnHealthDamaged;
                Health.Destroyed -= OnHealthDestroyed;
            }
            if (brain != null) brain.TargetChanged -= OnTargetChanged;
            if (launcher != null) launcher.Launched -= OnLaunched;
        }

        private void OnHealthDamaged(ShipHealth _, HitInfo hit) => onDamaged.Invoke(this, hit.Damage);

        private void OnHealthDestroyed(ShipHealth _, HitInfo hit) => onDestroyed.Invoke(this);

        private void OnTargetChanged(CombatBrain _, SpaceShip target)
        {
            if (target != null) onTargetAcquired.Invoke(this, target);
        }

        private void OnLaunched(MissileLauncher source, int _) => onMissileLaunched.Invoke(this, source.LockTarget);

        private void OnDrawGizmosSelected()
        {
            ShipMotor shipMotor = GetComponent<ShipMotor>();
            ShipProfile shape = profile != null ? profile : shipMotor != null ? shipMotor.Profile : null;
            if (shape == null) return;
            Transform t = transform;
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(t.position, shape.Radius);

            // Tightest loop at cruise speed: bright in the pitch plane, faint in the yaw plane.
            float turn = shape.TurnRadius(shape.CruiseSpeed);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            GizmoUtils.DrawCircle(t.position + t.up * turn, t.right, t.forward, turn);
            GizmoUtils.DrawCircle(t.position - t.up * turn, t.right, t.forward, turn);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
            GizmoUtils.DrawCircle(t.position + t.right * turn, t.up, t.forward, turn);
            GizmoUtils.DrawCircle(t.position - t.right * turn, t.up, t.forward, turn);

            ShipGun gun = GetComponent<ShipGun>();
            WeaponProfile gunProfile = weapon != null ? weapon : gun != null ? gun.Weapon : null;
            if (gunProfile == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawLine(t.position, t.position + t.forward * gunProfile.Range);
        }

        private void OnCollisionEnter(Collision collision) => Collided?.Invoke(this, collision);
    }
}
