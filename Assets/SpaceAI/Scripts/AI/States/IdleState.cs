using SpaceAI.Events;
using SpaceAI.ScaneTools;
using SpaceAI.Ship;
using SpaceAI.ShipSystems;
using System;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class IdleState : FSMState
    {
        private int scanRange;
        private int rT;
        private float timeState;
        private float requestTime;
        private float requestFrequency;

        private ShipTargetRequesEvent targetSerchRequest;

        public IdleState(IShip obj) : base(obj)
        {
            stateID = StateID.Idle;

            scanRange = owner.ShipConfiguration.AIConfig.ShipTargetScanRange;
            requestFrequency = owner.ShipConfiguration.AIConfig.TargetRequestFrequency;

            targetSerchRequest = new ShipTargetRequesEvent(owner, scanRange);
        }

        public override void DoBeforeEntering()
        {
            rT = UnityEngine.Random.Range(1, 3);
            timeState = Time.time;
            requestTime = 0;
            owner.CanFollowTarget(false);
        }

        public override void Reason()
        {
            if (owner.WayIsFree())
            {
                if (owner.CurrentEnemy)
                {
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Attack);
                }
                else
                {
                    if (Time.time > requestTime + requestFrequency)
                    {
                        EventsBus.Publish(targetSerchRequest);

                        requestTime = Time.time;
                    }
                }
            }
        }

        public override void Act()
        {
            if (owner.WayIsFree())
            {
                if (Vector3.Distance(owner.CurrentShipTransform.position, owner.ShipConfiguration.MainConfig.patrolPoint) > owner.ShipConfiguration.MainConfig.flyDistance)
                {
                    owner.CanFollowTarget(true);

                    owner.SetTarget(owner.ShipConfiguration.MainConfig.patrolPoint);
                }
                else
                {
                    if (Time.time > timeState + rT)
                    {
                        owner.CanFollowTarget(false);

                        timeState = Time.time;

                        if (owner.CurrentEnemy)
                        {
                            owner.CurrentAIProvider.FSM.PerformTransition(Transition.Attack);
                        }
                    }
                }
            }
        }

        public override void DoBeforeLeaving()
        {
            timeState = 0;
            requestTime = 0;
        }
    }
}