#if DOTWEEN
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
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
                height += SPACING * 2 + EditorGUIUtility.singleLineHeight;
                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    var valProp = paramsProp.GetArrayElementAtIndex(i).FindPropertyRelative("value");
                    if (valProp != null && valProp.managedReferenceValue != null)
                    {
                        var dataProp = valProp.FindPropertyRelative("data");
                        if (dataProp != null)
                            height += EditorGUI.GetPropertyHeight(dataProp, true) + SPACING;
                        else 
                            height += EditorGUIUtility.singleLineHeight + SPACING;
                    }
                }
                height += PADDING;
            }
            return height;
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
                {
                    ShowMethodMenu((GameObject)objProp.objectReferenceValue, property);
                }
            }
            else
            {
                property.FindPropertyRelative("targetComponent").objectReferenceValue = null;
                property.FindPropertyRelative("methodName").stringValue = "";
                property.FindPropertyRelative("parameters").ClearArray();
            }

            drawRect.y += drawRect.height + SPACING;
            SerializedProperty paramsProp = property.FindPropertyRelative("parameters");

            if (paramsProp != null && paramsProp.arraySize > 0 && compProp.objectReferenceValue != null)
            {
                drawRect.y += SPACING;
                EditorGUI.LabelField(drawRect, "Parameters", EditorStyles.boldLabel);
                drawRect.y += drawRect.height;

                for (int i = 0; i < paramsProp.arraySize; i++)
                {
                    SerializedProperty pElement = paramsProp.GetArrayElementAtIndex(i);
                    SerializedProperty valueWrapper = pElement.FindPropertyRelative("value");
                    string pName = pElement.FindPropertyRelative("name").stringValue;

                    if (valueWrapper != null && valueWrapper.managedReferenceValue != null)
                    {
                        SerializedProperty dataProp = valueWrapper.FindPropertyRelative("data");
                        if (dataProp != null)
                        {
                            float pHeight = EditorGUI.GetPropertyHeight(dataProp, true);
                            Rect pRect = new Rect(drawRect.x, drawRect.y, drawRect.width, pHeight);

                            EditorGUI.BeginChangeCheck();
                            EditorGUI.PropertyField(pRect, dataProp, new GUIContent(pName), true);
                            if (EditorGUI.EndChangeCheck())
                            {
                                property.serializedObject.ApplyModifiedProperties();
                            }
                            
                            drawRect.y += pHeight + SPACING;
                        }
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

            bool isExt = method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
            var parameters = method.GetParameters();

            for (int i = isExt ? 1 : 0; i < parameters.Length; i++)
            {
                var paramInfo = parameters[i];
                string pName = paramInfo.Name.ToLower();
                Type pType = paramInfo.ParameterType;

                if (pName.Contains("duration") || pName == "target" || pName == "t") continue;

                paramsProp.InsertArrayElementAtIndex(paramsProp.arraySize);
                var element = paramsProp.GetArrayElementAtIndex(paramsProp.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = paramInfo.Name;
                element.FindPropertyRelative("typeName").stringValue = pType.AssemblyQualifiedName;

                Type wrapperType = typeof(TweenerFunction.ParameterValue<>).MakeGenericType(pType);
                object valueToAssign = null;

                if (paramInfo.HasDefaultValue)
                {
                    valueToAssign = paramInfo.DefaultValue;
                }
                else
                {
                    if (pType.IsArray)
                        valueToAssign = Array.CreateInstance(pType.GetElementType(), 0);
                    else if (pType.IsValueType)
                        valueToAssign = Activator.CreateInstance(pType);
                }

                element.FindPropertyRelative("value").managedReferenceValue = Activator.CreateInstance(wrapperType, valueToAssign);
            }

            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif