using SpaceAI.Core;
using SpaceAI.Ship;
using SpaceAI.WeaponSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
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
            GoToEscapeDirection
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

        public ObstacleState M_ObstacleState { get; private set; }

        public SA_ObstacleSystem() { }

        public SA_ObstacleSystem(IShip ship) : base(ship)
        {
            this.ship = ship;
            wingSpan = ship.CurrentMesh.bounds.size.x * 2;
        }

        public override IShipSystem Init(IShip ship, GameObject gameObject)
        {
            return new SA_ObstacleSystem(ship);
        }

        #region Obstacles Functions
        public void OvoideObstacles()
        {
            shipSpeed = ship.ShipConfiguration.MainConfig.MoveSpeed;

            switch (M_ObstacleState)
            {
                case ObstacleState.Scan:

                    ship.ShipConfiguration.MainConfig.Speed = ship.ShipConfiguration.MainConfig.SpeedMax;

                    ObstacleOvoidanceDeteсtor(ship.CurrentShipTransform.forward, 0);

                    break;

                case ObstacleState.GoToEscapeDirection:

                    float distanceToTarget = Vector3.Distance(ship.CurrentShipTransform.position, newTargetPos);
                    float timeForTarget = distanceToTarget / ship.ShipConfiguration.MainConfig.MoveSpeed;
                    ship.SetTarget(newTargetPos);
                    ship.CanFollowTarget(true);

                    if (ship.CurrentShipSize < 50 && timeForTarget < 1F)
                    {
                        ReturnToTarget();
                    }

                    if (ship.CurrentShipSize > 50 && timeForTarget < 2F)
                    {
                        ReturnToTarget();
                    }

                    if (Time.time > executeTime + maxStateTime && oldTargetPos == newTargetPos)
                    {
                        maxStateTime = Time.time;
                        ReturnToTarget();
                    }

                    break;
            }
        }

        private async void ReturnToTarget()
        {
            ForceStop();

            await Task.Delay(TimeSpan.FromSeconds(0.3F));

            ship.SetTarget(storeTarget);
            savePos = false;
            overrideTarget = false;
            ship.CanFollowTarget(false);
            escapeDirections.Clear();
            M_ObstacleState = ObstacleState.Scan;
        }

        private void ObstacleOvoidanceDeteсtor(Vector3 direction, float offsetX)
        {
            RaycastHit hit = Rays(direction, offsetX);

            if (!hit.transform || hit.transform.GetComponentInParent(typeof(IDamageSendler))) return;

            if (hit.transform.root.gameObject != ship.CurrentShipTransform.gameObject)
            {
                if (!savePos)
                {
                    storeTarget = ship.GetCurrentTargetPosition;
                    savePos = true;
                }

                FindEscapeDirections(hit.collider);
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
            Vector3 clos = escapeDirections[0].dir;

            float distanse = Vector3.Distance(ship.CurrentShipTransform.position, escapeDirections[0].dir);

            foreach (var dir in escapeDirections)
            {
                float tempDistance = Vector3.Distance(ship.CurrentShipTransform.position, dir.dir);

                if (tempDistance < distanse)
                {
                    distanse = tempDistance;

                    clos = dir.dir;
                }
            }

            return clos;
        }

        readonly RaycastHit[] raycastHits = new RaycastHit[5];

        private RaycastHit Rays(Vector3 direction, float offsetX)
        {
            float LoockAhed = ship.CurrentShipSize > 50 ? shipSpeed * 10 : shipSpeed * 20;
            Ray ray = new Ray(ship.CurrentShipTransform.position + new Vector3(offsetX, 0, 0), direction);
            //Debug.DrawRay(ship.CurrentShipTransform.position + new Vector3(offsetX, 0, 0), direction * LoockAhed, Color.red);
            Physics.SphereCastNonAlloc(ray, ship.CurrentShipSize, raycastHits, LoockAhed);
            return raycastHits[0];
        }

        private void ForceStop()
        {
            ship.ShipConfiguration.MainConfig.MoveSpeed /= 1.5F;
        }

        public override void ShipSystemEvent(Collision obj)
        {
        }
        #endregion
    }
}