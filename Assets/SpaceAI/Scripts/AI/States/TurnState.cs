using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class TurnState : FSMState
    {
        private float timeStamp = 2;
        private float turnTime = 3;
        private float rollFrequency;
        private int makeManuver;

        public TurnState(SA_IShip obj) : base(obj)
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

        public override void DoBeforeLeaving()
        {
        }
    }
}
