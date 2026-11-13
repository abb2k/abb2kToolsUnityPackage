#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomEditor(typeof(SoundModificationBase), true)]
    public class SoundModificationEditor : UnityEditor.Editor
    {
        private readonly struct WaveformTextureKey : System.IEquatable<WaveformTextureKey>
        {
            public readonly int Width;
            public readonly int Height;
            public readonly float Gain;

            public WaveformTextureKey(int width, int height, float gain)
            {
                Width = width;
                Height = height;
                Gain = gain;
            }

            public bool Equals(WaveformTextureKey other)
            {
                return Width == other.Width && Height == other.Height && Gain.Equals(other.Gain);
            }

            public override bool Equals(object obj)
            {
                return obj is WaveformTextureKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Width;
                    hash = (hash * 397) ^ Height;
                    hash = (hash * 397) ^ Gain.GetHashCode();
                    return hash;
                }
            }
        }

        private static readonly Dictionary<AudioClip, Dictionary<WaveformTextureKey, Texture2D>> _waveformCache = new();
        private static readonly Dictionary<AudioClip, float[]> _waveformPeakCache = new();
        private static readonly Dictionary<int, bool> _previewFadeFoldouts = new();
        private ReorderableList _compositionList;
        private bool _isDraggingPlayhead;
        private int _draggingClipIndex = -1;
        private int _draggingEdge = 0; 
        private float _dragStartMouseX;
        private float _dragStartValue;

        private const float MaxZoomX = 50f;
        private static float _zoomX = 1f;
        private static float _zoomY = 3f;
        private Vector2 _scrollPos;
        private float _displayedZoomX = 1f;
        private float _displayedZoomY = 3f;

        private const float PRE_PAD_SECONDS = 2f;
        private const float POST_PAD_SECONDS = 2f;

        private void OnEnable()
        {
            _displayedZoomX = _zoomX;
            _displayedZoomY = _zoomY;
            InitializeCompositionList();
            EditorApplication.update += RepaintOnPreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintOnPreview;
            EditorAudioPreviewer.StopPreview();
        }

        private void RepaintOnPreview()
        {
            if (EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == target)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (target is SoundComposition)
            {
                if (_compositionList == null)
                    InitializeCompositionList();
                _compositionList?.DoLayoutList();
                DrawPropertiesExcluding(serializedObject, "m_Script", "parameterBindings", "fadeIn", "fadeOut", "composition");
            }
            else
            {
                DrawPropertiesExcluding(serializedObject, "m_Script", "parameterBindings", "fadeIn", "fadeOut");
            }

            SerializedProperty parameterBindings = serializedObject.FindProperty("parameterBindings");
            if (parameterBindings != null)
            {
                EditorGUILayout.PropertyField(parameterBindings, new GUIContent("Sound Property Bindings"), true);
            }

            GUILayout.Space(15);

            var soundBase = (SoundModificationBase)target;

            List<PlayableClipData> clips = new();
            soundBase.CollectPlayableClips(clips);

            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
            {
                TogglePlayPause(soundBase);
                e.Use();
                Repaint();
            }

            bool fadeSettingsChanged = DrawPreviewFadeOptions(soundBase);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.BeginHorizontal();
            
            bool isPlayingHere = EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == target;

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(isPlayingHere ? "▶ Restart" : "▶ Play", GUILayout.Height(22), GUILayout.Width(75)))
            {
                EditorAudioPreviewer.PlayPreview(soundBase);
            }

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("▶ Bound Preview", GUILayout.Height(22), GUILayout.Width(105)))
            {
                EditorAudioPreviewer.PlayPreviewWithBindings(
                    soundBase,
                    1f,
                    1f,
                    null,
                    null,
                    new SoundBasePlaybackSettings
                    {
                        volume = 1f,
                        pitch = 1f,
                        panStereo = 0f,
                        spatialBlend = 0f,
                        reverbZoneMix = 1f,
                        prio = 128,
                        dopplerLevel = 1f,
                        spread = 0f,
                        minDist = 1f,
                        maxDist = 500f
                    },
                    soundBase.parameterBindings,
                    SoundBaseDrawer.CollectPreviewBindingInputs(soundBase));
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

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

            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            DrawTimelineZoomControls();
            DrawTimelineGUI(soundBase, clips);

            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
            if (fadeSettingsChanged &&
                EditorAudioPreviewer.IsPlaying &&
                EditorAudioPreviewer.CurrentTarget == soundBase)
            {
                EditorAudioPreviewer.RefreshLiveClips(soundBase);
            }
        }

        private void InitializeCompositionList()
        {
            if (!(target is SoundComposition))
            {
                _compositionList = null;
                return;
            }

            SerializedProperty composition = serializedObject.FindProperty("composition");
            if (composition == null)
                return;

            _compositionList = new ReorderableList(serializedObject, composition, true, true, true, true);
            _compositionList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Composition Clips", EditorStyles.boldLabel);
            _compositionList.elementHeightCallback = index =>
            {
                SerializedProperty element = composition.GetArrayElementAtIndex(index);
                return GetCompositionElementHeight(element);
            };
            _compositionList.drawElementCallback = (rect, index, isActive, isFocused) =>
                DrawCompositionElement(
                    rect,
                    composition.GetArrayElementAtIndex(index),
                    index);
            _compositionList.onAddCallback = list =>
            {
                int index = list.serializedProperty.arraySize;
                list.serializedProperty.InsertArrayElementAtIndex(index);
                InitializeCompositionElement(list.serializedProperty.GetArrayElementAtIndex(index));
                list.serializedProperty.GetArrayElementAtIndex(index).isExpanded = true;
                list.index = index;
            };
            _compositionList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= list.serializedProperty.arraySize)
                    return;

                list.serializedProperty.DeleteArrayElementAtIndex(list.index);
                list.index = Mathf.Min(list.index, list.serializedProperty.arraySize - 1);
            };
        }

        private static void InitializeCompositionElement(SerializedProperty element)
        {
            element.FindPropertyRelative("sound").objectReferenceValue = null;
            element.FindPropertyRelative("volume").floatValue = 1f;
            element.FindPropertyRelative("pitch").floatValue = 1f;
            element.FindPropertyRelative("playDelay").floatValue = 0f;
            element.FindPropertyRelative("startOffset").floatValue = 0f;
            element.FindPropertyRelative("endOffset").floatValue = 0f;
        }

        private static void DrawCompositionElement(Rect rect, SerializedProperty element, int index)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            SerializedProperty sound = element.FindPropertyRelative("sound");
            string soundName = sound.objectReferenceValue != null
                ? sound.objectReferenceValue.name
                : "No sound assigned";
            float padding = 5f;
            Rect headerRect = new(
                rect.x + padding + 12f,
                rect.y + padding,
                rect.width - (padding + 12f) - padding,
                EditorGUIUtility.singleLineHeight);
            element.isExpanded = EditorGUI.Foldout(
                headerRect,
                element.isExpanded,
                $"Clip {index + 1}: {soundName}",
                true);
            if (!element.isExpanded)
                return;

            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            float fieldX = rect.x + padding + 4f;
            float fieldWidth = rect.width - (padding + 4f) * 2f;
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                sound,
                "Sound",
                ref y);
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                element.FindPropertyRelative("volume"),
                "Volume",
                ref y);
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                element.FindPropertyRelative("pitch"),
                "Pitch",
                ref y);
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                element.FindPropertyRelative("playDelay"),
                "Play Delay",
                ref y);
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                element.FindPropertyRelative("startOffset"),
                "Start Offset",
                ref y);
            DrawCompositionElementField(
                new Rect(fieldX, y, fieldWidth, 0f),
                element.FindPropertyRelative("endOffset"),
                "End Offset",
                ref y);
        }

        private static float GetCompositionElementHeight(SerializedProperty element)
        {
            float height = 5f + EditorGUIUtility.singleLineHeight + 5f;
            if (!element.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("sound"), "Sound");
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("volume"), "Volume");
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("pitch"), "Pitch");
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("playDelay"), "Play Delay");
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("startOffset"), "Start Offset");
            height += GetCompositionElementFieldHeight(element.FindPropertyRelative("endOffset"), "End Offset");
            return height + 5f;
        }

        private static float GetCompositionElementFieldHeight(SerializedProperty property, string label)
        {
            float fieldHeight = EditorGUI.GetPropertyHeight(property, new GUIContent(label), true);
            return fieldHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static void DrawCompositionElementField(
            Rect fieldRect,
            SerializedProperty property,
            string label,
            ref float y)
        {
            float fieldHeight = EditorGUI.GetPropertyHeight(property, new GUIContent(label), true);
            fieldRect.y = y;
            fieldRect.height = fieldHeight;
            EditorGUI.PropertyField(
                fieldRect,
                property,
                new GUIContent(label),
                true);
            y += fieldHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private void TogglePlayPause(SoundModificationBase soundBase)
        {
            bool isPlayingHere = EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == soundBase;
            if (!isPlayingHere)
                EditorAudioPreviewer.PlayPreview(soundBase);
            else if (EditorAudioPreviewer.IsPaused) EditorAudioPreviewer.ResumePreview();
            else EditorAudioPreviewer.PausePreview();
        }

        private bool DrawPreviewFadeOptions(SoundModificationBase soundBase)
        {
            int instanceId = soundBase.GetInstanceID();
            if (!_previewFadeFoldouts.TryGetValue(instanceId, out bool expanded))
                expanded = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            foldoutRect.xMin += 10f;
            foldoutRect.xMax -= 6f;
            expanded = EditorGUI.Foldout(
                foldoutRect,
                expanded,
                "Fade In / Fade Out",
                true);
            EditorGUI.indentLevel = previousIndentLevel;
            _previewFadeFoldouts[instanceId] = expanded;

            bool changed = false;
            if (expanded)
            {
                EditorGUI.BeginChangeCheck();
                DrawPreviewFadeGroup(serializedObject.FindProperty("fadeIn"), "Fade In", Color.green);
                DrawPreviewFadeGroup(
                    serializedObject.FindProperty("fadeOut"),
                    "Fade Out",
                    new Color(1f, 0.65f, 0.2f));
                changed = EditorGUI.EndChangeCheck();
            }
            EditorGUILayout.EndVertical();
            return changed;
        }

        private static void DrawPreviewFadeGroup(SerializedProperty fadeSettings, string label, Color curveColor)
        {
            if (fadeSettings == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            EditorGUILayout.PropertyField(
                fadeSettings.FindPropertyRelative("enabled"),
                new GUIContent(label));
            EditorGUILayout.EndHorizontal();

            if (fadeSettings.FindPropertyRelative("enabled").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(fadeSettings.FindPropertyRelative("duration"), new GUIContent("Duration"));
                SerializedProperty curve = fadeSettings.FindPropertyRelative("curve");
                curve.animationCurveValue = EditorGUILayout.CurveField(
                    "Curve",
                    curve.animationCurveValue,
                    curveColor,
                    new Rect(0f, 0f, 1f, 1f));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTimelineZoomControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Timeline Zoom", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Zoom X", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            float zoomX = EditorGUILayout.Slider(_zoomX, 1f, MaxZoomX);
            if (EditorGUI.EndChangeCheck())
                _zoomX = zoomX;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Zoom Y", GUILayout.Width(50f));
            EditorGUI.BeginChangeCheck();
            float zoomY = EditorGUILayout.Slider(_zoomY, 1f, 10f);
            if (EditorGUI.EndChangeCheck())
                _zoomY = zoomY;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawTimelineGUI(SoundModificationBase soundBase, List<PlayableClipData> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign valid AudioClips to view the timeline.", MessageType.Info);
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

            float viewHeight = Mathf.Min(innerTimelineHeight + 20f, 400f);
            float baseWidth = Mathf.Max(300f, EditorGUIUtility.currentViewWidth - 40f);
            if (!Mathf.Approximately(_displayedZoomX, _zoomX) ||
                !Mathf.Approximately(_displayedZoomY, _zoomY))
            {
                float previousContentWidth = baseWidth * _displayedZoomX;
                float nextContentWidth = baseWidth * _zoomX;
                _scrollPos.x = PreserveScrollFraction(
                    _scrollPos.x,
                    previousContentWidth - baseWidth,
                    nextContentWidth - baseWidth);

                float previousTimelineHeight = headerHeight + maxTracks * 36f * _displayedZoomY + 10f;
                float previousViewHeight = Mathf.Min(previousTimelineHeight + 20f, 400f);
                _scrollPos.y = PreserveScrollFraction(
                    _scrollPos.y,
                    previousTimelineHeight - previousViewHeight,
                    innerTimelineHeight - viewHeight);
                _displayedZoomX = _zoomX;
                _displayedZoomY = _zoomY;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(viewHeight));

            float expandedWidth = baseWidth * _zoomX;

            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, innerTimelineHeight, GUILayout.ExpandWidth(false));
            GUI.Box(timelineRect, GUIContent.none, EditorStyles.helpBox);

            Rect trackAreaRect = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, timelineRect.height - headerHeight);
            EditorGUI.DrawRect(trackAreaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            DrawZeroSecondLine(trackAreaRect, timelineStartTime, timelineDuration);
            DrawTimelineRuler(new Rect(timelineRect.x, timelineRect.y, timelineRect.width, headerHeight), timelineStartTime, timelineEndTime, timelineDuration);

            for (int i = 0; i < clips.Count; i++)
            {
                DrawClipBlock(
                    clips[i],
                    trackAreaRect,
                    trackIndices[i],
                    timelineStartTime,
                    timelineDuration,
                    trackHeight,
                    i,
                    soundBase);
            }

            DrawPlayheadAndHandleEvents(timelineRect, trackAreaRect, soundBase, timelineStartTime, timelineDuration, maxEnd);
            HandleTimelineScroll(
                timelineRect,
                baseWidth,
                expandedWidth,
                viewHeight,
                innerTimelineHeight);
            
            EditorGUILayout.EndScrollView();
        }

        private void HandleTimelineScroll(
            Rect timelineRect,
            float viewportWidth,
            float contentWidth,
            float viewportHeight,
            float contentHeight)
        {
            Event currentEvent = Event.current;

            int panControlId = GUIUtility.GetControlID(FocusType.Passive);
            if (currentEvent.type == EventType.MouseDown &&
                currentEvent.button == 2 &&
                timelineRect.Contains(currentEvent.mousePosition))
            {
                GUIUtility.hotControl = panControlId;
                currentEvent.Use();
                return;
            }

            if (GUIUtility.hotControl == panControlId)
            {
                if (currentEvent.type == EventType.MouseDrag)
                {
                    _scrollPos.x = Mathf.Clamp(
                        _scrollPos.x - currentEvent.delta.x,
                        0f,
                        Mathf.Max(0f, contentWidth - viewportWidth));
                    _scrollPos.y = Mathf.Clamp(
                        _scrollPos.y - currentEvent.delta.y,
                        0f,
                        Mathf.Max(0f, contentHeight - viewportHeight));
                    currentEvent.Use();
                    Repaint();
                }
                else if (currentEvent.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    currentEvent.Use();
                }
                return;
            }

            if (currentEvent.type != EventType.ScrollWheel || !timelineRect.Contains(currentEvent.mousePosition))
                return;

            if (currentEvent.control)
            {
                _zoomX = Mathf.Clamp(
                    _zoomX * Mathf.Pow(1.1f, -currentEvent.delta.y),
                    1f,
                    MaxZoomX);
            }
            else if (currentEvent.shift)
            {
                _scrollPos.y = Mathf.Clamp(
                    _scrollPos.y + currentEvent.delta.y * 20f,
                    0f,
                    Mathf.Max(0f, contentHeight - viewportHeight));
            }
            else
            {
                _scrollPos.x = Mathf.Clamp(
                    _scrollPos.x + currentEvent.delta.y * 20f,
                    0f,
                    Mathf.Max(0f, contentWidth - viewportWidth));
            }

            currentEvent.Use();
            Repaint();
        }

        private static float PreserveScrollFraction(float scrollOffset, float previousRange, float nextRange)
        {
            if (previousRange <= 0f || nextRange <= 0f)
                return 0f;

            float scrollFraction = Mathf.Clamp01(scrollOffset / previousRange);
            return scrollFraction * nextRange;
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

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            string startLabel = $"{Mathf.FloorToInt(timelineStartTime)}s";
            string endLabel = $"{Mathf.CeilToInt(timelineEndTime)}s";
            float labelSpacing = Mathf.Max(
                40f,
                Mathf.Max(
                    style.CalcSize(new GUIContent(startLabel)).x,
                    style.CalcSize(new GUIContent(endLabel)).x) + 8f);
            float pixelsPerSecond = rulerRect.width / Mathf.Max(0.001f, timelineDuration);
            float interval = Mathf.Max(1f, GetNiceRulerInterval(labelSpacing / Mathf.Max(0.001f, pixelsPerSecond)));
            float firstTick = Mathf.Ceil(timelineStartTime / interval) * interval;

            for (float sec = firstTick; sec <= timelineEndTime; sec += interval)
            {
                float normX = (sec - timelineStartTime) / timelineDuration;
                float xPos = rulerRect.x + (normX * rulerRect.width);

                EditorGUI.DrawRect(new Rect(xPos, rulerRect.y + rulerRect.height - 6f, 1f, 6f), new Color(0.6f, 0.6f, 0.6f, 1f));

                string label = $"{Mathf.RoundToInt(sec)}s";
                Vector2 labelSize = style.CalcSize(new GUIContent(label));
                GUI.Label(new Rect(Mathf.Clamp(xPos - (labelSize.x / 2f), rulerRect.x, rulerRect.xMax - labelSize.x), rulerRect.y, labelSize.x, labelSize.y), label, style);
            }
        }

        internal static float GetNiceRulerInterval(float minimumInterval)
        {
            if (minimumInterval <= 1f)
                return 1f;

            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(minimumInterval)));
            float normalizedInterval = minimumInterval / magnitude;
            float niceInterval = normalizedInterval <= 1f ? 1f :
                normalizedInterval <= 2f ? 2f :
                normalizedInterval <= 5f ? 5f : 10f;
            return niceInterval * magnitude;
        }

        private void DrawClipBlock(
            PlayableClipData clipData,
            Rect trackAreaRect,
            int trackIndex,
            float timelineStartTime,
            float timelineDuration,
            float trackHeight,
            int clipIndex,
            SoundModificationBase targetBase)
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

            Color baseColor = new Color(0.4f, 0.8f, 1f, 0.8f);
            if (clipData.Filters != null && clipData.Filters.enableDistortion)
            {
                baseColor = Color.Lerp(baseColor, new Color(1f, 0.3f, 0.1f, 0.9f), clipData.Filters.distortionLevel);
            }

            float waveformGain = Mathf.Max(0f, clipData.Volume);
            if (clipData.Filters != null && clipData.Filters.enableDistortion)
                waveformGain += clipData.Filters.distortionLevel;

            int waveformWidth = Mathf.Clamp(
                Mathf.CeilToInt(fullBlockRect.width * EditorGUIUtility.pixelsPerPoint),
                64,
                4096);
            int waveformHeight = Mathf.Clamp(
                Mathf.CeilToInt(fullBlockRect.height * EditorGUIUtility.pixelsPerPoint),
                64,
                512);
            Texture2D waveformTex = GetWaveformTexture(
                clipData.Clip,
                waveformWidth,
                waveformHeight,
                baseColor,
                new Color(0f, 0f, 0f, 0f),
                waveformGain);
            if (waveformTex != null)
            {
                Rect waveRect = fullBlockRect;
                if (waveformGain > 0f)
                    GUI.DrawTexture(waveRect, waveformTex, ScaleMode.StretchToFill);

                DrawPreviewFadeEnvelope(waveRect, trueStart, fullDuration, clipData.FadeEnvelopes);

                // 2. Draw Ghosted Echo Waveforms
                if (waveformGain > 0f && clipData.Filters != null && clipData.Filters.enableEcho)
                {
                    float echoDelaySec = clipData.Filters.echoDelay / 1000f;
                    int bounces = Mathf.Clamp(Mathf.RoundToInt(clipData.Filters.echoDecayRatio * 5f), 1, 5);
                    
                    for (int e = 1; e <= bounces; e++)
                    {
                        float ghostStartNorm = (trueStart + (echoDelaySec * e) - timelineStartTime) / timelineDuration;
                        float ghostX = trackAreaRect.x + (ghostStartNorm * trackAreaRect.width);
                        
                        float ghostAlpha = 0.4f * Mathf.Pow(clipData.Filters.echoDecayRatio, e);
                        
                        Rect ghostRect = new Rect(ghostX, waveRect.y, blockWidth, waveRect.height);
                        
                        GUI.color = new Color(1f, 1f, 1f, ghostAlpha); 
                        GUI.DrawTexture(ghostRect, waveformTex, ScaleMode.StretchToFill);
                    }
                    GUI.color = Color.white; 
                }
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

            HandleInteractiveDragging(targetBase, clipIndex, leftTrimRect, rightTrimRect, activeRect, trackAreaRect.width, timelineDuration, pitch);
        }

        internal static void DrawPreviewFadeEnvelope(
            Rect waveformRect,
            float clipStartTime,
            float clipDuration,
            List<SoundFadeEnvelope> fadeEnvelopes)
        {
            if (fadeEnvelopes == null || fadeEnvelopes.Count == 0)
                return;

            int pointCount = Mathf.Clamp(Mathf.CeilToInt(waveformRect.width / 4f), 2, 256);
            Vector3[] upperPoints = new Vector3[pointCount];
            Vector3[] lowerPoints = new Vector3[pointCount];
            float centerY = waveformRect.center.y;
            float halfHeight = waveformRect.height * 0.48f;

            for (int i = 0; i < pointCount; i++)
            {
                float fraction = i / (float)(pointCount - 1);
                float time = clipStartTime + clipDuration * fraction;
                float amplitude = 1f;
                foreach (SoundFadeEnvelope envelope in fadeEnvelopes)
                    amplitude *= envelope.Evaluate(time);
                float x = Mathf.Lerp(waveformRect.xMin, waveformRect.xMax, fraction);
                float yOffset = halfHeight * amplitude;
                upperPoints[i] = new Vector3(x, centerY - yOffset);
                lowerPoints[i] = new Vector3(x, centerY + yOffset);
            }

            Handles.BeginGUI();
            Color previousColor = Handles.color;
            Handles.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            Handles.DrawAAPolyLine(2f, upperPoints);
            Handles.DrawAAPolyLine(2f, lowerPoints);
            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private void HandleInteractiveDragging(SoundModificationBase targetBase, int clipIndex, Rect leftTrim, Rect rightTrim, Rect activeBody, float trackWidth, float timelineDuration, float pitch)
        {
            Event e = Event.current;
            Rect leftHandle = new Rect(leftTrim.xMax - 3f, leftTrim.y, 6f, leftTrim.height);
            Rect rightHandle = new Rect(rightTrim.x - 3f, rightTrim.y, 6f, rightTrim.height);

            EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(activeBody, MouseCursor.Pan);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (leftHandle.Contains(e.mousePosition)) { StartDrag(clipIndex, 1, e.mousePosition.x, targetBase); e.Use(); }
                else if (rightHandle.Contains(e.mousePosition)) { StartDrag(clipIndex, 2, e.mousePosition.x, targetBase); e.Use(); }
                else if (activeBody.Contains(e.mousePosition)) { StartDrag(clipIndex, 0, e.mousePosition.x, targetBase); e.Use(); }
            }

            if (e.type == EventType.MouseDrag && _draggingClipIndex == clipIndex)
            {
                float timeDelta = ((e.mousePosition.x - _dragStartMouseX) / trackWidth) * timelineDuration;
                ApplyDrag(targetBase, timeDelta, pitch);
                e.Use();
            }

            if (e.type == EventType.MouseUp && _draggingClipIndex == clipIndex)
            {
                _draggingClipIndex = -1;
                e.Use();
            }
        }

        private void StartDrag(int clipIndex, int edge, float mouseX, SoundModificationBase targetBase)
        {
            _draggingClipIndex = clipIndex;
            _draggingEdge = edge;
            _dragStartMouseX = mouseX;

            if (targetBase is SoundModification)
            {
                if (edge == 1) _dragStartValue = serializedObject.FindProperty("startOffset").floatValue;
                else if (edge == 2) _dragStartValue = serializedObject.FindProperty("endOffset").floatValue;
            }
            else if (targetBase is SoundComposition)
            {
                var compArray = serializedObject.FindProperty("composition");
                if (clipIndex >= 0 && clipIndex < compArray.arraySize)
                {
                    var elementProp = compArray.GetArrayElementAtIndex(clipIndex);
                    if (edge == 0) _dragStartValue = elementProp.FindPropertyRelative("playDelay").floatValue;
                    else if (edge == 1) _dragStartValue = elementProp.FindPropertyRelative("startOffset").floatValue;
                    else if (edge == 2) _dragStartValue = elementProp.FindPropertyRelative("endOffset").floatValue;
                }
            }
        }

        private void ApplyDrag(SoundModificationBase targetBase, float timeDelta, float pitch)
        {
            if (targetBase is SoundModification)
            {
                var startProp = serializedObject.FindProperty("startOffset");
                var endProp = serializedObject.FindProperty("endOffset");
                var clipProp = serializedObject.FindProperty("clip");
                float maxLength = clipProp.objectReferenceValue != null ? ((AudioClip)clipProp.objectReferenceValue).length : 0f;

                if (_draggingEdge == 1) 
                {
                    startProp.floatValue = Mathf.Clamp(_dragStartValue + (timeDelta * pitch), 0f, maxLength - endProp.floatValue);
                }
                else if (_draggingEdge == 2) 
                {
                    endProp.floatValue = Mathf.Clamp(_dragStartValue - (timeDelta * pitch), 0f, maxLength - startProp.floatValue);
                }
            }
            else if (targetBase is SoundComposition)
            {
                var compArray = serializedObject.FindProperty("composition");
                if (_draggingClipIndex >= 0 && _draggingClipIndex < compArray.arraySize)
                {
                    var elementProp = compArray.GetArrayElementAtIndex(_draggingClipIndex);
                    
                    if (_draggingEdge == 0) 
                    {
                        var delayProp = elementProp.FindPropertyRelative("playDelay");
                        delayProp.floatValue = Mathf.Max(0f, _dragStartValue + timeDelta);
                    }
                    else if (_draggingEdge == 1) 
                    {
                        var startProp = elementProp.FindPropertyRelative("startOffset");
                        startProp.floatValue = Mathf.Max(0f, _dragStartValue + timeDelta);
                    }
                    else if (_draggingEdge == 2) 
                    {
                        var endProp = elementProp.FindPropertyRelative("endOffset");
                        endProp.floatValue = Mathf.Max(0f, _dragStartValue - timeDelta);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == targetBase)
            {
                EditorAudioPreviewer.RefreshLiveClips(targetBase);
            }
        }

        private void DrawPlayheadAndHandleEvents(Rect timelineRect, Rect trackAreaRect, SoundModificationBase soundBase, float timelineStartTime, float timelineDuration, float maxEnd)
        {
            Event e = Event.current;

            if (timelineRect.Contains(e.mousePosition) || _isDraggingPlayhead)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && _draggingClipIndex == -1) 
                {
                    _isDraggingPlayhead = true;
                    
                    if (EditorAudioPreviewer.IsPlaying && !EditorAudioPreviewer.IsPaused)
                    {
                        EditorAudioPreviewer.PausePreview();
                    }

                    SeekToMouse(e.mousePosition.x, timelineRect, soundBase, timelineStartTime, timelineDuration, maxEnd);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _isDraggingPlayhead)
                {
                    SeekToMouse(e.mousePosition.x, timelineRect, soundBase, timelineStartTime, timelineDuration, maxEnd);
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

        private void SeekToMouse(float mouseX, Rect timelineRect, SoundModificationBase soundBase, float timelineStartTime, float timelineDuration, float maxEnd)
        {
            float normX = (mouseX - timelineRect.x) / timelineRect.width;
            float seekTime = timelineStartTime + (normX * timelineDuration);
            seekTime = Mathf.Clamp(seekTime, 0f, maxEnd);

            if (!EditorAudioPreviewer.IsPlaying || EditorAudioPreviewer.CurrentTarget != soundBase)
            {
                EditorAudioPreviewer.PlayPreview(soundBase);
                EditorAudioPreviewer.PausePreview(); 
            }
            
            EditorAudioPreviewer.Seek(seekTime);
        }

        internal static Texture2D GetWaveformTexture(AudioClip clip, int width, int height, Color waveformColor, Color bgColor, float gain)
        {
            if (clip == null) return null;
            width = Mathf.Min(4096, Mathf.NextPowerOfTwo(Mathf.Max(64, width)));
            height = Mathf.Min(512, Mathf.NextPowerOfTwo(Mathf.Max(64, height)));
            WaveformTextureKey key = new(width, height, gain);
            if (_waveformCache.TryGetValue(clip, out var clipCache) &&
                clipCache.TryGetValue(key, out var cachedTex) &&
                cachedTex != null)
            {
                return cachedTex;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgColor;

            float[] waveformPeaks = GetWaveformPeaks(clip);
            if (waveformPeaks != null && waveformPeaks.Length > 0)
            {
                int halfHeight = height / 2;

                for (int x = 0; x < width; x++)
                {
                    float maxSample = 0f;
                    int peakStart = (int)((long)x * waveformPeaks.Length / width);
                    int peakEnd = (int)((long)(x + 1) * waveformPeaks.Length / width);
                    if (peakEnd <= peakStart)
                        peakEnd = Mathf.Min(waveformPeaks.Length, peakStart + 1);
                    for (int peakIndex = peakStart; peakIndex < peakEnd; peakIndex++)
                        maxSample = Mathf.Max(maxSample, waveformPeaks[peakIndex]);

                    int lineHeight = Mathf.Clamp(Mathf.RoundToInt(maxSample * gain * halfHeight), 1, halfHeight);
                    for (int y = halfHeight - lineHeight; y <= halfHeight + lineHeight; y++)
                        if (y >= 0 && y < height) pixels[y * width + x] = waveformColor;
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            if (clipCache == null)
            {
                clipCache = new Dictionary<WaveformTextureKey, Texture2D>();
                _waveformCache[clip] = clipCache;
            }
            clipCache[key] = tex;
            return tex;
        }

        private static float[] GetWaveformPeaks(AudioClip clip)
        {
            if (_waveformPeakCache.TryGetValue(clip, out float[] cachedPeaks))
                return cachedPeaks;

            float[] sampleData = new float[clip.samples * clip.channels];
            if (!clip.GetData(sampleData, 0))
            {
                _waveformPeakCache.Add(clip, null);
                return null;
            }

            int peakCount = Mathf.Min(8192, sampleData.Length);
            float[] peaks = new float[peakCount];
            for (int i = 0; i < peakCount; i++)
            {
                int sampleStart = (int)((long)i * sampleData.Length / peakCount);
                int sampleEnd = (int)((long)(i + 1) * sampleData.Length / peakCount);
                float maxSample = 0f;
                for (int sampleIndex = sampleStart; sampleIndex < sampleEnd; sampleIndex++)
                    maxSample = Mathf.Max(maxSample, Mathf.Abs(sampleData[sampleIndex]));
                peaks[i] = maxSample;
            }

            _waveformPeakCache.Add(clip, peaks);
            return peaks;
        }
    }
}
#endif