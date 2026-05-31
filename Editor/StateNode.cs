using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Abb2kTools
{
    public class StateNode : Node
    {
        public Port InputPort;
        public Port OutputPort;
        
        public State StateData;

        public StateNode(string stateName)
        {
            title = stateName;

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}