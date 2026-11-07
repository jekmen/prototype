using UnityEngine;

namespace SpaceAI2
{
    public struct ShipInput
    {
        public float Pitch;
        public float Yaw;
        public float Roll;
        public float Throttle;
        public bool Boost;

        public ShipInput Clamped() => new ShipInput
        {
            Pitch = Mathf.Clamp(Pitch, -1f, 1f),
            Yaw = Mathf.Clamp(Yaw, -1f, 1f),
            Roll = Mathf.Clamp(Roll, -1f, 1f),
            Throttle = Mathf.Clamp(Throttle, -1f, 1f),
            Boost = Boost
        };
    }
}
