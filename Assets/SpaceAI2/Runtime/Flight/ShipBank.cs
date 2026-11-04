using UnityEngine;

namespace SpaceAI2
{
    [RequireComponent(typeof(ShipMotor))]
    public class ShipBank : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Range(0f, 90f)] private float maxBank = 50f;
        [SerializeField, Range(0f, 30f)] private float maxPitchLean = 6f;
        [SerializeField, Min(0.01f)] private float sharpness = 6f;

        private ShipMotor motor;
        private Quaternion baseRotation;

        public Transform Visual
        {
            get => visual;
            set => visual = value;
        }

        private void Awake() => motor = GetComponent<ShipMotor>();

        private void Start()
        {
            if (visual != null) baseRotation = visual.localRotation;
        }

        private void LateUpdate()
        {
            if (visual == null || motor.Profile == null) return;

            Vector3 turn = motor.NormalizedTurn;
            Quaternion lean = Quaternion.Euler(-turn.x * maxPitchLean, 0f, -turn.y * maxBank);
            Quaternion target = lean * baseRotation;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }
    }
}
