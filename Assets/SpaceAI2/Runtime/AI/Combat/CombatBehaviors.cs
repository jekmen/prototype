using UnityEngine;

namespace SpaceAI2
{
    /// <summary>One utility-scored combat behavior. Instances belong to a single brain and may keep state.</summary>
    public abstract class CombatBehavior
    {
        public abstract string Name { get; }
        /// <summary>May replace the current behavior before its commit time runs out.</summary>
        public virtual bool Urgent => false;
        /// <summary>Time spent in it counts towards the pilot's patience.</summary>
        public virtual bool Offensive => false;
        /// <summary>After this many seconds the behavior's score is cut, so the pilot moves on.</summary>
        public virtual float MaxTime => float.PositiveInfinity;
        public float LastScore { get; internal set; }

        public abstract float Score(CombatContext c);
        public virtual void Enter(CombatContext c) { }
        public abstract void Execute(CombatContext c, float dt);
    }

    public class PatrolBehavior : CombatBehavior
    {
        private readonly float seed = Random.value * 100f;

        public override string Name => "Patrol";
        public override float Score(CombatContext c) => c.HasTarget ? 0.05f : 0.3f;

        public override void Execute(CombatContext c, float dt)
        {
            Vector3 offset = c.Brain.PatrolCenter - c.Ship.Position;
            Vector3 direction;
            if (offset.magnitude > c.Brain.PatrolRadius) direction = offset;
            else
            {
                float t = Time.time * 0.1f;
                Vector3 wander = new Vector3(Mathf.PerlinNoise(seed, t) - 0.5f, (Mathf.PerlinNoise(t, seed) - 0.5f) * 0.5f, Mathf.PerlinNoise(seed + t, seed) - 0.5f);
                direction = c.Ship.Forward + wander * 1.5f;
            }
            c.Pilot.Direct(direction);
            c.Pilot.FireTarget = c.Target;
        }
    }

    public class InterceptBehavior : CombatBehavior
    {
        public override string Name => "Intercept";
        public override float Score(CombatContext c) => c.HasTarget && c.Distance > c.WeaponRange * 1.3f ? 0.6f : 0f;
        public override void Execute(CombatContext c, float dt) => c.Pilot.Pursue(c.Target, PursuitMode.Lead);
    }

    public class PursuitBehavior : CombatBehavior
    {
        private PursuitMode mode;

