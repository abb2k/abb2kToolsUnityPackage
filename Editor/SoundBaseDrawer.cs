#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Abb2kTools.AudioSystem
{
    [CustomPropertyDrawer(typeof(SoundBase), true)]
    public class SoundBaseDrawer : PropertyDrawer
    {
        private readonly HashSet<string> _3dProperties = new() 
        { 
            "dopplerLevel", "spread", "rolloff", "minDist", "maxDist" 
        };

        private readonly HashSet<string> _persistentAndRandomizationProperties = new()
        {
            "soundID", "loop", "volumeRange", "pitchRange"
        };

        private static readonly Dictionary<string, bool> _3dSettingsStates = new();

        private bool GetFoldoutState(SerializedProperty property)
        {
            if (!_3dSettingsStates.TryGetValue(property.propertyPath, out bool isExpanded))
            {
                isExpanded = false;
                _3dSettingsStates[property.propertyPath] = isExpanded;
            }
            return isExpanded;
        }

        private void SetFoldoutState(SerializedProperty property, bool value)
        {
            _3dSettingsStates[property.propertyPath] = value;
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

                    if (iterator.name == "mixerGroupPreference")
                    {
                        captureOptions = true;
                    }

                    if (captureOptions)
                    {
                        optionsProps.Add(iterator.Copy());
                        if (iterator.name == "prio")
                        {
                            captureOptions = false; // Stop capturing after prio
                        }
                        continue;
                    }

                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == "sound")
                    {
                        height += (EditorGUIUtility.singleLineHeight * 1.5f) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                // Calculate Persistent & Randomization box height (no title header, just inner padding)
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

                // Calculate Options box height
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

                if (GetFoldoutState(property))
                {
                    foreach (var prop3D in threeDProps)
                    {
                        height += EditorGUI.GetPropertyHeight(prop3D, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    height += 5f; 
                }
            }

            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            GUIContent displayLabel = new GUIContent(label);
            string typeName = property.type.Replace("managedReference<", "").Replace(">", ""); 
            displayLabel.text = $"{label.text} <{typeName}>";

            if (typeName.Contains("SoundEffect"))
            {
                displayLabel.image = EditorGUIUtility.IconContent("AudioClip Icon").image;
            }
            else
            {
                displayLabel.image = EditorGUIUtility.IconContent("AudioSource Icon").image;
            }

            Rect foldoutRect = new Rect(position.x, position.y, position.width - 35, EditorGUIUtility.singleLineHeight);
            Rect tinyButtonRect = new Rect(position.x + position.width - 30, position.y, 30, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);

            if (!property.isExpanded)
            {
                SerializedProperty soundProp = property.FindPropertyRelative("sound");
                if (soundProp != null && soundProp.objectReferenceValue != null)
                {
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUI.Button(tinyButtonRect, "▶", EditorStyles.miniButton))
                    {
                        TriggerPreview(property);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            float currentY = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                SerializedProperty iterator = property.Copy();
                SerializedProperty endProperty = iterator.GetEndProperty();
                bool enterChildren = true;

                SerializedProperty prefProp = property.FindPropertyRelative("mixerGroupPreference");
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

                    if (iterator.name == "mixerGroupPreference")
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
                            if (iterator.name == "prio") break;
                        }
                        continue;
                    }

                    float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect rect = new Rect(position.x, currentY, position.width, propHeight);
                    EditorGUI.PropertyField(rect, iterator, true);
                    currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (iterator.name == "sound")
                    {
                        Rect buttonRect = new Rect(position.x, currentY, position.width, EditorGUIUtility.singleLineHeight * 1.5f);
                        buttonRect = EditorGUI.IndentedRect(buttonRect);

                        SerializedProperty soundProp = property.FindPropertyRelative("sound");
                        if (soundProp != null && soundProp.objectReferenceValue != null)
                        {
                            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f); 
                            if (GUI.Button(buttonRect, "▶ Preview Audio Settings"))
                            {
                                TriggerPreview(property);
                            }
                            GUI.backgroundColor = Color.white;
                        }
                        else
                        {
                            EditorGUI.BeginDisabledGroup(true);
                            GUI.Button(buttonRect, "Assign a Sound to Preview");
                            EditorGUI.EndDisabledGroup();
                        }

                        currentY += buttonRect.height + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                // ==========================================
                // DRAW "Persistent Options & Randomization" BOX (No Title Header)
                // ==========================================
                if (persistentAndRandProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = boxContentHeight + 4f;
                    Rect boxRect = new Rect(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    float innerY = boxRect.y + 4f;
                    foreach (var prop in persistentAndRandProps)
                    {
                        float propHeight = EditorGUI.GetPropertyHeight(prop, true);
                        Rect propRect = new Rect(boxRect.x + 6, innerY, boxRect.width - 12, propHeight);
                        EditorGUI.PropertyField(propRect, prop, true);
                        innerY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }

                    currentY += boxHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                // ==========================================
                // DRAW "Options" BOX (mixerGroupPreference -> prio)
                // ==========================================
                if (optionsProps.Count > 0)
                {
                    float boxContentHeight = 0f;
                    foreach (var prop in optionsProps)
                    {
                        boxContentHeight += EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                    float boxHeight = 22f + boxContentHeight + 6f;
                    Rect boxRect = new Rect(position.x, currentY, position.width, boxHeight);
                    GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                    Rect labelRect = new Rect(boxRect.x + 6, boxRect.y + 4, boxRect.width - 12, 18f);
                    EditorGUI.LabelField(labelRect, "Options", EditorStyles.boldLabel);

                    float innerY = boxRect.y + 22f;
                    foreach (var prop in optionsProps)
                    {
                        float propHeight = EditorGUI.GetPropertyHeight(prop, true);
                        Rect propRect = new Rect(boxRect.x + 10, innerY, boxRect.width - 20, propHeight);

                        bool disableField = (prop.name == "specificMixerGroup" && prefProp != null && prefProp.enumValueIndex == (int)SoundBase.MixerGroupPreference.Preferred);

                        if (disableField) EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.PropertyField(propRect, prop, true);
                        if (disableField) EditorGUI.EndDisabledGroup();

                        innerY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }

                    currentY += boxHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                // ==========================================
                // DRAW 3D SOUND SETTINGS BOX
                // ==========================================
                float contentHeight = 0f;
                bool isExpanded = GetFoldoutState(property);

                if (isExpanded)
                {
                    foreach (var prop3D in threeDProps)
                    {
                        contentHeight += EditorGUI.GetPropertyHeight(prop3D, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }

                float headerHeight = 20f; 
                float totalBoxHeight = headerHeight + (isExpanded ? contentHeight + 5f : 0f);

                Rect boxRect3D = new Rect(position.x, currentY, position.width, totalBoxHeight);
                GUI.Box(boxRect3D, GUIContent.none, EditorStyles.helpBox);

                Rect foldoutRect2 = new Rect(boxRect3D.x + 5, boxRect3D.y + 2, boxRect3D.width - 10, headerHeight);

                bool newIsExpanded = EditorGUI.Foldout(foldoutRect2, isExpanded, "3D Sound Settings", true, EditorStyles.foldout);
                if (newIsExpanded != isExpanded)
                {
                    SetFoldoutState(property, newIsExpanded);
                }

                currentY += headerHeight + EditorGUIUtility.standardVerticalSpacing;

                if (newIsExpanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (var prop3D in threeDProps)
                    {
                        float propHeight = EditorGUI.GetPropertyHeight(prop3D, true);
                        Rect rect = new Rect(position.x + 15, currentY, position.width - 25, propHeight);
                        EditorGUI.PropertyField(rect, prop3D, true);
                        currentY += propHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                    EditorGUI.indentLevel--;
                }

                currentY += 5f;
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void TriggerPreview(SerializedProperty property)
        {
            var soundProp = property.FindPropertyRelative("sound");
            if (soundProp == null || soundProp.objectReferenceValue == null) return;

            var mod = soundProp.objectReferenceValue as SoundModificationBase;
            if (mod == null) return;

            float baseVol = property.FindPropertyRelative("volume")?.floatValue ?? 1f;
            float basePitch = property.FindPropertyRelative("pitch")?.floatValue ?? 1f;

            var volRangeProp = property.FindPropertyRelative("volumeRange");
            var pitchRangeProp = property.FindPropertyRelative("pitchRange");

            if (volRangeProp != null && pitchRangeProp != null)
            {
                float vMin = volRangeProp.FindPropertyRelative("min").floatValue;
                float vMax = volRangeProp.FindPropertyRelative("max").floatValue;
                
                float pMin = pitchRangeProp.FindPropertyRelative("min").floatValue;
                float pMax = pitchRangeProp.FindPropertyRelative("max").floatValue;
                
                baseVol *= Random.Range(vMin, vMax);
                basePitch *= Random.Range(pMin, pMax);
            }

            EditorAudioPreviewer.PlayPreview(mod, baseVol, basePitch);
        }
    }
}
#endif