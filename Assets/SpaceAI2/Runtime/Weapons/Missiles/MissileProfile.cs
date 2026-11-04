using UnityEngine;

namespace SpaceAI2
{
    public enum MissileGuidance { Unguided, Heat, Radar }

    /// <summary>A missile or torpedo: motor, proportional-navigation seeker, warhead and the lock the launcher needs.</summary>
    [CreateAssetMenu(menuName = "SpaceAI2/Missile Profile", fileName = "MissileProfile")]
    public class MissileProfile : ScriptableObject
    {
        public MissileGuidance Guidance = MissileGuidance.Heat;

        [Header("Motor")]
        [Tooltip("Speed added to the launcher's velocity on release, m/s.")]
        [Min(0f)] public float EjectSpeed = 20f;
        [Min(1f)] public float MaxSpeed = 300f;
        [Tooltip("Thrust while the motor burns, m/s².")]
        [Min(0f)] public float Acceleration = 220f;
        [Min(0f)] public float BurnTime = 4f;
        [Tooltip("Speed lost per second once the motor burns out, as a share of speed.")]
        [Min(0f)] public float Drag = 0.25f;
        [Tooltip("Speed lost per m/s² of turning per second, as a share. Hard turns bleed energy.")]
        [Min(0f)] public float TurnDrag = 0.0006f;
        [Min(0.1f)] public float Lifetime = 10f;

        [Header("Guidance")]
        [Tooltip("Flies straight for this long after release before the seeker steers, seconds.")]
        [Min(0f)] public float ArmTime = 0.3f;
        [Tooltip("Proportional navigation constant, usually 3–5.")]
        [Min(0f)] public float Navigation = 4f;
        [Tooltip("Lateral acceleration limit, m/s². Targets pulling more than this divided by the navigation constant can outturn it.")]
        [Min(0f)] public float MaxTurnAcceleration = 300f;
        [Tooltip("Seconds the airframe takes to follow a new steering command. A sudden break inside this time beats the missile.")]
        [Min(0f)] public float GuidanceLag = 0.5f;
        [Tooltip("The seeker loses the target outside this half-angle around the missile's flight path, degrees.")]
        [Range(1f, 180f)] public float SeekerAngle = 45f;
        [Tooltip("Chance a flare inside the seeker cone pulls the missile off. Heat seekers only.")]
        [Range(0f, 1f)] public float FlareSusceptibility = 0.6f;

        [Header("Lock")]
        [Tooltip("Seconds the target has to stay in the lock cone before launch. Unguided needs none.")]
        [Min(0f)] public float LockTime = 0.6f;
        [Tooltip("Half-angle around the launcher's nose the target must stay in while locking, degrees.")]
        [Range(1f, 90f)] public float LockAngle = 15f;
        [Min(1f)] public float LockRange = 1600f;
        [Tooltip("The AI does not launch closer than this, the seeker would not have time to steer.")]
        [Min(0f)] public float MinRange = 200f;
        [Tooltip("The AI keeps this weapon for targets at least this large, meters. Torpedoes are wasted on fighters.")]
        [Min(0f)] public float MinTargetRadius = 0f;

        [Header("Warhead")]
        [Min(0f)] public float Damage = 90f;
        [Min(0f)] public float BlastRadius = 15f;
        [Tooltip("Detonates when the target passes within this distance of its surface, meters.")]
        [Min(0f)] public float ProximityFuse = 6f;
        [Tooltip("Hit points for point defense; zero means it cannot be shot down.")]
        [Min(0f)] public float Hull = 5f;

        [Header("Launch")]
        [Tooltip("Missiles per launch. More than one is a swarm fanned out around the nose.")]
        [Min(1)] public int Salvo = 1;
        [Range(0f, 45f)] public float SalvoSpread = 10f;
        [Tooltip("Seconds between launches from one launcher.")]
        [Min(0f)] public float Reload = 3f;

        [Header("Visual")]
        public Color BodyColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        public Color FlameColor = new Color(1f, 0.6f, 0.25f, 1f);
        [Min(0.1f)] public float Length = 2.5f;
        [Min(0.05f)] public float Width = 0.35f;
        public Color TrailColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        public bool IsGuided => Guidance != MissileGuidance.Unguided;
    }
}
