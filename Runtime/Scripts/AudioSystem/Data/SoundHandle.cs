using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if DOTWEEN
using DG.Tweening;
#endif

namespace Abb2kTools.AudioSystem
{
    public class SoundHandle
    {
        public string ID                  { get; }
        public bool IsPersistent          { get; }
        public ExternalAudioSource Holder { get; }

        public bool IsPaused     { get; private set; }
        public bool IsStopped    { get; private set; } = true;
        public bool SequenceLoop { get; set; }

        private event Action OnCompleteEvent;

        private class SourceData
        {
            public AudioSource Source;
            public PlayableClipData ClipData;
            public float BaseVolume;
            public float BasePitch;
            public float DelayTimer;
            public float Duration;
            public float PlaybackTimer;
            public bool IsPlaying;
            public bool IsFinished;
        }

        private readonly List<SourceData> _sources = new();
        private Coroutine _playbackRoutine;
        
        private float _currentVolumeMultiplier = 1f;
        private float _currentPitchMultiplier = 1f;

#if DOTWEEN
        private readonly List<Tween> _activeTweens = new();
#endif

        public SoundHandle(string id, ExternalAudioSource holder, bool isPersistent)
        {
            ID = id;
            Holder = holder;
            IsPersistent = isPersistent;
        }

        public void AddSource(AudioSource source, PlayableClipData clipData, float volumeMultiplier, float pitchMultiplier)
        {
            float duration = (clipData.Clip.length - clipData.StartOffset - clipData.EndOffset) / Mathf.Max(0.001f, clipData.Pitch * pitchMultiplier);
            _sources.Add(new SourceData
            {
                Source = source,
                ClipData = clipData,
                BaseVolume = clipData.Volume * volumeMultiplier,
                BasePitch = clipData.Pitch * pitchMultiplier,
                Duration = duration
            });
        }

        public SoundHandle OnComplete(Action callback)
        {
            OnCompleteEvent += callback;
            return this; 
        }

        public SoundHandle SetVolume(float volumeMultiplier)
        {
            _currentVolumeMultiplier = volumeMultiplier;
            foreach (var s in _sources)
                if (s.Source != null) s.Source.volume = s.BaseVolume * _currentVolumeMultiplier;
            return this;
        }

        public SoundHandle SetPitch(float pitchMultiplier)
        {
            _currentPitchMultiplier = pitchMultiplier;
            foreach (var s in _sources)
                if (s.Source != null) s.Source.pitch = s.BasePitch * _currentPitchMultiplier;
            return this;
        }

#if DOTWEEN
        
        /// <summary>
        /// Fades the volume multiplier of this sound handle to a target value.
        /// </summary>
        public Tween DOFade(float endValue, float duration)
        {
            Tween t = DOTween.To(() => _currentVolumeMultiplier, x => SetVolume(x), endValue, duration);
            TrackTween(t);
            return t;
        }

        /// <summary>
        /// Tweens the pitch multiplier of this sound handle to a target value.
        /// </summary>
        public Tween DOPitch(float endValue, float duration)
        {
            Tween t = DOTween.To(() => _currentPitchMultiplier, x => SetPitch(x), endValue, duration);
            TrackTween(t);
            return t;
        }

        private void TrackTween(Tween t)
        {
            _activeTweens.Add(t);
            t.OnKill(() => _activeTweens.Remove(t));
        }

        private void KillAllActiveTweens()
        {
            foreach (var t in _activeTweens)
            {
                if (t != null && t.IsActive()) t.Kill();
            }
            _activeTweens.Clear();
        }

#endif

        public SoundHandle Play()
        {
            if (!IsStopped && !IsPaused) return this; 

            IsStopped = false;
            IsPaused = false;

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);

            _playbackRoutine = SoundManager.Instance.StartCoroutine(PlaybackRoutine());
            
            return this;
        }

        public void Pause()
        {
            if (IsStopped) return;

            IsPaused = true;

            foreach (var t in _activeTweens)
                if (t != null && t.IsActive()) t.Pause();

            foreach (var s in _sources)
                if (s.IsPlaying && s.Source != null) s.Source.Pause();
        }

        public void Resume()
        {
            if (IsStopped) return;

            IsPaused = false;

            foreach (var t in _activeTweens)
                if (t != null && t.IsActive()) t.Play();

            foreach (var s in _sources)
                if (s.IsPlaying && s.Source != null) s.Source.UnPause();
        }

        public void Stop()
        {
            if (IsStopped) return;

            IsStopped = true;
            KillAllActiveTweens();

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);

            foreach (var s in _sources)
                if (s.Source != null) s.Source.Stop();

            if (!IsPersistent) DestroyHandle();
        }

        public void DestroyHandle()
        {
            IsStopped = true;
            KillAllActiveTweens();

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);

            foreach (var s in _sources)
                if (s.Source != null && Holder != null)
                    Holder.DeleteSource(s.Source);

            _sources.Clear();

            if (IsPersistent && !string.IsNullOrEmpty(ID))
                SoundManager.Instance.RemovePersistentSound(ID);
        }

        private IEnumerator PlaybackRoutine()
        {
            foreach (var s in _sources)
            {
                s.DelayTimer    = 0f;
                s.PlaybackTimer = 0f;
                s.IsPlaying     = false;
                s.IsFinished    = false;
            }

            while (true)
            {
                if (IsStopped || Holder == null) yield break;

                if (!IsPaused)
                {
                    int finishedCount = 0;
                    foreach (var s in _sources)
                    {
                        if (s.Source == null) s.IsFinished = true;

                        if (s.IsFinished)
                        {
                            finishedCount++;
                            continue;
                        }

                        if (!s.IsPlaying)
                        {
                            if (s.DelayTimer >= s.ClipData.Delay)
                            {
                                s.Source.time = s.ClipData.StartOffset; 
                                s.Source.Play();
                                s.IsPlaying = true;
                            }
                            else
                            {
                                s.DelayTimer += Time.deltaTime;
                            }
                        }
                        
                        if (s.IsPlaying)
                        {
                            if (!s.Source.loop)
                            {
                                s.PlaybackTimer += Time.deltaTime;
                                if (s.PlaybackTimer >= s.Duration)
                                {
                                    s.Source.Stop();
                                    s.IsPlaying  = false;
                                    s.IsFinished = true;
                                    finishedCount++;
                                }
                            }
                        }
                    }

                    if (finishedCount >= _sources.Count)
                    {
                        if (SequenceLoop)
                        {
                            foreach (var s in _sources)
                            {
                                s.DelayTimer    = 0f;
                                s.PlaybackTimer = 0f;
                                s.IsPlaying     = false;
                                s.IsFinished    = false;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                yield return null;
            }

            IsStopped = true;
            OnCompleteEvent?.Invoke();
            OnCompleteEvent = null;

            if (!IsPersistent) DestroyHandle();
        }

        public static implicit operator bool(SoundHandle handle) => handle != null && !handle.IsStopped;
    }
}