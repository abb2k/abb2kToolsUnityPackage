using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

public enum DialogueVariableType
{
    Boolean,
    Byte,
    SByte,
    Short,
    UShort,
    Integer,
    UInteger,
    Long,
    ULong,
    Float,
    Double,
    Decimal,
    Character,
    String,
    Vector2,
    Vector2Int,
    Vector3,
    Vector3Int,
    Vector4,
    Quaternion,
    Color,
    Color32,
    Rect,
    RectInt,
    Bounds,
    BoundsInt,
    Matrix4x4,
    LayerMask,
    AnimationCurve,
    Gradient,
    Enum,
    ObjectReference,
    Custom
}

[Serializable]
public class DialogueCustomObjectReference
{
    public string fieldPath;
    public UnityEngine.Object value;
}

[Serializable]
public class DialogueExposedVariable
{
    public string id = Guid.NewGuid().ToString("N");
    public string name = "New Variable";
    public DialogueVariableType type;
    public bool boolValue;
    public byte byteValue;
    public sbyte sbyteValue;
    public short shortValue;
    public ushort ushortValue;
    public int intValue;
    public uint uintValue;
    public long longValue;
    public ulong ulongValue;
    public float floatValue;
    public double doubleValue;
    public string decimalValue = "0";
    public string charValue = "a";
    public string stringValue;
    public Vector2 vector2Value;
    public Vector2Int vector2IntValue;
    public Vector3 vector3Value;
    public Vector3Int vector3IntValue;
    public Vector4 vector4Value;
    public Quaternion quaternionValue = Quaternion.identity;
    public Color colorValue = Color.white;
    public Color32 color32Value = Color.white;
    public Rect rectValue;
    public RectInt rectIntValue;
    public Bounds boundsValue;
    public BoundsInt boundsIntValue;
    public Matrix4x4 matrix4x4Value = Matrix4x4.identity;
    public LayerMask layerMaskValue;
    public AnimationCurve animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
    public Gradient gradientValue = new();
    public string customTypeName;
    [SerializeReference] public object customManagedValue;
    public int enumValue;
    public UnityEngine.Object objectReferenceValue;
    public List<DialogueCustomObjectReference> customObjectReferences = new();
    [TextArea(2, 8)] public string customJson = "{}";

    public Type GetValueType()
    {
        switch (type)
        {
            case DialogueVariableType.Boolean: return typeof(bool);
            case DialogueVariableType.Byte: return typeof(byte);
            case DialogueVariableType.SByte: return typeof(sbyte);
            case DialogueVariableType.Short: return typeof(short);
            case DialogueVariableType.UShort: return typeof(ushort);
            case DialogueVariableType.Integer: return typeof(int);
            case DialogueVariableType.UInteger: return typeof(uint);
            case DialogueVariableType.Long: return typeof(long);
            case DialogueVariableType.ULong: return typeof(ulong);
            case DialogueVariableType.Float: return typeof(float);
            case DialogueVariableType.Double: return typeof(double);
            case DialogueVariableType.Decimal: return typeof(decimal);
            case DialogueVariableType.Character: return typeof(char);
            case DialogueVariableType.String: return typeof(string);
            case DialogueVariableType.Vector2: return typeof(Vector2);
            case DialogueVariableType.Vector2Int: return typeof(Vector2Int);
            case DialogueVariableType.Vector3: return typeof(Vector3);
            case DialogueVariableType.Vector3Int: return typeof(Vector3Int);
            case DialogueVariableType.Vector4: return typeof(Vector4);
            case DialogueVariableType.Quaternion: return typeof(Quaternion);
            case DialogueVariableType.Color: return typeof(Color);
            case DialogueVariableType.Color32: return typeof(Color32);
            case DialogueVariableType.Rect: return typeof(Rect);
            case DialogueVariableType.RectInt: return typeof(RectInt);
            case DialogueVariableType.Bounds: return typeof(Bounds);
            case DialogueVariableType.BoundsInt: return typeof(BoundsInt);
            case DialogueVariableType.Matrix4x4: return typeof(Matrix4x4);
            case DialogueVariableType.LayerMask: return typeof(LayerMask);
            case DialogueVariableType.AnimationCurve: return typeof(AnimationCurve);
            case DialogueVariableType.Gradient: return typeof(Gradient);
            case DialogueVariableType.Enum:
            case DialogueVariableType.Custom:
                return string.IsNullOrEmpty(customTypeName) ? null : Type.GetType(customTypeName);
            case DialogueVariableType.ObjectReference: return typeof(UnityEngine.Object);
            default: return null;
        }
    }

