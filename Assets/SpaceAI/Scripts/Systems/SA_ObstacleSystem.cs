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
        private bool savePos;
        private bool overrideTarget;
        private float shipSpeed;
        private float wingSpan;
        private readonly List<EscapeDirections> escapeDirections = new List<EscapeDirections>();

        public ObstacleState M_ObstacleState { get; private set; }

        public SA_ObstacleSystem() { }

        public SA_ObstacleSystem(IShip ship) : base(ship)
        {
            this.ship = ship;
            var com = ship as SA_BaseShip;
            wingSpan = com.GetComponent<MeshFilter>().mesh.bounds.size.x;
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

                    if(ship.CurrentShipSize > 50 && timeForTarget < 2F)
                    {
                        ReturnToTarget();
                    }

                    break;
            }
        }

        private async void ReturnToTarget()
        {
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
                    ship.SetTarget(newTargetPos);
                    ship.CanFollowTarget(true);
                    overrideTarget = true;
                    M_ObstacleState = ObstacleState.GoToEscapeDirection;
                }
            }
        }

        private void FindEscapeDirections(Collider col)
        {
            if (Physics.Raycast(col.transform.position, col.transform.up, out RaycastHit hitUp, col.bounds.extents.y * 2 + wingSpan))
            {
                // Calculate heigth of obj to create a new Transform.position
            }
            else
            {
                // Write calculeted heigth to transform and add it to list           
                Vector3 dir = col.transform.position + new Vector3(0, col.bounds.extents.y * 2 + wingSpan, 0);

                EscapeDirections d = new EscapeDirections(dir);

                if (!escapeDirections.Contains(d))
                {
                    escapeDirections.Add(d);
                }
            }
            if (Physics.Raycast(col.transform.position, -col.transform.up, out RaycastHit hitDown, col.bounds.extents.y * 2 + wingSpan))
            {

            }
            else
            {
                Vector3 dir = col.transform.position + new Vector3(0, -col.bounds.extents.y * 2 - wingSpan, 0);

                EscapeDirections d = new EscapeDirections(dir);

                if (!escapeDirections.Contains(d))
                {
                    escapeDirections.Add(d);
                }
            }
            if (Physics.Raycast(col.transform.position, col.transform.right, out RaycastHit hitRight, col.bounds.extents.x * 2 + wingSpan))
            {

            }
            else
            {
                Vector3 dir = col.transform.position + new Vector3(col.bounds.extents.x * 2 + wingSpan, 0, 0);

                EscapeDirections d = new EscapeDirections(dir);

                if (!escapeDirections.Contains(d))
                {
                    escapeDirections.Add(d);
                }
            }
            if (Physics.Raycast(col.transform.position, -col.transform.right, out RaycastHit hitLeft, col.bounds.extents.x * 2 + wingSpan))
            {

            }
            else
            {
                Vector3 dir = col.transform.position + new Vector3(-col.bounds.extents.x * 2 - wingSpan, 0, 0);

                EscapeDirections d = new EscapeDirections(dir);

                if (!escapeDirections.Contains(d))
                {
                    escapeDirections.Add(d);
                }
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
            float LoockAhed = ship.CurrentShipSize > 50 ? shipSpeed * 2 : shipSpeed * 3;
            Ray ray = new Ray(ship.CurrentShipTransform.position + new Vector3(offsetX, 0, 0), direction);
            //Debug.DrawRay(ship.CurrentShipTransform().position + new Vector3(offsetX, 0, 0), direction * LoockAhed, Color.red);
            Physics.SphereCastNonAlloc(ray, ship.CurrentShipSize, raycastHits, LoockAhed);
            return raycastHits[0];
        }

        public override void ShipSystemEvent(Collision obj)
        {
        }
        #endregion
    }
}