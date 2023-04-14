using SpaceAI.Ship;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.FSM
{
    public enum Transition
    {
        NullTransition, // Use this transition to represent a non-existing transition in your system
        Patrol,
        Attack,
        Turn,
    }

    public enum StateID
    {
        NullStateID,// Use this ID to represent a non-existing State in your system
        Idle,
        Attack,
        Turn,
    }

    [Serializable]
    public class FSMSystem 
    {
        private List<FSMState> states;

        public StateID CurrentStateID { get; private set; }
        public FSMState CurrentState { get; private set; }

        public FSMSystem()
        {
            states = new List<FSMState>();
        }

        /// <summary>
        /// Add new states inside the FSM
        /// </summary>
        public void AddState(FSMState s)
        {
            // Check for Null reference before deleting
            if (s == null)
            {
                Debug.LogError("FSM ERROR: Null reference is not allowed");
            }

            // First State inserted is also the Initial state
            if (states.Count == 0)
            {
                states.Add(s);
                CurrentState = s;
                CurrentStateID = s.ID;
                return;
            }

            // Add the state to the List if it's not inside it
            foreach (FSMState state in states)
            {
                if (state.ID == s.ID)
                {
                    Debug.LogError("FSM ERROR: Impossible to add state " + s.ID.ToString() +
                                   " because state has already been added");
                    return;
                }
            }

            states.Add(s);
        }

        /// <summary>
        /// This method delete a state from the FSM List  
        /// </summary>
        public void DeleteState(StateID id)
        {
            // Check for NullState before deleting
            if (id == StateID.NullStateID)
            {
                Debug.LogError("FSM ERROR: NullStateID is not allowed for a real state");
                return;
            }

            // Search the List and delete the state if it's inside it
            foreach (FSMState state in states)
            {
                if (state.ID == id)
                {
                    states.Remove(state);
                    return;
                }
            }
            Debug.LogError("FSM ERROR: Impossible to delete state " + id.ToString() +
                           ". It was not on the list of states");
        }

        /// <summary>
        /// Change the state in FSM
        /// </summary>
        public void PerformTransition(Transition trans)
        {
            // Check for NullTransition before changing the current state
            if (trans == Transition.NullTransition)
            {
                Debug.LogError("FSM ERROR: NullTransition is not allowed for a real transition");
                return;
            }

            // Check if the currentState has the transition passed as argument
            StateID id = CurrentState.GetOutputState(trans);

            if (id == StateID.NullStateID)
            {
                Debug.LogError("FSM ERROR: State " + CurrentStateID.ToString() + " does not have a target state " +
                               " for transition " + trans.ToString());
                return;
            }

            // Update the currentStateID and currentState      
            CurrentStateID = id;

            foreach (FSMState state in states)
            {
                if (state.ID == CurrentStateID)
                {
                    // Do the post processing of the state before setting the new one
                    CurrentState.DoBeforeLeaving();

                    CurrentState = state;

                    // Reset the state to its desired condition before it can reason or act
                    CurrentState.DoBeforeEntering();

                    break;
                }
            }
        }
    } 
}

