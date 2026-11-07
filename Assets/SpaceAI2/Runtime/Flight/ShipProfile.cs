using UnityEngine;

namespace SpaceAI2
{
    [CreateAssetMenu(menuName = "SpaceAI2/Ship Profile", fileName = "ShipProfile")]
    public class ShipProfile : ScriptableObject
    {
        [Header("Speed, m/s")]
        [Min(0f)] public float MinSpeed = 20f;
        [Min(0f)] public float CruiseSpeed = 60f;
        [Min(0f)] public float MaxSpeed = 90f;
        [Min(0f)] public float BoostSpeed = 140f;

        [Header("Acceleration, m/s²")]
        [Min(0.01f)] public float Acceleration = 40f;
        [Min(0.01f)] public float Deceleration = 60f;

        [Header("Turn rates, °/s")]
        [Min(0f)] public float PitchRate = 90f;
        [Min(0f)] public float YawRate = 75f;
        [Min(0f)] public float RollRate = 180f;
        [Tooltip("Seconds to reach full turn rate from rest.")]
        [Min(0.01f)] public float TurnResponse = 0.25f;
        [Tooltip("Turn rate multiplier while boosting.")]
        [Range(0.1f, 1f)] public float BoostTurnFactor = 0.6f;

        [Header("Handling")]
        [Tooltip("How fast velocity follows the nose, 1/s. Lower values drift more.")]
        [Min(0.01f)] public float Grip = 3f;

        [Header("Durability")]
        [Min(1f)] public float Hull = 100f;
        [Min(0f)] public float Shield = 50f;
        [Min(0.1f)] public float Radius = 4f;

        public float TurnRadius(float speed) => speed / (PitchRate * Mathf.Deg2Rad);
        public float HalfTurnTime => PitchRate > 0f ? 180f / PitchRate : float.PositiveInfinity;

        public float TargetSpeed(float throttle, bool boost)
        {
            if (boost) return BoostSpeed;
            return throttle >= 0f
                ? Mathf.Lerp(CruiseSpeed, MaxSpeed, throttle)
                : Mathf.Lerp(CruiseSpeed, MinSpeed, -throttle);
        }

        /// <summary>Throttle at which <see cref="TargetSpeed"/> returns this speed without boost.</summary>
        public float ThrottleFor(float speed)
        {
            if (speed >= CruiseSpeed) return MaxSpeed > CruiseSpeed ? Mathf.Clamp01((speed - CruiseSpeed) / (MaxSpeed - CruiseSpeed)) : 0f;
            return CruiseSpeed > MinSpeed ? -Mathf.Clamp01((CruiseSpeed - speed) / (CruiseSpeed - MinSpeed)) : 0f;
        }

        private void OnValidate()
        {
            CruiseSpeed = Mathf.Max(CruiseSpeed, MinSpeed);
            MaxSpeed = Mathf.Max(MaxSpeed, CruiseSpeed);
            BoostSpeed = Mathf.Max(BoostSpeed, MaxSpeed);
        }
    }
}