        public override string Name => $"Pursuit {mode}";
        public override bool Offensive => true;
        public override float MaxTime => 25f;

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget) return 0f;
            float score = 0.3f + 0.6f * (1f - c.Aspect / 180f) + 0.2f * c.Profile.Aggression;
            if (c.Distance > c.WeaponRange * 1.3f) score *= 0.5f;
            if (c.TimeSinceFiringChance > c.Profile.Patience) score *= 0.3f;
            return score;
        }

        public override void Enter(CombatContext c) => mode = PursuitMode.Pure;

        public override void Execute(CombatContext c, float dt)
        {
            bool overrunning = c.Closure > (mode == PursuitMode.Lag ? 20f : 35f) && c.Distance < 300f;
            if (overrunning) mode = PursuitMode.Lag;
            else if (c.AngleOff < (mode == PursuitMode.Lead ? 40f : 30f)) mode = PursuitMode.Lead;
            else mode = PursuitMode.Pure;
            c.Pilot.Pursue(c.Target, mode);
        }
    }

    /// <summary>High yo-yo: climbs out of the target's turn plane to bleed closure instead of flying past it.</summary>
    public class OvershootBehavior : CombatBehavior
    {
        public override string Name => "Yo-yo";
        public override bool Urgent => true;
        public override bool Offensive => true;
        public override float MaxTime => 3f;

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget) return 0f;
            bool close = c.Distance < Mathf.Max(c.TurnRadius * 3f, 200f);
            if (close && c.Closure > 25f && c.AngleOff > 45f && c.Aspect < 90f) return 0.95f;
            // A flat turning circle never ends by itself: go vertical to change the geometry.
            bool stalemate = c.TimeSinceFiringChance > c.Profile.Patience && c.Distance < c.WeaponRange;
            return stalemate ? 0.75f : 0f;
        }

        public override void Enter(CombatContext c) => c.Brain.ResetPatience();

        public override void Execute(CombatContext c, float dt)
        {
            Vector3 up = c.Ship.transform.up;
            Vector3 normal = Vector3.Cross(c.Target.Velocity, c.ToTarget);
            if (normal.sqrMagnitude < 1f) normal = up;
            else if (Vector3.Dot(normal, up) < 0f) normal = -normal;
            c.Pilot.Direct(normal.normalized * 0.8f + c.ToTarget.normalized * 0.6f, -1f);
            c.Pilot.FireTarget = c.Target;
        }
    }

    /// <summary>Attacker on the tail: random jinks to spoil its aim, a hard break when it gets close.</summary>
    public class DefendBehavior : CombatBehavior
    {
        private const float BreakDistance = 250f;

        private string phase = "jink";
        private Vector3 jink;
        private float jinkTimer;

        public override string Name => "Defend " + phase;
        public override bool Urgent => true;
        public override float MaxTime => 12f;

        public override float Score(CombatContext c)
        {
            if (c.Threat == null) return 0f;
            float level = c.ThreatLevel;
            // Both noses on each other is a joust, not an attacker on the tail.
            if (c.Threat == c.Target && c.AngleOff < 30f) level *= 0.4f;
            return level * (0.9f + 0.4f * c.Profile.Caution);
        }

        public override void Enter(CombatContext c) => jinkTimer = 0f;

        public override void Execute(CombatContext c, float dt)
        {
            Transform t = c.Ship.transform;
            if (c.Threat == null)
            {
                c.Pilot.Direct(t.forward);
                return;
            }

            Vector3 toThreat = c.ToThreat.normalized;
            float closure = Vector3.Dot(c.Threat.Velocity - c.Ship.Velocity, -toThreat);
            if (c.ThreatDistance < BreakDistance && closure > -10f)
            {
                phase = "break";
                Vector3 lateral = Vector3.ProjectOnPlane(toThreat, t.forward);
                if (lateral.sqrMagnitude < 0.01f) lateral = t.up;
                c.Pilot.Direct(lateral.normalized * 2f + t.forward * 0.3f, -0.3f);
            }
            else
            {
                phase = "jink";
                jinkTimer -= dt;
                if (jinkTimer <= 0f)
                {
                    jinkTimer = Random.Range(0.6f, 1.3f);
                    Vector3 away = Vector3.ProjectOnPlane(-toThreat, Vector3.up).normalized;
                    jink = (away + Random.onUnitSphere * 0.9f).normalized;
                }
                bool aligned = Vector3.Angle(t.forward, jink) < 30f;
                c.Pilot.Direct(jink, 1f, aligned && c.ThreatDistance > BreakDistance * 2f);
            }
            c.Pilot.FireTarget = c.Threat;
        }
    }

    /// <summary>Boosts away to regain shields or to reset a fight that is going nowhere.</summary>
    public class ExtendBehavior : CombatBehavior
    {
        private Vector3 away;

        public override string Name => "Extend";
        public override float MaxTime => 5f;

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget && c.Threat == null) return 0f;
            float score = 0.35f;
            float caution = c.Profile.Caution;
            // After a pass cautious pilots open the distance instead of turning back into a knife fight.
            if (Time.time - c.LastMerge < 4f) score = Mathf.Max(score, 0.45f + 0.5f * (1f - c.Profile.Aggression));
            if (c.HullShare < caution) score = Mathf.Max(score, 0.75f + 0.3f * (1f - c.HullShare / Mathf.Max(caution, 0.01f)));
            return score;
        }

        public override void Enter(CombatContext c)
        {
            c.Brain.ResetPatience();
            Vector3 from = c.Threat != null ? c.ToThreat : c.ToTarget;
            away = Vector3.ProjectOnPlane(-from.normalized, Vector3.up) + Random.insideUnitSphere * 0.4f;
            if (away.sqrMagnitude < 0.01f) away = c.Ship.Forward;
        }

        public override void Execute(CombatContext c, float dt)
        {
            bool aligned = Vector3.Angle(c.Ship.Forward, away) < 30f;
            c.Pilot.Direct(away, 1f, aligned);
        }
    }

    /// <summary>Closing the last few hundred meters of a joust: slide off the line of fire and pass, then turn back.</summary>
    public class MergeBehavior : CombatBehavior
    {
        private Vector3 side;

        public override string Name => "Merge";
        public override bool Urgent => true;
        public override float MaxTime => 4f;

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget) return 0f;
            bool merging = c.Aspect > 120f && c.TargetAngleOff < 45f && c.Distance < 450f && c.Closure > 30f;
            return merging ? 1f : 0f;
        }

        public override void Enter(CombatContext c)
        {
            c.LastMerge = Time.time;
            Vector3 line = c.ToTarget.normalized;
            side = Vector3.ProjectOnPlane(c.Ship.Velocity, line);
            if (side.sqrMagnitude < 1f) side = Vector3.ProjectOnPlane(c.Ship.transform.up, line);
            side.Normalize();
        }

        public override void Execute(CombatContext c, float dt)
        {
            c.Pilot.Direct(c.ToTarget.normalized + side * 1.2f);
            c.Pilot.FireTarget = c.Target;
        }
    }

    public class HeadOnBehavior : CombatBehavior
    {
        public override string Name => "Head-on";
        public override bool Offensive => true;
        public override float MaxTime => 8f;

        public override float Score(CombatContext c)
        {
            if (!c.HasTarget) return 0f;
            bool joust = c.Aspect > 135f && c.TargetAngleOff < 30f && c.Distance < c.WeaponRange * 1.5f && c.Distance > 350f;
            return joust ? 0.5f + 0.6f * c.Profile.Aggression : 0f;
        }

        public override void Execute(CombatContext c, float dt) => c.Pilot.Pursue(c.Target, PursuitMode.Pure);
    }
}
