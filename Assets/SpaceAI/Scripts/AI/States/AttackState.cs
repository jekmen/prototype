using SpaceAI.Core;
using SpaceAI.Ship;
using SpaceAI.ShipSystems;
using UnityEngine;

namespace SpaceAI.FSM
{
    public class AttackState : FSMState
    {
        private const float MinEnemyDistance = 80f;
        private const float MaxEnemyDistance = 200f;
        private const float CloseEnemyDistance = 50f;

        private int toClose;
        private float rangeAttackDirection;
        private int sitOnTale;

        public AttackState(IShip obj) : base(obj)
        {
            stateID = StateID.Attack;

            if (owner.ShipConfiguration.Options.useTurrets)
            {
                owner.WeaponControll.ResetTurrets();
            }
        }

        public override void DoBeforeEntering()
        {
            if (owner.CurrentEnemy)
            {
                var enemySizeZ = owner.CurrentEnemy.GetComponentInChildren<MeshFilter>().mesh.bounds.size.z;

                if (enemySizeZ > CloseEnemyDistance)
                {
                    toClose = (int)(enemySizeZ + owner.CurrentEnemy.GetComponentInChildren<MeshFilter>().mesh.bounds.size.x + owner.ShipConfiguration.MainConfig.MoveSpeed);
                }
                else
                {
                    toClose = Mathf.RoundToInt(UnityEngine.Random.Range(MinEnemyDistance, MaxEnemyDistance) + (owner.ShipConfiguration.MainConfig.MoveSpeed / 2f));
                }
            }

            sitOnTale = UnityEngine.Random.Range(1, 3);
            rangeAttackDirection = UnityEngine.Random.Range(0.8F, 1.5F);
        }

        public override void Act()
        {
            if (!owner.CurrentEnemy || !owner.WayIsFree()) return;

            Vector3 targetFuturePos = CalculatePrediction(owner);

            owner.SetTarget(targetFuturePos);

            if (owner.WayIsFree())
            {
                owner.CanFollowTarget(true);

                var dir = (targetFuturePos - owner.CurrentShipTransform.position).normalized;
                var dot = Mathf.Round(Vector3.Dot(dir, owner.CurrentShipTransform.transform.forward * owner.ShipConfiguration.MainConfig.MoveSpeed));
                var attackDirection = Mathf.Round(owner.ShipConfiguration.MainConfig.MoveSpeed) - rangeAttackDirection;

                if (sitOnTale >= 2) owner.ShipConfiguration.MainConfig.MoveSpeed = Mathf.Lerp(owner.ShipConfiguration.MainConfig.MoveSpeed, owner.ShipConfiguration.MainConfig.MoveSpeed / 2, Time.deltaTime * 5);

                if (dot >= attackDirection)
                {
                    if (!owner.ShipConfiguration.Options.useTurrets)
                    {
                        owner.WeaponControll.LaunchWeapons();
                    }

                    if (owner.ShipConfiguration.Options.useTurrets && !owner.ShipConfiguration.Options.independentTurrets)
                    {
                        owner.WeaponControll.TurretsControl(targetFuturePos);
                    }
                }
            }

            if (owner.ShipConfiguration.Options.useTurrets && owner.ShipConfiguration.Options.independentTurrets)
            {
                owner.WeaponControll.TurretsControl(owner.CurrentEnemy.transform, targetFuturePos);
            }
        }

        private Vector3 CalculatePrediction(IShip ship)
        {
            float bulletSpeed = owner.ShipConfiguration.MainConfig.Prediction;
            float distanceToTarget = Vector3.Distance(ship.CurrentShipTransform.position, ship.CurrentEnemy.transform.position);
            float timeToIntercept = distanceToTarget / bulletSpeed;
            Vector3 targetVelocity = ship.CurrentEnemy.GetComponent<Rigidbody>().velocity;
            Vector3 targetFuturePos = ship.CurrentEnemy.transform.position + targetVelocity * timeToIntercept;
            return targetFuturePos;
        }

        public override void Reason()
        {
            if (!owner.WayIsFree())
            {
                owner.CurrentAIProvider.FSM.PerformTransition(Transition.Patrol);

                return;
            }

            if (owner.CurrentEnemy)
            {
                var disToTarget = (owner.CurrentShipTransform.position - owner.CurrentEnemy.transform.position).magnitude;

                if (disToTarget < toClose)
                {
                    owner.CurrentAIProvider.FSM.PerformTransition(Transition.Turn);
                }
            }
            else
            {
                owner.CurrentAIProvider.FSM.PerformTransition(Transition.Patrol);
            }

            if (owner.ToFar())
            {
                owner.SetTarget(owner.ShipConfiguration.MainConfig.patrolPoint);

                owner.CurrentAIProvider.FSM.PerformTransition(Transition.Patrol);
            }
        }

        public override void DoBeforeLeaving()
        {
            if (owner.ShipConfiguration.Options.useTurrets)
            {
                owner.WeaponControll.ResetTurrets();
            }
        }
    }
}