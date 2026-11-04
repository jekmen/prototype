using System;
using UnityEngine;

namespace SpaceAI2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipMotor))]
    public class SpaceShip : MonoBehaviour
    {
        [SerializeField] private int team;

        private ShipMotor motor;

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

        public bool IsEnemy(SpaceShip other) => other != null && other.team != team;

        public void ReceiveHit(in HitInfo hit) => Hit?.Invoke(hit);

        private void Awake() => motor = GetComponent<ShipMotor>();

        private void OnEnable() => SpaceAIWorld.Instance.Registry.Add(this);

        private void OnDisable()
        {
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world)) world.Registry.Remove(this);
        }

        private void OnCollisionEnter(Collision collision) => Collided?.Invoke(this, collision);
    }
}
