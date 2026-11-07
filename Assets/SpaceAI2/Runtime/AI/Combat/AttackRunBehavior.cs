using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Attacks a large ship in passes: picks an approach line to the chosen subsystem that the fewest turrets can
    /// cover, flies around to its start, runs in firing, breaks away along the hull and sets up the next pass.
    /// </summary>
    public class AttackRunBehavior : CombatBehavior
    {
        private enum Phase { Setup, Approach, Run, Breakaway }

        private const float StartRangeFactor = 0.55f;
        private const float BreakRangeFactor = 0.1f;
        private const float ReturnRangeFactor = 0.65f;
        private const float TurnCost = 1f;
        private const float ApproachTolerance = 25f;
        private const float MaxApproachTime = 25f;
        private const float MaxRunTime = 12f;
        private const float MaxBreakTime = 8f;
        private const float WingSpacingAngle = 30f;
        private const float WeaveAmplitude = 0.6f;
        private const float WeaveFrequency = 2.2f;
        private static readonly float[] Samples = { 1f, 0.7f, 0.45f, 0.25f, 0.1f };
        private static Vector3[] candidates;

        private Phase phase;
        private float phaseTime;
        private Vector3 localDirection;
        private Vector3 localExit;
        private float weavePhase;

        public override string Name => "Attack run " + phase;
        public override bool Offensive => true;

        /// <summary>Approach line of the current pass in world space, pointing from the target out to the start.</summary>
        public Vector3 Direction { get; private set; }
        public float Exposure { get; private set; }

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget || !c.Brain.UseAttackRuns || c.Target.Subsystems.Count == 0) return 0f;
            return 1.2f;
        }

        public override void Enter(CombatContext c) => SetPhase(Phase.Setup);

        public override void Execute(CombatContext c, float dt)
        {
            phaseTime += dt;
            SpaceShip target = c.Target;
            // The target can drop out between decisions while this behavior is still committed.
            if (target == null)
            {
                c.Pilot.Direct(c.Ship.Forward);
                SetPhase(Phase.Setup);
                return;
            }
            float start = StartDistance(c);
            switch (phase)
            {
                case Phase.Setup:
                    Plan(c, start);
                    SetPhase(Phase.Approach);
                    goto case Phase.Approach;

                case Phase.Approach:
                {
                    Direction = target.transform.rotation * localDirection;
                    Vector3 point = c.TargetPoint;
                    Vector3 from = c.Ship.Position - point;
                    float distance = from.magnitude;
                    float angle = Vector3.Angle(from, Direction);
                    // Any line with room for a burst before the break will do.
                    if (angle < ApproachTolerance && distance > BreakDistance(c) * 2.5f || phaseTime > MaxApproachTime)
                    {
                        SetPhase(Phase.Run);
                        goto case Phase.Run;
                    }
                    // Swing around the target instead of cutting across its guns, no further out than it has to.
                    Vector3 around = Vector3.Slerp(from / Mathf.Max(distance, 0.01f), Direction, Mathf.Min(1f, 60f / Mathf.Max(angle, 1f)));
                    Vector3 waypoint = point + around * Mathf.Clamp(distance, BreakDistance(c) * 2f, start);
                    c.Pilot.Direct(waypoint - c.Ship.Position, 1f, distance > start * 1.5f);
                    break;
                }

                case Phase.Run:
                {
                    Vector3 toPoint = c.TargetPoint - c.Ship.Position;
                    // The pass ends just outside the turn radius, close enough for guns to hit a part but not to ram the hull.
                    float breakDistance = BreakDistance(c);
                    bool passed = Vector3.Dot(toPoint, c.Ship.Forward) < 0f;
                    if (toPoint.magnitude < breakDistance || passed || c.Subsystem == null && target.Subsystems.Count > 0 && phaseTime > 2f ||
                        phaseTime > MaxRunTime)
                    {
                        BeginBreakaway(c);
                        goto case Phase.Breakaway;
                    }
                    // The guns gimbal only a little, so the nose follows the lead on the part rather than on the hull's centre.
                    if (c.Subsystem != null && c.Pilot.HasFiringSolution) c.Pilot.Direct(c.Pilot.FiringDirection);
                    else c.Pilot.Pursue(target, PursuitMode.Lead);
                    break;
                }

                case Phase.Breakaway:
                {
                    float distance = Vector3.Distance(c.Ship.Position, target.Position);
                    if (distance > start * ReturnRangeFactor || phaseTime > MaxBreakTime)
                    {
                        SetPhase(Phase.Setup);
                        break;
                    }
                    Vector3 exit = target.transform.rotation * localExit;
                    c.Pilot.Direct(exit + Weave(exit), 1f, true);
                    break;
                }
            }
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private static float StartDistance(CombatContext c) => c.WeaponRange * StartRangeFactor + c.Target.Radius;

        private static float BreakDistance(CombatContext c) => Mathf.Max(c.WeaponRange * BreakRangeFactor, c.TurnRadius * 1.5f);

        /// <summary>The exit was planned with the pass; a straight climb away is the easiest shot for flak, so it weaves.</summary>
        private void BeginBreakaway(CombatContext c)
        {
            weavePhase = Random.value * 10f;
            SetPhase(Phase.Breakaway);
        }

        /// <summary>
        /// Exits that turn off the approach line sideways and away: flying on past the target keeps the fighter inside
        /// point defense range, a full reversal keeps it slow there.
        /// </summary>
        private static readonly Vector2[] ExitTurns = { new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f) };

        private static Vector3 ExitFor(Vector3 approach, Vector2 turn)
        {
            Vector3 side = Vector3.Cross(approach, Mathf.Abs(approach.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, approach);
            return (side * turn.x + up * turn.y + approach * 0.6f).normalized;
        }

        private Vector3 Weave(Vector3 direction)
        {
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, direction);
            float t = (phaseTime + weavePhase) * WeaveFrequency;
            return (side * Mathf.Sin(t) + up * Mathf.Sin(t * 0.63f + 1f)) * WeaveAmplitude;
        }

        /// <summary>
        /// Scores approach lines around the target: turret coverage along the line, the subsystem hidden behind the
        /// hull, how far the fighter has to fly round, and lines wingmates already use.
        /// </summary>
        private void Plan(CombatContext c, float start)
        {
            SpaceShip target = c.Target;
            Quaternion rotation = target.transform.rotation;
            Vector3 point = c.TargetPoint;
            Subsystem part = c.Subsystem;
            Collider hull = target.GetComponent<Collider>();
            Vector3 from = (c.Ship.Position - point).normalized;

            Vector3 normal = SurfaceNormal(hull, point, target.Position);

            float bestCost = float.MaxValue;
            Vector3 best = Quaternion.Inverse(rotation) * from;
            Vector3 bestExit = Quaternion.Inverse(rotation) * normal;
            float bestExposure = 0f;
            float breakDistance = BreakDistance(c);
            foreach (Vector3 local in Candidates())
            {
                Vector3 direction = rotation * local;
                if (Vector3.Dot(direction, normal) < 0.15f) continue;
                if (part != null && hull != null && Hidden(hull, point, direction, start, part.Radius)) continue;

                float approach = 0f;
                foreach (float sample in Samples)
                    approach += Coverage(target, point + direction * Mathf.Max(start * sample, breakDistance));
                Vector3 breakPoint = point + direction * breakDistance;
                float exitExposure = float.MaxValue;
                Vector3 exit = direction;
                foreach (Vector2 turn in ExitTurns)
                {
                    Vector3 candidate = ExitFor(direction, turn);
                    if (Vector3.Dot(candidate, normal) < 0f) continue;
                    float exposure = 0f;
                    foreach (float sample in Samples)
                        exposure += Coverage(target, breakPoint + candidate * (start * (1f - sample)));
                    if (exposure < exitExposure)
                    {
                        exitExposure = exposure;
                        exit = candidate;
                    }
                }
                float total = (approach + exitExposure) / Samples.Length;

                float cost = total + TurnCost * Vector3.Angle(from, direction) / 180f + Random.value * 0.15f;
                foreach (CombatBrain brain in CombatBrain.Active)
                {
                    if (brain == c.Brain || brain.Target != target || brain.Current is not AttackRunBehavior run) continue;
                    if (Vector3.Angle(run.Direction, direction) < WingSpacingAngle) cost += 0.4f;
                }
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = local;
                    bestExit = Quaternion.Inverse(rotation) * exit;
                    bestExposure = total;
                }
            }
            localDirection = best;
            localExit = bestExit;
            Direction = rotation * best;
            Exposure = bestExposure;
        }

        private static Vector3 SurfaceNormal(Collider hull, Vector3 point, Vector3 center)
        {
            Vector3 offset = hull != null ? point - hull.ClosestPoint(point) : Vector3.zero;
            if (offset.sqrMagnitude < 0.01f) offset = point - center;
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.up;
        }

        private static bool Hidden(Collider hull, Vector3 point, Vector3 direction, float start, float radius)
        {
            var ray = new Ray(point + direction * start, -direction);
            // Hitboxes sit inside a hull collider that encloses them; shots carry that far in (SpaceShip.BuriedSubsystem).
            Vector3 extents = hull.bounds.extents;
            float buried = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z));
            return hull.Raycast(ray, out RaycastHit hit, start) && hit.distance < start - Mathf.Max(radius * 1.5f, buried);
        }

        /// <summary>Weighted count of the target's working turrets that can fire at this point.</summary>
        private static float Coverage(SpaceShip target, Vector3 point)
        {
            float coverage = 0f;
            foreach (Subsystem part in target.Subsystems)
            {
                ShipTurret turret = part.Turret;
                if (turret == null || part.IsDestroyed || !turret.isActiveAndEnabled || turret.Weapon == null) continue;
                if (turret.Role == TurretRole.Main) continue;
                if ((point - turret.transform.position).sqrMagnitude > turret.Weapon.Range * turret.Weapon.Range) continue;
                if (!turret.CanReach(point)) continue;
                coverage += turret.Role == TurretRole.Flak ? 1.5f : 1f;
            }
            return coverage;
        }

        private static Vector3[] Candidates()
        {
            if (candidates != null) return candidates;
            // Evenly spread over the sphere (Fibonacci lattice).
            const int count = 64;
            candidates = new Vector3[count];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float r = Mathf.Sqrt(1f - y * y);
                candidates[i] = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
            }
            return candidates;
        }
    }
}
