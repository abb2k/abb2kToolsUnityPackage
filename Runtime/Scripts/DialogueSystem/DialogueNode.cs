using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using System;
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

public sealed class DialogueContentInputPortData
{
    public string optionId;
    public string fieldPath;
}

// Visual representation of one DialogueData asset; edits to its position are saved separately.
public class DialogueNode : Node
{
    private const float ResizeGridSize = 25f;
    public readonly DialogueData room;
    private readonly System.Action _graphChanged;
    private readonly System.Action<DialogueData, string> _removeOutputLinks;
    private readonly bool _isSelectorOnly;
    private readonly Dictionary<Port, string> _outputPortIds = new();
    private readonly Dictionary<string, Port> _inputParameterPorts = new();
    private readonly VisualElement _selectionOptionsContainer = new();
    private readonly VisualElement _contentContainer = new();
    private string _selectedSelectionOptionId;
    private VisualElement _resizeHandle;
    private Vector2 _resizeStartPointer;
    private Vector2 _resizeStartSize;
    private Vector2 _resizeCurrentSize;
    private bool _isResizing;
    public Port execInput;
    public Port execOutput;
    public System.Action geometryChanged;

    public Port GetInputParameterPort(string fieldPath) => GetInputParameterPort(null, fieldPath);

    public Port GetInputParameterPort(string optionId, string fieldPath) =>
        _inputParameterPorts.TryGetValue(GetInputParameterKey(optionId, fieldPath), out var port) ? port : null;

