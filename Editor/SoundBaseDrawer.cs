#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

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
        
        // --- Timeline State ---
        private static readonly Dictionary<string, bool> _timelineStates = new();
        private static readonly Dictionary<string, Vector2> _timelineScrolls = new();
        private static readonly Dictionary<AudioClip, Texture2D> _waveformCache = new();
        private static float _zoomX = 1f;
        private static float _zoomY = 1f;
        private static bool _isDraggingPlayhead;
        private const float TIMELINE_AREA_HEIGHT = 220f;
        private const float PRE_PAD_SECONDS = 2f;
        private const float POST_PAD_SECONDS = 2f;

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

                // Add space for the Timeline Foldout
                SerializedProperty soundProp = property.FindPropertyRelative(Sound);
                if (soundProp != null && soundProp.objectReferenceValue != null)
                {
                    height += 20f + EditorGUIUtility.standardVerticalSpacing; 
                    if (GetFoldoutState(property, _timelineStates))
                    {
                        height += TIMELINE_AREA_HEIGHT + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
            }

            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            GUIContent displayLabel = new(label);
            string typeName = property.type.Replace("managedReference<", "").Replace(">", ""); 
            displayLabel.text = $"{label.text} <{typeName}>";

            if (typeName.Contains(SFXTypeName))
            {
                displayLabel.image = EditorGUIUtility.IconContent(SFXIcon).image;
            }
            else
            {
                displayLabel.image = EditorGUIUtility.IconContent(SoundIcon).image;
            }

            Rect foldoutRect = new(
                position.x,
                position.y,
                position.width - 35,
                EditorGUIUtility.singleLineHeight
            );
            Rect tinyButtonRect = new(
                position.x + position.width - 30,
                position.y,
                30,
                EditorGUIUtility.singleLineHeight
            );

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);

            if (!property.isExpanded)
            {
                SerializedProperty soundProp = property.FindPropertyRelative(Sound);
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

                        SerializedProperty soundProp = property.FindPropertyRelative(Sound);
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
                
                // =====================================
                // TIMELINE FOLDOUT
                // =====================================
                SerializedProperty baseSoundProp = property.FindPropertyRelative(Sound);
                if (baseSoundProp != null && baseSoundProp.objectReferenceValue != null)
                {
                    Rect timelineFoldoutRect = new Rect(position.x, currentY, position.width, 20f);
                    bool tlExpanded = GetFoldoutState(property, _timelineStates);
                    
                    GUIStyle tlFoldoutStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
                    bool newTlExpanded = EditorGUI.Foldout(timelineFoldoutRect, tlExpanded, "Audio Timeline Preview", true, tlFoldoutStyle);
                    if (newTlExpanded != tlExpanded) SetFoldoutState(property, _timelineStates, newTlExpanded);

                    currentY += 20f + EditorGUIUtility.standardVerticalSpacing;

                    if (newTlExpanded)
                    {
                        Rect areaRect = new Rect(position.x, currentY, position.width, TIMELINE_AREA_HEIGHT);
                        GUI.Box(areaRect, GUIContent.none, EditorStyles.helpBox);
                        
                        Rect innerAreaRect = new Rect(areaRect.x + 4, areaRect.y + 4, areaRect.width - 8, areaRect.height - 8);
                        DrawTimelineArea(innerAreaRect, property, baseSoundProp.objectReferenceValue as SoundModificationBase);
                        
                        currentY += TIMELINE_AREA_HEIGHT + EditorGUIUtility.standardVerticalSpacing;
                    }
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

        // ==============================================================
        // READ-ONLY TIMELINE RENDERING (Using GUILayout Area)
        // ==============================================================

        private void DrawTimelineArea(Rect areaRect, SerializedProperty property, SoundModificationBase soundBase)
        {
            if (soundBase == null) return;

            GUILayout.BeginArea(areaRect);
            
            GUILayout.BeginHorizontal();
            
            bool isPlayingHere = EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == soundBase;

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(isPlayingHere ? "▶ Restart" : "▶ Play", GUILayout.Height(22), GUILayout.Width(75)))
            {
                TriggerPreview(property);
            }

            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button(EditorAudioPreviewer.IsPaused ? "Resume" : "Pause", GUILayout.Height(22), GUILayout.Width(65)))
            {
                if (EditorAudioPreviewer.IsPaused) EditorAudioPreviewer.ResumePreview();
                else EditorAudioPreviewer.PausePreview();
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("■ Stop", GUILayout.Height(22), GUILayout.Width(55)))
            {
                EditorAudioPreviewer.StopPreview();
            }

            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();

            GUILayout.Label("Zoom X:", GUILayout.Width(50));
            _zoomX = GUILayout.HorizontalSlider(_zoomX, 1f, 10f, GUILayout.Width(70));
            GUILayout.Label("Zoom Y:", GUILayout.Width(50));
            _zoomY = GUILayout.HorizontalSlider(_zoomY, 1f, 10f, GUILayout.Width(70));

            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            List<PlayableClipData> clips = new();
            soundBase.CollectPlayableClips(clips);

            if (clips.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign valid AudioClips to view the timeline.", MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            float maxEnd = 0f;
            foreach (var clip in clips)
            {
                float fullDur = clip.Clip.length / Mathf.Max(0.001f, clip.Pitch);
                float trueStart = clip.Delay - (clip.StartOffset / Mathf.Max(0.001f, clip.Pitch));
                float end = trueStart + fullDur;
                if (end > maxEnd) maxEnd = end;
            }

            float timelineStartTime = -PRE_PAD_SECONDS;
            float timelineEndTime = Mathf.Ceil(maxEnd) + POST_PAD_SECONDS;
            float timelineDuration = timelineEndTime - timelineStartTime;

            List<int> trackIndices = AssignTracks(clips);
            int maxTracks = 1;
            foreach (int idx in trackIndices) if (idx + 1 > maxTracks) maxTracks = idx + 1;

            float trackHeight = 36f * _zoomY;
            float headerHeight = 20f;
            float innerTimelineHeight = headerHeight + (maxTracks * trackHeight) + 10f;

            if (!_timelineScrolls.TryGetValue(property.propertyPath, out Vector2 scrollPos)) scrollPos = Vector2.zero;
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            _timelineScrolls[property.propertyPath] = scrollPos;

            float expandedWidth = (areaRect.width - 25f) * _zoomX;
            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, innerTimelineHeight, GUILayout.ExpandWidth(false));
            GUI.Box(timelineRect, GUIContent.none, EditorStyles.helpBox);

            Rect trackAreaRect = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, timelineRect.height - headerHeight);
            EditorGUI.DrawRect(trackAreaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            DrawZeroSecondLine(trackAreaRect, timelineStartTime, timelineDuration);
            DrawTimelineRuler(new Rect(timelineRect.x, timelineRect.y, timelineRect.width, headerHeight), timelineStartTime, timelineEndTime, timelineDuration);

            for (int i = 0; i < clips.Count; i++)
            {
                DrawClipBlockReadOnly(clips[i], trackAreaRect, trackIndices[i], timelineStartTime, timelineDuration, trackHeight);
            }

            DrawPlayheadAndHandleEvents(timelineRect, soundBase, timelineStartTime, timelineDuration, maxEnd, property);
            
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawZeroSecondLine(Rect trackAreaRect, float timelineStartTime, float timelineDuration)
        {
            float normZero = (0f - timelineStartTime) / timelineDuration;
            if (normZero >= 0f && normZero <= 1f)
            {
                float xPos = trackAreaRect.x + (normZero * trackAreaRect.width);
                float dashHeight = 4f;
                float gapHeight = 4f;
                float currentY = trackAreaRect.y;

                Color lineColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

                while (currentY < trackAreaRect.yMax)
                {
                    float segmentHeight = Mathf.Min(dashHeight, trackAreaRect.yMax - currentY);
                    EditorGUI.DrawRect(new Rect(xPos - 0.5f, currentY, 1f, segmentHeight), lineColor);
                    currentY += dashHeight + gapHeight;
                }
            }
        }

        private List<int> AssignTracks(List<PlayableClipData> clips)
        {
            List<int> tracks = new();
            List<float> trackEndTimes = new();

            foreach (var clip in clips)
            {
                float fullDur = clip.Clip.length / Mathf.Max(0.001f, clip.Pitch);
                float startTime = clip.Delay - (clip.StartOffset / Mathf.Max(0.001f, clip.Pitch));
                float endTime = startTime + fullDur;

                int assignedTrack = -1;
                for (int t = 0; t < trackEndTimes.Count; t++)
                {
                    if (startTime >= trackEndTimes[t])
                    {
                        assignedTrack = t;
                        trackEndTimes[t] = endTime;
                        break;
                    }
                }

                if (assignedTrack == -1)
                {
                    assignedTrack = trackEndTimes.Count;
                    trackEndTimes.Add(endTime);
                }
                tracks.Add(assignedTrack);
            }
            return tracks;
        }

        private void DrawTimelineRuler(Rect rulerRect, float timelineStartTime, float timelineEndTime, float timelineDuration)
        {
            EditorGUI.DrawRect(rulerRect, new Color(0.18f, 0.18f, 0.18f, 1f));
            
            int startSec = Mathf.CeilToInt(timelineStartTime);
            int endSec = Mathf.FloorToInt(timelineEndTime);

            for (int sec = startSec; sec <= endSec; sec++)
            {
                float normX = (sec - timelineStartTime) / timelineDuration;
                float xPos = rulerRect.x + (normX * rulerRect.width);

                EditorGUI.DrawRect(new Rect(xPos, rulerRect.y + rulerRect.height - 6f, 1f, 6f), new Color(0.6f, 0.6f, 0.6f, 1f));

                string label = $"{sec}s";
                GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                Vector2 labelSize = style.CalcSize(new GUIContent(label));
                GUI.Label(new Rect(Mathf.Clamp(xPos - (labelSize.x / 2f), rulerRect.x, rulerRect.xMax - labelSize.x), rulerRect.y, labelSize.x, labelSize.y), label, style);
            }
        }

        private void DrawClipBlockReadOnly(PlayableClipData clipData, Rect trackAreaRect, int trackIndex, float timelineStartTime, float timelineDuration, float trackHeight)
        {
            float pitch = Mathf.Max(0.001f, clipData.Pitch);
            float fullDuration = clipData.Clip.length / pitch;
            float startTrimDur = clipData.StartOffset / pitch;
            float endTrimDur = clipData.EndOffset / pitch;
            
            float trueStart = clipData.Delay - startTrimDur;
            
            float startNorm = (trueStart - timelineStartTime) / timelineDuration;
            float durNorm = fullDuration / timelineDuration;

            float blockX = trackAreaRect.x + (startNorm * trackAreaRect.width);
            float blockWidth = Mathf.Max(durNorm * trackAreaRect.width, 4f);
            float blockY = trackAreaRect.y + (trackIndex * trackHeight) + 2f;

            Rect fullBlockRect = new Rect(blockX, blockY, blockWidth, trackHeight - 4f);

            EditorGUI.DrawRect(fullBlockRect, new Color(0.15f, 0.25f, 0.35f, 0.9f));

            Texture2D waveformTex = GetWaveformTexture(clipData.Clip, 256, 64, new Color(0.4f, 0.8f, 1f, 0.8f), new Color(0f, 0f, 0f, 0f));
            if (waveformTex != null)
            {
                float volumeScale = Mathf.Clamp01(clipData.Volume); 
                float waveHeight = fullBlockRect.height * volumeScale;
                Rect waveRect = new Rect(fullBlockRect.x, fullBlockRect.y + ((fullBlockRect.height - waveHeight) / 2f), fullBlockRect.width, waveHeight);
                GUI.DrawTexture(waveRect, waveformTex, ScaleMode.StretchToFill);
            }

            float maxTrimWidth = fullBlockRect.width;
            float trimLeftWidth = (startTrimDur / timelineDuration) * trackAreaRect.width;
            float trimRightWidth = (endTrimDur / timelineDuration) * trackAreaRect.width;

            if (trimLeftWidth + trimRightWidth > maxTrimWidth)
            {
                float ratio = trimLeftWidth / (trimLeftWidth + trimRightWidth);
                trimLeftWidth = maxTrimWidth * ratio;
                trimRightWidth = maxTrimWidth * (1f - ratio);
            }

            Rect leftTrimRect = new Rect(fullBlockRect.x, fullBlockRect.y, trimLeftWidth, fullBlockRect.height);
            Rect rightTrimRect = new Rect(fullBlockRect.xMax - trimRightWidth, fullBlockRect.y, trimRightWidth, fullBlockRect.height);
            Rect activeRect = new Rect(fullBlockRect.x + trimLeftWidth, fullBlockRect.y, fullBlockRect.width - trimLeftWidth - trimRightWidth, fullBlockRect.height);

            EditorGUI.DrawRect(leftTrimRect, new Color(0f, 0f, 0f, 0.6f));
            EditorGUI.DrawRect(rightTrimRect, new Color(0f, 0f, 0f, 0.6f));

            if (activeRect.width > 0f)
            {
                Handles.DrawSolidRectangleWithOutline(activeRect, Color.clear, new Color(0.5f, 0.7f, 1f, 0.9f));
            }
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, normal = { textColor = Color.white } };
            if (activeRect.width > 20f)
            {
                GUI.Label(new Rect(activeRect.x + 4f, activeRect.y + 2f, activeRect.width - 8f, 16f), clipData.Clip.name, labelStyle);
            }
        }

        private void DrawPlayheadAndHandleEvents(Rect timelineRect, SoundModificationBase soundBase, float timelineStartTime, float timelineDuration, float maxEnd, SerializedProperty property)
        {
            Event e = Event.current;

            if (timelineRect.Contains(e.mousePosition) || _isDraggingPlayhead)
            {
                if (e.type == EventType.MouseDown && e.button == 0) 
                {
                    _isDraggingPlayhead = true;
                    
                    if (EditorAudioPreviewer.IsPlaying && !EditorAudioPreviewer.IsPaused)
                    {
                        EditorAudioPreviewer.PausePreview();
                    }

                    SeekToMouse(e.mousePosition.x, timelineRect, soundBase, timelineStartTime, timelineDuration, maxEnd, property);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _isDraggingPlayhead)
                {
                    SeekToMouse(e.mousePosition.x, timelineRect, soundBase, timelineStartTime, timelineDuration, maxEnd, property);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && _isDraggingPlayhead)
                {
                    _isDraggingPlayhead = false;
                    e.Use();
                }
            }

            if (EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == soundBase)
            {
                float currentTime = EditorAudioPreviewer.CurrentTime;
                float normTime = (currentTime - timelineStartTime) / timelineDuration;
                
                if (normTime >= 0f && normTime <= 1f)
                {
                    float playheadX = timelineRect.x + (normTime * timelineRect.width);

                    EditorGUI.DrawRect(new Rect(playheadX - 1f, timelineRect.y, 2f, timelineRect.height), new Color(1f, 0.2f, 0.2f, 1f));

                    Vector3[] headTriangle = new Vector3[] {
                        new Vector3(playheadX - 5f, timelineRect.y),
                        new Vector3(playheadX + 5f, timelineRect.y),
                        new Vector3(playheadX, timelineRect.y + 8f)
                    };
                    Handles.color = new Color(1f, 0.25f, 0.25f, 1f);
                    Handles.DrawAAConvexPolygon(headTriangle);
                }
            }
        }

        private void SeekToMouse(float mouseX, Rect timelineRect, SoundModificationBase soundBase, float timelineStartTime, float timelineDuration, float maxEnd, SerializedProperty property)
        {
            float normX = (mouseX - timelineRect.x) / timelineRect.width;
            float seekTime = timelineStartTime + (normX * timelineDuration);
            seekTime = Mathf.Clamp(seekTime, 0f, maxEnd);

            if (!EditorAudioPreviewer.IsPlaying || EditorAudioPreviewer.CurrentTarget != soundBase)
            {
                TriggerPreview(property);
                EditorAudioPreviewer.PausePreview(); 
            }
            
            EditorAudioPreviewer.Seek(seekTime);
        }

        private static Texture2D GetWaveformTexture(AudioClip clip, int width, int height, Color waveformColor, Color bgColor)
        {
            if (clip == null) return null;
            if (_waveformCache.TryGetValue(clip, out var cachedTex) && cachedTex != null) return cachedTex;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgColor;

            float[] sampleData = new float[clip.samples * clip.channels];
            if (clip.GetData(sampleData, 0))
            {
                int step = Mathf.Max(1, (clip.samples * clip.channels) / width);
                int halfHeight = height / 2;

                for (int x = 0; x < width; x++)
                {
                    float maxSample = 0f;
                    for (int s = 0; s < step && (x * step + s) < sampleData.Length; s++)
                        if (Mathf.Abs(sampleData[x * step + s]) > maxSample) maxSample = Mathf.Abs(sampleData[x * step + s]);

                    int lineHeight = Mathf.Clamp(Mathf.RoundToInt(maxSample * halfHeight), 1, halfHeight);
                    for (int y = halfHeight - lineHeight; y <= halfHeight + lineHeight; y++)
                        if (y >= 0 && y < height) pixels[y * width + x] = waveformColor;
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            _waveformCache[clip] = tex;
            return tex;
        }
    }
}
#endif