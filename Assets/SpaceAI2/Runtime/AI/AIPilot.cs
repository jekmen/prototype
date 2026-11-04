using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    public enum PursuitMode { Lead, Pure, Lag }

    /// <summary>
    /// Flies the ship through the same stick input as a player. Commands set an intent (direction, throttle, boost);
    /// traffic separation and obstacle avoidance are layered on top, and the gun tracks its own fire target.
    /// An optional <see cref="IPilotBrain"/> on the same object issues the commands every tick.
    /// </summary>
    [RequireComponent(typeof(SpaceShip))]
    public class AIPilot : MonoBehaviour
    {
        private enum Order { None, FlyTo, Pursue, Direct }

        private struct Intent
        {
            public Vector3 Direction;
            public float Throttle;
            public bool Boost;
        }

        [Tooltip("Skill and temperament. Without one the gunnery fields below are used.")]
        [SerializeField] private PilotProfile profile;

        [Header("Steering")]
        [Tooltip("Stick deflection per radian of error.")]
        [SerializeField, Min(0.1f)] private float steerGain = 2.5f;
        [Tooltip("How far ahead the pilot anticipates its own rotation, in multiples of the ship's turn response.")]
        [SerializeField, Min(0f)] private float anticipation = 0.4f;
        [Tooltip("Angle off the nose where the pilot starts rolling into the turn.")]
        [SerializeField, Range(0f, 90f)] private float bankTurnStart = 10f;
        [Tooltip("Angle off the nose where the pilot turns purely by roll and pull.")]
        [SerializeField, Range(0f, 180f)] private float bankTurnFull = 40f;
        [SerializeField, Min(0f)] private float levelGain = 0.6f;

        [Header("Navigation")]
        [SerializeField, Min(1f)] private float arrivalRadius = 60f;
        [SerializeField, Min(1f)] private float slowdownDistance = 300f;

        [Header("Pursuit")]
        [SerializeField, Min(0f)] private float pursuitRange = 150f;
        [SerializeField, Min(1f)] private float rangeTolerance = 150f;
        [SerializeField, Min(0f)] private float boostDistance = 1000f;
        [SerializeField, Min(0f)] private float maxLeadTime = 2f;
        [SerializeField, Min(0f)] private float lagTime = 0.6f;

        [Header("Traffic")]
        [Tooltip("Steer clear of ships whose closest approach is less than this many seconds away.")]
        [SerializeField, Min(0f)] private float trafficTime = 1.5f;
        [Tooltip("Desired miss distance from other ships, in multiples of the combined radii.")]
        [SerializeField, Min(1f)] private float trafficClearance = 3f;

        [Header("Obstacle avoidance")]
        [SerializeField] private bool avoidObstacles = true;
        [Tooltip("Seconds of flight the forward probe covers, on top of the turn radius.")]
        [SerializeField, Min(0.1f)] private float lookAheadTime = 2.5f;
        [Tooltip("Probe radius in multiples of the ship radius.")]
        [SerializeField, Min(1f)] private float clearance = 1.5f;
        [Tooltip("Below this time to impact the pilot brakes hard instead of choosing a stylish maneuver.")]
        [SerializeField, Min(0.05f)] private float emergencyTime = 0.8f;
        [SerializeField, Range(0f, 1f)] private float maneuverRandomness = 0.3f;
        [SerializeField, Min(0f)] private float minManeuverTime = 0.5f;
        [SerializeField, Min(0.1f)] private float maxManeuverTime = 4f;

        [Header("Gunnery")]
        [Tooltip("Slow random aim wobble, degrees. Overridden by the pilot profile.")]
        [SerializeField, Min(0f)] private float aimError = 0.3f;
        [Tooltip("Fire when the predicted miss is within this many target radii. Overridden by the pilot profile.")]
        [SerializeField, Min(0f)] private float fireTolerance = 1f;
        [Tooltip("Open fire within this share of the weapon range.")]
        [SerializeField, Range(0.1f, 1f)] private float fireRange = 0.9f;
        [SerializeField] private bool checkFriendlyFire = true;

        private const float RollFlipZone = 150f * Mathf.Deg2Rad;
        private const float EscapeAlignedAngle = 15f;

        private readonly AvoidanceManeuver avoidance = new AvoidanceManeuver();
        private readonly List<SpaceShip> scratch = new List<SpaceShip>();
        private ObstacleReading lastReading;

        private SpaceShip ship;
        private ShipMotor motor;
        private ShipGun gun;
        private IPilotBrain brain;
        private float noiseSeed;
        private float rollSign = 1f;
        private Order order;
        private Transform destination;
        private SpaceShip target;
        private SpaceShip fireTarget;
        private PursuitMode pursuitMode;
        private Intent direct;
        private Vector3 solutionDirection;
        private float solutionTime;

        public event Action<AIPilot> Arrived;
        public event Action<AIPilot, ManeuverKind, ProbeSide> ManeuverStarted;

        public ManeuverKind Maneuver => avoidance.Kind;
        public ProbeSide ManeuverSide => avoidance.Side;
        public SpaceShip Ship => ship;
        public ShipGun Gun => gun;
        public SpaceShip Target => target;
        public Transform Destination => destination;
        public PursuitMode PursuitMode => pursuitMode;
        public Vector3 AimPoint { get; private set; }

        /// <summary>Ship the gun shoots at whenever a firing solution lines up, whatever the pilot is doing.</summary>
        public SpaceShip FireTarget
        {
            get => fireTarget;
            set => fireTarget = value;
        }

        public bool HasFiringSolution { get; private set; }
        public Vector3 FiringDirection => solutionDirection;

        /// <summary>Angle between the nose and the firing solution, degrees; negative when there is none.</summary>
        public float GunAimAngle { get; private set; } = -1f;

        public PilotProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public float AimError
        {
            get => profile != null ? profile.AimError : aimError;
            set => aimError = Mathf.Max(0f, value);
        }

        public float FireTolerance => profile != null ? profile.FireTolerance : fireTolerance;
        public float SprayAngle => profile != null ? profile.SprayAngle : 0f;

        /// <summary>Shrinks the open-fire range, e.g. to hold fire in a joust until the pass. Set per tick by a brain.</summary>
        public float FireRangeScale { get; set; } = 1f;

        /// <summary>Extra aim wobble in degrees on top of the profile, e.g. for hard shots like a joust. Set per tick by a brain.</summary>
        public float ExtraAimError { get; set; }

        public float PursuitRange
        {
            get => pursuitRange;
            set => pursuitRange = Mathf.Max(0f, value);
        }

        public void FlyTo(Transform point)
        {
            order = point != null ? Order.FlyTo : Order.None;
            destination = point;
            target = null;
        }

        public void Pursue(SpaceShip targetShip, PursuitMode mode = PursuitMode.Lead)
        {
            order = targetShip != null ? Order.Pursue : Order.None;
            target = targetShip;
            fireTarget = targetShip;
            pursuitMode = mode;
            destination = null;
        }

        /// <summary>Fly along a world direction. Meant to be refreshed every tick by a brain.</summary>
        public void Direct(Vector3 direction, float throttle = 0f, bool boost = false)
        {
            order = Order.Direct;
            direct = new Intent { Direction = direction, Throttle = throttle, Boost = boost };
            destination = null;
            target = null;
        }

        public void Stop()
        {
            order = Order.None;
            destination = null;
            target = null;
            fireTarget = null;
        }

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            motor = GetComponent<ShipMotor>();
            gun = GetComponent<ShipGun>();
            brain = GetComponent<IPilotBrain>();
            noiseSeed = UnityEngine.Random.value * 100f;
        }

        private void OnEnable() => SpaceAIWorld.Instance.Add(this);

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Remove(this);
            motor.Input = default;
            if (gun != null) gun.Trigger = false;
        }

        internal void Tick(float dt)
        {
            if (motor.Profile == null) return;
            brain?.Think(this, dt);
            UpdateFiringSolution();

            Intent intent;
            switch (order)
            {
                case Order.FlyTo when destination != null:
                    intent = FlyToDestination();
                    break;
                case Order.Pursue when IsValid(target):
                    intent = PursueTarget();
                    break;
                case Order.Direct:
                    intent = direct;
                    if (intent.Direction.sqrMagnitude < 0.0001f) intent.Direction = transform.forward;
                    AimPoint = transform.position + intent.Direction.normalized * 100f;
                    break;
                default:
                    order = Order.None;
                    intent = new Intent { Direction = transform.forward };
                    AimPoint = transform.position + transform.forward * 100f;
                    break;
            }

            ShipInput input = Steer(AvoidTraffic(intent.Direction));
            input.Throttle = intent.Throttle;
            input.Boost = intent.Boost;
            if (profile != null && profile.TurnSkill < 1f)
            {
                float limit = profile.TurnSkill;
                input.Pitch = Mathf.Clamp(input.Pitch, -limit, limit);
                input.Yaw = Mathf.Clamp(input.Yaw, -limit, limit);
                input.Roll = Mathf.Clamp(input.Roll, -limit, limit);
            }

            if (avoidance.Active)
            {
                avoidance.Elapsed += dt;
                bool done = avoidance.Elapsed >= minManeuverTime && avoidance.ClearReadings >= 2;
                if (done || avoidance.Elapsed >= maxManeuverTime || !avoidObstacles) avoidance.End();
                else input = avoidance.Shape(Steer(avoidance.Escape));
            }

            UpdateTrigger();
            motor.Input = input;
        }

        internal bool TryPrepareProbes(out ProbeSetup setup)
        {
            setup = default;
            if (!avoidObstacles || motor.Profile == null) return false;

            float speed = motor.Speed;
            Vector3 forward = speed > 1f ? motor.Velocity / speed : transform.forward;
            Vector3 up = Vector3.ProjectOnPlane(transform.up, forward);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(-transform.forward, forward);
            up.Normalize();

            setup = new ProbeSetup
            {
                Origin = transform.position,
                Forward = forward,
                Up = up,
                Right = Vector3.Cross(up, forward),
                Radius = ship.Radius * clearance,
                Range = speed * lookAheadTime + motor.Profile.TurnRadius(speed) * 1.5f
            };
            return true;
        }

        internal void ReceiveReading(in ObstacleReading reading)
        {
            lastReading = reading;
            float speed = motor.Speed;

            if (!avoidance.Active)
            {
                if (reading.Blocked) StartManeuver(reading, speed);
                return;
            }

            if (!reading.Blocked)
            {
                avoidance.ClearReadings++;
                return;
            }

            avoidance.ClearReadings = 0;
            bool urgent = reading.Distance / Mathf.Max(speed, 1f) < emergencyTime;
            bool escalate = urgent && avoidance.Kind != ManeuverKind.EmergencyBrake && avoidance.Kind != ManeuverKind.Reverse;
            bool exhausted = Vector3.Angle(reading.Probe.Forward, avoidance.Escape) < EscapeAlignedAngle;
            if (escalate || exhausted) StartManeuver(reading, speed);
        }

        private void StartManeuver(in ObstacleReading reading, float speed)
        {
            avoidance.Choose(reading, speed, emergencyTime, maneuverRandomness);
            ManeuverStarted?.Invoke(this, avoidance.Kind, avoidance.Side);
        }

        private static bool IsValid(SpaceShip other) => other != null && other.isActiveAndEnabled;

        private Intent FlyToDestination()
        {
            AimPoint = destination.position;
            Vector3 offset = AimPoint - transform.position;
            float distance = offset.magnitude;

            var intent = new Intent { Direction = offset, Throttle = Mathf.Lerp(-0.5f, 1f, distance / slowdownDistance) };
            float turnRadius = motor.Profile.TurnRadius(motor.Speed);
            if (Vector3.Angle(transform.forward, offset) > 60f && distance < turnRadius * 2.5f) intent.Throttle = -1f;

            if (distance < Mathf.Max(arrivalRadius, ship.Radius * 2f))
            {
                Transform reached = destination;
                Arrived?.Invoke(this);
                if (destination == reached) order = Order.None;
            }
            return intent;
        }

        private Intent PursueTarget()
        {
            Vector3 toTarget = target.Position - transform.position;
            float distance = toTarget.magnitude;
            AimPoint = HasFiringSolution && fireTarget == target
                ? transform.position + solutionDirection * distance
                : target.Position + AimOffset(distance);

            float closing = distance > 0.01f ? Vector3.Dot(target.Velocity - motor.Velocity, toTarget / distance) : 0f;
            return new Intent
            {
                Direction = AimPoint - transform.position,
                Throttle = (distance - pursuitRange) / rangeTolerance + closing / 20f,
                Boost = distance > boostDistance && Vector3.Angle(transform.forward, toTarget) < 20f
            };
        }

        private Vector3 AimOffset(float distance)
        {
            Vector3 velocity = target.Velocity;
            switch (pursuitMode)
            {
                case PursuitMode.Lead:
                    float time = Mathf.Min(distance / Mathf.Max(motor.Speed, 1f), maxLeadTime);
                    return velocity * time;
                case PursuitMode.Lag:
                    return -velocity * lagTime;
                default:
                    return Vector3.zero;
            }
        }

        private Vector3 AvoidTraffic(Vector3 direction)
        {
            if (trafficTime <= 0f || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return direction;
            world.Registry.Query(transform.position, motor.Speed * trafficTime * 2f + 50f, scratch);

            Vector3 push = Vector3.zero;
            foreach (SpaceShip other in scratch)
            {
                if (other == ship) continue;
                Vector3 offset = other.Position - transform.position;
                Vector3 relative = other.Velocity - motor.Velocity;
                float relativeSq = relative.sqrMagnitude;
                if (relativeSq < 1f) continue;

                float time = -Vector3.Dot(offset, relative) / relativeSq;
                if (time < 0f || time > trafficTime) continue;
                Vector3 miss = offset + relative * time;
                float safe = (ship.Radius + other.Radius) * trafficClearance;
                float missDistance = miss.magnitude;
                if (missDistance >= safe) continue;

                Vector3 away = missDistance > 0.01f ? -miss / missDistance : transform.up;
                push += away * ((1f - missDistance / safe) * (1f - time / trafficTime));
            }

            if (push.sqrMagnitude < 0.0001f) return direction;
            return direction.normalized + push * 2f;
        }

        private void UpdateFiringSolution()
        {
            HasFiringSolution = false;
            GunAimAngle = -1f;
            WeaponProfile weapon = gun != null ? gun.Weapon : null;
            if (weapon == null) return;
            gun.AimDirection = Vector3.zero;
            if (!IsValid(fireTarget) || !ship.IsEnemy(fireTarget)) return;

            Vector3 toTarget = fireTarget.Position - transform.position;
            float distance = toTarget.magnitude;
            if (distance > weapon.Range * 1.5f) return;

            Vector3 direction;
            float time;
            if (weapon.IsInstant)
            {
                direction = toTarget / Mathf.Max(distance, 0.001f);
                time = 0f;
            }
            else if (!Ballistics.Intercept(toTarget, fireTarget.Velocity - motor.Velocity, fireTarget.Motor.Acceleration,
                         weapon.MuzzleSpeed, out direction, out time))
            {
                return;
            }

            solutionDirection = Wobble(direction);
            solutionTime = time;
            HasFiringSolution = true;
            GunAimAngle = Vector3.Angle(transform.forward, solutionDirection);
            gun.AimDirection = solutionDirection;
            gun.FuseTime = weapon.Kind == WeaponKind.Flak ? time : 0f;
        }

        private void UpdateTrigger()
        {
            if (gun == null) return;
            gun.Trigger = false;
            if (!HasFiringSolution) return;

            WeaponProfile weapon = gun.Weapon;
            float distance = Vector3.Distance(fireTarget.Position, transform.position);
            float travel = weapon.IsInstant ? distance : weapon.MuzzleSpeed * solutionTime;
            if (travel > weapon.Range * fireRange * FireRangeScale) return;

            Vector3 fireDirection = gun.FireDirection;
            float angle = Vector3.Angle(fireDirection, solutionDirection);
            float miss = angle < 90f ? Mathf.Sin(angle * Mathf.Deg2Rad) * travel : float.MaxValue;
            float tolerance = fireTarget.Radius * FireTolerance + travel * Mathf.Tan(SprayAngle * Mathf.Deg2Rad) +
                              (weapon.Kind == WeaponKind.Flak ? weapon.BlastRadius * 0.5f : 0f);
            if (miss > tolerance) return;
            if (checkFriendlyFire && AllyInLineOfFire(fireDirection, travel)) return;
            if (ObstacleInLineOfFire(fireDirection, travel - fireTarget.Radius * 2f)) return;
            gun.Trigger = true;
        }

        private bool ObstacleInLineOfFire(Vector3 direction, float range)
        {
            if (range <= 0f || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return false;
            // Ships have rigidbodies, static rocks do not.
            return Physics.Raycast(transform.position, direction, out RaycastHit hit, range, world.ObstacleMask, QueryTriggerInteraction.Ignore) &&
                   hit.rigidbody == null;
        }

        private bool AllyInLineOfFire(Vector3 direction, float range)
        {
            if (!SpaceAIWorld.TryGet(out SpaceAIWorld world)) return false;
            Vector3 origin = transform.position;
            world.Registry.Query(origin + direction * (range * 0.5f), range * 0.5f + 20f, scratch);
            foreach (SpaceShip other in scratch)
            {
                if (other == ship || ship.IsEnemy(other)) continue;
                Vector3 offset = other.Position - origin;
                float along = Vector3.Dot(offset, direction);
                if (along < 0f || along > range) continue;
                if ((offset - direction * along).sqrMagnitude < other.Radius * other.Radius * 4f) return true;
            }
            return false;
        }

        private Vector3 Wobble(Vector3 direction)
        {
            float error = AimError + ExtraAimError;
            if (error <= 0f) return direction;
            float t = Time.time * 0.8f;
            float yaw = (Mathf.PerlinNoise(noiseSeed, t) * 2f - 1f) * error;
            float pitch = (Mathf.PerlinNoise(t, noiseSeed) * 2f - 1f) * error;
            return Quaternion.AngleAxis(yaw, transform.up) * Quaternion.AngleAxis(pitch, transform.right) * direction;
        }

        private ShipInput Steer(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            Vector3 local = transform.InverseTransformDirection(direction.normalized);
            float angleOff = Mathf.Acos(Mathf.Clamp(local.z, -1f, 1f));
            float bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bankTurnStart, bankTurnFull, angleOff * Mathf.Rad2Deg));

            float pitchError = Mathf.Atan2(local.y, local.z);
            float yawError = Mathf.Atan2(local.x, local.z);
            float rollError = StableRollError(Mathf.Atan2(local.x, local.y));
            float pull = Mathf.Clamp01(Mathf.Cos(rollError)) * angleOff;

            ShipProfile shipProfile = motor.Profile;
            Vector3 turn = motor.NormalizedTurn;
            Vector3 rates = new Vector3(turn.x * shipProfile.PitchRate, turn.y * shipProfile.YawRate, turn.z * shipProfile.RollRate) * Mathf.Deg2Rad;
            float lead = shipProfile.TurnResponse * anticipation;

            return new ShipInput
            {
                Pitch = steerGain * (Mathf.Lerp(pitchError, pull, bank) - rates.x * lead),
                Yaw = steerGain * (yawError * (1f - bank) - rates.y * lead),
                Roll = Mathf.Lerp(levelGain * RollToLevel(), steerGain * rollError, bank) - steerGain * rates.z * lead
            };
        }

        private float StableRollError(float rollError)
        {
            if (Mathf.Abs(rollError) > RollFlipZone) return rollSign * Mathf.Abs(rollError);
            rollSign = rollError >= 0f ? 1f : -1f;
            return rollError;
        }

        private float RollToLevel() =>
            Mathf.Atan2(Vector3.Dot(transform.right, Vector3.up), Vector3.Dot(transform.up, Vector3.up));

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (order != Order.None)
            {
                Gizmos.color = order == Order.Pursue ? Color.red : Color.cyan;
                Gizmos.DrawLine(transform.position, AimPoint);
                Gizmos.DrawWireSphere(AimPoint, 3f);
            }

            ProbeSetup probe = lastReading.Probe;
            if (probe.Range <= 0f) return;
            Gizmos.color = lastReading.Blocked ? Color.yellow : Color.green;
            Gizmos.DrawLine(probe.Origin, probe.Origin + probe.Forward * lastReading.Distance);
            if (avoidance.Active)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, transform.position + avoidance.Escape * 60f);
            }
        }
    }
}