    public object CreateValue()
    {
        switch (type)
        {
            case DialogueVariableType.Boolean: return boolValue;
            case DialogueVariableType.Byte: return byteValue;
            case DialogueVariableType.SByte: return sbyteValue;
            case DialogueVariableType.Short: return shortValue;
            case DialogueVariableType.UShort: return ushortValue;
            case DialogueVariableType.Integer: return intValue;
            case DialogueVariableType.UInteger: return uintValue;
            case DialogueVariableType.Long: return longValue;
            case DialogueVariableType.ULong: return ulongValue;
            case DialogueVariableType.Float: return floatValue;
            case DialogueVariableType.Double: return doubleValue;
            case DialogueVariableType.Decimal:
                return decimal.TryParse(decimalValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalResult)
                    ? decimalResult : 0m;
            case DialogueVariableType.Character: return string.IsNullOrEmpty(charValue) ? '\0' : charValue[0];
            case DialogueVariableType.String: return stringValue;
            case DialogueVariableType.Vector2: return vector2Value;
            case DialogueVariableType.Vector2Int: return vector2IntValue;
            case DialogueVariableType.Vector3: return vector3Value;
            case DialogueVariableType.Vector3Int: return vector3IntValue;
            case DialogueVariableType.Vector4: return vector4Value;
            case DialogueVariableType.Quaternion: return quaternionValue;
            case DialogueVariableType.Color: return colorValue;
            case DialogueVariableType.Color32: return color32Value;
            case DialogueVariableType.Rect: return rectValue;
            case DialogueVariableType.RectInt: return rectIntValue;
            case DialogueVariableType.Bounds: return boundsValue;
            case DialogueVariableType.BoundsInt: return boundsIntValue;
            case DialogueVariableType.Matrix4x4: return matrix4x4Value;
            case DialogueVariableType.LayerMask: return layerMaskValue;
            case DialogueVariableType.AnimationCurve:
                return animationCurveValue == null ? new AnimationCurve() : new AnimationCurve(animationCurveValue.keys)
                {
                    preWrapMode = animationCurveValue.preWrapMode,
                    postWrapMode = animationCurveValue.postWrapMode
                };
            case DialogueVariableType.Gradient:
            {
                var gradient = new Gradient();
                if (gradientValue != null)
                {
                    gradient.SetKeys(gradientValue.colorKeys, gradientValue.alphaKeys);
                    gradient.mode = gradientValue.mode;
                }
                return gradient;
            }
            case DialogueVariableType.Enum:
            {
                var enumType = GetValueType();
                return enumType != null && enumType.IsEnum ? Enum.ToObject(enumType, enumValue) : null;
            }
            case DialogueVariableType.ObjectReference: return objectReferenceValue;
            case DialogueVariableType.Custom:
            {
                var valueType = GetValueType();
                if (valueType == null) return null;
                if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
                    return objectReferenceValue;
                if (!valueType.IsValueType && customManagedValue != null)
                    return customManagedValue;
                if (string.IsNullOrEmpty(customJson)) return valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                try
                {
                    var customValue = JsonUtility.FromJson(customJson, valueType);
                    foreach (var reference in customObjectReferences ?? new List<DialogueCustomObjectReference>())
                    {
                        if (reference == null || string.IsNullOrEmpty(reference.fieldPath)) continue;
                        customValue = SetObjectReference(customValue, reference.fieldPath.Split('.'), 0, reference.value);
                    }
                    return customValue;
                }
                catch
                {
                    return valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                }
            }
            default: return null;
        }
    }

