using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

// Data attached to a standard GraphView Edge for dialogue-specific behavior.
public class DialogueTransitionEdgeData
{
    public DialogueLink transition;
    public bool startsAtEntry;
    public bool endsAtExit;
    public DialogueNodeSide outputSide;
    public DialogueNodeSide inputSide;
    public DialogueTransitionStroke stroke;
}

public sealed class DialogueTransitionStroke : VisualElement
{
    private readonly DialogueGraphView _graph;
    private readonly Edge _edge;
    private readonly float _lineWidth;
    private readonly Color _lineColor;

    public DialogueTransitionStroke(DialogueGraphView graph, Edge edge, float lineWidth, Color lineColor)
    {
        _graph = graph;
        _edge = edge;
        _lineWidth = lineWidth;
        _lineColor = lineColor;
        style.position = Position.Absolute;
        style.left = 0;
        style.right = 0;
        style.top = 0;
        style.bottom = 0;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext context)
    {
        var route = _graph.GetTransitionPath(_edge, out _);
        if (route.Count < 2) return;

        var edgeTransform = worldTransform.inverse;
        var graphTransform = _graph.contentViewContainer.worldTransform;
        var localRoute = new List<Vector2>(route.Count);
        foreach (var graphPoint in route)
        {
            var worldPoint = graphTransform.MultiplyPoint3x4(new Vector3(graphPoint.x, graphPoint.y, 0));
            var localPoint = edgeTransform.MultiplyPoint3x4(worldPoint);
            localRoute.Add(new Vector2(localPoint.x, localPoint.y));
        }

        var start = localRoute[0];
        var end = localRoute[localRoute.Count - 1];
        var direction = (end - localRoute[localRoute.Count - 2]).normalized; 
        if (direction.sqrMagnitude == 0f) return;

        const float arrowLength = 10f;
        const float arrowHalfWidth = 5f;
        var arrowBase = end - direction * arrowLength;
        var perpendicular = new Vector2(-direction.y, direction.x) * arrowHalfWidth;
        var edgeData = _edge.userData as DialogueTransitionEdgeData;
        var defaultColor = _edge.selected ? _edge.selectedColor : _lineColor;
        var entryColor = _edge.selected ? _edge.selectedColor : new Color(0.2f, 0.85f, 0.32f, 1f);
        var exitColor = _edge.selected ? _edge.selectedColor : new Color(0.95f, 0.24f, 0.22f, 1f);
        var painter = context.painter2D;

        painter.lineWidth = _lineWidth;
        if (edgeData?.startsAtEntry == true && edgeData.endsAtExit)
        {
            var half = GetHalfwayRoute(localRoute, out var routeIndex);
            DrawRoute(painter, localRoute, 0, routeIndex, half, entryColor);
            DrawRoute(painter, localRoute, routeIndex, localRoute.Count - 1, half, exitColor);
        }
        else
        {
            var routeColor = edgeData?.endsAtExit == true ? exitColor :
                edgeData?.startsAtEntry == true ? entryColor : defaultColor;
            DrawRoute(painter, localRoute, 0, localRoute.Count - 1, null, routeColor);
        }

        var arrowColor = edgeData?.endsAtExit == true ? exitColor :
            edgeData?.startsAtEntry == true ? entryColor : defaultColor;
        painter.fillColor = arrowColor;
        painter.BeginPath();
        painter.MoveTo(end);
        painter.LineTo(arrowBase + perpendicular);
        painter.LineTo(arrowBase - perpendicular);
        painter.ClosePath();
        painter.Fill();
    }

    private static void DrawRoute(Painter2D painter, List<Vector2> route, int startIndex, int endIndex, Vector2? splitPoint, Color color)
    {
        painter.strokeColor = color;
        painter.BeginPath();
        painter.MoveTo(startIndex == 0 ? route[0] : splitPoint.Value);
        if (startIndex > 0)
            for (int i = startIndex + 1; i <= endIndex; i++) painter.LineTo(route[i]);
        else
            for (int i = 1; i <= endIndex; i++) painter.LineTo(route[i]);
        if (splitPoint.HasValue && startIndex == 0) painter.LineTo(splitPoint.Value);
        painter.Stroke();
    }

    private static Vector2 GetHalfwayRoute(List<Vector2> route, out int routeIndex)
    {
        float totalLength = 0;
        for (int i = 1; i < route.Count; i++) totalLength += Vector2.Distance(route[i - 1], route[i]);
        float halfway = totalLength * 0.5f;
        float traversed = 0;
        for (int i = 1; i < route.Count; i++)
        {
            float segmentLength = Vector2.Distance(route[i - 1], route[i]);
            if (traversed + segmentLength >= halfway)
            {
                routeIndex = i;
                return Vector2.Lerp(route[i - 1], route[i], (halfway - traversed) / segmentLength);
            }
            traversed += segmentLength;
        }
        routeIndex = route.Count - 1;
        return route[routeIndex];
    }
}

public sealed class DialogueCurvePointHandle : GraphElement
{
    public readonly DialogueLink transition;
    public readonly DialogueCurvePoint point;
    private readonly System.Action<Vector2> _move;
    private readonly System.Action _setLinear;
    private readonly System.Action _setEased;
    private readonly System.Action _delete;
    private bool _suppressMoveCallback;
    private bool _selected;

    public DialogueCurvePointHandle(
        DialogueLink transition,
        DialogueCurvePoint point,
        System.Action<Vector2> move,
        System.Action setLinear,
        System.Action setEased,
        System.Action delete)
    {
        this.transition = transition;
        this.point = point;
        _move = move;
        _setLinear = setLinear;
        _setEased = setEased;
        _delete = delete;

        viewDataKey = $"{DialogueGraphLayout.GetAssetKey(transition)}:curve:{point.id}";
        capabilities = Capabilities.Selectable | Capabilities.Movable | Capabilities.Groupable;

        style.position = Position.Absolute;
        style.width = 12;
        style.height = 12;
        style.borderTopLeftRadius = 6;
        style.borderTopRightRadius = 6;
        style.borderBottomLeftRadius = 6;
        style.borderBottomRightRadius = 6;
        style.backgroundColor = new Color(1f, 0.72f, 0.2f, 1f);
        style.borderLeftWidth = 2;
        style.borderRightWidth = 2;
        style.borderTopWidth = 2;
        style.borderBottomWidth = 2;
        UpdateSelectionStyle();
        focusable = true;

        RegisterCallback<MouseDownEvent>(OnMouseDown);
    }

    public void SetGraphPosition(Vector2 position)
    {
        _suppressMoveCallback = true;
        SetPosition(new Rect(position - new Vector2(6f, 6f), new Vector2(12f, 12f)));
        _suppressMoveCallback = false;
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        if (_suppressMoveCallback) return;

        point.position = newPos.center;
        Undo.RecordObject(transition, "Move Transition Curve Point");
        EditorUtility.SetDirty(transition);
        _move?.Invoke(point.position);
    }

    public void SetPointMode(DialogueCurvePointMode mode)
    {
        style.backgroundColor = mode == DialogueCurvePointMode.Eased
            ? new Color(1f, 0.72f, 0.2f, 1f)
            : new Color(0.35f, 0.82f, 0.95f, 1f);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        UpdateSelectionStyle();
    }

    private void UpdateSelectionStyle()
    {
        var borderColor = _selected ? Color.white : Color.black;
        style.borderLeftColor = borderColor;
        style.borderRightColor = borderColor;
        style.borderTopColor = borderColor;
        style.borderBottomColor = borderColor;
    }

    private void OnContextMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Point Mode/Linear", _ => _setLinear?.Invoke());
        evt.menu.AppendAction("Point Mode/Eased", _ => _setEased?.Invoke());
        evt.menu.AppendAction("Delete Point", _ => _delete?.Invoke());
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 1) return;

        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Linear"), point.mode == DialogueCurvePointMode.Linear, () => _setLinear?.Invoke());
        menu.AddItem(new GUIContent("Eased"), point.mode == DialogueCurvePointMode.Eased, () => _setEased?.Invoke());
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Delete Point"), false, () => _delete?.Invoke());
        menu.ShowAsContext();
        evt.StopImmediatePropagation();
    }
}

public sealed class DialogueTransitionPreview : VisualElement
{
    private Vector2 _start;
    private Vector2 _end;

    public DialogueTransitionPreview()
    {
        style.position = Position.Absolute;
        style.left = 0;
        style.right = 0;
        style.top = 0;
        style.bottom = 0;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    public void SetPoints(Vector2 start, Vector2 end)
    {
        _start = start;
        _end = end;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        var direction = (_end - _start).normalized;
        if (direction.sqrMagnitude == 0f) return;

        const float arrowLength = 10f;
        const float arrowHalfWidth = 5f;
        var arrowBase = _end - direction * arrowLength;
        var perpendicular = new Vector2(-direction.y, direction.x) * arrowHalfWidth;
        var painter = context.painter2D;
        var color = new Color(1f, 0.82f, 0.28f, 0.9f);

        painter.strokeColor = color;
        painter.lineWidth = 2f;
        painter.BeginPath();
        painter.MoveTo(_start);
        painter.LineTo(_end);
        painter.Stroke();

        painter.fillColor = color;
        painter.BeginPath();
        painter.MoveTo(_end);
        painter.LineTo(arrowBase + perpendicular);
        painter.LineTo(arrowBase - perpendicular);
        painter.ClosePath();
        painter.Fill();
    }
}

public sealed class DialogueStartNode : Node
{
    private readonly Dialogue _dialogue;
    public readonly Port output;

