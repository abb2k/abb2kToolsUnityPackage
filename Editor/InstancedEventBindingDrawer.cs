using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.Events
{
    [CustomPropertyDrawer(typeof(InstancedEventBinding))]
    public class InstancedEventBindingDrawer : PropertyDrawer
    {
        private const string TooltipPriority = "Higher numbers evaluate first. Determines listener execution order.";
        private const string TooltipAutoBind = "Automatically unregisters this listener when the holding MonoBehaviour is destroyed.";
        private const string TooltipActiveInEditor = "If true, this listener will trigger even when outside of Play Mode.";

        private static Type[] availableEventTypes = null;
        private static string[] availableEventNames = null;

        private void InitializeTypes()
        {
            if (availableEventTypes != null) return;

            availableEventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && typeof(InstancedEventBaseOpaque).IsAssignableFrom(t))
                .ToArray();

            // Format names to include <T1, T2> based on expected parameters
            availableEventNames = availableEventTypes.Select(t => 
            {
                Type[] expectedParams = GetExpectedParameterTypes(t);
                if (expectedParams.Length == 0) return t.Name;
                
                string paramString = string.Join(", ", expectedParams.Select(p => p.Name));
                return $"{t.Name} <{paramString}>";
            }).ToArray();
        }

        private static Type[] GetExpectedParameterTypes(Type eventType)
        {
            if (eventType == null) return new Type[0];

            Type currentType = eventType;
            while (currentType != null && currentType != typeof(object))
            {
                if (currentType.IsGenericType)
                {
                    Type genericTypeDef = currentType.GetGenericTypeDefinition();
                    string name = genericTypeDef.Name;
                    
                    if (name.StartsWith("InstancedEvent`"))
                    {
                        Type[] genArgs = currentType.GetGenericArguments();
                        
                        // The first argument is TSelf. Skip it to get the parameter types.
                        if (genArgs.Length > 1)
                        {
                            Type[] result = new Type[genArgs.Length - 1];
                            Array.Copy(genArgs, 1, result, 0, genArgs.Length - 1);
                            return result;
                        }
                        return new Type[0]; 
                    }
                }
                currentType = currentType.BaseType;
            }
            return new Type[0];
        }

        private void SyncBindingState(SerializedProperty property)
        {
            if (property == null || property.serializedObject == null) return;

            property.serializedObject.ApplyModifiedProperties();

            object targetObject = property.serializedObject.targetObject;
            if (targetObject == null) return;

            InstancedEventBinding binding = fieldInfo?.GetValue(targetObject) as InstancedEventBinding;
            if (binding == null) return;

            MonoBehaviour holder = targetObject as MonoBehaviour;
            bool isComplete = !string.IsNullOrEmpty(binding.eventTypeAssemblyQualifiedName)
                && binding.targetObject != null
                && !string.IsNullOrEmpty(binding.methodName);

            if (isComplete)
            {
                binding.Initialize(holder);
            }
            else
            {
                binding.Uninitialize();
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            InitializeTypes();
            EditorGUI.BeginProperty(position, label, property);
            
            // 1. Calculate Box Layout
            float padding = 4f;
            float spacing = 2f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            
            // Get the true indented rect to draw the box perfectly, minus a little bottom margin
            Rect indentedBoxRect = EditorGUI.IndentedRect(position);
            indentedBoxRect.height -= 4f; 

            // Draw the background container (Matches UnityEvent aesthetic)
            GUI.Box(indentedBoxRect, GUIContent.none, EditorStyles.helpBox);

            // Temporarily reset indent to 0 so we can manually position elements inside the box 
            // without Unity double-indenting our fields.
            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Start drawing inside the box padding
            float currentY = indentedBoxRect.y + padding;
            float contentX = indentedBoxRect.x + padding;
            float contentWidth = indentedBoxRect.width - (padding * 2);

            SerializedProperty eventTypeProp = property.FindPropertyRelative("eventTypeAssemblyQualifiedName");
            SerializedProperty targetObjProp = property.FindPropertyRelative("targetObject");
            SerializedProperty methodNameProp = property.FindPropertyRelative("methodName");
            SerializedProperty priorityProp = property.FindPropertyRelative("priority");
            SerializedProperty autoBindProp = property.FindPropertyRelative("autoBindToHolder");
            SerializedProperty activeInEditorProp = property.FindPropertyRelative("activeInEditor"); // <--- NEW Property Link

            // 2. Header Label
            Rect labelRect = new Rect(contentX, currentY, contentWidth, lineHeight);
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
            currentY += lineHeight + spacing;

            // 3. Event Type Selection
            Rect eventRect = new Rect(contentX, currentY, contentWidth, lineHeight);
            int currentIndex = Array.FindIndex(availableEventTypes, t => t.AssemblyQualifiedName == eventTypeProp.stringValue);
            
            EditorGUI.BeginChangeCheck();
            currentIndex = EditorGUI.Popup(eventRect, "", Mathf.Max(0, currentIndex), availableEventNames);
            if ((EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(eventTypeProp.stringValue) && availableEventTypes.Length > 0) || string.IsNullOrEmpty(eventTypeProp.stringValue) && availableEventTypes.Length > 0)
            {
                eventTypeProp.stringValue = availableEventTypes[currentIndex].AssemblyQualifiedName;
                methodNameProp.stringValue = ""; // Clear method if event type changes
                SyncBindingState(property);
            }
            currentY += lineHeight + spacing;

            // 4. Target Object & Function Row
            float targetWidth = contentWidth * 0.35f; // 35% for the object
            float funcWidth = contentWidth * 0.65f;   // 65% for the dropdown

            Rect targetRect = new Rect(contentX, currentY, targetWidth - 2f, lineHeight);
            Rect funcRect = new Rect(contentX + targetWidth + 2f, currentY, funcWidth - 2f, lineHeight);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(targetRect, targetObjProp, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                methodNameProp.stringValue = ""; // Clear method if target changes
                SyncBindingState(property);
            }

            Type currentEventType = null;
            if (!string.IsNullOrEmpty(eventTypeProp.stringValue))
            {
                currentEventType = Type.GetType(eventTypeProp.stringValue);
            }

            DrawMethodDropdown(funcRect, property, targetObjProp, methodNameProp, currentEventType);
            currentY += lineHeight + spacing;

            // 5. Priority and AutoBind Settings Row
            float halfWidth = contentWidth * 0.5f;
            Rect halfRect1 = new Rect(contentX, currentY, halfWidth - 5f, lineHeight);
            Rect halfRect2 = new Rect(contentX + halfWidth + 5f, currentY, halfWidth - 5f, lineHeight);
            
            float oldLabelWidth = EditorGUIUtility.labelWidth;

            var priorityValue = priorityProp.intValue;

            EditorGUIUtility.labelWidth = 50;
            EditorGUI.PropertyField(halfRect1, priorityProp, new GUIContent("Priority", TooltipPriority));

            if (priorityValue != priorityProp.intValue)
            {
                SyncBindingState(property);
            }

            var autoBindValue = autoBindProp.boolValue;
            
            EditorGUIUtility.labelWidth = 65;
            EditorGUI.PropertyField(halfRect2, autoBindProp, new GUIContent("Auto Bind", TooltipAutoBind));

            if (autoBindValue != autoBindProp.boolValue)
            {
                SyncBindingState(property);
            }
            
            currentY += lineHeight + spacing;

            // <--- NEW: 6. Active In Editor Row
            Rect editorRect = new Rect(contentX, currentY, contentWidth, lineHeight);
            var activeInEditorValue = activeInEditorProp.boolValue;
            
            EditorGUIUtility.labelWidth = 100;
            EditorGUI.PropertyField(editorRect, activeInEditorProp, new GUIContent("Active In Editor", TooltipActiveInEditor));

            if (activeInEditorValue != activeInEditorProp.boolValue)
            {
                SyncBindingState(property);
            }

            // Restore original editor states
            EditorGUIUtility.labelWidth = oldLabelWidth; 
            EditorGUI.indentLevel = oldIndent;

            EditorGUI.EndProperty();
        }

        private void DrawMethodDropdown(Rect buttonRect, SerializedProperty property, SerializedProperty targetObjProp, SerializedProperty methodNameProp, Type currentEventType)
        {
            UnityEngine.Object currentTarget = targetObjProp.objectReferenceValue;
            string currentMethod = methodNameProp.stringValue;
            
            GameObject rootGO = null;
            if (currentTarget is GameObject go) rootGO = go;
            else if (currentTarget is Component comp) rootGO = comp.gameObject;

            string displayString = "No Function";
            if (currentTarget != null && !string.IsNullOrEmpty(currentMethod))
            {
                displayString = currentMethod; 
            }

            GUI.enabled = currentTarget != null && currentEventType != null;

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayString), FocusType.Keyboard))
            {
                GenericMenu menu = new GenericMenu();

                string targetPath = targetObjProp.propertyPath;
                string methodPath = methodNameProp.propertyPath;
                SerializedObject serializedObj = targetObjProp.serializedObject;

                Type[] expectedParams = GetExpectedParameterTypes(currentEventType);

                GenericMenu.MenuFunction2 onMenuSelect = (object data) =>
                {
                    SelectionData selection = (SelectionData)data;
                    serializedObj.Update();
                    serializedObj.FindProperty(targetPath).objectReferenceValue = selection.target;
                    serializedObj.FindProperty(methodPath).stringValue = selection.methodName;
                    serializedObj.ApplyModifiedProperties();
                    SyncBindingState(property);
                };

                bool isNoFunctionSelected = string.IsNullOrEmpty(currentMethod) || currentTarget == null;
                menu.AddItem(new GUIContent("No Function"), isNoFunctionSelected, onMenuSelect, new SelectionData { target = rootGO, methodName = "" });

                if (rootGO != null)
                {
                    Component[] components = rootGO.GetComponents<Component>();
                    foreach (Component c in components)
                    {
                        if (c == null) continue;
                        PopulateMenuWithMethods(menu, c.GetType(), c, currentMethod, expectedParams, onMenuSelect);
                    }
                }
                else if (currentTarget != null)
                {
                    PopulateMenuWithMethods(menu, currentTarget.GetType(), currentTarget, currentMethod, expectedParams, onMenuSelect);
                }

                menu.DropDown(buttonRect);
            }

            GUI.enabled = true; 
        }

        private void PopulateMenuWithMethods(GenericMenu menu, Type componentType, UnityEngine.Object targetInstance, string currentMethod, Type[] expectedParams, GenericMenu.MenuFunction2 onMenuSelect)
        {
            var methods = componentType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.ReturnType == typeof(ListenerResult) && !m.IsSpecialName)
                .ToArray();

            foreach (var m in methods)
            {
                bool isSelected = (currentMethod == m.Name);
                bool isValid = IsMethodValid(m, expectedParams);
                
                string paramString = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                string menuLabel = $"{componentType.Name}/{m.Name} ({paramString})";

                GUIContent content = new GUIContent(menuLabel);

                if (isValid)
                {
                    menu.AddItem(content, isSelected, onMenuSelect, new SelectionData { target = targetInstance, methodName = m.Name });
                }
                else
                {
                    menu.AddDisabledItem(content, isSelected);
                }
            }
        }

        private bool IsMethodValid(MethodInfo m, Type[] expectedParams)
        {
            ParameterInfo[] methodParams = m.GetParameters();
            if (methodParams.Length != expectedParams.Length) return false;

            for (int i = 0; i < methodParams.Length; i++)
            {
                if (methodParams[i].ParameterType != expectedParams[i]) return false;
            }
            return true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float padding = 4f;
          
            int lines = 5;
            return (lines * lineHeight) + ((lines - 1) * spacing) + (padding * 2) + 4f;
        }

        private struct SelectionData
        {
            public UnityEngine.Object target;
            public string methodName;
        }
    }
}