using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class TurnState : FSMState
    {
        private float timeStamp = 0;
        private int turnTime = 0;
        private float rollFrequency;
        private int makeManuver;

        public TurnState(IShip obj) : base(obj)
        {
            stateID = StateID.Turn;
        }

        public override void DoBeforeEntering()
        {
            turnTime = UnityEngine.Random.Range(2, 4);
            timeStamp = Time.time;
            rollFrequency = UnityEngine.Random.Range(0.5F, 2);
            makeManuver = UnityEngine.Random.Range(1, 3);
            owner.SetTarget(owner.CurrentShipTransform.position - new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)) * 100);
        }

        public override void Act()
        {
            if (owner.CurrentShipSize > 50) return;

            if (makeManuver < 3)
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
                    timeStamp = Time.time;
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Attack);
                }
                else
                {
                    timeStamp = Time.time;
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Patrol);
                }
            }
        }

        public override void DoBeforeLeaving()
        {
        }
    }
}
