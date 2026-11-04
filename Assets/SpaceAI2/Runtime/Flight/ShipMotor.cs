using UnityEngine;

namespace SpaceAI2
{
    [RequireComponent(typeof(Rigidbody))]
    public class ShipMotor : MonoBehaviour
    {
        [SerializeField] private ShipProfile profile;

        private Rigidbody body;
        private ShipInput input;
        private Vector3 localRates;

        public ShipProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public ShipInput Input
        {
            get => input;
            set => input = value.Clamped();
        }

        public Rigidbody Body => body;
        public Vector3 Velocity => body.velocity;
        public float Speed => body.velocity.magnitude;
        public float TargetSpeed => profile.TargetSpeed(input.Throttle, input.Boost);

        public Vector3 NormalizedTurn
        {
            get
            {
                Vector3 rates = Quaternion.Inverse(body.rotation) * body.angularVelocity * Mathf.Rad2Deg;
                return new Vector3(
                    profile.PitchRate > 0f ? -rates.x / profile.PitchRate : 0f,
                    profile.YawRate > 0f ? rates.y / profile.YawRate : 0f,
                    profile.RollRate > 0f ? -rates.z / profile.RollRate : 0f);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.drag = 0f;
            body.angularDrag = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 20f;
        }

        private void OnEnable()
        {
            if (profile != null) body.velocity = transform.forward * profile.CruiseSpeed;
        }

        private void FixedUpdate()
        {
            if (profile == null) return;
            float dt = Time.fixedDeltaTime;
            ApplyRotation(dt);
            ApplyVelocity(dt);
        }

        private void ApplyRotation(float dt)
        {
            float factor = input.Boost ? profile.BoostTurnFactor : 1f;
            Vector3 maxRates = new Vector3(profile.PitchRate, profile.YawRate, profile.RollRate) * (factor * Mathf.Deg2Rad);
            Vector3 target = new Vector3(-input.Pitch * maxRates.x, input.Yaw * maxRates.y, -input.Roll * maxRates.z);

            Quaternion rotation = body.rotation;
            localRates = Quaternion.Inverse(rotation) * body.angularVelocity;
            float step = dt / profile.TurnResponse;
            localRates.x = Mathf.MoveTowards(localRates.x, target.x, maxRates.x * step);
            localRates.y = Mathf.MoveTowards(localRates.y, target.y, maxRates.y * step);
            localRates.z = Mathf.MoveTowards(localRates.z, target.z, maxRates.z * step);
            body.angularVelocity = rotation * localRates;
        }

        private void ApplyVelocity(float dt)
        {
            Vector3 forward = body.rotation * Vector3.forward;
            Vector3 velocity = body.velocity;
            float speed = velocity.magnitude;
            Vector3 direction = speed > 0.01f ? velocity / speed : forward;

            direction = Vector3.Slerp(direction, forward, 1f - Mathf.Exp(-profile.Grip * dt));
            float target = TargetSpeed;
            float rate = target > speed ? profile.Acceleration : profile.Deceleration;
            speed = Mathf.MoveTowards(speed, target, rate * dt);

            body.velocity = direction * speed;
        }
    }
}
