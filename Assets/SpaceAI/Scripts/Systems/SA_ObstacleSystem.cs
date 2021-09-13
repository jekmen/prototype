using SpaceAI.Core;
using SpaceAI.Ship;
using SpaceAI.WeaponSystem;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SpaceAI.ShipSystems
{
    [CreateAssetMenu(fileName = "ObstacleSystem")]
    public class SA_ObstacleSystem : SA_ShipSystem
    {
        [Serializable]
        public class EscapeDirections
        {
            public Vector3 dir;
            public string key;

            public EscapeDirections(Vector3 vector, string key)
            {
                dir = vector;
                this.key = key;
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
        private readonly float wingSpan;
        private float shipSpeed;
        private float t;
        private int pointTime;
        private readonly List<EscapeDirections> escapeDirections = new List<EscapeDirections>();

        public ObstacleState M_ObstacleState { get; private set; }

        public bool BlockScan { get; set; }

        public SA_ObstacleSystem(SA_BaseShip ship, bool enableLoop) : base(ship, enableLoop)
        {
            this.ship = ship;
            wingSpan = ship.meshObj.bounds.size.x;
            ship.SubscribeEvent(CollisionEvent);
            m_event += OvoideObstacles;
        }

        #region Obstacles Functions
        public void OvoideObstacles()
        {
            if (!BlockScan)
            {
                shipSpeed = ship.Configuration.MainConfig.MoveSpeed;

                switch (M_ObstacleState)
                {
                    case ObstacleState.Scan:

                        ObstacleOvoidanceDeteсtor(ship.transform.forward, 0);

                        break;

                    case ObstacleState.GoToEscapeDirection:

                        if (Vector3.Distance(ship.transform.position, newTargetPos) < ship.ShipSize * 5 || Time.time > t + 5)
                        {
                            t = Time.time;
                            ship.SetTarget(storeTarget);
                            savePos = false;
                            overrideTarget = false;
                            ship.FollowTarget = false;
                            escapeDirections.Clear();
                            M_ObstacleState = ObstacleState.Scan;
                        }
                        else
                        {
                            ship.SetTarget(newTargetPos);
                        }

                        break;
                }
            }
        }

        private void ObstacleOvoidanceDeteсtor(Vector3 direction, float offsetX)
        {
            RaycastHit hit = Rays(direction, offsetX);

            if (!hit.transform || hit.transform.GetComponentInParent(typeof(IDamageSendler))) return;

            if (hit.transform.root.gameObject != ship.gameObject)
            {
                if (!savePos)
                {
                    storeTarget = ship.GetTarget();
                    savePos = true;
                }

                FindEscapeDirections(hit.collider);
            }

            if (escapeDirections.Count > 0)
            {
                if (!overrideTarget)
                {
                    pointTime = UnityEngine.Random.Range(1, 3);
                    newTargetPos = GetClosest();
                    ship.SetTarget(newTargetPos);
                    ship.FollowTarget = true;
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

                EscapeDirections d = new EscapeDirections(dir, "Up");

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

                EscapeDirections d = new EscapeDirections(dir, "Down");

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
                EscapeDirections d = new EscapeDirections(dir, "Right");

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
                EscapeDirections d = new EscapeDirections(dir, "Left");

                if (!escapeDirections.Contains(d))
                {
                    escapeDirections.Add(d);
                }
            }
        }

        private Vector3 GetClosest()
        {
            Vector3 clos = escapeDirections[0].dir;
            float distanse = Vector3.Distance(ship.transform.position, escapeDirections[0].dir);

            foreach (var dir in escapeDirections)
            {
                float tempDistance = Vector3.Distance(ship.transform.position, dir.dir);

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
            float LoockAhed = ship.ShipSize > 50 ? shipSpeed * 10 : shipSpeed * 3;
            Ray ray = new Ray(ship.transform.position + new Vector3(offsetX, 0, 0), direction);
            //Debug.DrawRay(ship.transform.position + new Vector3(offsetX, 0, 0), direction * LoockAhed, Color.red);
            Physics.SphereCastNonAlloc(ray, 5, raycastHits, LoockAhed);
            return raycastHits[0];
        }

        public override void CollisionEvent(Collision collision)
        {
            var com = collision.gameObject.GetComponentsInParent(typeof(IDamageSendler));

            foreach (var item in com)
            {
                if ((IDamageSendler)item == null)
                {
                    t = Time.time;
                    ship.SetTarget(storeTarget);
                    savePos = false;
                    overrideTarget = false;
                    ship.FollowTarget = false;
                    escapeDirections.Clear();
                    M_ObstacleState = ObstacleState.Scan;
                    Move();
                }
            }
        }

        float storedSpeed;

        async void Move()
        {
            storedSpeed = ship.Configuration.MainConfig.Speed;

            float timer = 0;
            while (timer < 2)
            {
                ship.Configuration.MainConfig.Speed = -100;
                timer += Time.deltaTime;
                await Task.Yield();
            }

            ship.Configuration.MainConfig.Speed = storedSpeed;
        }

        #endregion
    }
}