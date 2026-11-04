using UnityEngine;

namespace SpaceAI2
{
    public enum ManeuverKind { None, TurnAway, BoostTurn, RollAround, EmergencyBrake, Reverse }

    internal class AvoidanceManeuver
    {
        private const float BoostTurnMinTime = 1.6f;
        private const float BlockedSideShare = 0.6f;

        public ManeuverKind Kind;
        public ProbeSide Side;
        public Vector3 Escape;
        public float Elapsed;
        public int ClearReadings;

        public bool Active => Kind != ManeuverKind.None;

        public void End() => Kind = ManeuverKind.None;

        public void Choose(in ObstacleReading reading, float speed, float emergencyTime, float randomness)
        {
            ProbeSetup probe = reading.Probe;
            float timeToImpact = reading.Distance / Mathf.Max(speed, 1f);

            Side = BestSide(reading, randomness, out bool allBlocked);
            float free = reading.Free(Side);
            bool smallObstacle = reading.Free(Opposite(Side)) >= probe.Range * 0.99f;

            float angle;
            if (allBlocked)
            {
                Kind = ManeuverKind.Reverse;
                angle = 180f;
            }
            else if (timeToImpact < emergencyTime)
            {
                Kind = ManeuverKind.EmergencyBrake;
                angle = 75f;
            }
            else if (free >= probe.Range * 0.99f && timeToImpact > BoostTurnMinTime && Random.value < 0.35f)
            {
                Kind = ManeuverKind.BoostTurn;
                angle = 50f;
            }
            else if (smallObstacle && Random.value < 0.35f)
            {
                Kind = ManeuverKind.RollAround;
                angle = 45f;
            }
            else
            {
                Kind = ManeuverKind.TurnAway;
                angle = 55f;
            }

            float radians = angle * Mathf.Deg2Rad;
            Escape = (probe.Forward * Mathf.Cos(radians) + probe.SideDirection(Side) * Mathf.Sin(radians)).normalized;
            Elapsed = 0f;
            ClearReadings = 0;
        }

        public ShipInput Shape(ShipInput steer)
        {
            switch (Kind)
            {
                case ManeuverKind.TurnAway:
                    steer.Throttle = -0.2f;
                    break;
                case ManeuverKind.BoostTurn:
                    steer.Throttle = 1f;
                    steer.Boost = true;
                    break;
                case ManeuverKind.RollAround:
                    steer.Throttle = 0f;
                    steer.Roll += Side == ProbeSide.Left ? -0.6f : 0.6f;
                    break;
                default:
                    steer.Throttle = -1f;
                    break;
            }
            return steer;
        }

        private static ProbeSide BestSide(in ObstacleReading reading, float randomness, out bool allBlocked)
        {
            ProbeSetup probe = reading.Probe;
            ProbeSide best = ProbeSide.Up;
            float bestScore = float.MinValue;
            allBlocked = true;

            for (int i = 0; i < 4; i++)
            {
                var side = (ProbeSide)i;
                float free = reading.Free(side) / probe.Range;
                if (free >= BlockedSideShare) allBlocked = false;

                float score = free
                              + 0.5f * Vector3.Dot(reading.Normal, probe.SideDirection(side))
                              + Random.value * randomness
                              + (side == ProbeSide.Up ? 0.1f : side == ProbeSide.Down ? -0.1f : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = side;
                }
            }
            return best;
        }

        private static ProbeSide Opposite(ProbeSide side) => side switch
        {
            ProbeSide.Up => ProbeSide.Down,
            ProbeSide.Down => ProbeSide.Up,
            ProbeSide.Left => ProbeSide.Right,
            _ => ProbeSide.Left
        };
    }
}
