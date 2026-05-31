
using System;

namespace Abb2kTools
{
    public enum StateCallback 
    { 
        Enter, 
        Exit 
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class StateMachineCallbackAttribute : Attribute
    {
        public object State { get; }
        public StateCallback CallbackType { get; }

        public StateMachineCallbackAttribute(object state, StateCallback callbackType)
        {
            State = state;
            CallbackType = callbackType;
        }
    }
}