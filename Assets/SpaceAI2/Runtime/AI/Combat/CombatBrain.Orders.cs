using System;
using UnityEngine;

namespace SpaceAI2
{
    /// <summary>When a pilot picks a fight.</summary>
    public enum Engagement
    {
        /// <summary>Attacks any enemy in sensor range; an escort only those near its charge.</summary>
        Attack,
        /// <summary>Fights only enemies that attack it or the ship it escorts.</summary>
        Defend,
        /// <summary>Never picks a fight: runs from enemies inside the flee range and resumes its order once clear.</summary>
        Flee,
        /// <summary>Ignores enemies and keeps to its order, leaving them to its turrets. Meant for large ships.</summary>
        Passive
    }

    /// <summary>What a pilot does when it is not fighting.</summary>
    public enum ShipOrder
    {
        /// <summary>Wanders around its patrol area.</summary>
        Patrol,
        FollowRoute,
        MoveTo,
        /// <summary>Keeps station by another ship and protects it.</summary>
        Escort
    }

    public partial class CombatBrain
    {
        [Header("Orders")]
        [Tooltip("When the pilot picks a fight: any enemy, only its attackers, or never.")]
        [SerializeField] private Engagement engagement = Engagement.Attack;
        [Tooltip("Route to fly from the start. Empty patrols.")]
        [SerializeField] private ShipRoute route;
        [Tooltip("Ship to escort from the start; takes precedence over the route.")]
        [SerializeField] private SpaceShip escort;
        [Tooltip("With Flee, the pilot runs from enemies closer than this.")]
        [SerializeField, Min(100f)] private float fleeRange = 1500f;
        [Tooltip("An escort attacks only enemies within this distance of its charge, or those attacking it.")]
        [SerializeField, Min(100f)] private float escortRadius = 1500f;

        private const float MoveArriveRadius = 150f;
        private const float AttackerMemory = 10f;
        private const float DangerHysteresis = 1.25f;
        private const float CloseDanger = 0.4f;

        private int waypoint;
        private Vector3 movePoint;
        private Transform moveTarget;
        private bool followsTransform;
        private Vector3 patrolPoint;
        private bool hasPatrolPoint;
        private float escortSide;

        /// <summary>Raised when a route without a loop ends, a move arrives or the escorted ship is lost; the pilot then patrols where it is.</summary>
        public event Action<CombatBrain, ShipOrder> OrderCompleted;

        public ShipOrder Order { get; private set; }

        public Engagement Engagement
        {
            get => engagement;
            set => engagement = value;
        }

        public float FleeRange
        {
            get => fleeRange;
            set => fleeRange = Mathf.Max(0f, value);
        }

        public float EscortRadius
        {
            get => escortRadius;
            set => escortRadius = Mathf.Max(0f, value);
        }

        public ShipRoute Route => Order == ShipOrder.FollowRoute ? route : null;
        public int Waypoint => waypoint;
        public SpaceShip EscortTarget => Order == ShipOrder.Escort ? escort : null;
        /// <summary>The enemy a fleeing pilot is running from.</summary>
        public SpaceShip Danger => context.Danger;

        /// <summary>Where the current order leads: the waypoint, the move point, the escort station or the patrol centre.</summary>
        public Vector3 OrderPoint
        {
            get
            {
                switch (Order)
                {
                    case ShipOrder.FollowRoute: return route.GetPoint(waypoint);
                    case ShipOrder.MoveTo: return movePoint;
                    case ShipOrder.Escort:
                        Transform charge = escort.transform;
                        float spacing = escort.Radius + ship.Radius;
                        return charge.position + charge.rotation * new Vector3(escortSide * (spacing + 60f), 20f, -(spacing + 100f));
                    default: return PatrolCenter;
                }
            }
        }

        /// <summary>Patrols the inspector's patrol area, or the spawn point without one.</summary>
        public void Patrol()
        {
            Order = ShipOrder.Patrol;
            hasPatrolPoint = false;
        }

        public void Patrol(Vector3 center)
        {
            Order = ShipOrder.Patrol;
            patrolPoint = center;
            hasPatrolPoint = true;
        }

        /// <summary>Flies the route from its waypoint nearest to the ship.</summary>
        public void FollowRoute(ShipRoute newRoute) => FollowRoute(newRoute, newRoute != null ? newRoute.Nearest(transform.position) : 0);

        public void FollowRoute(ShipRoute newRoute, int startWaypoint)
        {
            if (newRoute == null || newRoute.Count == 0)
            {
                Patrol();
                return;
            }
            route = newRoute;
            waypoint = Mathf.Clamp(startWaypoint, 0, newRoute.Count - 1);
            Order = ShipOrder.FollowRoute;
        }

