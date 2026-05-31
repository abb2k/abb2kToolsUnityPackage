using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Abb2kTools
{
    public class Trigger
    {
        public bool Active { get; private set; } = false;

        public void Activate() => Active = true;
        public void Reset() => Active = false;

        public Trigger(bool startActive) => Active = startActive;
    }

    [System.Serializable]
    public class StateMachine
    {
        [SerializeField, HideInInspector]
        private State[] _states;
        public State[] States => _states;

        [SerializeField, HideInInspector]
        private UnityEvent<State> OnStateChanged;
        
        
        private Dictionary<string, object> _parameters = new();

        private State _currentState;
        public State CurrentState
        {
            get => _currentState;
            private set
            {
                _currentState?.Exit();
                _currentState = value;
                _currentState?.Enter();

                OnStateChanged?.Invoke(_currentState);

                CheckTransitions();
            }
        }

        public State GetState(string state)
        {
            foreach (var currState in _states)
            {
                if (!state.Equals(currState.GetID())) continue;

                return currState;
            }

            return null;
        }
        
        public void SetState(string stateEnum)
        {
            var state = GetState(stateEnum);
            if (state == null) return;

            CurrentState = state;
        }

        public bool TryGetParameter<T>(string name, out T param)
        {
            param = default;
            if (!_parameters.TryGetValue(name, out var paramO)) return false;

            if (paramO is not T paramVal) return false;

            param = paramVal;

            return true;
        }

        public bool TryGetBool(string name, out bool value) => TryGetParameter(name, out value);
        public bool TryGetInt(string name, out int value) => TryGetParameter(name, out value);
        public bool TryGetFloat(string name, out float value) => TryGetParameter(name, out value);
        public bool TryGetTrigger(string name, out Trigger value) => TryGetParameter(name, out value);

        public void SetParameter<T>(string name, T value)
        {
            if (_parameters.ContainsKey(name))
            {
                if (_parameters[name] is not T) return;

                _parameters[name] = value;
                OnParamChanged();
                return;
            }

            _parameters.Add(name, value);
            OnParamChanged();
        }

        public void SetBool(string name, bool value) => SetParameter(name, value);
        public void SetInt(string name, int value) => SetParameter(name, value);
        public void SetFloat(string name, float value) => SetParameter(name, value);
        public void RunTrigger(string name)
        {
            Trigger current;

            if (TryGetParameter(name, out current))
            {
                current.Activate();
                OnParamChanged();
            }
            else
            {
                current = new Trigger(true);
                SetParameter(name, current);
            }

            current.Reset();
        }

        private void OnParamChanged()
        {
            CheckTransitions();
        }

        private void CheckTransitions(int iterations = 0)
        {
            var toTransitionTo = CurrentState.EvaluateTransitionsWith(this);
            if (toTransitionTo == null) return;

            if (iterations >= 15)
            {
                return;
            }

            CurrentState = _states.First(s => s.GetID().Equals(toTransitionTo.DestinationState));

            CheckTransitions(iterations + 1);
        }
    }
}