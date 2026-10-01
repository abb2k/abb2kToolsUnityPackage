#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueLink))]
public sealed class DialogueLinkEditor : Editor
{
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
            return !type.IsValueType && type != typeof(string);
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

    private static bool DrawTypedValue(SerializedProperty serializedValue, Type valueType, string label, bool compact = false)
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
                customValueChanged = DrawCustomValue(serializedValue, valueType, label);
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

    private static bool DrawCustomValue(SerializedProperty serializedValue, Type valueType, string label)
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

        if (!valueType.IsValueType)
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
        try
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
        for (int i = 0; i < references.arraySize; i++)
        {
            var reference = references.GetArrayElementAtIndex(i);
            RestoreObjectReference(customValue,
                reference.FindPropertyRelative("fieldPath").stringValue.Split('.'), 0,
                reference.FindPropertyRelative("value").objectReferenceValue);
        }

        EditorGUILayout.LabelField($"{label} ({valueType.Name})", EditorStyles.boldLabel);
        var serializedReferences = new Dictionary<string, UnityEngine.Object>();
        bool changed = false;
        DrawSerializableFields(customValue, valueType, string.Empty, 0, serializedReferences, ref changed);
        if (!changed) return false;

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
        ref bool changed)
    {
        if (value == null || depth >= 5) return;
        foreach (var field in GetSerializableFields(valueType))
        {
            var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
            var currentValue = field.GetValue(value);
            bool fieldChanged = false;
            var updatedValue = DrawSerializableField(field.Name, field.FieldType, currentValue,
                fieldPath, depth, references, ref fieldChanged);
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
        ref bool changed)
    {
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            var currentObject = currentValue as UnityEngine.Object;
            var updatedObject = EditorGUILayout.ObjectField(label, currentObject, type, false);
            if (updatedObject != null) references[path] = updatedObject;
            if (updatedObject != currentObject) changed = true;
            return updatedObject;
        }

        EditorGUI.BeginChangeCheck();
        object result = currentValue;
        if (type == typeof(bool)) result = EditorGUILayout.Toggle(label, currentValue != null && (bool)currentValue);
        else if (type == typeof(string)) result = EditorGUILayout.TextField(label, currentValue as string ?? string.Empty);
        else if (type == typeof(char))
        {
            string text = EditorGUILayout.TextField(label, currentValue?.ToString() ?? string.Empty);
            result = text.Length == 0 ? '\0' : text[0];
        }
        else if (type == typeof(int)) result = EditorGUILayout.IntField(label, currentValue == null ? 0 : (int)currentValue);
        else if (type == typeof(long)) result = EditorGUILayout.LongField(label, currentValue == null ? 0L : (long)currentValue);
        else if (type == typeof(float)) result = EditorGUILayout.FloatField(label, currentValue == null ? 0f : (float)currentValue);
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
        else if (type.IsEnum)
            result = EditorGUILayout.EnumPopup(label, (Enum)(currentValue ?? Enum.ToObject(type, 0)));
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
        else if (type.IsSerializable && depth < 4)
        {
            if (result == null)
            {
                try { result = Activator.CreateInstance(type); }
                catch { EditorGUILayout.LabelField(label, $"({type.Name})"); }
            }
            if (result == null) return null;
            DrawSerializableFields(result, type, path, depth + 1, references, ref changed);
        }
        else
            EditorGUILayout.LabelField(label, currentValue?.ToString() ?? $"({type.Name})");

        if (EditorGUI.EndChangeCheck()) changed = true;
        return result;
    }

    private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
    {
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => !field.IsStatic && !field.IsInitOnly &&
                !field.IsDefined(typeof(NonSerializedAttribute), true) &&
                (field.IsPublic || field.IsDefined(typeof(SerializeField), true)));
    }

    private static object RestoreObjectReference(object target, string[] path, int index, UnityEngine.Object value)
    {
        if (target == null || index >= path.Length) return target;
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