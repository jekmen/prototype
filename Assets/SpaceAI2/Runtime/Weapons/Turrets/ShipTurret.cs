using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    public enum TurretRole { PointDefense, Flak, Main }

    /// <summary>
    /// A turret on its own mount: yaw around the mount's up axis, pitch above it, each with its own limits, rate and
    /// acceleration. Picks targets by threat against the time to swing onto them and the projectile's flight time,
    /// and never fires through its own ship. The mount's forward is yaw zero.
    /// </summary>
    public class ShipTurret : MonoBehaviour
    {
        private struct Axis
        {
            public float Angle;
            public float Velocity;

            public void Step(float target, float rate, float acceleration, float dt, bool wrap)
            {
                float error = wrap ? Mathf.DeltaAngle(Angle, target) : target - Angle;
                // Fastest approach that can still stop on the target.
                float wanted = Mathf.Sign(error) * Mathf.Min(rate, Mathf.Sqrt(2f * acceleration * Mathf.Abs(error)));
                Velocity = Mathf.MoveTowards(Velocity, wanted, acceleration * dt);
                Angle += Velocity * dt;
                if (wrap) Angle = Mathf.DeltaAngle(0f, Angle);
            }
        }

        private struct Aim
        {
            public SpaceShip Ship;
            public int MissileId;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Radius;

            public bool IsMissile => MissileId != 0;
            public bool IsValid => Ship != null || MissileId != 0;
        }

        private const float RetargetInterval = 0.25f;
        private const float BigShipRadius = 15f;

        [SerializeField] private WeaponProfile weapon;
        [SerializeField] private TurretRole role = TurretRole.PointDefense;

        [Header("Traverse")]
        [Tooltip("Half-angle the turret can yaw either side of the mount's forward; 180 turns all the way round, degrees.")]
        [SerializeField, Range(0f, 180f)] private float yawLimit = 180f;
        [SerializeField, Range(-90f, 90f)] private float minPitch = -5f;
        [SerializeField, Range(-90f, 90f)] private float maxPitch = 85f;
        [Tooltip("Degrees per second.")]
        [SerializeField, Min(1f)] private float yawRate = 120f;
        [SerializeField, Min(1f)] private float pitchRate = 120f;
        [Tooltip("Degrees per second squared.")]
        [SerializeField, Min(1f)] private float yawAcceleration = 600f;
        [SerializeField, Min(1f)] private float pitchAcceleration = 600f;

        [Header("Barrels")]
        [SerializeField, Min(1)] private int barrels = 2;
        [SerializeField, Min(0f)] private float barrelSpacing = 0.8f;
        [Tooltip("Distance from the pivot to the muzzles, meters.")]
        [SerializeField, Min(0f)] private float barrelLength = 3f;
        [Tooltip("Fires once the aim is within the target's angular size plus this, degrees.")]
        [SerializeField, Range(0f, 10f)] private float aimTolerance = 0.5f;
        [Tooltip("Flak only: random error of the fuse setting, as a share of the flight time.")]
        [SerializeField, Range(0f, 0.2f)] private float fuseError = 0.04f;

        [Header("Visual")]
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;

        private readonly List<SpaceShip> scratch = new List<SpaceShip>();
        private SpaceShip ship;
        private Subsystem subsystem;
        private Collider[] hull;
        private Axis yaw, pitch;
        private Aim aim;
        private Vector3 aimVelocity;
        private Vector3 aimAcceleration;
        private float retarget;
        private float cooldown;
        private int barrel;

        public event Action<ShipTurret> Fired;

        public WeaponProfile Weapon
        {
            get => weapon;
            set => weapon = value;
        }

        public TurretRole Role
        {
            get => role;
            set => role = value;
        }

        public SpaceShip Ship => ship;
        public bool IsDestroyed => subsystem != null && subsystem.IsDestroyed;
        public bool HasTarget => aim.IsValid;
        public SpaceShip TargetShip => aim.Ship;
        public bool TargetIsMissile => aim.IsMissile;
        /// <summary>Shots that would have crossed the own hull and were held; should stay zero with a sound setup.</summary>
        public int BlockedShots { get; private set; }

        public void Configure(TurretRole turretRole, float yawHalfAngle, float pitchMin, float pitchMax, float traverseRate, float traverseAcceleration)
        {
            role = turretRole;
            yawLimit = yawHalfAngle;
            minPitch = pitchMin;
            maxPitch = pitchMax;
            yawRate = pitchRate = traverseRate;
            yawAcceleration = pitchAcceleration = traverseAcceleration;
        }

        public void SetBarrels(int count, float spacing, float length, Transform yawVisual, Transform pitchVisual)
        {
            barrels = Mathf.Max(1, count);
            barrelSpacing = spacing;
            barrelLength = length;
            yawPivot = yawVisual;
            pitchPivot = pitchVisual;
        }

        public Vector3 FireDirection => transform.rotation * (Quaternion.Euler(-pitch.Angle, yaw.Angle, 0f) * Vector3.forward);

        /// <summary>True when the turret can point at the point and the line to it is clear of its own ship.</summary>
        public bool CanReach(Vector3 point)
        {
            if (!InArc(point - transform.position, out _, out _)) return false;
            return IsClear(transform.position, point - transform.position, Vector3.Distance(transform.position, point));
        }

        // Traverse sector: the yaw arc at the elevation limits, out to the weapon's range.
        private void OnDrawGizmosSelected()
        {
            Transform t = transform;
            float range = weapon != null ? weapon.Range : 500f;
            float reach = Mathf.Min(range, 150f);
            Gizmos.color = role == TurretRole.PointDefense ? new Color(0.3f, 1f, 1f, 0.6f) : new Color(1f, 0.5f, 0.2f, 0.6f);
            foreach (float elevation in new[] { minPitch, maxPitch })
            {
                Vector3 start = t.rotation * (Quaternion.Euler(-elevation, -yawLimit, 0f) * Vector3.forward);
                float radius = Mathf.Cos(elevation * Mathf.Deg2Rad) * reach;
                Vector3 center = t.position + t.up * (Mathf.Sin(elevation * Mathf.Deg2Rad) * reach);
                GizmoUtils.DrawArc(center, t.up, Vector3.ProjectOnPlane(start, t.up).normalized * radius, yawLimit * 2f);
                if (yawLimit >= 180f) continue;
                Gizmos.DrawLine(t.position, t.position + start * reach);
                Gizmos.DrawLine(t.position, t.position + t.rotation * (Quaternion.Euler(-elevation, yawLimit, 0f) * Vector3.forward) * reach);
            }
            if (!Application.isPlaying) return;
            Gizmos.color = HasTarget ? Color.red : Color.gray;
            Gizmos.DrawLine(t.position, t.position + FireDirection * (HasTarget ? Vector3.Distance(t.position, aim.Position) : reach));
        }

        private void OnEnable() => SpaceAI.OriginShifted += OnOriginShifted;

        private void OnDisable() => SpaceAI.OriginShifted -= OnOriginShifted;

        private void OnOriginShifted(Vector3 offset) => aim.Position -= offset;

        private void Awake()
        {
            ship = GetComponentInParent<SpaceShip>();
            subsystem = GetComponent<Subsystem>();
            var own = new List<Collider>();
            foreach (Collider collider in ship.GetComponentsInChildren<Collider>(true))
                if (!collider.transform.IsChildOf(transform)) own.Add(collider);
            hull = own.ToArray();
            retarget = UnityEngine.Random.Range(0f, RetargetInterval);
        }

        private void FixedUpdate()
        {
            if (weapon == null || IsDestroyed) return;
            float dt = Time.fixedDeltaTime;
            if (cooldown > 0f) cooldown -= dt;

            bool had = aim.IsValid;
            bool kept = Refresh(dt);
            retarget -= dt;
            if (retarget <= 0f || (had && !kept))
            {
                retarget = RetargetInterval;
                SelectTarget();
            }

            if (!aim.IsValid)
            {
                Traverse(0f, 0f, dt);
                return;
            }

            bool solved = Solve(aim, out Vector3 direction, out float time);
            InArc(direction, out float wantedYaw, out float wantedPitch);
            Traverse(wantedYaw, wantedPitch, dt);
            if (!solved || time * weapon.MuzzleSpeed > weapon.Range) return;

            float distance = Mathf.Max(1f, Vector3.Distance(aim.Position, transform.position));
            float tolerance = Mathf.Atan2(aim.Radius, distance) * Mathf.Rad2Deg + aimTolerance;
            Vector3 fire = FireDirection;
            if (Vector3.Angle(fire, direction) > tolerance) return;

            while (cooldown <= 0f)
            {
                if (!Fire(fire, time)) break;
                cooldown += 1f / weapon.FireRate;
            }
        }

        private void LateUpdate()
        {
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, yaw.Angle, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(-pitch.Angle, 0f, 0f);
        }

        private void Traverse(float wantedYaw, float wantedPitch, float dt)
        {
            bool full = yawLimit >= 180f;
            yaw.Step(full ? wantedYaw : Mathf.Clamp(wantedYaw, -yawLimit, yawLimit), yawRate, yawAcceleration, dt, full);
            if (!full) yaw.Angle = Mathf.Clamp(yaw.Angle, -yawLimit, yawLimit);
            pitch.Step(Mathf.Clamp(wantedPitch, minPitch, maxPitch), pitchRate, pitchAcceleration, dt, false);
            pitch.Angle = Mathf.Clamp(pitch.Angle, minPitch, maxPitch);
        }

        private bool Fire(Vector3 direction, float time)
        {
            Quaternion rotation = Quaternion.LookRotation(direction, transform.up);
            float offset = barrels > 1 ? (barrel / (barrels - 1f) - 0.5f) * barrelSpacing : 0f;
            Vector3 origin = transform.position + rotation * new Vector3(offset, 0f, barrelLength);
            if (!IsClear(origin, direction, weapon.Range))
            {
                BlockedShots++;
                return false;
            }
            barrel = (barrel + 1) % barrels;

            if (weapon.Spread > 0f)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * Mathf.Tan(weapon.Spread * Mathf.Deg2Rad);
                direction = (direction + rotation * new Vector3(jitter.x, jitter.y, 0f)).normalized;
            }
            float fuse = weapon.Kind == WeaponKind.Flak ? time * (1f + UnityEngine.Random.Range(-fuseError, fuseError)) : 0f;
            ProjectileSystem.Instance.Fire(weapon, ship, origin, direction, fuse);
            Fired?.Invoke(this);
            return true;
        }

        /// <summary>Updates the current target; false when it is gone or out of reach.</summary>
        private bool Refresh(float dt)
        {
            if (!aim.IsValid) return false;
            Vector3 velocity;
            if (aim.IsMissile)
            {
                if (!MissileSystem.TryGet(out MissileSystem missiles) || !missiles.TryGetTrack(aim.MissileId, out MissileTrack track))
                    return Drop();
                aim.Position = track.Position;
                velocity = track.Velocity;
                aimAcceleration = Vector3.Lerp(aimAcceleration, (velocity - aimVelocity) / dt, 0.3f);
            }
            else
            {
                if (aim.Ship == null || !aim.Ship.isActiveAndEnabled) return Drop();
                aim.Position = aim.Ship.Position;
                velocity = aim.Ship.Velocity;
                aimAcceleration = aim.Ship.Motor.Acceleration;
            }
            aimVelocity = velocity;
            aim.Velocity = velocity;
            return Vector3.Distance(aim.Position, transform.position) <= weapon.Range * 1.1f || Drop();
        }

        private bool Drop()
        {
            aim = default;
            return false;
        }

        private bool Solve(in Aim target, out Vector3 direction, out float time) =>
            Ballistics.Intercept(target.Position - transform.position, target.Velocity - ship.Velocity, aimAcceleration,
                weapon.MuzzleSpeed, out direction, out time);

        private void SelectTarget()
        {
            Aim best = default;
            float bestScore = 0f;

            if (role != TurretRole.Main && MissileSystem.TryGet(out MissileSystem missiles))
            {
                for (int i = 0; i < missiles.Count; i++)
                {
                    MissileTrack track = missiles[i];
                    if (!MissileSystem.Threatens(track, ship.Team)) continue;
                    var candidate = new Aim { MissileId = track.Id, Position = track.Position, Velocity = track.Velocity, Radius = track.Profile.HitRadius };
                    float threat = (role == TurretRole.PointDefense ? 10f : 3f) * Mathf.Max(0.5f, track.Profile.Damage / 100f);
                    if (track.Target == ship) threat *= 2f;
                    Consider(candidate, threat, ref best, ref bestScore);
                }
            }

            SpaceAIWorld.Instance.Registry.Query(transform.position, weapon.Range, scratch);
            foreach (SpaceShip other in scratch)
            {
                if (!ship.IsEnemy(other)) continue;
                bool big = other.Radius >= BigShipRadius;
                // The main gun traverses too slowly for fighters; it is kept for ships its size.
                if (role == TurretRole.Main && !big) continue;
                float threat = role switch
                {
                    TurretRole.Main => 5f,
                    TurretRole.Flak => big ? 0.5f : 3f,
                    _ => big ? 0.2f : 1f
                };
                var candidate = new Aim { Ship = other, Position = other.Position, Velocity = other.Velocity, Radius = other.Radius };
                Consider(candidate, threat, ref best, ref bestScore);
            }

            bool same = best.IsMissile ? best.MissileId == aim.MissileId : best.Ship == aim.Ship;
            if (!same) aimAcceleration = Vector3.zero;
            aimVelocity = best.Velocity;
            aim = best;
        }

        private void Consider(in Aim candidate, float threat, ref Aim best, ref float bestScore)
        {
            if (!Solve(candidate, out Vector3 direction, out float time) || time * weapon.MuzzleSpeed > weapon.Range) return;
            if (!InArc(direction, out float wantedYaw, out float wantedPitch)) return;
            if (!IsClear(transform.position, direction, Vector3.Distance(transform.position, candidate.Position))) return;

            float swing = Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(yaw.Angle, wantedYaw)) / yawRate, Mathf.Abs(wantedPitch - pitch.Angle) / pitchRate);
            float score = threat / (0.5f + swing + time);
            // Keeping the current target avoids flicking between two that score alike.
            bool current = candidate.IsMissile ? candidate.MissileId == aim.MissileId : candidate.Ship != null && candidate.Ship == aim.Ship;
            if (current) score *= 1.3f;
            if (score <= bestScore) return;
            best = candidate;
            bestScore = score;
        }

        private bool InArc(Vector3 worldDirection, out float wantedYaw, out float wantedPitch)
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * worldDirection;
            wantedYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            wantedPitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            return (yawLimit >= 180f || Mathf.Abs(wantedYaw) <= yawLimit) && wantedPitch >= minPitch && wantedPitch <= maxPitch;
        }

        private bool IsClear(Vector3 origin, Vector3 direction, float distance)
        {
            var ray = new Ray(origin, direction.normalized);
            foreach (Collider collider in hull)
                if (collider.enabled && collider.Raycast(ray, out _, distance)) return false;
            return true;
        }
    }
}
