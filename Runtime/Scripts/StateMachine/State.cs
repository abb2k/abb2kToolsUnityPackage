
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools
{
    [System.Serializable]
    public class State
    {
        [SerializeField]
        private string ID;
        [SerializeField]
        private StateTransition[] _transitions;
        public StateTransition[] Transitions => _transitions;
        [SerializeField]
        private Vector2 _position;
        public Vector2 Position => _position;
        

        public event Action OnEnter;
        public event Action OnExit;

        public void Enter()
        {
            OnEnter?.Invoke();
        }

        public void Exit()
        {
            OnExit?.Invoke();
        }

        public State(string id)
        {
            this.ID = id;
        }

        public StateTransition EvaluateTransitionsWith(StateMachine stateMachine)
        {
            foreach (var transiton in _transitions)
            {
                if (transiton.CompareWith(stateMachine))
                    return transiton;
            }

            return null;
        }

        public string GetID() => ID;
    }
}