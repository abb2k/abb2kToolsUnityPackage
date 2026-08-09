using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Abb2kTools.Events
{
public class UniversalEventSender : EditorWindow
{
    private Type[] eventTypes;
    private string[] eventTypeNames;
    private int selectedIndex = 0;
    
    // Stores the dynamic values to be passed into Send(...)
    private object[] paramValues = new object[0];

    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    [MenuItem("Tools/Universal Event Sender")]
    public static void ShowWindow()
    {
        GetWindow<UniversalEventSender>("Universal Event Sender");
    }

    private void OnEnable()
    {
        // Find all concrete classes that inherit from InstancedEventBaseOpaque
        eventTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(InstancedEventBaseOpaque)))
            .OrderBy(t => t.Name)
            .ToArray();

        eventTypeNames = eventTypes.Select(t => t.Name).ToArray();
    }

    private void OnGUI()
    {
        if (eventTypes == null || eventTypes.Length == 0)
        {
            EditorGUILayout.HelpBox("No InstancedEvents found in the project.", MessageType.Info);
            return;
        }

        // Select the event type
        selectedIndex = EditorGUILayout.Popup("Target Event", selectedIndex, eventTypeNames);
        Type selectedType = eventTypes[selectedIndex];

        // Find the generic base type to extract the parameter types (T1, T2...)
        Type baseType = selectedType.BaseType;
        while (baseType != null && !baseType.Name.StartsWith("InstancedEvent`"))
        {
            baseType = baseType.BaseType;
        }

        if (baseType != null)
        {
            Type[] genericArgs = baseType.GetGenericArguments();
            
            // genericArgs[0] is TSelf. The rest are the actual parameters.
            int paramCount = genericArgs.Length - 1;

            // Resize the parameter array if needed
            if (paramValues == null || paramValues.Length != paramCount)
            {
                paramValues = new object[paramCount];
            }

            // Retrieve custom parameter names from the attribute
            var attr = selectedType.GetCustomAttribute<InstancedEventParamsAttribute>();
            string[] paramNames = attr?.ParameterNames;

            EditorGUILayout.Space();
            GUILayout.Label("Parameters:", EditorStyles.boldLabel);

            for (int i = 0; i < paramCount; i++)
            {
                Type pType = genericArgs[i + 1];
                string label = (paramNames != null && i < paramNames.Length) 
                    ? paramNames[i] 
                    : $"Param {i + 1} ({pType.Name})";

                paramValues[i] = DrawDynamicField(label, paramValues[i], pType);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Send Event", GUILayout.Height(30)))
            {
                MethodInfo sendMethod = selectedType.GetMethod("Send", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (sendMethod != null)
                {
                    sendMethod.Invoke(null, paramValues);
                    //Debug.Log($"[InstancedEvent] Invoked <b>{selectedType.Name}</b> successfully.");
                }
                else
                {
                    Debug.LogError($"Could not find Send method on {selectedType.Name}");
                }
            }

            GUILayout.FlexibleSpace(); 
            if (GUILayout.Button("Erase Listeners For Event", GUILayout.Height(30)))
            {
                object eventInstance = selectedType.GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.Invoke(null, null);
                if (eventInstance is InstancedEventBaseOpaque opaqueEvent)
                {
                    opaqueEvent.ClearAllListeners();
                }
                else
                {
                    Debug.LogWarning($"Could not clear listeners for {selectedType.Name}.");
                }
            }
        }
    }

    // Handles drawing the correct Unity Inspector field based on the generic parameter type
    private object DrawDynamicField(string label, object value, Type t, string uniqueId = "")
    {
        if (t == typeof(int)) return EditorGUILayout.IntField(label, value == null ? 0 : (int)value);
        if (t == typeof(float)) return EditorGUILayout.FloatField(label, value == null ? 0f : (float)value);
        if (t == typeof(string)) return EditorGUILayout.TextField(label, value == null ? string.Empty : (string)value);
        if (t == typeof(bool)) return EditorGUILayout.Toggle(label, value != null && (bool)value);
        if (t == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, value == null ? Vector2.zero : (Vector2)value);
        if (t == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, value == null ? Vector3.zero : (Vector3)value);
        if (t.IsEnum)
        {
            if (value == null) value = Enum.GetValues(t).GetValue(0);
            return EditorGUILayout.EnumPopup(label, (Enum)value);
        }
        if (typeof(UnityEngine.Object).IsAssignableFrom(t))
        {
            return EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, t, true);
        }
        if (t.IsSerializable)
        {
            // Use the label (or a provided uniqueId) as the key
            string key = string.IsNullOrEmpty(uniqueId) ? label : uniqueId;
            
            if (!foldoutStates.ContainsKey(key)) foldoutStates[key] = false;

            foldoutStates[key] = EditorGUILayout.Foldout(foldoutStates[key], label, true, EditorStyles.foldout);

            if (foldoutStates[key])
            {
                EditorGUI.indentLevel++;
                
                // Create instance if null
                if (value == null) value = Activator.CreateInstance(t);
                
                foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object fieldValue = field.GetValue(value);
                    // Recursively call with a combined key to keep paths unique
                    object newFieldValue = DrawDynamicField(field.Name, fieldValue, field.FieldType, key + "." + field.Name);
                    
                    if (!Equals(fieldValue, newFieldValue))
                    {
                        field.SetValue(value, newFieldValue);
                    }
                }
                EditorGUI.indentLevel--;
            }
            return value;
        }

        EditorGUILayout.LabelField(label, $"Unsupported Editor Type ({t.Name})");
        return value;
    }
}
}