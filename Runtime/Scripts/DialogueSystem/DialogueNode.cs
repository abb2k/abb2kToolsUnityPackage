using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum DialogueNodeSide
{
    Left,
    Right,
    Top,
    Bottom
}

// Visual representation of one DialogueData asset; edits to its position are saved separately.
public class DialogueNode : Node
{
    public readonly DialogueData room;
    private readonly System.Action<DialogueData, Vector2> _createTransition;
    private readonly VisualElement _anchorLayer = new();
    public System.Action geometryChanged;

    public DialogueNode(DialogueData room, System.Action<DialogueData, Vector2> createTransition)
    {
        // Keep the asset reference so graph interactions can update or select the real object.
        this.room = room;
        _createTransition = createTransition;
        RegisterCallback<GeometryChangedEvent>(_ => geometryChanged?.Invoke());
        title = room.name;
        // Include local file ID because multiple graph nodes can share the Dialogue asset file.
        viewDataKey = DialogueGraphLayout.GetAssetKey(room);

        // Keep invisible per-edge ports in node-local coordinates on an overlay layer.
        _anchorLayer.name = "transition-anchor-layer";
        ConfigureAnchorLayer(_anchorLayer);
        hierarchy.Add(_anchorLayer);

        // Show the backing asset in the node without allowing edits through this display field.
        var objectField = new ObjectField
        {
            objectType = typeof(DialogueData),
            value = room,
            allowSceneObjects = false
        };

        objectField.style.width = 190;
        objectField.style.height = 30;

        objectField.SetEnabled(false);
        mainContainer.Add(objectField);

        var nameField = new TextField("Name")
        {
            value = room.name,
            isDelayed = true
        };
        nameField.RegisterValueChangedCallback(evt =>
        {
            var newName = evt.newValue?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                nameField.SetValueWithoutNotify(room.name);
                return;
            }

            if (newName == room.name) return;

            Undo.RecordObject(room, "Rename Dialogue Data");
            room.name = newName;
            title = newName;
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
        });
        mainContainer.Add(nameField);

        // Extension point: add dialogue text, speaker, timing, and other editing controls here.
        this.style.width = 200;
        this.style.minHeight = 100;

        RefreshExpandedState();
        RefreshPorts();

        SetPosition(new Rect(DialogueGraphLayout.instance.GetPosition(room), Vector2.zero));
    }

    public override void SetPosition(Rect newPos)
    {
        // Persist only the position; the node's content and connections belong to the asset.
        base.SetPosition(newPos);
        DialogueGraphLayout.instance.SetPosition(room, newPos.position);
    }

    public Vector2 GetNearestEdgePoint(DialogueNode other, out DialogueNodeSide side)
        => GetNearestEdgePoint(other.worldBound.center, out side);

    public Vector2 GetNearestEdgePoint(Vector3 targetWorld, out DialogueNodeSide side)
    {
        var target = GetWorldPointLocal(targetWorld);
        Vector2 left = GetPointOnSide(targetWorld, DialogueNodeSide.Left);
        Vector2 right = GetPointOnSide(targetWorld, DialogueNodeSide.Right);
        Vector2 top = GetPointOnSide(targetWorld, DialogueNodeSide.Top);
        Vector2 bottom = GetPointOnSide(targetWorld, DialogueNodeSide.Bottom);

        side = DialogueNodeSide.Left;
        var closest = left;
        float closestDistance = (target - left).sqrMagnitude;

        ConsiderPoint(right, DialogueNodeSide.Right, target, ref side, ref closest, ref closestDistance);
        ConsiderPoint(top, DialogueNodeSide.Top, target, ref side, ref closest, ref closestDistance);
        ConsiderPoint(bottom, DialogueNodeSide.Bottom, target, ref side, ref closest, ref closestDistance);
        return closest;
    }

    public Vector2 GetPointOnSide(Vector3 targetWorld, DialogueNodeSide side)
    {
        var bounds = GetLocalBounds();
        var target = GetWorldPointLocal(targetWorld);
        float horizontalInset = Mathf.Min(24f, bounds.width * 0.25f);
        float verticalInset = Mathf.Min(24f, bounds.height * 0.25f);
        float minX = bounds.xMin + horizontalInset;
        float maxX = bounds.xMax - horizontalInset;
        float minY = bounds.yMin + verticalInset;
        float maxY = bounds.yMax - verticalInset;

        return side switch
        {
            DialogueNodeSide.Left => new Vector2(bounds.xMin, Mathf.Clamp(target.y, minY, maxY)),
            DialogueNodeSide.Right => new Vector2(bounds.xMax, Mathf.Clamp(target.y, minY, maxY)),
            DialogueNodeSide.Top => new Vector2(Mathf.Clamp(target.x, minX, maxX), bounds.yMin),
            DialogueNodeSide.Bottom => new Vector2(Mathf.Clamp(target.x, minX, maxX), bounds.yMax),
            _ => bounds.center
        };
    }

    private Rect GetLocalBounds()
    {
        float width = layout.width > 0 ? layout.width : Mathf.Max(1, resolvedStyle.width);
        float height = layout.height > 0 ? layout.height : Mathf.Max(1, resolvedStyle.height);
        return new Rect(0, 0, width, height);
    }

    private Vector2 GetWorldPointLocal(Vector3 worldPoint)
    {
        var local = worldTransform.inverse.MultiplyPoint3x4(worldPoint);
        return new Vector2(local.x, local.y);
    }

    public Port CreateAnchorPort(DialogueNodeSide side, Direction direction, Vector2 localPosition)
    {
        var orientation = side is DialogueNodeSide.Left or DialogueNodeSide.Right
            ? Orientation.Horizontal
            : Orientation.Vertical;
        var port = Port.Create<Edge>(orientation, direction, Port.Capacity.Single, typeof(DialogueData));
        port.portName = string.Empty;
        port.pickingMode = PickingMode.Ignore;
        port.style.position = Position.Absolute;
        port.style.width = 10;
        port.style.height = 10;
        port.style.opacity = 0;
        PositionAnchorPort(port, localPosition);
        _anchorLayer.Add(port);
        return port;
    }

    public void PositionAnchorPort(Port port, Vector2 localPosition)
    {
        const float halfPortSize = 5f;
        port.style.left = localPosition.x - halfPortSize;
        port.style.top = localPosition.y - halfPortSize;
    }

    public void RemoveAnchorPort(Port port)
    {
        port.DisconnectAll();
        port.RemoveFromHierarchy();
    }

    private static void ConsiderPoint(
        Vector2 candidate,
        DialogueNodeSide candidateSide,
        Vector2 target,
        ref DialogueNodeSide closestSide,
        ref Vector2 closest,
        ref float closestDistance)
    {
        float distance = (target - candidate).sqrMagnitude;
        if (distance >= closestDistance) return;

        closestSide = candidateSide;
        closest = candidate;
        closestDistance = distance;
    }

    private static void ConfigureAnchorLayer(VisualElement container)
    {
        container.style.position = Position.Absolute;
        container.style.left = 0;
        container.style.right = 0;
        container.style.top = 0;
        container.style.bottom = 0;
        container.pickingMode = PickingMode.Ignore;
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        // Let the owning graph create and connect a transition from this room node.
        var pointerPosition = evt.mousePosition;
        evt.menu.AppendAction("Create Transition", _ => _createTransition?.Invoke(room, pointerPosition));

        // Ping the asset in Unity's Project window for quick navigation.
        evt.menu.AppendAction("Select Asset", _ =>
        {
            Selection.activeObject = room;
            EditorGUIUtility.PingObject(room);
        });
    }
}
