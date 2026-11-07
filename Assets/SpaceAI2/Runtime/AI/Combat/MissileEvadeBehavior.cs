using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Defeats a guided missile: beams it at full speed so it has to pull lead and bleed energy, then breaks hard
    /// across its flight path just before impact. Proportional navigation needs several times the target's
    /// acceleration, so a well-timed break beats the missile's turn limit; the pilot's timing error decides how often.
    /// </summary>
    public class MissileEvadeBehavior : CombatBehavior
    {
        private const float BreakLead = 1.4f;
        private const float ShipResponse = 0.3f;

        private string phase = "beam";
        private int missileId;
        private float breakAt;
        private Vector3 breakAxis;

        public override string Name => "Evade " + phase;
        public override bool Urgent => true;
        public override float MaxTime => 20f;

        public override float Score(CombatContext c) => c.HasMissile ? 1.6f : 0f;

        public override void Enter(CombatContext c) => missileId = 0;

        public override void Execute(CombatContext c, float dt)
        {
            Transform t = c.Ship.transform;
            if (!c.HasMissile)
            {
                c.Pilot.Direct(t.forward);
                return;
            }

            MissileTrack missile = c.Missile;
            if (missile.Id != missileId)
            {
                missileId = missile.Id;
                MissileProfile profile = missile.Profile;
                // Breaking well before the missile's response time lets it follow; breaking later leaves the ship no time to move.
                float ideal = profile.GuidanceLag * BreakLead + ShipResponse;
                breakAt = Mathf.Max(0.1f, ideal + Random.Range(-1f, 1f) * c.Profile.BreakTimingError);
                breakAxis = Vector3.zero;
            }

            if (c.MissileTimeToGo <= breakAt || breakAxis != Vector3.zero)
            {
                phase = "break";
                if (breakAxis == Vector3.zero) breakAxis = ChooseBreakAxis(t, missile.Velocity);
                // Constant turn axis: the full stick in one plane, which is the tightest the ship can turn.
                Vector3 side = Vector3.Cross(breakAxis, t.forward);
                c.Pilot.Direct(side * 2f + t.forward * 0.2f, 1f);
            }
            else
            {
                phase = "beam";
                Vector3 los = (c.Ship.Position - missile.Position).normalized;
                Vector3 beam = Vector3.ProjectOnPlane(t.forward, los);
                if (beam.sqrMagnitude < 0.01f) beam = Vector3.ProjectOnPlane(t.up, los);
                beam.Normalize();
                c.Pilot.Direct(beam, 1f, Vector3.Angle(t.forward, beam) < 30f);
            }
            c.Pilot.FireTarget = c.Target;
        }

        /// <summary>
        /// The pull direction has to cut across the missile's path, and every degree of roll before the pull is time the
        /// missile gets back. Pulling straight up costs no roll, a quarter roll costs half a second.
        /// </summary>
        private static Vector3 ChooseBreakAxis(Transform t, Vector3 missileVelocity)
        {
            Vector3 path = missileVelocity.normalized;
            Vector3[] pulls = { t.up, t.right, -t.right };
            float[] rollCost = { 1f, 0.6f, 0.6f };
            Vector3 best = t.up;
            float bestScore = float.MinValue;
            for (int i = 0; i < pulls.Length; i++)
            {
                float across = Vector3.ProjectOnPlane(pulls[i], path).magnitude;
                float score = across * rollCost[i];
                if (score <= bestScore) continue;
                best = pulls[i];
                bestScore = score;
            }
            return Vector3.Cross(t.forward, best).normalized;
        }
    }
}
