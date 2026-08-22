using System;

namespace Abb2kTools.Commands
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HideInCommandInspectorAttribute : Attribute
    {
    }
}