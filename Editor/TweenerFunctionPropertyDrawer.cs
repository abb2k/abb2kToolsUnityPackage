#if DOTWEEN
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace Abb2kTools
{
    [CustomPropertyDrawer(typeof(TweenerFunction))]
    public class TweenerFunctionPropertyDrawer : PropertyDrawer
    {
        private const float PADDING = 4f;
        private const float SPACING = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + (PADDING * 2);
            height += EditorGUIUtility.singleLineHeight + SPACING;

            SerializedProperty paramsProp = property.FindPropertyRelative("parameters");
            if (paramsProp != null && paramsProp.arraySize > 0)
            {
                height += EditorGUIUtility.singleLineHeight + SPACING; 
                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    var valProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("value");
                    if (valProp != null && valProp.managedReferenceValue != null)
                    {
                        var dataProp = valProp.FindPropertyRelative("data");
                        float pHeight = (dataProp != null) ? EditorGUI.GetPropertyHeight(dataProp, true) : EditorGUIUtility.singleLineHeight;
                        height += pHeight + SPACING;
                    }
                }
            }
            return height + PADDING;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            Rect drawRect = new Rect(position.x + PADDING, position.y + PADDING, position.width - (PADDING * 2), EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(drawRect, label, EditorStyles.boldLabel);
            drawRect.y += drawRect.height + SPACING;

            float half = (drawRect.width - 4f) / 2f;
            Rect objRect = new Rect(drawRect.x, drawRect.y, half, drawRect.height);
            Rect btnRect = new Rect(drawRect.x + half + 4f, drawRect.y, half, drawRect.height);

            SerializedProperty objProp = property.FindPropertyRelative("targetObject");
            SerializedProperty compProp = property.FindPropertyRelative("targetComponent");
            SerializedProperty methodProp = property.FindPropertyRelative("methodName");

            EditorGUI.PropertyField(objRect, objProp, GUIContent.none);

            if (objProp.objectReferenceValue != null)
            {
                string display = (compProp.objectReferenceValue != null && !string.IsNullOrEmpty(methodProp.stringValue))
                    ? methodProp.stringValue : "Select Method...";

                if (EditorGUI.DropdownButton(btnRect, new GUIContent(display), FocusType.Keyboard))
                    ShowMethodMenu((GameObject)objProp.objectReferenceValue, property);
            }
            else
            {
                property.FindPropertyRelative("targetComponent").objectReferenceValue = null;
                property.FindPropertyRelative("methodName").stringValue = string.Empty;
                property.FindPropertyRelative("parameters").ClearArray();
            }

            drawRect.y += drawRect.height + SPACING;
            SerializedProperty paramsProp = property.FindPropertyRelative("parameters");

            if (paramsProp != null && paramsProp.arraySize > 0 && compProp.objectReferenceValue != null)
            {
                EditorGUI.LabelField(drawRect, "Parameters", EditorStyles.boldLabel);
                drawRect.y += drawRect.height + SPACING;

                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    SerializedProperty pElement = paramsProp.GetArrayElementAtIndex(i);
                    SerializedProperty valueWrapper = pElement.FindPropertyRelative("value");
                    string pName = pElement.FindPropertyRelative("name").stringValue;
                    string typeName = pElement.FindPropertyRelative("typeName").stringValue;

                    if (valueWrapper != null && valueWrapper.managedReferenceValue != null)
                    {
                        SerializedProperty dataProp = valueWrapper.FindPropertyRelative("data");
                        SerializedProperty hasValueProp = valueWrapper.FindPropertyRelative("hasValue");

                        bool isNullable = typeName.Contains("System.Nullable");
                        float pHeight = (dataProp != null) ? EditorGUI.GetPropertyHeight(dataProp, true) : EditorGUIUtility.singleLineHeight;
                        Rect pRect = new Rect(drawRect.x, drawRect.y, drawRect.width, pHeight);

                        if (isNullable && hasValueProp != null)
                        {
                            Rect toggleRect = new Rect(pRect.x, pRect.y, 20, EditorGUIUtility.singleLineHeight);
                            hasValueProp.boolValue = EditorGUI.Toggle(toggleRect, hasValueProp.boolValue);
                            
                            pRect.xMin += 25; // Indent the actual field
                            
                            EditorGUI.BeginDisabledGroup(!hasValueProp.boolValue);
                            if (dataProp != null) 
                                EditorGUI.PropertyField(pRect, dataProp, new GUIContent(pName), true);
                            else
                                EditorGUI.LabelField(pRect, pName, "Null");
                            EditorGUI.EndDisabledGroup();
                        }
                        else if (dataProp != null)
                        {
                            EditorGUI.PropertyField(pRect, dataProp, new GUIContent(pName), true);
                        }

                        drawRect.y += pHeight + SPACING;
                    }
                }
            }
            EditorGUI.EndProperty();
        }

        private void ShowMethodMenu(GameObject target, SerializedProperty property)
        {
            GenericMenu menu = new GenericMenu();
            foreach (var comp in target.GetComponents<Component>())
            {
                if (comp == null) continue;
                foreach (var m in TweenerFunction.GetValidMethods(comp.GetType()))
                {
                    Component c = comp; MethodInfo mi = m;
                    menu.AddItem(new GUIContent($"{c.GetType().Name}/{TweenerFunction.GetMethodKey(mi)}"), false, 
                        () => ApplySelection(property, c, mi));
                }
            }
            menu.ShowAsContext();
        }

        private void ApplySelection(SerializedProperty property, Component comp, MethodInfo method)
        {
            property.serializedObject.Update();
            property.FindPropertyRelative("targetComponent").objectReferenceValue = comp;
            property.FindPropertyRelative("methodName").stringValue = TweenerFunction.GetMethodKey(method);

            SerializedProperty paramsProp = property.FindPropertyRelative("parameters");
            paramsProp.ClearArray();

            var parameters = method.GetParameters();
            bool isExt = method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);

            for (int i = isExt ? 1 : 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (param.Name.ToLower().Contains("duration")) continue;

                paramsProp.InsertArrayElementAtIndex(paramsProp.arraySize);
                var element = paramsProp.GetArrayElementAtIndex(paramsProp.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = param.Name;
                
                Type pType = param.ParameterType;
                element.FindPropertyRelative("typeName").stringValue = pType.AssemblyQualifiedName;

                Type underlying = Nullable.GetUnderlyingType(pType) ?? pType;
                Type wrapperType = typeof(TweenerFunction.ParameterValue<>).MakeGenericType(underlying);
                
                object initVal = null;
                if (param.HasDefaultValue && param.DefaultValue != DBNull.Value) initVal = param.DefaultValue;
                else if (underlying.IsValueType) initVal = Activator.CreateInstance(underlying);

                element.FindPropertyRelative("value").managedReferenceValue = Activator.CreateInstance(wrapperType, initVal);
                
                if (Nullable.GetUnderlyingType(pType) != null && (!param.HasDefaultValue || param.DefaultValue == DBNull.Value || param.DefaultValue == null))
                {
                    var valProp = element.FindPropertyRelative("value");
                    var hasValueProp = valProp.FindPropertyRelative("hasValue");
                    if (hasValueProp != null) hasValueProp.boolValue = false;
                }
            }
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif