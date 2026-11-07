using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Keeps a wingman's slot: flies the leader's velocity plus a correction proportional to the slot error,
    /// boosting to rejoin from far away. Threats still win through Defend; orders decide whether combat wins.
    /// </summary>
    public class FormationBehavior : CombatBehavior
    {
        private const float Gain = 0.5f;

        public override string Name => "Formation";

        public override float Score(CombatContext c)
        {
            Formation formation = c.Brain.Formation;
            if (formation == null || formation.Leader == null) return 0f;
            switch (formation.Order)
            {
                // Holding formation only allows targets that attack the squad, and those must be fought off.
                case SquadOrder.HoldFormation: return c.HasTarget ? 0.4f : 0.85f;
                case SquadOrder.Cover: return c.HasTarget ? 0.1f : 0.85f;
                default: return 0.05f;
            }
        }

        public override void Execute(CombatContext c, float dt)
        {
            Formation formation = c.Brain.Formation;
            if (formation == null || formation.Leader == null)
            {
                c.Pilot.Direct(c.Ship.Forward);
                return;
            }

            ShipProfile ship = c.Ship.Motor.Profile;
            Vector3 leaderVelocity = formation.LeaderVelocity;
            Vector3 error = formation.SlotPosition(c.Brain.Slot) - c.Ship.Position;
            Vector3 desired = leaderVelocity + Vector3.ClampMagnitude(error * Gain, ship.BoostSpeed);

            // Ahead of the slot: keep flying the leader's way at low speed and let the slot catch up instead of turning back.
            Vector3 leaderForward = formation.Leader.Forward;
            float along = Vector3.Dot(desired, leaderForward);
            if (along < ship.MinSpeed) desired += leaderForward * (ship.MinSpeed - along);

            float speed = desired.magnitude;
            float boostDistance = formation.IsBroken(c.Brain) ? formation.Profile.JoinDistance * 2f : formation.Profile.BreakDistance;
            bool boost = speed > ship.MaxSpeed && error.magnitude > boostDistance;
            c.Pilot.Direct(desired, ship.ThrottleFor(speed), boost);
            c.Pilot.FireTarget = c.Target;
        }
    }
}
