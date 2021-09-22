using SpaceAI.Ship;
using SpaceAI.ShipSystems;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class AttackState : FSMState
    {
        private SA_ShipController shipController = null;

        private int toClose;
        private float rangeAttackDirection;
        private int on;

        public AttackState(SA_ShipController obj) : base(obj)
        {
            stateID = StateID.Attack;
            shipController = obj.GetComponent<SA_ShipController>();

            if (shipController.Configuration.Options.useTurrets)
            {
                shipController.WeaponController.ResetTurrets();
            }
        }

        public override void DoBeforeEntering()
        {
            if (shipController.EnemyTarget)
            {
                if (shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.z > 150)
                    toClose = ((int)shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.x + (int)shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.z);
                if (shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.z > 50)
                    toClose = ((int)shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.x + (int)shipController.EnemyTarget.GetComponent<MeshFilter>().mesh.bounds.size.z) * 2;
                else
                    toClose = UnityEngine.Random.Range(30, 150) + (int)shipController.Configuration.MainConfig.MoveSpeed / 2;
            }

            rangeAttackDirection = UnityEngine.Random.Range(0.8F, 1.5F);
            on = UnityEngine.Random.Range(-1, 3);
        }

        public override void Act(Component player)
        {
            SA_ShipController ship = (SA_ShipController)player;

            if (ship.EnemyTarget)
            {
                Vector3 targetPosForShot = ship.EnemyTarget.transform.position + ship.EnemyTarget.transform.forward * ship.EnemyTarget.GetComponent<SA_BaseShip>().Configuration.MainConfig.MoveSpeed;
                ship.SetTarget(targetPosForShot);
                ship.FollowTarget = true;

                Vector3 dir = (targetPosForShot - player.transform.position).normalized;
                float dot = Mathf.Round(Vector3.Dot(dir, player.transform.forward * ship.Configuration.MainConfig.MoveSpeed));
                float attackDirection = Mathf.Round(ship.Configuration.MainConfig.MoveSpeed) - rangeAttackDirection;

                if (on >= 2) ship.Configuration.MainConfig.MoveSpeed = Mathf.Lerp(ship.Configuration.MainConfig.MoveSpeed, ship.Configuration.MainConfig.MoveSpeed / 2, Time.deltaTime * 5);

                if (dot >= attackDirection)
                {
                    if (!ship.Configuration.Options.useTurrets)
                    {
                        ship.WeaponController.LaunchWeapon();
                    }

                    if (ship.Configuration.Options.useTurrets && !ship.Configuration.Options.independentTurrets)
                    {
                        ship.WeaponController.TurretControl(targetPosForShot);
                    }
                }

                if (ship.Configuration.Options.useTurrets && ship.Configuration.Options.independentTurrets)
                {
                    ship.WeaponController.TurretControl(ship.EnemyTarget.transform, targetPosForShot);
                }
            }
        }

        public override void Reason(Component player)
        {
            SA_ShipController ship = (SA_ShipController)player;

            if (ship.ObstacleSystem.M_ObstacleState == SA_ObstacleSystem.ObstacleState.GoToEscapeDirection)
            {
                ship.FSM.PerformTransition(Transition.Patrol);
            }

            if (ship.EnemyTarget && Vector3.Distance(ship.transform.position, ship.EnemyTarget.transform.position) < toClose)
            {
                ship.FSM.PerformTransition(Transition.Turn);
            }

            if (!ship.EnemyTarget)
            {
                ship.FSM.PerformTransition(Transition.Patrol);
            }

            if (ship.ToFar())
            {
                ship.SetTarget(ship.Configuration.MainConfig.patrolPoint);
                ship.FSM.PerformTransition(Transition.Patrol);
            }
        }

        public override void DoBeforeLeaving()
        {
            if (shipController.Configuration.Options.useTurrets)
            {
                shipController.WeaponController.ResetTurrets();
            }
        }
    }
}