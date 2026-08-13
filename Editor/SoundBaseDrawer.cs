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
        private const string VolumeRange = "volumeRange";
        private const string PitchRange = "pitchRange";
        private const string Min = "min";
        private const string Max = "max";
        
        private readonly HashSet<string> _3dProperties = new() 
        { 
            "dopplerLevel", "spread", "rolloff", "minDist", "maxDist" 
        };

        private readonly HashSet<string> _persistentAndRandomizationProperties = new()
        {
            "soundID", "loop", VolumeRange, PitchRange
        };

        private static readonly Dictionary<string, bool> _3dSettingsStates = new();
        
        // // --- Timeline State ---
        // private static readonly Dictionary<string, bool> _timelineStates = new();
        // private static readonly Dictionary<string, Vector2> _timelineScrolls = new();
        // private static readonly Dictionary<AudioClip, Texture2D> _waveformCache = new();
        // private static float _zoomX = 1f;
        // private static float _zoomY = 1f;
        // private static bool _isDraggingPlayhead;
        // private const float TIMELINE_AREA_HEIGHT = 220f;
        // private const float PRE_PAD_SECONDS = 2f;
        // private const float POST_PAD_SECONDS = 2f;

        // Forces the inspector to repaint while audio is playing so the playhead moves smoothly
        static SoundBaseDrawer()
        {
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

                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == Sound)
                    {
                        height += (EditorGUIUtility.singleLineHeight * 1.5f) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                if (persistentAndRandProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxPadding = 8f; 
                    height += boxContentHeight + boxPadding + EditorGUIUtility.standardVerticalSpacing;
                }

                if (optionsProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in optionsProps)
                    {
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
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
                        height += EditorGUI.GetPropertyHeight(prop3D, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    height += 5f; 
                }

                // // Add space for the Timeline Foldout
                // SerializedProperty soundProp = property.FindPropertyRelative(Sound);
                // if (soundProp != null && soundProp.objectReferenceValue != null)
                // {
                //     height += 20f + EditorGUIUtility.standardVerticalSpacing; 
                //     if (GetFoldoutState(property, _timelineStates))
                //     {
                //         height += TIMELINE_AREA_HEIGHT + EditorGUIUtility.standardVerticalSpacing;
                //     }
                // }
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

            if (typeName.Contains(SFXTypeName))
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

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);

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
                        if (GUI.Button(tinyButtonRect, "▶", EditorStyles.miniButton))
                        {
                            TriggerPreview(property);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }
            }

            float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

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

                    float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect rect = new(position.x, currentY, position.width, propHeight);
                    EditorGUI.PropertyField(rect, iterator, true);
                    currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == Sound)
                    {
                        Rect buttonRectFull = new(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight * 1.5f);
                        buttonRectFull = EditorGUI.IndentedRect(buttonRectFull);

                        if (soundProp != null && soundProp.objectReferenceValue != null)
                        {
                            Rect playBtnRect = new Rect(buttonRectFull.x, buttonRectFull.y, buttonRectFull.width - 80f, buttonRectFull.height);
                            Rect stopBtnRect = new Rect(buttonRectFull.xMax - 75f, buttonRectFull.y, 75f, buttonRectFull.height);
                            
                            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                            if (GUI.Button(playBtnRect, "▶ Preview Audio Settings"))
                            {
                                TriggerPreview(property);
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
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = boxContentHeight + 4f;
                    Rect boxRect = new(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    float innerY = boxRect.y + 4f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        float propHeight = EditorGUI.GetPropertyHeight(prop, true);
                        Rect propRect = new(boxRect.x + 6, innerY, boxRect.width - 12, propHeight);
                        EditorGUI.PropertyField(propRect, prop, true);
                        innerY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }

                    currentY += boxHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                if (optionsProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in optionsProps)
                    {
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = 22f + boxContentHeight + 6f;
                    Rect boxRect = new(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    Rect labelRect = new(boxRect.x + 6, boxRect.y + 4, boxRect.width - 12, 18f);
                    EditorGUI.LabelField(labelRect, "Options", EditorStyles.boldLabel);

                    float innerY = boxRect.y + 22f;
                    foreach (var prop in optionsProps)
                    {
                        float propHeight = EditorGUI.GetPropertyHeight(prop, true);
                        Rect propRect = new(boxRect.x + 10, innerY, boxRect.width - 20, propHeight);

                        bool disableField = 
                            prop.name == SpecificMixerGroup &&
                            prefProp != null &&
                            prefProp.enumValueIndex == (int)SoundBase.MixerGroupPreference.Preferred
                        ;

                        if (disableField)
                            EditorGUI.BeginDisabledGroup(true);
                        
                        EditorGUI.PropertyField(propRect, prop, true);
                    
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
                        contentHeight += EditorGUI.GetPropertyHeight(prop3D, true) + EditorGUIUtility.standardVerticalSpacing;
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
                        float propHeight = EditorGUI.GetPropertyHeight(prop3D, true);
                        Rect rect = new(position.x + 15, currentY, position.width - 25, propHeight);
                        EditorGUI.PropertyField(rect, prop3D, true);
                        currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void TriggerPreview(SerializedProperty property)
        {
            var soundProp = property.FindPropertyRelative(Sound);
            if (soundProp == null || soundProp.objectReferenceValue == null) return;

            var mod = soundProp.objectReferenceValue as SoundModificationBase;
            if (mod == null) return;

            float baseVol = property.FindPropertyRelative(Volume)?.floatValue ?? 1f;
            float basePitch = property.FindPropertyRelative(Pitch)?.floatValue ?? 1f;

            var volRangeProp = property.FindPropertyRelative(VolumeRange);
            var pitchRangeProp = property.FindPropertyRelative(PitchRange);

            if (volRangeProp != null && pitchRangeProp != null)
            {
                float vMin = volRangeProp.FindPropertyRelative(Min).floatValue;
                float vMax = volRangeProp.FindPropertyRelative(Max).floatValue;
                
                float pMin = pitchRangeProp.FindPropertyRelative(Min).floatValue;
                float pMax = pitchRangeProp.FindPropertyRelative(Max).floatValue;
                
                baseVol *= Random.Range(vMin, vMax);
                basePitch *= Random.Range(pMin, pMax);
            }

            EditorAudioPreviewer.PlayPreview(mod, baseVol, basePitch);
        }
    }
}
#endif