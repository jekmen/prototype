using SpaceAI.ScaneTools;
using SpaceAI.Ship;
using SpaceAI.ShipSystems;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class IdleState : FSMState
    {
        private SA_ShipController shipController = null;
        private readonly int scanRange = 800;
        private int rT;
        private float timeState;

        public IdleState(SA_ShipController obj) : base(obj)
        {
            stateID = StateID.Idle;
            shipController = obj;
        }

        public override void DoBeforeEntering()
        {
            rT = UnityEngine.Random.Range(3, 7) * 2;
            timeState = Time.time;
            shipController.FollowTarget = false;
        }

        public override void Reason(Component player)
        {
            SA_ShipController ship = (SA_ShipController)player;

            if (SA_Manager.instance.GetTarget(shipController, scanRange))
            {
                ship.FSM.PerformTransition(Transition.Attack);
            }
        }

        public override void Act(Component player)
        {
            SA_ShipController ship = (SA_ShipController)player;

            if (ship.ObstacleSystem.M_ObstacleState == SA_ObstacleSystem.ObstacleState.GoToEscapeDirection) return;

            if (Vector3.Distance(ship.transform.position, ship.Configuration.MainConfig.patrolPoint) > ship.Configuration.MainConfig.flyDistance)
            {
                ship.FollowTarget = true;
                ship.SetTarget(ship.Configuration.MainConfig.patrolPoint);
            }
            else
            {
                if (Time.time > timeState + rT && ship.ObstacleSystem.M_ObstacleState == SA_ObstacleSystem.ObstacleState.Scan)
                {
                    timeState = Time.time;
                    ship.FollowTarget = false;

                    if (ship.EnemyTarget)
                    {
                        ship.FSM.PerformTransition(Transition.Attack);
                    }
                }
            }
        }

        public override void DoBeforeLeaving()
        {
            timeState = 0;
        }
    }
}