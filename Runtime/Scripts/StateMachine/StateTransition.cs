
using System;

namespace Abb2kTools
{
    [System.Serializable]
    public class StateTransition
    {
        public string DestinationState;
        public int Priority;
        public StateTransitionConditionOpaque[] conditions;

        public bool CompareWith(StateMachine machine)
        {
            foreach (var condition in conditions)
            {
                if (!condition.Compare(machine)) return false;
            }

            return true;
        }

        public StateTransition(string destinationState, int priority, params StateTransitionConditionOpaque[] conditions)
        {
            DestinationState = destinationState;
            Priority = priority;
            this.conditions = conditions;
        }

        public StateTransition(string destinationState, params StateTransitionConditionOpaque[] conditions) : this(destinationState, 0, conditions) { }
    }
}