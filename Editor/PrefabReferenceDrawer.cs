#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PrefabReferenceBase<>))]
public class PrefabReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty componentProp = property.FindPropertyRelative("_component");

        Type fieldType = fieldInfo.FieldType;
        if (fieldType.IsArray) 
        {
            fieldType = fieldType.GetElementType();
        }
        else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            fieldType = fieldType.GetGenericArguments()[0];
        }

        Type componentType = fieldType.GetGenericArguments()[0];

        GameObject currentPrefab = null;
        if (componentProp.objectReferenceValue != null)
        {
            currentPrefab = ((Component)componentProp.objectReferenceValue).gameObject;
        }

        EditorGUI.BeginChangeCheck();

        GameObject newPrefab = (GameObject)EditorGUI.ObjectField(position, label, currentPrefab, typeof(GameObject), false);

        if (EditorGUI.EndChangeCheck())
        {
            if (newPrefab == null)
            {
                componentProp.objectReferenceValue = null;
            }
            else
            {
                Component comp = newPrefab.GetComponent(componentType);
                
                if (comp != null && comp.transform.parent == null)
                {
                    componentProp.objectReferenceValue = comp;
                }
                else if (comp == null)
                {
                    Debug.LogWarning($"[PrefabReference] The selected prefab does not have the '{componentType.Name}' component!");
                }
                else
                {
                    Debug.LogWarning($"[PrefabReference] The '{componentType.Name}' component must be on the ROOT object of the prefab.");
                }
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif