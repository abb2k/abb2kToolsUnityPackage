#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Abb2kTools.Commands;

namespace Abb2kTools.EditorScripts
{
    [CustomPropertyDrawer(typeof(ICommand), true)]
    public class ICommandDrawer : PropertyDrawer
    {
        private static Type[] _commandTypes;
        private static string[] _commandTypeNames;
        private static GUIContent[] _commandTypeContents;

        private void InitializeReflection()
        {
            if (_commandTypes == null)
            {
                var baseType = typeof(ICommand);
                var excludeType = typeof(HideInCommandInspectorAttribute);

                // Get valid types (skip hidden ones, abstract ones, and those without default constructors)
                _commandTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => baseType.IsAssignableFrom(p) && 
                                !p.IsInterface && 
                                !p.IsAbstract &&
                                !Attribute.IsDefined(p, excludeType) &&
                                (p.IsValueType || p.GetConstructor(Type.EmptyTypes) != null))
                    .ToArray();

                // Create lists with a "Null / None" option at index 0
                var typeList = _commandTypes.ToList();
                typeList.Insert(0, null);
                _commandTypes = typeList.ToArray();

                _commandTypeNames = _commandTypes.Select(t => t == null ? "None (Null)" : t.Name).ToArray();
                _commandTypeContents = _commandTypeNames.Select(n => new GUIContent(n)).ToArray();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight; // Header row

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                // Sum up heights of all child properties if expanded
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty)) break;
                        height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                    } while (iterator.NextVisible(false));
                }
                height += EditorGUIUtility.standardVerticalSpacing * 2; // Bottom padding
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InitializeReflection();

            EditorGUI.BeginProperty(position, label, property);

            // Determine current selected type
            object currentObj = property.managedReferenceValue;
            Type currentType = currentObj?.GetType();
            int currentIndex = 0;
            if (currentType != null)
            {
                currentIndex = Array.IndexOf(_commandTypes, currentType);
                if (currentIndex == -1) currentIndex = 0; // Fallback if type was removed
            }

            // Draw Header Rects
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, EditorGUIUtility.labelWidth, headerRect.height);
            Rect popupRect = new Rect(headerRect.x + EditorGUIUtility.labelWidth, headerRect.y, headerRect.width - EditorGUIUtility.labelWidth, headerRect.height);

            // 1. Foldout (Only show arrow if we actually have an object with fields)
            if (currentObj != null)
            {
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            }
            else
            {
                EditorGUI.LabelField(foldoutRect, label);
            }

            // 2. Dropdown Selector
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, currentIndex, _commandTypeContents);
            if (EditorGUI.EndChangeCheck())
            {
                Type newType = _commandTypes[newIndex];
                if (newType == null)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    property.managedReferenceValue = Activator.CreateInstance(newType);
                    property.isExpanded = true; // Auto-expand when a new command is chosen
                }
                property.serializedObject.ApplyModifiedProperties();
                return; // Exit GUI loop this frame to avoid layout mismatch errors
            }

            // 3. Draw Children if Expanded
            if (property.isExpanded && currentObj != null)
            {
                EditorGUI.indentLevel++;
                
                // Indent box background for visual grouping
                Rect boxRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.height - EditorGUIUtility.singleLineHeight);
                GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                        float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                        Rect childRect = new Rect(position.x, currentY, position.width - 4f, childHeight);
                        
                        EditorGUI.PropertyField(childRect, iterator, true);
                        currentY += childHeight + EditorGUIUtility.standardVerticalSpacing;

                    } while (iterator.NextVisible(false));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif