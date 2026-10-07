#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueLink))]
public sealed class DialogueLinkEditor : Editor
{
    private static readonly Dictionary<int, Dialogue> DialogueByRoomCache = new();

    private sealed class PresetFieldContext
    {
        public bool presetOnly;
        public object rootValue;
        public object presetValue;
        public SerializedProperty localFieldOverrides;
        public SerializedProperty serializedValue;
        public SerializedProperty fieldBindings;
        public Action inputPortsChanged;
    }

    [Serializable]
    private sealed class ClipboardCollection
    {
        public List<string> values = new();
    }

    internal static Dialogue FindDialogueForTarget(UnityEngine.Object target)
    {
        if (target is Dialogue dialogue) return dialogue;
        if (target is not DialogueData room) return null;

        int instanceId = room.GetInstanceID();
        if (DialogueByRoomCache.TryGetValue(instanceId, out var cachedDialogue))
        {
            if (cachedDialogue != null && cachedDialogue.rooms != null && cachedDialogue.rooms.Contains(room))
                return cachedDialogue;
            DialogueByRoomCache.Remove(instanceId);
        }

        string assetPath = AssetDatabase.GetAssetPath(room);
        if (!string.IsNullOrEmpty(assetPath))
        {
            var sameAssetDialogue = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Dialogue>()
                .FirstOrDefault(candidate => candidate.rooms != null && candidate.rooms.Contains(room));
            if (sameAssetDialogue != null)
            {
                DialogueByRoomCache[instanceId] = sameAssetDialogue;
                return sameAssetDialogue;
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Dialogue"))
        {
            string dialoguePath = AssetDatabase.GUIDToAssetPath(guid);
            var candidate = AssetDatabase.LoadAssetAtPath<Dialogue>(dialoguePath);
            if (candidate?.rooms == null || !candidate.rooms.Contains(room)) continue;
            DialogueByRoomCache[instanceId] = candidate;
            return candidate;
        }

        return null;
    }

    public override void OnInspectorGUI()
    {
        MigrateLegacyConditions();
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "conditions", "source", "sourcePortId",
            "destination", "destinationPortId", "sourceIsDialogueStart", "destinationIsDialogueExit",
            "sourceEntryId", "destinationExitId", "curvePoints");
        DrawConditions();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConditions()
    {
        var link = (DialogueLink)target;
        var dialogue = FindDialogue(link);
        var conditions = serializedObject.FindProperty("conditions");

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
            GUILayout.Label("ALL must pass", EditorStyles.label);
            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                int index = conditions.arraySize++;
                var condition = conditions.GetArrayElementAtIndex(index);
                condition.FindPropertyRelative("variableId").stringValue = dialogue?.variables?.FirstOrDefault(item => item != null)?.id;
                condition.FindPropertyRelative("valueSource").enumValueIndex = (int)DialogueConditionValueSource.Variable;
                condition.FindPropertyRelative("methodSignature").stringValue = string.Empty;
                condition.FindPropertyRelative("comparison").enumValueIndex = (int)DialogueConditionComparison.Equal;
                condition.FindPropertyRelative("expectedValue").stringValue = string.Empty;
                condition.FindPropertyRelative("expectedObject").objectReferenceValue = null;
                condition.FindPropertyRelative("methodArguments").ClearArray();
                condition.FindPropertyRelative("methodObjectArguments").ClearArray();
                condition.FindPropertyRelative("typedMethodArguments").ClearArray();
                condition.FindPropertyRelative("useTypedValues").boolValue = true;
            }
        }

        if (conditions.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No conditions: this transition is always eligible.", MessageType.None);
            return;
        }

        if (dialogue == null)
        {
            EditorGUILayout.HelpBox("The Dialogue asset containing this transition could not be found.", MessageType.Warning);
            return;
        }

        var variables = dialogue.variables?.Where(item => item != null).ToArray() ?? Array.Empty<DialogueExposedVariable>();
        for (int i = 0; i < conditions.arraySize; i++)
        {
            var condition = conditions.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(i == 0 ? "IF" : "AND", EditorStyles.boldLabel, GUILayout.Width(34));
                var variableId = condition.FindPropertyRelative("variableId");
                DrawVariablePopup(variableId, variables);
                var sourceModeProperty = condition.FindPropertyRelative("valueSource");
                var sourceVariable = variables.FirstOrDefault(item => item.id == variableId.stringValue);
                bool canCall = CanCallVariable(sourceVariable);
                if (!canCall)
                    sourceModeProperty.enumValueIndex = (int)DialogueConditionValueSource.Variable;
                var sourceOptions = canCall ? new[] { "Value", "Call" } : new[] { "Value" };
                sourceModeProperty.enumValueIndex = EditorGUILayout.Popup(
                    sourceModeProperty.enumValueIndex, sourceOptions, GUILayout.Width(canCall ? 58 : 52));

                Type valueType = sourceVariable != null ? GetVariableSourceType(sourceVariable) : null;
                if (canCall && (DialogueConditionValueSource)sourceModeProperty.enumValueIndex == DialogueConditionValueSource.Method)
                    DrawMethodSelector(condition, valueType);

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    conditions.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    break;
                }

            }

            var sourceProperty = condition.FindPropertyRelative("valueSource");
            if ((DialogueConditionValueSource)sourceProperty.enumValueIndex == DialogueConditionValueSource.Method)
            {
                var methodSignature = condition.FindPropertyRelative("methodSignature").stringValue;
                var sourceVariable = variables.FirstOrDefault(item => item.id == condition.FindPropertyRelative("variableId").stringValue);
                var sourceType = sourceVariable != null ? GetVariableSourceType(sourceVariable) : null;
                var selectedMethod = sourceType == null ? null : GetCallableMethods(sourceType)
                    .FirstOrDefault(method => GetMethodSignature(method) == methodSignature);

                if (selectedMethod != null && selectedMethod.GetParameters().Length > 0)
                    DrawMethodArguments(condition, selectedMethod);
                else if (selectedMethod == null)
                    EditorGUILayout.LabelField("Choose a method to compare its result.", EditorStyles.label);
            }

