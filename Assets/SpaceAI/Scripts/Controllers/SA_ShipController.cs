using SpaceAI.FSM;
using UnityEngine;

namespace SpaceAI.Ship
{
    public class SA_ShipController : SA_BaseShip
    {
        private GameObject enemyTarget;

        protected override void Start()
        {
            base.Start();

            aIProvider = new SA_AIProvider(this);

            aIProvider.CreateBaseModelFSM();
        }

        private void FixedUpdate()
        {
            aIProvider.LoopStates();

            Move();
        }

        private void LateUpdate()
        {
            obstacleSystem.OvoideObstacles();
        }

        protected override void Move()
        {
            if (followTarget)
            {
                Vector3 relativePoint = transform.InverseTransformPoint(GetCurrentTargetPosition).normalized;

                if (CurrentShipSize < 50)
                {
                    ShipConfiguration.MainConfig.MainRot = Quaternion.LookRotation(GetCurrentTargetPosition - transform.position);
                }
                else
                {
                    if (WayIsFree())
                    {
                        ShipConfiguration.MainConfig.MainRot = Quaternion.LookRotation((GetCurrentTargetPosition + Vector3.left * ShipConfiguration.MainConfig.MoveSpeed) * 2 - transform.position);
                    }
                    else
                    {
                        ShipConfiguration.MainConfig.MainRot = Quaternion.LookRotation(GetCurrentTargetPosition - transform.position);
                    }
                }

                var shipRotation = Quaternion.LerpUnclamped(rb.rotation, ShipConfiguration.MainConfig.MainRot, Time.deltaTime * ShipConfiguration.MainConfig.RotationSpeed) *
                                   Quaternion.Euler(-relativePoint.y * (Time.deltaTime * ShipConfiguration.MainConfig.PichSens), 0, -relativePoint.x * (Time.deltaTime * ShipConfiguration.MainConfig.YawSens));

                rb.rotation = shipRotation;
            }

            MovmentCalculation();
        }

        private void MovmentCalculation()
        {
            ShipConfiguration.MainConfig.MoveSpeed = Mathf.Lerp(ShipConfiguration.MainConfig.MoveSpeed, ShipConfiguration.MainConfig.Speed, Time.deltaTime / ShipConfiguration.MainConfig.MoveSpeedIncrease);
            rb.velocity = Vector3.Lerp(rb.velocity, velocityTarget, Time.deltaTime * 2);
            SetVelocityTarget(rb.rotation * Vector3.forward * (ShipConfiguration.MainConfig.Speed + ShipConfiguration.MainConfig.MoveSpeed));
        }

        public override bool ToFar()
        {
            if (Vector3.Distance(transform.position, ShipConfiguration.MainConfig.patrolPoint) > ShipConfiguration.MainConfig.flyDistance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void SetCurrentEnemy(GameObject newTarget)
        {
            CurrentEnemy = newTarget;
        }
    }
}