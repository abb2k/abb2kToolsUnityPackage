using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Executes one step of a Dialogue graph and reports every destination reached by an exec pin.
public sealed class DialogueRunner
{
    private readonly Dialogue _dialogue;
    private readonly Dictionary<string, object> _variables = new();

    public DialogueData CurrentNode { get; private set; }
    public object CurrentContent => CurrentNode?.ResolveContent(ResolveVariable);
    public bool IsExited { get; private set; }

    public event Action<DialogueData> NodeEntered;
    public event Action<IReadOnlyList<DialogueSelectionOptionData>> SelectionRequested;
    public event Action DialogueExited;
    public event Action<string, object> VariableChanged;

    public DialogueRunner(Dialogue dialogue)
    {
        _dialogue = dialogue;
        foreach (var variable in dialogue?.variables ?? new List<DialogueExposedVariable>())
        {
            if (variable == null || string.IsNullOrEmpty(variable.id)) continue;
            _variables[variable.id] = variable.CreateValue();
        }
    }

    public T GetVariable<T>(string variableNameOrId)
    {
        if (!TryGetVariable<T>(variableNameOrId, out var value))
            throw new KeyNotFoundException($"Dialogue variable '{variableNameOrId}' does not exist or is not of type {typeof(T).Name}.");
        return value;
    }

