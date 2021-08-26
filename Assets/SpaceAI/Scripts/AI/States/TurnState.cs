using SpaceAI.Ship;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class TurnState : FSMState
    {
        private SA_ShipController shipController = null;
        private float timeStamp = 0;
        private int turnTime = 0;

        public TurnState(SA_ShipController obj) : base(obj)
        {
            stateID = StateID.Turn;
            shipController = obj;
        }

        public override void DoBeforeEntering()
        {
            turnTime = UnityEngine.Random.Range(3, 6);
            timeStamp = Time.time;

            shipController.SetTarget(shipController.transform.position - new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100)) * 100);
        }

        public override void Act(Component player)
        {
        }

        public override void Reason(Component player)
        {
            SA_ShipController ship = (SA_ShipController)player;

            if (Time.time > timeStamp + turnTime)
            {
                if (ship.EnemyTarget)
                {
                    timeStamp = Time.time;
                    ship.FSM.PerformTransition(Transition.Attack);
                }
                else
                {
                    timeStamp = Time.time;
                    ship.FSM.PerformTransition(Transition.Patrol);
                }
            }
        }

        public override void DoBeforeLeaving()
        {
        }
    }
}
