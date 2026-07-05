using UnityEngine;

namespace Abb2kTools
{
    // 1. The Attribute for custom parameter names
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
    public class InstancedEventParamsAttribute : System.Attribute
    {
        public string[] ParameterNames { get; private set; }

        public InstancedEventParamsAttribute(params string[] parameterNames)
        {
            ParameterNames = parameterNames;
        }
    }
}