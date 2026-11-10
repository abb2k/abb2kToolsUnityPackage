using System.Collections;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
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

        private SoundHandle _startHandle;
        private SoundHandle _loopHandle;
        private SoundHandle _endHandle;
        private Coroutine _playForRoutine;
        private Coroutine _transitionRoutine;
        private int _playbackVersion;
        private bool _isActive;
        private float _volumeMultiplier = 1f;
        private float _pitchMultiplier = 1f;
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
            ApplyLoopVolume();
        }

        private void SetPitchMultiplier(float multiplier)
        {
            _pitchMultiplier = multiplier;
            _startHandle?.SetPitch(multiplier);
            _endHandle?.SetPitch(multiplier);
        }

        private void ApplyCurrentModifiers(SoundHandle handle)
        {
            if (handle == null) return;

            handle.SetVolume(_volumeMultiplier).SetPitch(_pitchMultiplier);
        }

        private void ApplyLoopVolume()
        {
            _loopHandle?.SetVolumeLocal(_volumeMultiplier * _transitionFactor);
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
    }
}
