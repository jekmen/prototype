/// <summary>
/// The `FSMState` class is an abstract base class for defining states in a finite state machine. It is part of the `SpaceAI.FSM` namespace.
/// Key components of the class include :
///- `owner`: A reference to the `SA_IShip` object that owns the FSM.
///- `map`: A dictionary that maps transitions to state IDs.
///- `stateID`: The ID of the state.
///- `ID`: A property to access the state ID.
/// The class provides the following functionality:
///- `AddTransition`: Adds a transition and its associated state ID to the state's map.
///- `DeleteTransition`: Deletes a transition from the state's map.
///- `GetOutputState`: Retrieves the state ID associated with a given transition.
///- `DoBeforeEntering`: A method called before entering the state to set up the state's condition.
///- `DoBeforeLeaving`: A method called before leaving the state to perform any necessary actions or reset variables.
///- `Reason`: An abstract method that determines if the state should transition to another state based on the current conditions.
///- `Act`: An abstract method that controls the behavior of the object controlled by the state in the game world.
/// The `FSMState` class serves as a blueprint for creating specific states in a finite state machine.Subclasses inherit from this base class
/// and implement the `Reason` and `Act` methods to define the behavior of the object in different states.
/// Note: This class is part of a larger AI system and works in conjunction with other classes such as `FSMSystem`, 
/// `SA_IShip`, and the `Transition` and `StateID` enums to create a functional finite state machine for controlling AI behavior.
/// </summary>
namespace SpaceAI.FSM
{
    using SpaceAI.Ship;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public abstract class SA_FSMState
    {
        /// <summary>
        /// GameObject that has the FSMSystem attached. Npc must be set by FSMSystem.
        /// </summary>
        protected SA_IShip owner;
        protected Dictionary<Transition, StateID> map = new Dictionary<Transition, StateID>();
        protected StateID stateID;
        public StateID ID { get { return stateID; } }
	
	    protected SA_FSMState(SA_IShip npc)
	    {
		    owner = npc;
	    }

        public void AddTransition(Transition trans, StateID id)
        {
            // Check if anyone of the args is invalid
            if (trans == Transition.NullTransition)
            {
                Debug.LogError("FSMState ERROR: NullTransition is not allowed for a real transition");
                return;
            }

            if (id == StateID.NullStateID)
            {
                Debug.LogError("FSMState ERROR: NullStateID is not allowed for a real ID");
                return;
            }

            // Since this is a Deterministic FSM,
            //   check if the current transition was already inside the map
            if (map.ContainsKey(trans))
            {
                Debug.LogError("FSMState ERROR: State " + stateID.ToString() + " already has transition " + trans.ToString() + 
                               "Impossible to assign to another state");
                return;
            }

            map.Add(trans, id);
        }

        /// <summary>
        /// This method deletes a pair transition-state from this state's map.
        /// If the transition was not inside the state's map, an ERROR message is printed.
        /// </summary>
        public void DeleteTransition(Transition trans)
        {
            // Check for NullTransition
            if (trans == Transition.NullTransition)
            {
                Debug.LogError("FSMState ERROR: NullTransition is not allowed");
                return;
            }

            // Check if the pair is inside the map before deleting
            if (map.ContainsKey(trans))
            {
                map.Remove(trans);
                return;
            }
            Debug.LogError("FSMState ERROR: Transition " + trans.ToString() + " passed to " + stateID.ToString() + 
                           " was not on the state's transition list");
        }

        /// <summary>
        /// This method returns the new state the FSM should be if
        ///    this state receives a transition and 
        /// </summary>
        public StateID GetOutputState(Transition trans)
        {
            // Check if the map has this transition
            if (map.ContainsKey(trans))
            {
                return map[trans];
            }
            return StateID.NullStateID;
        }

        /// <summary>
        /// This method is used to set up the State condition before entering it.
        /// It is called automatically by the FSMSystem class before assigning it
        /// to the current state.
        /// </summary>
        public virtual void DoBeforeEntering() { }

        /// <summary>
        /// This method is used to make anything necessary, as reseting variables
        /// before the FSMSystem changes to another one. It is called automatically
        /// by the FSMSystem before changing to a new state.
        /// </summary>
        public virtual void DoBeforeLeaving() { } 

        /// <summary>
        /// This method decides if the state should transition to another on its list
        /// NPC is a reference to the object that is controlled by this class
        /// </summary>
        public abstract void Reason();

        /// <summary>
        /// This method controls the behavior of the NPC in the game World.
        /// Every action, movement or communication the NPC does should be placed here
        /// NPC is a reference to the object that is controlled by this class
        /// </summary>
        public abstract void Act();
    } 
}
