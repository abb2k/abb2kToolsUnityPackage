#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomPropertyDrawer(typeof(SoundBase), true)]
    public class SoundBaseDrawer : PropertyDrawer
    {
        private const string MixerGroupPreference = "mixerGroupPreference";
        private const string Sound = "sound";
        private const string SFXIcon = "AudioClip Icon";
        private const string SoundIcon = "AudioSource Icon";
        private const string SFXTypeName = "SoundEffect";
        private const string Prio = "prio";
        private const string SpecificMixerGroup = "specificMixerGroup";
        private const string Volume = "volume";
        private const string Pitch = "pitch";
        private const string Min = "min";
        private const string Max = "max";
        private const string StartSound = "startSound";
        private const string EndSound = "endSound";
        private const string EnterTransitionDuration = "enterTransitionDuration";
        private const string ExitTransitionDuration = "exitTransitionDuration";
        
        private static readonly Dictionary<string, float> _loopingPreviewDurations = new();

        private sealed class LoopingInspectorProperties
        {
            public SerializedProperty LoopSound;
            public SerializedProperty StartSound;
            public SerializedProperty EndSound;
            public readonly List<SerializedProperty> Transitions = new();
            public readonly List<SerializedProperty> Settings = new();
            public readonly List<SerializedProperty> ThreeDSettings = new();
        }

        private readonly HashSet<string> _3dProperties = new() 
        { 
            "dopplerLevel", "spread", "rolloff", "minDist", "maxDist" 
        };

        private readonly HashSet<string> _persistentAndRandomizationProperties = new()
        {
            "soundID", "loop"
        };

        private static readonly Dictionary<string, bool> _3dSettingsStates = new();

        static SoundBaseDrawer()
        {
            EditorApplication.contextualPropertyMenu += AddRandomizationMenuItems;
            EditorApplication.update += () =>
            {
                if (EditorAudioPreviewer.IsPlaying)
                {
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            };
        }

        private bool GetFoldoutState(SerializedProperty property, Dictionary<string, bool> dict)
        {
            if (!dict.TryGetValue(property.propertyPath, out bool isExpanded))
            {
                isExpanded = false;
                dict[property.propertyPath] = isExpanded;
            }
            return isExpanded;
        }

        private void SetFoldoutState(SerializedProperty property, Dictionary<string, bool> dict, bool value)
        {
            dict[property.propertyPath] = value;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded && IsLoopingSound(property))
                return GetLoopingInspectorHeight(property);

            float height = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded)
            {
                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                bool enterChildren = true;

                List<SerializedProperty> threeDProps = new();
                List<SerializedProperty> persistentAndRandProps = new();
                List<SerializedProperty> optionsProps = new();

                bool captureOptions = false;

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                    enterChildren = false;

                    if (_3dProperties.Contains(iterator.name))
                    {
                        threeDProps.Add(iterator.Copy());
                        continue;
                    }

                    if (_persistentAndRandomizationProperties.Contains(iterator.name))
                    {
                        persistentAndRandProps.Add(iterator.Copy());
                        continue;
                    }

                    if (iterator.name == MixerGroupPreference)
                        captureOptions = true;

                    if (captureOptions)
                    {
                        optionsProps.Add(iterator.Copy());
                        if (iterator.name == Prio)
                            captureOptions = false;

                        continue;
                    }

                    height += GetSoundBaseFieldHeight(iterator) + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == Sound)
                    {
                        height += (EditorGUIUtility.singleLineHeight * 1.5f) + EditorGUIUtility.standardVerticalSpacing;
                        if (IsLoopingSound(property))
                            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                if (persistentAndRandProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        boxContentHeight += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxPadding = 8f; 
                    height += boxContentHeight + boxPadding + EditorGUIUtility.standardVerticalSpacing;
                }

                if (optionsProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in optionsProps)
                    {
                        boxContentHeight += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeaderHeight = 22f;
                    height += boxHeaderHeight + boxContentHeight + 8f + EditorGUIUtility.standardVerticalSpacing;
                }

                float headerHeight = 20f;
                height += headerHeight + EditorGUIUtility.standardVerticalSpacing;

                if (GetFoldoutState(property, _3dSettingsStates))
                {
                    foreach (var prop3D in threeDProps)
                    {
                        height += GetSoundBaseFieldHeight(prop3D) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    height += 5f; 
                }
            }

            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            GUIContent displayLabel = new(label);
            string typeName = property.type.Replace("managedReference<", "").Replace(">", "");
            if (property.isExpanded) 
                displayLabel.text = $"{label.text} <{typeName}>";
            else
                displayLabel.text = $"{label.text}";

            if (typeName.Contains(SFXTypeName) || IsLoopingSound(property))
            {
                displayLabel.image = EditorGUIUtility.IconContent(SFXIcon).image;
            }
            else
            {
                displayLabel.image = EditorGUIUtility.IconContent(SoundIcon).image;
            }

            SerializedProperty soundProp = property.FindPropertyRelative(Sound);

            float labelWidth = EditorStyles.foldout.CalcSize(displayLabel).x + 15f; 
            
            float rightElementsWidth = 0f;
            if (!property.isExpanded && soundProp != null && soundProp.objectReferenceValue != null)
            {
                rightElementsWidth = 35f;
            }

            float labelSpacingOffset = -45f; 

            float leftOffset = position.x + labelWidth + labelSpacingOffset;
            float rightBound = position.x + position.width - rightElementsWidth - 5f;
            float availableWidth = rightBound - leftOffset;

            Rect foldoutRect = new(
                position.x,
                position.y,
                Mathf.Min(position.width - rightElementsWidth, labelWidth),
                EditorGUIUtility.singleLineHeight
            );

            if (property.serializedObject.targetObject is AdvancedAudioSource)
            {
                property.isExpanded = true;
                EditorGUI.LabelField(foldoutRect, displayLabel, EditorStyles.boldLabel);
            }
            else
            {
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);
            }

            if (!property.isExpanded)
            {
                if (soundProp != null && availableWidth > 40f)
                {
                    Rect soundFieldRect = new(
                        leftOffset,
                        position.y,
                        availableWidth,
                        EditorGUIUtility.singleLineHeight
                    );

                    EditorGUI.PropertyField(soundFieldRect, soundProp, GUIContent.none);

                    if (soundProp.objectReferenceValue != null)
                    {
                        Rect tinyButtonRect = new(
                            position.x + position.width - 30,
                            position.y,
                            30,
                            EditorGUIUtility.singleLineHeight
                        );

                        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                        
                        bool isShiftClick = Event.current.shift;
                        GUIContent tinyBtnContent = new GUIContent("▶", "Click to preview\nShift-Click to preview with 3D settings in Scene");
                        
                        if (GUI.Button(tinyButtonRect, tinyBtnContent, EditorStyles.miniButton))
                        {
                            TriggerPreview(property, isShiftClick);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }
            }

            float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded && IsLoopingSound(property))
            {
                DrawLoopingInspector(position, property, currentY);
                EditorGUI.EndProperty();
                return;
            }

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                bool enterChildren = true;

                SerializedProperty prefProp = property.FindPropertyRelative(MixerGroupPreference);
                List<SerializedProperty> threeDProps = new();
                List<SerializedProperty> persistentAndRandProps = new();
                List<SerializedProperty> optionsProps = new();

                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                    enterChildren = false;

                    if (_3dProperties.Contains(iterator.name))
                    {
                        threeDProps.Add(iterator.Copy());
                        continue; 
                    }

                    if (_persistentAndRandomizationProperties.Contains(iterator.name))
                    {
                        persistentAndRandProps.Add(iterator.Copy());
                        continue;
                    }

                    if (iterator.name == MixerGroupPreference)
                    {
                        optionsProps.Add(iterator.Copy());
                        
                        while (iterator.NextVisible(false))
                        {
                            if (SerializedProperty.EqualContents(iterator, endProperty)) break;

                            if (_3dProperties.Contains(iterator.name) || _persistentAndRandomizationProperties.Contains(iterator.name))
                            {
                                continue;
                            }
                            optionsProps.Add(iterator.Copy());

                            if (iterator.name == Prio) break;
                        }

                        continue;
                    }

                    float propHeight = GetSoundBaseFieldHeight(iterator);
                    Rect rect = new(position.x, currentY, position.width, propHeight);
                    if (iterator.name == Sound && IsLoopingSound(property))
                        EditorGUI.PropertyField(rect, iterator, new GUIContent("Looping Sound"), true);
                    else
                        DrawSoundBaseField(rect, property, iterator);
                    currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == Sound)
                    {
                        if (IsLoopingSound(property))
                        {
                            Rect durationRect = new(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight);
                            EditorGUI.BeginChangeCheck();
                            float duration = EditorGUI.FloatField(durationRect, "Preview Duration (seconds)", GetLoopingPreviewDuration(property));
                            if (EditorGUI.EndChangeCheck())
                                SetLoopingPreviewDuration(property, Mathf.Max(0f, duration));

                            currentY += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        }

                        Rect buttonRectFull = new(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight * 1.5f);
                        buttonRectFull = EditorGUI.IndentedRect(buttonRectFull);

                        if (soundProp != null && soundProp.objectReferenceValue != null)
                        {
                            Rect playBtnRect = new Rect(buttonRectFull.x, buttonRectFull.y, buttonRectFull.width - 80f, buttonRectFull.height);
                            Rect stopBtnRect = new Rect(buttonRectFull.xMax - 75f, buttonRectFull.y, 75f, buttonRectFull.height);
                            
                            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);

                            bool isShiftClick = Event.current.shift;
                            GUIContent playBtnContent = new GUIContent("▶ Preview Audio Settings", "Click to preview\nShift-Click to preview with 3D settings in Scene");

                            if (GUI.Button(playBtnRect, playBtnContent))
                            {
                                TriggerPreview(property, isShiftClick);
                            }
                                                        
                            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                            if (GUI.Button(stopBtnRect, "■ Stop"))
                            {
                                EditorAudioPreviewer.StopPreview();
                            }
                            
                            GUI.backgroundColor = Color.white;
                        }
                        else
                        {
                            EditorGUI.BeginDisabledGroup(true);
                            GUI.Button(buttonRectFull, "Assign a Sound to Preview");
                            EditorGUI.EndDisabledGroup();
                        }

                        currentY += buttonRectFull.height + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                if (persistentAndRandProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        boxContentHeight += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = boxContentHeight + 4f;
                    Rect boxRect = new(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    float innerY = boxRect.y + 4f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        float propHeight = GetSoundBaseFieldHeight(prop);
                        Rect propRect = new(boxRect.x + 6, innerY, boxRect.width - 12, propHeight);

                        if (prop.name.Equals("soundID") && prop.propertyType == SerializedPropertyType.String)
                        {
                            float buttonWidth = 60f;
                            float spacing = 5f;
                            float singleLineHeight = EditorGUIUtility.singleLineHeight;

                            Rect buttonRect = new(
                                propRect.x + propRect.width - buttonWidth, 
                                propRect.y + propRect.height - singleLineHeight,
                                buttonWidth, 
                                singleLineHeight
                            );
                            
                            propRect.width -= buttonWidth + spacing;

                            if (GUI.Button(buttonRect, "Random"))
                            {
                                prop.stringValue = "SND_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                            }
                        }

                        DrawSoundBaseField(propRect, property, prop);

                        innerY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }

                    currentY += boxHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                if (optionsProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in optionsProps)
                    {
                        boxContentHeight += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = 22f + boxContentHeight + 6f;
                    Rect boxRect = new(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    Rect labelRect = new(boxRect.x + 6, boxRect.y + 4, boxRect.width - 12, 18f);
                    EditorGUI.LabelField(labelRect, IsLoopingSound(property) ? "Looping Sound Settings" : "Options", EditorStyles.boldLabel);

                    float innerY = boxRect.y + 22f;
                    foreach (var prop in optionsProps)
                    {
                        float propHeight = GetSoundBaseFieldHeight(prop);
                        Rect propRect = new(boxRect.x + 10, innerY, boxRect.width - 20, propHeight);

                        bool disableField = 
                            prop.name == SpecificMixerGroup &&
                            prefProp != null &&
                            prefProp.enumValueIndex == (int)SoundBase.MixerGroupPreference.Preferred
                        ;

                        if (disableField)
                            EditorGUI.BeginDisabledGroup(true);
                        
                        DrawSoundBaseField(propRect, property, prop);
                    
                        if (disableField)
                            EditorGUI.EndDisabledGroup();

                        innerY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }

                    currentY += boxHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                float contentHeight = 0f;
                bool isExpanded = GetFoldoutState(property, _3dSettingsStates);

                if (isExpanded)
                {
                    foreach (var prop3D in threeDProps)
                    {
                        contentHeight += GetSoundBaseFieldHeight(prop3D) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                float headerHeight = 20f;
                float totalBoxHeight = headerHeight + (isExpanded ? contentHeight + 5f : 0f);

                Rect boxRect3D = new(position.x, currentY, position.width, totalBoxHeight);
                GUI.Box(boxRect3D, GUIContent.none, EditorStyles.helpBox);

                Rect foldoutRect2 = new(boxRect3D.x + 5, boxRect3D.y + 2, boxRect3D.width - 10, headerHeight);

                bool newIsExpanded = EditorGUI.Foldout(foldoutRect2, isExpanded, "3D Sound Settings", true, EditorStyles.foldout);
                if (newIsExpanded != isExpanded)
                {
                    SetFoldoutState(property, _3dSettingsStates, newIsExpanded);
                }

                currentY += headerHeight + EditorGUIUtility.standardVerticalSpacing;

                if (newIsExpanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (var prop3D in threeDProps)
                    {
                        float propHeight = GetSoundBaseFieldHeight(prop3D);
                        Rect rect = new(position.x + 15, currentY, position.width - 25, propHeight);
                        DrawSoundBaseField(rect, property, prop3D);
                        currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private LoopingInspectorProperties CollectLoopingInspectorProperties(SerializedProperty property)
        {
            LoopingInspectorProperties result = new();
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, endProperty)) break;
                enterChildren = false;

                SerializedProperty child = iterator.Copy();
                switch (child.name)
                {
                    case Sound:
                        result.LoopSound = child;
                        break;
                    case StartSound:
                        result.StartSound = child;
                        break;
                    case EndSound:
                        result.EndSound = child;
                        break;
                    case EnterTransitionDuration:
                    case ExitTransitionDuration:
                        result.Transitions.Add(child);
                        break;
                    default:
                        if (_3dProperties.Contains(child.name))
                            result.ThreeDSettings.Add(child);
                        else
                            result.Settings.Add(child);
                        break;
                }
            }

            return result;
        }

        private float GetLoopingInspectorHeight(SerializedProperty property)
        {
            LoopingInspectorProperties properties = CollectLoopingInspectorProperties(property);
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = EditorGUIUtility.singleLineHeight + spacing;
            height += GetSoundBaseFieldHeight(properties.LoopSound) + spacing;
            height += GetLoopingPropertyGroupHeight(properties.Transitions, property) + spacing;
            height += GetLoopingSettingsGroupHeight(properties, property) + spacing;
            height += EditorGUIUtility.singleLineHeight + spacing;
            height += EditorGUIUtility.singleLineHeight * 1.5f + spacing;
            height += GetSoundBaseFieldHeight(properties.StartSound) + spacing;
            height += GetSoundBaseFieldHeight(properties.EndSound) + spacing;
            return height + spacing;
        }

        private float GetLoopingPropertyGroupHeight(List<SerializedProperty> properties, SerializedProperty soundBaseProperty)
        {
            float height = 22f + 8f;
            foreach (SerializedProperty prop in properties)
                height += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;

            return height;
        }

        private float GetLoopingSettingsGroupHeight(LoopingInspectorProperties properties, SerializedProperty soundBaseProperty)
        {
            float height = 22f + 8f + 20f + EditorGUIUtility.standardVerticalSpacing;
            foreach (SerializedProperty prop in properties.Settings)
                height += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;

            if (GetFoldoutState(soundBaseProperty, _3dSettingsStates))
            {
                height += 5f;
                foreach (SerializedProperty prop in properties.ThreeDSettings)
                    height += GetSoundBaseFieldHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private void DrawLoopingInspector(Rect position, SerializedProperty property, float currentY)
        {
            LoopingInspectorProperties properties = CollectLoopingInspectorProperties(property);
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.indentLevel++;

            if (properties.LoopSound != null)
            {
                float height = GetSoundBaseFieldHeight(properties.LoopSound);
                Rect rect = new(position.x, currentY, position.width, height);
                EditorGUI.PropertyField(rect, properties.LoopSound, new GUIContent("Loop Sound"), true);
                currentY += height + spacing;
            }

            float transitionHeight = GetLoopingPropertyGroupHeight(properties.Transitions, property);
            Rect transitionRect = new(position.x, currentY, position.width, transitionHeight);
            GUI.Box(transitionRect, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.LabelField(
                new Rect(transitionRect.x + 6f, transitionRect.y + 4f, transitionRect.width - 12f, 18f),
                "Loop Transitions",
                EditorStyles.boldLabel);
            float transitionY = transitionRect.y + 22f;
            foreach (SerializedProperty prop in properties.Transitions)
            {
                float height = GetSoundBaseFieldHeight(prop);
                Rect rect = new(transitionRect.x + 10f, transitionY, transitionRect.width - 20f, height);
                EditorGUI.PropertyField(rect, prop, true);
                transitionY += height + spacing;
            }
            currentY += transitionHeight + spacing;

            float settingsHeight = GetLoopingSettingsGroupHeight(properties, property);
            Rect settingsRect = new(position.x, currentY, position.width, settingsHeight);
            GUI.Box(settingsRect, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.LabelField(
                new Rect(settingsRect.x + 6f, settingsRect.y + 4f, settingsRect.width - 12f, 18f),
                "Looping Sound Settings",
                EditorStyles.boldLabel);
            float settingsY = settingsRect.y + 22f;
            foreach (SerializedProperty prop in properties.Settings)
            {
                float height = GetSoundBaseFieldHeight(prop);
                Rect rect = new(settingsRect.x + 10f, settingsY, settingsRect.width - 20f, height);
                DrawSoundBaseField(rect, property, prop);
                settingsY += height + spacing;
            }

            float threeDHeight = 20f;
            bool showThreeDSettings = GetFoldoutState(property, _3dSettingsStates);
            if (showThreeDSettings)
            {
                threeDHeight += 5f;
                foreach (SerializedProperty prop in properties.ThreeDSettings)
                    threeDHeight += GetSoundBaseFieldHeight(prop) + spacing;
            }
            Rect threeDRect = new(settingsRect.x + 6f, settingsY, settingsRect.width - 12f, threeDHeight);
            GUI.Box(threeDRect, GUIContent.none, EditorStyles.helpBox);
            bool newShowThreeDSettings = EditorGUI.Foldout(
                new Rect(threeDRect.x + 5f, threeDRect.y + 2f, threeDRect.width - 10f, 18f),
                showThreeDSettings,
                "3D Sound Settings",
                true,
                EditorStyles.foldout);
            if (newShowThreeDSettings != showThreeDSettings)
                SetFoldoutState(property, _3dSettingsStates, newShowThreeDSettings);

            if (newShowThreeDSettings)
            {
                float threeDY = threeDRect.y + 20f;
                foreach (SerializedProperty prop in properties.ThreeDSettings)
                {
                    float height = GetSoundBaseFieldHeight(prop);
                    Rect rect = new(threeDRect.x + 10f, threeDY, threeDRect.width - 20f, height);
                    DrawSoundBaseField(rect, property, prop);
                    threeDY += height + spacing;
                }
            }
            currentY += settingsHeight + spacing;

            Rect durationRect = new(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUI.FloatField(
                durationRect,
                "Preview Duration (seconds)",
                GetLoopingPreviewDuration(property));
            if (EditorGUI.EndChangeCheck())
                SetLoopingPreviewDuration(property, Mathf.Max(0f, duration));
            currentY += EditorGUIUtility.singleLineHeight + spacing;

            float controlsHeight = EditorGUIUtility.singleLineHeight * 1.5f;
            Rect controlsRect = EditorGUI.IndentedRect(new Rect(position.x, currentY, position.width, controlsHeight));
            if (properties.LoopSound != null && properties.LoopSound.objectReferenceValue != null)
            {
                Rect playRect = new(controlsRect.x, controlsRect.y, controlsRect.width - 80f, controlsRect.height);
                Rect stopRect = new(controlsRect.xMax - 75f, controlsRect.y, 75f, controlsRect.height);

                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUI.Button(playRect, new GUIContent("▶ Preview Audio Settings")))
                    TriggerPreview(property, Event.current.shift);

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUI.Button(stopRect, "■ Stop"))
                    EditorAudioPreviewer.StopPreview();
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                GUI.Button(controlsRect, "Assign a Sound to Preview");
                EditorGUI.EndDisabledGroup();
            }
            currentY += controlsHeight + spacing;

            DrawLoopingEffectField(position, ref currentY, properties.StartSound, "Start Sound", spacing);
            DrawLoopingEffectField(position, ref currentY, properties.EndSound, "End Sound", spacing);
            EditorGUI.indentLevel--;
        }

        private void DrawLoopingEffectField(
            Rect position,
            ref float currentY,
            SerializedProperty effect,
            string label,
            float spacing)
        {
            if (effect == null) return;

            float height = GetSoundBaseFieldHeight(effect);
            Rect rect = new(position.x, currentY, position.width, height);
            EditorGUI.PropertyField(rect, effect, new GUIContent(label), true);
            currentY += height + spacing;
        }

        private void TriggerPreview(SerializedProperty property, bool use3DSettings = false)
        {
            if (IsLoopingSound(property))
            {
                TriggerLoopingPreview(property, use3DSettings, GetLoopingPreviewDuration(property));
                return;
            }

            var soundProp = property.FindPropertyRelative(Sound);
            if (soundProp == null || soundProp.objectReferenceValue == null) return;

            var mod = soundProp.objectReferenceValue as SoundModificationBase;
            if (mod == null) return;

            float baseVol = property.FindPropertyRelative(Volume)?.floatValue ?? 1f;
            float basePitch = property.FindPropertyRelative(Pitch)?.floatValue ?? 1f;

            baseVol = GetPreviewParameter(property, Volume, baseVol);
            basePitch = GetPreviewParameter(property, Pitch, basePitch);

            Transform targetTransform = null;
            
            if (use3DSettings)
            {
                if (property.serializedObject.targetObject is Component targetComponent)
                {
                    targetTransform = targetComponent.transform;
                }
            }

            EditorAudioPreviewer.PlayPreview(mod, baseVol, basePitch, targetTransform, use3DSettings ? property : null);
        }

        private void TriggerLoopingPreview(SerializedProperty property, bool use3DSettings, float duration)
        {
            GetPreviewSettings(property.FindPropertyRelative("startSound"), out var startSound, out float startVolume, out float startPitch);
            GetPreviewSettings(property, out var loopingSound, out float loopingVolume, out float loopingPitch);
            GetPreviewSettings(property.FindPropertyRelative("endSound"), out var endSound, out float endVolume, out float endPitch);
            float enterTransitionDuration = property.FindPropertyRelative("enterTransitionDuration")?.floatValue ?? 0f;
            float exitTransitionDuration = property.FindPropertyRelative("exitTransitionDuration")?.floatValue ?? 0f;

            Transform targetTransform = null;
            if (use3DSettings && property.serializedObject.targetObject is Component targetComponent)
                targetTransform = targetComponent.transform;

            EditorAudioPreviewer.PlayLoopingPreview(
                property.serializedObject.targetObject,
                startSound, startVolume, startPitch,
                loopingSound, loopingVolume, loopingPitch,
                endSound, endVolume, endPitch,
                duration,
                enterTransitionDuration,
                exitTransitionDuration,
                targetTransform,
                use3DSettings ? property : null);
        }

        private static void GetPreviewSettings(SerializedProperty soundBaseProperty, out SoundModificationBase sound, out float volume, out float pitch)
        {
            sound = null;
            volume = 1f;
            pitch = 1f;
            if (soundBaseProperty == null) return;

            var soundProperty = soundBaseProperty.FindPropertyRelative(Sound);
            sound = soundProperty != null ? soundProperty.objectReferenceValue as SoundModificationBase : null;
            volume = soundBaseProperty.FindPropertyRelative(Volume)?.floatValue ?? 1f;
            pitch = soundBaseProperty.FindPropertyRelative(Pitch)?.floatValue ?? 1f;

            volume = GetPreviewParameter(soundBaseProperty, Volume, volume);
            pitch = GetPreviewParameter(soundBaseProperty, Pitch, pitch);
        }

        private static float GetPreviewParameter(SerializedProperty soundBaseProperty, string parameterName, float constantValue)
        {
            SerializedProperty randomization = soundBaseProperty.FindPropertyRelative($"{parameterName}Randomization");
            if (randomization == null) return constantValue;

            SerializedProperty mode = randomization.FindPropertyRelative("mode");
            if (mode == null || mode.enumValueIndex != (int)SoundParameterMode.RandomBetween)
                return constantValue;

            return Random.Range(
                randomization.FindPropertyRelative(Min).floatValue,
                randomization.FindPropertyRelative(Max).floatValue);
        }

        private static void DrawSoundBaseField(Rect rect, SerializedProperty soundBaseProperty, SerializedProperty field)
        {
            if (!IsRandomizableField(field.name))
            {
                EditorGUI.PropertyField(rect, field, true);
                return;
            }

            SerializedProperty randomization = soundBaseProperty.FindPropertyRelative($"{field.name}Randomization");
            if (randomization == null)
            {
                EditorGUI.PropertyField(rect, field, true);
                return;
            }

            SerializedProperty mode = randomization.FindPropertyRelative("mode");
            if (mode.enumValueIndex == (int)SoundParameterMode.Constant)
            {
                EditorGUI.PropertyField(rect, field, true);
            }
            else
            {
                int previousIndentLevel = EditorGUI.indentLevel;
                Rect contentRect = EditorGUI.IndentedRect(rect);
                EditorGUI.indentLevel = 0;
                try
                {
                    float propertyLabelWidth = Mathf.Min(EditorGUIUtility.labelWidth, contentRect.width * 0.4f);
                    Rect propertyLabelRect = new(
                        contentRect.x,
                        contentRect.y,
                        propertyLabelWidth,
                        contentRect.height);
                    Rect valuesRect = new(
                        propertyLabelRect.xMax,
                        contentRect.y,
                        contentRect.width - propertyLabelWidth,
                        contentRect.height);
                    const float rangeLabelWidth = 30f;
                    const float gap = 6f;
                    float inputWidth = (valuesRect.width - (rangeLabelWidth * 2f) - gap) * 0.5f;
                    Rect minLabelRect = new(valuesRect.x, valuesRect.y, rangeLabelWidth, valuesRect.height);
                    Rect minRect = new(valuesRect.x + rangeLabelWidth, valuesRect.y, inputWidth, valuesRect.height);
                    Rect maxLabelRect = new(minRect.xMax + gap, valuesRect.y, rangeLabelWidth, valuesRect.height);
                    Rect maxRect = new(maxLabelRect.xMax, valuesRect.y, inputWidth, valuesRect.height);

                    SerializedProperty min = randomization.FindPropertyRelative(Min);
                    SerializedProperty max = randomization.FindPropertyRelative(Max);
                    EditorGUI.BeginProperty(propertyLabelRect, GUIContent.none, field);
                    EditorGUI.LabelField(propertyLabelRect, field.displayName);
                    EditorGUI.EndProperty();
                    EditorGUI.LabelField(minLabelRect, "Min", EditorStyles.miniLabel);
                    EditorGUI.LabelField(maxLabelRect, "Max", EditorStyles.miniLabel);
                    EditorGUI.BeginProperty(minRect, GUIContent.none, min);
                    min.floatValue = EditorGUI.FloatField(minRect, min.floatValue);
                    EditorGUI.EndProperty();
                    EditorGUI.BeginProperty(maxRect, GUIContent.none, max);
                    max.floatValue = EditorGUI.FloatField(maxRect, max.floatValue);
                    EditorGUI.EndProperty();
                }
                finally
                {
                    EditorGUI.indentLevel = previousIndentLevel;
                }
            }
        }

        private static void AddRandomizationMenuItems(GenericMenu menu, SerializedProperty property)
        {
            if (property == null) return;

            string propertyPath = property.propertyPath;
            const string randomizationSuffix = "Randomization";
            int randomizationIndex = propertyPath.IndexOf(randomizationSuffix, System.StringComparison.Ordinal);
            string parameterPath;
            string randomizationPath;
            if (randomizationIndex >= 0)
            {
                int randomizationPathEnd = randomizationIndex + randomizationSuffix.Length;
                randomizationPath = propertyPath.Substring(0, randomizationPathEnd);
                parameterPath = propertyPath.Substring(0, randomizationIndex);
                parameterPath = parameterPath.TrimEnd('.');
            }
            else
            {
                parameterPath = propertyPath;
                randomizationPath = parameterPath + "Randomization";
            }

            int parameterPathStart = parameterPath.LastIndexOf('.');
            string parameterName = parameterPathStart < 0
                ? parameterPath
                : parameterPath.Substring(parameterPathStart + 1);
            if (!IsRandomizableField(parameterName)) return;

            SerializedProperty randomization = property.serializedObject.FindProperty(randomizationPath);
            SerializedProperty mode = randomization?.FindPropertyRelative("mode");
            if (mode == null) return;

            SerializedObject serializedObject = property.serializedObject;
            string modePath = mode.propertyPath;
            menu.AddItem(new GUIContent("Constant"), mode.enumValueIndex == (int)SoundParameterMode.Constant, () =>
            {
                SetRandomizationMode(serializedObject, modePath, SoundParameterMode.Constant);
            });
            menu.AddItem(new GUIContent("Random Between Min and Max"), mode.enumValueIndex == (int)SoundParameterMode.RandomBetween, () =>
            {
                SetRandomizationMode(serializedObject, modePath, SoundParameterMode.RandomBetween);
            });
        }

        private static void SetRandomizationMode(SerializedObject serializedObject, string modePath, SoundParameterMode value)
        {
            serializedObject.Update();
            SerializedProperty mode = serializedObject.FindProperty(modePath);
            if (mode == null) return;

            mode.enumValueIndex = (int)value;
            serializedObject.ApplyModifiedProperties();
        }

        private static float GetSoundBaseFieldHeight(SerializedProperty field)
        {
            return EditorGUI.GetPropertyHeight(field, true);
        }

        private static bool IsRandomizableField(string fieldName)
        {
            return fieldName == Volume ||
                   fieldName == Pitch ||
                   fieldName == "panStereo" ||
                   fieldName == "spatialBlend" ||
                   fieldName == "reverbZoneMix" ||
                   fieldName == Prio ||
                   fieldName == "dopplerLevel" ||
                   fieldName == "spread" ||
                   fieldName == "minDist" ||
                   fieldName == "maxDist";
        }

        private static bool IsLoopingSound(SerializedProperty property)
        {
            return property.type.Contains(nameof(SoundEffectLooping));
        }

        private static string GetLoopingPreviewDurationKey(SerializedProperty property)
        {
            return $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
        }

        private static float GetLoopingPreviewDuration(SerializedProperty property)
        {
            string key = GetLoopingPreviewDurationKey(property);
            return _loopingPreviewDurations.TryGetValue(key, out float duration) ? duration : 3f;
        }

        private static void SetLoopingPreviewDuration(SerializedProperty property, float duration)
        {
            _loopingPreviewDurations[GetLoopingPreviewDurationKey(property)] = duration;
        }
    }
}
#endif