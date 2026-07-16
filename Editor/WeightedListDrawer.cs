#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Abb2kTools
{
    [CustomPropertyDrawer(typeof(WeightedList<>))]
    public class WeightedListDrawer : PropertyDrawer
    {
        private const float BoxPadding = 6f;
        private const float HeaderHeight = 18f;

        private readonly Dictionary<string, ReorderableList> lists = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetList(property).GetHeight();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GetList(property).DoList(position);
        }

        private ReorderableList GetList(SerializedProperty property)
        {
            if (lists.TryGetValue(property.propertyPath, out var list))
                return list;

            SerializedProperty elements = property.FindPropertyRelative("elements");

            list = new ReorderableList(property.serializedObject, elements, true, true, true, true);

            list.drawHeaderCallback = rect =>
            {
                float total = 0f;
                for (int i = 0; i < elements.arraySize; i++)
                    total += elements.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").floatValue;

                EditorGUI.LabelField(rect, $"{property.displayName} (Total Weight: {total:0.##})");
            };

            list.elementHeightCallback = index =>
            {
                SerializedProperty element = elements.GetArrayElementAtIndex(index);
                SerializedProperty value = element.FindPropertyRelative("element");

                float valueHeight = GetValueHeight(value);

                return valueHeight
                       + EditorGUIUtility.singleLineHeight  // weight slider
                       + EditorGUIUtility.singleLineHeight  // help box
                       + 20; // padding between sections
            };

            list.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty entry = elements.GetArrayElementAtIndex(index);
                SerializedProperty value = entry.FindPropertyRelative("element");
                SerializedProperty weight = entry.FindPropertyRelative("_weight");

                rect.y += 4;

                const float rightPadding = 12f;

                float valueHeight = GetValueHeight(value);

                Rect valueRect = new Rect(rect.x, rect.y, rect.width - rightPadding, valueHeight);
                DrawValue(valueRect, value);

                Rect weightRect = new Rect(
                    rect.x,
                    valueRect.yMax + 5,
                    rect.width - rightPadding,
                    EditorGUIUtility.singleLineHeight);

                weight.floatValue = EditorGUI.Slider(weightRect, "Weight", weight.floatValue, 0f, 100f);

                float total = 0f;
                for (int i = 0; i < elements.arraySize; i++)
                    total += elements.GetArrayElementAtIndex(i).FindPropertyRelative("_weight").floatValue;

                float chance = total > 0 ? weight.floatValue / total * 100f : 0;

                Rect chanceRect = new Rect(
                    rect.x,
                    weightRect.yMax + 3,
                    rect.width - rightPadding,
                    EditorGUIUtility.singleLineHeight);

                EditorGUI.HelpBox(chanceRect, $"Chance: {chance:0.0}%   Weight: {weight.floatValue:0.##}", MessageType.None);
            };

            lists[property.propertyPath] = list;
            return list;
        }


        // ---- helpers ----

        private static bool ShouldFlatten(SerializedProperty value)
        {
            // Only plain [Serializable] classes/structs get the foldout-bypass + box treatment.
            // Built-in types (Vector3, Color, Rect, object refs, primitives...) draw as a single
            // control already, so leave them alone.
            return value.propertyType == SerializedPropertyType.Generic
                   && value.hasVisibleChildren;
        }

        private static float GetChildrenHeight(SerializedProperty value)
        {
            float height = 0f;
            SerializedProperty child = value.Copy();
            SerializedProperty end = value.GetEndProperty();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            return height;
        }

        private static float GetValueHeight(SerializedProperty value)
        {
            if (!ShouldFlatten(value))
                return EditorGUI.GetPropertyHeight(value, GUIContent.none, true);

            float height = 0f;
            SerializedProperty child = value.Copy();
            SerializedProperty end = value.GetEndProperty();
            bool enterChildren = true;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            return height;
        }

        // // Peeks the first visible child; if it's a string with a value, use it as the title
        // // (mirrors the vanilla "name field becomes element label" convention).
        // private static string GetElementTitle(SerializedProperty value, int index)
        // {
        //     SerializedProperty first = value.Copy();
        //     SerializedProperty end = value.GetEndProperty();

        //     if (first.NextVisible(true) && !SerializedProperty.EqualContents(first, end))
        //     {
        //         if (first.propertyType == SerializedPropertyType.String
        //             && !string.IsNullOrEmpty(first.stringValue))
        //         {
        //             return first.stringValue;
        //         }
        //     }

        //     return $"Element {index}";
        // }

        private static void DrawValue(Rect rect, SerializedProperty value)
        {
            if (!ShouldFlatten(value))
            {
                EditorGUI.PropertyField(rect, value, GUIContent.none, true);
                return;
            }

            SerializedProperty child = value.Copy();
            SerializedProperty end = value.GetEndProperty();
            bool enterChildren = true;
            float y = rect.y;

            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                Rect childRect = new Rect(rect.x, y, rect.width, h);

                EditorGUI.PropertyField(childRect, child, true);

                y += h + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }
        }
    }
}

#endif