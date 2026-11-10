using System.Collections;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    public enum SoundLoopModulationMode
    {
        GrowToMaximum,
        SineWave
    }

    [System.Serializable]
    public class SoundEffectLooping : SoundBase
    {
        [Header("Looping SFX")]
        public SoundEffectOneShot startSound;
        public SoundEffectOneShot endSound;

        [Header("Loop Transitions")]
        [Min(0f)]
        public float enterTransitionDuration = 0.15f;
        [Min(0f)]
        public float exitTransitionDuration = 0.15f;

        [Header("Loop Modulation")]
        [Tooltip("Modulate the loop volume over time.")]
        public bool modulateLoopVolume;
        [Min(0f)]
        public float minLoopVolumeMultiplier = 1f;
        [Min(0f)]
        public float startLoopVolumeMultiplier = 1f;
        [Min(0f)]
        public float maxLoopVolumeMultiplier = 1f;
        public SoundLoopModulationMode loopVolumeModulationMode;
        [Min(0f)]
        [Tooltip("Loop volume multiplier increase per second.")]
        public float loopVolumeGrowthRate = 0.2f;
        [Min(0.01f)]
        [Tooltip("Duration of one complete min-to-max-to-min sine cycle.")]
        public float loopVolumeSineCycleDuration = 2f;
        [Tooltip("Modulate the loop pitch over time.")]
        public bool modulateLoopPitch;
        [Min(0f)]
        public float minLoopPitchMultiplier = 1f;
        [Min(0f)]
        public float startLoopPitchMultiplier = 1f;
        [Min(0f)]
        public float maxLoopPitchMultiplier = 1f;
        public SoundLoopModulationMode loopPitchModulationMode;
        [Min(0f)]
        [Tooltip("Loop pitch multiplier increase per second.")]
        public float loopPitchGrowthRate = 0.2f;
        [Min(0.01f)]
        [Tooltip("Duration of one complete min-to-max-to-min sine cycle.")]
        public float loopPitchSineCycleDuration = 2f;

        private SoundHandle _startHandle;
        private SoundHandle _loopHandle;
        private SoundHandle _endHandle;
        private Coroutine _playForRoutine;
        private Coroutine _transitionRoutine;
        private Coroutine _modulationRoutine;
        private int _playbackVersion;
        private bool _isActive;
        private float _volumeMultiplier = 1f;
        private float _pitchMultiplier = 1f;
        private float _loopVolumeModulationMultiplier = 1f;
        private float _loopPitchModulationMultiplier = 1f;
        private float _transitionFactor = 1f;
        private Transform _attachedTransform;
        private AudioAttachmentType _attachType;

        public SoundHandle Start(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return Play(attachedTransform, attachType);
        }

        public SoundHandle Play(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            CancelPlayFor();
            StopPlaybackWithoutEnd();

            _attachedTransform = attachedTransform;
            _attachType = attachType;
            _volumeMultiplier = 1f;
            _pitchMultiplier = 1f;
            _loopVolumeModulationMultiplier = modulateLoopVolume
                ? GetModulationValue(
                    minLoopVolumeMultiplier,
                    startLoopVolumeMultiplier,
                    maxLoopVolumeMultiplier,
                    loopVolumeModulationMode,
                    loopVolumeGrowthRate,
                    loopVolumeSineCycleDuration,
                    0f)
                : 1f;
            _loopPitchModulationMultiplier = modulateLoopPitch
                ? GetModulationValue(
                    minLoopPitchMultiplier,
                    startLoopPitchMultiplier,
                    maxLoopPitchMultiplier,
                    loopPitchModulationMode,
                    loopPitchGrowthRate,
                    loopPitchSineCycleDuration,
                    0f)
                : 1f;
            _transitionFactor = enterTransitionDuration > 0f ? 0f : 1f;
            _isActive = true;
            int playbackVersion = ++_playbackVersion;

            _loopHandle = SoundManager.Instance.CreateSoundHandle(this, attachedTransform, attachType, true, false);
            if (_loopHandle == null)
            {
                _isActive = false;
                return null;
            }

            _loopHandle.SetLifecycleCallbacks(
                PauseStartSound,
                ResumeStartSound,
                OnLoopStopRequested,
                SetVolumeMultiplier,
                SetPitchMultiplier);
            _loopHandle.SetVolume(_volumeMultiplier).SetPitch(_pitchMultiplier);

            if (startSound == null)
            {
                StartLoop(playbackVersion);
                return _loopHandle;
            }

            _startHandle = SoundManager.Instance.CreateSoundHandle(startSound, attachedTransform, attachType, false, true);
            if (_startHandle == null)
            {
                StartLoop(playbackVersion);
                return _loopHandle;
            }

            ApplyCurrentModifiers(_startHandle);
            StartLoop(playbackVersion);

            return _loopHandle;
        }

        public SoundHandle PlayFor(float seconds, Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            SoundHandle handle = Play(attachedTransform, attachType);
            if (handle != null)
                _playForRoutine = SoundManager.Instance.StartCoroutine(StopAfter(seconds));
            return handle;
        }

        public void Stop()
        {
            if (!_isActive) return;

            _isActive = false;
            _playbackVersion++;
            CancelPlayFor();
            CancelTransition();
            CancelModulation();
            _startHandle?.Stop();
            _startHandle = null;

            if (endSound != null)
            {
                _endHandle = SoundManager.Instance.CreateSoundHandle(endSound, _attachedTransform, _attachType, false, true);
                ApplyCurrentModifiers(_endHandle);
            }

            if (_loopHandle == null || _loopHandle.IsStopped || exitTransitionDuration <= 0f)
            {
                StopLoopImmediately();
                return;
            }

            _transitionRoutine = SoundManager.Instance.StartCoroutine(FadeLoopTo(0f, exitTransitionDuration, true));
        }

        private void StartLoop(int playbackVersion)
        {
            if (!_isActive || playbackVersion != _playbackVersion || _loopHandle == null) return;

            bool wasPaused = _loopHandle.IsPaused;
            _loopHandle.Play();
            if (wasPaused)
                _loopHandle.Pause();

            if (enterTransitionDuration > 0f)
                _transitionRoutine = SoundManager.Instance.StartCoroutine(FadeLoopTo(1f, enterTransitionDuration, false));

            if (modulateLoopVolume || modulateLoopPitch)
                _modulationRoutine = SoundManager.Instance.StartCoroutine(ModulateLoop());
        }

        private bool OnLoopStopRequested()
        {
            if (!_isActive) return false;

            Stop();
            return true;
        }

        private IEnumerator FadeLoopTo(float targetFactor, float duration, bool stopAfter)
        {
            float startFactor = _transitionFactor;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_loopHandle == null) yield break;
                if (_loopHandle.IsPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                _transitionFactor = Mathf.Lerp(startFactor, targetFactor, Mathf.Clamp01(elapsed / duration));
                ApplyLoopVolume();
                yield return null;
            }

            _transitionFactor = targetFactor;
            ApplyLoopVolume();
            _transitionRoutine = null;

            if (stopAfter)
                StopLoopImmediately();
        }

        private IEnumerator StopAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, seconds));
            _playForRoutine = null;
            Stop();
        }

        private IEnumerator ModulateLoop()
        {
            float elapsed = 0f;
            while (_isActive && _loopHandle != null && !_loopHandle.IsStopped)
            {
                if (!_loopHandle.IsPaused)
                {
                    _loopVolumeModulationMultiplier = modulateLoopVolume
                        ? GetModulationValue(
                            minLoopVolumeMultiplier,
                            startLoopVolumeMultiplier,
                            maxLoopVolumeMultiplier,
                            loopVolumeModulationMode,
                            loopVolumeGrowthRate,
                            loopVolumeSineCycleDuration,
                            elapsed)
                        : 1f;
                    _loopPitchModulationMultiplier = modulateLoopPitch
                        ? GetModulationValue(
                            minLoopPitchMultiplier,
                            startLoopPitchMultiplier,
                            maxLoopPitchMultiplier,
                            loopPitchModulationMode,
                            loopPitchGrowthRate,
                            loopPitchSineCycleDuration,
                            elapsed)
                        : 1f;
                    ApplyLoopModifiers();
                    elapsed += Time.deltaTime;
                }

                yield return null;
            }

            _modulationRoutine = null;
        }

        private static float GetModulationValue(
            float minValue,
            float startValue,
            float maxValue,
            SoundLoopModulationMode mode,
            float growthRate,
            float sineCycleDuration,
            float elapsed)
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
                float phase = startPhase + (Mathf.PI * 2f * elapsed / Mathf.Max(0.01f, sineCycleDuration));
                float sineProgress = 0.5f + 0.5f * Mathf.Sin(phase);
                return Mathf.Lerp(lowerBound, upperBound, sineProgress);
            }

            return Mathf.Min(upperBound, clampedStart + elapsed * Mathf.Max(0f, growthRate));
        }

        private void PauseStartSound()
        {
            _startHandle?.Pause();
        }

        private void ResumeStartSound()
        {
            _startHandle?.Resume();
        }

        private void SetVolumeMultiplier(float multiplier)
        {
            _volumeMultiplier = multiplier;
            _startHandle?.SetVolume(multiplier);
            _endHandle?.SetVolume(multiplier);
            ApplyLoopModifiers();
        }

        private void SetPitchMultiplier(float multiplier)
        {
            _pitchMultiplier = multiplier;
            _startHandle?.SetPitch(multiplier);
            _endHandle?.SetPitch(multiplier);
            ApplyLoopModifiers();
        }

        private void ApplyCurrentModifiers(SoundHandle handle)
        {
            if (handle == null) return;

            handle.SetVolume(_volumeMultiplier).SetPitch(_pitchMultiplier);
        }

        private void ApplyLoopVolume()
        {
            _loopHandle?.SetVolumeLocal(
                _volumeMultiplier * _transitionFactor * _loopVolumeModulationMultiplier);
        }

        private void ApplyLoopModifiers()
        {
            ApplyLoopVolume();
            _loopHandle?.SetPitchLocal(_pitchMultiplier * _loopPitchModulationMultiplier);
        }

        private void CancelPlayFor()
        {
            if (_playForRoutine == null) return;

            SoundManager.Instance.StopCoroutine(_playForRoutine);
            _playForRoutine = null;
        }

        private void StopPlaybackWithoutEnd()
        {
            _isActive = false;
            CancelTransition();
            CancelModulation();
            _startHandle?.Stop();
            _loopHandle?.StopImmediately();
            _endHandle?.Stop();
            _startHandle = null;
            _loopHandle = null;
            _endHandle = null;
        }

        private void StopLoopImmediately()
        {
            _loopHandle?.StopImmediately();
            _loopHandle = null;
            _transitionRoutine = null;
        }

        private void CancelTransition()
        {
            if (_transitionRoutine == null) return;

            SoundManager.Instance.StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        private void CancelModulation()
        {
            if (_modulationRoutine == null) return;

            SoundManager.Instance.StopCoroutine(_modulationRoutine);
            _modulationRoutine = null;
        }
    }
}
