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
        private static string _currentLoopingPreviewKey;

        // --- 3D Settings State ---
        private static bool _apply3D;
        private static float _spatialBlend;
        private static float _dopplerLevel;
        private static float _minDist;
        private static float _maxDist;
        private static AudioRolloffMode _rolloffMode;
        private static BindingPreviewContext _bindingPreviewContext;
        private static Transform _followTransform;

        public static bool IsPlaying => _previewGO != null && _pendingClips.Count > 0;
        public static bool IsPaused { get; private set; }
        public static SoundModificationBase CurrentTarget => _currentTarget;
        public static string CurrentLoopingPreviewKey => _currentLoopingPreviewKey;

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

        private static void ApplyBoundSettings(
            AudioSource source,
            Dictionary<SoundParameterTarget, float> boundValues,
            SoundBasePlaybackSettings baseSettings)
        {
            source.panStereo = GetBoundValue(boundValues, SoundParameterTarget.PanStereo, baseSettings.panStereo);
            source.spatialBlend = GetBoundValue(boundValues, SoundParameterTarget.SpatialBlend, baseSettings.spatialBlend);
            source.reverbZoneMix = GetBoundValue(boundValues, SoundParameterTarget.ReverbZoneMix, baseSettings.reverbZoneMix);
            source.priority = baseSettings.prio;
            source.dopplerLevel = GetBoundValue(boundValues, SoundParameterTarget.DopplerLevel, baseSettings.dopplerLevel);
            source.spread = GetBoundValue(boundValues, SoundParameterTarget.Spread, baseSettings.spread);
            source.minDistance = GetBoundValue(boundValues, SoundParameterTarget.MinDistance, baseSettings.minDist);
            source.maxDistance = GetBoundValue(boundValues, SoundParameterTarget.MaxDistance, baseSettings.maxDist);
        }

        private static float GetBoundValue(
            Dictionary<SoundParameterTarget, float> boundValues,
            SoundParameterTarget target,
            float defaultValue)
        {
            return boundValues != null && boundValues.TryGetValue(target, out float value)
                ? value
                : defaultValue;
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
                    if (clip.Looping)
                    {
                        max = Mathf.Max(max, clip.StopTime);
                        continue;
                    }

                    float pitch = Mathf.Max(0.001f, clip.Data.Pitch * clip.PitchMult);
                    float fullDur = clip.Data.Clip.length / pitch;
                    float trueStart = clip.Data.Delay - (clip.Data.StartOffset / pitch);
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
            public Dictionary<SoundParameterTarget, float> BoundValues;
            public IReadOnlyList<SoundParameterBinding> AdditionalBindings;
            public bool Played;
            public float VolMult;
            public float PitchMult;
            public float BaseVolMult;
            public float BasePitchMult;
            public AudioSource Source;
            public double StartPlayTime;
            public double Duration;
            public bool Looping;
            public float StopTime;
            public float FadeInDuration;
            public float FadeOutDuration;
            public float PhaseStartTime;
            public float PhaseEndTime;
            public bool ModulateVolume;
            public float MinVolumeMultiplier = 1f;
            public float StartVolumeMultiplier = 1f;
            public float MaxVolumeMultiplier = 1f;
            public SoundLoopModulationMode VolumeMode;
            public float VolumeGrowthRate;
            public float VolumeSineCycleDuration = 2f;
            public bool ModulatePitch;
            public float MinPitchMultiplier = 1f;
            public float StartPitchMultiplier = 1f;
            public float MaxPitchMultiplier = 1f;
            public SoundLoopModulationMode PitchMode;
            public float PitchGrowthRate;
            public float PitchSineCycleDuration = 2f;
        }

        private sealed class BindingPreviewContext
        {
            public SoundBasePlaybackSettings BaseSettings;
            public IReadOnlyList<SoundParameterBinding> SoundBaseBindings;
            public Dictionary<SoundParameterBinding, float> PreviewInputs;
            public float BaseVolumeMultiplier;
            public float BasePitchMultiplier;
        }

        public static void PlayPreview(
            SoundModificationBase soundBase,
            float volMult = 1f,
            float pitchMult = 1f,
            Transform sourceTransform = null,
            SerializedProperty soundProperty = null)
        {
            PlayPreviewInternal(soundBase, volMult, pitchMult, sourceTransform, soundProperty, null);
        }

        public static void PlayPreviewWithBindings(
            SoundModificationBase soundBase,
            float volMult,
            float pitchMult,
            Transform sourceTransform,
            SerializedProperty soundProperty,
            SoundBasePlaybackSettings baseSettings,
            IReadOnlyList<SoundParameterBinding> soundBaseBindings,
            IReadOnlyDictionary<SoundParameterBinding, float> previewInputs)
        {
            Dictionary<SoundParameterBinding, float> copiedPreviewInputs = new();
            if (previewInputs != null)
            {
                foreach (KeyValuePair<SoundParameterBinding, float> previewInput in previewInputs)
                    copiedPreviewInputs.Add(previewInput.Key, previewInput.Value);
            }

            PlayPreviewInternal(
                soundBase,
                volMult,
                pitchMult,
                sourceTransform,
                soundProperty,
                new BindingPreviewContext
                {
                    BaseSettings = baseSettings,
                    SoundBaseBindings = soundBaseBindings,
                    PreviewInputs = copiedPreviewInputs,
                    BaseVolumeMultiplier = volMult,
                    BasePitchMultiplier = pitchMult
                });
        }

        public static void UpdatePreviewBindingInput(SoundParameterBinding binding, float input)
        {
            if (!IsPlaying || _bindingPreviewContext == null || binding == null)
                return;

            _bindingPreviewContext.PreviewInputs[binding] = input;
            float currentTime = CurrentTime;

            foreach (PreviewClip clip in _pendingClips)
            {
                ApplyPreviewBindings(clip);

                if (clip.Source == null)
                    continue;

                ApplyBoundSettings(clip.Source, clip.BoundValues, _bindingPreviewContext.BaseSettings);
                AudioSourceGain.SetVolume(
                    clip.Source,
                    clip.Data.Volume *
                    clip.VolMult *
                    GetPhaseVolumeMultiplier(clip, currentTime) *
                    GetLoopVolumeMultiplier(clip, currentTime) *
                    GetFadeMultiplier(clip.Data, currentTime));
                clip.Source.pitch = clip.Data.Pitch *
                    clip.PitchMult *
                    GetLoopPitchMultiplier(clip, currentTime);

                if (!clip.Looping)
                {
                    float remainingDuration = Mathf.Max(
                        0f,
                        (clip.Data.Clip.length - clip.Data.EndOffset - clip.Source.time) /
                        Mathf.Max(0.001f, clip.Source.pitch));
                    clip.Duration = (float)(EditorApplication.timeSinceStartup - clip.StartPlayTime) + remainingDuration;
                    clip.Source.SetScheduledEndTime(AudioSettings.dspTime + remainingDuration);
                }
            }
        }

        private static void PlayPreviewInternal(
            SoundModificationBase soundBase,
            float volMult,
            float pitchMult,
            Transform sourceTransform,
            SerializedProperty soundProperty,
            BindingPreviewContext bindingPreviewContext)
        {
            StopPreview();
            if (soundBase == null) return;

            _currentLoopingPreviewKey = null;
            _currentTarget = soundBase;
            _bindingPreviewContext = bindingPreviewContext;
            
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview_Hidden", HideFlags.HideAndDontSave);

            // Capture 3D settings ONLY if shift was clicked and we found a transform
            if (sourceTransform != null && soundProperty != null)
            {
                _apply3D = true;
                _previewGO.transform.position = sourceTransform.position;
                _followTransform = sourceTransform;
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

            LoadClips(soundBase, volMult, pitchMult, bindingPreviewContext);

            if (_pendingClips.Count == 0) { StopPreview(); return; }

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        private static void AppendLoopingPhase(
            SoundModificationBase sound,
            float volumeMultiplier,
            float pitchMultiplier,
            float endTime,
            float fadeInDuration,
            float fadeOutDuration,
            bool modulateVolume,
            float minVolumeMultiplier,
            float startVolumeMultiplier,
            float maxVolumeMultiplier,
            SoundLoopModulationMode volumeMode,
            float volumeGrowthRate,
            float volumeSineCycleDuration,
            bool modulatePitch,
            float minPitchMultiplier,
            float startPitchMultiplier,
            float maxPitchMultiplier,
            SoundLoopModulationMode pitchMode,
            float pitchGrowthRate,
            float pitchSineCycleDuration,
            IReadOnlyList<SoundParameterBinding> soundBaseBindings)
        {
            if (sound == null || endTime <= 0f) return;

            List<PlayableClipData> clips = new();
            sound.CollectPlayableClips(clips);

            if (clips.Count == 1 &&
                clips[0].Clip != null &&
                clips[0].Delay <= 0f &&
                clips[0].StartOffset <= 0f &&
                clips[0].EndOffset <= 0f)
            {
                PreviewClip previewClip = new()
                {
                    Data = clips[0],
                    Played = false,
                    VolMult = volumeMultiplier,
                    PitchMult = pitchMultiplier,
                    BaseVolMult = volumeMultiplier,
                    BasePitchMult = pitchMultiplier,
                    Looping = true,
                    StopTime = endTime,
                    FadeInDuration = fadeInDuration,
                    FadeOutDuration = fadeOutDuration,
                    PhaseStartTime = 0f,
                    PhaseEndTime = endTime,
                    ModulateVolume = modulateVolume,
                    MinVolumeMultiplier = minVolumeMultiplier,
                    StartVolumeMultiplier = startVolumeMultiplier,
                    MaxVolumeMultiplier = maxVolumeMultiplier,
                    VolumeMode = volumeMode,
                    VolumeGrowthRate = volumeGrowthRate,
                    VolumeSineCycleDuration = volumeSineCycleDuration,
                    ModulatePitch = modulatePitch,
                    MinPitchMultiplier = minPitchMultiplier,
                    StartPitchMultiplier = startPitchMultiplier,
                    MaxPitchMultiplier = maxPitchMultiplier,
                    PitchMode = pitchMode,
                    PitchGrowthRate = pitchGrowthRate,
                    PitchSineCycleDuration = pitchSineCycleDuration,
                    AdditionalBindings = soundBaseBindings
                };
                ApplyPreviewBindings(previewClip);
                _pendingClips.Add(previewClip);
                return;
            }

            float loopingDuration = GetSequenceDuration(sound, pitchMultiplier);
            if (loopingDuration <= 0f) return;

            int firstClipIndex = _pendingClips.Count;
            for (float loopStart = 0f; loopStart < endTime; loopStart += loopingDuration)
                AppendPhase(
                    sound,
                    volumeMultiplier,
                    pitchMultiplier,
                    loopStart,
                    endTime,
                    0f,
                    endTime,
                    fadeInDuration,
                    fadeOutDuration,
                    soundBaseBindings);

            for (int i = firstClipIndex; i < _pendingClips.Count; i++)
            {
                PreviewClip clip = _pendingClips[i];
                clip.ModulateVolume = modulateVolume;
                clip.MinVolumeMultiplier = minVolumeMultiplier;
                clip.StartVolumeMultiplier = startVolumeMultiplier;
                clip.MaxVolumeMultiplier = maxVolumeMultiplier;
                clip.VolumeMode = volumeMode;
                clip.VolumeGrowthRate = volumeGrowthRate;
                clip.VolumeSineCycleDuration = volumeSineCycleDuration;
                clip.ModulatePitch = modulatePitch;
                clip.MinPitchMultiplier = minPitchMultiplier;
                clip.StartPitchMultiplier = startPitchMultiplier;
                clip.MaxPitchMultiplier = maxPitchMultiplier;
                clip.PitchMode = pitchMode;
                clip.PitchGrowthRate = pitchGrowthRate;
                clip.PitchSineCycleDuration = pitchSineCycleDuration;
            }
        }

        public static void PlayLoopingPreview(
            object target,
            SoundModificationBase startSound, float startVolume, float startPitch,
            SoundModificationBase loopingSound, float loopingVolume, float loopingPitch,
            SoundModificationBase endSound, float endVolume, float endPitch,
            float loopDuration, float enterTransitionDuration, float exitTransitionDuration,
            bool modulateLoopVolume,
            float minLoopVolumeMultiplier, float startLoopVolumeMultiplier, float maxLoopVolumeMultiplier,
            SoundLoopModulationMode loopVolumeMode,
            float loopVolumeGrowthRate, float loopVolumeSineCycleDuration,
            bool modulateLoopPitch,
            float minLoopPitchMultiplier, float startLoopPitchMultiplier, float maxLoopPitchMultiplier,
            SoundLoopModulationMode loopPitchMode,
            float loopPitchGrowthRate, float loopPitchSineCycleDuration,
            Transform sourceTransform = null, SerializedProperty soundProperty = null, string previewKey = null,
            SoundBasePlaybackSettings? bindingBaseSettings = null,
            IReadOnlyList<SoundParameterBinding> soundBaseBindings = null,
            IReadOnlyDictionary<SoundParameterBinding, float> previewInputs = null,
            IReadOnlyList<SoundParameterBinding> startSoundBindings = null,
            IReadOnlyList<SoundParameterBinding> endSoundBindings = null)
        {
            StopPreview();

            if (target == null) return;

            _currentTarget = null;
            _currentLoopingPreviewKey = previewKey;
            if (bindingBaseSettings.HasValue)
            {
                Dictionary<SoundParameterBinding, float> copiedPreviewInputs = new();
                if (previewInputs != null)
                {
                    foreach (KeyValuePair<SoundParameterBinding, float> previewInput in previewInputs)
                        copiedPreviewInputs.Add(previewInput.Key, previewInput.Value);
                }

                _bindingPreviewContext = new BindingPreviewContext
                {
                    BaseSettings = bindingBaseSettings.Value,
                    SoundBaseBindings = soundBaseBindings,
                    PreviewInputs = copiedPreviewInputs,
                    BaseVolumeMultiplier = 1f,
                    BasePitchMultiplier = 1f
                };
            }
            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview_Hidden", HideFlags.HideAndDontSave);

            if (sourceTransform != null && soundProperty != null)
            {
                _apply3D = true;
                _previewGO.transform.position = sourceTransform.position;
                _followTransform = sourceTransform;
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

            float endTime = Mathf.Max(0f, loopDuration);
            AppendPhase(startSound, startVolume, startPitch, 0f, endTime, soundBaseBindings: startSoundBindings);
            AppendLoopingPhase(
                loopingSound,
                loopingVolume,
                loopingPitch,
                endTime,
                enterTransitionDuration,
                exitTransitionDuration,
                modulateLoopVolume,
                minLoopVolumeMultiplier,
                startLoopVolumeMultiplier,
                maxLoopVolumeMultiplier,
                loopVolumeMode,
                loopVolumeGrowthRate,
                loopVolumeSineCycleDuration,
                modulateLoopPitch,
                minLoopPitchMultiplier,
                startLoopPitchMultiplier,
                maxLoopPitchMultiplier,
                loopPitchMode,
                loopPitchGrowthRate,
                loopPitchSineCycleDuration,
                soundBaseBindings);
            AppendPhase(
                endSound,
                endVolume,
                endPitch,
                endTime,
                float.PositiveInfinity,
                soundBaseBindings: endSoundBindings);

            if (_pendingClips.Count == 0)
            {
                StopPreview();
                return;
            }

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        private static float GetSequenceDuration(SoundModificationBase sound, float pitchMultiplier)
        {
            if (sound == null) return 0f;

            List<PlayableClipData> clips = new();
            sound.CollectPlayableClips(clips);

            float duration = 0f;
            foreach (var clip in clips)
            {
                if (clip.Clip == null) continue;

                float pitch = Mathf.Max(0.001f, clip.Pitch * pitchMultiplier);
                float clipDuration = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / pitch;
                duration = Mathf.Max(duration, clip.Delay + clipDuration);
            }

            return duration;
        }

        private static void AppendPhase(
            SoundModificationBase sound,
            float volumeMultiplier,
            float pitchMultiplier,
            float startTime,
            float endTime,
            float phaseStartTime = 0f,
            float phaseEndTime = 0f,
            float fadeInDuration = 0f,
            float fadeOutDuration = 0f,
            IReadOnlyList<SoundParameterBinding> soundBaseBindings = null)
        {
            if (sound == null) return;

            List<PlayableClipData> clips = new();
            sound.CollectPlayableClips(clips);

            foreach (var clipData in clips)
            {
                if (clipData.Clip == null) continue;

                PlayableClipData scheduledClip = clipData;
                scheduledClip.Delay += startTime;
                if (scheduledClip.FadeEnvelopes != null)
                {
                    scheduledClip.FadeEnvelopes = new List<SoundFadeEnvelope>(scheduledClip.FadeEnvelopes);
                    for (int i = 0; i < scheduledClip.FadeEnvelopes.Count; i++)
                    {
                        SoundFadeEnvelope envelope = scheduledClip.FadeEnvelopes[i];
                        envelope.StartTime += startTime;
                        envelope.EndTime += startTime;
                        scheduledClip.FadeEnvelopes[i] = envelope;
                    }
                }
                if (scheduledClip.Delay >= endTime) continue;

                if (!float.IsPositiveInfinity(endTime))
                {
                    float clipPitch = Mathf.Max(0.001f, scheduledClip.Pitch * pitchMultiplier);
                    float remainingDuration = endTime - scheduledClip.Delay;
                    float currentDuration = (scheduledClip.Clip.length - scheduledClip.StartOffset - scheduledClip.EndOffset) / clipPitch;
                    if (currentDuration > remainingDuration)
                    {
                        scheduledClip.EndOffset += (currentDuration - remainingDuration) * clipPitch;
                        scheduledClip.EndOffset = Mathf.Min(scheduledClip.EndOffset, scheduledClip.Clip.length - scheduledClip.StartOffset);
                    }
                }

                _pendingClips.Add(new PreviewClip
                {
                    Data = scheduledClip,
                    Played = false,
                    VolMult = volumeMultiplier,
                    PitchMult = pitchMultiplier,
                    BaseVolMult = volumeMultiplier,
                    BasePitchMult = pitchMultiplier,
                    AdditionalBindings = soundBaseBindings,
                    FadeInDuration = fadeInDuration,
                    FadeOutDuration = fadeOutDuration,
                    PhaseStartTime = phaseStartTime,
                    PhaseEndTime = phaseEndTime
                });
            }
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

        private static void LoadClips(
            SoundModificationBase soundBase,
            float volMult,
            float pitchMult,
            BindingPreviewContext bindingPreviewContext)
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
                        PreviewClip previewClip = new()
                        {
                            Data = clip,
                            Played = false,
                            VolMult = volMult,
                            PitchMult = pitchMult,
                            BaseVolMult = volMult,
                            BasePitchMult = pitchMult
                        };
                        if (bindingPreviewContext != null)
                            ApplyPreviewBindings(previewClip);
                        _pendingClips.Add(previewClip);
                    }
                }
            }
        }

        private static void ApplyPreviewBindings(PreviewClip clip)
        {
            if (_bindingPreviewContext == null)
                return;

            clip.BoundValues = new Dictionary<SoundParameterTarget, float>();
            AddPreviewBindingValues(
                _bindingPreviewContext.SoundBaseBindings,
                _bindingPreviewContext,
                clip.BoundValues);
            AddPreviewBindingValues(
                clip.AdditionalBindings,
                _bindingPreviewContext,
                clip.BoundValues);
            AddPreviewBindingValues(
                clip.Data.ParameterBindings,
                _bindingPreviewContext,
                clip.BoundValues);

            clip.VolMult = clip.BaseVolMult;
            if (clip.BoundValues.TryGetValue(SoundParameterTarget.VolumeMultiplier, out float boundVolume))
                clip.VolMult *= boundVolume;

            clip.PitchMult = clip.BasePitchMult;
            if (clip.BoundValues.TryGetValue(SoundParameterTarget.PitchMultiplier, out float boundPitch))
                clip.PitchMult *= boundPitch;
        }

        private static void AddPreviewBindingValues(
            IEnumerable<SoundParameterBinding> bindings,
            BindingPreviewContext context,
            Dictionary<SoundParameterTarget, float> targetValues)
        {
            if (bindings == null)
                return;

            foreach (SoundParameterBinding binding in bindings)
            {
                if (binding == null || binding.parameter == null)
                    continue;

                float input = context.PreviewInputs != null &&
                              context.PreviewInputs.TryGetValue(binding, out float previewInput)
                    ? previewInput
                    : binding.parameter.CurrentValue;
                float mappedValue = binding.Evaluate(input);
                if (targetValues.TryGetValue(binding.target, out float currentValue))
                    targetValues[binding.target] = currentValue * mappedValue;
                else
                    targetValues[binding.target] = mappedValue;
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
                    AudioSourceGain.SetVolume(pClip.Source, newClipData.Volume *
                        pClip.VolMult *
                        GetPhaseVolumeMultiplier(pClip, currentTime) *
                        GetLoopVolumeMultiplier(pClip, currentTime) *
                        GetFadeMultiplier(newClipData, (float)currentTime));
                    pClip.Source.pitch = newClipData.Pitch *
                        pClip.PitchMult *
                        GetLoopPitchMultiplier(pClip, currentTime);
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
                        clipGO.transform.SetParent(_previewGO.transform, false);
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        Apply3DSettings(pClip.Source);

                        pClip.Data.Filters?.ApplyTo(pClip.Source);
                        pClip.Source.clip = pClip.Data.Clip;
                        AudioSourceGain.SetVolume(pClip.Source, pClip.Data.Volume *
                            pClip.VolMult *
                            GetPhaseVolumeMultiplier(pClip, currentTime) *
                            GetLoopVolumeMultiplier(pClip, currentTime) *
                            GetFadeMultiplier(pClip.Data, (float)currentTime));
                        pClip.Source.pitch = pClip.Data.Pitch *
                            pClip.PitchMult *
                            GetLoopPitchMultiplier(pClip, currentTime);
                        pClip.Source.loop = pClip.Looping;
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
                        clipGO.transform.SetParent(_previewGO.transform, false);
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        Apply3DSettings(pClip.Source);

                        pClip.Data.Filters?.ApplyTo(pClip.Source);
                        pClip.Source.clip = pClip.Data.Clip;
                        AudioSourceGain.SetVolume(pClip.Source, pClip.Data.Volume *
                            pClip.VolMult *
                            GetPhaseVolumeMultiplier(pClip, targetTime) *
                            GetLoopVolumeMultiplier(pClip, targetTime) *
                            GetFadeMultiplier(pClip.Data, targetTime));
                        pClip.Source.pitch = pClip.Data.Pitch *
                            pClip.PitchMult *
                            GetLoopPitchMultiplier(pClip, targetTime);
                        pClip.Source.loop = pClip.Looping;
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
            _bindingPreviewContext = null;
            _followTransform = null;
            _currentLoopingPreviewKey = null;
            
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
            if (_previewGO == null) return;

            if (_followTransform != null)
                _previewGO.transform.position = _followTransform.position;
            else
                _followTransform = null;

            if (IsPaused) return;

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
                        clipGO.transform.SetParent(_previewGO.transform, false);
                        
                        pClip.Source = clipGO.AddComponent<AudioSource>();

                        Apply3DSettings(pClip.Source);
                        if (_bindingPreviewContext != null)
                            ApplyBoundSettings(pClip.Source, pClip.BoundValues, _bindingPreviewContext.BaseSettings);

                        pClip.Source.clip = pClip.Data.Clip;
                        AudioSourceGain.SetVolume(pClip.Source, pClip.Data.Volume *
                            pClip.VolMult *
                            GetPhaseVolumeMultiplier(pClip, elapsed) *
                            GetLoopVolumeMultiplier(pClip, elapsed) *
                            GetFadeMultiplier(pClip.Data, (float)elapsed));
                        pClip.Source.pitch = pClip.Data.Pitch *
                            pClip.PitchMult *
                            GetLoopPitchMultiplier(pClip, elapsed);
                        pClip.Source.loop = pClip.Looping;
                        if (pClip.Data.PreferredMixerGroup != null) pClip.Source.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;

                        pClip.Data.Filters?.ApplyTo(pClip.Source);

                        pClip.Source.time = pClip.Data.StartOffset;
                        pClip.Duration = pClip.Looping
                            ? Mathf.Max(0f, pClip.StopTime - pClip.Data.Delay)
                            : (pClip.Data.Clip.length - pClip.Data.StartOffset - pClip.Data.EndOffset) / Mathf.Max(0.001f, pClip.Source.pitch);
                        
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
                        AudioSourceGain.SetVolume(pClip.Source, pClip.Data.Volume *
                            pClip.VolMult *
                            GetPhaseVolumeMultiplier(pClip, elapsed) *
                            GetLoopVolumeMultiplier(pClip, elapsed) *
                            GetFadeMultiplier(pClip.Data, (float)elapsed));
                        pClip.Source.pitch = pClip.Data.Pitch *
                            pClip.PitchMult *
                            GetLoopPitchMultiplier(pClip, elapsed);
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

        private static float GetPhaseVolumeMultiplier(PreviewClip clip, double elapsed)
        {
            float fadeIn = clip.FadeInDuration > 0f
                ? Mathf.Clamp01(((float)elapsed - clip.PhaseStartTime) / clip.FadeInDuration)
                : 1f;
            float fadeOut = clip.FadeOutDuration > 0f
                ? Mathf.Clamp01((clip.PhaseEndTime - (float)elapsed) / clip.FadeOutDuration)
                : 1f;
            return Mathf.Min(fadeIn, fadeOut);
        }

        private static float GetLoopVolumeMultiplier(PreviewClip clip, double elapsed)
        {
            return clip.ModulateVolume
                ? GetLoopModulationValue(
                    (float)elapsed,
                    clip.MinVolumeMultiplier,
                    clip.StartVolumeMultiplier,
                    clip.MaxVolumeMultiplier,
                    clip.VolumeMode,
                    clip.VolumeGrowthRate,
                    clip.VolumeSineCycleDuration)
                : 1f;
        }

        private static float GetLoopPitchMultiplier(PreviewClip clip, double elapsed)
        {
            return clip.ModulatePitch
                ? GetLoopModulationValue(
                    (float)elapsed,
                    clip.MinPitchMultiplier,
                    clip.StartPitchMultiplier,
                    clip.MaxPitchMultiplier,
                    clip.PitchMode,
                    clip.PitchGrowthRate,
                    clip.PitchSineCycleDuration)
                : 1f;
        }

        private static float GetFadeMultiplier(PlayableClipData clipData, float elapsed)
        {
            if (clipData.FadeEnvelopes == null)
                return 1f;

            float multiplier = 1f;
            foreach (SoundFadeEnvelope envelope in clipData.FadeEnvelopes)
                multiplier *= envelope.Evaluate(elapsed);
            return multiplier;
        }

        private static float GetLoopModulationValue(
            float elapsed,
            float minValue,
            float startValue,
            float maxValue,
            SoundLoopModulationMode mode,
            float growthRate,
            float sineCycleDuration)
        {
            float lowerBound = Mathf.Min(minValue, maxValue);
            float upperBound = Mathf.Max(minValue, maxValue);
            float clampedStart = Mathf.Clamp(startValue, lowerBound, upperBound);
            if (mode == SoundLoopModulationMode.SineWave)
            {
                if (upperBound <= lowerBound)
                    return lowerBound;

                float startProgress = (clampedStart - lowerBound) / (upperBound - lowerBound);
                float startPhase = Mathf.Asin(startProgress * 2f - 1f);
                float phase = startPhase +
                    (Mathf.PI * 2f * (float)elapsed / Mathf.Max(0.01f, sineCycleDuration));
                float sineProgress = 0.5f + 0.5f * Mathf.Sin(phase);
                return Mathf.Lerp(lowerBound, upperBound, sineProgress);
            }

            return Mathf.Min(upperBound, clampedStart + elapsed * Mathf.Max(0f, growthRate));
        }
    }
}
#endif