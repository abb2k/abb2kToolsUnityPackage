using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[Serializable]
public sealed class DialogueContentFieldBinding
{
    public string fieldPath;
    public string variableId;
    public string variableNodeId;
}

[Serializable]
public sealed class DialogueContentValue
{
    public DialogueExposedVariable value = new() { type = DialogueVariableType.Custom };
    public List<DialogueContentFieldBinding> fieldBindings = new();
    public List<string> localPresetFieldOverrides = new();
    public DialogueCharacterPreset characterPreset;
    public DialogueCharacterPresetVariant characterPresetVariant;

    public Type ContentType => value?.GetValueType();

    public object Resolve(Func<string, object> resolveVariable = null)
    {
        if (value == null) return null;
        var clone = Clone();
        var resolved = clone.value?.CreateValue();
        if (resolved == null) return null;

        var presetContent = clone.characterPreset == null
            ? null
            : clone.characterPreset.GetContent(clone.characterPresetVariant);
        if (presetContent?.ContentType == resolved.GetType())
            ApplyPresetOverrides(resolved, presetContent.Resolve(), 0, string.Empty,
                new HashSet<string>(clone.localPresetFieldOverrides ?? new List<string>()));

        if (resolveVariable == null) return resolved;

        foreach (var binding in clone.fieldBindings ?? new List<DialogueContentFieldBinding>())
        {
            if (binding == null || string.IsNullOrEmpty(binding.fieldPath) ||
                string.IsNullOrEmpty(binding.variableId)) continue;
            var variableValue = resolveVariable(binding.variableId);
            SetBoundValue(resolved, binding.fieldPath.Split('.'), 0, variableValue);
        }

        return resolved;
    }

    public void SetType(Type type)
    {
        if (type == null) return;
        object defaultValue;
        try { defaultValue = Activator.CreateInstance(type, nonPublic: true); }
        catch { return; }

        value = new DialogueExposedVariable
        {
            type = DialogueVariableType.Custom,
            customTypeName = type.AssemblyQualifiedName,
            customManagedValue = type.IsValueType ? null : defaultValue,
            customJson = type.IsValueType ? JsonUtility.ToJson(defaultValue, true) : "{}"
        };
        fieldBindings.Clear();
        localPresetFieldOverrides ??= new List<string>();
        localPresetFieldOverrides.Clear();
        characterPreset = null;
        characterPresetVariant = null;
    }

    public DialogueContentValue Clone()
    {
        if (value == null) return new DialogueContentValue();
        var clone = new DialogueContentValue
        {
            value = JsonUtility.FromJson<DialogueExposedVariable>(JsonUtility.ToJson(value))
        };
        clone.value ??= new DialogueExposedVariable();
        clone.value.objectReferenceValue = value.objectReferenceValue;
        clone.value.customObjectReferences = CloneObjectReferences(value.customObjectReferences);
        clone.value.customManagedValue = CloneManagedValue(value.customManagedValue);
        clone.characterPreset = characterPreset;
        clone.characterPresetVariant = characterPresetVariant;
        clone.localPresetFieldOverrides = new List<string>(localPresetFieldOverrides ?? new List<string>());
        clone.fieldBindings = new List<DialogueContentFieldBinding>();
        foreach (var binding in fieldBindings ?? new List<DialogueContentFieldBinding>())
        {
            if (binding == null) continue;
            clone.fieldBindings.Add(new DialogueContentFieldBinding
            {
                fieldPath = binding.fieldPath,
                variableId = binding.variableId,
                variableNodeId = binding.variableNodeId
            });
        }

        return clone;
    }

