using SpaceAI.FSM;
using UnityEngine;

namespace SpaceAI.Ship
{
    public class SA_ShipController : SA_BaseShip
    {
        public FSMSystem FSM { get; private set; }
        public GameObject EnemyTarget { get; set; }
        public Vector3 SetVelocityTarget(Vector3 vector) { VelocityTarget = vector; return VelocityTarget; }
        public Vector3 VelocityTarget { get; set; } = Vector3.zero;

        protected override void Start()
        {
            base.Start();

            CreateBaseFSM();
        }

        private void CreateBaseFSM()
        {
            FSM = new FSMSystem(this);

            IdleState idleState = new IdleState(this);
            idleState.AddTransition(Transition.Patrol, StateID.Idle);
            idleState.AddTransition(Transition.Attack, StateID.Attack);
            idleState.AddTransition(Transition.Turn, StateID.Turn);

            AttackState attackState = new AttackState(this);
            attackState.AddTransition(Transition.Patrol, StateID.Idle);
            attackState.AddTransition(Transition.Turn, StateID.Turn);

            TurnState turnState = new TurnState(this);
            turnState.AddTransition(Transition.Turn, StateID.Turn);
            turnState.AddTransition(Transition.Attack, StateID.Attack);
            turnState.AddTransition(Transition.Patrol, StateID.Idle);

            FSM.AddState(idleState);
            FSM.AddState(attackState);
            FSM.AddState(turnState);
        }

        private void FixedUpdate()
        {
            FSM.CurrentState.Act(this);
            FSM.CurrentState.Reason(this);

            Move();
        }

        protected override void Move()
        {
            if (FollowTarget)
            {
                Vector3 relativePoint = transform.InverseTransformPoint(GetTarget()).normalized;

                if (ShipSize < 50)
                {
                    Configuration.MainConfig.MainRot = Quaternion.LookRotation(GetTarget() - transform.position);
                }
                else
                {
                    Configuration.MainConfig.MainRot = Quaternion.LookRotation((GetTarget() + Vector3.left * Configuration.MainConfig.MoveSpeed) * 2 - transform.position);
                }

                Rb.rotation = Quaternion.Lerp(Rb.rotation, Configuration.MainConfig.MainRot, Time.deltaTime * Configuration.MainConfig.RotationSpeed);
                Rb.rotation *= Quaternion.Euler(-relativePoint.y * (Time.deltaTime * Configuration.MainConfig.PichSens) * 10, 0, -relativePoint.x * (Time.deltaTime * Configuration.MainConfig.YawSens) * 10);
            }

            MovmentCalculation();
        }

        private void MovmentCalculation()
        {
            Configuration.MainConfig.MoveSpeed = Mathf.Lerp(Configuration.MainConfig.MoveSpeed, Configuration.MainConfig.Speed, Time.deltaTime / 10);
            Rb.velocity = Vector3.Lerp(Rb.velocity, VelocityTarget, Time.deltaTime * 2);
            SetVelocityTarget((Rb.rotation * Vector3.forward) * (Configuration.MainConfig.Speed + Configuration.MainConfig.MoveSpeed));
        }

        public bool ToFar()
        {
            if (Vector3.Distance(transform.position, Configuration.MainConfig.patrolPoint) > Configuration.MainConfig.flyDistance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}