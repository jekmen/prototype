using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>
    /// Utility AI for dogfights: picks a target, watches for threats and every reaction interval switches to the
    /// best-scoring behavior, with hysteresis, a minimum commit time and fatigue for behaviors that run too long.
    /// </summary>
    [RequireComponent(typeof(AIPilot))]
    public class CombatBrain : MonoBehaviour, IPilotBrain
    {
        [SerializeField, Min(100f)] private float sensorRange = 4000f;
        [SerializeField, Min(0.1f)] private float targetInterval = 1f;
        [Tooltip("Area to patrol when there is nothing to fight. Defaults to the spawn point.")]
        [SerializeField] private Transform patrolCenter;
        [SerializeField, Min(10f)] private float patrolRadius = 800f;

        private const float ThreatCone = 25f;
        private const float RecentDamageTime = 2f;
        private const float FiringChanceAngle = 12f;
        private const float FatigueFactor = 0.3f;
        private const float AwarenessDelay = 4f;
        private const float JoustAspect = 120f;
        private const float JoustFireRange = 0.75f;
        private const float JoustAimError = 2.5f;

        private static readonly List<CombatBrain> active = new List<CombatBrain>();
        private static PilotProfile fallbackProfile;

        private readonly CombatContext context = new CombatContext();
        private readonly List<SpaceShip> scratch = new List<SpaceShip>();
        private readonly List<MissileTrack> incoming = new List<MissileTrack>();
        private CombatBehavior[] behaviors;
        private CombatBehavior current;
        private AIPilot pilot;
        private SpaceShip ship;
        private ShipHealth health;
        private MissileLauncher launcher;
        private Vector3 spawnPoint;
        private float decisionTimer;
        private float targetTimer;
        private float lastDamageTime = float.NegativeInfinity;
        private SpaceShip lastAttacker;
        private SpaceShip noticedThreat;
        private float noticedSince;

        public event Action<CombatBrain, CombatBehavior, CombatBehavior> BehaviorChanged;

        public static IReadOnlyList<CombatBrain> Active => active;
        public IReadOnlyList<CombatBehavior> Behaviors => behaviors;
        public CombatBehavior Current => current;
        public CombatContext Context => context;
        public SpaceShip Target => context.Target;
        public SpaceShip Ship => ship;
        public int Switches { get; private set; }

        /// <summary>Squad this ship flies in as a wingman, set by the formation.</summary>
        public Formation Formation { get; internal set; }
        public int Slot { get; internal set; }

        /// <summary>Overrides target selection while it is alive.</summary>
        public SpaceShip ForcedTarget { get; set; }

        public Vector3 PatrolCenter => patrolCenter != null ? patrolCenter.position : spawnPoint;
        public float PatrolRadius => patrolRadius;

        public Transform PatrolArea
        {
            get => patrolCenter;
            set => patrolCenter = value;
        }

        public void ResetPatience() => context.TimeSinceFiringChance = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake()
        {
            pilot = GetComponent<AIPilot>();
            ship = GetComponent<SpaceShip>();
            health = GetComponent<ShipHealth>();
            spawnPoint = transform.position;
            behaviors = new CombatBehavior[]
            {
                new PatrolBehavior(), new InterceptBehavior(), new PursuitBehavior(), new OvershootBehavior(),
                new DefendBehavior(), new ExtendBehavior(), new HeadOnBehavior(), new MergeBehavior(), new FormationBehavior(),
                new MissileEvadeBehavior()
            };
            launcher = GetComponent<MissileLauncher>();
            context.Brain = this;
            context.Pilot = pilot;
            context.Ship = ship;
        }

        private void OnEnable()
        {
            active.Add(this);
            if (health != null) health.Damaged += OnDamaged;
            current = null;
            context.Target = null;
            context.Threat = null;
            context.HasMissile = false;
            context.TimeSinceFiringChance = 0f;
            lastAttacker = null;
            decisionTimer = UnityEngine.Random.value * 0.3f;
            targetTimer = 0f;
        }

        private void OnDisable()
        {
            active.Remove(this);
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(ShipHealth _, HitInfo hit)
        {
            lastDamageTime = Time.time;
            if (hit.Source != null && ship.IsEnemy(hit.Source)) lastAttacker = hit.Source;
        }

        public void Think(AIPilot _, float dt)
        {
            context.Profile = pilot.Profile != null ? pilot.Profile : FallbackProfile();
            context.TimeInBehavior += dt;

            targetTimer -= dt;
            bool lost = context.Target != null && !IsAlive(context.Target);
            if (targetTimer <= 0f || lost)
            {
                SelectTarget();
                targetTimer = targetInterval;
            }

            int warned = context.HasMissile ? context.Missile.Id : 0;
            SenseMissiles();
            bool newMissile = context.HasMissile && context.Missile.Id != warned;

            decisionTimer -= dt;
            bool decide = decisionTimer <= 0f || current == null || lost || newMissile;
            if (decide) FindThreat();
            UpdateGeometry();
            UpdatePatience(dt);

            if (decide)
            {
                Decide();
                decisionTimer = context.Profile.ReactionTime;
            }
            // Head-on both sides would hit almost every shot and trade kills, so a joust is a short, inaccurate burst.
            bool joust = context.HasTarget && context.Aspect > JoustAspect;
            pilot.FireRangeScale = joust ? JoustFireRange : 1f;
            pilot.ExtraAimError = joust ? JoustAimError : 0f;
            current.Execute(context, dt);
            UpdateLauncher();
        }

        /// <summary>Picks the noticed missile closest to impact. Pilots notice a launch only after their warning delay.</summary>
        private void SenseMissiles()
        {
            context.HasMissile = false;
            if (!MissileSystem.TryGet(out MissileSystem system)) return;
            system.GetIncoming(ship, incoming);
            float soonest = float.PositiveInfinity;
            foreach (MissileTrack missile in incoming)
            {
                if (missile.Age < context.Profile.MissileWarningDelay) continue;
                Vector3 offset = ship.Position - missile.Position;
                float distance = offset.magnitude;
                float closing = Vector3.Dot(missile.Velocity - ship.Velocity, offset / Mathf.Max(distance, 0.01f));
                float timeToGo = closing > 1f ? distance / closing : 60f;
                if (context.HasMissile && timeToGo >= soonest) continue;
                context.HasMissile = true;
                context.Missile = missile;
                soonest = timeToGo;
            }
            context.MissileTimeToGo = soonest;
        }

        private void UpdateLauncher()
        {
            if (launcher == null || launcher.Missile == null) return;
            MissileProfile missile = launcher.Missile;
            SpaceShip target = context.Target;
            if (target != null && target.Radius < missile.MinTargetRadius) target = null;
            launcher.LockTarget = target;
            if (target == null || !launcher.CanLaunch) return;
            if (context.Distance < missile.MinRange || context.Distance > missile.LockRange) return;
            if (!missile.IsGuided && context.AngleOff > 3f) return;
            // Long head-on shots are easy to beam; wait until the closure leaves less time to react.
            if (context.Aspect > 120f && context.Distance > missile.LockRange * 0.5f) return;
            if (MissileSystem.TryGet(out MissileSystem system) && system.IsTracking(ship, target)) return;
            launcher.Launch();
        }

        private static bool IsAlive(SpaceShip other) => other != null && other.isActiveAndEnabled;

        private static PilotProfile FallbackProfile()
        {
            if (fallbackProfile == null)
            {
                fallbackProfile = ScriptableObject.CreateInstance<PilotProfile>();
                fallbackProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            return fallbackProfile;
        }

        private void Decide()
        {
            PilotProfile profile = context.Profile;
            CombatBehavior best = null;
            float bestScore = float.MinValue;
            foreach (CombatBehavior behavior in behaviors)
            {
                float score = behavior.Score(context);
                if (behavior == current)
                {
                    if (context.TimeInBehavior > behavior.MaxTime) score *= FatigueFactor;
                    else score += profile.Hysteresis;
                }
                behavior.LastScore = score;
                if (score > bestScore)
                {
                    best = behavior;
                    bestScore = score;
                }
            }

            if (best == current) return;
            bool committed = current != null && context.TimeInBehavior < profile.MinCommitTime && !best.Urgent;
            if (!committed) Switch(best);
        }

        private void Switch(CombatBehavior next)
        {
            CombatBehavior previous = current;
            current = next;
            context.TimeInBehavior = 0f;
            if (previous != null) Switches++;
            next.Enter(context);
            BehaviorChanged?.Invoke(this, previous, next);
        }

        private void SelectTarget()
        {
            SpaceShip forced = ForcedTarget;
            if (IsAlive(forced) && ship.IsEnemy(forced))
            {
                context.Target = forced;
                return;
            }

            Formation formation = Formation;
            if (formation != null && formation.Order == SquadOrder.Attack && IsAlive(formation.OrderTarget))
            {
                context.Target = formation.OrderTarget;
                return;
            }

            SpaceShip best = null;
            if (SpaceAIWorld.TryGet(out SpaceAIWorld world))
            {
                world.Registry.Query(ship.Position, sensorRange, scratch);
                float weaponRange = WeaponRange();
                float bestScore = float.MinValue;
                foreach (SpaceShip enemy in scratch)
                {
                    if (!ship.IsEnemy(enemy) || !enemy.isActiveAndEnabled) continue;
                    if (formation != null && !formation.MayEngage(enemy)) continue;
                    Vector3 offset = enemy.Position - ship.Position;
                    float distance = Mathf.Max(offset.magnitude, 0.01f);

                    float score = 1f - distance / sensorRange;
                    score += 0.3f * Vector3.Dot(ship.Forward, offset / distance);
                    if (distance < weaponRange * 1.3f && Vector3.Angle(enemy.Forward, -offset) < 30f) score += 0.8f;
                    if (enemy == lastAttacker && Time.time - lastDamageTime < RecentDamageTime * 2f) score += 0.5f;
                    if (enemy.TryGetComponent(out ShipHealth enemyHealth)) score += 0.3f * (1f - enemyHealth.Hull / enemyHealth.MaxHull);
                    if (enemy == context.Target) score += 0.3f;
                    score += AlliesInvolved(enemy);

                    if (score > bestScore)
                    {
                        best = enemy;
                        bestScore = score;
                    }
                }
            }
            context.Target = best;
        }

        /// <summary>Bonus for an enemy attacking an ally, penalty for one allies are already on, so a wing spreads out.</summary>
        private float AlliesInvolved(SpaceShip enemy)
        {
            float score = 0f;
            CombatBrain enemyBrain = null;
            foreach (CombatBrain brain in active)
            {
                if (brain == this) continue;
                if (brain.ship == enemy) enemyBrain = brain;
                else if (!ship.IsEnemy(brain.ship) && brain.Target == enemy) score -= 0.25f;
            }
            SpaceShip victim = enemyBrain != null ? enemyBrain.Target : null;
            if (victim != null && victim != ship && !ship.IsEnemy(victim)) score += 0.4f;
            return score;
        }

        private void FindThreat()
        {
            context.Threat = null;
            context.ThreatLevel = 0f;
            if (!SpaceAIWorld.TryGet(out SpaceAIWorld world)) return;

            float range = WeaponRange() * 1.3f;
            world.Registry.Query(ship.Position, range, scratch);
            foreach (SpaceShip enemy in scratch)
            {
                if (!ship.IsEnemy(enemy) || !enemy.isActiveAndEnabled) continue;
                Vector3 offset = ship.Position - enemy.Position;
                float distance = offset.magnitude;
                float angle = Vector3.Angle(enemy.Forward, offset);
                if (angle > ThreatCone) continue;

                float level = (1f - angle / ThreatCone) * 0.6f + (1f - distance / range) * 0.4f;
                if (level > context.ThreatLevel)
                {
                    context.Threat = enemy;
                    context.ThreatLevel = level;
                }
            }

            if (Time.time - lastDamageTime < RecentDamageTime && IsAlive(lastAttacker) && context.ThreatLevel < 0.7f)
            {
                context.Threat = lastAttacker;
                context.ThreatLevel = 0.7f;
            }

            // A new threat takes a few reaction times to notice; this is what gives better pilots their openings.
            if (context.Threat != noticedThreat)
            {
                noticedThreat = context.Threat;
                noticedSince = Time.time;
            }
            if (context.Threat != null)
                context.ThreatLevel *= Mathf.Clamp01((Time.time - noticedSince) / (context.Profile.ReactionTime * AwarenessDelay));
        }

        private void UpdateGeometry()
        {
            ShipMotor motor = ship.Motor;
            context.WeaponRange = WeaponRange();
            context.TurnRadius = motor.Profile.TurnRadius(motor.Speed);
            context.HullShare = health != null ? health.Hull / health.MaxHull : 1f;

            if (!IsAlive(context.Threat)) context.Threat = null;
            if (context.Threat != null)
            {
                context.ToThreat = context.Threat.Position - ship.Position;
                context.ThreatDistance = context.ToThreat.magnitude;
            }

            SpaceShip target = context.Target;
            if (target == null) return;
            Vector3 toTarget = target.Position - ship.Position;
            float distance = Mathf.Max(toTarget.magnitude, 0.01f);
            context.ToTarget = toTarget;
            context.Distance = distance;
            context.AngleOff = Vector3.Angle(ship.Forward, toTarget);
            context.TargetAngleOff = Vector3.Angle(target.Forward, -toTarget);
            context.Aspect = 180f - context.TargetAngleOff;
            context.Closure = Vector3.Dot(motor.Velocity - target.Velocity, toTarget / distance);
        }

        private void UpdatePatience(float dt)
        {
            bool chance = pilot.HasFiringSolution && pilot.GunAimAngle < FiringChanceAngle && context.Distance < context.WeaponRange;
            if (chance) context.TimeSinceFiringChance = 0f;
            else if (current != null && current.Offensive) context.TimeSinceFiringChance += dt;
        }

        private float WeaponRange() => pilot.Gun != null && pilot.Gun.Weapon != null ? pilot.Gun.Weapon.Range : 900f;
    }
}