    public DialogueStartNode(Dialogue dialogue)
    {
        _dialogue = dialogue;
        _dialogue.startOutput ??= new DialogueExecPortData { label = "Start" };
        title = "Dialogue Start";
        titleContainer.style.backgroundColor = new Color(0.12f, 0.48f, 0.2f, 1f);
        viewDataKey = $"{DialogueGraphLayout.GetAssetKey(dialogue)}:start";
        output = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(DialogueExecPortData));
        output.portName = _dialogue.startOutput.label;
        output.userData = _dialogue.startOutput.id;
        outputContainer.Add(output);
        style.width = 180;
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(_dialogue.startNodePosition, new Vector2(180, 80)));
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        if (_dialogue.startNodePosition == newPos.position) return;
        Undo.RecordObject(_dialogue, "Move Dialogue Start");
        _dialogue.startNodePosition = newPos.position;
        EditorUtility.SetDirty(_dialogue);
    }
}

public sealed class DialogueExitNode : Node
{
    private readonly Dialogue _dialogue;
    public readonly Port input;

    public DialogueExitNode(Dialogue dialogue)
    {
        _dialogue = dialogue;
        _dialogue.exitInput ??= new DialogueExecPortData { label = "Exit" };
        title = "Dialogue Exit";
        titleContainer.style.backgroundColor = new Color(0.58f, 0.16f, 0.15f, 1f);
        viewDataKey = $"{DialogueGraphLayout.GetAssetKey(dialogue)}:exit";
        input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(DialogueExecPortData));
        input.portName = _dialogue.exitInput.label;
        input.userData = _dialogue.exitInput.id;
        inputContainer.Add(input);
        style.width = 180;
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(_dialogue.exitNodePosition, new Vector2(180, 80)));
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        if (_dialogue.exitNodePosition == newPos.position) return;
        Undo.RecordObject(_dialogue, "Move Dialogue Exit");
        _dialogue.exitNodePosition = newPos.position;
        EditorUtility.SetDirty(_dialogue);
    }
}

public sealed class DialogueAdditionalEntryNode : Node
{
    private readonly Dialogue _dialogue;
    public readonly DialogueEntryData data;
    public readonly Port output;

    public DialogueAdditionalEntryNode(Dialogue dialogue, DialogueEntryData data)
    {
        _dialogue = dialogue;
        this.data = data;
        data.output ??= new DialogueExecPortData { label = "Start" };
        title = data.name;
        titleContainer.style.backgroundColor = new Color(0.12f, 0.48f, 0.2f, 1f);
        viewDataKey = $"{DialogueGraphLayout.GetAssetKey(dialogue)}:entry:{data.id}";
        output = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(DialogueExecPortData));
        output.portName = data.output.label;
        output.userData = data.output.id;
        outputContainer.Add(output);
        style.width = 180;
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(data.position, new Vector2(180, 80)));
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        if (data.position == newPos.position) return;
        Undo.RecordObject(_dialogue, "Move Dialogue Entry");
        data.position = newPos.position;
        EditorUtility.SetDirty(_dialogue);
    }
}

public sealed class DialogueAdditionalExitNode : Node
{
    private readonly Dialogue _dialogue;
    public readonly DialogueExitData data;
    public readonly Port input;

    public DialogueAdditionalExitNode(Dialogue dialogue, DialogueExitData data)
    {
        _dialogue = dialogue;
        this.data = data;
        data.input ??= new DialogueExecPortData { label = "Exit" };
        title = data.name;
        titleContainer.style.backgroundColor = new Color(0.58f, 0.16f, 0.15f, 1f);
        viewDataKey = $"{DialogueGraphLayout.GetAssetKey(dialogue)}:exit:{data.id}";
        input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(DialogueExecPortData));
        input.portName = data.input.label;
        input.userData = data.input.id;
        inputContainer.Add(input);
        style.width = 180;
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(data.position, new Vector2(180, 80)));
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        if (data.position == newPos.position) return;
        Undo.RecordObject(_dialogue, "Move Dialogue Exit");
        data.position = newPos.position;
        EditorUtility.SetDirty(_dialogue);
    }
}

// Editor-only GraphView that projects a Dialogue asset's data into draggable nodes and edges.
public class DialogueGraphView : GraphView
{
    // These maps let graph operations find the backing ScriptableObject for each visual element.
    private readonly Dialogue _dialogue;
    private readonly Dictionary<DialogueData, DialogueNode> _roomNodes = new();
    private readonly Dictionary<string, DialogueAdditionalEntryNode> _additionalEntryNodes = new();
    private readonly Dictionary<string, DialogueAdditionalExitNode> _additionalExitNodes = new();
    private DialogueStartNode _startNode;
    private DialogueExitNode _exitNode;
    private readonly Dictionary<DialogueLink, List<DialogueCurvePointHandle>> _curvePointHandles = new();
    private readonly Dictionary<Edge, Label> _overlapBadges = new();
    private DialogueCurvePointHandle _selectedCurvePointHandle;
    private List<DialogueLink> _lastOverlapCycle = new();
    private int _overlapCycleIndex = -1;
    private bool _isRebuilding;
    private bool _preservingGroupContents;
    private bool _adjustingGroupBounds;
    private bool _movingGroup;
    private int _pointerUndoGroup = -1;

    public DialogueGraphView(Dialogue dialogue) : base()
    {
        _dialogue = dialogue;

        // GraphView's built-in manipulators provide zooming, panning, selection, and box selection.
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.AddManipulator(new ContentZoomer());
        this.AddManipulator(new ContextualMenuManipulator(BuildGraphContextMenu));
        RegisterCallback<MouseDownEvent>(OnGraphMouseDown, TrickleDown.TrickleDown);
        RegisterCallback<MouseUpEvent>(OnGraphMouseUp);
        RegisterCallback<KeyDownEvent>(OnGraphKeyDown);
        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

        // The grid is inserted behind nodes and stretched to cover the graph canvas.
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // Extension point: load additional USS here when the graph needs custom visual styling.
        StyleSheet styleSheet = (StyleSheet)EditorGUIUtility.Load("packages/com.abb2k.abb2ktools/Runtime/Scripts/DialogueSystem/GraphViewStyles.uss");

        styleSheets.Add(styleSheet);

        graphViewChanged = OnGraphViewChanged;
        elementsAddedToGroup += OnElementsAddedToGroup;
        elementsRemovedFromGroup += OnElementsRemovedFromGroup;
        groupTitleChanged += OnGroupTitleChanged;

        RebuildGraph();
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports
            .Where(port => port != startPort &&
                port.node != startPort.node &&
                port.direction != startPort.direction &&
                port.portType == typeof(DialogueExecPortData))
            .ToList();
    }

    private int BeginUndoGroup(string actionName)
    {
        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(actionName);
        return undoGroup;
    }

