using System;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
#endif
#endif

namespace Abb2kTools.Collections
{
    /// <summary>
    /// Apply this to any List/Array field, OR directly to a custom List Class definition.
    /// Usage: [WeightedListDisplay(nameof(WeightFunction))]
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
    public class WeightedListDisplayAttribute : PropertyAttribute
    {
        public string MethodName { get; private set; }

        public WeightedListDisplayAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

#if UNITY_EDITOR

#if ODIN_INSPECTOR

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

            object listObject = listProperty.ValueEntry?.WeakSmartValue;
            object parentObject = listProperty.ParentValues.Count > 0 ? listProperty.ParentValues[0] : null;
            object rootObject = listProperty.SerializationRoot?.ValueEntry?.WeakSmartValue;

            object[] targetsToTry = new object[] { listObject, parentObject, rootObject };

            foreach (var target in targetsToTry)
            {
                if (target == null) continue;

                var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (method != null)
                {
                    try
                    {
                        return Convert.ToSingle(method.Invoke(target, new object[] { elementValue }));
                    }
                    catch { }
                }
            }

            return 0f;
        }
    }

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
            boxStyle.alignment = TextAnchor.MiddleCenter;
            
            GUILayout.Label($"Chance: {chance:F1}% | Weight: {myWeight}", boxStyle);
            GUILayout.Space(6);
        }
    }

#else
    
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

        private float GetWeightFromMethod(
            SerializedProperty elementProp,
            string methodName)
        {
            object elementValue = GetValueFromProperty(elementProp);

            if (elementValue == null)
                return 0f;

            // Walk up the property hierarchy until we find a method
            string path = elementProp.propertyPath;

            while (!string.IsNullOrEmpty(path))
            {
                SerializedProperty currentProp =
                    elementProp.serializedObject.FindProperty(path);

                if (currentProp != null)
                {
                    object currentObj = GetValueFromProperty(currentProp);

                    if (currentObj != null)
                    {
                        var method = currentObj.GetType().GetMethod(
                            methodName,
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.Instance |
                            BindingFlags.Static);

                        if (method != null)
                        {
                            try
                            {
                                return Convert.ToSingle(
                                    method.Invoke(
                                        currentObj,
                                        new object[] { elementValue }));
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }
                }

                int lastDot = path.LastIndexOf('.');
                if (lastDot < 0)
                    break;

                path = path.Substring(0, lastDot);
            }

            // Fallback to MonoBehaviour
            object target = elementProp.serializedObject.targetObject;

            var fallbackMethod = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static);

            if (fallbackMethod != null)
            {
                try
                {
                    return Convert.ToSingle(
                        fallbackMethod.Invoke(
                            target,
                            new object[] { elementValue }));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            return 0f;
        }

        private static object GetValueFromProperty(SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] fieldStructure = path.Split('.');
            
            foreach (string field in fieldStructure)
            {
                if (obj == null) return null;

                if (field.Contains("["))
                {
                    string elementName = field.Substring(0, field.IndexOf("["));
                    int index = Convert.ToInt32(field.Substring(field.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetFieldValue(obj, elementName);
                    
                    if (obj is System.Collections.IList list && index < list.Count)
                    {
                        obj = list[index];
                    }
                }
                else
                {
                    obj = GetFieldValue(obj, field);
                }
            }
            return obj;
        }

        private static object GetFieldValue(object source, string name)
        {
            if (source == null) return null;
            var type = source.GetType();
            
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(source);
            
            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead) return p.GetValue(source);

            return null;
        }
    }
#endif
#endif
}