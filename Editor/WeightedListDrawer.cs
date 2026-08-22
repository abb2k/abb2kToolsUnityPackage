#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Abb2kTools.Collections.Editor
{
    [CustomPropertyDrawer(typeof(WeightedList<>))]
    public class WeightedListDrawer : PropertyDrawer
    {
        private readonly Dictionary<string, ReorderableList> lists = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            return GetList(property).GetHeight() + EditorGUIUtility.singleLineHeight + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = EditorGUIUtility.singleLineHeight;

            // Calculate total weight for the header display
            SerializedProperty elements = property.FindPropertyRelative("elements");
            float total = 0f;
            if (elements != null)
            {
                for (int i = 0; i < elements.arraySize; i++)
                    total += elements.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").floatValue;
            }

            string labelText = $"{label.text} (Total Weight: {total:0.##})";

            // Draw clean main foldout header
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, labelText, true);

            if (!property.isExpanded)
                return;

            // Draw the reorderable list right below the foldout title line
            position.y += EditorGUIUtility.singleLineHeight + 4f;
            position.height = GetList(property).GetHeight();

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            GetList(property).DoList(position);

            EditorGUI.indentLevel = oldIndent;
        }

        private ReorderableList GetList(SerializedProperty property)
        {
            if (lists.TryGetValue(property.propertyPath, out var list))
                return list;

            SerializedProperty elements = property.FindPropertyRelative("elements");

            // Pass false for 'displayHeader' since we handle the foldout header cleanly above
            list = new ReorderableList(property.serializedObject, elements, true, false, true, true);

            list.elementHeightCallback = index =>
            {
                SerializedProperty entry = elements.GetArrayElementAtIndex(index);
                SerializedProperty value = entry.FindPropertyRelative("element");

                float valueHeight = EditorGUI.GetPropertyHeight(value, true);
                float sliderHeight = EditorGUIUtility.singleLineHeight;
                float helpBoxHeight = EditorGUIUtility.singleLineHeight + 4;
                float spacing = 12f;

                return valueHeight + sliderHeight + helpBoxHeight + spacing;
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty entry = elements.GetArrayElementAtIndex(index);
                SerializedProperty value = entry.FindPropertyRelative("element");
                SerializedProperty weight = entry.FindPropertyRelative("_weight");

                rect.y += 4;
                
                const float leftIndentOffset = 10f;
                const float rightPadding = 12f;

                int oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                Rect adjustedRect = new Rect(
                    rect.x + leftIndentOffset, 
                    rect.y, 
                    rect.width - leftIndentOffset - rightPadding, 
                    rect.height);

                string elementTitle = GetElementTitle(value, index);

                // 1. Draw the element with the dynamic title label
                float valueHeight = EditorGUI.GetPropertyHeight(value, true);
                Rect valueRect = new Rect(adjustedRect.x, adjustedRect.y, adjustedRect.width, valueHeight);
                
                GUIContent elementLabel = new GUIContent(elementTitle);
                EditorGUI.PropertyField(valueRect, value, elementLabel, true);

                // 2. Draw the weight slider right below the element
                Rect weightRect = new Rect(
                    adjustedRect.x,
                    valueRect.yMax + 4,
                    adjustedRect.width,
                    EditorGUIUtility.singleLineHeight);

                weight.floatValue = EditorGUI.Slider(weightRect, "Weight", weight.floatValue, 0f, 100f);

                // 3. Calculate and display calculated chance info box
                float total = 0f;
                for (int i = 0; i < elements.arraySize; i++)
                    total += elements.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").floatValue;

                float chance = total > 0 ? weight.floatValue / total * 100f : 0;

                Rect chanceRect = new Rect(
                    adjustedRect.x,
                    weightRect.yMax + 3,
                    adjustedRect.width,
                    EditorGUIUtility.singleLineHeight);

                EditorGUI.HelpBox(chanceRect, $"Chance: {chance:0.0}%    Weight: {weight.floatValue:0.##}", MessageType.None);

                EditorGUI.indentLevel = oldIndent;
            };

            lists[property.propertyPath] = list;
            return list;
        }

        private static string GetElementTitle(SerializedProperty value, int index)
        {
            SerializedProperty first = value.Copy();
            SerializedProperty end = value.GetEndProperty();

            if (first.NextVisible(true) && !SerializedProperty.EqualContents(first, end))
            {
                if (first.propertyType == SerializedPropertyType.String && !string.IsNullOrEmpty(first.stringValue))
                {
                    return first.stringValue;
                }
            }

            return $"Element {index}";
        }
    }
}

#endif