            Type expectedType = GetConditionValueType(condition, variables);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(38);
                EditorGUILayout.LabelField(
                    (DialogueConditionValueSource)sourceProperty.enumValueIndex == DialogueConditionValueSource.Method ? "Result" : "Value",
                    EditorStyles.label, GUILayout.Width(50));
                var comparisonProperty = condition.FindPropertyRelative("comparison");
                DrawComparison(comparisonProperty, expectedType, compact: true);
                if (expectedType != null && !RequiresExpandedInspector(expectedType))
                {
                    var typedValue = condition.FindPropertyRelative("typedExpectedValue");
                    if (DrawTypedValue(typedValue, expectedType, string.Empty, compact: true))
                        condition.FindPropertyRelative("useTypedValues").boolValue = true;
                }
                if (!condition.FindPropertyRelative("useTypedValues").boolValue)
                    GUILayout.Label("legacy", EditorStyles.label, GUILayout.Width(44));
            }

            if (expectedType != null && RequiresExpandedInspector(expectedType))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(88);
                    if (DrawTypedValue(condition.FindPropertyRelative("typedExpectedValue"), expectedType, "Compare to"))
                        condition.FindPropertyRelative("useTypedValues").boolValue = true;
                }
            }

            EditorGUILayout.EndVertical();
        }
    }

    private static Type GetConditionValueType(SerializedProperty condition, DialogueExposedVariable[] variables)
    {
        var variable = variables.FirstOrDefault(item => item.id == condition.FindPropertyRelative("variableId").stringValue);
        if (variable == null) return null;
        Type sourceType = GetVariableSourceType(variable);
        if ((DialogueConditionValueSource)condition.FindPropertyRelative("valueSource").enumValueIndex == DialogueConditionValueSource.Variable)
            return sourceType;

        string signature = condition.FindPropertyRelative("methodSignature").stringValue;
        return GetCallableMethods(sourceType ?? typeof(UnityEngine.Object))
            .FirstOrDefault(method => GetMethodSignature(method) == signature)?.ReturnType;
    }

    private static void DrawVariablePopup(SerializedProperty variableId, DialogueExposedVariable[] variables)
    {
        if (variables.Length == 0)
        {
            GUILayout.Label("No variables", EditorStyles.label, GUILayout.Width(90));
            return;
        }

        var labels = variables.Select(variable => variable.name).ToArray();
        int selectedIndex = Array.FindIndex(variables, variable => variable.id == variableId.stringValue);
        int nextIndex = EditorGUILayout.Popup(Mathf.Max(0, selectedIndex), labels, GUILayout.Width(90));
        if (nextIndex >= 0 && nextIndex < variables.Length)
            variableId.stringValue = variables[nextIndex].id;
    }

    private static bool CanCallVariable(DialogueExposedVariable variable)
    {
        if (variable == null) return false;
        var type = GetVariableSourceType(variable);
        if (type == null) return false;
        if (variable.type == DialogueVariableType.Custom)
            return !type.IsValueType && type != typeof(string) &&
                type.Assembly != typeof(UnityEngine.Object).Assembly;
        return variable.type == DialogueVariableType.ObjectReference && variable.objectReferenceValue != null &&
            variable.objectReferenceValue.GetType().Assembly != typeof(UnityEngine.Object).Assembly;
    }

    private static bool RequiresExpandedInspector(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type != typeof(bool) && type != typeof(byte) && type != typeof(sbyte) &&
            type != typeof(short) && type != typeof(ushort) && type != typeof(int) &&
            type != typeof(uint) && type != typeof(long) && type != typeof(ulong) &&
            type != typeof(float) && type != typeof(double) && type != typeof(decimal) &&
            type != typeof(char) && type != typeof(string) && !type.IsEnum;
    }

    private static MethodInfo DrawMethodSelector(SerializedProperty condition, Type sourceType)
    {
        var signatureProperty = condition.FindPropertyRelative("methodSignature");
        if (sourceType == null)
        {
            return null;
        }

        var methods = GetCallableMethods(sourceType)
            .OrderBy(method => method.Name)
            .ThenBy(method => method.ToString())
            .ToArray();
        if (methods.Length == 0)
        {
            return null;
        }

        var labels = methods.Select(method => $"{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})").ToArray();
        int selectedIndex = Array.FindIndex(methods, method => GetMethodSignature(method) == signatureProperty.stringValue);
        int nextIndex = EditorGUILayout.Popup(selectedIndex, labels, GUILayout.MinWidth(70));
        if (nextIndex < 0) return null;
        var selectedMethod = methods[Mathf.Clamp(nextIndex, 0, methods.Length - 1)];
        if (signatureProperty.stringValue != GetMethodSignature(selectedMethod))
            condition.FindPropertyRelative("useTypedValues").boolValue = true;
        signatureProperty.stringValue = GetMethodSignature(selectedMethod);
        return selectedMethod;
    }

    private static void DrawMethodArguments(SerializedProperty condition, MethodInfo selectedMethod)
    {
        var parameters = selectedMethod.GetParameters();
        var arguments = condition.FindPropertyRelative("typedMethodArguments");
        arguments.arraySize = parameters.Length;
        var typedMode = condition.FindPropertyRelative("useTypedValues");
        var inlineIndices = Enumerable.Range(0, parameters.Length)
            .Where(index => !RequiresExpandedInspector(parameters[index].ParameterType)).ToArray();
        if (inlineIndices.Length > 0)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(78);
                GUILayout.Label("Arguments", EditorStyles.label, GUILayout.Width(64));
                foreach (int i in inlineIndices)
                {
                    if (DrawTypedValue(arguments.GetArrayElementAtIndex(i), parameters[i].ParameterType,
                        parameters[i].Name, compact: true))
                        typedMode.boolValue = true;
                }
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (!RequiresExpandedInspector(parameters[i].ParameterType)) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(88);
                if (DrawTypedValue(arguments.GetArrayElementAtIndex(i), parameters[i].ParameterType,
                    parameters[i].Name))
                    typedMode.boolValue = true;
            }
        }
    }

    private static bool IsSupportedConditionValueType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        bool unityObject = typeof(UnityEngine.Object).IsAssignableFrom(type);
        return type != typeof(object) && (unityObject || (!type.IsAbstract && !type.IsInterface)) &&
            (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
             unityObject || (type.IsSerializable &&
                (typeof(IComparable).IsAssignableFrom(type) || type.IsValueType)));
    }

    private static bool IsSupportedConditionArgumentType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        bool unityObject = typeof(UnityEngine.Object).IsAssignableFrom(type);
        return unityObject || (type != typeof(object) && !type.IsAbstract && !type.IsInterface &&
            (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
             (type.IsValueType && type.IsSerializable) || (type.IsClass && type.IsSerializable)));
    }

    private static void DrawComparison(SerializedProperty comparison, Type valueType, bool compact = false)
    {
        bool canOrder = valueType != null && valueType != typeof(bool) &&
            typeof(IComparable).IsAssignableFrom(Nullable.GetUnderlyingType(valueType) ?? valueType);
        var options = canOrder
            ? (DialogueConditionComparison[])Enum.GetValues(typeof(DialogueConditionComparison))
            : new[] { DialogueConditionComparison.Equal, DialogueConditionComparison.NotEqual };
        int selectedIndex = Array.IndexOf(options, (DialogueConditionComparison)comparison.enumValueIndex);
        if (selectedIndex < 0) selectedIndex = 0;
        var labels = options.Select(option => option switch
        {
            DialogueConditionComparison.Equal => "==",
            DialogueConditionComparison.NotEqual => "!=",
            DialogueConditionComparison.Greater => ">",
            DialogueConditionComparison.GreaterOrEqual => ">=",
            DialogueConditionComparison.Less => "<",
            _ => "<="
        }).ToArray();
        comparison.enumValueIndex = (int)options[compact
            ? EditorGUILayout.Popup(selectedIndex, labels, GUILayout.Width(48))
            : EditorGUILayout.Popup("Comparison", selectedIndex, labels)];
    }

    internal static bool DrawTypedValue(
        SerializedProperty serializedValue,
        Type valueType,
        string label,
        bool compact = false,
        SerializedProperty fieldBindings = null,
        bool presetOnly = false,
        SerializedProperty localFieldOverrides = null,
        object presetValue = null,
        Action inputPortsChanged = null)
    {
        if (valueType == null) return false;
        valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        var dialogueType = GetDialogueVariableType(valueType);
        var typeProperty = serializedValue.FindPropertyRelative("type");
        bool typeChanged = typeProperty.enumValueIndex != (int)dialogueType;
        if (typeChanged)
        {
            typeProperty.enumValueIndex = (int)dialogueType;
            ResetTypedValue(serializedValue, dialogueType, valueType);
        }

        if (dialogueType is DialogueVariableType.Enum or DialogueVariableType.Custom)
            serializedValue.FindPropertyRelative("customTypeName").stringValue = valueType.AssemblyQualifiedName;

        EditorGUI.BeginChangeCheck();
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        bool customValueChanged = false;
        if (compact && !string.IsNullOrEmpty(label))
            EditorGUIUtility.labelWidth = 54;
        switch (dialogueType)
        {
            case DialogueVariableType.String:
                DrawStringValue(serializedValue.FindPropertyRelative("stringValue"), label, compact: compact);
                break;
            case DialogueVariableType.Character:
                DrawStringValue(serializedValue.FindPropertyRelative("charValue"), label, singleCharacter: true, compact: compact);
                break;
            case DialogueVariableType.Decimal:
                DrawStringValue(serializedValue.FindPropertyRelative("decimalValue"), label, compact: compact);
                break;
            case DialogueVariableType.Enum:
            {
                var enumValue = serializedValue.FindPropertyRelative("enumValue");
                if (valueType.IsEnum)
                {
                    var current = (Enum)Enum.ToObject(valueType, enumValue.intValue);
                    var selected = compact
                        ? string.IsNullOrEmpty(label)
                            ? EditorGUILayout.EnumPopup(current, GUILayout.MinWidth(60))
                            : EditorGUILayout.EnumPopup(label, current, GUILayout.MinWidth(60))
                        : EditorGUILayout.EnumPopup(label, current);
                    enumValue.intValue = Convert.ToInt32(selected, CultureInfo.InvariantCulture);
                }
                else
                    EditorGUILayout.PropertyField(enumValue, compact ? GUIContent.none : new GUIContent(label),
                        true, GUILayout.MinWidth(60));
                break;
            }
            case DialogueVariableType.ObjectReference:
            {
                var objectValue = serializedValue.FindPropertyRelative("objectReferenceValue");
                objectValue.objectReferenceValue = compact
                    ? string.IsNullOrEmpty(label)
                        ? EditorGUILayout.ObjectField(objectValue.objectReferenceValue, valueType, false, GUILayout.MinWidth(70))
                        : EditorGUILayout.ObjectField(label, objectValue.objectReferenceValue, valueType, false, GUILayout.MinWidth(70))
                    : EditorGUILayout.ObjectField(label, objectValue.objectReferenceValue, valueType, false);
                break;
            }
            case DialogueVariableType.Custom:
                customValueChanged = DrawCustomValue(
                    serializedValue, valueType, label, fieldBindings,
                    presetOnly, localFieldOverrides, presetValue, inputPortsChanged);
                break;
            default:
                var fieldName = GetValueFieldName(dialogueType);
                var field = serializedValue.FindPropertyRelative(fieldName);
                if (field != null)
                    EditorGUILayout.PropertyField(field,
                        compact && string.IsNullOrEmpty(label) ? GUIContent.none : new GUIContent(label),
                        true, GUILayout.MinWidth(compact ? 65 : 0));
                break;
        }

        bool changed = EditorGUI.EndChangeCheck() || customValueChanged;
        EditorGUIUtility.labelWidth = previousLabelWidth;
        return changed;
    }

    private static DialogueVariableType GetDialogueVariableType(Type type)
    {
        if (type == typeof(bool)) return DialogueVariableType.Boolean;
        if (type == typeof(byte)) return DialogueVariableType.Byte;
        if (type == typeof(sbyte)) return DialogueVariableType.SByte;
        if (type == typeof(short)) return DialogueVariableType.Short;
        if (type == typeof(ushort)) return DialogueVariableType.UShort;
        if (type == typeof(int)) return DialogueVariableType.Integer;
        if (type == typeof(uint)) return DialogueVariableType.UInteger;
        if (type == typeof(long)) return DialogueVariableType.Long;
        if (type == typeof(ulong)) return DialogueVariableType.ULong;
        if (type == typeof(float)) return DialogueVariableType.Float;
        if (type == typeof(double)) return DialogueVariableType.Double;
        if (type == typeof(decimal)) return DialogueVariableType.Decimal;
        if (type == typeof(char)) return DialogueVariableType.Character;
        if (type == typeof(string)) return DialogueVariableType.String;
        if (type == typeof(Vector2)) return DialogueVariableType.Vector2;
        if (type == typeof(Vector2Int)) return DialogueVariableType.Vector2Int;
        if (type == typeof(Vector3)) return DialogueVariableType.Vector3;
        if (type == typeof(Vector3Int)) return DialogueVariableType.Vector3Int;
        if (type == typeof(Vector4)) return DialogueVariableType.Vector4;
        if (type == typeof(Quaternion)) return DialogueVariableType.Quaternion;
        if (type == typeof(Color)) return DialogueVariableType.Color;
        if (type == typeof(Color32)) return DialogueVariableType.Color32;
        if (type == typeof(Rect)) return DialogueVariableType.Rect;
        if (type == typeof(RectInt)) return DialogueVariableType.RectInt;
        if (type == typeof(Bounds)) return DialogueVariableType.Bounds;
        if (type == typeof(BoundsInt)) return DialogueVariableType.BoundsInt;
        if (type == typeof(Matrix4x4)) return DialogueVariableType.Matrix4x4;
        if (type == typeof(LayerMask)) return DialogueVariableType.LayerMask;
        if (type == typeof(AnimationCurve)) return DialogueVariableType.AnimationCurve;
        if (type == typeof(Gradient)) return DialogueVariableType.Gradient;
        if (type.IsEnum) return DialogueVariableType.Enum;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return DialogueVariableType.ObjectReference;
        return DialogueVariableType.Custom;
    }

    private static string GetValueFieldName(DialogueVariableType type)
    {
        return type switch
        {
            DialogueVariableType.Boolean => "boolValue",
            DialogueVariableType.Byte => "byteValue",
            DialogueVariableType.SByte => "sbyteValue",
            DialogueVariableType.Short => "shortValue",
            DialogueVariableType.UShort => "ushortValue",
            DialogueVariableType.Integer => "intValue",
            DialogueVariableType.UInteger => "uintValue",
            DialogueVariableType.Long => "longValue",
            DialogueVariableType.ULong => "ulongValue",
            DialogueVariableType.Float => "floatValue",
            DialogueVariableType.Double => "doubleValue",
            DialogueVariableType.Vector2 => "vector2Value",
            DialogueVariableType.Vector2Int => "vector2IntValue",
            DialogueVariableType.Vector3 => "vector3Value",
            DialogueVariableType.Vector3Int => "vector3IntValue",
            DialogueVariableType.Vector4 => "vector4Value",
            DialogueVariableType.Quaternion => "quaternionValue",
            DialogueVariableType.Color => "colorValue",
            DialogueVariableType.Color32 => "color32Value",
            DialogueVariableType.Rect => "rectValue",
            DialogueVariableType.RectInt => "rectIntValue",
            DialogueVariableType.Bounds => "boundsValue",
            DialogueVariableType.BoundsInt => "boundsIntValue",
            DialogueVariableType.Matrix4x4 => "matrix4x4Value",
            DialogueVariableType.LayerMask => "layerMaskValue",
            DialogueVariableType.AnimationCurve => "animationCurveValue",
            DialogueVariableType.Gradient => "gradientValue",
            _ => string.Empty
        };
    }

    private static void DrawStringValue(
        SerializedProperty property,
        string label,
        bool singleCharacter = false,
        bool compact = false)
    {
        string value = compact
            ? string.IsNullOrEmpty(label)
                ? EditorGUILayout.TextField(property.stringValue, GUILayout.MinWidth(60))
                : EditorGUILayout.TextField(label, property.stringValue, GUILayout.MinWidth(60))
            : EditorGUILayout.TextField(label, property.stringValue);
        property.stringValue = singleCharacter && value.Length > 1 ? value.Substring(0, 1) : value;
    }

    private static bool DrawCustomValue(
        SerializedProperty serializedValue,
        Type valueType,
        string label,
        SerializedProperty fieldBindings = null,
        bool presetOnly = false,
        SerializedProperty localFieldOverrides = null,
        object presetValue = null,
        Action inputPortsChanged = null)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
        {
            var objectValue = serializedValue.FindPropertyRelative("objectReferenceValue");
            var previousValue = objectValue.objectReferenceValue;
            objectValue.objectReferenceValue = EditorGUILayout.ObjectField(
                label, previousValue, valueType, false);
            return objectValue.objectReferenceValue != previousValue;
        }

        if (valueType.IsEnum)
        {
            var enumValue = serializedValue.FindPropertyRelative("enumValue");
            EditorGUI.BeginChangeCheck();
            enumValue.intValue = Convert.ToInt32(EditorGUILayout.EnumPopup(
                label, (Enum)Enum.ToObject(valueType, enumValue.intValue)), CultureInfo.InvariantCulture);
            return EditorGUI.EndChangeCheck();
        }

        if (!valueType.IsValueType && fieldBindings == null && !presetOnly)
        {
            var managedValue = serializedValue.FindPropertyRelative("customManagedValue");
            EditorGUI.BeginChangeCheck();
            if (managedValue.managedReferenceValue == null || managedValue.managedReferenceValue.GetType() != valueType)
            {
                try
                {
                    managedValue.managedReferenceValue = Activator.CreateInstance(valueType, nonPublic: true);
                }
                catch
                {
                    EditorGUILayout.HelpBox($"{valueType.Name} needs a parameterless constructor to edit its fields.", MessageType.Warning);
                    return EditorGUI.EndChangeCheck();
                }
            }
            EditorGUILayout.PropertyField(managedValue, new GUIContent(label), true);
            return EditorGUI.EndChangeCheck();
        }

        var customJson = serializedValue.FindPropertyRelative("customJson");
        object customValue;
        var managedValueProperty = serializedValue.FindPropertyRelative("customManagedValue");
        bool restoreSerializedReferences = true;
        if (!valueType.IsValueType && managedValueProperty.managedReferenceValue != null)
        {
            var existingManagedValue = managedValueProperty.managedReferenceValue;
            if (existingManagedValue.GetType() == valueType)
            {
                var objectReferences = CollectObjectReferences(existingManagedValue, valueType, string.Empty, 0);
                try
                {
                    customValue = JsonUtility.FromJson(JsonUtility.ToJson(existingManagedValue), valueType);
                }
                catch
                {
                    customValue = null;
                }

                if (customValue != null)
                {
                    foreach (var reference in objectReferences)
                        RestoreObjectReference(customValue, reference.Key.Split('.'), 0, reference.Value);
                    restoreSerializedReferences = false;
                }
                else
                    customValue = existingManagedValue;
            }
            else
                customValue = null;
        }
        else
            customValue = null;

        if (customValue == null) try
        {
            customValue = string.IsNullOrEmpty(customJson.stringValue)
                ? Activator.CreateInstance(valueType)
                : JsonUtility.FromJson(customJson.stringValue, valueType);
        }
        catch
        {
            customValue = Activator.CreateInstance(valueType);
        }
        customValue ??= Activator.CreateInstance(valueType);

        var references = serializedValue.FindPropertyRelative("customObjectReferences");
        if (restoreSerializedReferences)
        {
            for (int i = 0; i < references.arraySize; i++)
            {
                var reference = references.GetArrayElementAtIndex(i);
                RestoreObjectReference(customValue,
                    reference.FindPropertyRelative("fieldPath").stringValue.Split('.'), 0,
                    reference.FindPropertyRelative("value").objectReferenceValue);
            }
        }

        EditorGUILayout.LabelField($"{label} ({valueType.Name})", EditorStyles.boldLabel);
        var serializedReferences = CollectObjectReferences(customValue, valueType, string.Empty, 0);
        bool changed = false;
        var presetContext = new PresetFieldContext
        {
            presetOnly = presetOnly,
            rootValue = customValue,
            presetValue = presetValue,
            localFieldOverrides = localFieldOverrides,
            serializedValue = serializedValue,
            fieldBindings = fieldBindings,
            inputPortsChanged = inputPortsChanged
        };
        DrawSerializableFields(customValue, valueType, string.Empty, 0, serializedReferences,
            fieldBindings, ref changed, presetContext);
        if (!changed) return false;

        if (!valueType.IsValueType)
            managedValueProperty.managedReferenceValue = customValue;
        customJson.stringValue = JsonUtility.ToJson(customValue, true);
        references.arraySize = serializedReferences.Count;
        int referenceIndex = 0;
        foreach (var pair in serializedReferences)
        {
            var reference = references.GetArrayElementAtIndex(referenceIndex++);
            reference.FindPropertyRelative("fieldPath").stringValue = pair.Key;
            reference.FindPropertyRelative("value").objectReferenceValue = pair.Value;
        }
        return true;
    }

    private static void DrawSerializableFields(
        object value,
        Type valueType,
        string path,
        int depth,
        Dictionary<string, UnityEngine.Object> references,
        SerializedProperty fieldBindings,
        ref bool changed,
        PresetFieldContext presetContext,
        bool revealAll = false,
        string inheritedPresetPath = null)
    {
        if (value == null || depth >= 5) return;
        foreach (var field in GetSerializableFields(valueType))
        {
            bool isPresetOverride = field.IsDefined(typeof(DialoguePresetOverrideAttribute), true);
            if (presetContext?.presetOnly == true && !revealAll && !isPresetOverride &&
                !HasPresetOverrideDescendant(field.FieldType, 0))
                continue;

            var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
            var presetOwnerPath = inheritedPresetPath ?? (isPresetOverride ? fieldPath : null);
            var currentValue = field.GetValue(value);
            if (IsPresetFieldLocked(presetContext, presetOwnerPath))
                currentValue = GetValueAtPath(presetContext.presetValue, fieldPath);
            bool fieldChanged = false;
            var updatedValue = DrawSerializableField(field.Name, field.FieldType, currentValue,
                fieldPath, depth, references, field, fieldBindings, ref fieldChanged,
                presetContext, presetOwnerPath, revealAll || isPresetOverride);
            if (fieldChanged) changed = true;
            if (fieldChanged || !Equals(currentValue, updatedValue))
                field.SetValue(value, updatedValue);
        }
    }

    private static object DrawSerializableField(
        string label,
        Type type,
        object currentValue,
        string path,
        int depth,
        Dictionary<string, UnityEngine.Object> references,
        FieldInfo fieldInfo,
        SerializedProperty fieldBindings,
        ref bool changed,
        PresetFieldContext presetContext,
        string presetOwnerPath,
        bool revealAll)
    {
        if (TryGetCollectionElementType(type, out var elementType))
        {
            int count = currentValue is IList list ? list.Count : 0;
            EditorGUILayout.LabelField($"{label} ({count})", EditorStyles.boldLabel);
            HandlePresetFieldContextMenu(GUILayoutUtility.GetLastRect(), presetContext, path, presetOwnerPath, currentValue, type);
            if (IsFieldBound(fieldBindings, path)) return currentValue;
            return DrawSerializableCollection(label, type, elementType, currentValue, path, depth,
                references, fieldBindings, ref changed,
                presetContext, presetOwnerPath, revealAll);
        }

        if (IsNestedSerializableType(type, depth))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            HandlePresetFieldContextMenu(GUILayoutUtility.GetLastRect(), presetContext, path, presetOwnerPath, currentValue, type);
            if (IsFieldBound(fieldBindings, path)) return currentValue;

            object nestedValue = currentValue;
            if (nestedValue == null)
            {
                try { nestedValue = Activator.CreateInstance(type, nonPublic: true); }
                catch { return null; }
            }
            using (new EditorGUI.DisabledScope(IsPresetFieldLocked(presetContext, presetOwnerPath)))
                DrawSerializableFields(nestedValue, type, path, depth + 1, references,
                    fieldBindings, ref changed, presetContext,
                    revealAll, presetOwnerPath);
            return nestedValue;
        }

        foreach (var space in fieldInfo?.GetCustomAttributes<SpaceAttribute>() ?? Array.Empty<SpaceAttribute>())
            GUILayout.Space(space.height);
        foreach (var header in fieldInfo?.GetCustomAttributes<HeaderAttribute>() ?? Array.Empty<HeaderAttribute>())
            EditorGUILayout.LabelField(header.header, EditorStyles.boldLabel);

        var tooltip = fieldInfo?.GetCustomAttribute<TooltipAttribute>()?.tooltip;
        var fieldLabel = new GUIContent(label, tooltip);
        object result = currentValue;
        var textArea = fieldInfo?.GetCustomAttribute<TextAreaAttribute>();
        var multiline = fieldInfo?.GetCustomAttribute<MultilineAttribute>();
        if (type == typeof(string) && (textArea != null || multiline != null))
        {
            EditorGUILayout.LabelField(fieldLabel);
            Rect fieldRect;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(IsFieldBound(fieldBindings, path) || IsPresetFieldLocked(presetContext, presetOwnerPath)))
                {
                    EditorGUI.BeginChangeCheck();
                    if (textArea != null)
                        result = EditorGUILayout.TextArea(currentValue as string ?? string.Empty,
                            GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * textArea.minLines),
                            GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight * textArea.maxLines));
                    else
                        result = EditorGUILayout.TextArea(currentValue as string ?? string.Empty,
                            GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * multiline.lines));
                    if (EditorGUI.EndChangeCheck()) changed = true;
                }
                fieldRect = GUILayoutUtility.GetLastRect();
            }
            HandlePresetFieldContextMenu(fieldRect, presetContext, path, presetOwnerPath, result, type);
        }
        else
        {
            Rect fieldRect;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(IsFieldBound(fieldBindings, path) || IsPresetFieldLocked(presetContext, presetOwnerPath)))
                {
                    if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                    {
                        EditorGUI.BeginChangeCheck();
                        result = EditorGUILayout.ObjectField(fieldLabel, currentValue as UnityEngine.Object, type, false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            changed = true;
                            if (result is UnityEngine.Object objectReference && objectReference != null)
                                references[path] = objectReference;
                            else
                                references.Remove(path);
                        }
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        DrawSimpleSerializableField(fieldLabel, type, currentValue, fieldInfo, ref result);
                        if (EditorGUI.EndChangeCheck()) changed = true;
                    }
                }
                fieldRect = GUILayoutUtility.GetLastRect();
            }
            HandlePresetFieldContextMenu(fieldRect, presetContext, path, presetOwnerPath, result, type);
        }

        return result;
    }

    private static bool IsNestedSerializableType(Type type, int depth)
    {
        if (!type.IsSerializable || depth >= 4 || type == typeof(string) || type.IsPrimitive || type.IsEnum ||
            typeof(UnityEngine.Object).IsAssignableFrom(type) || TryGetCollectionElementType(type, out _))
            return false;
        return type != typeof(decimal) && type != typeof(Vector2) && type != typeof(Vector2Int) &&
            type != typeof(Vector3) && type != typeof(Vector3Int) && type != typeof(Vector4) &&
            type != typeof(Quaternion) && type != typeof(Color) && type != typeof(Color32) &&
            type != typeof(Rect) && type != typeof(RectInt) && type != typeof(Bounds) &&
            type != typeof(BoundsInt) && type != typeof(Matrix4x4) && type != typeof(LayerMask) &&
            type != typeof(AnimationCurve) && type != typeof(Gradient);
    }

    private static void DrawSimpleSerializableField(
        GUIContent label,
        Type type,
        object currentValue,
        FieldInfo fieldInfo,
        ref object result)
    {
        if (type == typeof(bool)) result = EditorGUILayout.Toggle(label, currentValue != null && (bool)currentValue);
        else if (type == typeof(string))
        {
            var multiline = fieldInfo?.GetCustomAttribute<MultilineAttribute>();
            result = fieldInfo?.IsDefined(typeof(DelayedAttribute), true) == true
                ? EditorGUILayout.DelayedTextField(label, currentValue as string ?? string.Empty)
                : multiline != null
                    ? EditorGUILayout.TextArea(currentValue as string ?? string.Empty,
                        GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * multiline.lines))
                    : EditorGUILayout.TextField(label, currentValue as string ?? string.Empty);
        }
        else if (type == typeof(char))
        {
            string text = fieldInfo?.IsDefined(typeof(DelayedAttribute), true) == true
                ? EditorGUILayout.DelayedTextField(label, currentValue?.ToString() ?? string.Empty)
                : EditorGUILayout.TextField(label, currentValue?.ToString() ?? string.Empty);
            result = text.Length == 0 ? '\0' : text[0];
        }
        else if (type == typeof(int))
        {
            var range = fieldInfo?.GetCustomAttribute<RangeAttribute>();
            int value = currentValue == null ? 0 : (int)currentValue;
            result = range != null
                ? EditorGUILayout.IntSlider(label, value, Mathf.RoundToInt(range.min), Mathf.RoundToInt(range.max))
                : fieldInfo?.IsDefined(typeof(DelayedAttribute), true) == true
                    ? EditorGUILayout.DelayedIntField(label, value)
                    : EditorGUILayout.IntField(label, value);
        }
        else if (type == typeof(float))
        {
            var range = fieldInfo?.GetCustomAttribute<RangeAttribute>();
            float value = currentValue == null ? 0f : (float)currentValue;
            result = range != null
                ? EditorGUILayout.Slider(label, value, range.min, range.max)
                : EditorGUILayout.FloatField(label, value);
            if (fieldInfo?.GetCustomAttribute<MinAttribute>() is MinAttribute min)
                result = Mathf.Max(min.min, (float)result);
        }
        else if (type == typeof(long)) result = EditorGUILayout.LongField(label, currentValue == null ? 0L : (long)currentValue);
        else if (type == typeof(double)) result = EditorGUILayout.DoubleField(label, currentValue == null ? 0d : (double)currentValue);
        else if (type == typeof(decimal))
        {
            string text = EditorGUILayout.TextField(label, currentValue?.ToString() ?? "0");
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                result = decimalValue;
        }
        else if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort))
        {
            int number = EditorGUILayout.IntField(label, currentValue == null ? 0 : Convert.ToInt32(currentValue, CultureInfo.InvariantCulture));
            if (type == typeof(byte)) number = Mathf.Clamp(number, byte.MinValue, byte.MaxValue);
            else if (type == typeof(sbyte)) number = Mathf.Clamp(number, sbyte.MinValue, sbyte.MaxValue);
            else if (type == typeof(short)) number = Mathf.Clamp(number, short.MinValue, short.MaxValue);
            else number = Mathf.Clamp(number, ushort.MinValue, ushort.MaxValue);
            result = Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
        }
        else if (type == typeof(uint) || type == typeof(ulong))
        {
            string text = EditorGUILayout.TextField(label, currentValue?.ToString() ?? "0");
            if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsignedValue))
                result = Convert.ChangeType(unsignedValue, type, CultureInfo.InvariantCulture);
        }
        else if (type.IsEnum) result = EditorGUILayout.EnumPopup(label, (Enum)(currentValue ?? Enum.ToObject(type, 0)));
        else if (type == typeof(Vector2)) result = EditorGUILayout.Vector2Field(label, currentValue == null ? default : (Vector2)currentValue);
        else if (type == typeof(Vector2Int)) result = EditorGUILayout.Vector2IntField(label, currentValue == null ? default : (Vector2Int)currentValue);
        else if (type == typeof(Vector3)) result = EditorGUILayout.Vector3Field(label, currentValue == null ? default : (Vector3)currentValue);
        else if (type == typeof(Vector3Int)) result = EditorGUILayout.Vector3IntField(label, currentValue == null ? default : (Vector3Int)currentValue);
        else if (type == typeof(Vector4)) result = EditorGUILayout.Vector4Field(label, currentValue == null ? default : (Vector4)currentValue);
        else if (type == typeof(Quaternion))
        {
            var quaternion = currentValue == null ? Quaternion.identity : (Quaternion)currentValue;
            var vector = EditorGUILayout.Vector4Field(label, new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w));
            result = new Quaternion(vector.x, vector.y, vector.z, vector.w);
        }
        else if (type == typeof(Color)) result = EditorGUILayout.ColorField(label, currentValue == null ? Color.white : (Color)currentValue);
        else if (type == typeof(Color32)) result = (Color32)EditorGUILayout.ColorField(label, currentValue == null ? Color.white : (Color)(Color32)currentValue);
        else if (type == typeof(Rect)) result = EditorGUILayout.RectField(label, currentValue == null ? default : (Rect)currentValue);
        else if (type == typeof(RectInt)) result = EditorGUILayout.RectIntField(label, currentValue == null ? default : (RectInt)currentValue);
        else if (type == typeof(Bounds)) result = EditorGUILayout.BoundsField(label, currentValue == null ? default : (Bounds)currentValue);
        else if (type == typeof(BoundsInt)) result = EditorGUILayout.BoundsIntField(label, currentValue == null ? default : (BoundsInt)currentValue);
        else if (type == typeof(AnimationCurve)) result = EditorGUILayout.CurveField(label, currentValue as AnimationCurve);
        else if (type == typeof(Gradient)) result = EditorGUILayout.GradientField(label, currentValue as Gradient ?? new Gradient());
        else EditorGUILayout.LabelField(label, currentValue?.ToString() ?? $"({type.Name})");
    }

    private static bool TryGetCollectionElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        if (type.IsGenericType && typeof(IList).IsAssignableFrom(type))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static object DrawSerializableCollection(
        string label,
        Type collectionType,
        Type elementType,
        object currentValue,
        string path,
        int depth,
        Dictionary<string, UnityEngine.Object> references,
        SerializedProperty fieldBindings,
        ref bool changed,
        PresetFieldContext presetContext,
        string presetOwnerPath,
        bool revealAll)
    {
        var values = new List<object>();
        if (currentValue is IList currentList)
            foreach (var item in currentList) values.Add(item);

        EditorGUILayout.LabelField($"{label} ({values.Count})", EditorStyles.boldLabel);
        HandlePresetFieldContextMenu(GUILayoutUtility.GetLastRect(), presetContext, path, presetOwnerPath, currentValue, collectionType);

        EditorGUI.indentLevel++;
        bool collectionChanged = false;
        using (new EditorGUI.DisabledScope(IsPresetFieldLocked(presetContext, presetOwnerPath)))
        {
            for (int i = 0; i < values.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool itemChanged = false;
                    var updated = DrawSerializableField($"Element {i}", elementType, values[i],
                        $"{path}.{i}", depth + 1, references, null, fieldBindings,
                        ref itemChanged, presetContext, presetOwnerPath, revealAll);
                    if (!Equals(updated, values[i]))
                    {
                        values[i] = updated;
                        itemChanged = true;
                    }
                    collectionChanged |= itemChanged;

                    bool canChangeCollection = presetContext?.presetOnly != true || revealAll;
                    using (new EditorGUI.DisabledScope(!canChangeCollection))
                    {
                        if (GUILayout.Button("-", GUILayout.Width(22)))
                        {
                            values.RemoveAt(i--);
                            collectionChanged = true;
                        }
                    }
                }
            }

            bool canAddCollectionElement = presetContext?.presetOnly != true || revealAll;
            using (new EditorGUI.DisabledScope(!canAddCollectionElement))
            {
                if (GUILayout.Button("+ Element"))
                {
                    object newValue = null;
                    if (elementType.IsValueType)
                        newValue = Activator.CreateInstance(elementType);
                    else if (!elementType.IsAbstract && !elementType.IsInterface)
                    {
                        try { newValue = Activator.CreateInstance(elementType, nonPublic: true); }
                        catch { }
                    }
                    values.Add(newValue);
                    collectionChanged = true;
                }
            }
        }
        EditorGUI.indentLevel--;

        if (!collectionChanged) return currentValue;
        changed = true;
        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++) array.SetValue(values[i], i);
            return array;
        }

        IList result = currentValue as IList;
        if (result == null)
        {
            try { result = (IList)Activator.CreateInstance(collectionType); }
            catch { return currentValue; }
        }
        result.Clear();
        foreach (var item in values) result.Add(item);
        return result;
    }

    private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
    {
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsStatic && !field.IsInitOnly &&
                !field.IsDefined(typeof(NonSerializedAttribute), true) &&
                (field.IsPublic || field.IsDefined(typeof(SerializeField), true)));
    }

    private static bool HasPresetOverrideDescendant(Type type, int depth)
    {
        if (type == null || depth >= 5 || type == typeof(string) || type.IsPrimitive || type.IsEnum ||
            typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;

        if (TryGetCollectionElementType(type, out var elementType))
            return HasPresetOverrideDescendant(elementType, depth + 1);

        foreach (var field in GetSerializableFields(type))
        {
            if (field.IsDefined(typeof(DialoguePresetOverrideAttribute), true) ||
                HasPresetOverrideDescendant(field.FieldType, depth + 1))
                return true;
        }

        return false;
    }

    private static bool IsPresetFieldLocked(PresetFieldContext context, string path)
    {
        if (context?.presetOnly != false || context.presetValue == null ||
            context.localFieldOverrides == null || string.IsNullOrEmpty(path))
            return false;

        for (int i = 0; i < context.localFieldOverrides.arraySize; i++)
        {
            if (context.localFieldOverrides.GetArrayElementAtIndex(i).stringValue == path)
                return false;
        }

        return true;
    }

    private static bool IsFieldBound(SerializedProperty bindings, string fieldPath)
    {
        if (bindings == null) return false;
        for (int i = 0; i < bindings.arraySize; i++)
        {
            var binding = bindings.GetArrayElementAtIndex(i);
            if (binding.FindPropertyRelative("fieldPath").stringValue == fieldPath &&
                !string.IsNullOrEmpty(binding.FindPropertyRelative("variableId").stringValue))
                return true;
        }

        return false;
    }

    private static void HandlePresetFieldContextMenu(
        Rect rect,
        PresetFieldContext context,
        string fieldPath,
        string presetOwnerPath,
        object fieldValue,
        Type fieldType)
    {
        var currentEvent = Event.current;
        bool isRightClick = currentEvent.type == EventType.ContextClick ||
            currentEvent.type == EventType.MouseDown && currentEvent.button == 1;
        if (!isRightClick || !rect.Contains(currentEvent.mousePosition))
            return;

        var menu = new GenericMenu();
        var clipboardValue = GetClipboardValue(fieldValue);
        menu.AddItem(new GUIContent("Copy Value"), false,
            () => EditorGUIUtility.systemCopyBuffer = clipboardValue);

        var clipboardText = EditorGUIUtility.systemCopyBuffer;
        bool canPaste = TryParseClipboardValue(clipboardText, fieldType, out _);
        if (canPaste)
        {
            var targetObject = context?.serializedValue?.serializedObject.targetObject;
            var valuePropertyPath = context?.serializedValue?.propertyPath;
            var overridesPropertyPath = context?.localFieldOverrides?.propertyPath;
            var bindingsPropertyPath = context?.fieldBindings?.propertyPath;
            var presetValue = context?.presetValue;
            menu.AddItem(new GUIContent("Paste Value"), false,
                () => PasteFieldValue(targetObject, valuePropertyPath, overridesPropertyPath,
                    bindingsPropertyPath, presetValue, fieldPath, presetOwnerPath, fieldType, clipboardText));
        }
        else
            menu.AddDisabledItem(new GUIContent("Paste Value"));

        if (context?.fieldBindings != null && !string.IsNullOrEmpty(fieldPath))
        {
            bool isExposed = Enumerable.Range(0, context.fieldBindings.arraySize)
                .Any(index => context.fieldBindings.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("fieldPath").stringValue == fieldPath);
            var targetObject = context.serializedValue?.serializedObject.targetObject;
            var bindingsPropertyPath = context.fieldBindings.propertyPath;
            var inputPortsChanged = context.inputPortsChanged;
            menu.AddItem(new GUIContent(isExposed ? "Hide Input" : "Expose as Input"), false,
                () => SetFieldInputExposed(targetObject, bindingsPropertyPath, fieldPath,
                    !isExposed, inputPortsChanged));
        }

        bool canTogglePresetField = context?.presetOnly == false && context.presetValue != null &&
            context.localFieldOverrides != null && !string.IsNullOrEmpty(presetOwnerPath);
        if (canTogglePresetField)
        {
            bool isLocal = !IsPresetFieldLocked(context, presetOwnerPath);
            var targetObject = context.serializedValue.serializedObject.targetObject;
            var valuePropertyPath = context.serializedValue.propertyPath;
            var overridesPropertyPath = context.localFieldOverrides.propertyPath;
            var presetValue = context.presetValue;
            if (isLocal)
                menu.AddItem(new GUIContent("Use Character Preset"), false,
                    () => SetPresetFieldLocal(targetObject, valuePropertyPath, overridesPropertyPath,
                        presetValue, presetOwnerPath, false));
            else
                menu.AddItem(new GUIContent("Edit Locally"), false,
                    () => SetPresetFieldLocal(targetObject, valuePropertyPath, overridesPropertyPath,
                        presetValue, presetOwnerPath, true));
        }

        menu.ShowAsContext();
        currentEvent.Use();
    }

    private static void SetFieldInputExposed(
        UnityEngine.Object targetObject,
        string bindingsPropertyPath,
        string fieldPath,
        bool exposed,
        Action inputPortsChanged)
    {
        if (targetObject == null) return;
        var serializedObject = new SerializedObject(targetObject);
        serializedObject.Update();
        var bindings = serializedObject.FindProperty(bindingsPropertyPath);
        if (bindings == null) return;
        Undo.RecordObject(targetObject, exposed ? "Expose Dialogue Input" : "Hide Dialogue Input");

        int existingIndex = -1;
        for (int i = bindings.arraySize - 1; i >= 0; i--)
        {
            if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("fieldPath").stringValue != fieldPath)
                continue;
            if (existingIndex < 0) existingIndex = i;
            else bindings.DeleteArrayElementAtIndex(i);
        }

        if (exposed && existingIndex < 0)
        {
            var binding = bindings.GetArrayElementAtIndex(bindings.arraySize++);
            binding.FindPropertyRelative("fieldPath").stringValue = fieldPath;
            binding.FindPropertyRelative("variableId").stringValue = string.Empty;
        }
        else if (!exposed && existingIndex >= 0)
        {
            bindings.DeleteArrayElementAtIndex(existingIndex);
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetObject);
        inputPortsChanged?.Invoke();
    }

    private static string GetClipboardValue(object value)
    {
        if (value == null) return string.Empty;
        if (value is UnityEngine.Object unityObject)
        {
            if (unityObject == null) return string.Empty;
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            return string.IsNullOrEmpty(assetPath) ? unityObject.name : assetPath;
        }

        if (value is string text) return text;
        if (value is IList collection)
        {
            var clipboardCollection = new ClipboardCollection();
            foreach (var item in collection)
                clipboardCollection.values.Add(GetClipboardValue(item));
            return JsonUtility.ToJson(clipboardCollection, true);
        }
        if (value is Enum) return value.ToString();
        if ((value.GetType().IsPrimitive || value is decimal) && value is IFormattable)
            return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);

        try { return JsonUtility.ToJson(value, true); }
        catch { return value.ToString(); }
    }

    private static bool TryParseClipboardValue(string text, Type type, out object value)
    {
        value = null;
        if (type == null) return false;
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            if (string.IsNullOrEmpty(text)) return true;
            value = AssetDatabase.LoadAssetAtPath(text, type);
            return value != null;
        }
        if (type.IsEnum)
        {
            try
            {
                value = Enum.Parse(type, text, ignoreCase: true);
                return true;
            }
            catch { return false; }
        }
        if (type == typeof(bool) && bool.TryParse(text, out var booleanValue))
        {
            value = booleanValue;
            return true;
        }
        if (type == typeof(char) && text.Length == 1)
        {
            value = text[0];
            return true;
        }
        if (type == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var decimalValue))
        {
            value = decimalValue;
            return true;
        }
        if (type.IsPrimitive)
        {
            try
            {
                value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }
        if (TryGetCollectionElementType(type, out var elementType))
        {
            ClipboardCollection clipboardCollection;
            try { clipboardCollection = JsonUtility.FromJson<ClipboardCollection>(text); }
            catch { return false; }
            if (clipboardCollection?.values == null) return false;

            var items = new List<object>();
            foreach (var itemText in clipboardCollection.values)
            {
                if (!TryParseClipboardValue(itemText, elementType, out var item)) return false;
                items.Add(item);
            }

            if (type.IsArray)
            {
                var array = Array.CreateInstance(elementType, items.Count);
                for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
                value = array;
                return true;
            }

            if (type.IsAbstract || type.IsInterface) return false;
            try
            {
                var collection = (IList)Activator.CreateInstance(type);
                foreach (var item in items) collection.Add(item);
                value = collection;
                return true;
            }
            catch { return false; }
        }

        try
        {
            value = JsonUtility.FromJson(text, type);
            return value != null;
        }
        catch { return false; }
    }

    private static void PasteFieldValue(
        UnityEngine.Object targetObject,
        string valuePropertyPath,
        string overridesPropertyPath,
        string bindingsPropertyPath,
        object presetValue,
        string fieldPath,
        string presetOwnerPath,
        Type fieldType,
        string clipboardText)
    {
        if (targetObject == null || !TryParseClipboardValue(clipboardText, fieldType, out var pastedValue)) return;

        var serializedObject = new SerializedObject(targetObject);
        serializedObject.Update();
        var serializedValue = serializedObject.FindProperty(valuePropertyPath);
        if (serializedValue == null) return;

        var rootValue = CloneCustomValue(LoadCustomValue(serializedValue));
        if (rootValue == null || !SetValueAtPath(rootValue, fieldPath, pastedValue)) return;

        Undo.RecordObject(targetObject, "Paste Dialogue Content Field");
        var localFieldOverrides = string.IsNullOrEmpty(overridesPropertyPath)
            ? null
            : serializedObject.FindProperty(overridesPropertyPath);
        if (presetValue != null && !string.IsNullOrEmpty(presetOwnerPath) && localFieldOverrides != null)
        {
            bool alreadyLocal = Enumerable.Range(0, localFieldOverrides.arraySize)
                .Any(index => localFieldOverrides.GetArrayElementAtIndex(index).stringValue == presetOwnerPath);
            if (!alreadyLocal)
                localFieldOverrides.GetArrayElementAtIndex(localFieldOverrides.arraySize++).stringValue = presetOwnerPath;
        }

        if (!string.IsNullOrEmpty(bindingsPropertyPath))
        {
            var bindings = serializedObject.FindProperty(bindingsPropertyPath);
            if (bindings != null)
            {
                for (int i = bindings.arraySize - 1; i >= 0; i--)
                {
                    if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("fieldPath").stringValue == fieldPath)
                        bindings.DeleteArrayElementAtIndex(i);
                }
            }
        }

        PersistCustomValue(serializedValue, rootValue);
    }

    private static object CloneCustomValue(object source)
    {
        if (source == null) return null;
        var type = source.GetType();
        var objectReferences = CollectObjectReferences(source, type, string.Empty, 0);
        object clone;
        try { clone = JsonUtility.FromJson(JsonUtility.ToJson(source), type); }
        catch { return null; }
        if (clone == null) return null;

        foreach (var reference in objectReferences)
            RestoreObjectReference(clone, reference.Key.Split('.'), 0, reference.Value);
        return clone;
    }

    private static void SetPresetFieldLocal(
        UnityEngine.Object targetObject,
        string valuePropertyPath,
        string overridesPropertyPath,
        object presetValue,
        string path,
        bool isLocal)
    {
        if (targetObject == null) return;

        var serializedObject = new SerializedObject(targetObject);
        serializedObject.Update();
        var serializedValue = serializedObject.FindProperty(valuePropertyPath);
        var localFieldOverrides = serializedObject.FindProperty(overridesPropertyPath);
        if (serializedValue == null || localFieldOverrides == null) return;

        Undo.RecordObject(targetObject,
            isLocal ? "Edit Dialogue Field Locally" : "Use Character Preset Field");

        int existingIndex = -1;
        for (int i = 0; i < localFieldOverrides.arraySize; i++)
        {
            if (localFieldOverrides.GetArrayElementAtIndex(i).stringValue == path)
            {
                existingIndex = i;
                break;
            }
        }

        if (isLocal && existingIndex < 0)
            localFieldOverrides.GetArrayElementAtIndex(localFieldOverrides.arraySize++).stringValue = path;
        else if (!isLocal && existingIndex >= 0)
            localFieldOverrides.DeleteArrayElementAtIndex(existingIndex);

        if (!isLocal)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetObject);
            return;
        }

        var rootValue = LoadCustomValue(serializedValue);
        var presetFieldValue = GetValueAtPath(presetValue, path);
        if (rootValue == null || !SetValueAtPath(rootValue, path, presetFieldValue)) return;
        PersistCustomValue(serializedValue, rootValue);
    }

    private static object LoadCustomValue(SerializedProperty serializedValue)
    {
        var typeName = serializedValue.FindPropertyRelative("customTypeName")?.stringValue;
        var type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
        if (type == null) return null;

        var managedValue = serializedValue.FindPropertyRelative("customManagedValue");
        object value = managedValue?.managedReferenceValue;
        if (value == null || value.GetType() != type)
        {
            var customJson = serializedValue.FindPropertyRelative("customJson")?.stringValue;
            try
            {
                value = string.IsNullOrEmpty(customJson)
                    ? Activator.CreateInstance(type, nonPublic: true)
                    : JsonUtility.FromJson(customJson, type);
            }
            catch
            {
                return null;
            }
        }

        var references = serializedValue.FindPropertyRelative("customObjectReferences");
        if (references != null)
        {
            for (int i = 0; i < references.arraySize; i++)
            {
                var reference = references.GetArrayElementAtIndex(i);
                RestoreObjectReference(value,
                    reference.FindPropertyRelative("fieldPath").stringValue.Split('.'), 0,
                    reference.FindPropertyRelative("value").objectReferenceValue);
            }
        }

        return value;
    }

    private static object GetValueAtPath(object value, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (value == null) return null;
            if (value is IList collection && int.TryParse(segment, out int index) &&
                index >= 0 && index < collection.Count)
            {
                value = collection[index];
                continue;
            }

            var field = value.GetType().GetField(segment,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return null;
            value = field.GetValue(value);
        }

        return value;
    }

    private static bool SetValueAtPath(object target, string path, object value)
    {
        return SetValueAtPath(target, path.Split('.'), 0, value);
    }

    private static bool SetValueAtPath(object target, string[] segments, int index, object value)
    {
        if (target == null || index >= segments.Length) return false;
        if (target is IList collection && int.TryParse(segments[index], out int itemIndex) &&
            itemIndex >= 0 && itemIndex < collection.Count)
        {
            if (index == segments.Length - 1)
            {
                collection[itemIndex] = value;
                return true;
            }

            var item = collection[itemIndex];
            if (!SetValueAtPath(item, segments, index + 1, value)) return false;
            if (item != null && item.GetType().IsValueType)
                collection[itemIndex] = item;
            return true;
        }

        var field = target.GetType().GetField(segments[index],
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return false;
        if (index == segments.Length - 1)
        {
            field.SetValue(target, value);
            return true;
        }

        var nested = field.GetValue(target);
        if (!SetValueAtPath(nested, segments, index + 1, value)) return false;
        if (nested != null && nested.GetType().IsValueType)
            field.SetValue(target, nested);
        return true;
    }

    private static void PersistCustomValue(SerializedProperty serializedValue, object value)
    {
        var managedValue = serializedValue.FindPropertyRelative("customManagedValue");
        if (managedValue != null && !value.GetType().IsValueType)
            managedValue.managedReferenceValue = value;

        var customJson = serializedValue.FindPropertyRelative("customJson");
        if (customJson != null)
            customJson.stringValue = JsonUtility.ToJson(value, true);

        var referencesProperty = serializedValue.FindPropertyRelative("customObjectReferences");
        var references = CollectObjectReferences(value, value.GetType(), string.Empty, 0);
        referencesProperty.arraySize = references.Count;
        int index = 0;
        foreach (var pair in references)
        {
            var reference = referencesProperty.GetArrayElementAtIndex(index++);
            reference.FindPropertyRelative("fieldPath").stringValue = pair.Key;
            reference.FindPropertyRelative("value").objectReferenceValue = pair.Value;
        }

        var serializedObject = serializedValue.serializedObject;
        serializedObject.ApplyModifiedProperties();
        if (serializedObject.targetObject != null)
            EditorUtility.SetDirty(serializedObject.targetObject);
    }

    private static Dictionary<string, UnityEngine.Object> CollectObjectReferences(
        object value,
        Type type,
        string path,
        int depth)
    {
        var references = new Dictionary<string, UnityEngine.Object>();
        CollectObjectReferences(value, type, path, depth, references);
        return references;
    }

    private static void CollectObjectReferences(
        object value,
        Type type,
        string path,
        int depth,
        Dictionary<string, UnityEngine.Object> references)
    {
        if (value == null || type == null || depth >= 8) return;
        if (value is UnityEngine.Object unityObject)
        {
            if (unityObject != null) references[path] = unityObject;
            return;
        }

        if (value is IList list && TryGetCollectionElementType(type, out var elementType))
        {
            for (int i = 0; i < list.Count; i++)
                CollectObjectReferences(list[i], elementType, $"{path}.{i}", depth + 1, references);
            return;
        }

        foreach (var field in GetSerializableFields(type))
        {
            var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
            CollectObjectReferences(field.GetValue(value), field.FieldType, fieldPath, depth + 1, references);
        }
    }

    private static object RestoreObjectReference(object target, string[] path, int index, UnityEngine.Object value)
    {
        if (target == null || index >= path.Length) return target;
        if (target is IList collection && int.TryParse(path[index], out int itemIndex) &&
            itemIndex >= 0 && itemIndex < collection.Count)
        {
            if (index == path.Length - 1)
                collection[itemIndex] = value;
            else
                collection[itemIndex] = RestoreObjectReference(collection[itemIndex], path, index + 1, value);
            return target;
        }
        var field = target.GetType().GetField(path[index], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return target;
        if (index == path.Length - 1)
        {
            field.SetValue(target, value);
            return target;
        }
        var nested = field.GetValue(target);
        nested = RestoreObjectReference(nested, path, index + 1, value);
        field.SetValue(target, nested);
        return target;
    }

    private static void ResetTypedValue(SerializedProperty value, DialogueVariableType type, Type valueType)
    {
        value.FindPropertyRelative("customTypeName").stringValue =
            type is DialogueVariableType.Enum or DialogueVariableType.Custom ? valueType.AssemblyQualifiedName : string.Empty;
        value.FindPropertyRelative("enumValue").intValue = 0;
        value.FindPropertyRelative("objectReferenceValue").objectReferenceValue = null;
        value.FindPropertyRelative("customManagedValue").managedReferenceValue = null;
        value.FindPropertyRelative("customObjectReferences").ClearArray();
        value.FindPropertyRelative("boolValue").boolValue = false;
        value.FindPropertyRelative("byteValue").intValue = 0;
        value.FindPropertyRelative("sbyteValue").intValue = 0;
        value.FindPropertyRelative("shortValue").intValue = 0;
        value.FindPropertyRelative("ushortValue").intValue = 0;
        value.FindPropertyRelative("intValue").intValue = 0;
        value.FindPropertyRelative("uintValue").longValue = 0;
        value.FindPropertyRelative("longValue").longValue = 0;
        value.FindPropertyRelative("ulongValue").longValue = 0;
        value.FindPropertyRelative("floatValue").floatValue = 0;
        value.FindPropertyRelative("doubleValue").doubleValue = 0;
        if (type == DialogueVariableType.String) value.FindPropertyRelative("stringValue").stringValue = string.Empty;
        if (type == DialogueVariableType.Decimal) value.FindPropertyRelative("decimalValue").stringValue = "0";
        if (type == DialogueVariableType.Character) value.FindPropertyRelative("charValue").stringValue = string.Empty;
        if (type == DialogueVariableType.Custom)
            value.FindPropertyRelative("customJson").stringValue = valueType.IsValueType ? "{}" : string.Empty;
    }

    private void MigrateLegacyConditions()
    {
        var link = (DialogueLink)target;
        var dialogue = FindDialogue(link);
        if (dialogue == null || link.conditions == null) return;

        var migrations = new System.Collections.Generic.List<(
            DialogueTransitionCondition condition,
            DialogueExposedVariable expected,
            System.Collections.Generic.List<DialogueExposedVariable> arguments)>();

        foreach (var condition in link.conditions.Where(item => item != null && !item.useTypedValues))
        {
            var variable = dialogue.variables?.FirstOrDefault(item => item != null && item.id == condition.variableId);
            if (variable == null) continue;

            var arguments = new System.Collections.Generic.List<DialogueExposedVariable>();
            Type valueType = variable.GetValueType();
            if (condition.valueSource == DialogueConditionValueSource.Method)
            {
                Type sourceType = GetVariableSourceType(variable);
                var method = sourceType == null ? null : GetCallableMethods(sourceType)
                    .FirstOrDefault(candidate => GetMethodSignature(candidate) == condition.methodSignature);
                if (method == null) continue;

                var parameters = method.GetParameters();
                var legacyArguments = condition.methodArguments ?? new System.Collections.Generic.List<string>();
                if (parameters.Length != legacyArguments.Count) continue;
                var legacyObjects = condition.methodObjectArguments ?? new System.Collections.Generic.List<UnityEngine.Object>();
                bool argumentsValid = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    var objectValue = i < legacyObjects.Count ? legacyObjects[i] : null;
                    if (!TryParseConditionValue(legacyArguments[i], parameters[i].ParameterType, objectValue, out var parsed))
                    {
                        argumentsValid = false;
                        break;
                    }
                    var typedArgument = CreateTypedConditionValue(parsed, parameters[i].ParameterType);
                    if (typedArgument == null)
                    {
                        argumentsValid = false;
                        break;
                    }
                    arguments.Add(typedArgument);
                }
                if (!argumentsValid) continue;
                valueType = method.ReturnType;
            }

            if (!TryParseConditionValue(condition.expectedValue, valueType, condition.expectedObject, out var expectedValue))
                continue;
            var typedExpected = CreateTypedConditionValue(expectedValue, valueType);
            if (typedExpected != null)
                migrations.Add((condition, typedExpected, arguments));
        }

        if (migrations.Count == 0) return;
        Undo.RecordObject(link, "Migrate Dialogue Transition Conditions");
        foreach (var migration in migrations)
        {
            migration.condition.typedExpectedValue = migration.expected;
            migration.condition.typedMethodArguments = migration.arguments;
            migration.condition.useTypedValues = true;
        }
        EditorUtility.SetDirty(link);
    }

    private static Type GetVariableSourceType(DialogueExposedVariable variable)
    {
        var type = variable.GetValueType();
        if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type) && variable.objectReferenceValue != null)
            return variable.objectReferenceValue.GetType();
        return type;
    }

    private static IEnumerable<MethodInfo> GetCallableMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName && !method.IsGenericMethodDefinition &&
                !method.ContainsGenericParameters && IsSupportedConditionValueType(method.ReturnType) &&
                method.GetParameters().All(parameter => !parameter.IsOut && !parameter.ParameterType.IsByRef &&
                    IsSupportedConditionArgumentType(parameter.ParameterType)));
    }

    private static DialogueExposedVariable CreateTypedConditionValue(object value, Type valueType)
    {
        valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        var result = new DialogueExposedVariable
        {
            type = GetDialogueVariableType(valueType),
            customTypeName = valueType.AssemblyQualifiedName
        };
        if (value == null)
        {
            if (result.type == DialogueVariableType.Custom && !valueType.IsValueType)
                result.customJson = string.Empty;
            return result;
        }

        try
        {
            result.SetValue(value);
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseConditionValue(string text, Type type, UnityEngine.Object objectValue, out object value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            value = objectValue;
            return value == null || type.IsInstanceOfType(value);
        }
        if (type == typeof(string))
        {
            value = text;
            return true;
        }
        if (string.IsNullOrEmpty(text) && !type.IsValueType)
        {
            value = null;
            return true;
        }

        try
        {
            if (type.IsEnum)
                value = Enum.Parse(type, text, true);
            else if (type == typeof(char))
            {
                if (text.Length != 1) { value = null; return false; }
                value = text[0];
            }
            else if (type.IsPrimitive || type == typeof(decimal))
                value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            else
                value = JsonUtility.FromJson(text, type);
            return value != null || !type.IsValueType;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static Dialogue FindDialogue(DialogueLink link)
    {
        string path = AssetDatabase.GetAssetPath(link);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Dialogue>().FirstOrDefault();
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        return $"{method.ReturnType.FullName} {method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName))})";
    }
}
#endif