using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Abb2kTools.Events.Editor
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
                    MethodInfo[] allMethods = selectedType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    
                    MethodInfo sendMethod = allMethods.FirstOrDefault(m => 
                    {
                        if (m.Name != "Send") return false;
                        var p = m.GetParameters();
                        
                        if (p.Length == paramCount) return true;
                        if (p.Length == paramCount + 1 && p.Last().ParameterType == typeof(bool)) return true;
                        
                        return false;
                    });

                    if (sendMethod != null)
                    {
                        var methodParams = sendMethod.GetParameters();
                        object[] invokeArgs;

                        if (methodParams.Length == paramCount + 1)
                        {
                            invokeArgs = new object[paramCount + 1];
                            Array.Copy(paramValues, invokeArgs, paramCount);
                            invokeArgs[paramCount] = false;
                        }
                        else
                        {
                            invokeArgs = paramValues;
                        }

                        sendMethod.Invoke(null, invokeArgs);
                    }
                    else
                    {
                        Debug.LogError($"[UES] Could not find a compatible static 'Send' method on {selectedType.Name}.");
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
            // 1. Handle Null Initialization
            if (value == null)
            {
                if (t == typeof(string)) value = string.Empty;
                else if (t.IsValueType) value = Activator.CreateInstance(t);
            }

            // 2. Standard Primitives
            if (t == typeof(int)) return EditorGUILayout.IntField(label, (int)value);
            if (t == typeof(float)) return EditorGUILayout.FloatField(label, (float)value);
            if (t == typeof(double)) return EditorGUILayout.DoubleField(label, (double)value);
            if (t == typeof(long)) return EditorGUILayout.LongField(label, (long)value);
            if (t == typeof(string)) return EditorGUILayout.TextField(label, (string)value);
            if (t == typeof(bool)) return EditorGUILayout.Toggle(label, (bool)value);

            // 3. Unity Structs & Math Types
            if (t == typeof(Vector2)) return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (t == typeof(Vector3)) return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (t == typeof(Vector4)) return EditorGUILayout.Vector4Field(label, (Vector4)value);
            if (t == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField(label, (Vector2Int)value);
            if (t == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField(label, (Vector3Int)value);
            if (t == typeof(Color)) return EditorGUILayout.ColorField(label, (Color)value);
            if (t == typeof(Rect)) return EditorGUILayout.RectField(label, (Rect)value);
            if (t == typeof(RectInt)) return EditorGUILayout.RectIntField(label, (RectInt)value);
            if (t == typeof(Bounds)) return EditorGUILayout.BoundsField(label, (Bounds)value);
            if (t == typeof(BoundsInt)) return EditorGUILayout.BoundsIntField(label, (BoundsInt)value);
            
            // 4. Unity Classes
            if (t == typeof(AnimationCurve)) return EditorGUILayout.CurveField(label, (AnimationCurve)value ?? new AnimationCurve());

            // 5. Enums
            if (t.IsEnum)
            {
                if (value == null) value = Enum.GetValues(t).GetValue(0);
                return EditorGUILayout.EnumPopup(label, (Enum)value);
            }

            // 6. Unity Objects (Monobehaviours, GameObjects, ScriptableObjects, etc.)
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, t, true);
            }

            // 7. Arrays and Lists
            if (typeof(IList).IsAssignableFrom(t))
            {
                return DrawListField(label, value, t, uniqueId);
            }

            // 8. Fallback: Custom Classes & Structs (Reflection)
            return DrawReflectionField(label, value, t, uniqueId);
        }

        // Helper to draw Lists and Arrays of ANY type recursively
        private object DrawListField(string label, object value, Type t, string uniqueId)
        {
            string key = string.IsNullOrEmpty(uniqueId) ? label : uniqueId;
            if (!foldoutStates.ContainsKey(key)) foldoutStates[key] = false;

            foldoutStates[key] = EditorGUILayout.Foldout(foldoutStates[key], label, true, EditorStyles.foldout);

            IList list = value as IList;
            Type elementType = t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];

            if (list == null)
            {
                list = t.IsArray ? Array.CreateInstance(elementType, 0) : (IList)Activator.CreateInstance(t);
            }

            if (foldoutStates[key])
            {
                EditorGUI.indentLevel++;
                
                int newSize = Math.Max(0, EditorGUILayout.DelayedIntField("Size", list.Count));
                
                if (newSize != list.Count)
                {
                    if (t.IsArray)
                    {
                        Array newArray = Array.CreateInstance(elementType, newSize);
                        for (int i = 0; i < Math.Min(list.Count, newSize); i++) newArray.SetValue(list[i], i);
                        list = newArray;
                    }
                    else
                    {
                        while (list.Count < newSize) 
                            list.Add(elementType.IsValueType ? Activator.CreateInstance(elementType) : null);
                        while (list.Count > newSize) 
                            list.RemoveAt(list.Count - 1);
                    }
                }

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = DrawDynamicField($"Element {i}", list[i], elementType, $"{key}[{i}]");
                }
                EditorGUI.indentLevel--;
            }

            return list;
        }

        // Helper to draw custom objects/structs via Reflection
        private object DrawReflectionField(string label, object value, Type t, string uniqueId)
        {
            string key = string.IsNullOrEmpty(uniqueId) ? label : uniqueId;
            if (!foldoutStates.ContainsKey(key)) foldoutStates[key] = false;

            foldoutStates[key] = EditorGUILayout.Foldout(foldoutStates[key], label, true, EditorStyles.foldout);

            if (value == null)
            {
                try { value = Activator.CreateInstance(t); } 
                catch { return value; } // If it has no parameterless constructor, we skip
            }

            if (foldoutStates[key])
            {
                EditorGUI.indentLevel++;
                foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object fieldValue = field.GetValue(value);
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
    }
}