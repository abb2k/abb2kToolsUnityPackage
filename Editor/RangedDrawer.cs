using UnityEngine;
using UnityEditor;

namespace Abb2kTools.Editor
{
    [CustomPropertyDrawer(typeof(Ranged))]
    public class RangedDrawer : PropertyDrawer
    {
        override public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");

            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, label);

            var originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float marginBetweenFields = 4f;
            float halfWidth = (contentRect.width - marginBetweenFields) / 2f;

            var minRect = new Rect(contentRect.x, contentRect.y, halfWidth, contentRect.height);
            var maxRect = new Rect(minRect.xMax + marginBetweenFields, contentRect.y, halfWidth, contentRect.height);

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 30f; 

            EditorGUI.PropertyField(minRect, minProp, new GUIContent("Min"));
            EditorGUI.PropertyField(maxRect, maxProp, new GUIContent("Max"));

            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUI.indentLevel = originalIndent;

            EditorGUI.EndProperty();
        }
    }
}