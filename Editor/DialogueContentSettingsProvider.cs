#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

public static class DialogueContentSettingsProvider
{
    private const string SettingsPath = "Assets/Resources/DialogueContentProjectSettings.asset";
    private const string ProjectSettingsPath = "Project/Abb2kTools/Dialogue Content";

    [SettingsProvider]
    public static SettingsProvider CreateProvider()
    {
        return new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
        {
            label = "Dialogue Content",
            guiHandler = DrawSettings
        };
    }

    [MenuItem("Abb2kTools/Dialogue Content Settings")]
    private static void OpenSettings()
    {
        SettingsService.OpenProjectSettings(ProjectSettingsPath);
    }

    private static void DrawSettings(string searchContext)
    {
        var settings = DialogueContentProjectSettings.Load();
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Choose the content type used by dialogue nodes. Each node stores its own content values.", MessageType.Info);
            if (GUILayout.Button("Create Dialogue Content Settings"))
                CreateSettingsAsset();
            return;
        }

        EditorGUILayout.LabelField("Dialogue Content Type", EditorStyles.boldLabel);
        var type = settings.DefaultContentType;
        using (new EditorGUILayout.HorizontalScope())
        {
            string buttonLabel = type == null ? "Select [DialogueContent] type..." : type.Name;
            if (GUILayout.Button(buttonLabel, EditorStyles.popup))
                ShowTypeMenu(settings);
        }

        if (type == null)
        {
            EditorGUILayout.HelpBox("Choose a serializable class or struct marked with [DialogueContent]. Each dialogue node will have separate values.", MessageType.None);
        }
    }

    private static void ShowTypeMenu(DialogueContentProjectSettings settings)
    {
        var types = TypeCache.GetTypesWithAttribute<DialogueContentAttribute>()
            .Where(type => type.IsDefined(typeof(SerializableAttribute), false) &&
                !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters &&
                (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null))
            .OrderBy(type => type.FullName)
            .ToArray();
        var menu = new GenericMenu();
        if (types.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("No serializable content types found"));
        }
        else
        {
            foreach (var type in types)
                menu.AddItem(new GUIContent(type.FullName), type == settings.DefaultContentType,
                    () => SetDefaultType(settings, type));
        }
        menu.ShowAsContext();
    }

    private static void SetDefaultType(DialogueContentProjectSettings settings, Type type)
    {
        Undo.RecordObject(settings, "Change Default Dialogue Content Type");
        settings.defaultContentTypeName = type.AssemblyQualifiedName;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void CreateSettingsAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var settings = ScriptableObject.CreateInstance<DialogueContentProjectSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(settings);
    }
}
#endif