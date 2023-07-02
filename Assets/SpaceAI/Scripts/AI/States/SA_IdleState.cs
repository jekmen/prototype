/// <summary>
/// The `IdleState` class is another specific state implementation for the finite state machine (FSM) used in AI behavior. 
/// It is part of the `SpaceAI.FSM` namespace.
/// Key components of the class include :
/// - `scanRange`, `rT`, `timeState`, `requestTime`, `requestFrequency`: Variables used for determining idle behavior and target scanning frequency.
/// - `targetSerchRequest`: An instance of the `SA_ShipTargetRequesEvent` class used to request target scanning.
/// - Constructor: Initializes the state ID and sets up variables for target scanning.
/// - `DoBeforeEntering`: Sets up the state's initial conditions, such as randomizing timers and disabling target following.
/// - `Reason`: Determines if the state should transition to another state based on certain conditions. 
/// It checks if the way is free and either transitions to the attack state if there is a current enemy or makes a target search request 
/// if the time has passed the target request frequency.
/// - `Act`: Controls the behavior of the object (ship) in the game world during the idle state. 
/// It checks if the way is free and either sets the target to the patrol point if the ship is too far or transitions to the attack state 
/// if enough time has passed and there is a current enemy.
/// - `DoBeforeLeaving`: Performs any necessary actions or resets before leaving the state, such as resetting timers.
/// </summary>
namespace SpaceAI.FSM
{
    using SpaceAI.Events;
    using SpaceAI.Ship;
    using UnityEngine;

    public class SA_IdleState : SA_FSMState
    {
        private int scanRange;
        private int rT;
        private float timeState;
        private float requestTime;
        private float requestFrequency;

        private SA_ShipTargetRequesEvent targetSerchRequest;

        public SA_IdleState(SA_IShip obj) : base(obj)
        {
            stateID = StateID.Idle;            
            scanRange = owner.ShipConfiguration.AIConfig.ShipTargetScanRange;
            requestFrequency = owner.ShipConfiguration.AIConfig.TargetRequestFrequency;
            targetSerchRequest = new SA_ShipTargetRequesEvent(owner, scanRange);
        }

        public override void DoBeforeEntering()
        {
            rT = UnityEngine.Random.Range(3, 6);
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
                        SA_EventsBus.Publish(targetSerchRequest);

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