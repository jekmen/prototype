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

        protected override void OnSystemsReady(ShipSystemsInitedEvent e)
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
            }

            float pitchAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(rb.velocity, transform.right), transform.right);
            float yawAngle = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(rb.velocity, transform.up), transform.up);

            float pitchSens = Mathf.Lerp(ShipConfiguration.MainConfig.MinPitchSens, ShipConfiguration.MainConfig.MaxPitchSens, Mathf.InverseLerp(0f, ShipConfiguration.MainConfig.MaxPitchAngle, Mathf.Abs(pitchAngle)));
            float yawSens = Mathf.Lerp(ShipConfiguration.MainConfig.MinYawSens, ShipConfiguration.MainConfig.MaxYawSens, Mathf.InverseLerp(0f, ShipConfiguration.MainConfig.MaxYawAngle, Mathf.Abs(yawAngle)));

            var shipRotation = Quaternion.Lerp(rb.rotation, ShipConfiguration.MainConfig.MainRot, Time.deltaTime * ShipConfiguration.MainConfig.RotationSpeed) *
                               Quaternion.Euler(-relativePoint.y * (Time.deltaTime * pitchSens), 0, -relativePoint.x * (Time.deltaTime * yawSens));

            rb.rotation = shipRotation;

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