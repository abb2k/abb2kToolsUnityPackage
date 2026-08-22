#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [InitializeOnLoad]
    public static class EditorAudioPreviewer
    {
        private static GameObject _previewGO;
        private static readonly List<PreviewClip> _pendingClips = new();
        
        private static double _startTime;
        private static double _pauseTime;
        private static SoundModificationBase _currentTarget;

        // --- 3D Settings State ---
        private static bool _apply3D;
        private static float _spatialBlend;
        private static float _dopplerLevel;
        private static float _minDist;
        private static float _maxDist;
        private static AudioRolloffMode _rolloffMode;

        public static bool IsPlaying => _previewGO != null && _pendingClips.Count > 0;
        public static bool IsPaused { get; private set; }
        public static SoundModificationBase CurrentTarget => _currentTarget;

        // 1. Hook into Editor state changes to prevent leaks
        static EditorAudioPreviewer()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                StopPreview();
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            StopPreview();
        }

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

        public static void PlayPreview(SoundModificationBase soundBase, float volMult = 1f, float pitchMult = 1f, Transform sourceTransform = null, SerializedProperty soundProperty = null)
        {
            StopPreview();
            if (soundBase == null) return;

            _currentTarget = soundBase;
            
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview_Hidden", HideFlags.HideAndDontSave);

            // Capture 3D settings ONLY if shift was clicked and we found a transform
            if (sourceTransform != null && soundProperty != null)
            {
                _apply3D = true;
                _previewGO.transform.position = sourceTransform.position;
                _spatialBlend = soundProperty.FindPropertyRelative("spatialBlend").floatValue;
                _dopplerLevel = soundProperty.FindPropertyRelative("dopplerLevel").floatValue;
                _minDist = soundProperty.FindPropertyRelative("minDist").floatValue;
                _maxDist = soundProperty.FindPropertyRelative("maxDist").floatValue;
                _rolloffMode = (AudioRolloffMode)soundProperty.FindPropertyRelative("rolloff").enumValueIndex;
            }
            else
            {
                _apply3D = false;
            }

            LoadClips(soundBase, volMult, pitchMult);

            if (_pendingClips.Count == 0) { StopPreview(); return; }

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        private static void Apply3DSettings(AudioSource source)
        {
            // Fully skips touching the spatial blend if Shift isn't held (exact same as old behavior)
            if (_apply3D)
            {
                source.spatialBlend = _spatialBlend;
                source.dopplerLevel = _dopplerLevel;
                source.minDistance = _minDist;
                source.maxDistance = _maxDist;
                source.rolloffMode = _rolloffMode;
            }
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

                        Apply3DSettings(pClip.Source);

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

                        Apply3DSettings(pClip.Source);

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
            _apply3D = false;
            
            if (_previewGO != null) 
            {
                GameObject.DestroyImmediate(_previewGO);
                _previewGO = null;
            }
            
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
                float tailDuration = 0f;
                if (pClip.Data.Filters != null)
                {
                    if (pClip.Data.Filters.enableEcho) tailDuration = (pClip.Data.Filters.echoDelay / 1000f) * 5f;
                    if (pClip.Data.Filters.enableReverb) tailDuration = Mathf.Max(tailDuration, 3f);
                }

                if (!pClip.Played)
                {
                    allClipsStarted = false;
                    if (elapsed >= pClip.Data.Delay)
                    {
                        GameObject clipGO = new GameObject("PreviewClip");
                        clipGO.transform.parent = _previewGO.transform;
                        
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        Apply3DSettings(pClip.Source);

                        pClip.Source.clip = pClip.Data.Clip;
                        pClip.Source.volume = pClip.Data.Volume * pClip.VolMult;
                        pClip.Source.pitch = pClip.Data.Pitch * pClip.PitchMult;
                        if (pClip.Data.PreferredMixerGroup != null) pClip.Source.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;

                        pClip.Data.Filters?.ApplyTo(pClip.Source);

                        pClip.Source.time = pClip.Data.StartOffset;
                        pClip.Duration = (pClip.Data.Clip.length - pClip.Data.StartOffset - pClip.Data.EndOffset) / Mathf.Max(0.001f, pClip.Source.pitch);
                        
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