    private static void ApplyPresetOverrides(
        object target,
        object preset,
        int depth,
        string path,
        HashSet<string> localFieldOverrides)
    {
        if (target == null || preset == null || depth >= 8 || target.GetType() != preset.GetType() ||
            typeof(UnityEngine.Object).IsAssignableFrom(target.GetType())) return;

        for (var type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized ||
                    !(field.IsPublic || field.IsDefined(typeof(SerializeField), true))) continue;

                var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";
                var presetValue = field.GetValue(preset);
                if (field.IsDefined(typeof(DialoguePresetOverrideAttribute), true))
                {
                    if (!localFieldOverrides.Contains(fieldPath))
                        field.SetValue(target, presetValue);
                    continue;
                }

                var targetValue = field.GetValue(target);
                if (targetValue is IList targetList && presetValue is IList presetList)
                {
                    int count = Math.Min(targetList.Count, presetList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var targetItem = targetList[i];
                        ApplyPresetOverrides(targetItem, presetList[i], depth + 1,
                            $"{fieldPath}.{i}", localFieldOverrides);
                        if (targetItem != null && targetItem.GetType().IsValueType)
                            targetList[i] = targetItem;
                    }
                }
                else if (presetValue != null && field.FieldType.IsSerializable &&
                    !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    if (targetValue == null)
                    {
                        try { targetValue = Activator.CreateInstance(field.FieldType, nonPublic: true); }
                        catch { continue; }
                    }
                    ApplyPresetOverrides(targetValue, presetValue, depth + 1, fieldPath, localFieldOverrides);
                    if (field.FieldType.IsValueType)
                        field.SetValue(target, targetValue);
                }
            }
        }
    }

    private static object SetBoundValue(object target, string[] path, int index, object value)
    {
        if (target == null || index >= path.Length) return target;
        if (target is IList collection && int.TryParse(path[index], out int itemIndex) &&
            itemIndex >= 0 && itemIndex < collection.Count)
        {
            if (index == path.Length - 1)
            {
                if (CanAssign(GetCollectionElementType(collection), value))
                    collection[itemIndex] = value;
            }
            else
                collection[itemIndex] = SetBoundValue(collection[itemIndex], path, index + 1, value);
            return target;
        }

        var field = target.GetType().GetField(path[index], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return target;
        if (index == path.Length - 1)
        {
            if (CanAssign(field.FieldType, value)) field.SetValue(target, value);
            return target;
        }

        var nested = SetBoundValue(field.GetValue(target), path, index + 1, value);
        field.SetValue(target, nested);
        return target;
    }

    private static bool CanAssign(Type targetType, object value)
    {
        if (targetType == null) return value != null;
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return value == null
            ? !targetType.IsValueType
            : targetType.IsInstanceOfType(value);
    }

    private static Type GetCollectionElementType(IList collection)
    {
        var collectionType = collection.GetType();
        if (collectionType.IsArray) return collectionType.GetElementType();
        return collectionType.IsGenericType ? collectionType.GetGenericArguments()[0] : typeof(object);
    }

    private static object CloneManagedValue(object source)
    {
        if (source == null) return null;
        var type = source.GetType();
        object clone;
        try { clone = JsonUtility.FromJson(JsonUtility.ToJson(source), type); }
        catch { return source; }
        CopyUnityObjectReferences(source, clone, 0);
        return clone;
    }

    private static List<DialogueCustomObjectReference> CloneObjectReferences(
        List<DialogueCustomObjectReference> references)
    {
        var result = new List<DialogueCustomObjectReference>();
        foreach (var reference in references ?? new List<DialogueCustomObjectReference>())
        {
            if (reference == null) continue;
            result.Add(new DialogueCustomObjectReference
            {
                fieldPath = reference.fieldPath,
                value = reference.value
            });
        }
        return result;
    }

    private static void CopyUnityObjectReferences(object source, object destination, int depth)
    {
        if (source == null || destination == null || depth >= 8) return;
        var type = source.GetType();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized) continue;
            var sourceValue = field.GetValue(source);
            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                field.SetValue(destination, sourceValue);
                continue;
            }

            if (sourceValue is IList sourceList && field.GetValue(destination) is IList destinationList)
            {
                int count = Math.Min(sourceList.Count, destinationList.Count);
                for (int i = 0; i < count; i++)
                {
                    var destinationItem = destinationList[i];
                    CopyUnityObjectReferences(sourceList[i], destinationItem, depth + 1);
                    if (destinationItem != null && destinationItem.GetType().IsValueType)
                        destinationList[i] = destinationItem;
                }
                continue;
            }

            if (sourceValue != null && field.FieldType.IsSerializable &&
                field.GetValue(destination) is object destinationValue)
            {
                CopyUnityObjectReferences(sourceValue, destinationValue, depth + 1);
                if (field.FieldType.IsValueType)
                    field.SetValue(destination, destinationValue);
            }
        }
    }
}