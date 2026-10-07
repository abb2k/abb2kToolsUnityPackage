#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(DialogueContentValue))]
public sealed class DialogueContentValueDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var contentProperty = property.Copy();
        IMGUIContainer contentContainer = null;
        contentContainer = new IMGUIContainer(() =>
        {
            var serializedObject = contentProperty.serializedObject;
            serializedObject.Update();
            var valueProperty = contentProperty.FindPropertyRelative("value");
            var typeName = valueProperty?.FindPropertyRelative("customTypeName")?.stringValue;
            var type = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            var bindingsProperty = contentProperty.FindPropertyRelative("fieldBindings");
            var localOverridesProperty = contentProperty.FindPropertyRelative("localPresetFieldOverrides");
            bool isCharacterPreset = serializedObject.targetObject is DialogueCharacterPreset;
            var presetProperty = contentProperty.FindPropertyRelative("characterPreset");
            var variantProperty = contentProperty.FindPropertyRelative("characterPresetVariant");

            if (!isCharacterPreset)
                EditorGUILayout.PropertyField(presetProperty, new GUIContent("Character Preset"));

            var preset = presetProperty?.objectReferenceValue as DialogueCharacterPreset;
            DialogueCharacterPresetVariant selectedVariant = null;
            if (!isCharacterPreset && preset != null)
            {
                DrawVariantSelector(EditorGUILayout.GetControlRect(), preset, variantProperty);
                selectedVariant = variantProperty?.objectReferenceValue as DialogueCharacterPresetVariant;
            }
            else if (variantProperty != null)
            {
                variantProperty.objectReferenceValue = null;
            }

            var graphNode = contentContainer.GetFirstAncestorOfType<DialogueNode>();
            var presetContent = preset?.GetContent(selectedVariant);
            object presetValue = presetContent != null && type != null &&
                presetContent.ContentType == type
                ? presetContent.Resolve()
                : null;

            if (type == null)
                EditorGUILayout.LabelField("No dialogue content type is selected.", EditorStyles.label);
            else
                DialogueLinkEditor.DrawTypedValue(valueProperty, type, "Content",
                    fieldBindings: bindingsProperty,
                    presetOnly: isCharacterPreset,
                    localFieldOverrides: isCharacterPreset ? null : localOverridesProperty,
                    presetValue: presetValue,
                    inputPortsChanged: graphNode != null ? graphNode.RefreshInputParameterPorts : null);

            if (serializedObject.ApplyModifiedProperties() &&
                serializedObject.targetObject != null)
                EditorUtility.SetDirty(serializedObject.targetObject);
        });
        return contentContainer;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var preset = property.FindPropertyRelative("characterPreset");
        var variant = property.FindPropertyRelative("characterPresetVariant");
        var value = property.FindPropertyRelative("value");
        var presetRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(presetRect, preset, new GUIContent("Character Preset"));
        float nextY = presetRect.yMax + EditorGUIUtility.standardVerticalSpacing;
        var presetAsset = preset.objectReferenceValue as DialogueCharacterPreset;
        if (presetAsset != null)
        {
            var variantRect = new Rect(position.x, nextY, position.width, EditorGUIUtility.singleLineHeight);
            DrawVariantSelector(variantRect, presetAsset, variant);
            nextY = variantRect.yMax + EditorGUIUtility.standardVerticalSpacing;
        }
        else
        {
            variant.objectReferenceValue = null;
        }
        var valueRect = new Rect(position.x, nextY,
            position.width, EditorGUI.GetPropertyHeight(value, label, true));
        EditorGUI.PropertyField(valueRect, value, label, includeChildren: true);
    }

    private static DialogueCharacterPresetVariant DrawVariantSelector(
        Rect rect,
        DialogueCharacterPreset preset,
        SerializedProperty variantProperty)
    {
        if (preset == null || variantProperty == null) return null;
        var variants = (preset.variants ?? new System.Collections.Generic.List<DialogueCharacterPresetVariant>())
            .Where(variant => variant != null)
            .ToList();
        var labels = new[] { "Default" }.Concat(variants.Select(variant =>
            string.IsNullOrEmpty(variant.variantName) ? variant.name : variant.variantName)).ToArray();
        var currentVariant = variantProperty.objectReferenceValue as DialogueCharacterPresetVariant;
        int selectedIndex = variants.IndexOf(currentVariant) + 1;
        if (currentVariant != null && selectedIndex == 0)
            variantProperty.objectReferenceValue = null;
        int newIndex = EditorGUI.Popup(rect, "Preset Variant", selectedIndex, labels);
        if (newIndex != selectedIndex)
            variantProperty.objectReferenceValue = newIndex == 0 ? null : variants[newIndex - 1];
        return newIndex == 0 ? null : variants[newIndex - 1];
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var value = property.FindPropertyRelative("value");
        int lines = property.FindPropertyRelative("characterPreset")?.objectReferenceValue != null ? 2 : 1;
        return value == null
            ? lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing)
            : lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) +
                EditorGUI.GetPropertyHeight(value, label, true);
    }
}
#endif