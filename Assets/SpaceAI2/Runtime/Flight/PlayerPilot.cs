using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    [RequireComponent(typeof(ShipMotor))]
    public class PlayerPilot : MonoBehaviour
    {
        public enum MouseHorizontal { Yaw, Roll }

        [Header("Mouse virtual stick")]
        [SerializeField] private MouseHorizontal mouseHorizontal = MouseHorizontal.Yaw;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.04f;
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.05f;
        [SerializeField] private bool invertPitch;
        [Tooltip("Stick recentering speed, 1/s. Zero keeps the stick where the mouse left it.")]
        [SerializeField, Min(0f)] private float recenter = 0f;

        [Header("Keys")]
        [SerializeField] private KeyCode throttleUp = KeyCode.W;
        [SerializeField] private KeyCode throttleDown = KeyCode.S;
        [SerializeField] private KeyCode yawLeft = KeyCode.A;
        [SerializeField] private KeyCode yawRight = KeyCode.D;
        [SerializeField] private KeyCode rollLeft = KeyCode.Q;
        [SerializeField] private KeyCode rollRight = KeyCode.E;
        [SerializeField] private KeyCode boost = KeyCode.LeftShift;
        [SerializeField] private KeyCode centerStick = KeyCode.Space;
        [SerializeField] private KeyCode fire = KeyCode.Mouse0;
        [SerializeField] private KeyCode launchMissile = KeyCode.Mouse1;

        [Header("Missile lock")]
        [Tooltip("Enemies within this many lock angles of the nose become the seeker's target.")]
        [SerializeField, Min(1f)] private float targetCone = 2f;

        private ShipMotor motor;
        private Vector2 stick;
        private SpaceShip ship;
        private ShipGun gun;
        private MissileLauncher launcher;
        private float nextTargetSearch;
        private readonly List<SpaceShip> nearby = new List<SpaceShip>();

        public Vector2 Stick => stick;
        public float DeadZone => deadZone;

        public MissileLauncher Launcher => launcher;
        public ShipGun Gun => gun;
        public SpaceShip Ship => ship;

        private void Awake()
        {
            motor = GetComponent<ShipMotor>();
            ship = GetComponent<SpaceShip>();
        }

        private void Start()
        {
            gun = GetComponent<ShipGun>();
            launcher = GetComponent<MissileLauncher>();
        }

        private void OnEnable() => LockCursor(true);

        private void OnDisable()
        {
            LockCursor(false);
            motor.Input = default;
            if (gun != null) gun.Trigger = false;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (UnityEngine.Input.GetMouseButtonDown(0)) LockCursor(true);

            UpdateStick();
            Vector2 axes = ApplyDeadZone(stick);

            float pitch = invertPitch ? -axes.y : axes.y;
            float yaw = Axis(yawLeft, yawRight);
            float roll = Axis(rollLeft, rollRight);
            if (mouseHorizontal == MouseHorizontal.Yaw) yaw += axes.x;
            else roll += axes.x;

            motor.Input = new ShipInput
            {
                Pitch = pitch,
                Yaw = yaw,
                Roll = roll,
                Throttle = Axis(throttleDown, throttleUp),
                Boost = UnityEngine.Input.GetKey(boost)
            };

            UpdateWeapons();
        }

        private void UpdateWeapons()
        {
            bool armed = Cursor.lockState == CursorLockMode.Locked;
            if (gun != null) gun.Trigger = armed && UnityEngine.Input.GetKey(fire);
            if (launcher == null || launcher.Missile == null) return;

            if (Time.time >= nextTargetSearch)
            {
                nextTargetSearch = Time.time + 0.2f;
                launcher.LockTarget = FindLockTarget(launcher.Missile);
            }
            if (armed && UnityEngine.Input.GetKeyDown(launchMissile)) launcher.Launch();
        }

        private SpaceShip FindLockTarget(MissileProfile missile)
        {
            if (!missile.IsGuided || ship == null || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return null;
            float cone = missile.LockAngle * targetCone;
            Vector3 position = ship.Position;
            Vector3 forward = ship.Forward;

            SpaceShip current = launcher.LockTarget;
            if (IsCandidate(current, missile) && Vector3.Angle(forward, current.Position - position) <= cone) return current;

            world.Registry.Query(position, missile.LockRange, nearby);
            SpaceShip best = null;
            float bestAngle = cone;
            foreach (SpaceShip other in nearby)
            {
                if (!IsCandidate(other, missile)) continue;
                float angle = Vector3.Angle(forward, other.Position - position);
                if (angle > bestAngle) continue;
                bestAngle = angle;
                best = other;
            }
            return best;
        }

        private bool IsCandidate(SpaceShip other, MissileProfile missile) =>
            other != null && other.isActiveAndEnabled && ship.IsEnemy(other) && other.Radius >= missile.MinTargetRadius &&
            (other.Position - ship.Position).sqrMagnitude <= missile.LockRange * missile.LockRange;

        private void UpdateStick()
        {
            if (UnityEngine.Input.GetKeyDown(centerStick)) stick = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                stick.x += UnityEngine.Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                stick.y += UnityEngine.Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            }
            if (recenter > 0f) stick = Vector2.Lerp(stick, Vector2.zero, 1f - Mathf.Exp(-recenter * Time.deltaTime));
            stick = Vector2.ClampMagnitude(stick, 1f);
        }

        private Vector2 ApplyDeadZone(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= deadZone) return Vector2.zero;
            return value / magnitude * ((magnitude - deadZone) / (1f - deadZone));
        }

        private static float Axis(KeyCode negative, KeyCode positive) =>
            (UnityEngine.Input.GetKey(positive) ? 1f : 0f) - (UnityEngine.Input.GetKey(negative) ? 1f : 0f);

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