    public bool TryGetVariable<T>(string variableNameOrId, out T value)
    {
        if (TryFindVariable(variableNameOrId, out var definition) &&
            _variables.TryGetValue(definition.id, out var storedValue))
        {
            if (storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            var definitionType = definition.GetValueType();
            if (storedValue == null && default(T) is null && definitionType != null &&
                (definitionType.IsAssignableFrom(typeof(T)) || typeof(T).IsAssignableFrom(definitionType)))
            {
                value = default;
                return true;
            }
        }

        value = default;
        return false;
    }

    public void SetVariable<T>(string variableNameOrId, T value)
    {
        TryFindVariable(variableNameOrId, out var definition);
        if (definition == null)
            throw new KeyNotFoundException($"Dialogue variable '{variableNameOrId}' does not exist.");
        if (!definition.AcceptsValue(value))
            throw new ArgumentException($"Value is not compatible with dialogue variable '{definition.name}'.", nameof(value));

        _variables[definition.id] = value;
        VariableChanged?.Invoke(definition.name, value);
    }

    public object GetOptionContent(string optionId)
    {
        var option = CurrentNode?.selectionOptions?.FirstOrDefault(item => item != null && item.id == optionId);
        return option?.ResolveContent(ResolveVariable);
    }

    private object ResolveVariable(string variableId)
    {
        return _variables.TryGetValue(variableId, out var value) ? value : null;
    }

    private bool TryFindVariable(string nameOrId, out DialogueExposedVariable definition)
    {
        definition = (_dialogue?.variables ?? new List<DialogueExposedVariable>())
            .Find(variable => variable != null &&
                (variable.id == nameOrId || variable.name == nameOrId));
        return definition != null;
    }

    public IReadOnlyList<DialogueData> Start()
    {
        if (_dialogue == null || _dialogue.startOutput == null) return Array.Empty<DialogueData>();

        CurrentNode = null;
        IsExited = false;
        return ExecuteOutput(_dialogue.startOutput.id, fromStart: true);
    }

    public IReadOnlyList<DialogueData> Start(string entryNameOrId)
    {
        var entry = (_dialogue?.additionalEntries ?? new List<DialogueEntryData>())
            .FirstOrDefault(item => item != null && (item.id == entryNameOrId || item.name == entryNameOrId));
        if (entry?.output == null) return Array.Empty<DialogueData>();

        CurrentNode = null;
        IsExited = false;
        return ExecuteOutput(entry.output.id, fromStart: true, entryId: entry.id);
    }

    // Continue through the node's default output. Selection nodes must use ChooseOption instead.
    public IReadOnlyList<DialogueData> Continue()
    {
        if (IsExited || CurrentNode == null || CurrentNode.hasSelection || CurrentNode.execOutput == null)
            return Array.Empty<DialogueData>();

        return ExecuteOutput(CurrentNode.execOutput.id, fromStart: false);
    }

    public IReadOnlyList<DialogueData> ChooseOption(string optionId)
    {
        if (IsExited || CurrentNode == null || !CurrentNode.hasSelection ||
            CurrentNode.selectionOptions == null ||
            !CurrentNode.selectionOptions.Any(option => option != null && option.id == optionId))
            return Array.Empty<DialogueData>();

        return ExecuteOutput(optionId, fromStart: false);
    }

    private IReadOnlyList<DialogueData> ExecuteOutput(string portId, bool fromStart, string entryId = null)
    {
        var destinations = new List<DialogueData>();
        foreach (var link in GetLinks())
        {
            if (!IsFromOutput(link, portId, fromStart, entryId)) continue;
            if (!ConditionsAreMet(link)) continue;

            if (link.destinationIsDialogueExit || !string.IsNullOrEmpty(link.destinationExitId))
            {
                IsExited = true;
                CurrentNode = null;
                DialogueExited?.Invoke();
                continue;
            }

            if (link.destination == null) continue;

            CurrentNode = link.destination;
            destinations.Add(CurrentNode);
            NodeEntered?.Invoke(CurrentNode);
            if (CurrentNode.hasSelection)
            {
                IReadOnlyList<DialogueSelectionOptionData> options = CurrentNode.selectionOptions ?? new List<DialogueSelectionOptionData>();
                SelectionRequested?.Invoke(options);
            }
        }

        return destinations;
    }

    private bool ConditionsAreMet(DialogueLink link)
    {
        foreach (var condition in link.conditions ?? new List<DialogueTransitionCondition>())
        {
            if (condition == null || !TryEvaluateCondition(condition))
                return false;
        }

        return true;
    }

    private bool TryEvaluateCondition(DialogueTransitionCondition condition)
    {
        var definition = (_dialogue?.variables ?? new List<DialogueExposedVariable>())
            .FirstOrDefault(variable => variable != null && variable.id == condition.variableId);
        if (definition == null || !_variables.TryGetValue(definition.id, out var sourceValue))
            return false;

        object value = sourceValue;
        if (condition.valueSource == DialogueConditionValueSource.Method)
        {
            if (sourceValue == null || string.IsNullOrEmpty(condition.methodSignature)) return false;

            var method = GetCallableMethods(sourceValue.GetType())
                .FirstOrDefault(candidate => GetMethodSignature(candidate) == condition.methodSignature);
            if (method == null) return false;

            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            if (condition.useTypedValues)
            {
                var typedArguments = condition.typedMethodArguments ?? new List<DialogueExposedVariable>();
                if (parameters.Length != typedArguments.Count) return false;
                for (int i = 0; i < parameters.Length; i++)
                {
                    arguments[i] = typedArguments[i]?.CreateValue();
                    var parameterType = Nullable.GetUnderlyingType(parameters[i].ParameterType) ?? parameters[i].ParameterType;
                    if (arguments[i] == null
                        ? parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null
                        : !parameterType.IsInstanceOfType(arguments[i]))
                        return false;
                }
            }
            else
            {
                var argumentTexts = condition.methodArguments ?? new List<string>();
                if (parameters.Length != argumentTexts.Count) return false;
                var objectArguments = condition.methodObjectArguments ?? new List<UnityEngine.Object>();
                for (int i = 0; i < parameters.Length; i++)
                {
                    var objectArgument = i < objectArguments.Count ? objectArguments[i] : null;
                    if (!TryConvertConditionValue(argumentTexts[i], parameters[i].ParameterType, objectArgument, out arguments[i]))
                        return false;
                }
            }

            try
            {
                value = method.Invoke(sourceValue, arguments);
            }
            catch
            {
                return false;
            }
        }

        object expected;
        if (condition.useTypedValues)
        {
            var typedExpected = condition.typedExpectedValue;
            if (typedExpected == null) return false;
            expected = typedExpected.CreateValue();
        }
        else
        {
            var valueType = value?.GetType() ??
                (condition.valueSource == DialogueConditionValueSource.Variable ? definition.GetValueType() : null);
            if (valueType == null || !TryConvertConditionValue(condition.expectedValue, valueType, condition.expectedObject, out expected))
                return false;
        }

        try
        {
            switch (condition.comparison)
            {
                case DialogueConditionComparison.Equal:
                    return ValuesEqual(value, expected);
                case DialogueConditionComparison.NotEqual:
                    return !ValuesEqual(value, expected);
                case DialogueConditionComparison.Greater:
                case DialogueConditionComparison.GreaterOrEqual:
                case DialogueConditionComparison.Less:
                case DialogueConditionComparison.LessOrEqual:
                    if (value is bool || value is not IComparable comparable) return false;
                    int comparison = comparable.CompareTo(expected);
                    return condition.comparison switch
                    {
                        DialogueConditionComparison.Greater => comparison > 0,
                        DialogueConditionComparison.GreaterOrEqual => comparison >= 0,
                        DialogueConditionComparison.Less => comparison < 0,
                        _ => comparison <= 0
                    };
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool ValuesEqual(object first, object second)
    {
        if (first is UnityEngine.Object || second is UnityEngine.Object)
            return first as UnityEngine.Object == second as UnityEngine.Object;
        return Equals(first, second);
    }

    private static IEnumerable<MethodInfo> GetCallableMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName && !method.IsGenericMethodDefinition &&
                !method.ContainsGenericParameters && IsSupportedConditionValueType(method.ReturnType) &&
                method.GetParameters().All(parameter => !parameter.IsOut && !parameter.ParameterType.IsByRef &&
                    IsSupportedConditionArgumentType(parameter.ParameterType)));
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

    private static string GetMethodSignature(MethodInfo method)
    {
        return $"{method.ReturnType.FullName} {method.Name}({string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName))})";
    }

    private static bool TryConvertConditionValue(string text, Type targetType, UnityEngine.Object objectValue, out object value)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        if (nullableType != null) targetType = nullableType;

        if (objectValue != null && targetType.IsInstanceOfType(objectValue))
        {
            value = objectValue;
            return true;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
        {
            value = objectValue;
            return value == null || targetType.IsInstanceOfType(value);
        }

        if (targetType == typeof(string))
        {
            value = text;
            return true;
        }

        if (string.IsNullOrEmpty(text) && !targetType.IsValueType)
        {
            value = null;
            return true;
        }

        try
        {
            if (targetType.IsEnum)
                value = Enum.Parse(targetType, text, ignoreCase: true);
            else if (targetType == typeof(char))
            {
                if (text.Length != 1) { value = null; return false; }
                value = text[0];
            }
            else if (targetType.IsPrimitive || targetType == typeof(decimal))
                value = Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
            else
                value = JsonUtility.FromJson(text, targetType);

            return value != null || !targetType.IsValueType;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private bool IsFromOutput(DialogueLink link, string portId, bool fromStart, string entryId)
    {
        if (fromStart)
            return string.IsNullOrEmpty(entryId)
                ? link.sourceIsDialogueStart && link.sourcePortId == portId
                : link.sourceEntryId == entryId && link.sourcePortId == portId;

        if (link.sourceIsDialogueStart || !string.IsNullOrEmpty(link.sourceEntryId) || CurrentNode == null) return false;

        bool sourceMatches = link.source == CurrentNode ||
            (link.source == null && CurrentNode.transitions != null && CurrentNode.transitions.Contains(link));
        string outputId = string.IsNullOrEmpty(link.sourcePortId)
            ? CurrentNode.execOutput?.id
            : link.sourcePortId;
        return sourceMatches && outputId == portId;
    }

    private IEnumerable<DialogueLink> GetLinks()
    {
        var links = new List<DialogueLink>();
        var seen = new HashSet<DialogueLink>();
        foreach (var link in _dialogue.links ?? Array.Empty<DialogueLink>())
        {
            if (link != null && seen.Add(link))
                links.Add(link);
        }

        foreach (var room in _dialogue.rooms ?? Array.Empty<DialogueData>())
        {
            if (room?.transitions == null) continue;
            foreach (var link in room.transitions)
            {
                if (link != null && seen.Add(link))
                    links.Add(link);
            }
        }

        return links;
    }
}