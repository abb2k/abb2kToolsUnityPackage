#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

[CustomPropertyDrawer(typeof(PrefabReferenceBase<>))]
public class PrefabReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty componentProp = property.FindPropertyRelative("_component");

        // Safety fallback: If this is drawn in a native context that doesn't support 
        // nested collections (like 2D arrays) without Odin, prevent a NullReferenceException.
        if (componentProp == null)
        {
            EditorGUI.HelpBox(position, "Property not found. If this is a 2D array/matrix, Odin Inspector is required.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

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
                    componentProp.objectReferenceValue = null;
                }
                else
                {
                    Debug.LogWarning($"[PrefabReference] The '{componentType.Name}' component must be on the ROOT object of the prefab.");
                    componentProp.objectReferenceValue = null;
                }
            }
        }

        EditorGUI.EndProperty();
    }
}

#if ODIN_INSPECTOR
// This highly-prioritized drawer takes over entirely when Odin is installed.
// It resolves generics natively, avoids SerializedProperty entirely, and maps perfectly to TableMatrix cells.
[DrawerPriority(0, 0, 1)] 
public class PrefabReferenceOdinDrawer<TBase, TComponent> : OdinValueDrawer<TBase>
    where TBase : PrefabReferenceBase<TComponent>
    where TComponent : Component
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var entry = this.ValueEntry;

        // Auto-instantiate class wrappers if the TableMatrix cell is empty (null)
        if (entry.SmartValue == null)
        {
            entry.SmartValue = Activator.CreateInstance<TBase>();
        }

        GameObject currentPrefab = entry.SmartValue.GameObject;

        EditorGUI.BeginChangeCheck();

        // TableMatrix heavily relies on GUILayout blocks. GetControlRect fits perfectly inside Odin cells.
        bool hasLabel = label != null && label != GUIContent.none;
        Rect rect = EditorGUILayout.GetControlRect(hasLabel);

        GameObject newPrefab = (GameObject)EditorGUI.ObjectField(rect, label, currentPrefab, typeof(GameObject), false);

        if (EditorGUI.EndChangeCheck())
        {
            var componentProperty = entry.Property.Children["_component"];

            if (newPrefab == null)
            {
                componentProperty.ValueEntry.WeakSmartValue = null;
            }
            else
            {
                TComponent comp = newPrefab.GetComponent<TComponent>();
                
                if (comp != null && comp.transform.parent == null)
                {
                    componentProperty.ValueEntry.WeakSmartValue = comp;
                }
                else if (comp == null)
                {
                    Debug.LogWarning($"[PrefabReference] The selected prefab does not have the '{typeof(TComponent).Name}' component!");
                    componentProperty.ValueEntry.WeakSmartValue = null;
                }
                else
                {
                    Debug.LogWarning($"[PrefabReference] The '{typeof(TComponent).Name}' component must be on the ROOT object of the prefab.");
                    componentProperty.ValueEntry.WeakSmartValue = null;
                }
            }

            // Force Odin to mark the matrix element as dirty
            componentProperty.ValueEntry.ApplyChanges();
        }
    }
}
#endif
#endif