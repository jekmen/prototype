/// <summary>
/// The `SA_AIProvider` class is part of the `SpaceAI.FSM` namespace and serves as a provider for AI functionality within the game. 
/// It manages the Finite State Machine (FSM) for controlling the behavior of an AI-controlled ship.
/// The class has the following key components:
/// - `ship`: An instance of the `SA_IShip` interface representing the ship controlled by the AI provider.
/// - `fsm`: An instance of the FSMSystem class that encapsulates the FSM for the AI behavior.
/// The `CreateBaseModelFSM` method is responsible for setting up the FSM with the required states and transitions. 
/// It creates and adds the following states to the FSM:
/// - `IdleState`: Represents the idle state of the AI ship. It has transitions for transitioning to the patrol, attack, and turn states.
/// - `AttackState`: Represents the attack state of the AI ship. It has transitions for transitioning to the patrol and turn states.
/// - `TurnState`: Represents the turn state of the AI ship. It has transitions for transitioning to the turn, attack, and idle states.
/// The `LoopStates` method is used to execute the actions and reasoning of the current state in the FSM. 
/// It calls the `Act` method of the current state to perform actions and the `Reason` method to evaluate transitions and make decisions.
/// Overall, the `SA_AIProvider` class provides the foundation for managing the FSM-based AI behavior of a ship in the game, 
/// allowing it to perform different actions based on the current state and transitions defined in the FSM.
/// </summary>
namespace SpaceAI.FSM
{
    using SpaceAI.Ship;
    using System;

    [Serializable]
    public class SA_AIProvider
    {
        private readonly SA_IShip ship;
        private SA_FSMSystem fsm;

        public SA_FSMSystem FSM => fsm;

        public SA_AIProvider(SA_IShip ship)
        {
            this.ship = ship;
        }

        public void CreateBaseModelFSM()
        {
            fsm = new SA_FSMSystem();

            SA_IdleState idleState = new(ship);
            idleState.AddTransition(Transition.Patrol, StateID.Idle);
            idleState.AddTransition(Transition.Attack, StateID.Attack);
            idleState.AddTransition(Transition.Turn, StateID.Turn);

            SA_AttackState attackState = new(ship);
            attackState.AddTransition(Transition.Patrol, StateID.Idle);
            attackState.AddTransition(Transition.Turn, StateID.Turn);

            SA_TurnState turnState = new(ship);
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