    private static object SetObjectReference(object target, string[] path, int index, UnityEngine.Object value)
    {
        if (target == null || index >= path.Length) return target;
        if (target is IList collection && int.TryParse(path[index], out var itemIndex) &&
            itemIndex >= 0 && itemIndex < collection.Count)
        {
            if (index == path.Length - 1)
                collection[itemIndex] = value;
            else
                collection[itemIndex] = SetObjectReference(collection[itemIndex], path, index + 1, value);
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
        nested = SetObjectReference(nested, path, index + 1, value);
        field.SetValue(target, nested);
        return target;
    }

    public bool AcceptsValue(object value)
    {
        var valueType = GetValueType();
        if (valueType == null) return false;
        if (value == null) return !valueType.IsValueType;
        return valueType.IsInstanceOfType(value);
    }

    public void SetValue(object value)
    {
        if (!AcceptsValue(value))
            throw new ArgumentException($"Value is not compatible with variable '{name}'.", nameof(value));

        switch (type)
        {
            case DialogueVariableType.Boolean: boolValue = (bool)value; break;
            case DialogueVariableType.Byte: byteValue = (byte)value; break;
            case DialogueVariableType.SByte: sbyteValue = (sbyte)value; break;
            case DialogueVariableType.Short: shortValue = (short)value; break;
            case DialogueVariableType.UShort: ushortValue = (ushort)value; break;
            case DialogueVariableType.Integer: intValue = (int)value; break;
            case DialogueVariableType.UInteger: uintValue = (uint)value; break;
            case DialogueVariableType.Long: longValue = (long)value; break;
            case DialogueVariableType.ULong: ulongValue = (ulong)value; break;
            case DialogueVariableType.Float: floatValue = (float)value; break;
            case DialogueVariableType.Double: doubleValue = (double)value; break;
            case DialogueVariableType.Decimal: decimalValue = ((decimal)value).ToString(CultureInfo.InvariantCulture); break;
            case DialogueVariableType.Character: charValue = value.ToString(); break;
            case DialogueVariableType.String: stringValue = (string)value; break;
            case DialogueVariableType.Vector2: vector2Value = (Vector2)value; break;
            case DialogueVariableType.Vector2Int: vector2IntValue = (Vector2Int)value; break;
            case DialogueVariableType.Vector3: vector3Value = (Vector3)value; break;
            case DialogueVariableType.Vector3Int: vector3IntValue = (Vector3Int)value; break;
            case DialogueVariableType.Vector4: vector4Value = (Vector4)value; break;
            case DialogueVariableType.Quaternion: quaternionValue = (Quaternion)value; break;
            case DialogueVariableType.Color: colorValue = (Color)value; break;
            case DialogueVariableType.Color32: color32Value = (Color32)value; break;
            case DialogueVariableType.Rect: rectValue = (Rect)value; break;
            case DialogueVariableType.RectInt: rectIntValue = (RectInt)value; break;
            case DialogueVariableType.Bounds: boundsValue = (Bounds)value; break;
            case DialogueVariableType.BoundsInt: boundsIntValue = (BoundsInt)value; break;
            case DialogueVariableType.Matrix4x4: matrix4x4Value = (Matrix4x4)value; break;
            case DialogueVariableType.LayerMask: layerMaskValue = (LayerMask)value; break;
            case DialogueVariableType.AnimationCurve: animationCurveValue = (AnimationCurve)value; break;
            case DialogueVariableType.Gradient: gradientValue = (Gradient)value; break;
            case DialogueVariableType.Enum: enumValue = Convert.ToInt32(value, CultureInfo.InvariantCulture); break;
            case DialogueVariableType.ObjectReference: objectReferenceValue = (UnityEngine.Object)value; break;
            case DialogueVariableType.Custom:
                if (value is UnityEngine.Object unityObject)
                    objectReferenceValue = unityObject;
                else if (value != null && !value.GetType().IsValueType)
                    customManagedValue = value;
                else
                    customJson = JsonUtility.ToJson(value, true);
                break;
        }
    }
}