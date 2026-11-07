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

        private ShipMotor motor;
        private Vector2 stick;

        public Vector2 Stick => stick;
        public float DeadZone => deadZone;

        private void Awake() => motor = GetComponent<ShipMotor>();

        private void OnEnable() => LockCursor(true);

        private void OnDisable()
        {
            LockCursor(false);
            motor.Input = default;
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
        }

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