    private void OnGraphMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0) return;

        var curvePointHandle = FindCurvePointHandle(evt.target as VisualElement);
        if (curvePointHandle != null)
        {
            _selectedCurvePointHandle = curvePointHandle;
            Selection.activeObject = curvePointHandle.transition;
                _pointerUndoGroup = BeginUndoGroup("Move Transition Curve Points");
            foreach (var handle in selection.OfType<DialogueCurvePointHandle>().Append(curvePointHandle).Distinct())
                    Undo.RecordObject(handle.transition, "Move Transition Curve Points");
            return;
        }
        SelectCurvePointHandle(null);

        if (evt.clickCount >= 2 &&
            FindCurvePointHandle(evt.target as VisualElement) == null &&
            TryAddTransitionCurvePoint(evt.mousePosition))
        {
            evt.StopImmediatePropagation();
            return;
        }

        if (TrySelectTransitionAt(evt.mousePosition))
        {
            evt.StopImmediatePropagation();
            return;
        }

        var target = FindGraphElement(evt.target as VisualElement);
        if (target is not (DialogueNode or DialogueStartNode or DialogueExitNode or
            DialogueAdditionalEntryNode or DialogueAdditionalExitNode or DialogueGroup)) return;

        _pointerUndoGroup = BeginUndoGroup("Edit Dialogue Graph");
        Undo.RecordObject(DialogueGraphLayout.instance, "Edit Dialogue Graph");
    }

    private void OnGraphMouseUp(MouseUpEvent evt)
    {
        var selectedCurvePoint = selection.OfType<DialogueCurvePointHandle>().LastOrDefault();
        if (selectedCurvePoint != null)
            Selection.activeObject = selectedCurvePoint.transition;

        var selectedTransition = selection.OfType<Edge>()
            .FirstOrDefault(edge => edge.userData is DialogueTransitionEdgeData);
        if (selectedTransition?.userData is DialogueTransitionEdgeData selectedData && selectedData.transition != null)
        {
            Selection.activeObject = selectedData.transition;
        }
        else if (selection.OfType<DialogueNode>().FirstOrDefault() is { } selectedNode)
        {
            Selection.activeObject = selectedNode.room;
        }

        RefreshTransitionStrokeSelection();
        RefreshCurvePointSelectionFromNodeSelection();
        RefreshTransitionEdgeAnchors();

        if (_pointerUndoGroup < 0) return;

        Undo.CollapseUndoOperations(_pointerUndoGroup);
        _pointerUndoGroup = -1;
        DialogueGraphLayout.instance.SaveGroups();
        AssetDatabase.SaveAssets();
        AssetDatabase.SaveAssets();
    }

    private void OnGraphKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode is not (KeyCode.Delete or KeyCode.Backspace)) return;

        var selectedPoints = selection.OfType<DialogueCurvePointHandle>().ToList();
        if (selectedPoints.Count == 0 && _selectedCurvePointHandle != null)
            selectedPoints.Add(_selectedCurvePointHandle);
        if (selectedPoints.Count == 0) return;

        foreach (var handle in selectedPoints)
            DeleteCurvePoint(handle.transition, handle.point);
        evt.StopPropagation();
    }

    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        viewTransformChanged -= OnViewTransformChanged;
        viewTransformChanged += OnViewTransformChanged;

        if (DialogueGraphLayout.instance.TryGetView(_dialogue, out var position, out var scale))
            UpdateViewTransform(position, scale);
    }

    private void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        viewTransformChanged -= OnViewTransformChanged;
        SaveViewTransform();
        DialogueGraphLayout.instance.SaveGroups();
    }

    private void OnViewTransformChanged(GraphView graphView)
    {
        SaveViewTransform();
    }

    private void SaveViewTransform()
    {
        DialogueGraphLayout.instance.SetView(_dialogue, viewTransform.position, viewTransform.scale);
    }

    private void OnUndoRedoPerformed()
    {
        _pointerUndoGroup = -1;
        DialogueGraphLayout.instance.SaveGroups();
        AssetDatabase.SaveAssets();
        RebuildGraph();
    }

    private static DialogueNode FindDialogueNode(VisualElement element)
    {
        while (element != null)
        {
            if (element is DialogueNode node) return node;
            element = element.parent;
        }

        return null;
    }

    private static GraphElement FindGraphElement(VisualElement element)
    {
        while (element != null)
        {
            if (element is GraphElement graphElement) return graphElement;
            element = element.parent;
        }

        return null;
    }

    private void BuildGraphContextMenu(ContextualMenuPopulateEvent evt)
    {
        // Convert the mouse's world position to graph-content coordinates for placement.
        var position = contentViewContainer.WorldToLocal(evt.mousePosition);

        var selectedNodes = selection.OfType<GraphElement>()
            .Where(e => e is DialogueNode or DialogueStartNode or DialogueExitNode or
                DialogueAdditionalEntryNode or DialogueAdditionalExitNode or DialogueCurvePointHandle)
            .ToList();

        if (selectedNodes.Count > 0)
            evt.menu.AppendAction("Group Selection", _ => CreateGroupAroundSelection(selectedNodes));

        evt.menu.AppendAction("Create Dialogue Data", _ => CreateDialogueDataNode(position));
        evt.menu.AppendAction("Create Selection Node", _ => CreateDialogueSelectorNode(position));
        evt.menu.AppendAction("Create Entry", _ => CreateAdditionalEntryNode(position));
        evt.menu.AppendAction("Create Exit", _ => CreateAdditionalExitNode(position));
        evt.menu.AppendAction("Create Group", _ => CreateGroup("New Group", position, new Vector2(300, 200)));
    }

    // Creates DialogueData as a sub-asset, then registers it so the node survives graph rebuilds.
    public void CreateDialogueDataNode(Vector2 position) => CreateRoomNode<DialogueData>("DialogueData", position, isSelectorOnly: false);

    // Creates a standalone branch node that only exposes selection options, no dialogue content.
    public void CreateDialogueSelectorNode(Vector2 position) => CreateRoomNode<DialogueSelectorData>("Selector", position, isSelectorOnly: true);

    public void CreateAdditionalEntryNode(Vector2 position)
    {
        Undo.RecordObject(_dialogue, "Create Dialogue Entry");
        _dialogue.additionalEntries ??= new List<DialogueEntryData>();
        var data = new DialogueEntryData { name = $"Entry {_dialogue.additionalEntries.Count + 1}", position = position };
        _dialogue.additionalEntries.Add(data);
        var node = new DialogueAdditionalEntryNode(_dialogue, data);
        node.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
        AddElement(node);
        _additionalEntryNodes[data.id] = node;
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();
    }

    public void CreateAdditionalExitNode(Vector2 position)
    {
        Undo.RecordObject(_dialogue, "Create Dialogue Exit");
        _dialogue.additionalExits ??= new List<DialogueExitData>();
        var data = new DialogueExitData { name = $"Exit {_dialogue.additionalExits.Count + 1}", position = position };
        _dialogue.additionalExits.Add(data);
        var node = new DialogueAdditionalExitNode(_dialogue, data);
        node.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
        AddElement(node);
        _additionalExitNodes[data.id] = node;
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();
    }

    private void CreateRoomNode<T>(string baseName, Vector2 position, bool isSelectorOnly) where T : DialogueData
    {
        var undoGroup = BeginUndoGroup("Create Dialogue Data");
        var data = CreateDialogueSubAsset<T>(baseName, "Create Dialogue Data");
        if (data == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        // The graph is reconstructed from this array, so registering here makes the node persistent.
        Undo.RecordObject(_dialogue, "Add Dialogue Data");
        _dialogue.rooms = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
            .Concat(new[] { (DialogueData)data })
            .ToArray();
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();

        // Add the new visual directly so rebuilding does not run graph-removal callbacks.
        var node = new DialogueNode(data, RebuildGraph, RemoveExecOutputLinks, isSelectorOnly);
        node.geometryChanged = OnDialogueNodeGeometryChanged;
        AddElement(node);
        _roomNodes[data] = node;
        node.SetPosition(new Rect(position, Vector2.zero));
        DialogueGraphLayout.instance.SaveGroups();
        Undo.CollapseUndoOperations(undoGroup);
    }

    private void OnDialogueNodeGeometryChanged()
    {
        RefreshTransitionEdgeAnchors();
        RefreshTransitionStrokeSelection();
    }

    // Centralize creation so graph-created ScriptableObjects are always stored inside this Dialogue.
    private T CreateDialogueSubAsset<T>(string baseName, string undoName) where T : ScriptableObject
    {
        var dialoguePath = AssetDatabase.GetAssetPath(_dialogue);
        if (string.IsNullOrEmpty(dialoguePath))
        {
            Debug.LogError("Cannot create a dialogue sub-asset because the Dialogue has not been saved as an asset.", _dialogue);
            return null;
        }

        var existingNames = AssetDatabase.LoadAllAssetsAtPath(dialoguePath)
            .Select(asset => asset.name)
            .ToArray();
        var subAsset = ScriptableObject.CreateInstance<T>();
        subAsset.name = ObjectNames.GetUniqueName(existingNames, baseName);

        Undo.RegisterCreatedObjectUndo(subAsset, undoName);
        AssetDatabase.AddObjectToAsset(subAsset, _dialogue);
        EditorUtility.SetDirty(subAsset);
        EditorUtility.SetDirty(_dialogue);
        return subAsset;
    }

    private List<DialogueLink> GetAllDialogueLinks()
    {
        var links = new List<DialogueLink>();
        var seen = new HashSet<DialogueLink>();
        foreach (var link in _dialogue.links ?? System.Array.Empty<DialogueLink>())
        {
            if (link != null && seen.Add(link)) links.Add(link);
        }
        foreach (var room in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
        {
            foreach (var link in room?.transitions ?? System.Array.Empty<DialogueLink>())
            {
                if (link != null && seen.Add(link)) links.Add(link);
            }
        }
        return links;
    }

    private DialogueData FindTransitionSourceRoom(DialogueLink link)
    {
        if (link.source != null) return link.source;
        return (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
            .FirstOrDefault(room => room != null && room.transitions != null && room.transitions.Contains(link));
    }

    private bool IsTransitionInsideGroup(DialogueLink link, HashSet<string> memberGuids)
    {
        string sourceGuid;
        if (!string.IsNullOrEmpty(link.sourceEntryId))
        {
            sourceGuid = _additionalEntryNodes.TryGetValue(link.sourceEntryId, out var entryNode)
                ? entryNode.viewDataKey
                : null;
        }
        else if (link.sourceIsDialogueStart)
        {
            sourceGuid = _startNode?.viewDataKey;
        }
        else
        {
            var sourceRoom = FindTransitionSourceRoom(link);
            sourceGuid = sourceRoom != null ? DialogueGraphLayout.GetAssetKey(sourceRoom) : null;
        }

        string destinationGuid;
        if (!string.IsNullOrEmpty(link.destinationExitId))
            destinationGuid = _additionalExitNodes.TryGetValue(link.destinationExitId, out var exitNode)
                ? exitNode.viewDataKey
                : null;
        else
            destinationGuid = link.destinationIsDialogueExit
                ? _exitNode?.viewDataKey
                : link.destination != null ? DialogueGraphLayout.GetAssetKey(link.destination) : null;
        return sourceGuid != null && destinationGuid != null &&
            memberGuids.Contains(sourceGuid) && memberGuids.Contains(destinationGuid);
    }

    private void CreateGroupAroundSelection(List<GraphElement> members)
    {
        members = members.ToList();
        var memberGuids = members.Select(member => member.viewDataKey).ToHashSet();
        foreach (var link in GetAllDialogueLinks())
        {
            if (!IsTransitionInsideGroup(link, memberGuids) ||
                !_curvePointHandles.TryGetValue(link, out var handles))
                continue;

            foreach (var handle in handles)
            {
                if (members.Contains(handle)) continue;
                members.Add(handle);
                memberGuids.Add(handle.viewDataKey);
            }
        }

        // Expand the group's bounds around selected nodes, leaving room for its title and padding.
        const float pad = 40f;
        float minX = members.Min(m => m.GetPosition().xMin);
        float minY = members.Min(m => m.GetPosition().yMin);
        float maxX = members.Max(m => m.GetPosition().xMax);
        float maxY = members.Max(m => m.GetPosition().yMax);

        foreach (var link in GetAllDialogueLinks())
        {
            if (!IsTransitionInsideGroup(link, memberGuids)) continue;
            foreach (var point in link.curvePoints ?? new List<DialogueCurvePoint>())
            {
                if (point == null) continue;
                minX = Mathf.Min(minX, point.position.x - 8f);
                minY = Mathf.Min(minY, point.position.y - 8f);
                maxX = Mathf.Max(maxX, point.position.x + 8f);
                maxY = Mathf.Max(maxY, point.position.y + 8f);
            }
        }

        var position = new Vector2(minX - pad, minY - pad * 2f);
        var size = new Vector2(maxX - minX + pad * 2f, maxY - minY + pad * 3f);

        CreateGroup("New Group", position, size, members);
    }

    private void CreateGroup(string title, Vector2 position, Vector2 size, IEnumerable<GraphElement> members = null)
    {
        // Layout data is stored outside the Dialogue asset because groups are editor presentation state.
        var undoGroup = BeginUndoGroup("Create Dialogue Graph Group");
        var data = DialogueGraphLayout.instance.CreateGroupData(_dialogue, title, position, size);
        var group = new DialogueGroup(data, delta => MoveGroupedTransitionPoints(data, delta), moving => _movingGroup = moving);
        AddElement(group);
        group.SetPosition(new Rect(position, size));

        if (members != null)
        {
            foreach (var member in members)
                group.AddElement(member);
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private void MoveGroupedTransitionPoints(DialogueGraphLayout.GroupData groupData, Vector2 delta)
    {
        if (_isRebuilding || _adjustingGroupBounds || delta == Vector2.zero) return;

        var memberGuids = groupData.memberAssetGuids.ToHashSet();
        var group = graphElements.OfType<DialogueGroup>().FirstOrDefault(item => item.data == groupData);
        var groupedHandles = group?.containedElements.OfType<DialogueCurvePointHandle>().ToHashSet()
            ?? new HashSet<DialogueCurvePointHandle>();
        foreach (var link in GetAllDialogueLinks())
        {
            if (!IsTransitionInsideGroup(link, memberGuids) || link.curvePoints == null) continue;

            Undo.RecordObject(link, "Move Grouped Transition Points");
            foreach (var point in link.curvePoints)
            {
                if (point == null) continue;
                if (_curvePointHandles.TryGetValue(link, out var linkHandles) &&
                    linkHandles.FirstOrDefault(handle => handle.point == point) is { } graphHandle &&
                    groupedHandles.Contains(graphHandle))
                    continue;

                point.position += delta;
                if (_curvePointHandles.TryGetValue(link, out var handlesToMove))
                {
                    var handle = handlesToMove.FirstOrDefault(candidate => candidate.point == point);
                    handle?.SetGraphPosition(point.position);
                }
            }

            EditorUtility.SetDirty(link);
            MarkTransitionStrokeDirty(link);
        }
    }

    private void ExpandGroupsToIncludeTransition(DialogueLink link)
    {
        if (_isRebuilding || _movingGroup || link == null || link.curvePoints == null) return;

        foreach (var groupData in DialogueGraphLayout.instance.GetGroups(_dialogue))
        {
            if (!IsTransitionInsideGroup(link, groupData.memberAssetGuids.ToHashSet()))
                continue;

            var group = graphElements.OfType<DialogueGroup>().FirstOrDefault(item => item.data == groupData);
            if (group == null) continue;

            var current = group.GetPosition();
            float minX = current.xMin;
            float minY = current.yMin;
            float maxX = current.xMax;
            float maxY = current.yMax;
            foreach (var point in link.curvePoints)
            {
                if (point == null) continue;
                minX = Mathf.Min(minX, point.position.x - 14f);
                minY = Mathf.Min(minY, point.position.y - 14f);
                maxX = Mathf.Max(maxX, point.position.x + 14f);
                maxY = Mathf.Max(maxY, point.position.y + 14f);
            }

            var expanded = Rect.MinMaxRect(minX, minY, maxX, maxY);
            if (expanded == current) continue;

            var memberNodes = graphElements.OfType<Node>()
                .Where(node => groupData.memberAssetGuids.Contains(node.viewDataKey))
                .ToDictionary(node => node, node => node.GetPosition());

            Undo.RecordObject(DialogueGraphLayout.instance, "Resize Dialogue Group for Curve Points");
            _adjustingGroupBounds = true;
            try
            {
                group.SetPosition(expanded);
            }
            finally
            {
                _adjustingGroupBounds = false;
            }

            foreach (var member in memberNodes)
                member.Key.SetPosition(member.Value);
        }
    }

    public void RebuildGraph()
    {
        // Recreate visuals from the authoritative asset arrays and saved editor layout.
        _isRebuilding = true;
        try
        {
            ClearAllCurvePointHandles();
            ClearOverlapBadges();
            _lastOverlapCycle.Clear();
            _overlapCycleIndex = -1;
            DeleteElements(graphElements.ToList());
        }
        finally
        {
            _isRebuilding = false;
        }

        _roomNodes.Clear();
        _additionalEntryNodes.Clear();
        _additionalExitNodes.Clear();

        var rooms = (_dialogue.rooms ?? System.Array.Empty<DialogueData>()).Where(r => r != null).ToList();
        _dialogue.startOutput ??= new DialogueExecPortData { label = "Start" };
        _dialogue.exitInput ??= new DialogueExecPortData { label = "Exit" };
        _startNode = new DialogueStartNode(_dialogue);
        _exitNode = new DialogueExitNode(_dialogue);
        _startNode.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
        _exitNode.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
        AddElement(_startNode);
        AddElement(_exitNode);

        foreach (var entry in _dialogue.additionalEntries ?? new List<DialogueEntryData>())
        {
            if (entry == null) continue;
            if (string.IsNullOrEmpty(entry.id)) entry.id = System.Guid.NewGuid().ToString("N");
            var node = new DialogueAdditionalEntryNode(_dialogue, entry);
            node.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
            AddElement(node);
            _additionalEntryNodes[entry.id] = node;
        }

        foreach (var exit in _dialogue.additionalExits ?? new List<DialogueExitData>())
        {
            if (exit == null) continue;
            if (string.IsNullOrEmpty(exit.id)) exit.id = System.Guid.NewGuid().ToString("N");
            var node = new DialogueAdditionalExitNode(_dialogue, exit);
            node.RegisterCallback<GeometryChangedEvent>(_ => OnDialogueNodeGeometryChanged());
            AddElement(node);
            _additionalExitNodes[exit.id] = node;
        }

        // First create all nodes so edges can resolve both endpoints in the second pass.
        foreach (var room in rooms)
        {
            var node = new DialogueNode(room, RebuildGraph, RemoveExecOutputLinks, room is DialogueSelectorData);
            node.geometryChanged = OnDialogueNodeGeometryChanged;
            AddElement(node);
            _roomNodes[room] = node;
        }

        // Rebuild all transitions from the serialized endpoint and pin IDs.
        var links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
            .Where(link => link != null)
            .ToList();
        foreach (var room in rooms)
        {
            foreach (var link in room.transitions ?? System.Array.Empty<DialogueLink>())
            {
                if (link != null && !links.Contains(link)) links.Add(link);
            }
        }

        foreach (var link in links)
        {
            if (!TryResolveTransitionPorts(link, out var output, out var input)) continue;
            AddDirectedEdge(output, input, link);
        }

        // Restore groups after nodes exist so their saved member GUIDs can be resolved.
        foreach (var groupData in DialogueGraphLayout.instance.GetGroups(_dialogue))
        {
            var group = new DialogueGroup(groupData, delta => MoveGroupedTransitionPoints(groupData, delta), moving => _movingGroup = moving);
            AddElement(group);
            group.SetPosition(new Rect(groupData.position, groupData.size));

            foreach (var guid in groupData.memberAssetGuids)
            {
                var member = FindNodeByGuid(guid);
                if (member != null)
                    group.AddElement(member);
            }
        }
    }

    private GraphElement FindNodeByGuid(string guid)
    {
        // Groups remember members by asset GUID, not by transient GraphView element instances.
        foreach (var node in _roomNodes.Values)
            if (node.viewDataKey == guid) return node;
        foreach (var handles in _curvePointHandles.Values)
        {
            var handle = handles.FirstOrDefault(item => item.viewDataKey == guid);
            if (handle != null) return handle;
        }
        if (_startNode != null && _startNode.viewDataKey == guid) return _startNode;
        if (_exitNode != null && _exitNode.viewDataKey == guid) return _exitNode;
        foreach (var node in _additionalEntryNodes.Values)
            if (node.viewDataKey == guid) return node;
        foreach (var node in _additionalExitNodes.Values)
            if (node.viewDataKey == guid) return node;
        return null;
    }

    // The custom stroke overlays a standard edge connected directly to the selected exec pins.
    private Edge AddDirectedEdge(Port output, Port input, DialogueLink transition)
    {
        var edge = output.ConnectTo(input);
        ConfigureTransitionEdge(edge, transition);
        AddElement(edge);
        edge.UpdateEdgeControl();
        HideBuiltInBezier(edge);
        edge.edgeControl.MarkDirtyRepaint();
        edge.schedule.Execute(() => RefreshTransitionEdgeAnchors()).ExecuteLater(0);
        RefreshTransitionStrokeSelection();
        return edge;
    }

    private void ConfigureTransitionEdge(Edge edge, DialogueLink transition)
    {
        if (edge.userData is DialogueTransitionEdgeData existing && existing.stroke != null)
        {
            UpdateTransitionEndpointColors(edge, existing);
            existing.stroke.MarkDirtyRepaint();
            return;
        }

        edge.userData = new DialogueTransitionEdgeData
        {
            transition = transition
        };
        UpdateTransitionEndpointColors(edge, (DialogueTransitionEdgeData)edge.userData);
        float lineWidth = edge.edgeControl.edgeWidth;
        var lineColor = edge.edgeControl.outputColor;
        edge.edgeControl.drawToCap = false;
        edge.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            if (edge.userData is not DialogueTransitionEdgeData edgeData || edgeData.transition == null) return;
            evt.menu.AppendAction("Select Transition Asset", _ =>
            {
                Selection.activeObject = edgeData.transition;
                EditorGUIUtility.PingObject(edgeData.transition);
            });
        }));
        var transitionData = (DialogueTransitionEdgeData)edge.userData;
        transitionData.stroke = new DialogueTransitionStroke(this, edge, lineWidth, lineColor);
        edge.hierarchy.Add(transitionData.stroke);
        transitionData.stroke.BringToFront();
        CreateCurvePointHandles(transition);
    }

    private static void UpdateTransitionEndpointColors(Edge edge, DialogueTransitionEdgeData data)
    {
        data.startsAtEntry = edge.output?.node is DialogueStartNode or DialogueAdditionalEntryNode;
        data.endsAtExit = edge.input?.node is DialogueExitNode or DialogueAdditionalExitNode;
    }

    public int GetTransitionOverlapCount(Edge edge, out bool isFirstOverlap)
    {
        isFirstOverlap = false;
        if (edge.userData is not DialogueTransitionEdgeData edgeData || edgeData.transition == null)
            return 0;

        int count = 0;
        foreach (var candidate in graphElements.OfType<Edge>())
        {
            if (candidate.output?.node != edge.output?.node ||
                candidate.input?.node != edge.input?.node ||
                candidate.userData is not DialogueTransitionEdgeData candidateData ||
                !HaveSameCurve(edgeData.transition, candidateData.transition))
                continue;

            if (candidate == edge)
                isFirstOverlap = count == 0;
            count++;
        }

        return count;
    }

    private static bool HaveSameCurve(DialogueLink first, DialogueLink second)
    {
        if (first == null || second == null) return false;
        var firstPoints = first.curvePoints ?? new List<DialogueCurvePoint>();
        var secondPoints = second.curvePoints ?? new List<DialogueCurvePoint>();
        if (firstPoints.Count != secondPoints.Count) return false;

        for (int i = 0; i < firstPoints.Count; i++)
        {
            var firstPoint = firstPoints[i];
            var secondPoint = secondPoints[i];
            if (firstPoint == null || secondPoint == null)
            {
                if (firstPoint != secondPoint) return false;
                continue;
            }

            if (firstPoint.position != secondPoint.position || firstPoint.mode != secondPoint.mode)
                return false;
        }

        return true;
    }

    private bool TryResolveTransitionPorts(DialogueLink link, out Port output, out Port input)
    {
        output = null;
        input = null;

        DialogueNode sourceNode = null;
        if (!string.IsNullOrEmpty(link.sourceEntryId))
        {
            if (!_additionalEntryNodes.TryGetValue(link.sourceEntryId, out var entryNode)) return false;
            output = entryNode.output;
        }
        else if (link.sourceIsDialogueStart)
        {
            if (_startNode == null) return false;
            output = _startNode.output;
        }
        else
        {
            DialogueData sourceAsset = link.source;
            if (sourceAsset == null)
            {
                sourceAsset = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
                    .FirstOrDefault(room => room != null && room.transitions != null && room.transitions.Contains(link));
                if (sourceAsset != null)
                {
                    link.source = sourceAsset;
                    link.sourcePortId = sourceAsset.execOutput.id;
                }
            }

            if (sourceAsset == null || !_roomNodes.TryGetValue(sourceAsset, out sourceNode)) return false;
            if (string.IsNullOrEmpty(link.sourcePortId))
                link.sourcePortId = sourceNode.room.execOutput.id;
            output = sourceNode.GetOutputPort(link.sourcePortId);
        }

        if (!string.IsNullOrEmpty(link.destinationExitId))
        {
            if (!_additionalExitNodes.TryGetValue(link.destinationExitId, out var exitNode)) return false;
            input = exitNode.input;
        }
        else if (link.destinationIsDialogueExit)
        {
            if (_exitNode == null) return false;
            input = _exitNode.input;
        }
        else if (link.destination != null && _roomNodes.TryGetValue(link.destination, out var destinationNode))
        {
            if (string.IsNullOrEmpty(link.destinationPortId))
                link.destinationPortId = destinationNode.room.execInput.id;
            input = destinationNode.GetInputPort(link.destinationPortId);
        }

        return output != null && input != null;
    }

    private void RefreshTransitionEdgeAnchors()
    {
        foreach (var edge in graphElements.OfType<Edge>())
        {
            edge.UpdateEdgeControl();
            HideBuiltInBezier(edge);
            if (edge.userData is DialogueTransitionEdgeData edgeData)
                edgeData.stroke?.MarkDirtyRepaint();
        }
    }

    private static void HideBuiltInBezier(Edge edge)
    {
        edge.edgeControl.inputColor = Color.clear;
        edge.edgeControl.outputColor = Color.clear;
        edge.edgeControl.pickingMode = PickingMode.Ignore;
        edge.edgeControl.style.opacity = 0;
        edge.pickingMode = PickingMode.Ignore;
    }

    private bool TrySelectTransitionAt(Vector2 pointerPosition)
    {
        if (!FindTransitionNear(pointerPosition, out var closestEdge, out _, out _)) return false;

        var overlappingEdges = GetOverlappingEdges(closestEdge);
        Edge edgeToSelect = closestEdge;
        if (overlappingEdges.Count > 1)
        {
            var links = overlappingEdges
                .Select(edge => ((DialogueTransitionEdgeData)edge.userData).transition)
                .ToList();
            if (links.SequenceEqual(_lastOverlapCycle))
                _overlapCycleIndex = (_overlapCycleIndex + 1) % links.Count;
            else
            {
                _lastOverlapCycle = links;
                _overlapCycleIndex = 0;
            }

            var selectedLink = links[_overlapCycleIndex];
            edgeToSelect = overlappingEdges.First(edge =>
                ((DialogueTransitionEdgeData)edge.userData).transition == selectedLink);
        }
        else
        {
            _lastOverlapCycle.Clear();
            _overlapCycleIndex = -1;
        }

        SelectCurvePointHandle(null);
        ClearSelection();
        AddToSelection(edgeToSelect);
        if (edgeToSelect.userData is DialogueTransitionEdgeData edgeData)
        {
            Selection.activeObject = edgeData.transition;
            edgeData.stroke?.MarkDirtyRepaint();
        }
        return true;
    }

    private List<Edge> GetOverlappingEdges(Edge edge)
    {
        if (edge.output?.node == null || edge.input?.node == null ||
            edge.userData is not DialogueTransitionEdgeData edgeData)
            return new List<Edge> { edge };

        return graphElements.OfType<Edge>()
            .Where(candidate => candidate.output?.node == edge.output.node &&
                candidate.input?.node == edge.input.node &&
                candidate.userData is DialogueTransitionEdgeData candidateData &&
                HaveSameCurve(edgeData.transition, candidateData.transition))
            .ToList();
    }

    private bool TryAddTransitionCurvePoint(Vector2 pointerPosition)
    {
        if (!FindTransitionNear(pointerPosition, out var edge, out var graphPosition, out var routeSegment))
            return false;
        if (edge.userData is not DialogueTransitionEdgeData edgeData || edgeData.transition == null)
            return false;

        var link = edgeData.transition;
        var undoGroup = BeginUndoGroup("Add Transition Curve Point");
        Undo.RecordObject(link, "Add Transition Curve Point");
        link.curvePoints ??= new List<DialogueCurvePoint>();
        int index = Mathf.Clamp(routeSegment, 0, link.curvePoints.Count);
        link.curvePoints.Insert(index, new DialogueCurvePoint
        {
            position = graphPosition,
            mode = DialogueCurvePointMode.Eased
        });

        EditorUtility.SetDirty(link);
        RebuildCurvePointHandles(link);
        ExpandGroupsToIncludeTransition(link);
        RefreshTransitionEdgesForLink(link);
        DialogueGraphLayout.instance.SaveGroups();
        AssetDatabase.SaveAssets();
        ClearSelection();
        AddToSelection(edge);
        Selection.activeObject = link;
        Undo.CollapseUndoOperations(undoGroup);
        return true;
    }

    private bool FindTransitionNear(
        Vector2 pointerPosition,
        out Edge closestEdge,
        out Vector2 closestGraphPosition,
        out int closestRouteSegment)
    {
        const float hitRadius = 9f;
        float closestDistance = hitRadius * hitRadius;
        closestEdge = null;
        closestGraphPosition = default;
        closestRouteSegment = 0;

        var graphToWorld = contentViewContainer.worldTransform;
        foreach (var edge in graphElements.OfType<Edge>())
        {
            if (edge.userData is not DialogueTransitionEdgeData || edge.output == null || edge.input == null)
                continue;

            var path = GetTransitionPath(edge, out var segmentRoutes);
            for (int i = 0; i < segmentRoutes.Count; i++)
            {
                var start3 = graphToWorld.MultiplyPoint3x4(new Vector3(path[i].x, path[i].y, 0));
                var end3 = graphToWorld.MultiplyPoint3x4(new Vector3(path[i + 1].x, path[i + 1].y, 0));
                var start = new Vector2(start3.x, start3.y);
                var end = new Vector2(end3.x, end3.y);
                var segment = end - start;
                float lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= 0) continue;

                float t = Mathf.Clamp01(Vector2.Dot(pointerPosition - start, segment) / lengthSquared);
                var nearestWorld = start + segment * t;
                float distance = (pointerPosition - nearestWorld).sqrMagnitude;
                if (distance >= closestDistance) continue;

                var graphPoint = Vector2.Lerp(path[i], path[i + 1], t);
                closestDistance = distance;
                closestEdge = edge;
                closestGraphPosition = graphPoint;
                closestRouteSegment = segmentRoutes[i];
            }
        }

        return closestEdge != null;
    }

    public List<Vector2> GetTransitionPath(Edge edge, out List<int> segmentRoutes)
    {
        segmentRoutes = new List<int>();
        if (edge.output == null || edge.input == null ||
            edge.userData is not DialogueTransitionEdgeData edgeData || edgeData.transition == null)
            return new List<Vector2>();

        var link = edgeData.transition;
        var knots = new List<Vector2> { WorldToGraph(edge.output.GetGlobalCenter()) };
        var curvePoints = (link.curvePoints ?? new List<DialogueCurvePoint>())
            .Where(point => point != null)
            .ToList();
        knots.AddRange(curvePoints.Select(point => point.position));
        knots.Add(WorldToGraph(edge.input.GetGlobalCenter()));

        var path = new List<Vector2> { knots[0] };
        for (int i = 0; i < knots.Count - 1; i++)
        {
            var start = knots[i];
            var end = knots[i + 1];
            bool isTerminalSegment = i == knots.Count - 2;
            bool easeStart = !isTerminalSegment && i > 0 && curvePoints[i - 1].mode == DialogueCurvePointMode.Eased;
            bool easeEnd = !isTerminalSegment && i < curvePoints.Count && curvePoints[i].mode == DialogueCurvePointMode.Eased;
            int steps = easeStart || easeEnd ? 16 : 1;

            var previous = i > 0 ? knots[i - 1] : start;
            var next = i + 2 < knots.Count ? knots[i + 2] : end;
            var controlA = easeStart ? start + (end - previous) / 6f : start;
            var controlB = easeEnd ? end - (next - start) / 6f : end;

            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                float inverseT = 1f - t;
                var point = inverseT * inverseT * inverseT * start +
                    3f * inverseT * inverseT * t * controlA +
                    3f * inverseT * t * t * controlB +
                    t * t * t * end;
                path.Add(point);
                segmentRoutes.Add(i);
            }
        }

        ClipRouteAtTargetNode(path, segmentRoutes, edge.input.node);

        return path;
    }

    private void ClipRouteAtTargetNode(List<Vector2> path, List<int> segmentRoutes, Node node)
    {
        if (node == null || path.Count < 2) return;

        var worldBounds = node.worldBound;
        var graphMin = WorldToGraph(new Vector3(worldBounds.xMin, worldBounds.yMin, 0));
        var graphMax = WorldToGraph(new Vector3(worldBounds.xMax, worldBounds.yMax, 0));
        var bounds = Rect.MinMaxRect(
            Mathf.Min(graphMin.x, graphMax.x),
            Mathf.Min(graphMin.y, graphMax.y),
            Mathf.Max(graphMin.x, graphMax.x),
            Mathf.Max(graphMin.y, graphMax.y));

        for (int i = path.Count - 2; i >= 0; i--)
        {
            if (!bounds.Contains(path[i + 1]) || bounds.Contains(path[i])) continue;

            var boundary = GetNodeBoundaryPoint(bounds, path[i + 1], path[i]);
            path.RemoveRange(i + 1, path.Count - i - 1);
            path.Add(boundary);
            int firstRemovedRoute = i + 1;
            if (firstRemovedRoute < segmentRoutes.Count)
                segmentRoutes.RemoveRange(firstRemovedRoute, segmentRoutes.Count - firstRemovedRoute);
            return;
        }
    }

    private static Vector2 GetNodeBoundaryPoint(Rect bounds, Vector2 inside, Vector2 outside)
    {
        var direction = outside - inside;
        float t = 1f;
        if (direction.x > 0f)
            t = Mathf.Min(t, (bounds.xMax - inside.x) / direction.x);
        else if (direction.x < 0f)
            t = Mathf.Min(t, (bounds.xMin - inside.x) / direction.x);

        if (direction.y > 0f)
            t = Mathf.Min(t, (bounds.yMax - inside.y) / direction.y);
        else if (direction.y < 0f)
            t = Mathf.Min(t, (bounds.yMin - inside.y) / direction.y);

        return inside + direction * Mathf.Clamp01(t);
    }

    private Vector2 WorldToGraph(Vector3 position)
    {
        var local = contentViewContainer.worldTransform.inverse.MultiplyPoint3x4(position);
        return new Vector2(local.x, local.y);
    }

    private void CreateCurvePointHandles(DialogueLink link)
    {
        RemoveCurvePointHandles(link);
        if (link == null || link.curvePoints == null || link.curvePoints.Count == 0) return;

        var handles = new List<DialogueCurvePointHandle>();
        foreach (var curvePoint in link.curvePoints)
        {
            if (curvePoint == null) continue;
            if (string.IsNullOrEmpty(curvePoint.id))
                curvePoint.id = System.Guid.NewGuid().ToString("N");
            var point = curvePoint;
            var handle = new DialogueCurvePointHandle(
                link,
                point,
                position =>
                {
                    point.position = position;
                    Undo.RecordObject(link, "Move Transition Curve Point");
                    EditorUtility.SetDirty(link);
                    ExpandGroupsToIncludeTransition(link);
                    MarkTransitionStrokeDirty(link);
                },
                () => SetCurvePointMode(link, point, DialogueCurvePointMode.Linear),
                () => SetCurvePointMode(link, point, DialogueCurvePointMode.Eased),
                () => DeleteCurvePoint(link, point));
            handle.SetGraphPosition(point.position);
            handle.SetPointMode(point.mode);
            AddElement(handle);
            handle.BringToFront();
            handles.Add(handle);
        }

        _curvePointHandles[link] = handles;
    }

    private void RemoveCurvePointHandles(DialogueLink link)
    {
        if (!_curvePointHandles.TryGetValue(link, out var handles)) return;
        foreach (var handle in handles)
        {
            if (_selectedCurvePointHandle == handle)
                SelectCurvePointHandle(null);
            RemoveElement(handle);
        }
        _curvePointHandles.Remove(link);
    }

    private void ClearAllCurvePointHandles()
    {
        foreach (var handles in _curvePointHandles.Values)
        {
            foreach (var handle in handles)
            {
                if (_selectedCurvePointHandle == handle)
                    SelectCurvePointHandle(null);
                RemoveElement(handle);
            }
        }
        _curvePointHandles.Clear();
    }

    private void SelectCurvePointHandle(DialogueCurvePointHandle handle)
    {
        if (_selectedCurvePointHandle == handle) return;
        _selectedCurvePointHandle?.SetSelected(false);
        _selectedCurvePointHandle = handle;
        _selectedCurvePointHandle?.SetSelected(true);
        if (handle?.transition != null)
        {
            handle.Focus();
            Selection.activeObject = handle.transition;
        }
    }

    private static DialogueCurvePointHandle FindCurvePointHandle(VisualElement element)
    {
        while (element != null)
        {
            if (element is DialogueCurvePointHandle handle) return handle;
            element = element.parent;
        }
        return null;
    }

    private void SetCurvePointMode(DialogueLink link, DialogueCurvePoint point, DialogueCurvePointMode mode)
    {
        if (point.mode == mode) return;

        var undoGroup = BeginUndoGroup("Change Transition Curve Point Mode");
        Undo.RecordObject(link, "Change Transition Curve Point Mode");
        point.mode = mode;
        EditorUtility.SetDirty(link);
        AssetDatabase.SaveAssets();
        RebuildCurvePointHandles(link);
        MarkTransitionStrokeDirty(link);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private void DeleteCurvePoint(DialogueLink link, DialogueCurvePoint point)
    {
        if (link.curvePoints == null || !link.curvePoints.Contains(point)) return;

        var undoGroup = BeginUndoGroup("Delete Transition Curve Point");
        Undo.RecordObject(link, "Delete Transition Curve Point");
        link.curvePoints.Remove(point);
        EditorUtility.SetDirty(link);
        AssetDatabase.SaveAssets();
        RebuildCurvePointHandles(link);
        MarkTransitionStrokeDirty(link);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private void RebuildCurvePointHandles(DialogueLink link) => CreateCurvePointHandles(link);

    private void MarkTransitionStrokeDirty(DialogueLink link)
    {
        foreach (var edge in graphElements.OfType<Edge>())
        {
            if (edge.userData is DialogueTransitionEdgeData edgeData && edgeData.transition == link)
            {
                edge.UpdateEdgeControl();
                HideBuiltInBezier(edge);
                edgeData.stroke?.MarkDirtyRepaint();
            }
        }

        RefreshTransitionStrokeSelection();
    }

    private void RefreshTransitionEdgesForLink(DialogueLink link)
    {
        foreach (var edge in graphElements.OfType<Edge>())
        {
            if (edge.userData is DialogueTransitionEdgeData edgeData && edgeData.transition == link)
            {
                edge.UpdateEdgeControl();
                HideBuiltInBezier(edge);
                edgeData.stroke?.MarkDirtyRepaint();
            }
        }
    }

    private void RefreshTransitionStrokeSelection()
    {
        var graphEdges = graphElements.OfType<Edge>().ToHashSet();
        foreach (var staleEdge in _overlapBadges.Keys.Where(edge => !graphEdges.Contains(edge)).ToList())
        {
            _overlapBadges[staleEdge].RemoveFromHierarchy();
            _overlapBadges.Remove(staleEdge);
        }

        foreach (var edge in graphElements.OfType<Edge>())
        {
            if (edge.userData is not DialogueTransitionEdgeData edgeData || edgeData.stroke == null)
                continue;

            int overlapCount = GetTransitionOverlapCount(edge, out bool isFirstOverlap);
            var path = GetTransitionPath(edge, out _);
            if (isFirstOverlap && overlapCount > 1 && path.Count > 0)
            {
                var graphPosition = GetPolylineMidpoint(path);
                var badge = GetOverlapBadge(edge);
                badge.text = $"x{overlapCount}";
                badge.style.left = graphPosition.x - 14f;
                badge.style.top = graphPosition.y - 9f;
                badge.style.display = DisplayStyle.Flex;
                badge.BringToFront();
            }
            else if (_overlapBadges.TryGetValue(edge, out var badge))
            {
                badge.style.display = DisplayStyle.None;
            }

            edgeData.stroke.MarkDirtyRepaint();
        }
    }

    private void RefreshCurvePointSelectionFromNodeSelection()
    {
        var selectedHandles = selection.OfType<DialogueCurvePointHandle>().ToHashSet();
        _selectedCurvePointHandle = selectedHandles.LastOrDefault();
        var selectedRooms = selection.OfType<DialogueNode>()
            .Select(node => node.room)
            .ToHashSet();
        bool startSelected = selection.OfType<DialogueStartNode>().Any();
        bool exitSelected = selection.OfType<DialogueExitNode>().Any();
        var selectedEntryIds = selection.OfType<DialogueAdditionalEntryNode>().Select(node => node.data.id).ToHashSet();
        var selectedExitIds = selection.OfType<DialogueAdditionalExitNode>().Select(node => node.data.id).ToHashSet();

        foreach (var pair in _curvePointHandles)
        {
            var link = pair.Key;
            DialogueData source = link.source;
            if (source == null && !link.sourceIsDialogueStart)
            {
                source = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
                    .FirstOrDefault(room => room != null && room.transitions != null && room.transitions.Contains(link));
            }

            bool sourceSelected = !string.IsNullOrEmpty(link.sourceEntryId)
                ? selectedEntryIds.Contains(link.sourceEntryId)
                : link.sourceIsDialogueStart ? startSelected : selectedRooms.Contains(source);
            bool destinationSelected = !string.IsNullOrEmpty(link.destinationExitId)
                ? selectedExitIds.Contains(link.destinationExitId)
                : link.destinationIsDialogueExit ? exitSelected : selectedRooms.Contains(link.destination);
            bool transitionSelected = sourceSelected && destinationSelected;

            foreach (var handle in pair.Value)
                handle.SetSelected(transitionSelected || selectedHandles.Contains(handle));
        }
    }

    private Label GetOverlapBadge(Edge edge)
    {
        if (_overlapBadges.TryGetValue(edge, out var badge)) return badge;

        badge = new Label
        {
            pickingMode = PickingMode.Ignore
        };
        badge.style.position = Position.Absolute;
        badge.style.width = 28;
        badge.style.height = 18;
        badge.style.unityTextAlign = TextAnchor.MiddleCenter;
        badge.style.fontSize = 11;
        badge.style.color = Color.white;
        badge.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        badge.style.borderTopLeftRadius = 8;
        badge.style.borderTopRightRadius = 8;
        badge.style.borderBottomLeftRadius = 8;
        badge.style.borderBottomRightRadius = 8;
        contentViewContainer.Add(badge);
        _overlapBadges.Add(edge, badge);
        return badge;
    }

    private void ClearOverlapBadges()
    {
        foreach (var badge in _overlapBadges.Values)
            badge.RemoveFromHierarchy();
        _overlapBadges.Clear();
    }

    private static Vector2 GetPolylineMidpoint(IReadOnlyList<Vector2> path)
    {
        if (path.Count == 0) return Vector2.zero;

        float totalLength = 0f;
        for (int i = 1; i < path.Count; i++)
            totalLength += Vector2.Distance(path[i - 1], path[i]);

        if (totalLength <= Mathf.Epsilon) return path[0];

        float halfway = totalLength * 0.5f;
        float traversed = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float segmentLength = Vector2.Distance(path[i - 1], path[i]);
            if (traversed + segmentLength >= halfway)
            {
                float t = (halfway - traversed) / segmentLength;
                return Vector2.Lerp(path[i - 1], path[i], t);
            }
            traversed += segmentLength;
        }

        return path[path.Count - 1];
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        // GraphView reports user edits here; mirror edge/node changes into the backing assets.
        if (_isRebuilding) return change;

        bool dirty = false;

        if (change.edgesToCreate != null)
        {
            foreach (var edge in change.edgesToCreate)
                dirty |= ApplyEdge(edge, connect: true);
        }

        if (change.elementsToRemove != null)
        {
            foreach (var fixedEndpoint in change.elementsToRemove
                .Where(element => element is DialogueStartNode { } startNode && startNode.viewDataKey.EndsWith(":start") ||
                    element is DialogueExitNode { } exitNode && exitNode.viewDataKey.EndsWith(":exit"))
                .ToList())
                change.elementsToRemove.Remove(fixedEndpoint);

            foreach (var group in change.elementsToRemove.OfType<DialogueGroup>().ToList())
            {
                _preservingGroupContents = true;
                try
                {
                    var members = group.containedElements.ToHashSet();
                    var memberNodes = members.OfType<Node>().ToHashSet();
                    var connectedEdges = graphElements.OfType<Edge>()
                        .Where(edge => memberNodes.Contains(edge.output?.node) || memberNodes.Contains(edge.input?.node))
                        .ToList();

                    foreach (var edge in connectedEdges)
                        change.elementsToRemove.Remove(edge);

                    foreach (var member in members)
                    {
                        change.elementsToRemove.Remove(member);
                        group.RemoveElement(member);
                    }
                }
                finally
                {
                    _preservingGroupContents = false;
                }
            }

            // Remove edges first so their link assets are detached before endpoint assets are destroyed.
            foreach (var element in change.elementsToRemove.OrderBy(element => element is Edge ? 0 : 1).ToList())
            {
                switch (element)
                {
                    case Edge edge:
                        dirty |= ApplyEdge(edge, connect: false);
                        break;
                    case DialogueGroup roomGroup:
                        DialogueGraphLayout.instance.RemoveGroup(roomGroup.data);
                        break;
                    case DialogueNode roomNode:
                        dirty |= RemoveFromGraph(roomNode.room);
                        break;
                    case DialogueAdditionalEntryNode entryNode:
                        dirty |= RemoveFromGraph(entryNode.data);
                        break;
                    case DialogueAdditionalExitNode exitNode:
                        dirty |= RemoveFromGraph(exitNode.data);
                        break;
                }
            }
        }

        if (dirty)
        {
            AssetDatabase.SaveAssets();
            schedule.Execute(RefreshTransitionStrokeSelection).ExecuteLater(0);
        }

        return change;
    }

    private bool ApplyEdge(Edge edge, bool connect)
    {
        if (edge.output == null || edge.input == null ||
            edge.output.portType != typeof(DialogueExecPortData) ||
            edge.input.portType != typeof(DialogueExecPortData))
            return false;

        var sourceElement = edge.output.node;
        var destinationElement = edge.input.node;
        var sourceRoomNode = sourceElement as DialogueNode;
        var destinationRoomNode = destinationElement as DialogueNode;
        var sourceEntryNode = sourceElement as DialogueAdditionalEntryNode;
        var destinationExitNode = destinationElement as DialogueAdditionalExitNode;
        bool sourceIsStart = sourceElement is DialogueStartNode;
        bool destinationIsExit = destinationElement is DialogueExitNode;
        bool sourceIsEntry = sourceIsStart || sourceEntryNode != null;
        bool destinationIsExitNode = destinationIsExit || destinationExitNode != null;
        if ((!sourceIsEntry && sourceRoomNode == null) ||
            (!destinationIsExitNode && destinationRoomNode == null)) return false;

        var transitionData = edge.userData as DialogueTransitionEdgeData;
        var transition = transitionData?.transition;
        if (connect)
        {
            var undoGroup = -1;
            if (transition == null)
            {
                undoGroup = BeginUndoGroup("Create Dialogue Transition");
                transition = CreateDialogueSubAsset<DialogueLink>("DialogueLink", "Create Dialogue Transition");
                if (transition == null)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                    return false;
                }
            }

            bool changed = false;
            var dialogueLinks = _dialogue.links?.ToList() ?? new List<DialogueLink>();
            if (!dialogueLinks.Contains(transition))
            {
                Undo.RecordObject(_dialogue, "Add Dialogue Transition");
                dialogueLinks.Add(transition);
                _dialogue.links = dialogueLinks.ToArray();
                changed = true;
            }

            Undo.RecordObject(transition, "Set Dialogue Transition Endpoints");
            var newSource = sourceRoomNode != null ? sourceRoomNode.room : null;
            var newDestination = destinationRoomNode != null ? destinationRoomNode.room : null;
            var newSourceEntryId = sourceEntryNode?.data.id;
            var newDestinationExitId = destinationExitNode?.data.id;
            string newSourcePortId = GetExecPortId(edge.output);
            string newDestinationPortId = GetExecPortId(edge.input);
            changed |= transition.source != newSource ||
                transition.sourceIsDialogueStart != sourceIsStart ||
                transition.sourceEntryId != newSourceEntryId ||
                transition.sourcePortId != newSourcePortId ||
                transition.destination != newDestination ||
                transition.destinationIsDialogueExit != destinationIsExit ||
                transition.destinationExitId != newDestinationExitId ||
                transition.destinationPortId != newDestinationPortId;

            if (changed)
            {
                transition.source = newSource;
                transition.sourceIsDialogueStart = sourceIsStart;
                transition.sourceEntryId = newSourceEntryId;
                transition.sourcePortId = newSourcePortId;
                transition.destination = newDestination;
                transition.destinationIsDialogueExit = destinationIsExit;
                transition.destinationExitId = newDestinationExitId;
                transition.destinationPortId = newDestinationPortId;
            }

            if (sourceRoomNode != null)
            {
                var roomTransitions = sourceRoomNode.room.transitions?.ToList() ?? new List<DialogueLink>();
                if (!roomTransitions.Contains(transition))
                {
                    Undo.RecordObject(sourceRoomNode.room, "Connect Dialogue Transition");
                    roomTransitions.Add(transition);
                    sourceRoomNode.room.transitions = roomTransitions.ToArray();
                    EditorUtility.SetDirty(sourceRoomNode.room);
                }
            }

            transitionData ??= new DialogueTransitionEdgeData();
            transitionData.transition = transition;
            edge.userData = transitionData;
            ConfigureTransitionEdge(edge, transition);

            if (changed)
            {
                EditorUtility.SetDirty(_dialogue);
                if (sourceRoomNode != null) EditorUtility.SetDirty(sourceRoomNode.room);
                EditorUtility.SetDirty(transition);
                AssetDatabase.SaveAssets();
            }

            if (undoGroup >= 0)
                Undo.CollapseUndoOperations(undoGroup);
            return changed;
        }

        if (transition == null) return false;

        Undo.RecordObject(_dialogue, "Delete Dialogue Transition");
        _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
            .Where(link => link != transition)
            .ToArray();
        foreach (var room in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
        {
            if (room == null || room.transitions == null || !room.transitions.Contains(transition)) continue;
            Undo.RecordObject(room, "Remove Dialogue Transition Reference");
            room.transitions = room.transitions.Where(link => link != transition).ToArray();
            EditorUtility.SetDirty(room);
        }

        EditorUtility.SetDirty(_dialogue);
        DestroyDialogueSubAsset(transition);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static string GetExecPortId(Port port) => port.userData as string;

    private void OnElementsAddedToGroup(Group group, IEnumerable<GraphElement> elements)
    {
        // Persist grouped asset IDs so group membership can be restored after reopening the graph.
        if (_isRebuilding || _preservingGroupContents) return;
        if (group is not DialogueGroup roomGroup) return;

        bool changed = false;
        foreach (var element in elements)
        {
            var guid = element.viewDataKey;
            if (!string.IsNullOrEmpty(guid) && !roomGroup.data.memberAssetGuids.Contains(guid))
            {
                if (!changed)
                    Undo.RecordObject(DialogueGraphLayout.instance, "Add Dialogue Graph Group Members");
                roomGroup.data.memberAssetGuids.Add(guid);
                changed = true;
            }
        }

        if (changed)
        {
            foreach (var sourceRoom in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
            {
                foreach (var link in sourceRoom?.transitions ?? System.Array.Empty<DialogueLink>())
                    ExpandGroupsToIncludeTransition(link);
            }

            DialogueGraphLayout.instance.SaveGroups();
        }
    }

    private void OnElementsRemovedFromGroup(Group group, IEnumerable<GraphElement> elements)
    {
        // Keep the saved membership list in sync when a node is dragged out of its group.
        if (_isRebuilding) return;
        if (group is not DialogueGroup roomGroup) return;

        bool changed = false;
        foreach (var element in elements)
        {
            if (!roomGroup.data.memberAssetGuids.Contains(element.viewDataKey)) continue;
            if (!changed)
                Undo.RecordObject(DialogueGraphLayout.instance, "Remove Dialogue Graph Group Members");
            roomGroup.data.memberAssetGuids.Remove(element.viewDataKey);
            changed = true;
        }

        if (changed)
            DialogueGraphLayout.instance.SaveGroups();
    }

    private void OnGroupTitleChanged(Group group, string newTitle)
    {
        // Extension point: validate or constrain group titles here if the editor needs naming rules.
        if (_isRebuilding) return;
        if (group is not DialogueGroup roomGroup) return;
        if (roomGroup.data.title == newTitle) return;

        Undo.RecordObject(DialogueGraphLayout.instance, "Rename Dialogue Graph Group");
        roomGroup.data.title = newTitle;
        DialogueGraphLayout.instance.SaveGroups();
    }

    private bool RemoveFromGraph(DialogueData room)
    {
        // Remove the graph reference and delete this Dialogue's owned sub-asset with Undo support.
        Undo.RecordObject(_dialogue, "Remove Dialogue Data from Graph");
        _dialogue.rooms = (_dialogue.rooms ?? System.Array.Empty<DialogueData>()).Where(r => r != room).ToArray();

        foreach (var transition in room.transitions ?? System.Array.Empty<DialogueLink>())
        {
            if (transition == null) continue;
            bool stillUsed = _dialogue.rooms.Any(otherRoom =>
                otherRoom != null && otherRoom.transitions != null && otherRoom.transitions.Contains(transition));
            if (stillUsed) continue;

            _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
                .Where(link => link != transition)
                .ToArray();
            DestroyDialogueSubAsset(transition);
        }

        EditorUtility.SetDirty(_dialogue);
        DestroyDialogueSubAsset(room);
        return true;
    }

    private void DestroyDialogueSubAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;

        if (asset is DialogueLink link)
            RemoveCurvePointHandles(link);

        var dialoguePath = AssetDatabase.GetAssetPath(_dialogue);
        if (AssetDatabase.IsSubAsset(asset) && AssetDatabase.GetAssetPath(asset) == dialoguePath)
            Undo.DestroyObjectImmediate(asset);
    }

    private bool RemoveFromGraph(DialogueLink link)
    {
        // Remove references and delete only a sub-asset owned by this Dialogue.
        Undo.RecordObject(_dialogue, "Remove Dialogue Transition from Graph");
        _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>()).Where(l => l != link).ToArray();

        foreach (var room in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
        {
            if (room == null || room.transitions == null || !room.transitions.Contains(link)) continue;
            Undo.RecordObject(room, "Remove Dialogue Transition Reference");
            room.transitions = room.transitions.Where(transition => transition != link).ToArray();
            EditorUtility.SetDirty(room);
        }

        if (link.destination != null)
        {
            Undo.RecordObject(link, "Clear Dialogue Transition Destination");
            link.destination = null;
            EditorUtility.SetDirty(link);
        }

        EditorUtility.SetDirty(_dialogue);
        DestroyDialogueSubAsset(link);
        return true;
    }

    private bool RemoveFromGraph(DialogueEntryData entry)
    {
        foreach (var link in GetAllDialogueLinks().Where(link => link.sourceEntryId == entry.id).ToList())
            RemoveFromGraph(link);
        Undo.RecordObject(_dialogue, "Remove Dialogue Entry");
        _dialogue.additionalEntries = (_dialogue.additionalEntries ?? new List<DialogueEntryData>())
            .Where(item => item != entry).ToList();
        _additionalEntryNodes.Remove(entry.id);
        EditorUtility.SetDirty(_dialogue);
        return true;
    }

    private bool RemoveFromGraph(DialogueExitData exit)
    {
        foreach (var link in GetAllDialogueLinks().Where(link => link.destinationExitId == exit.id).ToList())
            RemoveFromGraph(link);
        Undo.RecordObject(_dialogue, "Remove Dialogue Exit");
        _dialogue.additionalExits = (_dialogue.additionalExits ?? new List<DialogueExitData>())
            .Where(item => item != exit).ToList();
        _additionalExitNodes.Remove(exit.id);
        EditorUtility.SetDirty(_dialogue);
        return true;
    }

    public void AddExistingRoom(DialogueData room)
    {
        if (room == null || _roomNodes.ContainsKey(room)) return;
        var undoGroup = BeginUndoGroup("Add Dialogue Data to Graph");
        AddRoom(room);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
    }

    public void AddExistingLink(DialogueLink link)
    {
        if (link == null || (_dialogue.links != null && _dialogue.links.Contains(link))) return;
        var undoGroup = BeginUndoGroup("Add Dialogue Transition to Graph");
        AddLink(link);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
    }

    private void AddRoom(DialogueData room)
    {
        Undo.RecordObject(_dialogue, "Add Dialogue Data to Graph");
        var list = _dialogue.rooms?.ToList() ?? new List<DialogueData>();
        list.Add(room);
        _dialogue.rooms = list.ToArray();
        EditorUtility.SetDirty(_dialogue);

        RebuildGraph();
    }

    private void RemoveExecOutputLinks(DialogueData source, string portId)
    {
        if (source == null || string.IsNullOrEmpty(portId)) return;

        var links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
            .Where(link => link != null && link.source == source && link.sourcePortId == portId)
            .ToList();
        foreach (var link in source.transitions ?? System.Array.Empty<DialogueLink>())
        {
            if (link != null && link.sourcePortId == portId && !links.Contains(link))
                links.Add(link);
        }
        if (links.Count == 0) return;

        Undo.RecordObject(_dialogue, "Remove Selection Output Transitions");
        Undo.RecordObject(source, "Remove Selection Output Transitions");
        _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
            .Where(link => !links.Contains(link))
            .ToArray();
        source.transitions = (source.transitions ?? System.Array.Empty<DialogueLink>())
            .Where(link => !links.Contains(link))
            .ToArray();

        foreach (var link in links)
        {
            EditorUtility.SetDirty(link);
            DestroyDialogueSubAsset(link);
        }

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();
    }

    private void AddLink(DialogueLink link)
    {
        Undo.RecordObject(_dialogue, "Add Dialogue Transition to Graph");
        var list = _dialogue.links?.ToList() ?? new List<DialogueLink>();
        list.Add(link);
        _dialogue.links = list.ToArray();
        EditorUtility.SetDirty(_dialogue);

        RebuildGraph();
    }
}
