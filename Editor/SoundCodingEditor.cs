#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomPropertyDrawer(typeof(SoundPartAttribute))]
    public class SoundPartDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var targetObj = property.serializedObject.targetObject as SoundCoding;
            if (targetObj == null) { EditorGUI.PropertyField(position, property, label); return; }

            var parts = targetObj.parts.Select(p => p.name).ToList();
            parts.Insert(0, "<None>");

            string currentVal = property.stringValue;
            int currentIndex = string.IsNullOrEmpty(currentVal) ? 0 : parts.IndexOf(currentVal);
            if (currentIndex == -1) currentIndex = 0;

            currentIndex = EditorGUI.Popup(position, label.text, currentIndex, parts.ToArray());
            property.stringValue = currentIndex == 0 ? "" : parts[currentIndex];
        }
    }

    [CustomEditor(typeof(SoundCoding))]
    public class SoundCodingEditor : UnityEditor.Editor
    {
        private enum TimelineViewMode { Part, Transition }
        private TimelineViewMode _viewMode = TimelineViewMode.Part;

        private int _selectedPartIndex = 0;
        private int _selectedTransitionIndex = 0;

        private static readonly Dictionary<AudioClip, Texture2D> _waveformCache = new();
        private static float _zoomX = 1f;
        private static float _zoomY = 1f;
        private Vector2 _timelineScrollPos;

        private bool _isDraggingFadeOut;
        private bool _isDraggingFadeIn;
        private bool _isDraggingPlayhead;

        private void OnEnable() { EditorApplication.update += RepaintOnPreview; }
        private void OnDisable() { EditorApplication.update -= RepaintOnPreview; EditorCodingPreviewer.StopPreview(); }
        
        private void RepaintOnPreview()
        {
            if (EditorCodingPreviewer.IsPlaying && EditorCodingPreviewer.CurrentTarget == target) Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var codingAsset = (SoundCoding)target;

            DrawDefaultInspector();
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live FMOD Interactive Transport", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string[] partNames = codingAsset.parts.Select(p => p.name).ToArray();
            if (partNames.Length == 0)
            {
                EditorGUILayout.HelpBox("Create at least one Part to preview.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (EditorCodingPreviewer.IsPlaying && !string.IsNullOrEmpty(EditorCodingPreviewer.CurrentPartName))
            {
                int idx = System.Array.IndexOf(partNames, EditorCodingPreviewer.CurrentPartName);
                if (idx >= 0) _selectedPartIndex = idx;
            }

            if (_selectedPartIndex >= partNames.Length) _selectedPartIndex = 0;
            string activePartName = partNames[_selectedPartIndex];

            // --- TRANSPORT BUTTONS (Play, Pause, Stop) ---
            GUILayout.BeginHorizontal();
            
            bool isPlayingHere = EditorCodingPreviewer.IsPlaying;

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(isPlayingHere ? "▶ Restart" : "▶ Play", GUILayout.Height(30), GUILayout.Width(75)))
            {
                if (_viewMode == TimelineViewMode.Part)
                {
                    EditorCodingPreviewer.PlayPreview(codingAsset, activePartName);
                }
                else if (codingAsset.transitions.Count > 0 && _selectedTransitionIndex < codingAsset.transitions.Count)
                {
                    var trans = codingAsset.transitions[_selectedTransitionIndex];
                    EditorCodingPreviewer.PlayTransitionPreview(codingAsset, trans);
                }
            }

            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button(EditorCodingPreviewer.IsPaused ? "Resume" : "Pause", GUILayout.Height(30), GUILayout.Width(75)))
            {
                if (EditorCodingPreviewer.IsPaused) EditorCodingPreviewer.ResumePreview();
                else EditorCodingPreviewer.PausePreview();
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("■ Stop", GUILayout.Height(30), GUILayout.Width(65)))
            {
                EditorCodingPreviewer.StopPreview();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            
            // --- TIMELINE MODE SELECTION ---
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Timeline Mode:", GUILayout.Width(100));
            _viewMode = (TimelineViewMode)EditorGUILayout.EnumPopup(_viewMode, GUILayout.MinWidth(120), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // --- ZOOM CONTROLS ---
            GUILayout.BeginHorizontal();
            GUILayout.Label("Zoom X:", GUILayout.Width(55));
            _zoomX = GUILayout.HorizontalSlider(_zoomX, 1f, 10f, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.Space(15);
            GUILayout.Label("Zoom Y:", GUILayout.Width(55));
            _zoomY = GUILayout.HorizontalSlider(_zoomY, 1f, 10f, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (_viewMode == TimelineViewMode.Part)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Select Part:", GUILayout.Width(90));
                int newPartIdx = EditorGUILayout.Popup(_selectedPartIndex, partNames);
                if (newPartIdx != _selectedPartIndex)
                {
                    _selectedPartIndex = newPartIdx;
                    if (EditorCodingPreviewer.IsPlaying) EditorCodingPreviewer.TransitionTo(partNames[newPartIdx], true);
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                DrawPartTimelineArea(codingAsset, activePartName);
            }
            else 
            {
                if (codingAsset.transitions.Count == 0)
                {
                    EditorGUILayout.HelpBox("Add at least one Transition in the array above to preview crossfades.", MessageType.Info);
                }
                else
                {
                    string[] transNames = codingAsset.transitions
                        .Select(t => $"{t.fromPart} ➔ {t.toPart} (Out: {t.fadeOutDuration}s / In: {t.fadeInDuration}s)")
                        .ToArray();

                    if (_selectedTransitionIndex >= transNames.Length) _selectedTransitionIndex = 0;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Select Transition:", GUILayout.Width(110));
                    _selectedTransitionIndex = EditorGUILayout.Popup(_selectedTransitionIndex, transNames);
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);
                    DrawTransitionTimelineArea(codingAsset, codingAsset.transitions[_selectedTransitionIndex]);
                }
            }

            if (EditorCodingPreviewer.IsPlaying)
            {
                EditorGUILayout.Space(5);
                GUIStyle statusStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                statusStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                EditorGUILayout.LabelField($"PLAYING: {EditorCodingPreviewer.CurrentPartName}", statusStyle, GUILayout.Height(25));
            }

            EditorGUILayout.EndVertical();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPartTimelineArea(SoundCoding coding, string partName)
        {
            var part = coding.parts.FirstOrDefault(p => p.name == partName);
            if (part == null) return;

            float timelineDuration = part.endTime - part.startTime;
            if (timelineDuration <= 0f) return;

            var clips = coding.GetPartClips(partName);
            List<int> trackIndices = AssignTracks(clips);

            int maxTracks = 1;
            foreach (int idx in trackIndices) if (idx + 1 > maxTracks) maxTracks = idx + 1;

            float trackHeight = 36f * _zoomY;
            float headerHeight = 20f;
            float innerTimelineHeight = headerHeight + (maxTracks * trackHeight) + 10f;

            float viewHeight = Mathf.Min(innerTimelineHeight + 20f, 300f);
            _timelineScrollPos = EditorGUILayout.BeginScrollView(_timelineScrollPos, GUILayout.Height(viewHeight));

            float expandedWidth = (EditorGUIUtility.currentViewWidth - 50f) * _zoomX;
            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, innerTimelineHeight, GUILayout.ExpandWidth(false));
            GUI.Box(timelineRect, GUIContent.none, EditorStyles.helpBox);

            Rect trackAreaRect = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, timelineRect.height - headerHeight);
            EditorGUI.DrawRect(trackAreaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            DrawTimelineRuler(new Rect(timelineRect.x, timelineRect.y, timelineRect.width, headerHeight), timelineDuration);

            for (int i = 0; i < clips.Count; i++)
            {
                DrawClipBlock(clips[i], trackAreaRect, trackIndices[i], timelineDuration, trackHeight);
            }

            float currentPlayheadTime = (EditorCodingPreviewer.IsPlaying && EditorCodingPreviewer.CurrentPartName == partName) ? EditorCodingPreviewer.CurrentPartTime : 0f;
            DrawPlayheadAndHandleEvents(trackAreaRect, timelineDuration, currentPlayheadTime, coding, partName, null);

            EditorGUILayout.EndScrollView();
        }

        private void DrawTransitionTimelineArea(SoundCoding coding, SoundCoding.SoundTransition transition)
        {
            var fromClips = coding.GetPartClips(transition.fromPart);
            var toClips = coding.GetPartClips(transition.toPart);

            var fromPartData = coding.parts.FirstOrDefault(p => p.name == transition.fromPart);
            var toPartData = coding.parts.FirstOrDefault(p => p.name == transition.toPart);

            float fromDur = fromPartData != null ? (fromPartData.endTime - fromPartData.startTime) : 5f;
            float toDur = toPartData != null ? (toPartData.endTime - toPartData.startTime) : 5f;

            float transitionOffset = Mathf.Max(0f, fromDur - transition.fadeOutDuration);
            float totalDuration = transitionOffset + toDur;

            List<int> fromTrackIndices = AssignTracks(fromClips);
            List<int> toTrackIndices = AssignTracks(toClips);

            int maxFromTrack = fromTrackIndices.Count > 0 ? fromTrackIndices.Max() + 1 : 1;
            int maxToTrack = toTrackIndices.Count > 0 ? toTrackIndices.Max() + 1 : 1;
            int totalTracks = maxFromTrack + maxToTrack;

            float trackHeight = 36f * _zoomY;
            float headerHeight = 20f;
            float innerTimelineHeight = headerHeight + (totalTracks * trackHeight) + 15f;

            float viewHeight = Mathf.Min(innerTimelineHeight + 20f, 350f);
            _timelineScrollPos = EditorGUILayout.BeginScrollView(_timelineScrollPos, GUILayout.Height(viewHeight));

            float expandedWidth = (EditorGUIUtility.currentViewWidth - 50f) * _zoomX;
            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, innerTimelineHeight, GUILayout.ExpandWidth(false));
            GUI.Box(timelineRect, GUIContent.none, EditorStyles.helpBox);

            Rect trackAreaRect = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, timelineRect.height - headerHeight);
            EditorGUI.DrawRect(trackAreaRect, new Color(0.12f, 0.12f, 0.12f, 1f));

            DrawTimelineRuler(new Rect(timelineRect.x, timelineRect.y, timelineRect.width, headerHeight), totalDuration);

            for (int i = 0; i < fromClips.Count; i++)
            {
                DrawClipBlock(fromClips[i], trackAreaRect, fromTrackIndices[i], totalDuration, trackHeight, 0f, $"[OUT] {transition.fromPart}");
            }

            float fadeOutStartX = trackAreaRect.x + ((transitionOffset / totalDuration) * trackAreaRect.width);
            float fadeOutWidth = (transition.fadeOutDuration / totalDuration) * trackAreaRect.width;
            Rect fadeOutRect = new Rect(fadeOutStartX, trackAreaRect.y, fadeOutWidth, maxFromTrack * trackHeight);
            
            EditorGUI.DrawRect(fadeOutRect, new Color(1f, 0.2f, 0.2f, 0.25f));
            Handles.DrawSolidRectangleWithOutline(fadeOutRect, Color.clear, new Color(1f, 0.3f, 0.3f, 0.8f));

            Rect fadeOutHandleRect = new Rect(fadeOutRect.xMax - 4f, fadeOutRect.y, 8f, fadeOutRect.height);
            EditorGUIUtility.AddCursorRect(fadeOutHandleRect, MouseCursor.ResizeHorizontal);

            float dividerY = trackAreaRect.y + (maxFromTrack * trackHeight) + 2f;
            EditorGUI.DrawRect(new Rect(trackAreaRect.x, dividerY, trackAreaRect.width, 2f), new Color(0.5f, 0.5f, 0.5f, 0.8f));

            for (int i = 0; i < toClips.Count; i++)
            {
                int trackShifted = maxFromTrack + toTrackIndices[i];
                DrawClipBlock(toClips[i], trackAreaRect, trackShifted, totalDuration, trackHeight, transitionOffset, $"[IN] {transition.toPart}");
            }

            float fadeInWidth = (transition.fadeInDuration / totalDuration) * trackAreaRect.width;
            Rect fadeInRect = new Rect(fadeOutStartX, trackAreaRect.y + (maxFromTrack * trackHeight), fadeInWidth, maxToTrack * trackHeight);
            
            EditorGUI.DrawRect(fadeInRect, new Color(0.2f, 1f, 0.2f, 0.25f));
            Handles.DrawSolidRectangleWithOutline(fadeInRect, Color.clear, new Color(0.3f, 1f, 0.3f, 0.8f));

            Rect fadeInHandleRect = new Rect(fadeInRect.xMax - 4f, fadeInRect.y, 8f, fadeInRect.height);
            EditorGUIUtility.AddCursorRect(fadeInHandleRect, MouseCursor.ResizeHorizontal);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (fadeOutHandleRect.Contains(e.mousePosition)) { _isDraggingFadeOut = true; e.Use(); }
                else if (fadeInHandleRect.Contains(e.mousePosition)) { _isDraggingFadeIn = true; e.Use(); }
            }
            if (e.type == EventType.MouseDrag)
            {
                if (_isDraggingFadeOut)
                {
                    float newX = Mathf.Clamp(e.mousePosition.x, trackAreaRect.x, trackAreaRect.xMax);
                    float newDur = ((newX - fadeOutStartX) / trackAreaRect.width) * totalDuration;
                    transition.fadeOutDuration = Mathf.Clamp(newDur, 0.05f, fromDur);
                    EditorUtility.SetDirty(coding);
                    e.Use();
                    Repaint();
                }
                else if (_isDraggingFadeIn)
                {
                    float newX = Mathf.Clamp(e.mousePosition.x, fadeOutStartX, trackAreaRect.xMax);
                    float newDur = ((newX - fadeOutStartX) / trackAreaRect.width) * totalDuration;
                    transition.fadeInDuration = Mathf.Clamp(newDur, 0.05f, toDur);
                    EditorUtility.SetDirty(coding);
                    e.Use();
                    Repaint();
                }
            }
            if (e.type == EventType.MouseUp)
            {
                _isDraggingFadeOut = false;
                _isDraggingFadeIn = false;
            }

            float currentPlayheadTime = EditorCodingPreviewer.IsPlaying ? EditorCodingPreviewer.CurrentPartTime : 0f;
            DrawPlayheadAndHandleEvents(trackAreaRect, totalDuration, currentPlayheadTime, coding, null, transition);

            EditorGUILayout.EndScrollView();
        }

        private List<int> AssignTracks(List<PlayableClipData> clips)
        {
            List<int> tracks = new();
            List<float> trackEndTimes = new();

            foreach (var clip in clips)
            {
                float pitch = Mathf.Max(0.001f, clip.Pitch);
                float fullDur = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / pitch;
                float startTime = clip.Delay;
                float endTime = startTime + fullDur;

                int assignedTrack = -1;
                for (int t = 0; t < trackEndTimes.Count; t++)
                {
                    if (startTime >= trackEndTimes[t])
                    {
                        assignedTrack = t; trackEndTimes[t] = endTime; break;
                    }
                }

                if (assignedTrack == -1)
                {
                    assignedTrack = trackEndTimes.Count; trackEndTimes.Add(endTime);
                }
                tracks.Add(assignedTrack);
            }
            return tracks;
        }

        private void DrawTimelineRuler(Rect rulerRect, float duration)
        {
            EditorGUI.DrawRect(rulerRect, new Color(0.18f, 0.18f, 0.18f, 1f));
            int endSec = Mathf.CeilToInt(duration);

            for (int sec = 0; sec <= endSec; sec++)
            {
                float normX = sec / duration;
                float xPos = rulerRect.x + (normX * rulerRect.width);

                EditorGUI.DrawRect(new Rect(xPos, rulerRect.y + rulerRect.height - 6f, 1f, 6f), new Color(0.6f, 0.6f, 0.6f, 1f));

                string label = $"{sec}s";
                GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { fontSize = 9, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                Vector2 labelSize = style.CalcSize(new GUIContent(label));
                GUI.Label(new Rect(Mathf.Clamp(xPos - (labelSize.x / 2f), rulerRect.x, rulerRect.xMax - labelSize.x), rulerRect.y, labelSize.x, labelSize.y), label, style);
            }
        }

        private void DrawClipBlock(PlayableClipData clipData, Rect trackAreaRect, int trackIndex, float timelineDuration, float trackHeight, float timeOffset = 0f, string prefix = "")
        {
            float pitch = Mathf.Max(0.001f, clipData.Pitch);
            float trueStart = clipData.Delay + timeOffset;
            float fullDur = (clipData.Clip.length - clipData.StartOffset - clipData.EndOffset) / pitch;

            float startNorm = trueStart / timelineDuration;
            float durNorm = fullDur / timelineDuration;

            Rect blockRect = new Rect(
                trackAreaRect.x + (startNorm * trackAreaRect.width), trackAreaRect.y + (trackIndex * trackHeight) + 2f, 
                Mathf.Max(durNorm * trackAreaRect.width, 4f), trackHeight - 4f
            );

            EditorGUI.DrawRect(blockRect, new Color(0.15f, 0.25f, 0.35f, 0.9f));

            Color baseColor = clipData.Filters != null && clipData.Filters.enableDistortion 
                ? Color.Lerp(new Color(0.4f, 0.8f, 1f, 0.8f), new Color(1f, 0.3f, 0.1f, 0.9f), clipData.Filters.distortionLevel) 
                : new Color(0.4f, 0.8f, 1f, 0.8f);

            Texture2D waveformTex = GetWaveformTexture(clipData.Clip, 256, 64, baseColor, Color.clear);
            if (waveformTex != null)
            {
                float vol = Mathf.Clamp01(clipData.Volume + (clipData.Filters != null && clipData.Filters.enableDistortion ? clipData.Filters.distortionLevel : 0f));
                float wh = blockRect.height * vol;
                Rect waveRect = new Rect(blockRect.x, blockRect.y + (blockRect.height - wh)/2f, blockRect.width, wh);
                GUI.DrawTexture(waveRect, waveformTex, ScaleMode.StretchToFill);
            }
            
            string labelText = string.IsNullOrEmpty(prefix) ? clipData.Clip.name : $"{prefix}: {clipData.Clip.name}";
            GUI.Label(new Rect(blockRect.x + 4f, blockRect.y + 2f, blockRect.width - 8f, 16f), labelText, EditorStyles.whiteMiniLabel);
        }

        private void DrawPlayheadAndHandleEvents(Rect trackAreaRect, float timelineDuration, float currentPlayheadTime, SoundCoding coding, string partName, SoundCoding.SoundTransition transition)
        {
            float playheadNorm = Mathf.Clamp01(currentPlayheadTime / timelineDuration);
            float playheadX = trackAreaRect.x + (playheadNorm * trackAreaRect.width);

            EditorGUI.DrawRect(new Rect(playheadX - 1f, trackAreaRect.y, 2f, trackAreaRect.height), Color.red);
            Vector3[] headTriangle = new Vector3[] {
                new Vector3(playheadX - 5f, trackAreaRect.y), new Vector3(playheadX + 5f, trackAreaRect.y), new Vector3(playheadX, trackAreaRect.y + 8f)
            };
            Handles.color = Color.red;
            Handles.DrawAAConvexPolygon(headTriangle);

            Event e = Event.current;
            if (trackAreaRect.Contains(e.mousePosition) || _isDraggingPlayhead)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && !_isDraggingFadeOut && !_isDraggingFadeIn)
                {
                    _isDraggingPlayhead = true;
                    if (EditorCodingPreviewer.IsPlaying && !EditorCodingPreviewer.IsPaused) EditorCodingPreviewer.PausePreview();
                    
                    float seekTime = Mathf.Clamp(((e.mousePosition.x - trackAreaRect.x) / trackAreaRect.width) * timelineDuration, 0f, timelineDuration);
                    if (transition != null) EditorCodingPreviewer.PlayTransitionPreview(coding, transition, seekTime);
                    else EditorCodingPreviewer.PlayPreview(coding, partName, seekTime);
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _isDraggingPlayhead)
                {
                    float seekTime = Mathf.Clamp(((e.mousePosition.x - trackAreaRect.x) / trackAreaRect.width) * timelineDuration, 0f, timelineDuration);
                    if (transition != null) EditorCodingPreviewer.PlayTransitionPreview(coding, transition, seekTime);
                    else EditorCodingPreviewer.PlayPreview(coding, partName, seekTime);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && _isDraggingPlayhead)
                {
                    _isDraggingPlayhead = false;
                    e.Use();
                }
            }
        }

        private static Texture2D GetWaveformTexture(AudioClip clip, int width, int height, Color waveCol, Color bgCol)
        {
            if (clip == null) return null;
            if (_waveformCache.TryGetValue(clip, out var cached) && cached != null) return cached;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgCol;

            float[] sampleData = new float[clip.samples * clip.channels];
            if (clip.GetData(sampleData, 0))
            {
                int step = Mathf.Max(1, (clip.samples * clip.channels) / width);
                int half = height / 2;
                for (int x = 0; x < width; x++)
                {
                    float max = 0f;
                    for (int s = 0; s < step && (x * step + s) < sampleData.Length; s++)
                        if (Mathf.Abs(sampleData[x * step + s]) > max) max = Mathf.Abs(sampleData[x * step + s]);

                    int lh = Mathf.Clamp(Mathf.RoundToInt(max * half), 1, half);
                    for (int y = half - lh; y <= half + lh; y++)
                        if (y >= 0 && y < height) pixels[y * width + x] = waveCol;
                }
            }
            tex.SetPixels(pixels); tex.Apply();
            _waveformCache[clip] = tex; return tex;
        }
    }

    // ==========================================================
    // EDITOR DSP SCHEDULER
    // ==========================================================
    public static class EditorCodingPreviewer
    {
        private static GameObject _previewGO;
        public static SoundCoding CurrentTarget { get; private set; }
        public static bool IsPlaying => _previewGO != null;
        public static bool IsPaused { get; private set; }
        public static string CurrentPartName { get; private set; }
        public static float CurrentPartTime => _currentPartData != null ? _partElapsedTime : _transitionElapsedTime;

        private static double _nextEventTime;
        private static SoundCoding.SoundPart _currentPartData;
        private static List<EditorCodingClip> _activeClips = new List<EditorCodingClip>();
        private static double _lastEditorTime;
        private static double _pauseTime;
        private static float _partElapsedTime;
        private static float _transitionElapsedTime;
        private static bool _isTransitionMode;

        private class EditorCodingClip
        {
            public AudioSource Source;
            public float TargetVolume;
            public bool IsFadingOut;
            public float FadeOutTime;
            public float FadeOutTimer;
            public bool IsFadingIn;
            public float FadeInTime;
            public float FadeInTimer;
            public double DestroyTime;
        }

        public static void PlayPreview(SoundCoding coding, string startPartName, float startOffset = 0f)
        {
            StopPreview();
            if (coding == null || coding.parts.Count == 0) return;

            CurrentTarget = coding;
            _isTransitionMode = false;
            IsPaused = false;
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioCoding_Preview", HideFlags.HideAndDontSave);
            
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;

            TransitionTo(startPartName, true, startOffset);
        }

        public static void PlayTransitionPreview(SoundCoding coding, SoundCoding.SoundTransition transition, float startOffset = 0f)
        {
            StopPreview();
            if (coding == null) return;

            CurrentTarget = coding;
            _isTransitionMode = true;
            IsPaused = false;
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioCoding_Preview", HideFlags.HideAndDontSave);

            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;

            CurrentPartName = $"{transition.fromPart} ➔ {transition.toPart}";
            _transitionElapsedTime = startOffset;
            _nextEventTime = AudioSettings.dspTime + 0.1f;

            ScheduleClipsForTransition(transition, startOffset);
        }

        public static void PausePreview()
        {
            if (!IsPlaying || IsPaused) return;
            IsPaused = true;
            _pauseTime = EditorApplication.timeSinceStartup;
            
            foreach (var c in _activeClips)
                if (c.Source != null && c.Source.isPlaying) c.Source.Pause();
        }

        public static void ResumePreview()
        {
            if (!IsPlaying || !IsPaused) return;
            IsPaused = false;
            
            double pausedDuration = EditorApplication.timeSinceStartup - _pauseTime;
            _nextEventTime += pausedDuration;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            
            foreach (var c in _activeClips)
            {
                if (c.Source != null && !c.Source.isPlaying) c.Source.UnPause();
            }
        }

        private static void ScheduleClipsForTransition(SoundCoding.SoundTransition transition, float playheadOffset)
        {
            var fromPartData = CurrentTarget.parts.FirstOrDefault(p => p.name == transition.fromPart);
            float fromDur = fromPartData != null ? (fromPartData.endTime - fromPartData.startTime) : 5f;
            float transitionBoundary = Mathf.Max(0f, fromDur - transition.fadeOutDuration);

            ScheduleClipsForPartInternal(transition.fromPart, 0f, playheadOffset, transition.fadeOutDuration, 0f, true, transitionBoundary);
            ScheduleClipsForPartInternal(transition.toPart, transitionBoundary, playheadOffset, 0f, transition.fadeInDuration, false, 0f);
        }

        public static void TransitionTo(string nextPartName, bool immediate = false, float startOffset = 0f)
        {
            if (CurrentTarget == null || _isTransitionMode) return;
            var nextPart = CurrentTarget.parts.FirstOrDefault(p => p.name == nextPartName);
            if (nextPart == null) return;

            float fadeOutTime = 0f, fadeInTime = 0f;
            if (!immediate && !string.IsNullOrEmpty(CurrentPartName))
            {
                var transition = CurrentTarget.transitions.FirstOrDefault(t => t.fromPart == CurrentPartName && t.toPart == nextPartName);
                if (transition != null) { fadeOutTime = transition.fadeOutDuration; fadeInTime = transition.fadeInDuration; }
            }

            foreach (var c in _activeClips)
            {
                if (c.Source != null && c.Source.isPlaying)
                {
                    c.IsFadingOut = true;
                    c.FadeOutTime = fadeOutTime;
                    c.FadeOutTimer = 0f;
                    if (fadeOutTime <= 0f) GameObject.DestroyImmediate(c.Source.gameObject);
                }
            }
            _activeClips.RemoveAll(c => c.Source == null);

            CurrentPartName = nextPartName;
            _currentPartData = nextPart;
            _partElapsedTime = startOffset;
            _nextEventTime = AudioSettings.dspTime + 0.1f;
            
            ScheduleClipsForPartInternal(nextPartName, 0f, startOffset, 0f, fadeInTime, false, 0f);

            float partDur = nextPart.endTime - nextPart.startTime;
            _nextEventTime += (partDur - startOffset);
        }

        private static void ScheduleClipsForPartInternal(string partName, float timeDelayOffset, float playheadOffset, float fadeOutTime, float fadeInTime, bool isFromTransition, float fadeOutBoundaryTime)
        {
            var clipsToPlay = CurrentTarget.GetPartClips(partName);

            foreach (var clipData in clipsToPlay)
            {
                float pitch = Mathf.Max(0.001f, clipData.Pitch);
                float clipStart = clipData.Delay + timeDelayOffset;
                float rawDur = (clipData.Clip.length - clipData.StartOffset - clipData.EndOffset) / pitch;
                
                if (clipStart + rawDur <= playheadOffset) continue; 

                float actualDelay = clipStart - playheadOffset;
                float actualStartOffset = clipData.StartOffset;

                if (actualDelay < 0)
                {
                    actualStartOffset += (-actualDelay) * pitch;
                    actualDelay = 0f;
                }

                float actualDur = (clipData.Clip.length - actualStartOffset - clipData.EndOffset) / pitch;
                if (actualDur <= 0f) continue; 

                GameObject go = new GameObject($"[Preview] {clipData.Clip.name}");
                go.transform.parent = _previewGO.transform;
                var source = go.AddComponent<AudioSource>();
                
                source.clip = clipData.Clip;
                source.pitch = pitch;
                if (clipData.PreferredMixerGroup != null) source.outputAudioMixerGroup = clipData.PreferredMixerGroup;
                clipData.Filters?.ApplyTo(source);
                
                source.time = actualStartOffset;
                source.PlayScheduled(AudioSettings.dspTime + 0.1f + actualDelay);
                
                double scheduledEnd = AudioSettings.dspTime + 0.1f + actualDelay + actualDur;
                if (isFromTransition && fadeOutTime > 0f)
                {
                    double boundaryDsp = AudioSettings.dspTime + 0.1f + (fadeOutBoundaryTime - playheadOffset);
                    if (boundaryDsp < scheduledEnd) scheduledEnd = boundaryDsp + fadeOutTime;
                }
                source.SetScheduledEndTime(scheduledEnd);
                
                float targetVol = clipData.Volume;
                source.volume = fadeInTime > 0f ? 0f : targetVol; 

                float tail = clipData.Filters != null && (clipData.Filters.enableEcho || clipData.Filters.enableReverb) ? 4f : 0f;
                
                bool willFadeOut = isFromTransition && fadeOutTime > 0f;

                _activeClips.Add(new EditorCodingClip {
                    Source = source, 
                    TargetVolume = targetVol, 
                    DestroyTime = scheduledEnd + tail,
                    IsFadingIn = fadeInTime > 0f,
                    FadeInTime = fadeInTime,
                    FadeInTimer = 0f,
                    IsFadingOut = willFadeOut,
                    FadeOutTime = fadeOutTime,
                    FadeOutTimer = 0f
                });
            }
        }

        public static void StopPreview()
        {
            EditorApplication.update -= EditorUpdate;
            IsPaused = false;
            if (_previewGO != null) GameObject.DestroyImmediate(_previewGO);
            _activeClips.Clear();
            CurrentTarget = null;
            _currentPartData = null;
            CurrentPartName = "";
            _isTransitionMode = false;
        }

        private static void EditorUpdate()
        {
            if (_previewGO == null || IsPaused) return;
            float deltaTime = (float)(EditorApplication.timeSinceStartup - _lastEditorTime);
            _lastEditorTime = EditorApplication.timeSinceStartup;
            
            if (_isTransitionMode) _transitionElapsedTime += deltaTime;
            else _partElapsedTime += deltaTime;

            for (int i = _activeClips.Count - 1; i >= 0; i--)
            {
                var c = _activeClips[i];
                if (c.Source == null) { _activeClips.RemoveAt(i); continue; }

                if (c.IsFadingOut)
                {
                    if (AudioSettings.dspTime >= (c.DestroyTime - 4f - c.FadeOutTime))
                    {
                        c.FadeOutTimer += deltaTime;
                        c.Source.volume = Mathf.Lerp(c.TargetVolume, 0f, Mathf.Clamp01(c.FadeOutTimer / Mathf.Max(0.001f, c.FadeOutTime)));
                    }
                }
                
                if (c.IsFadingIn && c.Source.isPlaying)
                {
                    c.FadeInTimer += deltaTime;
                    c.Source.volume = Mathf.Lerp(0f, c.TargetVolume, Mathf.Clamp01(c.FadeInTimer / Mathf.Max(0.001f, c.FadeInTime)));
                    if (c.FadeInTimer >= c.FadeInTime) c.IsFadingIn = false;
                }
                
                if (c.Source != null && AudioSettings.dspTime > c.DestroyTime)
                {
                    GameObject.DestroyImmediate(c.Source.gameObject); _activeClips.RemoveAt(i);
                }
            }

            if (!_isTransitionMode && _currentPartData != null)
            {
                if (AudioSettings.dspTime >= _nextEventTime - 0.2f)
                {
                    if (_currentPartData.loop) 
                    { 
                        _partElapsedTime = 0f; 
                        ScheduleClipsForPartInternal(_currentPartData.name, 0f, 0f, 0f, 0f, false, 0f); 
                        _nextEventTime += (_currentPartData.endTime - _currentPartData.startTime);
                    }
                    else if (!string.IsNullOrEmpty(_currentPartData.defaultNextPart)) 
                    {
                        TransitionTo(_currentPartData.defaultNextPart, false);
                    }
                    else 
                    {
                        _currentPartData = null;
                    }
                }
            }
            else if (_activeClips.Count == 0 && !_isTransitionMode) 
            {
                StopPreview();
            }
        }
    }
}
#endif