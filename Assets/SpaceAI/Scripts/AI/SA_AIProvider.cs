namespace SpaceAI.FSM
{
    using SpaceAI.Ship;
    using System;

    [Serializable]
    public class SA_AIProvider
    {
        private IShip ship;
        private FSMSystem fsm;

        public FSMSystem FSM => fsm;

        public SA_AIProvider(IShip ship)
        {
            this.ship = ship;
        }

        public void CreateBaseModelFSM()
        {
            fsm = new FSMSystem();

            IdleState idleState = new IdleState(ship);
            idleState.AddTransition(Transition.Patrol, StateID.Idle);
            idleState.AddTransition(Transition.Attack, StateID.Attack);
            idleState.AddTransition(Transition.Turn, StateID.Turn);

            AttackState attackState = new AttackState(ship);
            attackState.AddTransition(Transition.Patrol, StateID.Idle);
            attackState.AddTransition(Transition.Turn, StateID.Turn);

            TurnState turnState = new TurnState(ship);
            turnState.AddTransition(Transition.Turn, StateID.Turn);
            turnState.AddTransition(Transition.Attack, StateID.Attack);
            turnState.AddTransition(Transition.Patrol, StateID.Idle);

            fsm.AddState(idleState);
            fsm.AddState(attackState);
            fsm.AddState(turnState);
        }

        public void LoopStates()
        {
            fsm.CurrentState.Act();

            fsm.CurrentState.Reason();
        }
    }
}