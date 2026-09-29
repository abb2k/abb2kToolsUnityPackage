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
        var drawColor = _edge.selected ? _edge.selectedColor : _lineColor;
        var painter = context.painter2D;

        painter.strokeColor = drawColor;
        painter.lineWidth = _lineWidth;
        painter.BeginPath();
        painter.MoveTo(start);
        for (int i = 1; i < localRoute.Count; i++)
            painter.LineTo(localRoute[i]);
        painter.Stroke();

        painter.fillColor = drawColor;
        painter.BeginPath();
        painter.MoveTo(end);
        painter.LineTo(arrowBase + perpendicular);
        painter.LineTo(arrowBase - perpendicular);
        painter.ClosePath();
        painter.Fill();
    }
}

public sealed class DialogueCurvePointHandle : VisualElement
{
    public readonly DialogueLink transition;
    public readonly DialogueCurvePoint point;
    private readonly VisualElement _coordinateRoot;
    private readonly System.Action _beginDrag;
    private readonly System.Action<Vector2> _move;
    private readonly System.Action _endDrag;
    private readonly System.Action _setLinear;
    private readonly System.Action _setEased;
    private readonly System.Action _delete;
    private bool _dragging;
    private bool _selected;
    private int _pointerId;

    public DialogueCurvePointHandle(
        DialogueLink transition,
        DialogueCurvePoint point,
        VisualElement coordinateRoot,
        System.Action beginDrag,
        System.Action<Vector2> move,
        System.Action endDrag,
        System.Action setLinear,
        System.Action setEased,
        System.Action delete)
    {
        this.transition = transition;
        this.point = point;
        _coordinateRoot = coordinateRoot;
        _beginDrag = beginDrag;
        _move = move;
        _endDrag = endDrag;
        _setLinear = setLinear;
        _setEased = setEased;
        _delete = delete;

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

        RegisterCallback<PointerDownEvent>(OnPointerDown);
        RegisterCallback<MouseDownEvent>(OnMouseDown);
        coordinateRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        coordinateRoot.RegisterCallback<PointerUpEvent>(OnPointerUp);
        coordinateRoot.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
    }

