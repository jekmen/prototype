using SpaceAI.Events;
using SpaceAI.FSM;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Ship
{
    public class SA_ShipController : SA_BaseShip
    {
        private bool isReady = false;
        private float worldSpeed = 2.5F;

        protected override void OnSystemsReady(SA_ShipSystemsInitedEvent e)
        {
            isReady = ShipConfiguration;

            if (isReady)
            {
                aIProvider = new SA_AIProvider(this);

                aIProvider.CreateBaseModelFSM();
            }
        }

        private void FixedUpdate()
        {
            if (!isReady) return;

            aIProvider.LoopStates();

            Move();
        }

        protected override void Move()
        {
            Vector3 relativePoint = transform.InverseTransformPoint(GetCurrentTargetPosition).normalized;

            if (followTarget)
            {
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

                var barelRotationSpeed = ShipConfiguration.MainConfig.RotationSpeed * Time.deltaTime * 1000;
                var barelShipRotation = Quaternion.Euler(-relativePoint.y * Time.deltaTime * barelRotationSpeed, 0, -relativePoint.x * Time.deltaTime * barelRotationSpeed);
                var shipRotation = Quaternion.Lerp(rb.rotation, ShipConfiguration.MainConfig.MainRot, Time.deltaTime);

                rb.rotation = shipRotation;
                rb.rotation *= barelShipRotation;
            }

            MovmentCalculation();
        }

        private void MovmentCalculation()
        {
            ShipConfiguration.MainConfig.MoveSpeed = Mathf.Lerp(ShipConfiguration.MainConfig.MoveSpeed, ShipConfiguration.MainConfig.Speed, Time.deltaTime / ShipConfiguration.MainConfig.MoveSpeedIncrease);
            SetVelocityTarget(rb.rotation * Vector3.forward * (ShipConfiguration.MainConfig.Speed + ShipConfiguration.MainConfig.MoveSpeed));
            rb.velocity = Vector3.Lerp(rb.velocity, velocityTarget, Time.deltaTime * worldSpeed);
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

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, GetCurrentTargetPosition);
        }
    }
}