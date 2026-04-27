using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Abb2kTools {
    [CustomPropertyDrawer(typeof(Ranged))]
    public class RangedDrawer : PropertyDrawer
    {
        override public void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var minProp = property.FindPropertyRelative("min");
            var maxProp = property.FindPropertyRelative("max");

            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, label);

            float marginBetweenFields = 4f;

            float labelWidth = 30f;
            float fieldWidth = (contentRect.width - 2 * labelWidth) / 2 - marginBetweenFields;

            var minLabelRect = new Rect(contentRect.x, contentRect.y, labelWidth, contentRect.height);
            EditorGUI.LabelField(minLabelRect, "Min");

            var minFieldRect = new Rect(minLabelRect.xMax, contentRect.y, fieldWidth, contentRect.height);
            EditorGUI.PropertyField(minFieldRect, minProp, GUIContent.none);

            var maxLabelRect = new Rect(minFieldRect.xMax + marginBetweenFields, contentRect.y, labelWidth, contentRect.height);
            EditorGUI.LabelField(maxLabelRect, "Max");

            var maxFieldRect = new Rect(maxLabelRect.xMax + marginBetweenFields, contentRect.y, fieldWidth, contentRect.height);
            EditorGUI.PropertyField(maxFieldRect, maxProp, GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}