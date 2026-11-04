using System;
using UnityEngine;

namespace SpaceAI2
{
    public enum PursuitMode { Lead, Pure, Lag }

    [RequireComponent(typeof(SpaceShip))]
    public class AIPilot : MonoBehaviour
    {
        private enum Order { None, FlyTo, Pursue }

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

        private const float RollFlipZone = 150f * Mathf.Deg2Rad;

        private SpaceShip ship;
        private ShipMotor motor;
        private float rollSign = 1f;
        private Order order;
        private Transform destination;
        private SpaceShip target;
        private PursuitMode pursuitMode;

        public event Action<AIPilot> Arrived;

        public SpaceShip Ship => ship;
        public SpaceShip Target => target;
        public Transform Destination => destination;
        public PursuitMode PursuitMode => pursuitMode;
        public Vector3 AimPoint { get; private set; }

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
            pursuitMode = mode;
            destination = null;
        }

        public void Stop()
        {
            order = Order.None;
            destination = null;
            target = null;
        }

        private void Awake()
        {
            ship = GetComponent<SpaceShip>();
            motor = GetComponent<ShipMotor>();
        }

        private void OnEnable() => SpaceAIWorld.Instance.Add(this);

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Remove(this);
            motor.Input = default;
        }

        internal void Tick(float dt)
        {
            if (motor.Profile == null) return;
            switch (order)
            {
                case Order.FlyTo when destination != null:
                    motor.Input = FlyToDestination();
                    break;
                case Order.Pursue when target != null && target.isActiveAndEnabled:
                    motor.Input = PursueTarget();
                    break;
                default:
                    order = Order.None;
                    AimPoint = transform.position + transform.forward * 100f;
                    motor.Input = Steer(transform.forward);
                    break;
            }
        }

        private ShipInput FlyToDestination()
        {
            AimPoint = destination.position;
            Vector3 offset = AimPoint - transform.position;
            float distance = offset.magnitude;

            ShipInput input = Steer(offset);
            input.Throttle = Mathf.Lerp(-0.5f, 1f, distance / slowdownDistance);

            float turnRadius = motor.Profile.TurnRadius(motor.Speed);
            if (Vector3.Angle(transform.forward, offset) > 60f && distance < turnRadius * 2.5f) input.Throttle = -1f;

            if (distance < Mathf.Max(arrivalRadius, ship.Radius * 2f))
            {
                Transform reached = destination;
                Arrived?.Invoke(this);
                if (destination == reached) order = Order.None;
            }
            return input;
        }

        private ShipInput PursueTarget()
        {
            Vector3 toTarget = target.Position - transform.position;
            float distance = toTarget.magnitude;
            AimPoint = target.Position + AimOffset(distance);

            ShipInput input = Steer(AimPoint - transform.position);
            float closing = distance > 0.01f ? Vector3.Dot(target.Velocity - motor.Velocity, toTarget / distance) : 0f;
            input.Throttle = (distance - pursuitRange) / rangeTolerance + closing / 20f;
            input.Boost = distance > boostDistance && Vector3.Angle(transform.forward, toTarget) < 20f;
            return input;
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

            ShipProfile profile = motor.Profile;
            Vector3 turn = motor.NormalizedTurn;
            Vector3 rates = new Vector3(turn.x * profile.PitchRate, turn.y * profile.YawRate, turn.z * profile.RollRate) * Mathf.Deg2Rad;
            float lead = profile.TurnResponse * anticipation;

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
            if (!Application.isPlaying || order == Order.None) return;
            Gizmos.color = order == Order.Pursue ? Color.red : Color.cyan;
            Gizmos.DrawLine(transform.position, AimPoint);
            Gizmos.DrawWireSphere(AimPoint, 3f);
        }
    }
}