    public void UnexposeInputParameter(DialogueContentInputPortData input)
    {
        if (input == null || string.IsNullOrEmpty(input.fieldPath)) return;
        var content = input.optionId == null
            ? room.dialogueContent
            : room.selectionOptions?.FirstOrDefault(option =>
                option != null && option.id == input.optionId)?.dialogueContent;
        var bindings = content?.fieldBindings;
        int bindingIndex = bindings?.FindIndex(binding => binding != null &&
            binding.fieldPath == input.fieldPath) ?? -1;
        if (bindingIndex < 0) return;

        Undo.RecordObject(room, "Un-expose Dialogue Content Field");
        bindings.RemoveAt(bindingIndex);
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        _graphChanged?.Invoke();
    }

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
        RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(ApplyStoredNodeLayout).ExecuteLater(0));
        RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            if (_isResizing)
                SaveResizeSize();
            CancelResize();
            if (room == null)
            {
                _contentContainer.Unbind();
                _selectionOptionsContainer.Unbind();
            }
        });
        title = room.name;
        ApplyNodeStyling();
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

        mainContainer.Add(CreateSectionHeader(isSelectorOnly ? "Selector Settings" : "Dialogue Settings"));
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
            _contentContainer.style.flexDirection = FlexDirection.Column;
            _contentContainer.style.marginTop = 5;
            _contentContainer.style.marginBottom = 6;
            _contentContainer.style.paddingLeft = 6;
            _contentContainer.style.paddingRight = 6;
            _contentContainer.style.paddingTop = 4;
            _contentContainer.style.paddingBottom = 24;
            _contentContainer.style.backgroundColor = new Color(0.12f, 0.17f, 0.19f, 1f);
            _contentContainer.style.borderTopWidth = 1;
            _contentContainer.style.borderBottomWidth = 1;
            _contentContainer.style.borderLeftWidth = 1;
            _contentContainer.style.borderRightWidth = 1;
            _contentContainer.style.borderTopColor = new Color(0.29f, 0.45f, 0.47f, 1f);
            _contentContainer.style.borderBottomColor = new Color(0.29f, 0.45f, 0.47f, 1f);
            _contentContainer.style.borderLeftColor = new Color(0.29f, 0.45f, 0.47f, 1f);
            _contentContainer.style.borderRightColor = new Color(0.29f, 0.45f, 0.47f, 1f);
            mainContainer.Add(_contentContainer);
            RefreshDialogueContentEditor();
        }

        _selectionOptionsContainer.style.flexDirection = FlexDirection.Column;
        _selectionOptionsContainer.style.marginTop = 6;
        _selectionOptionsContainer.style.paddingTop = 6;
        _selectionOptionsContainer.style.borderTopWidth = 1;
        _selectionOptionsContainer.style.borderTopColor = new Color(0.36f, 0.53f, 0.56f, 1f);
        mainContainer.Add(_selectionOptionsContainer);
        RefreshSelectionOptions();
        RebuildExecPorts();

        AddResizeHandle();
        this.style.width = 200;
        this.style.minHeight = 100;
        this.style.minWidth = 200;
        this.style.maxWidth = 1200;
        this.style.maxHeight = 1600;

        RefreshExpandedState();
        RefreshPorts();

        SetPosition(new Rect(DialogueGraphLayout.instance.GetPosition(room), GetStoredNodeSize()));
    }

    private void ApplyNodeStyling()
    {
        var nodeBackground = new Color(0.16f, 0.22f, 0.25f, 1f);
        style.backgroundColor = nodeBackground;
        mainContainer.style.backgroundColor = nodeBackground;
        style.borderTopWidth = 1;
        style.borderBottomWidth = 1;
        style.borderLeftWidth = 1;
        style.borderRightWidth = 1;
        style.borderTopColor = new Color(0.36f, 0.53f, 0.56f, 1f);
        style.borderBottomColor = new Color(0.36f, 0.53f, 0.56f, 1f);
        style.borderLeftColor = new Color(0.36f, 0.53f, 0.56f, 1f);
        style.borderRightColor = new Color(0.36f, 0.53f, 0.56f, 1f);

        titleContainer.style.backgroundColor = new Color(0.12f, 0.34f, 0.36f, 1f);
        titleContainer.style.borderBottomWidth = 1;
        titleContainer.style.borderBottomColor = new Color(0.43f, 0.69f, 0.66f, 1f);
    }

    private static Label CreateSectionHeader(string text)
    {
        return new Label(text)
        {
            style =
            {
                marginTop = 4,
                marginBottom = 4,
                color = new Color(0.55f, 0.78f, 0.76f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };
    }

    private void AddResizeHandle()
    {
        _resizeHandle = new Label("Resize")
        {
            style =
            {
                position = Position.Absolute,
                right = 4,
                bottom = 4,
                width = 58,
                height = 22,
                unityTextAlign = TextAnchor.MiddleCenter,
                color = new Color(0.93f, 0.98f, 0.97f, 1f),
                backgroundColor = new Color(0.08f, 0.24f, 0.25f, 1f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.43f, 0.69f, 0.66f, 1f),
                borderBottomColor = new Color(0.43f, 0.69f, 0.66f, 1f),
                borderLeftColor = new Color(0.43f, 0.69f, 0.66f, 1f),
                borderRightColor = new Color(0.43f, 0.69f, 0.66f, 1f)
            },
            tooltip = "Drag to resize this dialogue node"
        };
        _resizeHandle.RegisterCallback<MouseDownEvent>(OnResizeMouseDown);
        _resizeHandle.RegisterCallback<MouseMoveEvent>(OnResizeMouseMove);
        _resizeHandle.RegisterCallback<MouseUpEvent>(OnResizeMouseUp);
        _resizeHandle.pickingMode = PickingMode.Position;
        this.Add(_resizeHandle);
        _resizeHandle.BringToFront();
    }

    private void OnResizeMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || room == null || panel == null) return;
        _isResizing = true;
        _resizeStartPointer = parent.WorldToLocal(evt.mousePosition);
        var position = GetPosition();
        _resizeStartSize = new Vector2(position.width, position.height);
        _resizeCurrentSize = _resizeStartSize;
        Undo.RecordObject(room, "Resize Dialogue Node");
        Undo.RecordObject(DialogueGraphLayout.instance, "Resize Dialogue Node");
        _resizeHandle.CaptureMouse();
        evt.StopImmediatePropagation();
    }

    private void OnResizeMouseMove(MouseMoveEvent evt)
    {
        if (!_isResizing) return;
        if (room == null || panel == null)
        {
            CancelResize();
            return;
        }

        var delta = parent.WorldToLocal(evt.mousePosition) - _resizeStartPointer;
        var size = new Vector2(
            Mathf.Clamp(Mathf.Round((_resizeStartSize.x + delta.x) / ResizeGridSize) * ResizeGridSize, 200, 1200),
            Mathf.Clamp(Mathf.Round((_resizeStartSize.y + delta.y) / ResizeGridSize) * ResizeGridSize, 100, 1600));
        _resizeCurrentSize = size;
        ApplyResizeSize(size);
        room.GraphNodeSize = size;
        room.GraphNodeSizeWasUserSpecified = true;
        EditorUtility.SetDirty(room);
        evt.StopImmediatePropagation();
    }

    private void OnResizeMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0 || !_isResizing) return;
        if (room == null || panel == null)
        {
            CancelResize();
            return;
        }

        CommitResize();
        evt.StopImmediatePropagation();
    }

    private void ApplyResizeSize(Vector2 size)
    {
        var position = GetPosition();
        style.width = size.x;
        style.height = size.y;
        base.SetPosition(new Rect(position.position, size));
    }

    private void CommitResize()
    {
        if (!_isResizing || room == null || panel == null)
        {
            CancelResize();
            return;
        }

        _isResizing = false;
        ApplyResizeSize(_resizeCurrentSize);
        SaveResizeSize();
        if (_resizeHandle.HasMouseCapture()) _resizeHandle.ReleaseMouse();
    }

    private void SaveResizeSize()
    {
        if (room == null) return;

        room.GraphNodeSize = _resizeCurrentSize;
        room.GraphNodeSizeWasUserSpecified = true;
        EditorUtility.SetDirty(room);
        DialogueGraphLayout.instance.SetSize(room, _resizeCurrentSize);
        SaveNodeAsset();
    }

    private void CancelResize()
    {
        _isResizing = false;
        if (_resizeHandle != null && _resizeHandle.HasMouseCapture())
            _resizeHandle.ReleaseMouse();
    }

    private void RefreshDialogueContentEditor()
    {
        _contentContainer.Clear();
        _contentContainer.Add(CreateSectionHeader("Dialogue Content"));
        var settings = DialogueContentProjectSettings.Load();
        var contentType = settings?.DefaultContentType;
        if (contentType == null)
        {
            _contentContainer.Add(new Label("Set a project default in Project Settings > Abb2kTools > Dialogue Content."));
            _contentContainer.Add(new Button(() => SettingsService.OpenProjectSettings(
                "Project/Abb2kTools/Dialogue Content")) { text = "Set Default Content" });
            return;
        }

        if (room.dialogueContent?.ContentType != contentType)
        {
            Undo.RecordObject(room, "Initialize Dialogue Content");
            room.dialogueContent = new DialogueContentValue();
            room.dialogueContent.SetType(contentType);
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
        }

        var serializedRoom = new SerializedObject(room);
        serializedRoom.Update();
        var contentProperty = serializedRoom.FindProperty("dialogueContent");
        if (contentProperty == null) return;
        var contentField = new PropertyField(contentProperty, "Content");
        contentField.Bind(serializedRoom);
        contentField.RegisterCallback<SerializedPropertyChangeEvent>(_ => EditorUtility.SetDirty(room));
        _contentContainer.Add(contentField);
    }

    public void RefreshInputParameterPorts() => _graphChanged?.Invoke();

    public override void SetPosition(Rect newPos)
    {
        // Persist only the position; the node's content and connections belong to the asset.
        if (room == null)
        {
            base.SetPosition(newPos);
            return;
        }

        var layout = DialogueGraphLayout.instance;
        var size = GetStoredNodeSize();
        base.SetPosition(new Rect(newPos.position, size));
        style.width = size.x;
        style.height = size.y;
        layout.SetPosition(room, newPos.position);
    }

    private Vector2 GetStoredNodeSize()
    {
        if (room == null) return new Vector2(380, 260);

        var layout = DialogueGraphLayout.instance;
        if (room.GraphNodeSizeWasUserSpecified && IsValidSize(room.GraphNodeSize))
        {
            layout.SetSize(room, room.GraphNodeSize);
            return room.GraphNodeSize;
        }

        if (layout.TryGetUserSpecifiedSize(room, out var userSize))
        {
            SyncNodeAssetSize(userSize);
            return userSize;
        }

        if (layout.TryGetSavedSize(room, out var legacyLayoutSize) &&
            legacyLayoutSize != new Vector2(380, 260))
        {
            room.GraphNodeSize = legacyLayoutSize;
            room.GraphNodeSizeWasUserSpecified = true;
            EditorUtility.SetDirty(room);
            layout.SetSize(room, legacyLayoutSize);
            SaveNodeAsset();
            return legacyLayoutSize;
        }

        if (IsValidSize(room.GraphNodeSize) && room.GraphNodeSize != new Vector2(380, 260))
        {
            room.GraphNodeSizeWasUserSpecified = true;
            EditorUtility.SetDirty(room);
            layout.SetSize(room, room.GraphNodeSize);
            SaveNodeAsset();
            return room.GraphNodeSize;
        }

        return layout.GetSize(room);
    }

    private void SyncNodeAssetSize(Vector2 size)
    {
        if (room.GraphNodeSize == size && room.GraphNodeSizeWasUserSpecified) return;
        room.GraphNodeSize = size;
        room.GraphNodeSizeWasUserSpecified = true;
        EditorUtility.SetDirty(room);
        SaveNodeAsset();
    }

    private static bool IsValidSize(Vector2 size) => size.x > 0 && size.y > 0;

    private void SaveNodeAsset()
    {
        AssetDatabase.SaveAssets();
    }

    private void ApplyStoredNodeLayout()
    {
        if (room == null || panel == null) return;

        var size = GetStoredNodeSize();
        base.SetPosition(new Rect(DialogueGraphLayout.instance.GetPosition(room), size));
        style.width = size.x;
        style.height = size.y;
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
        _inputParameterPorts.Clear();

        execInput = CreateExecPort(Direction.Input, room.execInput);
        inputContainer.Add(execInput);
        RebuildInputParameterPorts();
        execOutput = CreateExecPort(Direction.Output, room.execOutput);
        execOutput.visible = !room.hasSelection;
        outputContainer.Add(execOutput);

        if (!room.hasSelection)
        {
            RefreshPorts();
            return;
        }

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

    private void RebuildInputParameterPorts()
    {
        if (!_isSelectorOnly)
            AddInputParameterPorts(room.dialogueContent, null, null);

        if (!room.hasSelection) return;
        var options = room.selectionOptions ?? new List<DialogueSelectionOptionData>();
        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (option == null) continue;
            AddInputParameterPorts(option.dialogueContent, option.id, $"Option {i + 1}");
        }
    }

    private void AddInputParameterPorts(DialogueContentValue content, string optionId, string optionLabel)
    {
        var rootType = content?.ContentType;
        if (rootType == null) return;

        foreach (var binding in content.fieldBindings ?? new List<DialogueContentFieldBinding>())
        {
            if (binding == null || string.IsNullOrEmpty(binding.fieldPath)) continue;
            var portKey = GetInputParameterKey(optionId, binding.fieldPath);
            if (_inputParameterPorts.ContainsKey(portKey)) continue;
            var fieldType = GetFieldTypeAtPath(rootType, binding.fieldPath);
            if (fieldType == null) continue;

            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, fieldType);
            port.portName = string.IsNullOrEmpty(optionLabel)
                ? $"In: {binding.fieldPath}"
                : $"In: {optionLabel} / {binding.fieldPath}";
            port.userData = new DialogueContentInputPortData
            {
                optionId = optionId,
                fieldPath = binding.fieldPath
            };
            inputContainer.Add(port);
            _inputParameterPorts.Add(portKey, port);
        }
    }

    private static string GetInputParameterKey(string optionId, string fieldPath) =>
        $"{optionId ?? string.Empty}:{fieldPath}";

    private static Type GetFieldTypeAtPath(Type rootType, string path)
    {
        var type = rootType;
        foreach (var segment in path.Split('.'))
        {
            if (type.IsArray && int.TryParse(segment, out _))
            {
                type = type.GetElementType();
                continue;
            }
            if (type.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(type) &&
                int.TryParse(segment, out _))
            {
                type = type.GetGenericArguments()[0];
                continue;
            }

            var field = type.GetField(segment,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field == null) return null;
            type = field.FieldType;
        }

        return type;
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
        _selectionOptionsContainer.Add(CreateSectionHeader("Selection Options"));

        if (!_isSelectorOnly)
        {
            var selectionToggle = new Toggle("Enabled") { value = room.hasSelection };
            selectionToggle.RegisterValueChangedCallback(evt => SetSelectionEnabled(evt.newValue));
            _selectionOptionsContainer.Add(selectionToggle);
        }

        if (!room.hasSelection) return;

        var contentType = DialogueContentProjectSettings.Load()?.DefaultContentType;
        bool initializedContent = false;
        var options = room.selectionOptions ?? new List<DialogueSelectionOptionData>();
        if (options.Any(option => option != null && option.dialogueContent?.ContentType != contentType) &&
            contentType != null)
            Undo.RecordObject(room, "Initialize Selection Option Content");

        foreach (var option in options)
        {
            if (option == null) continue;
            if (string.IsNullOrEmpty(option.id))
                option.id = Guid.NewGuid().ToString("N");

            if (contentType != null && option.dialogueContent?.ContentType != contentType)
            {
                option.dialogueContent = new DialogueContentValue();
                option.dialogueContent.SetType(contentType);
                initializedContent = true;
            }
        }

        if (!options.Any(option => option != null && option.id == _selectedSelectionOptionId))
            _selectedSelectionOptionId = options.FirstOrDefault(option => option != null)?.id;

        var optionList = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexWrap = Wrap.Wrap
            }
        };
        for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
        {
            var option = options[optionIndex];
            if (option == null) continue;

            bool isSelected = option.id == _selectedSelectionOptionId;
            var optionButton = new Button(() => SelectSelectionOption(option.id))
            {
                text = string.IsNullOrWhiteSpace(option.text)
                    ? $"{optionIndex + 1}. Option"
                    : $"{optionIndex + 1}. {option.text}"
            };
            optionButton.style.height = 26;
            optionButton.style.marginBottom = 3;
            optionButton.style.marginRight = 4;
            optionButton.style.minWidth = 80;
            optionButton.style.flexGrow = 1;
            optionButton.style.flexShrink = 0;
            optionButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            optionButton.style.color = new Color(0.93f, 0.98f, 0.97f, 1f);
            optionButton.style.backgroundColor = isSelected
                ? new Color(0.12f, 0.38f, 0.39f, 1f)
                : new Color(0.1f, 0.15f, 0.17f, 1f);
            optionList.Add(optionButton);
        }

        _selectionOptionsContainer.Add(optionList);
        _selectionOptionsContainer.Add(new Button(AddSelectionOption) { text = "+ Add Option" });

        var selectedOption = options.FirstOrDefault(option =>
            option != null && option.id == _selectedSelectionOptionId);

        if (selectedOption != null)
        {
            int optionIndex = options.IndexOf(selectedOption);
            var contentBox = new VisualElement
            {
                style =
                {
                    marginTop = 6,
                    marginBottom = 6,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = new Color(0.1f, 0.15f, 0.17f, 1f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.29f, 0.45f, 0.47f, 1f),
                    borderBottomColor = new Color(0.29f, 0.45f, 0.47f, 1f),
                    borderLeftColor = new Color(0.29f, 0.45f, 0.47f, 1f),
                    borderRightColor = new Color(0.29f, 0.45f, 0.47f, 1f)
                }
            };

            var optionNameField = new TextField("Name")
            {
                value = selectedOption.text,
                isDelayed = true
            };
            optionNameField.RegisterValueChangedCallback(evt =>
            {
                if (selectedOption.text == evt.newValue) return;
                Undo.RecordObject(room, "Rename Dialogue Selection Option");
                selectedOption.text = evt.newValue;
                var outputPort = GetOutputPort(selectedOption.id);
                if (outputPort != null) outputPort.portName = selectedOption.text;
                RefreshPorts();
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssets();
                RefreshSelectionOptions();
            });
            contentBox.Add(optionNameField);

            var orderControls = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var moveUpButton = new Button(() => MoveSelectionOption(selectedOption, -1))
            {
                text = "Move Up"
            };
            moveUpButton.SetEnabled(optionIndex > 0);
            moveUpButton.style.flexGrow = 1;
            var moveDownButton = new Button(() => MoveSelectionOption(selectedOption, 1))
            {
                text = "Move Down"
            };
            moveDownButton.SetEnabled(optionIndex < options.Count - 1);
            moveDownButton.style.flexGrow = 1;
            orderControls.Add(moveUpButton);
            orderControls.Add(moveDownButton);
            contentBox.Add(orderControls);

            var serializedRoom = new SerializedObject(room);
            serializedRoom.Update();
            var optionsProperty = serializedRoom.FindProperty("selectionOptions");
            var contentProperty = optionsProperty?.GetArrayElementAtIndex(optionIndex)
                .FindPropertyRelative("dialogueContent");
            if (contentProperty != null)
            {
                var contentField = new PropertyField(contentProperty, "Content");
                contentField.Bind(serializedRoom);
                contentField.RegisterCallback<SerializedPropertyChangeEvent>(_ => EditorUtility.SetDirty(room));
                contentBox.Add(contentField);
            }

            contentBox.Add(new Button(() => RemoveSelectionOption(selectedOption)) { text = "Remove Option" });
            _selectionOptionsContainer.Add(contentBox);
        }

        if (initializedContent)
        {
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
        }
    }

    private void SelectSelectionOption(string optionId)
    {
        if (_selectedSelectionOptionId == optionId) return;
        _selectedSelectionOptionId = optionId;
        RefreshSelectionOptions();
    }

    public string SelectedSelectionOptionId => _selectedSelectionOptionId;

    public void RestoreSelectedSelectionOption(string optionId)
    {
        if (string.IsNullOrEmpty(optionId)) return;
        _selectedSelectionOptionId = optionId;
        RefreshSelectionOptions();
    }

    private void MoveSelectionOption(DialogueSelectionOptionData option, int offset)
    {
        var options = room.selectionOptions;
        int currentIndex = options?.IndexOf(option) ?? -1;
        int targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= options.Count) return;

        Undo.RecordObject(room, "Reorder Dialogue Selection Options");
        options.RemoveAt(currentIndex);
        options.Insert(targetIndex, option);
        _selectedSelectionOptionId = option.id;
        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        RebuildExecPorts();
        _graphChanged?.Invoke();
    }

    private void AddSelectionOption()
    {
        Undo.RecordObject(room, "Add Dialogue Selection Option");
        room.selectionOptions ??= new List<DialogueSelectionOptionData>();
        var option = new DialogueSelectionOptionData();
        room.selectionOptions.Add(option);
        _selectedSelectionOptionId = option.id;

        var outputPort = CreateExecPort(Direction.Output, new DialogueExecPortData
        {
            id = option.id,
            label = option.text
        });
        outputContainer.Add(outputPort);
        _outputPortIds[outputPort] = option.id;
        RefreshPorts();

        EditorUtility.SetDirty(room);
        AssetDatabase.SaveAssets();
        RefreshSelectionOptions();
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
        _selectedSelectionOptionId = room.selectionOptions
            .FirstOrDefault(item => item != null)?.id;
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