        public void MoveTo(Vector3 point)
        {
            movePoint = point;
            moveTarget = null;
            followsTransform = false;
            Order = ShipOrder.MoveTo;
        }

        /// <summary>Flies to a transform, following it if it moves.</summary>
        public void MoveTo(Transform point)
        {
            if (point == null)
            {
                Patrol();
                return;
            }
            MoveTo(point.position);
            moveTarget = point;
            followsTransform = true;
        }

        public void Escort(SpaceShip charge)
        {
            if (charge == null || charge == ship)
            {
                Patrol();
                return;
            }
            escort = charge;
            Order = ShipOrder.Escort;
        }

        private void InitOrders()
        {
            escortSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            if (escort != null) Escort(escort);
            else if (route != null) FollowRoute(route);
        }

        private void ShiftOrders(Vector3 offset)
        {
            movePoint -= offset;
            patrolPoint -= offset;
        }

        /// <summary>Advances waypoints and completes orders; runs every tick, whatever the pilot is doing.</summary>
        private void UpdateOrder()
        {
            Vector3 position = ship.Position;
            switch (Order)
            {
                case ShipOrder.FollowRoute:
                    if (route == null || route.Count == 0)
                    {
                        Patrol();
                        return;
                    }
                    if (waypoint >= route.Count) waypoint = 0;
                    Vector3 point = route.GetPoint(waypoint);
                    if ((point - position).sqrMagnitude > route.ArriveRadius * route.ArriveRadius) return;
                    if (waypoint + 1 < route.Count) waypoint++;
                    else if (route.Loop) waypoint = 0;
                    else Complete(point);
                    break;
                case ShipOrder.MoveTo:
                    if (followsTransform)
                    {
                        if (moveTarget == null)
                        {
                            Complete(movePoint);
                            return;
                        }
                        movePoint = moveTarget.position;
                    }
                    if ((movePoint - position).sqrMagnitude < MoveArriveRadius * MoveArriveRadius) Complete(movePoint);
                    break;
                case ShipOrder.Escort:
                    if (!IsAlive(escort)) Complete(position);
                    break;
            }
        }

        private void Complete(Vector3 at)
        {
            ShipOrder done = Order;
            Patrol(at);
            OrderCompleted?.Invoke(this, done);
        }

        /// <summary>
        /// The nearest enemy hunting this ship inside the flee range, or any enemy well inside it; kept until it
        /// falls back a little beyond the range.
        /// </summary>
        private void FindDanger()
        {
            SpaceShip held = IsAlive(context.Danger) && ship.IsEnemy(context.Danger) ? context.Danger : null;
            context.Danger = null;
            if (engagement != Engagement.Flee || !SpaceAIWorld.TryGet(out SpaceAIWorld world)) return;

            Vector3 position = ship.Position;
            world.Registry.Query(position, fleeRange * DangerHysteresis, scratch);
            RefreshEngagement();
            float best = float.PositiveInfinity;
            foreach (SpaceShip enemy in scratch)
            {
                if (!ship.IsEnemy(enemy) || !enemy.isActiveAndEnabled) continue;
                float distance = (enemy.Snapshot.Position - position).magnitude;
                if (distance > fleeRange && enemy != held) continue;
                // An enemy busy with someone else is only a danger up close; one hunting this ship is one anywhere in range.
                if (distance > fleeRange * CloseDanger && VictimOf(enemy) != ship) continue;
                if (distance >= best) continue;
                best = distance;
                context.Danger = enemy;
            }
        }

        /// <summary>Whether the engagement and the escort order let the pilot take this enemy on.</summary>
        private bool MayFight(SpaceShip enemy)
        {
            SpaceShip charge = EscortTarget;
            SpaceShip victim = VictimOf(enemy);
            if (charge != null && victim == charge) return true;
            if (engagement == Engagement.Attack)
                return charge == null || (enemy.Snapshot.Position - charge.Position).sqrMagnitude < escortRadius * escortRadius;
            if (victim == ship) return true;
            return enemy == lastAttacker && Time.time - lastDamageTime < AttackerMemory;
        }

        private static SpaceShip VictimOf(SpaceShip enemy) =>
            brainOf.TryGetValue(enemy, out CombatBrain enemyBrain) && enemyBrain != null ? enemyBrain.Target : null;

        private void DrawOrderGizmos()
        {
            if (!Application.isPlaying)
            {
                if (escort != null) Gizmos.DrawLine(transform.position, escort.transform.position);
                return;
            }
            if (Order == ShipOrder.Patrol) return;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            Gizmos.DrawLine(transform.position, OrderPoint);
        }
    }
}
