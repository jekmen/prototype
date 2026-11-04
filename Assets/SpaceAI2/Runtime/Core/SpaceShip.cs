using UnityEngine;

namespace SpaceAI2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipMotor))]
    public class SpaceShip : MonoBehaviour
    {
        [SerializeField] private int team;

        private ShipMotor motor;

        public int Team
        {
            get => team;
            set => team = value;
        }

        public ShipMotor Motor => motor;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Velocity => motor.Velocity;
        public float Radius => motor.Profile != null ? motor.Profile.Radius : 1f;

        public bool IsEnemy(SpaceShip other) => other != null && other.team != team;

        private void Awake() => motor = GetComponent<ShipMotor>();

        private void OnEnable() => SpaceAIWorld.Instance.Registry.Add(this);

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Registry.Remove(this);
        }
    }
}
