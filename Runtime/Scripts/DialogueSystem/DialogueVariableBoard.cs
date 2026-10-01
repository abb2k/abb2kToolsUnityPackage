using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DialogueVariableBoard : ScrollView
{
    private static Type[] _cachedCustomTypes;
    private static Type[] _cachedEnumTypes;
    private static int _cachedAssemblyCount = -1;
    private readonly Dialogue _dialogue;

    public DialogueVariableBoard(Dialogue dialogue)
    {
        _dialogue = dialogue;
        style.flexDirection = FlexDirection.Column;
        Refresh();
    }

    private void Refresh()
    {
        Clear();

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        header.Add(new Label("Exposed Variables") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
        header.Add(new Button(AddVariable) { text = "+" });
        Add(header);

        _dialogue.variables ??= new System.Collections.Generic.List<DialogueExposedVariable>();
        foreach (var variable in _dialogue.variables.ToArray())
        {
            if (variable != null)
                Add(CreateVariableRow(variable));
        }
    }

    private VisualElement CreateVariableRow(DialogueExposedVariable variable)
    {
        var container = new VisualElement();
        container.style.paddingTop = 6;
        container.style.paddingBottom = 6;
        container.style.paddingLeft = 6;
        container.style.paddingRight = 6;
        container.style.marginTop = 4;
        container.style.borderBottomWidth = 1;
        container.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);

        var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        var nameField = new TextField { value = variable.name, isDelayed = true };
        nameField.style.flexGrow = 1;
        nameField.RegisterValueChangedCallback(evt =>
        {
            var value = evt.newValue?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                nameField.SetValueWithoutNotify(variable.name);
                return;
            }
            if (_dialogue.variables.Any(item => item != variable && item != null && item.name == value))
            {
                nameField.SetValueWithoutNotify(variable.name);
                return;
            }
            Modify("Rename Dialogue Variable", () => variable.name = value);
        });
        nameRow.Add(nameField);
        nameRow.Add(new Button(() => RemoveVariable(variable)) { text = "-" });
        container.Add(nameRow);

        var typeField = new EnumField("Type", variable.type);
        typeField.RegisterValueChangedCallback(evt =>
        {
            Modify("Change Dialogue Variable Type", () => variable.type = (DialogueVariableType)evt.newValue);
            Refresh();
        });
        container.Add(typeField);

        var valueField = CreateValueField(variable);
        if (valueField != null)
            container.Add(valueField);

        return container;
    }

    private VisualElement CreateValueField(DialogueExposedVariable variable)
    {
        switch (variable.type)
        {
            case DialogueVariableType.Boolean:
            {
                var field = new Toggle("Value") { value = variable.boolValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.boolValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Byte:
            case DialogueVariableType.SByte:
            case DialogueVariableType.Short:
            case DialogueVariableType.UShort:
            case DialogueVariableType.Integer:
            {
                var field = new IntegerField("Value") { value = GetSignedInteger(variable) };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => SetSignedInteger(variable, evt.newValue)));
                return field;
            }
            case DialogueVariableType.UInteger:
            {
                var field = new TextField("Value") { value = variable.uintValue.ToString(CultureInfo.InvariantCulture), isDelayed = true };
                field.RegisterValueChangedCallback(evt =>
                {
                    if (uint.TryParse(evt.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        Modify("Set Dialogue Variable", () => variable.uintValue = value);
                });
                return field;
            }
            case DialogueVariableType.Long:
            {
                var field = new LongField("Value") { value = variable.longValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.longValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.ULong:
            {
                var field = new TextField("Value") { value = variable.ulongValue.ToString(CultureInfo.InvariantCulture), isDelayed = true };
                field.RegisterValueChangedCallback(evt =>
                {
                    if (ulong.TryParse(evt.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        Modify("Set Dialogue Variable", () => variable.ulongValue = value);
                });
                return field;
            }
            case DialogueVariableType.Float:
            {
                var field = new FloatField("Value") { value = variable.floatValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.floatValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Double:
            {
                var field = new DoubleField("Value") { value = variable.doubleValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.doubleValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Decimal:
            {
                var field = new TextField("Value") { value = variable.decimalValue, isDelayed = true };
                field.RegisterValueChangedCallback(evt =>
                {
                    if (decimal.TryParse(evt.newValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                        Modify("Set Dialogue Variable", () => variable.decimalValue = value.ToString(CultureInfo.InvariantCulture));
                });
                return field;
            }
            case DialogueVariableType.Character:
            {
                var field = new TextField("Value") { value = variable.charValue, isDelayed = true };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.charValue = string.IsNullOrEmpty(evt.newValue) ? string.Empty : evt.newValue.Substring(0, 1)));
                return field;
            }
            case DialogueVariableType.String:
            {
                var field = new TextField("Value") { value = variable.stringValue, isDelayed = true };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.stringValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Vector2:
            {
                var field = new Vector2Field("Value") { value = variable.vector2Value };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.vector2Value = evt.newValue));
                return field;
            }
            case DialogueVariableType.Vector2Int:
            {
                var field = new Vector2IntField("Value") { value = variable.vector2IntValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.vector2IntValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Vector3:
            {
                var field = new Vector3Field("Value") { value = variable.vector3Value };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.vector3Value = evt.newValue));
                return field;
            }
            case DialogueVariableType.Vector3Int:
            {
                var field = new Vector3IntField("Value") { value = variable.vector3IntValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.vector3IntValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Vector4:
            {
                var field = new Vector4Field("Value") { value = variable.vector4Value };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.vector4Value = evt.newValue));
                return field;
            }
            case DialogueVariableType.Quaternion:
            {
                var field = new Vector4Field("Value (XYZW)") { value = new Vector4(variable.quaternionValue.x, variable.quaternionValue.y, variable.quaternionValue.z, variable.quaternionValue.w) };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.quaternionValue = new Quaternion(evt.newValue.x, evt.newValue.y, evt.newValue.z, evt.newValue.w)));
                return field;
            }
            case DialogueVariableType.Color:
            {
                var field = new ColorField("Value") { value = variable.colorValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.colorValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Color32:
            {
                var field = new ColorField("Value") { value = variable.color32Value };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.color32Value = evt.newValue));
                return field;
            }
            case DialogueVariableType.Rect:
            {
                var field = new RectField("Value") { value = variable.rectValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.rectValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.RectInt:
            {
                var field = new RectIntField("Value") { value = variable.rectIntValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.rectIntValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Bounds:
            {
                var field = new BoundsField("Value") { value = variable.boundsValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.boundsValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.BoundsInt:
            {
                var field = new BoundsIntField("Value") { value = variable.boundsIntValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.boundsIntValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Matrix4x4:
                return CreateMatrixField(variable);
            case DialogueVariableType.LayerMask:
            {
                var field = new LayerMaskField("Value") { value = variable.layerMaskValue.value };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.layerMaskValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.AnimationCurve:
            {
                var field = new CurveField("Value") { value = variable.animationCurveValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.animationCurveValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Gradient:
            {
                var field = new GradientField("Value") { value = variable.gradientValue };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.gradientValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Enum:
                return CreateEnumField(variable);
            case DialogueVariableType.ObjectReference:
            {
                var field = new ObjectField("Value")
                {
                    objectType = typeof(UnityEngine.Object),
                    value = variable.objectReferenceValue,
                    allowSceneObjects = true
                };
                field.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.objectReferenceValue = evt.newValue));
                return field;
            }
            case DialogueVariableType.Custom:
                return CreateCustomField(variable);
            default:
                return null;
        }
    }

    private VisualElement CreateEnumField(DialogueExposedVariable variable)
    {
        var container = new VisualElement();
        container.Add(CreateTypePicker(variable, enumsOnly: true));
        var enumType = variable.GetValueType();
        if (enumType == null || !enumType.IsEnum) return container;

        var enumField = new EnumField("Value", (Enum)Enum.ToObject(enumType, variable.enumValue));
        enumField.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.enumValue = Convert.ToInt32(evt.newValue, CultureInfo.InvariantCulture)));
        container.Add(enumField);
        return container;
    }

    private VisualElement CreateCustomField(DialogueExposedVariable variable)
    {
        var container = new VisualElement();
        container.Add(CreateTypePicker(variable, enumsOnly: false));
        var type = variable.GetValueType();
        if (type == null) return container;

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            var objectField = new ObjectField("Reference")
            {
                objectType = type,
                value = variable.objectReferenceValue,
                allowSceneObjects = true
            };
            objectField.RegisterValueChangedCallback(evt => Modify("Set Dialogue Variable", () => variable.objectReferenceValue = evt.newValue));
            container.Add(objectField);
            return container;
        }

        if (!type.IsValueType)
        {
            if (variable.customManagedValue == null || variable.customManagedValue.GetType() != type)
            {
                try
                {
                    Modify("Initialize Dialogue Variable", () => variable.customManagedValue = Activator.CreateInstance(type, nonPublic: true));
                }
                catch
                {
                    container.Add(new HelpBox($"{type.Name} needs a parameterless constructor to edit its fields.", HelpBoxMessageType.Warning));
                    return container;
                }
            }

            int index = _dialogue.variables.IndexOf(variable);
            var serializedDialogue = new SerializedObject(_dialogue);
            var property = serializedDialogue.FindProperty($"variables.Array.data[{index}].customManagedValue");
            if (property != null)
            {
                var propertyField = new PropertyField(property, "Value");
                propertyField.Bind(serializedDialogue);
                container.Add(propertyField);
            }
            return container;
        }

        var customValue = variable.CreateValue() ?? Activator.CreateInstance(type);
        var foldout = new Foldout { text = $"Value ({type.Name})", value = true };
        AddSerializableFields(foldout, variable, type, customValue, Array.Empty<FieldInfo>(), 0);
        container.Add(foldout);
        return container;
    }

    private void AddSerializableFields(VisualElement parent, DialogueExposedVariable variable, Type valueType, object value, FieldInfo[] path, int depth)
    {
        if (depth >= 6 || value == null) return;
        foreach (var field in GetSerializableFields(valueType))
        {
            var fieldPath = path.Concat(new[] { field }).ToArray();
            var fieldValue = field.GetValue(value);
            var fieldEditor = CreateStructField(variable, valueType, fieldPath, field, fieldValue, depth);
            if (fieldEditor != null)
                parent.Add(fieldEditor);
        }
    }

    private VisualElement CreateStructField(
        DialogueExposedVariable variable,
        Type rootType,
        FieldInfo[] path,
        FieldInfo field,
        object value,
        int depth)
    {
        var type = field.FieldType;
        string pathKey = string.Join(".", path.Select(item => item.Name));
        if (type == typeof(bool))
        {
            var editor = new Toggle(field.Name) { value = value != null && (bool)value };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }
        if (type == typeof(int) || type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
        {
            var editor = new IntegerField(field.Name) { value = value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture) };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, Convert.ChangeType(evt.newValue, type, CultureInfo.InvariantCulture)));
            return editor;
        }
        if (type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        {
            var editor = new TextField(field.Name) { value = value?.ToString(), isDelayed = true };
            editor.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    var parsed = Convert.ChangeType(evt.newValue, type, CultureInfo.InvariantCulture);
                    SetCustomStructField(variable, rootType, path, pathKey, parsed);
                }
                catch { }
            });
            return editor;
        }
        if (type == typeof(float))
        {
            var editor = new FloatField(field.Name) { value = value == null ? 0f : (float)value };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }
        if (type == typeof(double))
        {
            var editor = new DoubleField(field.Name) { value = value == null ? 0d : (double)value };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }
        if (type == typeof(string))
        {
            var editor = new TextField(field.Name) { value = value as string, isDelayed = true };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }
        if (type == typeof(char))
        {
            var editor = new TextField(field.Name) { value = value?.ToString(), isDelayed = true };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, string.IsNullOrEmpty(evt.newValue) ? '\0' : evt.newValue[0]));
            return editor;
        }
        if (type.IsEnum)
        {
            var editor = new EnumField(field.Name, value as Enum ?? (Enum)Enum.ToObject(type, 0));
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            var editor = new ObjectField(field.Name) { objectType = type, value = value as UnityEngine.Object, allowSceneObjects = true };
            editor.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
            return editor;
        }

        VisualElement valueEditor = null;
        if (type == typeof(Vector2)) valueEditor = new Vector2Field(field.Name) { value = value == null ? default : (Vector2)value };
        else if (type == typeof(Vector2Int)) valueEditor = new Vector2IntField(field.Name) { value = value == null ? default : (Vector2Int)value };
        else if (type == typeof(Vector3)) valueEditor = new Vector3Field(field.Name) { value = value == null ? default : (Vector3)value };
        else if (type == typeof(Vector3Int)) valueEditor = new Vector3IntField(field.Name) { value = value == null ? default : (Vector3Int)value };
        else if (type == typeof(Vector4)) valueEditor = new Vector4Field(field.Name) { value = value == null ? default : (Vector4)value };
        else if (type == typeof(Color)) valueEditor = new ColorField(field.Name) { value = value == null ? default : (Color)value };
        else if (type == typeof(Rect)) valueEditor = new RectField(field.Name) { value = value == null ? default : (Rect)value };
        else if (type == typeof(Bounds)) valueEditor = new BoundsField(field.Name) { value = value == null ? default : (Bounds)value };
        else if (type == typeof(AnimationCurve)) valueEditor = new CurveField(field.Name) { value = value as AnimationCurve };
        else if (type == typeof(Gradient)) valueEditor = new GradientField(field.Name) { value = value as Gradient };
        if (valueEditor != null)
        {
            RegisterStructFieldCallback(valueEditor, variable, rootType, path, pathKey);
            return valueEditor;
        }

        if (typeof(IList).IsAssignableFrom(type) || type.IsArray)
            return CreateStructCollectionField(variable, rootType, path, pathKey, type, value);

        if (depth >= 5 || !type.IsSerializable)
            return new Label($"{field.Name}: {type.Name}");

        object nestedValue = value;
        if (nestedValue == null && type.IsValueType)
            nestedValue = Activator.CreateInstance(type);
        var foldout = new Foldout { text = field.Name, value = false };
        if (nestedValue != null)
            AddSerializableFields(foldout, variable, rootType, nestedValue, path, depth + 1);
        return foldout;
    }

    private VisualElement CreateStructCollectionField(
        DialogueExposedVariable variable,
        Type rootType,
        FieldInfo[] path,
        string pathKey,
        Type collectionType,
        object value)
    {
        var elementType = collectionType.IsArray
            ? collectionType.GetElementType()
            : collectionType.IsGenericType ? collectionType.GetGenericArguments()[0] : typeof(object);
        var collection = value as IList;
        var foldout = new Foldout { text = $"{path[path.Length - 1].Name} ({collection?.Count ?? 0})", value = false };
        var addButton = new Button(() => UpdateCustomCollection(variable, rootType, path, pathKey, collectionType, list =>
        {
            object newValue = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
            if (elementType != typeof(string) && elementType.IsClass && !typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                try { newValue = Activator.CreateInstance(elementType, nonPublic: true); }
                catch { }
            }
            list.Add(newValue);
        })) { text = "+ Add" };
        foldout.Add(addButton);

        if (collection == null) return foldout;
        for (int index = 0; index < collection.Count; index++)
        {
            int itemIndex = index;
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var itemField = CreateCollectionItemField(variable, rootType, path, pathKey, collectionType, elementType, itemIndex, collection[index]);
            itemField.style.flexGrow = 1;
            row.Add(itemField);
            row.Add(new Button(() => UpdateCustomCollection(variable, rootType, path, pathKey, collectionType, list => list.RemoveAt(itemIndex))) { text = "-" });
            foldout.Add(row);
        }
        return foldout;
    }

    private VisualElement CreateCollectionItemField(
        DialogueExposedVariable variable,
        Type rootType,
        FieldInfo[] path,
        string pathKey,
        Type collectionType,
        Type elementType,
        int index,
        object value)
    {
        string itemPath = $"{pathKey}.{index}";
        void Set(object newValue) => UpdateCustomCollection(variable, rootType, path, pathKey, collectionType, list => list[index] = newValue, itemPath, newValue, refreshAfter: false);

        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            var field = new ObjectField($"Element {index}") { objectType = elementType, value = value as UnityEngine.Object, allowSceneObjects = true };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType.IsEnum)
        {
            var field = new EnumField($"Element {index}", value as Enum ?? (Enum)Enum.ToObject(elementType, 0));
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(string))
        {
            var field = new TextField($"Element {index}") { value = value as string, isDelayed = true };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(bool))
        {
            var field = new Toggle($"Element {index}") { value = value != null && (bool)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(int))
        {
            var field = new IntegerField($"Element {index}") { value = value == null ? 0 : (int)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(float))
        {
            var field = new FloatField($"Element {index}") { value = value == null ? 0f : (float)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(double))
        {
            var field = new DoubleField($"Element {index}") { value = value == null ? 0d : (double)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(Vector2))
        {
            var field = new Vector2Field($"Element {index}") { value = value == null ? default : (Vector2)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }
        if (elementType == typeof(Vector3))
        {
            var field = new Vector3Field($"Element {index}") { value = value == null ? default : (Vector3)value };
            field.RegisterValueChangedCallback(evt => Set(evt.newValue));
            return field;
        }

        return new Label($"Element {index}: {value ?? $"({elementType.Name})"}");
    }

    private void UpdateCustomCollection(
        DialogueExposedVariable variable,
        Type rootType,
        FieldInfo[] path,
        string pathKey,
        Type collectionType,
        Action<IList> update,
        string referencePath = null,
        object referenceValue = null,
        bool refreshAfter = true)
    {
        Modify("Set Dialogue Variable", () =>
        {
            var root = variable.CreateValue() ?? Activator.CreateInstance(rootType);
            object collectionValue = GetFieldValue(root, path, 0);
            var elementType = collectionType.IsArray
                ? collectionType.GetElementType()
                : collectionType.GetGenericArguments()[0];
            var list = new List<object>();
            if (collectionValue is IList existing)
                foreach (var item in existing) list.Add(item);
            update(list);

            object updatedCollection;
            if (collectionType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                for (int index = 0; index < list.Count; index++) array.SetValue(list[index], index);
                updatedCollection = array;
            }
            else
            {
                var concreteList = collectionValue as IList;
                if (concreteList == null)
                {
                    concreteList = (IList)Activator.CreateInstance(collectionType);
                    SetFieldValue(root, path, 0, concreteList);
                }
                else
                {
                    concreteList.Clear();
                    foreach (var item in list) concreteList.Add(item);
                }
                updatedCollection = concreteList;
            }

            if (collectionType.IsArray)
                SetFieldValue(root, path, 0, updatedCollection);
            variable.customJson = JsonUtility.ToJson(root, true);
            variable.customObjectReferences ??= new List<DialogueCustomObjectReference>();
            variable.customObjectReferences.RemoveAll(reference => reference != null &&
                (reference.fieldPath == referencePath || reference.fieldPath.StartsWith(pathKey + ".", StringComparison.Ordinal)));
            if (collectionType.IsArray || typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                var savedList = updatedCollection as IList;
                for (int index = 0; index < savedList.Count; index++)
                    if (savedList[index] is UnityEngine.Object objectReference)
                        variable.customObjectReferences.Add(new DialogueCustomObjectReference
                        {
                            fieldPath = $"{pathKey}.{index}",
                            value = objectReference
                        });
            }
        });
        if (refreshAfter)
            Refresh();
    }

    private static object GetFieldValue(object target, FieldInfo[] path, int index)
    {
        if (target == null || index >= path.Length) return target;
        return GetFieldValue(path[index].GetValue(target), path, index + 1);
    }


    private void RegisterStructFieldCallback(VisualElement editor, DialogueExposedVariable variable, Type rootType, FieldInfo[] path, string pathKey)
    {
        if (editor is Vector2Field vector2) vector2.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is Vector2IntField vector2Int) vector2Int.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is Vector3Field vector3) vector3.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is Vector3IntField vector3Int) vector3Int.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is Vector4Field vector4) vector4.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is ColorField color) color.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is RectField rect) rect.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is BoundsField bounds) bounds.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is CurveField curve) curve.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
        else if (editor is GradientField gradient) gradient.RegisterValueChangedCallback(evt => SetCustomStructField(variable, rootType, path, pathKey, evt.newValue));
    }

    private void SetCustomStructField(DialogueExposedVariable variable, Type rootType, FieldInfo[] path, string pathKey, object fieldValue)
    {
        Modify("Set Dialogue Variable", () =>
        {
            var root = variable.CreateValue() ?? Activator.CreateInstance(rootType);
            SetFieldValue(root, path, 0, fieldValue);
            variable.customJson = JsonUtility.ToJson(root, true);
            variable.customObjectReferences ??= new List<DialogueCustomObjectReference>();
            variable.customObjectReferences.RemoveAll(reference => reference != null && reference.fieldPath == pathKey);
            if (fieldValue is UnityEngine.Object objectReference)
                variable.customObjectReferences.Add(new DialogueCustomObjectReference { fieldPath = pathKey, value = objectReference });
        });
    }

    private static object SetFieldValue(object target, FieldInfo[] path, int index, object value)
    {
        if (target == null || index >= path.Length) return target;
        var field = path[index];
        if (index == path.Length - 1)
        {
            field.SetValue(target, value);
            return target;
        }

        var nestedValue = field.GetValue(target);
        nestedValue = SetFieldValue(nestedValue, path, index + 1, value);
        field.SetValue(target, nestedValue);
        return target;
    }

    private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
    {
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsStatic && !field.IsInitOnly &&
                !field.IsDefined(typeof(NonSerializedAttribute), inherit: true) &&
                (field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true)));
    }

    private VisualElement CreateTypePicker(DialogueExposedVariable variable, bool enumsOnly)
    {
        var types = GetSerializableTypes(enumsOnly);
        var selectedType = variable.GetValueType();
        string selectedName = selectedType != null && types.Contains(selectedType)
            ? GetTypeDisplayName(selectedType)
            : enumsOnly ? "Select enum type..." : "Select custom type...";

        var button = new Button { text = enumsOnly ? $"Enum: {selectedName}" : $"Type: {selectedName}" };
        button.clicked += () => UnityEditor.PopupWindow.Show(button.worldBound, new DialogueTypePickerPopup(types, type =>
        {
            Modify("Change Dialogue Variable Type", () =>
            {
                variable.customTypeName = type.AssemblyQualifiedName;
                variable.enumValue = 0;
                if (!enumsOnly && !typeof(UnityEngine.Object).IsAssignableFrom(type))
                    variable.customJson = CreateDefaultJson(type);
                else if (typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                    variable.objectReferenceValue != null && !type.IsInstanceOfType(variable.objectReferenceValue))
                    variable.objectReferenceValue = null;
            });
            Refresh();
        }));
        return button;
    }

    private static string GetTypeDisplayName(Type type)
    {
        var namespaceName = string.IsNullOrEmpty(type.Namespace) ? "(Global)" : type.Namespace;
        return $"{namespaceName}.{type.Name}";
    }

    private sealed class DialogueTypePickerPopup : PopupWindowContent
    {
        private readonly TypeSearchEntry[] _types;
        private readonly Action<Type> _selectType;
        private readonly Dictionary<string, bool> _expandedNamespaces = new();
        private string _search = string.Empty;
        private Vector2 _scrollPosition;
        private NamespaceNode _visibleNamespaceRoot;

        public DialogueTypePickerPopup(Type[] types, Action<Type> selectType)
        {
            _types = types.Select(type => new TypeSearchEntry(type)).ToArray();
            _selectType = selectType;
            RefreshFilteredTypes();
        }

        public override Vector2 GetWindowSize() => new(380, 440);

        public override void OnGUI(Rect rect)
        {
            var search = EditorGUILayout.TextField("Search", _search);
            if (!string.Equals(search, _search, StringComparison.Ordinal))
            {
                _search = search;
                RefreshFilteredTypes();
            }
            EditorGUILayout.Space(3);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var namespaceNode in _visibleNamespaceRoot.Children.Values)
                DrawNamespaceNode(namespaceNode);

            EditorGUILayout.EndScrollView();
        }

        private void RefreshFilteredTypes()
        {
            var matchingTypes = string.IsNullOrWhiteSpace(_search)
                ? _types.Select(entry => entry.Type).ToArray()
                : _types.Where(entry => entry.SearchText.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(entry => entry.Type)
                    .ToArray();
            _visibleNamespaceRoot = BuildNamespaceTree(matchingTypes);
        }

        private void DrawNamespaceNode(NamespaceNode node)
        {
            bool searching = !string.IsNullOrWhiteSpace(_search);
            bool expanded = searching ||
                (_expandedNamespaces.TryGetValue(node.Path, out var savedExpanded) && savedExpanded);
            expanded = EditorGUILayout.Foldout(expanded, node.Name, true);
            if (!searching)
                _expandedNamespaces[node.Path] = expanded;
            if (!expanded) return;

            EditorGUI.indentLevel++;
            foreach (var type in node.Types)
            {
                if (GUILayout.Button(type.Name, EditorStyles.miniButtonLeft))
                {
                    _selectType?.Invoke(type);
                    editorWindow.Close();
                    break;
                }
            }

            foreach (var child in node.Children.Values)
                DrawNamespaceNode(child);
            EditorGUI.indentLevel--;
        }

        private static NamespaceNode BuildNamespaceTree(IEnumerable<Type> types)
        {
            var root = new NamespaceNode(string.Empty, string.Empty);
            foreach (var type in types)
            {
                var parts = string.IsNullOrEmpty(type.Namespace)
                    ? new[] { "(Global Namespace)" }
                    : type.Namespace.Split('.');
                var parent = root;
                string path = string.Empty;
                foreach (var part in parts)
                {
                    path = string.IsNullOrEmpty(path) ? part : $"{path}.{part}";
                    if (!parent.Children.TryGetValue(part, out var child))
                    {
                        child = new NamespaceNode(part, path);
                        parent.Children.Add(part, child);
                    }
                    parent = child;
                }
                parent.Types.Add(type);
            }

            SortNamespaceTree(root);
            return root;
        }

        private static void SortNamespaceTree(NamespaceNode node)
        {
            node.Types.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            foreach (var child in node.Children.Values)
                SortNamespaceTree(child);
        }

        private sealed class NamespaceNode
        {
            public readonly string Name;
            public readonly string Path;
            public readonly SortedDictionary<string, NamespaceNode> Children = new();
            public readonly List<Type> Types = new();

            public NamespaceNode(string name, string path)
            {
                Name = name;
                Path = path;
            }
        }

        private sealed class TypeSearchEntry
        {
            public readonly Type Type;
            public readonly string SearchText;

            public TypeSearchEntry(Type type)
            {
                Type = type;
                SearchText = $"{type.Namespace} {type.Name} {type.Assembly.GetName().Name}";
            }
        }
    }

    private static Type[] GetSerializableTypes(bool enumsOnly)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        if (_cachedAssemblyCount != assemblies.Length)
        {
            _cachedAssemblyCount = assemblies.Length;
            _cachedCustomTypes = null;
            _cachedEnumTypes = null;
        }

        var cachedTypes = enumsOnly ? _cachedEnumTypes : _cachedCustomTypes;
        if (cachedTypes != null) return cachedTypes;

        IEnumerable<Type> discoveredTypes = enumsOnly
            ? TypeCache.GetTypesDerivedFrom<Enum>()
            : TypeCache.GetTypesWithAttribute<SerializableAttribute>()
                .Concat(TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
                .Concat(AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetSerializableStructs));

        cachedTypes = discoveredTypes
            .Where(type => type != null &&
                !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: true) &&
                !(type.Name.StartsWith("<", StringComparison.Ordinal) || (type.FullName ?? string.Empty).Contains("<")))
            .Where(type => enumsOnly
                ? type.IsEnum
                : type != typeof(string) && !type.IsEnum && !type.IsArray &&
                                    (type.IsClass || type.IsValueType) &&
                                    (!type.IsAbstract || typeof(UnityEngine.Object).IsAssignableFrom(type)) &&
                                    !type.ContainsGenericParameters &&
                                    (typeof(UnityEngine.Object).IsAssignableFrom(type)
                                            ? type.Assembly != typeof(UnityEngine.Object).Assembly
                                            : type.IsDefined(typeof(SerializableAttribute), inherit: false)))
            .OrderBy(type => type.FullName)
            .ToArray();

        if (enumsOnly)
            _cachedEnumTypes = cachedTypes;
        else
            _cachedCustomTypes = cachedTypes;
        return cachedTypes;
    }

    private static IEnumerable<Type> GetSerializableStructs(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.Where(type => type != null).ToArray();
        }

        return types.Where(type => type.IsValueType && !type.IsEnum &&
            type.IsDefined(typeof(SerializableAttribute), inherit: false));
    }

    private VisualElement CreateMatrixField(DialogueExposedVariable variable)
    {
        var matrix = variable.matrix4x4Value;
        var values = new string[16];
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                values[row * 4 + column] = matrix[row, column].ToString("R", CultureInfo.InvariantCulture);

        var field = new TextField("Value (16 comma-separated numbers)")
        {
            value = string.Join(", ", values),
            isDelayed = true
        };
        field.RegisterValueChangedCallback(evt =>
        {
            var parts = evt.newValue.Split(',');
            if (parts.Length != 16) return;
            var parsed = new float[16];
            for (int i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i])) return;

            Modify("Set Dialogue Variable", () =>
            {
                var updated = new Matrix4x4();
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        updated[row, column] = parsed[row * 4 + column];
                variable.matrix4x4Value = updated;
            });
        });
        return field;
    }

    private static string CreateDefaultJson(Type type)
    {
        try
        {
            var instance = type.IsValueType ? Activator.CreateInstance(type) : Activator.CreateInstance(type, nonPublic: true);
            return JsonUtility.ToJson(instance, true);
        }
        catch
        {
            return "{}";
        }
    }

    private static int GetSignedInteger(DialogueExposedVariable variable)
    {
        switch (variable.type)
        {
            case DialogueVariableType.Byte: return variable.byteValue;
            case DialogueVariableType.SByte: return variable.sbyteValue;
            case DialogueVariableType.Short: return variable.shortValue;
            case DialogueVariableType.UShort: return variable.ushortValue;
            default: return variable.intValue;
        }
    }

    private static void SetSignedInteger(DialogueExposedVariable variable, int value)
    {
        switch (variable.type)
        {
            case DialogueVariableType.Byte: variable.byteValue = (byte)Mathf.Clamp(value, byte.MinValue, byte.MaxValue); break;
            case DialogueVariableType.SByte: variable.sbyteValue = (sbyte)Mathf.Clamp(value, sbyte.MinValue, sbyte.MaxValue); break;
            case DialogueVariableType.Short: variable.shortValue = (short)Mathf.Clamp(value, short.MinValue, short.MaxValue); break;
            case DialogueVariableType.UShort: variable.ushortValue = (ushort)Mathf.Clamp(value, ushort.MinValue, ushort.MaxValue); break;
            default: variable.intValue = value; break;
        }
    }

    private void AddVariable()
    {
        int suffix = _dialogue.variables.Count + 1;
        string name = "New Variable";
        while (_dialogue.variables.Any(variable => variable != null && variable.name == name))
            name = $"New Variable {suffix++}";
        Modify("Add Dialogue Variable", () => _dialogue.variables.Add(new DialogueExposedVariable { name = name }));
        Refresh();
    }

    private void RemoveVariable(DialogueExposedVariable variable)
    {
        Modify("Remove Dialogue Variable", () => _dialogue.variables.Remove(variable));
        Refresh();
    }

    private void Modify(string undoName, Action change)
    {
        Undo.RecordObject(_dialogue, undoName);
        change();
        EditorUtility.SetDirty(_dialogue);
        AssetDatabase.SaveAssets();
    }
}