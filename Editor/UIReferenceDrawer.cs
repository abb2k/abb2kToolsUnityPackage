using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace Abb2kTools.UI
{
[CustomPropertyDrawer(typeof(UIReference<>), true)]
public class UIReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var docProp = property.FindPropertyRelative("Document");
        var pathProp = property.FindPropertyRelative("_elementPath");
        var nameProp = property.FindPropertyRelative("_elementName");
        var keyProp = property.FindPropertyRelative("_viewDataKey");
        var sibIndexProp = property.FindPropertyRelative("_siblingIndex");
        var childCountProp = property.FindPropertyRelative("_parentChildCount");

        float labelWidth = EditorGUIUtility.labelWidth;
        float fullWidth = position.width - labelWidth;
        float docWidth = fullWidth;
        float popWidth = fullWidth * 0.7f;
        float spacing = 5f;

        if (docProp.objectReferenceValue is UIDocument tempDoc && tempDoc.rootVisualElement != null && tempDoc.visualTreeAsset != null)
        {
            docWidth = fullWidth * 0.3f;
        }

        var docRect = new Rect(position.x, position.y, labelWidth + docWidth, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(docRect, docProp, label);

        if (docProp.objectReferenceValue is UIDocument doc && doc.rootVisualElement != null && doc.visualTreeAsset != null)
        {
            EditorGUI.BeginChangeCheck();

            var dropdownRect = new Rect(
                position.x + labelWidth + docWidth + spacing, 
                position.y, 
                popWidth - spacing, 
                EditorGUIUtility.singleLineHeight
            );
            
            var elements = doc.rootVisualElement.Query().Build();
            
            List<string> popupNames = new() { "None" };
            List<string> viewDataKeys = new() { "" };
            List<string> actualNames = new() { "" };
            List<string> fullPaths = new() { "" };
            List<string> parentPaths = new() { "" };
            List<int> siblingIndices = new() { -1 };
            List<int> parentChildCounts = new() { -1 };

            Dictionary<string, int> displayCounts = new();

            foreach (var element in elements)
            {
                var targetType = GetGenericTypeArgument(property);
                if (targetType != null && !targetType.IsAssignableFrom(element.GetType())) continue;

                string path = GetPath(element);
                string elName = element.name ?? "";
                string key = element.viewDataKey ?? "";
                string typeName = element.GetType().Name;

                string baseDisplayName = string.IsNullOrEmpty(elName) ? $"<{typeName}>" : elName;
                if (!displayCounts.ContainsKey(baseDisplayName)) displayCounts[baseDisplayName] = 0;
                displayCounts[baseDisplayName]++;
                
                string uniqueDisplay = displayCounts[baseDisplayName] > 1 ? $"{baseDisplayName} ({displayCounts[baseDisplayName]})" : baseDisplayName;

                string keyIndicator = string.IsNullOrEmpty(key) ? "" : " [Keyed]";
                
                if (string.IsNullOrEmpty(elName)) {
                    popupNames.Add($"{uniqueDisplay}{keyIndicator}");
                } else {
                    popupNames.Add($"{uniqueDisplay} <{typeName}>{keyIndicator}"); 
                }

                viewDataKeys.Add(key);
                actualNames.Add(elName);
                fullPaths.Add(path);
                
                parentPaths.Add(GetParentPath(path));
                siblingIndices.Add(element.parent != null ? element.parent.IndexOf(element) : 0);
                parentChildCounts.Add(element.parent != null ? element.parent.childCount : 0);
            }
            
            string savedKey = keyProp.stringValue;
            string savedName = nameProp.stringValue;
            string savedPath = pathProp.stringValue;
            string savedStruct = GetStructPath(savedPath);
            string savedParentPath = GetParentPath(savedPath);

            int bestIndex = 0;
            int maxScore = -1;

            for (int i = 1; i < fullPaths.Count; i++)
            {
                if (!string.IsNullOrEmpty(savedKey) && viewDataKeys[i] == savedKey) { bestIndex = i; break; }
                if (!string.IsNullOrEmpty(savedPath) && fullPaths[i] == savedPath) { bestIndex = i; break; }

                int score = 0;
                string candStruct = GetStructPath(fullPaths[i]);

                if (!string.IsNullOrEmpty(savedStruct) && candStruct == savedStruct) score += 5000;
                if (!string.IsNullOrEmpty(savedName) && actualNames[i] == savedName) score += 1000;
                if (parentPaths[i] == savedParentPath) score += 300;
                if (siblingIndices[i] == sibIndexProp.intValue) score += 100;
                if (parentChildCounts[i] == childCountProp.intValue) score += 50;
                
                score += GetPathSimilarity(savedPath, fullPaths[i]) * 10;

                if (score > maxScore)
                {
                    maxScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex > 0)
            {
                bool needsHealing = false;

                if (fullPaths[bestIndex] != savedPath) { pathProp.stringValue = fullPaths[bestIndex]; needsHealing = true; }
                if (actualNames[bestIndex] != savedName) { nameProp.stringValue = actualNames[bestIndex]; needsHealing = true; }
                if (viewDataKeys[bestIndex] != savedKey) { keyProp.stringValue = viewDataKeys[bestIndex]; needsHealing = true; }
                if (siblingIndices[bestIndex] != sibIndexProp.intValue) { sibIndexProp.intValue = siblingIndices[bestIndex]; needsHealing = true; }
                if (parentChildCounts[bestIndex] != childCountProp.intValue) { childCountProp.intValue = parentChildCounts[bestIndex]; needsHealing = true; }

                if (needsHealing) property.serializedObject.ApplyModifiedProperties();
            }

            var newIndex = EditorGUI.Popup(dropdownRect, bestIndex, popupNames.ToArray());

            // Save Manual User Changes
            if (EditorGUI.EndChangeCheck())
            {
                keyProp.stringValue = viewDataKeys[newIndex];
                nameProp.stringValue = actualNames[newIndex];
                pathProp.stringValue = fullPaths[newIndex];
                sibIndexProp.intValue = siblingIndices[newIndex];
                childCountProp.intValue = parentChildCounts[newIndex];
                
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 2;

    private static Type GetGenericTypeArgument(SerializedProperty property)
    {
        object target = property.serializedObject.targetObject;
        var fieldInfo = target.GetType().GetField(property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (fieldInfo != null && fieldInfo.FieldType.IsGenericType)
        {
            return fieldInfo.FieldType.GetGenericArguments()[0];
        }
        return null;
    }

    private static string GetPath(VisualElement element)
    {
        List<string> pathParts = new();
        VisualElement current = element;

        while (current != null && current.parent != null)
        {
            int index = current.parent.IndexOf(current);
            var partName = string.IsNullOrEmpty(current.name) ? $"[{current.GetType().Name}]" : current.name;
            pathParts.Add($"{partName}:{index}");
            current = current.parent;
        }

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }

    private static string GetParentPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return "";
        int lastSlash = fullPath.LastIndexOf('/');
        return lastSlash > 0 ? fullPath.Substring(0, lastSlash) : "";
    }

    private static string GetStructPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return "";
        var parts = fullPath.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            int idx = parts[i].LastIndexOf(':');
            if (idx >= 0) parts[i] = parts[i].Substring(idx + 1);
        }
        return string.Join("/", parts);
    }

    private static int GetPathSimilarity(string pathA, string pathB)
    {
        if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB)) return 0;
        var partsA = pathA.Split('/');
        var partsB = pathB.Split('/');
        int matches = 0;
        for (int i = 0; i < Math.Min(partsA.Length, partsB.Length); i++)
        {
            if (partsA[i] == partsB[i]) matches++;
        }
        return matches;
    }
}
}