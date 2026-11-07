using UnityEngine;

namespace SpaceAI2
{
    /// <summary>Carries out the brain's <see cref="ShipOrder"/> while there is nothing to fight.</summary>
    public class OrderBehavior : CombatBehavior
    {
        private const float EscortGain = 0.5f;
        private const float SlowdownDistance = 600f;

        private string name = "Order";

        public override string Name => name;

        public override float Score(CombatContext c)
        {
            if (c.Brain.Order == ShipOrder.Patrol) return 0f;
            // Just above patrol, so a target or a threat takes over and the order resumes after.
            return c.HasTarget ? 0.1f : 0.35f;
        }

        public override void Execute(CombatContext c, float dt)
        {
            CombatBrain brain = c.Brain;
            Vector3 offset = brain.OrderPoint - c.Ship.Position;
            switch (brain.Order)
            {
                case ShipOrder.FollowRoute:
                    name = "Route " + brain.Waypoint;
                    c.Pilot.Direct(offset);
                    break;
                case ShipOrder.MoveTo:
                    name = "Move";
                    c.Pilot.Direct(offset, Mathf.Lerp(-1f, 0f, offset.magnitude / SlowdownDistance));
                    break;
                case ShipOrder.Escort:
                    name = "Escort";
                    Escort(c, brain.EscortTarget, offset);
                    break;
                default:
                    c.Pilot.Direct(c.Ship.Forward);
                    break;
            }
            c.Pilot.FireTarget = c.Target;
        }

        // Matches the charge's velocity and closes the station error, like a formation slot.
        private static void Escort(CombatContext c, SpaceShip charge, Vector3 error)
        {
            ShipProfile ship = c.Ship.Motor.Profile;
            Vector3 desired = charge.Velocity + Vector3.ClampMagnitude(error * EscortGain, ship.BoostSpeed);
            float along = Vector3.Dot(desired, charge.Forward);
            if (along < ship.MinSpeed) desired += charge.Forward * (ship.MinSpeed - along);
            float speed = desired.magnitude;
            c.Pilot.Direct(desired, ship.ThrottleFor(speed), speed > ship.MaxSpeed && error.magnitude > 600f);
        }
    }

    /// <summary>Runs from <see cref="CombatContext.Danger"/> at full boost, holding to the order's course where it points away.</summary>
    public class FleeBehavior : CombatBehavior
    {
        private readonly float seed = Random.value * 100f;

        public override string Name => "Flee";
        public override bool Urgent => true;

        public override float Score(CombatContext c) => c.Danger != null ? 0.95f : 0f;

        public override void Execute(CombatContext c, float dt)
        {
            Vector3 away = -c.ToDanger.normalized;
            Vector3 direction = away;
            if (c.Brain.Order != ShipOrder.Patrol)
            {
                Vector3 toGoal = (c.Brain.OrderPoint - c.Ship.Position).normalized;
                if (Vector3.Dot(toGoal, away) > 0f) direction += toGoal;
            }
            // Weave once the chaser is near gun range, so it cannot settle its aim.
            if (c.DangerDistance < c.WeaponRange * 1.5f)
            {
                float t = Time.time * 0.6f;
                Transform ship = c.Ship.transform;
                direction += (ship.right * (Mathf.PerlinNoise(seed, t) - 0.5f) + ship.up * (Mathf.PerlinNoise(t, seed) - 0.5f)) * 1.6f;
            }
            c.Pilot.Direct(direction, 1f, true);
            c.Pilot.FireTarget = null;
        }
    }
}
