/// <summary>
/// The `AttackState` class is a specific state implementation for the finite state machine (FSM) used in AI behavior. It is part of the `SpaceAI.FSM` namespace.
/// Key components of the class include :
/// - `MinEnemyDistance`, `MaxEnemyDistance`, `CloseEnemyDistance`: Constants that define distance thresholds for enemy engagement.
/// - `toClose`, `rangeAttackDirection`, `sitOnTale`: Variables used for determining attack behavior.
/// - Constructor: Initializes the state ID and resets turrets if configured to use them.
/// - `DoBeforeEntering`: Sets up the state's initial conditions, such as determining the distance to the current enemy and attack behavior variables.
/// - `Act`: Controls the behavior of the object (ship) in the game world during the attack state. 
/// It calculates the target's future position, sets the target, and handles weapon firing and turret control.
/// - `CalculatePrediction`: Helper method to calculate the predicted future position of the target based on its velocity and the bullet speed.
/// - `Reason`: Determines if the state should transition to another state based on certain conditions. 
/// It checks if the way is free, the distance to the target, and if the ship is too far from the patrol point.
/// - `DoBeforeLeaving`: Performs any necessary actions or resets before leaving the state, such as resetting turrets if configured to use them.
/// The `AttackState` class extends the abstract `FSMState` class and implements the abstract `Reason` and `Act` methods, 
/// which define the specific behavior for the attack state.
/// </summary>
namespace SpaceAI.FSM
{
    using SpaceAI.Ship;
    using UnityEngine;
    public class SA_AttackState : SA_FSMState
    {
        private const float MinEnemyDistance = 80f;
        private const float MaxEnemyDistance = 200f;
        private const float CloseEnemyDistance = 50f;

        private int toClose;
        private float rangeAttackDirection;
        private int sitOnTale;

        public SA_AttackState(SA_IShip obj) : base(obj)
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

        private Vector3 CalculatePrediction(SA_IShip ship)
        {
            // Get the current ship's speed
            float ownerSpeed = owner.CurrentShipTransform.GetComponent<Rigidbody>().velocity.magnitude;

                      // Get the bullet speed from the owner's weapon
            float bulletSpeed = owner.WeaponControll.GetCurrentWeapon().BulletSpeed;

            // Calculate the effective bullet speed including the owner's speed
            float effectiveBulletSpeed = bulletSpeed + ownerSpeed;

            // Calculate the distance to the target
            float distanceToTarget = Vector3.Distance(ship.CurrentShipTransform.position, ship.CurrentEnemy.transform.position);

            // Calculate the time to intercept using the effective bullet speed
            float timeToIntercept = distanceToTarget / effectiveBulletSpeed;

            // Get the target's rigidbody component
            Rigidbody targetRigidbody = ship.CurrentEnemy.GetComponent<Rigidbody>();

            if (targetRigidbody == null)
            {
                // Return a default position or handle the error case
                return Vector3.zero;
            }

            // Get the target's velocity
            Vector3 targetVelocity = targetRigidbody.velocity;

            // Calculate the predicted future position of the target
            Vector3 targetFuturePos = ship.CurrentEnemy.transform.position + targetVelocity * timeToIntercept / owner.ShipConfiguration.MainConfig.Prediction;

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