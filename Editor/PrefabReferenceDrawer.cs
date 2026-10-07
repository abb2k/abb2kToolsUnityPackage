#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.Editor
{
    [CustomPropertyDrawer(typeof(PrefabReferenceBase<>), true)]
    public class PrefabReferenceDrawer : PropertyDrawer
    {
        private const float EmbeddedInspectorPadding = 8f;
        private const float TextAreaLineHeight = 15f;
        private SerializedObject embeddedSerializedObject;
        private UnityEngine.Object embeddedInspectorTarget;
        private readonly Dictionary<string, Vector2> embeddedInspectorScrollPositions = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float fieldHeight = EditorGUIUtility.singleLineHeight;
            PrefabReferenceInspectorAttribute inspectorAttribute = GetInspectorAttribute();
            if (inspectorAttribute == null || (!inspectorAttribute.AlwaysOpen && !property.isExpanded))
            {
                return fieldHeight;
            }

            return fieldHeight + EditorGUIUtility.standardVerticalSpacing + GetEmbeddedInspectorHeight(property);
        }

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

            Rect fieldRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            PrefabReferenceInspectorAttribute inspectorAttribute = GetInspectorAttribute();
            if (inspectorAttribute != null && !inspectorAttribute.AlwaysOpen)
            {
                const float foldoutWidth = 14f;
                Rect foldoutRect = new Rect(fieldRect.x, fieldRect.y, foldoutWidth, fieldRect.height);
                foldoutRect.x += 12f;
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, false);
                fieldRect.x += foldoutWidth;
                fieldRect.width -= foldoutWidth;
            }

            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(
                originalLabelWidth,
                EditorStyles.label.CalcSize(label).x + 6f);
            Rect fieldPosition;
            try
            {
                fieldPosition = EditorGUI.PrefixLabel(fieldRect, label);
            }
            finally
            {
                EditorGUIUtility.labelWidth = originalLabelWidth;
            }

            const float pickerButtonWidth = 22f;
            const float clearButtonWidth = 18f;

            Rect pickerButtonPosition = new(fieldPosition.xMax - pickerButtonWidth, fieldPosition.y, pickerButtonWidth, fieldPosition.height);

            float clearWidth = currentPrefab != null ? clearButtonWidth : 0f;

            Rect clearButtonPosition = new(pickerButtonPosition.x - clearWidth, fieldPosition.y, clearWidth, fieldPosition.height);
            Rect valuePosition = new(fieldPosition.x, fieldPosition.y, fieldPosition.width - pickerButtonWidth - clearWidth, fieldPosition.height);

            bool isDragHover = HandlePrefabDrag(valuePosition, componentType, property, componentProp, out Component[] draggedComponents, out bool canDrop, out bool didDrop);
            if (didDrop)
            {
                currentPrefab = draggedComponents[0].gameObject;
            }

            GUIContent prefabContent = EditorGUIUtility.ObjectContent(currentPrefab, typeof(GameObject));
            Texture miniIcon = currentPrefab != null
                ? AssetPreview.GetMiniThumbnail(currentPrefab)
                : AssetPreview.GetMiniTypeThumbnail(typeof(GameObject));
            if (miniIcon != null)
            {
                prefabContent.image = miniIcon;
            }

            if (currentPrefab != null && GUI.Button(clearButtonPosition, new GUIContent("x", "Clear prefab"), EditorStyles.miniButton))
            {
                componentProp.objectReferenceValue = null;
            }

            bool openPicker = false;
            Color originalBackgroundColor = GUI.backgroundColor;
            if (isDragHover && canDrop)
            {
                Color blueTint = EditorGUIUtility.isProSkin
                    ? new Color(0.35f, 0.58f, 0.9f, originalBackgroundColor.a)
                    : new Color(0.55f, 0.72f, 0.95f, originalBackgroundColor.a);
                GUI.backgroundColor = Color.Lerp(originalBackgroundColor, blueTint, 0.45f);
            }

            try
            {
                if (GUI.Button(valuePosition, prefabContent, EditorStyles.objectField))
                {
                    if (currentPrefab != null)
                    {
                        if (Event.current.clickCount >= 2)
                        {
                            Selection.activeObject = currentPrefab;
                            AssetDatabase.OpenAsset(currentPrefab);
                        }
                        else
                        {
                            EditorGUIUtility.PingObject(currentPrefab);
                        }
                    }
                    else
                    {
                        openPicker = true;
                    }
                }
            }
            finally
            {
                GUI.backgroundColor = originalBackgroundColor;
            }

            openPicker |= GUI.Button(pickerButtonPosition, new GUIContent("...", $"Select a prefab with a root {componentType.Name} component"), EditorStyles.miniButton);
            if (openPicker)
            {
                Vector2 cursorScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                PrefabReferencePicker.Show(cursorScreenPosition, componentType, currentPrefab, property.serializedObject.targetObjects, property.propertyPath);
            }

            if (inspectorAttribute != null && (inspectorAttribute.AlwaysOpen || property.isExpanded))
            {
                DrawEmbeddedInspector(position, property, componentProp, inspectorAttribute.AlwaysOpen);
            }

            EditorGUI.EndProperty();
        }

        private PrefabReferenceInspectorAttribute GetInspectorAttribute()
        {
            return fieldInfo != null
                ? Attribute.GetCustomAttribute(fieldInfo, typeof(PrefabReferenceInspectorAttribute), true) as PrefabReferenceInspectorAttribute
                : null;
        }

        private static bool HandlePrefabDrag(
            Rect position,
            Type componentType,
            SerializedProperty referenceProperty,
            SerializedProperty componentProperty,
            out Component[] draggedComponents,
            out bool canDrop,
            out bool didDrop)
        {
            Event currentEvent = Event.current;
            draggedComponents = null;
            canDrop = false;
            didDrop = false;
            bool isDragEvent = currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform;
            bool isRepaint = currentEvent.type == EventType.Repaint;
            if ((!isDragEvent && !isRepaint) || !position.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
            if (draggedObjects == null || draggedObjects.Length == 0)
            {
                return false;
            }

            bool acceptsMultiple = draggedObjects.Length == 1 ||
                (referenceProperty.serializedObject.targetObjects.Length == 1 && TryGetListDropTarget(referenceProperty, out _, out _));
            draggedComponents = new Component[draggedObjects.Length];
            canDrop = acceptsMultiple;
            for (int index = 0; index < draggedObjects.Length && canDrop; index++)
            {
                draggedComponents[index] = GetDraggedPrefabComponent(draggedObjects[index], componentType);
                canDrop = draggedComponents[index] != null;
            }

            if (isDragEvent)
            {
                DragAndDrop.visualMode = canDrop ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (currentEvent.type == EventType.DragPerform && canDrop)
                {
                    DragAndDrop.AcceptDrag();
                    AssignDraggedPrefabComponents(referenceProperty, componentProperty, draggedComponents);
                    didDrop = true;
                }

                currentEvent.Use();
            }

            return true;
        }

        private static Component GetDraggedPrefabComponent(UnityEngine.Object draggedObject, Type componentType)
        {
            GameObject draggedGameObject = draggedObject as GameObject;
            if (draggedObject is Component draggedComponent)
            {
                draggedGameObject = draggedComponent.gameObject;
            }

            if (draggedGameObject == null)
            {
                return null;
            }

            GameObject prefabRoot = draggedGameObject.transform.root.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(prefabRoot))
            {
                prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot) as GameObject;
            }

            if (prefabRoot == null || !PrefabUtility.IsPartOfPrefabAsset(prefabRoot))
            {
                return null;
            }

            return prefabRoot.GetComponent(componentType);
        }

        private static void AssignDraggedPrefabComponents(SerializedProperty referenceProperty, SerializedProperty componentProperty, Component[] components)
        {
            if (components.Length == 1 || !TryGetListDropTarget(referenceProperty, out SerializedProperty listProperty, out int elementIndex))
            {
                componentProperty.objectReferenceValue = components[0];
                return;
            }

            for (int index = 1; index < components.Length; index++)
            {
                listProperty.InsertArrayElementAtIndex(elementIndex + index);
            }

            for (int index = 0; index < components.Length; index++)
            {
                SerializedProperty referenceElement = listProperty.GetArrayElementAtIndex(elementIndex + index);
                referenceElement.FindPropertyRelative("_component").objectReferenceValue = components[index];
            }
        }

        private static bool TryGetListDropTarget(SerializedProperty property, out SerializedProperty listProperty, out int elementIndex)
        {
            const string arrayElementMarker = ".Array.data[";
            string propertyPath = property.propertyPath;
            int markerIndex = propertyPath.LastIndexOf(arrayElementMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                listProperty = null;
                elementIndex = -1;
                return false;
            }

            int indexStart = markerIndex + arrayElementMarker.Length;
            int indexEnd = propertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(propertyPath.Substring(indexStart, indexEnd - indexStart), out elementIndex))
            {
                listProperty = null;
                elementIndex = -1;
                return false;
            }

            listProperty = property.serializedObject.FindProperty(propertyPath.Substring(0, markerIndex));
            return listProperty != null && listProperty.isArray;
        }

        private void DrawEmbeddedInspector(Rect position, SerializedProperty property, SerializedProperty componentProperty, bool alwaysOpen)
        {
            if (!alwaysOpen && !property.isExpanded)
            {
                ResetEmbeddedInspector();
                return;
            }

            const float inspectorIndent = 15f;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            Rect inspectorRect = new(
                position.x + inspectorIndent,
                y,
                position.width - inspectorIndent,
                position.height - (y - position.y));
            if (componentProperty.hasMultipleDifferentValues)
            {
                ResetEmbeddedInspector();
                EditorGUI.HelpBox(inspectorRect, "Multiple different prefabs are assigned.", MessageType.Info);
                return;
            }

            Component component = componentProperty.objectReferenceValue as Component;
            if (component == null)
            {
                ResetEmbeddedInspector();
                EditorGUI.HelpBox(inspectorRect, "Assign a prefab to inspect its component.", MessageType.Info);
                return;
            }

            SerializedObject serializedObject = GetEmbeddedSerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            GUI.Box(inspectorRect, GUIContent.none, EditorStyles.helpBox);
            Rect viewRect = new(inspectorRect.x + 4f, inspectorRect.y + 4f, inspectorRect.width - 8f, inspectorRect.height - 8f);
            float contentHeight = GetEmbeddedInspectorContentHeight(serializedObject);
            Rect contentRect = new(0f, 0f, viewRect.width - 16f, Mathf.Max(viewRect.height, contentHeight));
            string scrollKey = GetEmbeddedInspectorKey(property, component);
            embeddedInspectorScrollPositions.TryGetValue(scrollKey, out Vector2 scrollPosition);
            scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            float propertyY = 0f;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, true);
                Rect propertyRect = new(0f, propertyY, contentRect.width, propertyHeight);
                bool isTextArea = iterator.propertyType == SerializedPropertyType.String && GetTextAreaAttribute(iterator) != null;
                if (iterator.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.PropertyField(propertyRect, iterator, true);
                    }
                }
                else if (isTextArea)
                {
                    DrawTextAreaField(propertyRect, iterator);
                }
                else
                {
                    EditorGUI.PropertyField(propertyRect, iterator, true);
                }

                propertyY += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            GUI.EndScrollView();
            embeddedInspectorScrollPositions[scrollKey] = scrollPosition;
            if (serializedObject.ApplyModifiedProperties())
            {
                if (PrefabUtility.IsPartOfPrefabAsset(component))
                {
                    PrefabUtility.SavePrefabAsset(component.transform.root.gameObject);
                }
                else if (PrefabUtility.IsPartOfPrefabInstance(component))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
            }
        }

        private float GetEmbeddedInspectorContentHeight(SerializedObject serializedObject)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            float height = 0f;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                float propertyHeight = EditorGUI.GetPropertyHeight(iterator, true);
                TextAreaAttribute textAreaAttribute = GetTextAreaAttribute(iterator);
                if (textAreaAttribute != null)
                {
                    int maxLines = Mathf.Max(textAreaAttribute.minLines, textAreaAttribute.maxLines);
                    float maxTextAreaHeight = EditorGUIUtility.singleLineHeight * 2f + (maxLines - 1) * TextAreaLineHeight;
                    propertyHeight = Mathf.Max(propertyHeight, maxTextAreaHeight);
                }

                height += propertyHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private float GetEmbeddedInspectorHeight(SerializedProperty property)
        {
            SerializedProperty componentProperty = property.FindPropertyRelative("_component");
            if (componentProperty == null || componentProperty.hasMultipleDifferentValues || componentProperty.objectReferenceValue is not Component component)
            {
                return EditorGUIUtility.singleLineHeight * 2f + EmbeddedInspectorPadding;
            }

            SerializedObject serializedObject = GetEmbeddedSerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            return GetEmbeddedInspectorContentHeight(serializedObject) + EmbeddedInspectorPadding;
        }

        private SerializedObject GetEmbeddedSerializedObject(Component component)
        {
            if (embeddedSerializedObject == null || embeddedInspectorTarget != component)
            {
                embeddedSerializedObject = new SerializedObject(component);
                embeddedInspectorTarget = component;
            }

            return embeddedSerializedObject;
        }

        private static string GetEmbeddedInspectorKey(SerializedProperty property, Component component)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}:{component.GetInstanceID()}";
        }

        private static TextAreaAttribute GetTextAreaAttribute(SerializedProperty property)
        {
            Type currentType = property.serializedObject.targetObject.GetType();
            FieldInfo fieldInfo = null;
            string[] pathSegments = property.propertyPath.Split('.');
            for (int index = 0; index < pathSegments.Length; index++)
            {
                string pathSegment = pathSegments[index];
                if (pathSegment == "Array")
                {
                    index++;
                    currentType = GetCollectionElementType(currentType);
                    continue;
                }

                if (pathSegment.StartsWith("data[", StringComparison.Ordinal))
                {
                    continue;
                }

                fieldInfo = GetFieldInfo(currentType, pathSegment);
                if (fieldInfo == null)
                {
                    return null;
                }

                currentType = fieldInfo.FieldType;
            }

            return fieldInfo?.GetCustomAttribute<TextAreaAttribute>(true);
        }

        private static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (fieldInfo != null)
                {
                    return fieldInfo;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Type GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            return collectionType.IsGenericType ? collectionType.GetGenericArguments()[0] : collectionType;
        }

        private static void DrawTextAreaField(Rect position, SerializedProperty property)
        {
            GUIContent label = new(property.displayName, property.tooltip);
            label = EditorGUI.BeginProperty(position, label, property);
            bool previousShowMixedValue = EditorGUI.showMixedValue;
            int previousIndentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUI.indentLevel = 0;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

                Rect labelPosition = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                Rect textAreaPosition = new(position.x, labelPosition.yMax, position.width, position.height - labelPosition.height);
                EditorGUI.LabelField(labelPosition, label);

                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TextArea(textAreaPosition, property.stringValue, EditorStyles.textArea);
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = value;
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousShowMixedValue;
                EditorGUI.indentLevel = previousIndentLevel;
                EditorGUI.EndProperty();
            }
        }

        private void ResetEmbeddedInspector()
        {
            embeddedSerializedObject = null;
            embeddedInspectorTarget = null;
        }
    }

    internal sealed class PrefabReferencePicker : EditorWindow
    {
        private const string GridViewPreferenceKey = "Abb2kTools.PrefabReferencePicker.GridView";
        private static readonly string[] ViewModeLabels = { "List", "Grid" };
        private Type componentType;
        [SerializeField] private string componentTypeName;
        [SerializeField] private GameObject currentPrefab;
        [SerializeField] private List<GameObject> validPrefabs;
        [SerializeField] private UnityEngine.Object[] targetObjects;
        [SerializeField] private string propertyPath;
        private string searchText = string.Empty;
        private Vector2 scrollPosition;
        private bool gridView;
        private bool hasPressedGridItem;
        private int pressedGridItemId;

        private void OnLostFocus()
        {
            hasPressedGridItem = false;
            Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            if (TryRestoreComponentType())
            {
                RefreshValidPrefabs();
            }
        }

        public static void Show(Vector2 screenPosition, Type componentType, GameObject currentPrefab, UnityEngine.Object[] targetObjects, string propertyPath)
        {
            PrefabReferencePicker picker = CreateInstance<PrefabReferencePicker>();
            picker.componentType = componentType;
            picker.componentTypeName = componentType.AssemblyQualifiedName;
            picker.currentPrefab = currentPrefab;
            picker.targetObjects = targetObjects;
            picker.propertyPath = propertyPath;
            picker.gridView = EditorPrefs.GetBool(GridViewPreferenceKey, false);
            picker.titleContent = new GUIContent($"Select {componentType.Name} Prefab");
            picker.RefreshValidPrefabs();

            picker.minSize = new Vector2(320f, 240f);
            picker.ShowUtility();
            picker.position = new Rect(screenPosition, new Vector2(380f, 420f));
        }

        private void OnGUI()
        {
            if (!TryRestoreComponentType())
            {
                EditorGUILayout.HelpBox("The component type could not be restored after the project reload. Close and reopen this picker.", MessageType.Warning);
                return;
            }

            if (validPrefabs == null)
            {
                RefreshValidPrefabs();
            }

            EventType eventType = Event.current.type;
            if (eventType == EventType.MouseMove)
            {
                Repaint();
            }
            else if (eventType == EventType.MouseUp && Event.current.button == 0)
            {
                hasPressedGridItem = false;
                Repaint();
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            int selectedView = GUILayout.Toolbar(gridView ? 1 : 0, ViewModeLabels, EditorStyles.toolbarButton, GUILayout.Width(84f));
            if (selectedView != (gridView ? 1 : 0))
            {
                gridView = selectedView == 1;
                EditorPrefs.SetBool(GridViewPreferenceKey, gridView);
            }

            bool clearSearch = GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48f));
            EditorGUILayout.EndHorizontal();
            if (clearSearch)
            {
                searchText = string.Empty;
                GUI.FocusControl(null);
                Repaint();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            List<GameObject> filteredPrefabs = validPrefabs
                .Where(prefab => prefab.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                AssetDatabase.GetAssetPath(prefab).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            bool showNone = string.IsNullOrEmpty(searchText) || "None".IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            if (gridView)
            {
                DrawGridOptions(filteredPrefabs, showNone);
            }
            else
            {
                if (showNone)
                {
                    DrawPrefabOption(null, "None");
                }

                foreach (GameObject prefab in filteredPrefabs)
                {
                    DrawPrefabOption(prefab, prefab.name);
                }
            }

            if (filteredPrefabs.Count == 0 && !showNone)
            {
                EditorGUILayout.HelpBox("No matching valid prefabs.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGridOptions(List<GameObject> prefabs, bool showNone)
        {
            const float tileHeight = 112f;
            const float minimumTileWidth = 100f;
            const float spacing = 4f;

            float gridWidth = Mathf.Max(minimumTileWidth, position.width - 20f);
            int columns = Mathf.Max(1, Mathf.FloorToInt((gridWidth + spacing) / (minimumTileWidth + spacing)));
            float tileWidth = (gridWidth - spacing * (columns - 1)) / columns;
            int itemCount = prefabs.Count + (showNone ? 1 : 0);
            int itemIndex = 0;

            while (itemIndex < itemCount)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < columns && itemIndex < itemCount; column++, itemIndex++)
                {
                    Rect tilePosition = GUILayoutUtility.GetRect(tileWidth, tileHeight, GUILayout.Width(tileWidth), GUILayout.Height(tileHeight));
                    if (showNone && itemIndex == 0)
                    {
                        DrawGridPrefabOption(tilePosition, null, "None");
                    }
                    else
                    {
                        int prefabIndex = itemIndex - (showNone ? 1 : 0);
                        GameObject prefab = prefabs[prefabIndex];
                        DrawGridPrefabOption(tilePosition, prefab, prefab.name);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(spacing);
            }
        }

        private void DrawGridPrefabOption(Rect tilePosition, GameObject prefab, string label)
        {
            string tooltip = prefab != null ? AssetDatabase.GetAssetPath(prefab) : "Clear prefab reference";
            bool hovered = tilePosition.Contains(Event.current.mousePosition);
            int itemId = prefab != null ? prefab.GetInstanceID() : 0;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hovered)
            {
                pressedGridItemId = itemId;
                hasPressedGridItem = true;
                Repaint();
            }

            bool pressed = hovered && hasPressedGridItem && pressedGridItemId == itemId;
            if (hovered)
            {
                EditorGUIUtility.AddCursorRect(tilePosition, MouseCursor.Link);
            }

            if (GUI.Button(tilePosition, new GUIContent(string.Empty, tooltip), EditorStyles.helpBox))
            {
                AssignPrefab(prefab);
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (pressed)
                {
                    Color pressedColor = EditorGUIUtility.isProSkin
                        ? new Color(0.12f, 0.38f, 0.68f, 0.45f)
                        : new Color(0.12f, 0.42f, 0.78f, 0.3f);
                    EditorGUI.DrawRect(tilePosition, pressedColor);
                }

                if (hovered)
                {
                    const float outlineWidth = 1f;
                    Color outlineColor = new Color32(120, 120, 120, 255);
                    EditorGUI.DrawRect(new Rect(tilePosition.x, tilePosition.y, tilePosition.width, outlineWidth), outlineColor);
                    EditorGUI.DrawRect(new Rect(tilePosition.x, tilePosition.yMax - outlineWidth, tilePosition.width, outlineWidth), outlineColor);
                    EditorGUI.DrawRect(new Rect(tilePosition.x, tilePosition.y, outlineWidth, tilePosition.height), outlineColor);
                    EditorGUI.DrawRect(new Rect(tilePosition.xMax - outlineWidth, tilePosition.y, outlineWidth, tilePosition.height), outlineColor);
                }
            }

            Rect previewPosition = new(tilePosition.x + 5f, tilePosition.y + 5f, tilePosition.width - 10f, tilePosition.height - 31f);
            Texture preview = prefab != null ? AssetPreview.GetAssetPreview(prefab) : null;
            if (preview != null)
            {
                GUI.DrawTexture(previewPosition, preview, ScaleMode.ScaleToFit, true);
            }
            else
            {
                if (prefab != null && AssetPreview.IsLoadingAssetPreview(prefab.GetInstanceID()))
                {
                    Repaint();
                }

                Texture icon = prefab != null
                    ? AssetPreview.GetMiniThumbnail(prefab)
                    : AssetPreview.GetMiniTypeThumbnail(typeof(GameObject));
                if (icon != null)
                {
                    Rect iconPosition = new Rect(previewPosition.center.x - 16f, previewPosition.center.y - 16f, 32f, 32f);
                    GUI.DrawTexture(iconPosition, icon, ScaleMode.ScaleToFit, true);
                }
            }

            Rect labelPosition = new Rect(tilePosition.x + 3f, tilePosition.yMax - 23f, tilePosition.width - 6f, 18f);
            GUI.Label(labelPosition, new GUIContent(label, tooltip), EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawPrefabOption(GameObject prefab, string label)
        {
            GUIContent content = prefab != null
                ? EditorGUIUtility.ObjectContent(prefab, typeof(GameObject))
                : new GUIContent(label, EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image);
            content.text = label;

            if (GUILayout.Button(content, EditorStyles.objectField, GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                AssignPrefab(prefab);
            }
        }

        private void AssignPrefab(GameObject prefab)
        {
            if (!TryRestoreComponentType())
            {
                Debug.LogWarning("[PrefabReference] The component type could not be restored after the project reload. Reopen the prefab selector.");
                return;
            }

            if (targetObjects == null || string.IsNullOrEmpty(propertyPath))
            {
                Debug.LogWarning("[PrefabReference] The prefab selector state was lost during the project reload. Reopen the selector.");
                Close();
                return;
            } 

            Component component = prefab != null ? prefab.GetComponent(componentType) : null;
            if (prefab != null && component == null)
            {
                return;
            }

            foreach (UnityEngine.Object targetObject in targetObjects)
            {
                if (targetObject == null)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(targetObject);
                serializedObject.Update();
                SerializedProperty referenceProperty = serializedObject.FindProperty(propertyPath)?.FindPropertyRelative("_component");
                if (referenceProperty == null)
                {
                    continue;
                }

                referenceProperty.objectReferenceValue = component;
                serializedObject.ApplyModifiedProperties();
            }

            Close();
        }

        private bool TryRestoreComponentType()
        {
            if (componentType == null && !string.IsNullOrEmpty(componentTypeName))
            {
                componentType = Type.GetType(componentTypeName, false);
            }

            return componentType != null;
        }

        private void RefreshValidPrefabs()
        {
            validPrefabs = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null && prefab.GetComponent(componentType) != null)
                .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
#endif