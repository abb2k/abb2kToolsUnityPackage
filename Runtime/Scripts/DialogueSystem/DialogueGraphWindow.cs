using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Hosts the GraphView and chooses which Dialogue asset the editor is currently displaying.
public class DialogueGraphWindow : EditorWindow
{
    // Serialized so Unity can restore the selected graph when the editor window is reopened.
    [SerializeField] private Dialogue _target;
    private DialogueGraphView _graphView;
    private DialogueVariableWindow _variablesWindow;
    private bool _showVariablesWindow;
    private Vector2 _variablesPosition = new(12, 48);
    private Vector2 _variablesSize = new(300, 340);

    [MenuItem("Abb2kTools/Dialogue Graph")]
    public static void Open()
    {
        // Menu entry for opening an empty graph window; an asset can be selected afterward.
        var window = GetWindow<DialogueGraphWindow>();
        window.titleContent = new GUIContent("Dialogue Graph");
        window.minSize = new Vector2(800, 500);
        window.Rebuild();
    }

    
    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceId, int line)
    {
        // Unity calls this when an asset is opened; claim only Dialogue assets.
        if (EditorUtility.InstanceIDToObject(instanceId) is not Dialogue graph)
            return false;

        var window = GetWindow<DialogueGraphWindow>();
        window.titleContent = new GUIContent("Dialogue Graph");
        window.minSize = new Vector2(800, 500);
        window.SetTarget(graph);
        return true;
    }

    private void OnEnable() => Rebuild();

    public void SetTarget(Dialogue graph)
    {
        // Update both the window title and the graph contents when the selected asset changes.
        _target = graph;
        titleContent = new GUIContent(graph != null ? graph.name : "Dialogue Graph");
        Rebuild();
    }

    private void Rebuild()
    {
        // Clear old visual elements before drawing the current target.
        rootVisualElement.Clear();

        if (_target == null)
        {
            rootVisualElement.Add(new Label(
                "Double-click a Dialogue asset to edit it, or create one via " +
                "Assets > Create > Scriptable Objects > Dialogue.")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.Normal
                }
            });
            return;
        }

        _graphView = new DialogueGraphView(_target) { name = "Dialogue Graph" };
        _graphView.style.flexGrow = 1;

        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(ToggleVariablesWindow) { text = "Variables" });
        toolbar.Add(new ToolbarButton(() => _graphView.CreateAdditionalEntryNode(
            _graphView.contentViewContainer.WorldToLocal(_graphView.worldBound.center))) { text = "New Entry" });
        toolbar.Add(new ToolbarButton(() => _graphView.CreateAdditionalExitNode(
            _graphView.contentViewContainer.WorldToLocal(_graphView.worldBound.center) + new Vector2(220, 0))) { text = "New Exit" });

        // The toolbar creates a DialogueData sub-asset and places its node near the canvas center.
        toolbar.Add(new ToolbarButton(() =>
        {
            var center = _graphView.contentViewContainer.WorldToLocal(_graphView.worldBound.center);
            _graphView.CreateDialogueDataNode(center);
        }) { text = "New Dialogue Data" });

        // Extension point: add actions here for creating transitions or adding existing assets.
        var addRoomField = new ObjectField { objectType = typeof(DialogueData), allowSceneObjects = false };
        toolbar.Add(addRoomField);
        toolbar.Add(new ToolbarButton(() =>
        {
            _graphView.AddExistingRoom(addRoomField.value as DialogueData);
            addRoomField.value = null;
        }) { text = "Add Room" });

        var addLinkField = new ObjectField { objectType = typeof(DialogueLink), allowSceneObjects = false };
        toolbar.Add(addLinkField);
        toolbar.Add(new ToolbarButton(() =>
        {
            _graphView.AddExistingLink(addLinkField.value as DialogueLink);
            addLinkField.value = null;
        }) { text = "Add Link" });

        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.Add(toolbar);
        rootVisualElement.Add(_graphView);
        if (_showVariablesWindow)
            CreateVariablesWindow();
    }

    private void ToggleVariablesWindow()
    {
        if (_variablesWindow != null)
        {
            CloseVariablesWindow();
            return;
        }

        _showVariablesWindow = true;
        CreateVariablesWindow();
    }

    private void CreateVariablesWindow()
    {
        _variablesWindow = new DialogueVariableWindow(
            _target,
            rootVisualElement,
            _variablesPosition,
            _variablesSize,
            position => _variablesPosition = position,
            size => _variablesSize = size,
            CloseVariablesWindow,
            rebuildNodePorts => _graphView?.RefreshVariableNodes(rebuildNodePorts));
        rootVisualElement.Add(_variablesWindow);
        _variablesWindow.BringToFront();
    }

    private void CloseVariablesWindow()
    {
        if (_variablesWindow != null)
            rootVisualElement.Remove(_variablesWindow);
        _variablesWindow = null;
        _showVariablesWindow = false;
    }
}
