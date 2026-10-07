#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.Editor
{
    [CustomPropertyDrawer(typeof(PrefabReferenceBase<>), true)]
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

            Rect fieldPosition = EditorGUI.PrefixLabel(position, label);

            const float pickerButtonWidth = 22f;
            const float clearButtonWidth = 18f;

            Rect pickerButtonPosition = new(fieldPosition.xMax - pickerButtonWidth, fieldPosition.y, pickerButtonWidth, fieldPosition.height);

            float clearWidth = currentPrefab != null ? clearButtonWidth : 0f;

            Rect clearButtonPosition = new(pickerButtonPosition.x - clearWidth, fieldPosition.y, clearWidth, fieldPosition.height);
            Rect valuePosition = new(fieldPosition.x, fieldPosition.y, fieldPosition.width - pickerButtonWidth - clearWidth, fieldPosition.height);

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

            openPicker |= GUI.Button(pickerButtonPosition, new GUIContent("...", $"Select a prefab with a root {componentType.Name} component"), EditorStyles.miniButton);
            if (openPicker)
            {
                Vector2 cursorScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                PrefabReferencePicker.Show(cursorScreenPosition, componentType, currentPrefab, property.serializedObject.targetObjects, property.propertyPath);
            }

            EditorGUI.EndProperty();
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