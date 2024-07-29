namespace SpaceAI.ShipSystems
{
    using SpaceAI.Core;
    using SpaceAI.Ship;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public class SA_ObstacleSystem : SA_ShipSystem
    {
        [Serializable]
        public struct EscapeDirections
        {
            public Vector3 dir;

            public EscapeDirections(Vector3 vector)
            {
                dir = vector;
            }
        }

        public enum ObstacleState
        {
            Scan,
            GoToEscapeDirection,
            AdjustCourse,
            EmergencyStop
        }

        private Vector3 storeTarget;
        private Vector3 newTargetPos;
        private Vector3 oldTargetPos;
        private bool savePos;
        private bool overrideTarget;
        private float shipSpeed;
        private float wingSpan;
        private float executeTime = 3;
        private float maxStateTime = 3;
        private readonly List<EscapeDirections> escapeDirections = new List<EscapeDirections>();
        private readonly Dictionary<ObstacleState, Action> stateActions = new Dictionary<ObstacleState, Action>();

        public ObstacleState M_ObstacleState { get; private set; }

        // Cone raycast parameters
        private float coneAngle = 25f; // Angle of the cone in degrees
        private float coneRange = 10f; // Range of the rays
        private int numberOfRays = 5; // Number of rays to cast

        public SA_ObstacleSystem() 
        {
            InitializeStateActions();
        }

        public SA_ObstacleSystem(SA_IShip ship) : base(ship)
        {
            this.ship = ship;
            wingSpan = ship.CurrentMesh.bounds.size.x * 2;

            if (wingSpan > 100)
            {
                // Handle extremely large wingspan logic
            }

            if (wingSpan < 1)
            {
                wingSpan = 4;
            }

            InitializeStateActions();
        }

        public override SA_IShipSystem Init(SA_IShip ship, GameObject gameObject)
        {
            return new SA_ObstacleSystem(ship);
        }

        private void InitializeStateActions()
        {
            stateActions[ObstacleState.Scan] = HandleScanState;
            stateActions[ObstacleState.GoToEscapeDirection] = HandleGoToEscapeDirectionState;
            stateActions[ObstacleState.AdjustCourse] = HandleAdjustCourseState;
            stateActions[ObstacleState.EmergencyStop] = HandleEmergencyStopState;
        }

        #region Obstacles Functions
        public void OvoideObstacles()
        {
            shipSpeed = ship.ShipConfiguration.MainConfig.MoveSpeed;
            if (stateActions.ContainsKey(M_ObstacleState))
            {
                stateActions[M_ObstacleState].Invoke();
            }
        }

        private void HandleScanState()
        {
            ship.ShipConfiguration.MainConfig.Speed = ship.ShipConfiguration.MainConfig.SpeedMax;

            if (ship.CurrentShipSize < 50)
            {
                PerformConeRaycast(ship.CurrentShipTransform.forward);
            }
            else
            {
                // Different strategies for larger ships
                PerformConeRaycast(ship.CurrentShipTransform.forward);
            }
        }

        private void HandleGoToEscapeDirectionState()
        {
            float distanceToTarget = Vector3.Distance(ship.CurrentShipTransform.position, newTargetPos);
            float timeForTarget = distanceToTarget / ship.ShipConfiguration.MainConfig.MoveSpeed;
            ship.SetTarget(newTargetPos);
            ship.CanFollowTarget(true);

            if ((ship.CurrentShipSize < 50 && timeForTarget < 5F) || (ship.CurrentShipSize >= 50 && timeForTarget < 10F))
            {
                ReturnToTarget();
            }

            if (Time.time > executeTime + maxStateTime && oldTargetPos == newTargetPos)
            {
                maxStateTime = Time.time;
                //ReturnToTarget();
            }
        }

        private void HandleAdjustCourseState()
        {
            // Logic for adjusting course slightly without drastic maneuvers
        }

        private void HandleEmergencyStopState()
        {
            ship.SetTarget(ship.CurrentShipTransform.position);
            ship.CanFollowTarget(false);
            ship.ShipConfiguration.MainConfig.MoveSpeed = 0;
            // Additional emergency stop logic
        }

        private void ReturnToTarget()
        {
            ship.SetTarget(storeTarget);
            savePos = false;
            overrideTarget = false;
            ship.CanFollowTarget(false);
            escapeDirections.Clear();
            M_ObstacleState = ObstacleState.Scan;
        }

        private void PerformConeRaycast(Vector3 forwardDirection)
        {
            float halfAngle = coneAngle / 2.0f;
            float lookAhead = ship.CurrentShipSize > 50 ? shipSpeed * 20 : shipSpeed * 2;

            for (int i = 0; i < numberOfRays; i++)
            {
                for (int j = 0; j < numberOfRays; j++)
                {
                    float horizontalAngle = i * (coneAngle / (numberOfRays - 1)) - halfAngle;
                    float verticalAngle = j * (coneAngle / (numberOfRays - 1)) - halfAngle;

                    Quaternion horizontalRotation = Quaternion.AngleAxis(horizontalAngle, ship.CurrentShipTransform.up);
                    Quaternion verticalRotation = Quaternion.AngleAxis(verticalAngle, ship.CurrentShipTransform.right);

                    Vector3 direction = horizontalRotation * verticalRotation * forwardDirection;

                    Ray ray = new Ray(ship.CurrentShipTransform.position, direction);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, lookAhead))
                    {
                        Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red);

                        if (hit.transform.root.gameObject != ship.CurrentShipTransform.gameObject)
                        {
                            if (!savePos)
                            {
                                storeTarget = ship.GetCurrentTargetPosition;
                                savePos = true;
                            }

                            var Iship = hit.transform.GetComponent<SA_IShip>();

                            if (hit.transform.GetComponent<SA_IDamageSendler>() != null || Iship != null && Iship.CurrentShipSize < 50)
                            {
                                return;
                            }

                            FindEscapeDirections(hit.collider);
                        }
                    }
                    else
                    {
                        Debug.DrawRay(ray.origin, ray.direction * lookAhead, Color.green);
                    }
                }
            }

            if (escapeDirections.Count > 0)
            {
                if (!overrideTarget)
                {
                    newTargetPos = GetClosest();

                    if (oldTargetPos != newTargetPos)
                    {
                        oldTargetPos = newTargetPos;
                    }

                    ship.SetTarget(newTargetPos);
                    ship.CanFollowTarget(true);
                    overrideTarget = true;
                    M_ObstacleState = ObstacleState.GoToEscapeDirection;
                }
            }
        }

        private void FindEscapeDirections(Collider col)
        {
            AddEscapeDirectionIfNotBlocked(col.transform.position, col.transform.up, col.bounds.extents.y * 2 + wingSpan);
            AddEscapeDirectionIfNotBlocked(col.transform.position, -col.transform.up, col.bounds.extents.y * 2 + wingSpan);
            AddEscapeDirectionIfNotBlocked(col.transform.position, col.transform.right, col.bounds.extents.x * 2 + wingSpan);
            AddEscapeDirectionIfNotBlocked(col.transform.position, -col.transform.right, col.bounds.extents.x * 2 + wingSpan);
        }

        private void AddEscapeDirectionIfNotBlocked(Vector3 position, Vector3 direction, float distance)
        {
            if (Physics.Raycast(position, direction, out _, distance)) return;

            Vector3 dir = position + direction * distance;

            EscapeDirections escapeDirection = new EscapeDirections(dir);

            if (!escapeDirections.Contains(escapeDirection))
            {
                escapeDirections.Add(escapeDirection);
            }
        }

        private Vector3 GetClosest()
        {
            Vector3 closest = escapeDirections[0].dir;
            float distance = Vector3.Distance(ship.CurrentShipTransform.position, escapeDirections[0].dir);

            foreach (var dir in escapeDirections)
            {
                float tempDistance = Vector3.Distance(ship.CurrentShipTransform.position, dir.dir);

                if (tempDistance < distance)
                {
                    distance = tempDistance;
                    closest = dir.dir;
                }
            }

            return closest;
        }

        private void ForceStop()
        {
            ship.ShipConfiguration.MainConfig.MoveSpeed /= 1.5F;
        }

        public override void ShipSystemEvent(Collision obj)
        {
            // Detailed collision handling logic and logging
        }
        #endregion
    }
}
