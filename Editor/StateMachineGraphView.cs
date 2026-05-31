using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abb2kTools
{
    public class StateMachineGraphView : GraphView
    {
        public StateMachineGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger()); 
            this.AddManipulator(new SelectionDragger()); 
            this.AddManipulator(new RectangleSelector()); 

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        public void PopulateView(StateMachine machine)
        {
            DeleteElements(graphElements);

            if (machine == null || machine.States == null) return;

            var stateDictionary = new Dictionary<State, StateNode>();

            foreach (var state in machine.States)
            {
                var node = new StateNode(state.GetID())
                {
                    StateData = state
                };
                
                node.SetPosition(new Rect(state.Position, Vector2.zero)); 
                
                stateDictionary.Add(state, node);
                AddElement(node);
            }

            foreach (var state in machine.States)
            {
                if (state.Transitions == null) continue;

                var sourceNode = stateDictionary[state];

                foreach (var transition in state.Transitions)
                {
                    var stateObj = machine.GetState(transition.DestinationState);
                    if (stateObj == null) continue;

                    if (stateDictionary.TryGetValue(stateObj, out StateNode targetNode))
                    {
                        var edge = sourceNode.OutputPort.ConnectTo(targetNode.InputPort);
                        AddElement(edge);
                    }
                }
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                {
                    compatiblePorts.Add(port);
                }
            });
            return compatiblePorts;
        }
    }
}