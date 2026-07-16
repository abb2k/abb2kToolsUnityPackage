using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif

namespace Abb2kTools
{
    // =======================================================================
    // 1. THE ATTRIBUTE
    // =======================================================================
    
    /// <summary>
    /// Apply this to any List or Array to display chances and weights via a getter method.
    /// Usage: [WeightedListDisplay(nameof(WeightGetter))]
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class WeightedListDisplayAttribute : PropertyAttribute
    {
        public string MethodName { get; private set; }

        public WeightedListDisplayAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

#if UNITY_EDITOR

    // =======================================================================
    // 2. ODIN INSPECTOR IMPLEMENTATION
    // =======================================================================
#if ODIN_INSPECTOR

    namespace Editor
    {
        // A. Drawer for the List itself (Changes the header to show Overall Weight)
        [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
        public class WeightedListAttributeOdinDrawer : OdinAttributeDrawer<WeightedListDisplayAttribute>
        {
            protected override void DrawPropertyLayout(GUIContent label)
            {
                if (this.Property.ChildResolver is ICollectionResolver)
                {
                    float totalWeight = 0f;
                    foreach (var child in this.Property.Children)
                    {
                        totalWeight += GetWeightFromMethod(this.Property, child.ValueEntry.WeakSmartValue, this.Attribute.MethodName);
                    }

                    string baseName = label != null && !string.IsNullOrEmpty(label.text) ? label.text : this.Property.NiceName;
                    GUIContent newLabel = new GUIContent($"{baseName} (Overall Weight: {totalWeight})");

                    this.CallNextDrawer(newLabel);
                }
                else
                {
                    this.CallNextDrawer(label);
                }
            }
            
            internal static float GetWeightFromMethod(InspectorProperty listProperty, object elementValue, string methodName)
            {
                if (elementValue == null || string.IsNullOrEmpty(methodName)) return 0f;

                // Grab the object that holds the list (usually your MonoBehaviour)
                object parentObject = listProperty.ParentValues.Count > 0 ? listProperty.ParentValues[0] : null;
                object rootObject = listProperty.SerializationRoot?.ValueEntry?.WeakSmartValue;

                MethodInfo method = null;
                object targetInvokeObj = null;

                // Search the direct parent class first
                if (parentObject != null)
                {
                    method = parentObject.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    targetInvokeObj = parentObject;
                }
                
                // If not found, fallback to searching the root MonoBehaviour 
                if (method == null && rootObject != null && rootObject != parentObject)
                {
                    method = rootObject.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    targetInvokeObj = rootObject;
                }

                if (method != null)
                {
                    try
                    {
                        // Invoke the method, passing the list element as the parameter!
                        return Convert.ToSingle(method.Invoke(targetInvokeObj, new object[] { elementValue }));
                    }
                    catch { }
                }

                return 0f;
            }
        }

        // B. Drawer for the Elements inside the List (Adds the Chance/Weight Footer)
        [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
        public class WeightedListElementOdinDrawer : OdinDrawer
        {
            public override bool CanDrawProperty(InspectorProperty property)
            {
                return property.Parent != null && 
                       property.Parent.GetAttribute<WeightedListDisplayAttribute>() != null &&
                       property.Parent.ChildResolver is ICollectionResolver;
            }

            protected override void DrawPropertyLayout(GUIContent label)
            {
                this.CallNextDrawer(label);

                var attr = this.Property.Parent.GetAttribute<WeightedListDisplayAttribute>();
                float totalWeight = 0f;
                foreach (var child in this.Property.Parent.Children)
                {
                    totalWeight += WeightedListAttributeOdinDrawer.GetWeightFromMethod(this.Property.Parent, child.ValueEntry.WeakSmartValue, attr.MethodName);
                }

                float myWeight = WeightedListAttributeOdinDrawer.GetWeightFromMethod(this.Property.Parent, this.Property.ValueEntry.WeakSmartValue, attr.MethodName);
                float chance = totalWeight > 0 ? (myWeight / totalWeight) * 100f : 0f;

                GUIStyle boxStyle = SirenixGUIStyles.CustomizableMessageBox;
                boxStyle.alignment = TextAnchor.LowerLeft;
                
                GUILayout.Label($"Chance: {chance:F1}% | Weight: {myWeight}", boxStyle);
                GUILayout.Space(6);
            }
        }
    }

#else
    // =======================================================================
    // 3. VANILLA UNITY FALLBACK
    // =======================================================================
    
    [CustomPropertyDrawer(typeof(WeightedListDisplayAttribute))]
    public class WeightedListDisplayVanillaDrawer : PropertyDrawer
    {
        private const float FooterHeight = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true) + FooterHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (WeightedListDisplayAttribute)attribute;

            string path = property.propertyPath;
            int arrayIndex = path.LastIndexOf(".Array.data[");
            
            if (arrayIndex < 0)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            string arrayPath = path.Substring(0, arrayIndex);
            SerializedProperty arrayProp = property.serializedObject.FindProperty(arrayPath);

            float totalWeight = 0f;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var elem = arrayProp.GetArrayElementAtIndex(i);
                totalWeight += GetWeightFromMethod(elem, attr.MethodName);
            }

            float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);
            Rect propRect = new Rect(position.x, position.y, position.width, propertyHeight);
            EditorGUI.PropertyField(propRect, property, label, true);

            float myWeight = GetWeightFromMethod(property, attr.MethodName);
            float chance = totalWeight > 0 ? (myWeight / totalWeight) * 100f : 0f;

            Rect footerRect = new Rect(position.x, position.y + propertyHeight + 2, position.width, FooterHeight - 4);
            GUIStyle centeredStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(footerRect, $"Chance: {chance:F1}% | Weight: {myWeight} | (Total: {totalWeight})", centeredStyle);
        }

        private float GetWeightFromMethod(SerializedProperty elementProp, string methodName)
        {
            object targetObject = elementProp.serializedObject.targetObject;
            object elementValue = GetValueFromProperty(elementProp);

            if (targetObject == null || elementValue == null) return 0f;

            var method = targetObject.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method != null)
            {
                try
                {
                    return Convert.ToSingle(method.Invoke(targetObject, new object[] { elementValue }));
                }
                catch { }
            }
            return 0f;
        }

        // Helper to extract the actual object (like your struct) from the SerializedProperty
        private static object GetValueFromProperty(SerializedProperty property)
        {
#if UNITY_2022_1_OR_NEWER
            return property.boxedValue; // Native fast path for modern Unity
#else
            // Fallback parsing for older Unity versions
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] fieldStructure = path.Split('.');
            foreach (string field in fieldStructure)
            {
                if (field.Contains("["))
                {
                    string elementName = field.Substring(0, field.IndexOf("["));
                    int index = Convert.ToInt32(field.Substring(field.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetFieldValue(obj, elementName);
                    if (obj is System.Collections.IList list && index < list.Count) obj = list[index];
                }
                else
                {
                    obj = GetFieldValue(obj, field);
                }
            }
            return obj;
#endif
        }

        private static object GetFieldValue(object source, string name)
        {
            if (source == null) return null;
            var type = source.GetType();
            
            // Try to get it as a field first
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(source);
            
            // If it's not a field, try to get it as a property
            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead) return p.GetValue(source);

            return null;
        }
    }
#endif
#endif
}