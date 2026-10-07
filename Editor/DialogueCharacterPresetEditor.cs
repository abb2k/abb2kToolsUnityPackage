#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomEditor(typeof(DialogueCharacterPreset))]
public sealed class DialogueCharacterPresetEditor : Editor
{
    private int _selectedVariantIndex;

    public override void OnInspectorGUI()
    {
        var preset = (DialogueCharacterPreset)target;
        var contentType = DialogueContentProjectSettings.Load()?.DefaultContentType;
        if (contentType == null)
        {
            EditorGUILayout.HelpBox("Choose a dialogue content type in Project Settings before editing this preset.", MessageType.Info);
            if (GUILayout.Button("Open Dialogue Content Settings"))
                SettingsService.OpenProjectSettings("Project/Abb2kTools/Dialogue Content");
            return;
        }

        if (preset.content?.ContentType != contentType)
        {
            Undo.RecordObject(preset, "Set Character Preset Content Type");
            preset.content ??= new DialogueContentValue();
            preset.content.SetType(contentType);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        preset.variants ??= new System.Collections.Generic.List<DialogueCharacterPresetVariant>();
        var variants = preset.variants.Where(variant => variant != null).ToList();
        var labels = new[] { "Default" }.Concat(variants.Select(variant => variant.variantName)).ToArray();
        _selectedVariantIndex = Mathf.Clamp(_selectedVariantIndex, 0, labels.Length - 1);

        EditorGUILayout.LabelField($"Character Preset ({contentType.Name})", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _selectedVariantIndex = EditorGUILayout.Popup("Editing", _selectedVariantIndex, labels);
        if (GUILayout.Button("+", GUILayout.Width(28)))
        {
            CreateVariant(preset, contentType);
            variants = preset.variants.Where(variant => variant != null).ToList();
            _selectedVariantIndex = variants.Count;
        }
        using (new EditorGUI.DisabledScope(_selectedVariantIndex == 0))
        {
            if (GUILayout.Button("-", GUILayout.Width(28)))
            {
                DeleteVariant(preset, variants[_selectedVariantIndex - 1]);
                _selectedVariantIndex = 0;
                variants = preset.variants.Where(variant => variant != null).ToList();
            }
        }
        EditorGUILayout.EndHorizontal();

        var selectedVariant = _selectedVariantIndex > 0
            ? variants[_selectedVariantIndex - 1]
            : null;
        if (selectedVariant != null)
        {
            string variantName = EditorGUILayout.TextField("Variant Name", selectedVariant.variantName);
            if (variantName != selectedVariant.variantName)
            {
                Undo.RecordObject(selectedVariant, "Rename Character Preset Variant");
                selectedVariant.variantName = variantName.Trim();
                selectedVariant.name = $"{preset.name} - {selectedVariant.variantName}";
                EditorUtility.SetDirty(selectedVariant);
                AssetDatabase.SaveAssets();
            }
        }

        var contentOwner = selectedVariant != null ? (UnityEngine.Object)selectedVariant : preset;
        var content = selectedVariant != null ? selectedVariant.content : preset.content;
        if (content?.ContentType != contentType)
        {
            Undo.RecordObject(contentOwner, "Set Character Preset Variant Content Type");
            content ??= new DialogueContentValue();
            content.SetType(contentType);
            if (selectedVariant != null) selectedVariant.content = content;
            else preset.content = content;
            EditorUtility.SetDirty(contentOwner);
            AssetDatabase.SaveAssets();
        }

        var contentObject = new SerializedObject(contentOwner);
        contentObject.Update();
        var contentProperty = contentObject.FindProperty("content.value");
        bool contentChanged = DialogueLinkEditor.DrawTypedValue(
            contentProperty, contentType, "Values", presetOnly: true);
        bool serializedChanged = contentObject.ApplyModifiedProperties();
        if (contentChanged || serializedChanged)
        {
            EditorUtility.SetDirty(contentOwner);
            AssetDatabase.SaveAssets();
        }
    }

    private static void CreateVariant(DialogueCharacterPreset preset, System.Type contentType)
    {
        var assetPath = AssetDatabase.GetAssetPath(preset);
        if (string.IsNullOrEmpty(assetPath)) return;

        preset.variants ??= new System.Collections.Generic.List<DialogueCharacterPresetVariant>();
        int suffix = preset.variants.Count + 1;
        string variantName = $"Variant {suffix}";
        while (preset.variants.Any(variant => variant != null && variant.variantName == variantName))
            variantName = $"Variant {++suffix}";

        var variant = CreateInstance<DialogueCharacterPresetVariant>();
        variant.variantName = variantName;
        variant.name = $"{preset.name} - {variantName}";
        variant.content = preset.content?.Clone() ?? new DialogueContentValue();
        variant.content.characterPreset = null;
        variant.content.characterPresetVariant = null;
        if (variant.content.ContentType != contentType)
            variant.content.SetType(contentType);

        Undo.RecordObject(preset, "Add Character Preset Variant");
        Undo.RegisterCreatedObjectUndo(variant, "Add Character Preset Variant");
        AssetDatabase.AddObjectToAsset(variant, preset);
        preset.variants.Add(variant);
        EditorUtility.SetDirty(preset);
        EditorUtility.SetDirty(variant);
        AssetDatabase.SaveAssets();
    }

    private static void DeleteVariant(DialogueCharacterPreset preset, DialogueCharacterPresetVariant variant)
    {
        if (variant == null) return;
        Undo.RecordObject(preset, "Delete Character Preset Variant");
        preset.variants.Remove(variant);
        Undo.DestroyObjectImmediate(variant);
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
    }
}
#endif