#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomEditor(typeof(SoundCoding))]
    public class SoundCodingEditor : UnityEditor.Editor
    {
        private static float _zoomX = 1f;
        private Vector2 _scrollPos;
        
        // Dragging state
        private int _draggingIndex = -1;
        private int _draggingType = -1; 
        private int _draggingEdge = -1;
        private bool _isScrubbingPlayhead;
        private float _dragStartMouseX;
        private float _dragStartValue;
        private float _dragStartValue2;

        private static readonly Dictionary<AudioClip, Texture2D> _waveformCache = new();
        private static readonly Dictionary<string, Texture2D> _gradientCache = new();

        // Custom Previewer State
        private GameObject _previewGO;
        private AudioSource _primarySource;
        private AudioSource _secondarySource;
        
        private bool _isPlaying;
        private bool _isPaused;
        private float _previewTime;
        private double _lastEditorTime;
        private float _baseVolume = 1f;

        // Preview logic state
        private SoundCodingSection _isolatedSection;
        
        // Queuing State
        private SoundCodingTransition _queuedTransition;
        private float _queuedTransitionTriggerTime = -1f;
        private float _lastPlaybackTime = -1f;
        
        // Fade logic state
        private bool _isTransitioning;
        private float _transitionTimer;
        private float _transitionStartVolume;
        private SoundCodingTransition _activeTransitionData;
        
        // Bridge logic state
        private bool _isWaitingInBridge;
        private float _bridgeWaitTimer;

        private void OnEnable() => EditorApplication.update += EditorUpdate;
        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            StopPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SoundCoding coding = (SoundCoding)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("inputSound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("filters"));

            // Draw Default Starting Section Dropdown
            string[] secIds = coding.sections.Select(s => s.id).ToArray();
            string[] secNames = coding.sections.Select(s => s.name).ToArray();
            coding.defaultSectionId = DrawIdPopup("Default Start Section", coding.defaultSectionId, secIds, secNames, true);
            
            GUILayout.Space(15);
            DrawPlaybackControls(coding);
            GUILayout.Space(5);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Timeline Zoom:", GUILayout.Width(100));
            _zoomX = GUILayout.HorizontalSlider(_zoomX, 1f, 10f);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            DrawTimeline(coding);
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);
            DrawLists(coding);

            serializedObject.ApplyModifiedProperties();
        }

        #region Preview Playback
        private void DrawPlaybackControls(SoundCoding coding)
        {
            GUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(_isPlaying ? "▶ Restart" : "▶ Play All", GUILayout.Height(24)))
            {
                _isolatedSection = null;
                StartPreview(coding, 0f);
            }

            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button(_isPaused ? "Resume" : "Pause", GUILayout.Height(24)))
            {
                if (_isPlaying)
                {
                    _isPaused = !_isPaused;
                    if (_isPaused)
                    {
                        if (_primarySource != null) _primarySource.Pause();
                        if (_secondarySource != null) _secondarySource.Pause();
                    }
                    else
                    {
                        if (_primarySource != null) _primarySource.UnPause();
                        if (_secondarySource != null && _isTransitioning) _secondarySource.UnPause();
                    }
                }
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("■ Stop", GUILayout.Height(24)))
            {
                StopPreview();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        private void StartPreview(SoundCoding coding, float startTime)
        {
            StopPreview();
            if (coding.inputSound == null) return;

            List<PlayableClipData> clips = new();
            coding.inputSound.CollectPlayableClips(clips);
            if (clips.Count == 0 || clips[0].Clip == null) return;

            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("SoundCodingPreview", HideFlags.HideAndDontSave);
            
            _baseVolume = clips[0].Volume;

            _primarySource = _previewGO.AddComponent<AudioSource>();
            _primarySource.clip = clips[0].Clip;
            _primarySource.volume = _baseVolume;
            _primarySource.pitch = clips[0].Pitch;
            coding.Filters?.ApplyTo(_primarySource);
            
            _secondarySource = _previewGO.AddComponent<AudioSource>();
            _secondarySource.clip = clips[0].Clip;
            _secondarySource.volume = 0f;
            _secondarySource.pitch = clips[0].Pitch;
            coding.Filters?.ApplyTo(_secondarySource);

            _primarySource.time = Mathf.Clamp(startTime, 0f, clips[0].Clip.length - 0.01f);
            _primarySource.Play();
            
            _isPlaying = true;
            _isPaused = false;
            _previewTime = startTime;
            _lastPlaybackTime = startTime;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void PlaySpecificSection(SoundCoding coding, SoundCodingSection section)
        {
            _isolatedSection = section;
            StartPreview(coding, section.startTime);
        }

        private void TestTransition(SoundCoding coding, SoundCodingTransition trans)
        {
            float triggerTime = -1f;
            SoundCodingSection fromSec = null;

            var point = coding.transitionPoints.Find(p => p.targetTransitionId == trans.id);
            if (point != null)
            {
                fromSec = coding.GetSection(point.sectionId);
                triggerTime = fromSec.startTime + point.timeOffset;
            }
            else
            {
                fromSec = coding.GetSection(trans.fromSectionId);
                if (fromSec != null) triggerTime = fromSec.startTime + Mathf.Max(0f, fromSec.duration - trans.fadeOutDuration);
            }

            if (fromSec == null) return;

            _isolatedSection = null;
            
            float leadInTime = 2.5f;
            float startTime = Mathf.Max(fromSec.startTime, triggerTime - leadInTime);
            
            StartPreview(coding, startTime);
            
            // Queue the transition to happen when the playhead hits the exact target!
            _queuedTransition = trans;
            _queuedTransitionTriggerTime = triggerTime;
        }

        private void StopPreview()
        {
            _isPlaying = false;
            _isPaused = false;
            _isTransitioning = false;
            _isWaitingInBridge = false;
            _isolatedSection = null;
            _queuedTransition = null;
            _queuedTransitionTriggerTime = -1f;
            _lastPlaybackTime = -1f;

            if (_previewGO != null) DestroyImmediate(_previewGO);
        }

        private void StartActualTransition(SoundCoding coding, SoundCodingTransition transition, bool isBridgeExit)
        {
            if (!_isPlaying || transition == null) return;

            var targetSec = (!isBridgeExit && !string.IsNullOrEmpty(transition.bridgeSectionId)) ? 
                coding.GetSection(transition.bridgeSectionId) : coding.GetSection(transition.toSectionId);
                
            if (targetSec == null) return;

            _isTransitioning = true;
            _transitionTimer = 0f;
            _transitionStartVolume = _primarySource.volume;
            _activeTransitionData = transition;

            _secondarySource.time = Mathf.Clamp(targetSec.startTime, 0f, _secondarySource.clip.length - 0.01f);
            _secondarySource.Play();
        }

        private void EditorUpdate()
        {
            if (!_isPlaying || _isPaused)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            float dt = (float)(EditorApplication.timeSinceStartup - _lastEditorTime);
            _lastEditorTime = EditorApplication.timeSinceStartup;
            
            if (_isWaitingInBridge)
            {
                _bridgeWaitTimer -= dt;
                if (_bridgeWaitTimer <= 0f)
                {
                    _isWaitingInBridge = false;
                    StartActualTransition((SoundCoding)target, _activeTransitionData, isBridgeExit: true);
                }
            }

            if (_primarySource != null && _primarySource.isPlaying)
            {
                float currentTime = _primarySource.time;

                // 1. Process Queued Transition Crossing
                if (_queuedTransition != null && _queuedTransitionTriggerTime >= 0f)
                {
                    bool crossed = false;
                    if (_lastPlaybackTime >= 0f)
                    {
                        if (currentTime >= _lastPlaybackTime)
                            crossed = (_lastPlaybackTime <= _queuedTransitionTriggerTime && currentTime >= _queuedTransitionTriggerTime);
                        else 
                            crossed = (_lastPlaybackTime <= _queuedTransitionTriggerTime) || (currentTime >= _queuedTransitionTriggerTime);
                    }

                    if (crossed)
                    {
                        var transToFire = _queuedTransition;
                        _queuedTransition = null;
                        _queuedTransitionTriggerTime = -1f;
                        
                        StartActualTransition((SoundCoding)target, transToFire, isBridgeExit: false);
                    }
                }

                // 2. Process Section Looping
                if (_isolatedSection != null && !_isTransitioning)
                {
                    if (currentTime >= _isolatedSection.startTime + _isolatedSection.duration)
                    {
                        _primarySource.time = _isolatedSection.startTime;
                        currentTime = _isolatedSection.startTime;
                    }
                }

                _lastPlaybackTime = currentTime;
                _previewTime = currentTime;
            }

            if (_isTransitioning)
            {
                _transitionTimer += dt;
                
                float fadeOut = Mathf.Max(0.01f, _activeTransitionData.fadeOutDuration);
                float fadeIn = Mathf.Max(0.01f, _activeTransitionData.fadeInDuration);
                
                float tOut = Mathf.Clamp01(_transitionTimer / fadeOut);
                float tIn = Mathf.Clamp01(_transitionTimer / fadeIn);
                
                _primarySource.volume = _transitionStartVolume * Mathf.Cos(tOut * Mathf.PI / 2f);
                _secondarySource.volume = _baseVolume * Mathf.Sin(tIn * Mathf.PI / 2f);

                if (tOut >= 1f && tIn >= 1f)
                {
                    _isTransitioning = false;
                    
                    var temp = _primarySource;
                    _primarySource = _secondarySource;
                    _secondarySource = temp;
                    _secondarySource.Stop();

                    if (!_isWaitingInBridge && !string.IsNullOrEmpty(_activeTransitionData.bridgeSectionId))
                    {
                        var coding = (SoundCoding)target;
                        var bridgeSec = coding.GetSection(_activeTransitionData.bridgeSectionId);
                        
                        if (bridgeSec != null)
                        {
                            _isWaitingInBridge = true;
                            _bridgeWaitTimer = Mathf.Max(0f, bridgeSec.duration - _activeTransitionData.fadeInDuration);
                        }
                    }
                }
            }
            
            Repaint();
        }
        #endregion

        #region Timeline Rendering
        private void DrawTimeline(SoundCoding coding)
        {
            float timelineDuration = 10f;
            AudioClip baseClip = null;
            
            if (coding.inputSound != null)
            {
                List<PlayableClipData> clips = new();
                coding.inputSound.CollectPlayableClips(clips);
                if (clips.Count > 0 && clips[0].Clip != null)
                {
                    baseClip = clips[0].Clip;
                    timelineDuration = baseClip.length;
                }
            }

            float trackHeight = 100f;
            float headerHeight = 20f;
            
            float baseWidth = Mathf.Max(EditorGUIUtility.currentViewWidth - 40f, 300f);
            float expandedWidth = baseWidth * _zoomX;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(trackHeight + headerHeight + 20f));
            Rect timelineRect = GUILayoutUtility.GetRect(expandedWidth, trackHeight + headerHeight, GUILayout.ExpandWidth(false));
            
            Rect trackArea = new Rect(timelineRect.x, timelineRect.y + headerHeight, timelineRect.width, trackHeight);

            EditorGUI.DrawRect(trackArea, new Color(0.1f, 0.1f, 0.1f, 1f));
            if (baseClip != null)
            {
                Texture2D waveTex = GetWaveformTexture(baseClip, 512, 100, new Color(0.3f, 0.5f, 0.7f, 0.6f), Color.clear);
                GUI.DrawTexture(trackArea, waveTex, ScaleMode.StretchToFill);
            }

            for (int i = 0; i < coding.sections.Count; i++) DrawSectionBlock(coding.sections[i], i, trackArea, timelineDuration, coding);
            for (int i = 0; i < coding.transitionPoints.Count; i++) DrawTransitionBlock(coding.transitionPoints[i], i, trackArea, timelineDuration, coding);

            HandlePlayheadScrubbing(timelineRect, trackArea, timelineDuration, coding);
            EditorGUILayout.EndScrollView();
        }

        private void HandlePlayheadScrubbing(Rect timelineRect, Rect trackArea, float timelineDuration, SoundCoding coding)
        {
            Event e = Event.current;

            if (timelineRect.Contains(e.mousePosition) && _draggingIndex == -1)
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    _isScrubbingPlayhead = true;
                    SeekToMousePosition(e.mousePosition.x, trackArea, timelineDuration, coding);
                    e.Use();
                }
            }

            if (e.type == EventType.MouseDrag && _isScrubbingPlayhead)
            {
                SeekToMousePosition(e.mousePosition.x, trackArea, timelineDuration, coding);
                e.Use();
            }

            if (e.type == EventType.MouseUp && _isScrubbingPlayhead)
            {
                _isScrubbingPlayhead = false;
                e.Use();
            }

            if (_isPlaying)
            {
                float normTime = _previewTime / timelineDuration;
                float x = trackArea.x + (normTime * trackArea.width);
                
                EditorGUI.DrawRect(new Rect(x - 1f, timelineRect.y, 2f, timelineRect.height), Color.red);
                
                Vector3[] headPolygon = new Vector3[] {
                    new Vector3(x - 5f, timelineRect.y),
                    new Vector3(x + 5f, timelineRect.y),
                    new Vector3(x, timelineRect.y + 8f)
                };
                Handles.color = Color.red;
                Handles.DrawAAConvexPolygon(headPolygon);
                Handles.color = Color.white;
            }
        }

        private void SeekToMousePosition(float mouseX, Rect trackArea, float timelineDuration, SoundCoding coding)
        {
            float normX = Mathf.Clamp01((mouseX - trackArea.x) / trackArea.width);
            float targetTime = normX * timelineDuration;

            if (!_isPlaying)
            {
                _isolatedSection = null;
                StartPreview(coding, targetTime);
            }
            else if (_primarySource != null && _primarySource.clip != null)
            {
                _primarySource.time = Mathf.Clamp(targetTime, 0f, _primarySource.clip.length - 0.01f);
                _previewTime = _primarySource.time;
                _lastPlaybackTime = _primarySource.time;
            }
        }

        private void DrawSectionBlock(SoundCodingSection section, int index, Rect trackArea, float timelineDuration, SoundCoding coding)
        {
            float startNorm = section.startTime / timelineDuration;
            float durNorm = section.duration / timelineDuration;

            float x = trackArea.x + (startNorm * trackArea.width);
            float width = Mathf.Max(durNorm * trackArea.width, 4f);
            
            Rect blockRect = new Rect(x, trackArea.y + 15f, width, trackArea.height - 30f);

            EditorGUI.DrawRect(blockRect, section.color);
            Handles.DrawSolidRectangleWithOutline(blockRect, Color.clear, new Color(0, 0, 0, 0.5f));

            Rect playBtnRect = new Rect(blockRect.x + 2, blockRect.y + 2, 18, 16);
            if (GUI.Button(playBtnRect, "▶", EditorStyles.miniButton)) PlaySpecificSection(coding, section);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(blockRect.x + 22, blockRect.y + 2, blockRect.width - 24, 16), section.name, labelStyle);

            HandleInteractiveDrag(index, 0, blockRect, trackArea.width, timelineDuration);
        }

        private void DrawTransitionBlock(TransitionPoint point, int index, Rect trackArea, float timelineDuration, SoundCoding coding)
        {
            var section = coding.GetSection(point.sectionId);
            var transition = coding.GetTransition(point.targetTransitionId);
            
            if (section == null || transition == null) return;

            float absoluteTime = section.startTime + point.timeOffset;
            float normTime = absoluteTime / timelineDuration;
            float startX = trackArea.x + (normTime * trackArea.width);
            
            float fadeNorm = transition.fadeOutDuration / timelineDuration;
            float blockWidth = Mathf.Max(fadeNorm * trackArea.width, 4f);

            Rect blockRect = new Rect(startX, trackArea.y + 5f, blockWidth, trackArea.height - 10f);

            var fromSec = coding.GetSection(transition.fromSectionId);
            var toSec = coding.GetSection(transition.toSectionId);
            Color cFrom = fromSec != null ? fromSec.color : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Color cTo = toSec != null ? toSec.color : new Color(0.5f, 0.5f, 0.5f, 0.5f);

            cFrom.a = 0.8f; cTo.a = 0.8f;
            Texture2D gradTex = GetGradientTexture(cFrom, cTo);
            GUI.DrawTexture(blockRect, gradTex);
            Handles.DrawSolidRectangleWithOutline(blockRect, Color.clear, Color.white);

            Rect diamondRect = new Rect(startX - 8f, trackArea.y - 4f, 16f, 16f);
            Vector3[] pts = new Vector3[] {
                new Vector3(diamondRect.x + 8f, diamondRect.y),
                new Vector3(diamondRect.xMax, diamondRect.y + 8f),
                new Vector3(diamondRect.x + 8f, diamondRect.yMax),
                new Vector3(diamondRect.x, diamondRect.y + 8f)
            };
            Handles.color = Color.cyan;
            Handles.DrawAAConvexPolygon(pts);
            Handles.color = Color.white;

            Event e = Event.current;
            EditorGUIUtility.AddCursorRect(diamondRect, MouseCursor.Pan);
            
            if (e.type == EventType.MouseDown && e.button == 0 && diamondRect.Contains(e.mousePosition))
            {
                if (_isPlaying) 
                {
                    _queuedTransition = transition;
                    _queuedTransitionTriggerTime = section.startTime + point.timeOffset;
                }
                else StartDrag(index, 2, 0, e.mousePosition.x);
                e.Use();
            }

            HandleInteractiveDrag(coding.transitions.IndexOf(transition), 1, blockRect, trackArea.width, timelineDuration);
        }

        private void HandleInteractiveDrag(int index, int type, Rect blockRect, float trackWidth, float timelineDuration)
        {
            Event e = Event.current;
            Rect rightHandle = new Rect(blockRect.xMax - 6f, blockRect.y, 6f, blockRect.height);
            
            EditorGUIUtility.AddCursorRect(rightHandle, MouseCursor.ResizeHorizontal);
            
            if (type == 0) 
            {
                Rect leftHandle = new Rect(blockRect.x, blockRect.y, 6f, blockRect.height);
                Rect activeBody = new Rect(blockRect.x + 6f, blockRect.y, blockRect.width - 12f, blockRect.height);
                
                EditorGUIUtility.AddCursorRect(leftHandle, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(activeBody, MouseCursor.Pan);

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (leftHandle.Contains(e.mousePosition)) StartDrag(index, type, 1, e.mousePosition.x);
                    else if (rightHandle.Contains(e.mousePosition)) StartDrag(index, type, 2, e.mousePosition.x);
                    else if (activeBody.Contains(e.mousePosition)) StartDrag(index, type, 0, e.mousePosition.x);
                }
            }
            else if (type == 1 && e.type == EventType.MouseDown && e.button == 0 && rightHandle.Contains(e.mousePosition))
            {
                StartDrag(index, type, 2, e.mousePosition.x);
            }

            if (e.type == EventType.MouseDrag && _draggingIndex == index && _draggingType == type)
            {
                float timeDelta = ((e.mousePosition.x - _dragStartMouseX) / trackWidth) * timelineDuration;
                ApplyDrag(timeDelta);
                e.Use();
            }

            if (e.type == EventType.MouseUp && _draggingIndex == index && _draggingType == type)
            {
                _draggingIndex = -1;
                e.Use();
            }
        }

        private void StartDrag(int index, int type, int edge, float mouseX)
        {
            _draggingIndex = index;
            _draggingType = type;
            _draggingEdge = edge;
            _dragStartMouseX = mouseX;

            if (type == 0) 
            {
                var sectionProp = serializedObject.FindProperty("sections").GetArrayElementAtIndex(index);
                _dragStartValue = sectionProp.FindPropertyRelative("startTime").floatValue;
                _dragStartValue2 = sectionProp.FindPropertyRelative("duration").floatValue;
            }
            else if (type == 1) 
            {
                var transProp = serializedObject.FindProperty("transitions").GetArrayElementAtIndex(index);
                _dragStartValue = transProp.FindPropertyRelative("fadeOutDuration").floatValue;
            }
            else if (type == 2) 
            {
                var pointProp = serializedObject.FindProperty("transitionPoints").GetArrayElementAtIndex(index);
                _dragStartValue = pointProp.FindPropertyRelative("timeOffset").floatValue;
            }
        }

        private void ApplyDrag(float timeDelta)
        {
            if (_draggingType == 0)
            {
                var sectionProp = serializedObject.FindProperty("sections").GetArrayElementAtIndex(_draggingIndex);
                var startProp = sectionProp.FindPropertyRelative("startTime");
                var durProp = sectionProp.FindPropertyRelative("duration");

                if (_draggingEdge == 0) startProp.floatValue = Mathf.Max(0f, _dragStartValue + timeDelta);
                else if (_draggingEdge == 1)
                {
                    float newStart = Mathf.Max(0f, _dragStartValue + timeDelta);
                    startProp.floatValue = newStart;
                    durProp.floatValue = Mathf.Max(0.1f, _dragStartValue2 - (newStart - _dragStartValue));
                }
                else if (_draggingEdge == 2) durProp.floatValue = Mathf.Max(0.1f, _dragStartValue2 + timeDelta);
            }
            else if (_draggingType == 1)
            {
                var transProp = serializedObject.FindProperty("transitions").GetArrayElementAtIndex(_draggingIndex);
                var fadeProp = transProp.FindPropertyRelative("fadeOutDuration");
                fadeProp.floatValue = Mathf.Max(0.1f, _dragStartValue + timeDelta);
            }
            else if (_draggingType == 2)
            {
                var pointProp = serializedObject.FindProperty("transitionPoints").GetArrayElementAtIndex(_draggingIndex);
                var offsetProp = pointProp.FindPropertyRelative("timeOffset");
                offsetProp.floatValue = Mathf.Max(0f, _dragStartValue + timeDelta);
            }
        }
        #endregion

        #region Custom Lists & Dropdowns
        private void DrawLists(SoundCoding coding)
        {
            string[] secIds = coding.sections.Select(s => s.id).ToArray();
            string[] secNames = coding.sections.Select(s => s.name).ToArray();
            
            string[] transIds = coding.transitions.Select(t => t.id).ToArray();
            string[] transNames = coding.transitions.Select(t => t.name).ToArray();

            // Sections
            GUILayout.Label("Sections", EditorStyles.boldLabel);
            for (int i = 0; i < coding.sections.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var sec = coding.sections[i];
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("▶ Play", GUILayout.Width(50))) PlaySpecificSection(coding, sec);
                sec.name = EditorGUILayout.TextField(sec.name);
                sec.color = EditorGUILayout.ColorField(GUIContent.none, sec.color, false, true, false, GUILayout.Width(50));
                if (GUILayout.Button("X", GUILayout.Width(25))) { coding.sections.RemoveAt(i); break; }
                GUILayout.EndHorizontal();

                sec.startTime = EditorGUILayout.FloatField("Start Time", sec.startTime);
                sec.duration = EditorGUILayout.FloatField("Duration", sec.duration);
                
                sec.loopSection = EditorGUILayout.Toggle("Loop Section", sec.loopSection);
                if (!sec.loopSection)
                {
                    sec.isSongEnd = EditorGUILayout.Toggle("Is Song End", sec.isSongEnd);
                    if (!sec.isSongEnd)
                    {
                        sec.nextSectionId = DrawIdPopup("Next Section", sec.nextSectionId, secIds, secNames, true);
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Add Section"))
            {
                coding.sections.Add(new SoundCodingSection { 
                    color = new Color(Random.value, Random.value, Random.value, 0.6f) 
                });
            }

            GUILayout.Space(15);

            // Transitions
            GUILayout.Label("Transitions", EditorStyles.boldLabel);
            for (int i = 0; i < coding.transitions.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var trans = coding.transitions[i];
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⚡ Test", GUILayout.Width(50))) TestTransition(coding, trans);
                trans.name = EditorGUILayout.TextField(trans.name);
                if (GUILayout.Button("X", GUILayout.Width(25))) { coding.transitions.RemoveAt(i); break; }
                GUILayout.EndHorizontal();

                trans.fromSectionId = DrawIdPopup("From Section", trans.fromSectionId, secIds, secNames);
                trans.toSectionId = DrawIdPopup("To Section", trans.toSectionId, secIds, secNames);
                trans.bridgeSectionId = DrawIdPopup("Bridge Section (Opt)", trans.bridgeSectionId, secIds, secNames, true);

                trans.fadeOutDuration = EditorGUILayout.FloatField("Fade Out Dur", trans.fadeOutDuration);
                trans.fadeInDuration = EditorGUILayout.FloatField("Fade In Dur", trans.fadeInDuration);

                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Add Transition")) coding.transitions.Add(new SoundCodingTransition());

            GUILayout.Space(15);

            // Transition Points
            GUILayout.Label("Transition Points (Diamonds)", EditorStyles.boldLabel);
            for (int i = 0; i < coding.transitionPoints.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var tp = coding.transitionPoints[i];

                GUILayout.BeginHorizontal();
                tp.sectionId = DrawIdPopup("Host Section", tp.sectionId, secIds, secNames);
                if (GUILayout.Button("X", GUILayout.Width(25))) { coding.transitionPoints.RemoveAt(i); break; }
                GUILayout.EndHorizontal();

                tp.timeOffset = EditorGUILayout.FloatField("Time Offset", tp.timeOffset);
                tp.targetTransitionId = DrawIdPopup("Target Transition", tp.targetTransitionId, transIds, transNames);
                
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Add Transition Point")) coding.transitionPoints.Add(new TransitionPoint());
        }

        private string DrawIdPopup(string label, string currentId, string[] ids, string[] names, bool allowNone = false)
        {
            List<string> displayNames = new List<string>();
            List<string> actualIds = new List<string>();
            
            if (allowNone)
            {
                displayNames.Add("None");
                actualIds.Add("");
            }

            displayNames.AddRange(names);
            actualIds.AddRange(ids);

            int currentIndex = actualIds.IndexOf(currentId);
            if (currentIndex == -1) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(label, currentIndex, displayNames.ToArray());
            return actualIds.Count > 0 ? actualIds[newIndex] : "";
        }

        private Texture2D GetGradientTexture(Color from, Color to)
        {
            string key = from.ToString() + to.ToString();
            if (_gradientCache.TryGetValue(key, out var tex) && tex != null) return tex;

            tex = new Texture2D(2, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, from);
            tex.SetPixel(1, 0, to);
            tex.Apply();
            
            _gradientCache[key] = tex;
            return tex;
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
        #endregion
    }
}
#endif