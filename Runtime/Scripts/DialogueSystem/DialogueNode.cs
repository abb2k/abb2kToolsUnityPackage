using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Linq;
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
    private readonly System.Action _graphChanged;
    private readonly System.Action<DialogueData, string> _removeOutputLinks;
    private readonly bool _isSelectorOnly;
    private readonly Dictionary<Port, string> _outputPortIds = new();
    private readonly VisualElement _selectionOptionsContainer = new();
    public Port execInput;
    public Port execOutput;
    public System.Action geometryChanged;

    public DialogueNode(
        DialogueData room,
        System.Action graphChanged,
        System.Action<DialogueData, string> removeOutputLinks,
        bool isSelectorOnly = false)
    {
        // Keep the asset reference so graph interactions can update or select the real object.
        this.room = room;
        _graphChanged = graphChanged;
        _removeOutputLinks = removeOutputLinks;
        _isSelectorOnly = isSelectorOnly;
        RegisterCallback<GeometryChangedEvent>(_ => geometryChanged?.Invoke());
        title = room.name;
        // Include local file ID because multiple graph nodes can share the Dialogue asset file.
        viewDataKey = DialogueGraphLayout.GetAssetKey(room);

        if (!isSelectorOnly)
        {
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
        }

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

        if (!isSelectorOnly)
        {
            var selectionToggle = new Toggle("Selection") { value = room.hasSelection };
            selectionToggle.RegisterValueChangedCallback(evt => SetSelectionEnabled(evt.newValue));
            mainContainer.Add(selectionToggle);
        }

        _selectionOptionsContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.Add(_selectionOptionsContainer);
        RefreshSelectionOptions();
        RebuildExecPorts();

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

    public Port GetInputPort(string id) => execInput != null && execInput.userData as string == id ? execInput : null;

    public Port GetOutputPort(string id)
    {
        if (execOutput != null && (string)execOutput.userData == id) return execOutput;
        return _outputPortIds.FirstOrDefault(pair => pair.Value == id).Key;
    }

    public string GetPortId(Port port)
    {
        if (port == execInput || port == execOutput) return port.userData as string;
        return _outputPortIds.TryGetValue(port, out var id) ? id : null;
    }

    private void RebuildExecPorts()
    {
        room.execInput ??= new DialogueExecPortData { label = "In" };
        room.execOutput ??= new DialogueExecPortData { label = "Exec" };
        room.selectionOptions ??= new List<DialogueSelectionOptionData>();

        inputContainer.Clear();
        outputContainer.Clear();
        _outputPortIds.Clear();

        execInput = CreateExecPort(Direction.Input, room.execInput);
        inputContainer.Add(execInput);
        execOutput = CreateExecPort(Direction.Output, room.execOutput);
        execOutput.visible = !room.hasSelection;
        outputContainer.Add(execOutput);

        if (!room.hasSelection) return;

        foreach (var option in room.selectionOptions)
        {
            if (option == null) continue;
            if (string.IsNullOrEmpty(option.id)) option.id = System.Guid.NewGuid().ToString("N");
            var optionPort = CreateExecPort(Direction.Output, new DialogueExecPortData
            {
                id = option.id,
                label = option.text
            });
            outputContainer.Add(optionPort);
            _outputPortIds[optionPort] = option.id;
        }

        RefreshPorts();
    }

    private static Port CreateExecPort(Direction direction, DialogueExecPortData pin)
    {
        var port = Port.Create<Edge>(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(DialogueExecPortData));
        port.portName = pin.label;
        port.userData = pin.id;
        return port;
    }

    private void RefreshSelectionOptions()
    {
        _selectionOptionsContainer.Clear();
        if (!room.hasSelection) return;

        var addOptionButton = new Button(AddSelectionOption) { text = "+ Option" };
        _selectionOptionsContainer.Add(addOptionButton);

        foreach (var option in room.selectionOptions ?? new List<DialogueSelectionOptionData>())
        {
            if (option == null) continue;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var optionField = new TextField { value = option.text, isDelayed = true };
            optionField.style.flexGrow = 1;
            optionField.RegisterValueChangedCallback(evt =>
            {
                if (option.text == evt.newValue) return;
                Undo.RecordObject(room, "Rename Dialogue Selection Option");
                option.text = evt.newValue;
                RebuildExecPorts();
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssets();
                _graphChanged?.Invoke();
            });
            row.Add(optionField);
            row.Add(new Button(() => RemoveSelectionOption(option)) { text = "-" });
            _selectionOptionsContainer.Add(row);
        }
    }

    private void AddSelectionOption()
    {
        Undo.RecordObject(room, "Add Dialogue Selection Option");
        room.selectionOptions ??= new List<DialogueSelectionOptionData>();
        room.selectionOptions.Add(new DialogueSelectionOptionData());
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        RefreshSelectionOptions();
        RebuildExecPorts();
        _graphChanged?.Invoke();
    }

    private void RemoveSelectionOption(DialogueSelectionOptionData option)
    {
        if (room.selectionOptions == null || !room.selectionOptions.Contains(option)) return;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Dialogue Selection Option");
        _removeOutputLinks?.Invoke(room, option.id);
        Undo.RecordObject(room, "Remove Dialogue Selection Option");
        room.selectionOptions.Remove(option);
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        RefreshSelectionOptions();
        RebuildExecPorts();
        _graphChanged?.Invoke();
        Undo.CollapseUndoOperations(undoGroup);
    }

    public void SetSelectionEnabled(bool enabled)
    {
        if (room.hasSelection == enabled) return;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Toggle Dialogue Selection");
        if (!enabled)
        {
            foreach (var option in room.selectionOptions ?? new List<DialogueSelectionOptionData>())
            {
                if (option != null)
                    _removeOutputLinks?.Invoke(room, option.id);
            }
        }
        Undo.RecordObject(room, "Toggle Dialogue Selection");
        room.hasSelection = enabled;
        if (enabled && (room.selectionOptions == null || room.selectionOptions.Count == 0))
            room.selectionOptions = new List<DialogueSelectionOptionData> { new() };
        RefreshSelectionOptions();
        RebuildExecPorts();
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        _graphChanged?.Invoke();
        Undo.CollapseUndoOperations(undoGroup);
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        // Ping the asset in Unity's Project window for quick navigation.
        evt.menu.AppendAction("Select Asset", _ =>
        {
            Selection.activeObject = room;
            EditorGUIUtility.PingObject(room);
        });
    }
}
