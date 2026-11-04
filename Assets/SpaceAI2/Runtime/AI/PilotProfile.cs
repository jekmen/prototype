using UnityEngine;

namespace SpaceAI2
{
    [CreateAssetMenu(menuName = "SpaceAI2/Pilot Profile", fileName = "PilotProfile")]
    public class PilotProfile : ScriptableObject
    {
        [Header("Gunnery")]
        [Tooltip("Slow random aim wobble, degrees.")]
        [Min(0f)] public float AimError = 0.3f;
        [Tooltip("Fire when the predicted miss is within this many target radii.")]
        [Min(0f)] public float FireTolerance = 1f;
        [Tooltip("Extra cone around the firing solution the pilot still shoots into, degrees. Bursts and visible misses.")]
        [Range(0f, 15f)] public float SprayAngle = 3f;

        [Header("Flying")]
        [Tooltip("Share of the stick the pilot dares to use. Lower means wider turns, so better pilots win turn fights.")]
        [Range(0.3f, 1f)] public float TurnSkill = 0.85f;

        [Header("Decisions")]
        [Tooltip("Seconds between behavior decisions.")]
        [Min(0.02f)] public float ReactionTime = 0.3f;
        [Tooltip("Bonus to the current behavior's score, so close calls do not flip every decision.")]
        [Min(0f)] public float Hysteresis = 0.15f;
        [Tooltip("Seconds a behavior is kept before a non-urgent one may replace it.")]
        [Min(0f)] public float MinCommitTime = 1.5f;

        [Header("Missiles")]
        [Tooltip("Seconds after a launch before the pilot notices the missile.")]
        [Min(0f)] public float MissileWarningDelay = 0.8f;
        [Tooltip("Random error of the last-moment break against a missile, seconds either way. Early or late breaks let it follow.")]
        [Min(0f)] public float BreakTimingError = 0.5f;

        [Header("Temperament")]
        [Range(0f, 1f)] public float Aggression = 0.5f;
        [Tooltip("Hull share below which the pilot disengages.")]
        [Range(0f, 1f)] public float Caution = 0.3f;
        [Tooltip("Seconds of pursuit without a firing chance before the pilot breaks off to reset the fight.")]
        [Min(1f)] public float Patience = 12f;
    }
}