    public void SetGraphPosition(Vector2 position)
    {
        style.left = position.x - 6f;
        style.top = position.y - 6f;
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

    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0) return;
        _dragging = true;
        _pointerId = evt.pointerId;
        _beginDrag?.Invoke();
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_dragging || evt.pointerId != _pointerId) return;

        var local = _coordinateRoot.worldTransform.inverse.MultiplyPoint3x4(
            new Vector3(evt.position.x, evt.position.y, 0));
        var position = new Vector2(local.x, local.y);
        SetGraphPosition(position);
        _move?.Invoke(position);
        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_dragging || evt.pointerId != _pointerId) return;
        _dragging = false;
        _endDrag?.Invoke();
        evt.StopPropagation();
    }

    private void OnPointerCancel(PointerCancelEvent evt)
    {
        if (!_dragging || evt.pointerId != _pointerId) return;
        _dragging = false;
        _endDrag?.Invoke();
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

// Editor-only GraphView that projects a Dialogue asset's data into draggable nodes and edges.
public class DialogueGraphView : GraphView
{
    // These maps let graph operations find the backing ScriptableObject for each visual element.
    private readonly Dialogue _dialogue;
    private readonly Dictionary<DialogueData, DialogueNode> _roomNodes = new();
    private readonly Dictionary<DialogueLink, List<DialogueCurvePointHandle>> _curvePointHandles = new();
    private readonly Dictionary<Edge, Label> _overlapBadges = new();
    private DialogueCurvePointHandle _selectedCurvePointHandle;
    private List<DialogueLink> _lastOverlapCycle = new();
    private int _overlapCycleIndex = -1;
    private DialogueData _pendingTransitionSource;
    private DialogueTransitionPreview _transitionPreview;
    private Vector2 _previewMousePosition;
    private bool _isRebuilding;
    private bool _preservingGroupContents;
    private bool _adjustingGroupBounds;
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
        RegisterCallback<PointerMoveEvent>(OnGraphPointerMove);
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
            SelectCurvePointHandle(curvePointHandle);
            return;
        }
        SelectCurvePointHandle(null);

        if (_pendingTransitionSource != null)
        {
            var source = _pendingTransitionSource;
            _pendingTransitionSource = null;
            RemoveTransitionPreview();

            var destinationNode = FindDialogueNode(evt.target as VisualElement);
            if (destinationNode != null && destinationNode.room != source)
                CompleteTransition(source, destinationNode.room);

            return;
        }

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
        if (target is not (DialogueNode or DialogueGroup)) return;

        _pointerUndoGroup = BeginUndoGroup("Edit Dialogue Graph");
        Undo.RecordObject(DialogueGraphLayout.instance, "Edit Dialogue Graph");
    }

    private void OnGraphMouseUp(MouseUpEvent evt)
    {
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
        RefreshTransitionEdgeAnchors();

        if (_pointerUndoGroup < 0) return;

        Undo.CollapseUndoOperations(_pointerUndoGroup);
        _pointerUndoGroup = -1;
        DialogueGraphLayout.instance.SaveGroups();
        AssetDatabase.SaveAssets();
        AssetDatabase.SaveAssets();
    }

    private void OnGraphPointerMove(PointerMoveEvent evt)
    {
        if (_pendingTransitionSource != null)
            UpdateTransitionPreview(evt.position);
    }

    private void OnGraphKeyDown(KeyDownEvent evt)
    {
        if (_selectedCurvePointHandle == null || evt.keyCode is not (KeyCode.Delete or KeyCode.Backspace)) return;

        DeleteCurvePoint(_selectedCurvePointHandle.transition, _selectedCurvePointHandle.point);
        evt.StopPropagation();
    }

    private void OnAttachToPanel(AttachToPanelEvent evt)
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        RemoveTransitionPreview();
        DialogueGraphLayout.instance.SaveGroups();
    }

    private void OnUndoRedoPerformed()
    {
        _pendingTransitionSource = null;
        RemoveTransitionPreview();
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
            .Where(e => e is DialogueNode)
            .ToList();

        if (selectedNodes.Count > 0)
            evt.menu.AppendAction("Group Selection", _ => CreateGroupAroundSelection(selectedNodes));

        evt.menu.AppendAction("Create Dialogue Data", _ => CreateDialogueDataNode(position));
        evt.menu.AppendAction("Create Group", _ => CreateGroup("New Group", position, new Vector2(300, 200)));
    }

    // Creates DialogueData as a sub-asset, then registers it so the node survives graph rebuilds.
    public void CreateDialogueDataNode(Vector2 position)
    {
        var undoGroup = BeginUndoGroup("Create Dialogue Data");
        var data = CreateDialogueSubAsset<DialogueData>("DialogueData", "Create Dialogue Data");
        if (data == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        // The graph is reconstructed from this array, so registering here makes the node persistent.
        Undo.RecordObject(_dialogue, "Add Dialogue Data");
        _dialogue.rooms = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
            .Concat(new[] { data })
            .ToArray();
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();

        // Add the new visual directly so rebuilding does not run graph-removal callbacks.
        var node = new DialogueNode(data, BeginTransitionSelection);
        node.geometryChanged = OnDialogueNodeGeometryChanged;
        AddElement(node);
        _roomNodes[data] = node;
        node.SetPosition(new Rect(position, Vector2.zero));
        DialogueGraphLayout.instance.SaveGroups();
        Undo.CollapseUndoOperations(undoGroup);
    }

    // A transition is only created after the user selects a distinct destination room node.
    private void BeginTransitionSelection(DialogueData room, Vector2 pointerPosition)
    {
        _pendingTransitionSource = room;
        _previewMousePosition = pointerPosition;
        RemoveTransitionPreview();
        _transitionPreview = new DialogueTransitionPreview();
        contentViewContainer.Add(_transitionPreview);
        _transitionPreview.BringToFront();
        UpdateTransitionPreview(pointerPosition);
    }

    private void UpdateTransitionPreview(Vector2 pointerPosition)
    {
        if (_pendingTransitionSource == null || _transitionPreview == null ||
            !_roomNodes.TryGetValue(_pendingTransitionSource, out var sourceNode))
            return;

        _previewMousePosition = pointerPosition;
        var targetWorld = new Vector3(pointerPosition.x, pointerPosition.y, 0);
        var sourceLocal = sourceNode.GetNearestEdgePoint(targetWorld, out _);
        var sourceWorld = sourceNode.worldTransform.MultiplyPoint3x4(
            new Vector3(sourceLocal.x, sourceLocal.y, 0));
        _transitionPreview.SetPoints(WorldToGraph(sourceWorld), WorldToGraph(targetWorld));
    }

    private void RemoveTransitionPreview()
    {
        _transitionPreview?.RemoveFromHierarchy();
        _transitionPreview = null;
    }

    private void OnDialogueNodeGeometryChanged()
    {
        RefreshTransitionEdgeAnchors();
        RefreshTransitionStrokeSelection();
        if (_pendingTransitionSource != null)
            UpdateTransitionPreview(_previewMousePosition);
    }

    private void CompleteTransition(DialogueData source, DialogueData destination)
    {
        if (source == destination ||
            !_roomNodes.TryGetValue(source, out var sourceNode) ||
            !_roomNodes.TryGetValue(destination, out var destinationNode))
            return;

        var undoGroup = BeginUndoGroup("Create Dialogue Transition");
        var link = CreateDialogueSubAsset<DialogueLink>("DialogueLink", "Create Dialogue Transition");
        if (link == null)
        {
            Undo.CollapseUndoOperations(undoGroup);
            return;
        }

        Undo.RecordObject(_dialogue, "Add Dialogue Transition");
        _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
            .Concat(new[] { link })
            .ToArray();

        Undo.RecordObject(source, "Connect Dialogue Transition");
        source.transitions = (source.transitions ?? System.Array.Empty<DialogueLink>())
            .Concat(new[] { link })
            .ToArray();
        Undo.RecordObject(link, "Set Dialogue Transition Destination");
        link.destination = destination;

        AddDirectedEdge(sourceNode, destinationNode, link);

        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(link);
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
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

    private void CreateGroupAroundSelection(List<GraphElement> members)
    {
        // Expand the group's bounds around selected nodes, leaving room for its title and padding.
        const float pad = 40f;
        float minX = members.Min(m => m.GetPosition().xMin);
        float minY = members.Min(m => m.GetPosition().yMin);
        float maxX = members.Max(m => m.GetPosition().xMax);
        float maxY = members.Max(m => m.GetPosition().yMax);

        var memberRooms = members.OfType<DialogueNode>().Select(node => node.room).ToHashSet();
        foreach (var sourceRoom in memberRooms)
        {
            foreach (var link in sourceRoom.transitions ?? System.Array.Empty<DialogueLink>())
            {
                if (link == null || link.destination == null || !memberRooms.Contains(link.destination)) continue;
                foreach (var point in link.curvePoints ?? new List<DialogueCurvePoint>())
                {
                    if (point == null) continue;
                    minX = Mathf.Min(minX, point.position.x - 8f);
                    minY = Mathf.Min(minY, point.position.y - 8f);
                    maxX = Mathf.Max(maxX, point.position.x + 8f);
                    maxY = Mathf.Max(maxY, point.position.y + 8f);
                }
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
        var group = new DialogueGroup(data, delta => MoveGroupedTransitionPoints(data, delta));
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

        var memberRooms = _roomNodes.Values
            .Where(node => groupData.memberAssetGuids.Contains(node.viewDataKey))
            .Select(node => node.room)
            .ToHashSet();
        if (memberRooms.Count < 2) return;

        var movedLinks = new HashSet<DialogueLink>();
        foreach (var sourceRoom in memberRooms)
        {
            foreach (var link in sourceRoom.transitions ?? System.Array.Empty<DialogueLink>())
            {
                if (link == null || !memberRooms.Contains(link.destination) ||
                    !movedLinks.Add(link) || link.curvePoints == null)
                    continue;

                Undo.RecordObject(link, "Move Grouped Transition Points");
                foreach (var point in link.curvePoints)
                {
                    if (point == null) continue;
                    point.position += delta;
                    if (_curvePointHandles.TryGetValue(link, out var handles))
                    {
                        var handle = handles.FirstOrDefault(candidate => candidate.point == point);
                        handle?.SetGraphPosition(point.position);
                    }
                }

                EditorUtility.SetDirty(link);
                MarkTransitionStrokeDirty(link);
            }
        }
    }

    private void ExpandGroupsToIncludeTransition(DialogueLink link)
    {
        if (_isRebuilding || link == null || link.destination == null || link.curvePoints == null) return;

        var destinationKey = DialogueGraphLayout.GetAssetKey(link.destination);
        var sourceKeys = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
            .Where(room => room != null && room.transitions != null && room.transitions.Contains(link))
            .Select(DialogueGraphLayout.GetAssetKey)
            .ToHashSet();

        foreach (var groupData in DialogueGraphLayout.instance.GetGroups(_dialogue))
        {
            if (!groupData.memberAssetGuids.Contains(destinationKey) ||
                !sourceKeys.Any(groupData.memberAssetGuids.Contains))
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

            var memberNodes = _roomNodes.Values
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
        ClearAllCurvePointHandles();
        ClearOverlapBadges();
        _lastOverlapCycle.Clear();
        _overlapCycleIndex = -1;
        _isRebuilding = true;
        try
        {
            DeleteElements(graphElements.ToList());
        }
        finally
        {
            _isRebuilding = false;
        }

        _roomNodes.Clear();

        var rooms = (_dialogue.rooms ?? System.Array.Empty<DialogueData>()).Where(r => r != null).ToList();
        // First create all nodes so edges can resolve both endpoints in the second pass.
        foreach (var room in rooms)
        {
            var node = new DialogueNode(room, BeginTransitionSelection);
            node.geometryChanged = OnDialogueNodeGeometryChanged;
            AddElement(node);
            _roomNodes[room] = node;
        }

        // Each transition asset is now represented by one edge between its source and destination.
        foreach (var room in rooms)
        {
            if (room.transitions == null) continue;

            foreach (var link in room.transitions)
            {
                if (link == null || link.destination == null) continue;
                if (!_roomNodes.TryGetValue(link.destination, out var destinationNode)) continue;

                AddDirectedEdge(_roomNodes[room], destinationNode, link);
            }
        }

        // Restore groups after nodes exist so their saved member GUIDs can be resolved.
        foreach (var groupData in DialogueGraphLayout.instance.GetGroups(_dialogue))
        {
            var group = new DialogueGroup(groupData, delta => MoveGroupedTransitionPoints(groupData, delta));
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
        return null;
    }

    // All edges flow from an output port to an input port; the arrow marks the destination.
    private Edge AddDirectedEdge(DialogueNode source, DialogueNode destination, DialogueLink transition)
    {
        var outputGuide = GetTransitionGuideWorld(transition, sourceEndpoint: true, destination);
        var inputGuide = GetTransitionGuideWorld(transition, sourceEndpoint: false, source);
        var outputPosition = source.GetNearestEdgePoint(outputGuide, out var outputSide);
        var inputPosition = destination.GetNearestEdgePoint(inputGuide, out var inputSide);
        var output = source.CreateAnchorPort(outputSide, Direction.Output, outputPosition);
        var input = destination.CreateAnchorPort(inputSide, Direction.Input, inputPosition);
        var edge = output.ConnectTo(input);
        edge.userData = new DialogueTransitionEdgeData
        {
            transition = transition,
            outputSide = outputSide,
            inputSide = inputSide
        };
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
        AddElement(edge);
        edge.UpdateEdgeControl();
        var transitionData = (DialogueTransitionEdgeData)edge.userData;
        transitionData.stroke = new DialogueTransitionStroke(this, edge, lineWidth, lineColor);
        edge.hierarchy.Add(transitionData.stroke);
        transitionData.stroke.BringToFront();
        CreateCurvePointHandles(transition);
        HideBuiltInBezier(edge);
        edge.edgeControl.MarkDirtyRepaint();
        edge.schedule.Execute(() =>
        {
            RefreshTransitionEdgeAnchor(edge);
        }).ExecuteLater(0);
        RefreshTransitionStrokeSelection();
        return edge;
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

    private Vector3 GetTransitionGuideWorld(DialogueLink transition, bool sourceEndpoint, DialogueNode fallbackNode)
    {
        var points = transition.curvePoints;
        if (points != null && points.Count > 0)
        {
            var point = sourceEndpoint ? points[0] : points[points.Count - 1];
            if (point != null)
            {
                var position = point.position;
                return contentViewContainer.worldTransform.MultiplyPoint3x4(
                    new Vector3(position.x, position.y, 0));
            }
        }

        return fallbackNode.worldBound.center;
    }

    private void RefreshTransitionEdgeAnchors()
    {
        foreach (var edge in graphElements.OfType<Edge>())
            RefreshTransitionEdgeAnchor(edge);
    }

    private void RefreshTransitionEdgeAnchor(Edge edge)
    {
        if (edge.output?.node is not DialogueNode source ||
            edge.input?.node is not DialogueNode destination ||
            edge.userData is not DialogueTransitionEdgeData edgeData)
            return;

        var outputGuide = GetTransitionGuideWorld(edgeData.transition, sourceEndpoint: true, destination);
        var inputGuide = GetTransitionGuideWorld(edgeData.transition, sourceEndpoint: false, source);
        var outputPosition = source.GetNearestEdgePoint(outputGuide, out var outputSide);
        var inputPosition = destination.GetNearestEdgePoint(inputGuide, out var inputSide);

        source.PositionAnchorPort(edge.output, outputPosition);
        destination.PositionAnchorPort(edge.input, inputPosition);
        edgeData.outputSide = outputSide;
        edgeData.inputSide = inputSide;

        edge.UpdateEdgeControl();
        HideBuiltInBezier(edge);
        edgeData.stroke?.MarkDirtyRepaint();
        edge.edgeControl.MarkDirtyRepaint();
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

        return path;
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
            int undoGroup = -1;
            var point = curvePoint;
            var handle = new DialogueCurvePointHandle(
                link,
                point,
                contentViewContainer,
                () =>
                {
                    undoGroup = BeginUndoGroup("Move Transition Curve Point");
                    Undo.RecordObject(link, "Move Transition Curve Point");
                },
                position =>
                {
                    point.position = position;
                    EditorUtility.SetDirty(link);
                    ExpandGroupsToIncludeTransition(link);
                    MarkTransitionStrokeDirty(link);
                },
                () =>
                {
                    if (undoGroup < 0) return;
                    RefreshTransitionEdgesForLink(link);
                    DialogueGraphLayout.instance.SaveGroups();
                    AssetDatabase.SaveAssets();
                    Undo.CollapseUndoOperations(undoGroup);
                    undoGroup = -1;
                },
                () => SetCurvePointMode(link, point, DialogueCurvePointMode.Linear),
                () => SetCurvePointMode(link, point, DialogueCurvePointMode.Eased),
                () => DeleteCurvePoint(link, point));
            handle.SetGraphPosition(point.position);
            handle.SetPointMode(point.mode);
            contentViewContainer.Add(handle);
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
            handle.RemoveFromHierarchy();
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
                handle.RemoveFromHierarchy();
            }
        }
        _curvePointHandles.Clear();
    }

    private void SelectCurvePointHandle(DialogueCurvePointHandle handle)
    {
        if (_selectedCurvePointHandle == handle) return;
        _selectedCurvePointHandle?.SetSelected(false);
        if (handle != null)
            ClearSelection();
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
                RefreshTransitionEdgeAnchor(edge);
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
                RefreshTransitionEdgeAnchor(edge);
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
            foreach (var group in change.elementsToRemove.OfType<DialogueGroup>().ToList())
            {
                _preservingGroupContents = true;
                try
                {
                    foreach (var member in group.containedElements.ToList())
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
                        if (edge.userData is DialogueTransitionEdgeData transitionEdge)
                        {
                            if (edge.output?.node is DialogueNode source)
                                source.RemoveAnchorPort(edge.output);
                            if (edge.input?.node is DialogueNode destination)
                                destination.RemoveAnchorPort(edge.input);
                        }
                        break;
                    case DialogueGroup roomGroup:
                        DialogueGraphLayout.instance.RemoveGroup(roomGroup.data);
                        break;
                    case DialogueNode roomNode:
                        dirty |= RemoveFromGraph(roomNode.room);
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
        if (edge.output?.node is not DialogueNode sourceNode ||
            edge.input?.node is not DialogueNode destinationNode ||
            edge.userData is not DialogueTransitionEdgeData transitionEdge)
            return false;

        var transition = transitionEdge.transition;
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
                transitionEdge.transition = transition;
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

            var roomTransitions = sourceNode.room.transitions?.ToList() ?? new List<DialogueLink>();
            if (!roomTransitions.Contains(transition))
            {
                Undo.RecordObject(sourceNode.room, "Connect Dialogue Transition");
                roomTransitions.Add(transition);
                sourceNode.room.transitions = roomTransitions.ToArray();
                changed = true;
            }

            if (transition.destination != destinationNode.room)
            {
                Undo.RecordObject(transition, "Set Dialogue Transition Destination");
                transition.destination = destinationNode.room;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(_dialogue);
                EditorUtility.SetDirty(sourceNode.room);
                EditorUtility.SetDirty(transition);
                AssetDatabase.SaveAssets();
            }

            if (undoGroup >= 0)
                Undo.CollapseUndoOperations(undoGroup);
            return changed;
        }

        if (transition == null || sourceNode.room.transitions == null ||
            !sourceNode.room.transitions.Contains(transition))
            return false;

        Undo.RecordObject(sourceNode.room, "Delete Dialogue Transition");
        sourceNode.room.transitions = sourceNode.room.transitions
            .Where(link => link != transition)
            .ToArray();

        bool stillUsed = (_dialogue.rooms ?? System.Array.Empty<DialogueData>())
            .Where(room => room != null && room != sourceNode.room)
            .Any(room => room.transitions != null && room.transitions.Contains(transition));
        if (!stillUsed)
        {
            Undo.RecordObject(_dialogue, "Delete Dialogue Transition");
            _dialogue.links = (_dialogue.links ?? System.Array.Empty<DialogueLink>())
                .Where(link => link != transition)
                .ToArray();
            EditorUtility.SetDirty(_dialogue);
            DestroyDialogueSubAsset(transition);
        }

        EditorUtility.SetDirty(sourceNode.room);
        AssetDatabase.SaveAssets();
        return true;
    }

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

        var node = new DialogueNode(room, BeginTransitionSelection);
        node.geometryChanged = OnDialogueNodeGeometryChanged;
        AddElement(node);
        _roomNodes[room] = node;

        foreach (var link in room.transitions ?? System.Array.Empty<DialogueLink>())
        {
            if (link != null && link.destination != null && _roomNodes.TryGetValue(link.destination, out var destinationNode))
                AddDirectedEdge(node, destinationNode, link);
        }

        foreach (var sourceRoom in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
        {
            if (sourceRoom == null || sourceRoom == room || sourceRoom.transitions == null) continue;
            foreach (var link in sourceRoom.transitions)
            {
                if (link != null && link.destination == room && _roomNodes.TryGetValue(sourceRoom, out var sourceNode))
                    AddDirectedEdge(sourceNode, node, link);
            }
        }
    }

    private void AddLink(DialogueLink link)
    {
        Undo.RecordObject(_dialogue, "Add Dialogue Transition to Graph");
        var list = _dialogue.links?.ToList() ?? new List<DialogueLink>();
        list.Add(link);
        _dialogue.links = list.ToArray();
        EditorUtility.SetDirty(_dialogue);

        foreach (var room in _dialogue.rooms ?? System.Array.Empty<DialogueData>())
        {
            if (room != null && room.transitions != null && room.transitions.Contains(link) &&
                link.destination != null && _roomNodes.TryGetValue(room, out var roomNode) &&
                _roomNodes.TryGetValue(link.destination, out var destinationNode))
                AddDirectedEdge(roomNode, destinationNode, link);
        }
    }
}
