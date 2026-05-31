using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abb2kTools
{
public class StateMachineWIndow : EditorWindow
{
    private StateMachineGraphView _graphView;
    private StateMachine _machine;

    [MenuItem("Window/State Machine Editor")]
    internal static void OpenWindow()
    {
        var window = GetWindow<StateMachineWIndow>();
        window.titleContent = new GUIContent($"State Machine Editor");
    }

    internal static void SetWindowTo(StateMachine machine)
    {
        var window = GetWindow<StateMachineWIndow>();
        window._machine = machine;

        window.PopulateGraph();
    }

    private void OnEnable()
    {
        ConstructGraphView();
    }

    private void ConstructGraphView()
    {
        _graphView = new StateMachineGraphView
        {
            name = "State Machine Graph"
        };
        
        _graphView.StretchToParentSize();
        rootVisualElement.Add(_graphView);
    }

    private void PopulateGraph()
    {
        _graphView.PopulateView(_machine);
    }
}
}