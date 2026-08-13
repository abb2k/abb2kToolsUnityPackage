#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomEditor(typeof(SoundModificationBase), true)]
    public class SoundModificationEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<AudioClip, Texture2D> _waveformCache = new();
        private bool _isDraggingPlayhead;
        private int _draggingClipIndex = -1;
        private int _draggingEdge = 0; 
        private float _dragStartMouseX;
        private float _dragStartValue;

        private static float _zoomX = 1f;
        private static float _zoomY = 3f;
        private Vector2 _scrollPos;

        private const float PRE_PAD_SECONDS = 2f;
        private const float POST_PAD_SECONDS = 2f;

        private void OnEnable()
        {
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
            DrawDefaultInspector();

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

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.BeginHorizontal();
            
            bool isPlayingHere = EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == target;

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(isPlayingHere ? "▶ Restart" : "▶ Play", GUILayout.Height(22), GUILayout.Width(75)))
            {
                EditorAudioPreviewer.PlayPreview(soundBase);
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
            _zoomX = GUILayout.HorizontalSlider(_zoomX, 1f, 10f, GUILayout.Width(100));
            GUILayout.Label("Zoom Y:", GUILayout.Width(50));
            _zoomY = GUILayout.HorizontalSlider(_zoomY, 1f, 10f, GUILayout.Width(100));

            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            DrawTimelineGUI(soundBase, clips);

            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }

        private void TogglePlayPause(SoundModificationBase soundBase)
        {
            bool isPlayingHere = EditorAudioPreviewer.IsPlaying && EditorAudioPreviewer.CurrentTarget == soundBase;
            if (!isPlayingHere) EditorAudioPreviewer.PlayPreview(soundBase);
            else if (EditorAudioPreviewer.IsPaused) EditorAudioPreviewer.ResumePreview();
            else EditorAudioPreviewer.PausePreview();
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
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(viewHeight));

            float baseWidth = EditorGUIUtility.currentViewWidth - 40f;
            if (baseWidth < 300f) baseWidth = 300f;
            float expandedWidth = baseWidth * _zoomX;

            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, innerTimelineHeight, GUILayout.ExpandWidth(false));
            GUI.Box(timelineRect, GUIContent.none, EditorStyles.helpBox);

            Rect trackAreaRect = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, timelineRect.height - headerHeight);
            EditorGUI.DrawRect(trackAreaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            DrawZeroSecondLine(trackAreaRect, timelineStartTime, timelineDuration);
            DrawTimelineRuler(new Rect(timelineRect.x, timelineRect.y, timelineRect.width, headerHeight), timelineStartTime, timelineEndTime, timelineDuration);

            for (int i = 0; i < clips.Count; i++)
            {
                DrawClipBlock(clips[i], trackAreaRect, trackIndices[i], timelineStartTime, timelineDuration, trackHeight, i, soundBase);
            }

            DrawPlayheadAndHandleEvents(timelineRect, trackAreaRect, soundBase, timelineStartTime, timelineDuration, maxEnd);
            
            EditorGUILayout.EndScrollView();
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

        private void DrawClipBlock(PlayableClipData clipData, Rect trackAreaRect, int trackIndex, float timelineStartTime, float timelineDuration, float trackHeight, int clipIndex, SoundModificationBase targetBase)
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

            Texture2D waveformTex = GetWaveformTexture(clipData.Clip, 256, 64, baseColor, new Color(0f, 0f, 0f, 0f));
            if (waveformTex != null)
            {
                float volumeScale = Mathf.Clamp01(clipData.Volume); 
                if (clipData.Filters != null && clipData.Filters.enableDistortion) volumeScale = Mathf.Clamp01(volumeScale + clipData.Filters.distortionLevel);
                
                float waveHeight = fullBlockRect.height * volumeScale;
                Rect waveRect = new Rect(fullBlockRect.x, fullBlockRect.y + ((fullBlockRect.height - waveHeight) / 2f), fullBlockRect.width, waveHeight);
                
                // Draw Base Waveform
                GUI.DrawTexture(waveRect, waveformTex, ScaleMode.StretchToFill);

                // 2. Draw Ghosted Echo Waveforms
                if (clipData.Filters != null && clipData.Filters.enableEcho)
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

    public static class EditorAudioPreviewer
    {
        private static GameObject _previewGO;
        private static readonly List<PreviewClip> _pendingClips = new();
        
        private static double _startTime;
        private static double _pauseTime;
        private static SoundModificationBase _currentTarget;

        public static bool IsPlaying => _previewGO != null && _pendingClips.Count > 0;
        public static bool IsPaused { get; private set; }
        public static SoundModificationBase CurrentTarget => _currentTarget;

        public static float TotalDuration
        {
            get
            {
                float max = 0.05f;
                foreach (var clip in _pendingClips)
                {
                    float fullDur = clip.Data.Clip.length / Mathf.Max(0.001f, clip.Data.Pitch);
                    float trueStart = clip.Data.Delay - (clip.Data.StartOffset / Mathf.Max(0.001f, clip.Data.Pitch));
                    if (trueStart + fullDur > max) max = trueStart + fullDur;
                }
                return max;
            }
        }

        public static float CurrentTime
        {
            get
            {
                if (!IsPlaying) return 0f;
                if (IsPaused) return Mathf.Clamp((float)(_pauseTime - _startTime), 0f, TotalDuration);
                return Mathf.Clamp((float)(EditorApplication.timeSinceStartup - _startTime), 0f, TotalDuration);
            }
        }

        private class PreviewClip
        {
            public PlayableClipData Data;
            public bool Played;
            public float VolMult;
            public float PitchMult;
            public AudioSource Source;
            public double StartPlayTime;
            public double Duration;
        }

        public static void PlayPreview(SoundModificationBase soundBase, float volMult = 1f, float pitchMult = 1f)
        {
            StopPreview();
            if (soundBase == null) return;

            _currentTarget = soundBase;
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview_Hidden", HideFlags.HideAndDontSave);

            LoadClips(soundBase, volMult, pitchMult);

            if (_pendingClips.Count == 0) { StopPreview(); return; }

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        private static void LoadClips(SoundModificationBase soundBase, float volMult, float pitchMult)
        {
            _pendingClips.Clear();
            List<PlayableClipData> clips = new();
            soundBase.CollectPlayableClips(clips);

            foreach (var clip in clips)
            {
                if (clip.Clip != null)
                {
                    float activeDur = clip.Clip.length - clip.StartOffset - clip.EndOffset;
                    if (activeDur > 0.001f)
                    {
                        _pendingClips.Add(new PreviewClip { Data = clip, Played = false, VolMult = volMult, PitchMult = pitchMult });
                    }
                }
            }
        }

        public static void RefreshLiveClips(SoundModificationBase soundBase)
        {
            if (!IsPlaying) return;

            List<PlayableClipData> newClips = new();
            soundBase.CollectPlayableClips(newClips);

            double currentTime = CurrentTime;

            for (int i = 0; i < Mathf.Min(_pendingClips.Count, newClips.Count); i++)
            {
                var pClip = _pendingClips[i];
                var newClipData = newClips[i];

                pClip.Data = newClipData;
                pClip.Duration = (newClipData.Clip.length - newClipData.StartOffset - newClipData.EndOffset) / Mathf.Max(0.001f, newClipData.Pitch);

                if (pClip.Source != null)
                {
                    pClip.Source.volume = newClipData.Volume * pClip.VolMult;
                    pClip.Source.pitch = newClipData.Pitch * pClip.PitchMult;
                    if (newClipData.PreferredMixerGroup != null)
                        pClip.Source.outputAudioMixerGroup = newClipData.PreferredMixerGroup;
                }

                if (currentTime < newClipData.Delay)
                {
                    pClip.Played = false;
                    if (pClip.Source != null && pClip.Source.isPlaying) pClip.Source.Stop();
                }
            }
        }

        public static void PausePreview()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _pauseTime = EditorApplication.timeSinceStartup;
            
            foreach (var pClip in _pendingClips)
                if (pClip.Source != null && pClip.Source.isPlaying) pClip.Source.Pause();
        }

        public static void ResumePreview()
        {
            if (!IsPlaying || !IsPaused) return;
            IsPaused = false;
            
            double pausedDuration = EditorApplication.timeSinceStartup - _pauseTime;
            _startTime += pausedDuration; 
            
            double currentTime = CurrentTime;
            
            foreach (var pClip in _pendingClips)
            {
                float clipStartTime = pClip.Data.Delay;
                float clipDuration = (pClip.Data.Clip.length - pClip.Data.StartOffset - pClip.Data.EndOffset) / Mathf.Max(0.001f, pClip.Data.Pitch);
                float clipEndTime = clipStartTime + clipDuration;

                if (currentTime < clipStartTime)
                {
                    pClip.Played = false;
                    if (pClip.Source != null) pClip.Source.Stop();
                }
                else if (currentTime >= clipStartTime && currentTime < clipEndTime)
                {
                    float progressInClip = (float)(currentTime - clipStartTime) * pClip.Data.Pitch;
                    float clipTime = pClip.Data.StartOffset + progressInClip;

                    if (pClip.Source == null)
                    {
                        GameObject clipGO = new GameObject("PreviewClip");
                        clipGO.transform.parent = _previewGO.transform;
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        // Apply the filters to this isolated GameObject!
                        pClip.Data.Filters?.ApplyTo(pClip.Source);
                        pClip.Source.clip = pClip.Data.Clip;
                        pClip.Source.volume = pClip.Data.Volume * pClip.VolMult;
                        pClip.Source.pitch = pClip.Data.Pitch * pClip.PitchMult;
                        if (pClip.Data.PreferredMixerGroup != null) pClip.Source.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;
                    }

                    pClip.Source.time = Mathf.Clamp(clipTime, 0f, pClip.Data.Clip.length - 0.001f);
                    pClip.Duration = clipDuration;
                    pClip.StartPlayTime = EditorApplication.timeSinceStartup - (currentTime - clipStartTime);
                    pClip.Played = true;
                    
                    if (!pClip.Source.isPlaying) pClip.Source.Play();
                }
                else
                {
                    pClip.Played = true;
                    if (pClip.Source != null) pClip.Source.Stop();
                }
            }
        }

        public static void Seek(float targetTime)
        {
            if (!IsPlaying) return;

            targetTime = Mathf.Clamp(targetTime, 0f, TotalDuration);
            
            if (IsPaused) _pauseTime = EditorApplication.timeSinceStartup;
            _startTime = EditorApplication.timeSinceStartup - targetTime;

            foreach (var pClip in _pendingClips)
            {
                float clipStartTime = pClip.Data.Delay;
                float clipDuration = (pClip.Data.Clip.length - pClip.Data.StartOffset - pClip.Data.EndOffset) / Mathf.Max(0.001f, pClip.Data.Pitch);
                float clipEndTime = clipStartTime + clipDuration;

                if (targetTime >= clipStartTime && targetTime < clipEndTime)
                {
                    float progressInClip = (targetTime - clipStartTime) * pClip.Data.Pitch;
                    float clipTime = pClip.Data.StartOffset + progressInClip;

                    if (pClip.Source == null)
                    {
                        GameObject clipGO = new GameObject("PreviewClip");
                        clipGO.transform.parent = _previewGO.transform;
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        // Apply the filters to this isolated GameObject!
                        pClip.Data.Filters?.ApplyTo(pClip.Source);
                        pClip.Source.clip = pClip.Data.Clip;
                        pClip.Source.volume = pClip.Data.Volume * pClip.VolMult;
                        pClip.Source.pitch = pClip.Data.Pitch * pClip.PitchMult;
                        if (pClip.Data.PreferredMixerGroup != null) pClip.Source.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;
                    }

                    pClip.Source.time = Mathf.Clamp(clipTime, 0f, pClip.Data.Clip.length - 0.001f);
                    
                    if (!IsPaused && !pClip.Source.isPlaying) pClip.Source.Play();
                    else if (IsPaused && pClip.Source.isPlaying) pClip.Source.Pause();

                    pClip.Duration = clipDuration;
                    pClip.StartPlayTime = EditorApplication.timeSinceStartup - (targetTime - clipStartTime);
                    pClip.Played = true;
                }
                else
                {
                    if (pClip.Source != null) pClip.Source.Stop();
                    pClip.Played = targetTime >= clipEndTime;
                }
            }
        }

        public static void StopPreview()
        {
            EditorApplication.update -= EditorUpdate;
            IsPaused = false;
            if (_previewGO != null) GameObject.DestroyImmediate(_previewGO);
            _pendingClips.Clear();
            _currentTarget = null;
        }

        private static void EditorUpdate()
        {
            if (_previewGO == null || IsPaused) return;

            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            bool isAnythingStillPlaying = false;
            bool allClipsStarted = true;

            foreach (var pClip in _pendingClips)
            {
                // Calculate how long the filter tails need to survive after the clip ends
                float tailDuration = 0f;
                if (pClip.Data.Filters != null)
                {
                    if (pClip.Data.Filters.enableEcho) tailDuration = (pClip.Data.Filters.echoDelay / 1000f) * 5f; // Roughly 5 bounces
                    if (pClip.Data.Filters.enableReverb) tailDuration = Mathf.Max(tailDuration, 3f); // Standard reverb tail
                }

                if (!pClip.Played)
                {
                    allClipsStarted = false;
                    if (elapsed >= pClip.Data.Delay)
                    {
                        GameObject clipGO = new GameObject("PreviewClip");
                        clipGO.transform.parent = _previewGO.transform;
                        
                        pClip.Source = clipGO.AddComponent<AudioSource>();
                        pClip.Source.clip = pClip.Data.Clip;
                        pClip.Source.volume = pClip.Data.Volume * pClip.VolMult;
                        pClip.Source.pitch = pClip.Data.Pitch * pClip.PitchMult;
                        if (pClip.Data.PreferredMixerGroup != null) pClip.Source.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;

                        pClip.Data.Filters?.ApplyTo(pClip.Source);

                        pClip.Source.time = pClip.Data.StartOffset;
                        pClip.Duration = (pClip.Data.Clip.length - pClip.Data.StartOffset - pClip.Data.EndOffset) / Mathf.Max(0.001f, pClip.Source.pitch);
                        
                        // FIX: Use SetScheduledEndTime to stop reading the clip, but keep the AudioSource alive for the echo tail!
                        pClip.Source.Play();
                        pClip.Source.SetScheduledEndTime(AudioSettings.dspTime + pClip.Duration);

                        pClip.StartPlayTime = EditorApplication.timeSinceStartup;
                        pClip.Played = true;
                        isAnythingStillPlaying = true;
                    }
                }
                else
                {
                    if (pClip.Source != null && pClip.Source.gameObject != null)
                    {
                        // Wait for the duration PLUS the tail before physically destroying the source
                        if (EditorApplication.timeSinceStartup - pClip.StartPlayTime >= (pClip.Duration + tailDuration)) 
                        {
                            GameObject.DestroyImmediate(pClip.Source.gameObject);
                        }
                        else 
                        {
                            isAnythingStillPlaying = true;
                        }
                    }
                }
            }

            if (allClipsStarted && !isAnythingStillPlaying) StopPreview();
        }
    }
}
#endif