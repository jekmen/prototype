/// <summary>
/// The `TurnState` class is another specific state implementation for the finite state machine (FSM) used in AI behavior. 
/// It is part of the `SpaceAI.FSM` namespace.
/// Key components of the class include :
/// - `timeStamp`, `turnTime`, `rollFrequency`, `makeManuver`: Variables used for controlling the duration and frequency of turning maneuvers.
/// - Constructor: Initializes the state ID.
/// - `DoBeforeEntering`: Sets up the state's initial conditions, such as randomizing timers, roll frequency, 
/// and determining the target position for the turn maneuver.
/// - `Act`: Controls the behavior of the object (ship) in the game world during the turn state. 
/// If the ship's size is greater than 50, no action is taken. Otherwise, it performs a rolling maneuver based on the roll amplitude, 
/// roll offset, and roll frequency.
/// - `Reason`: Determines if the state should transition to another state based on certain conditions. 
/// It checks if enough time has passed, and if there is a current enemy, it transitions to the attack state; otherwise, it transitions to the patrol state.
/// - `DoBeforeLeaving`: Performs any necessary actions or resets before leaving the state. In this case, no action is taken.
/// </summary>
namespace SpaceAI.FSM
{
    using SpaceAI.Ship;
    using UnityEngine;

    public class SA_TurnState : SA_FSMState
    {
        private float timeStamp = 2;
        private float turnTime = 3;
        private float rollFrequency;
        private int makeManuver;

        public SA_TurnState(SA_IShip obj) : base(obj)
        {
            stateID = StateID.Turn;
        }

        public override void DoBeforeEntering()
        {
            turnTime = UnityEngine.Random.Range(3, 5);
            rollFrequency = UnityEngine.Random.Range(0.5F, 2);
            makeManuver = UnityEngine.Random.Range(1, 3);
            owner.SetTarget(owner.CurrentShipTransform.position - new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)) * 100);
        }

        public override void Act()
        {
            if (owner.CurrentShipSize > 50) return;

            if (makeManuver >= 2)
            {
                float rollAmplitude = 10f; // adjust as needed
                float rollOffset = Time.time * rollFrequency;
                float roll = Mathf.Sin(rollOffset) * rollAmplitude;
                owner.CurrentShipTransform.localEulerAngles += Vector3.Lerp(owner.CurrentShipTransform.localEulerAngles, new Vector3(0, 0, roll), 10);
            }
        }

        public override void Reason()
        {
            if (Time.time > timeStamp + turnTime)
            {
                if (owner.CurrentEnemy)
                {
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Attack);
                }
                else
                {
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Patrol);
                }

                timeStamp = Time.time;
            }
        }
    }
}
