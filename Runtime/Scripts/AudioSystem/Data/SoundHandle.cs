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

        public SoundCoding CodingData { get; private set; }
        public SoundCodingSection CurrentSection { get; private set; }
        
        public event Action<SoundCodingSection> OnSectionChanged;
        public event Action<SoundCodingTransition> OnTransitionStarted;

        private AudioSource _secondaryCodingSource;
        private Coroutine _codingTransitionRoutine;
        private bool _isTransitioning;

        // Queuing State
        private SoundCodingTransition _queuedTransition;
        private float _queuedTransitionTriggerTime = -1f;
        private float _lastPlaybackTime = -1f;

        public event Action OnLoopRestart;

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

        public void InitializeCoding(SoundCoding coding)
        {
            CodingData = coding;
            
            foreach (var s in _sources)
            {
                if (s.Source == null) continue;

                coding.Filters?.ApplyTo(s.Source);
                
                s.Source.loop = false; 
            }
        }

        public void PlaySection(string sectionId)
        {
            if (CodingData == null || _sources.Count == 0) return;
            
            var sec = CodingData.GetSection(sectionId);
            if (sec == null) return;

            CancelQueuedTransition();
            if (_codingTransitionRoutine != null) SoundManager.Instance.StopCoroutine(_codingTransitionRoutine);
            _isTransitioning = false;

            CurrentSection = sec;
            
            foreach (var s in _sources)
            {
                if (s.Source == null) continue;

                float clipTargetTime = sec.startTime + s.ClipData.Delay + s.ClipData.StartOffset;
                s.Source.time = Mathf.Clamp(clipTargetTime, 0f, s.Source.clip.length - 0.01f);
                
                if (IsStopped || !s.IsPlaying)
                {
                    s.Source.Play();
                    s.IsPlaying = true;
                }
            }

            IsStopped = false;
            IsPaused = false;
            _lastPlaybackTime = sec.startTime;

            OnSectionChanged?.Invoke(CurrentSection);
        }

        public void CancelQueuedTransition()
        {
            _queuedTransition = null;
            _queuedTransitionTriggerTime = -1f;
        }

        public bool TriggerTransition(string transitionIdOrName)
        {
            if (CodingData == null) return false;

            var trans = CodingData.transitions.Find(t => t.id == transitionIdOrName || t.name == transitionIdOrName);
            if (trans == null) return false;

            if (CurrentSection != null && _sources.Count > 0)
            {
                var s = _sources[0];
                var validPoints = CodingData.transitionPoints.FindAll(p => 
                    p.sectionId == CurrentSection.id && 
                    (p.targetTransitionId == trans.id || string.IsNullOrEmpty(p.targetTransitionId))
                );

                if (validPoints.Count > 0)
                {
                    float currentOffset = s.Source.time - CurrentSection.startTime;
                    validPoints.Sort((a, b) => a.timeOffset.CompareTo(b.timeOffset));
                    
                    TransitionPoint nextPoint = null;
                    foreach (var pt in validPoints)
                    {
                        if (pt.timeOffset > currentOffset + 0.05f) 
                        {
                            nextPoint = pt;
                            break;
                        }
                    }

                    if (nextPoint == null) nextPoint = validPoints[0];

                    _queuedTransition = trans;
                    _queuedTransitionTriggerTime = CurrentSection.startTime + nextPoint.timeOffset;
                    return true;
                }
            }

            StartActualTransition(trans);
            return true;
        }

        private void StartActualTransition(SoundCodingTransition trans)
        {
            CancelQueuedTransition();
            if (_codingTransitionRoutine != null) SoundManager.Instance.StopCoroutine(_codingTransitionRoutine);
            _codingTransitionRoutine = SoundManager.Instance.StartCoroutine(TransitionRoutine(trans));
        }

        public void SeekToSectionOffset(float timeOffset)
        {
            if (CurrentSection == null || _sources.Count == 0 || _sources[0].Source == null) return;

            var s = _sources[0].Source;
            float targetTime = Mathf.Clamp(CurrentSection.startTime + timeOffset, CurrentSection.startTime, CurrentSection.startTime + CurrentSection.duration);
            s.time = Mathf.Clamp(targetTime, 0f, s.clip.length - 0.01f);
            _lastPlaybackTime = s.time;
        }

        private IEnumerator TransitionRoutine(SoundCodingTransition transition)
        {
            _isTransitioning = true;
            OnTransitionStarted?.Invoke(transition);

            bool hasBridge = !string.IsNullOrEmpty(transition.bridgeSectionId);
            var targetSection = CodingData.GetSection(hasBridge ? transition.bridgeSectionId : transition.toSectionId);

            if (targetSection == null) yield break;

            yield return PerformCrossfade(targetSection, transition.fadeOutDuration, transition.fadeInDuration);

            if (hasBridge)
            {
                var finalSection = CodingData.GetSection(transition.toSectionId);
                if (finalSection != null)
                {
                    float bridgeDuration = targetSection.duration;
                    yield return new WaitForSeconds(Mathf.Max(0f, bridgeDuration - transition.fadeInDuration));
                    yield return PerformCrossfade(finalSection, transition.fadeOutDuration, transition.fadeInDuration);
                }
            }
            
            _isTransitioning = false;
        }

        private IEnumerator PerformCrossfade(SoundCodingSection targetSection, float fadeOutDur, float fadeInDur)
        {
            var primary = _sources[0];
            
            _secondaryCodingSource.clip = primary.ClipData.Clip;
            _secondaryCodingSource.time = Mathf.Clamp(targetSection.startTime, 0f, _secondaryCodingSource.clip.length - 0.01f);
            
            if (!IsPaused) _secondaryCodingSource.Play();

            float timer = 0f;
            float startVol = primary.Source.volume;
            float targetVol = primary.BaseVolume * _currentVolumeMultiplier;
            
            float maxDur = Mathf.Max(fadeOutDur, fadeInDur);

            while (timer < maxDur)
            {
                timer += Time.deltaTime;
                float tOut = Mathf.Clamp01(timer / Mathf.Max(0.001f, fadeOutDur));
                float tIn = Mathf.Clamp01(timer / Mathf.Max(0.001f, fadeInDur));

                primary.Source.volume = startVol * Mathf.Cos(tOut * Mathf.PI / 2f);
                _secondaryCodingSource.volume = targetVol * Mathf.Sin(tIn * Mathf.PI / 2f);

                yield return null;
            }

            primary.Source.volume = 0f;
            primary.Source.Stop();
            
            _secondaryCodingSource.volume = targetVol;

            var temp = primary.Source;
            primary.Source = _secondaryCodingSource;
            _secondaryCodingSource = temp;

            CurrentSection = targetSection;
            _lastPlaybackTime = primary.Source.time;
            
            OnSectionChanged?.Invoke(CurrentSection);
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
                
            if (_secondaryCodingSource != null && _sources.Count > 0)
                _secondaryCodingSource.pitch = _sources[0].BasePitch * _currentPitchMultiplier;

            return this;
        }

#if DOTWEEN
        public Tween DOFade(float endValue, float duration)
        {
            Tween t = DOTween.To(() => _currentVolumeMultiplier, x => SetVolume(x), endValue, duration);
            TrackTween(t);
            return t;
        }

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
            _lastPlaybackTime = -1f;

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);
            _playbackRoutine = SoundManager.Instance.StartCoroutine(PlaybackRoutine());
            
            if (CodingData != null && CurrentSection == null && CodingData.sections.Count > 0)
            {
                string targetSectionId = CodingData.defaultSectionId;
                if (string.IsNullOrEmpty(targetSectionId) || CodingData.GetSection(targetSectionId) == null)
                {
                    targetSectionId = CodingData.sections[0].id;
                }

                PlaySection(targetSectionId);
            }

            return this;
        }

        public void Pause()
        {
            if (IsStopped) return;
            IsPaused = true;

#if DOTWEEN
            foreach (var t in _activeTweens)
                if (t != null && t.IsActive()) t.Pause();
#endif

            foreach (var s in _sources)
                if (s.IsPlaying && s.Source != null) s.Source.Pause();
                
            if (_secondaryCodingSource != null && _secondaryCodingSource.isPlaying)
                _secondaryCodingSource.Pause();
        }

        public void Resume()
        {
            if (IsStopped) return;
            IsPaused = false;

#if DOTWEEN
            foreach (var t in _activeTweens)
                if (t != null && t.IsActive()) t.Play();
#endif

            foreach (var s in _sources)
                if (s.IsPlaying && s.Source != null) s.Source.UnPause();
                
            if (_secondaryCodingSource != null && _isTransitioning)
                _secondaryCodingSource.UnPause();
        }

        public void Stop()
        {
            if (IsStopped) return;

            IsStopped = true;
            CancelQueuedTransition();
            
#if DOTWEEN
            KillAllActiveTweens();
#endif

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);
            if (_codingTransitionRoutine != null) SoundManager.Instance.StopCoroutine(_codingTransitionRoutine);

            foreach (var s in _sources)
                if (s.Source != null) s.Source.Stop();
                
            if (_secondaryCodingSource != null)
                _secondaryCodingSource.Stop();

            if (!IsPersistent) DestroyHandle();
        }

        public void DestroyHandle()
        {
            IsStopped = true;
            CancelQueuedTransition();
#if DOTWEEN
            KillAllActiveTweens();
#endif

            if (_playbackRoutine != null) SoundManager.Instance.StopCoroutine(_playbackRoutine);
            if (_codingTransitionRoutine != null) SoundManager.Instance.StopCoroutine(_codingTransitionRoutine);

            foreach (var s in _sources)
                if (s.Source != null && Holder != null)
                    Holder.DeleteSource(s.Source);

            if (_secondaryCodingSource != null && Holder != null)
                Holder.DeleteSource(_secondaryCodingSource);

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
                    if (CodingData != null)
                    {
                        if (CurrentSection != null && _sources.Count > 0)
                        {
                            var s = _sources[0];
                            if (s.Source != null && s.IsPlaying)
                            {
                                float currentTime = s.Source.time;
                                float maxTime = CurrentSection.startTime + CurrentSection.duration;

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
                                        StartActualTransition(_queuedTransition);
                                    }
                                }

                                if (currentTime >= maxTime && !_isTransitioning)
                                {
                                    if (CurrentSection.loopSection)
                                    {
                                        foreach (var srcData in _sources)
                                        {
                                            if (srcData.Source != null)
                                            {
                                                float clipTargetTime = CurrentSection.startTime + srcData.ClipData.Delay + srcData.ClipData.StartOffset;
                                                srcData.Source.time = Mathf.Clamp(clipTargetTime, 0f, srcData.Source.clip.length - 0.01f);
                                            }
                                        }
                                        currentTime = CurrentSection.startTime;
                                    }
                                    else
                                    {
                                        if (CurrentSection.isSongEnd)
                                        {
                                            Stop();
                                            yield break;
                                        }
                                        else if (!string.IsNullOrEmpty(CurrentSection.nextSectionId))
                                        {
                                            PlaySection(CurrentSection.nextSectionId);
                                            currentTime = _sources[0].Source.time;
                                        }
                                        else
                                        {
                                            Stop();
                                            yield break;
                                        }
                                    }
                                }

                                _lastPlaybackTime = currentTime;
                            }
                        }
                    }
                    else
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
                                OnLoopRestart?.Invoke();

                                foreach (var s in _sources)
                                {
                                    s.DelayTimer    = 0f;
                                    s.PlaybackTimer = 0f;
                                    s.IsPlaying     = false;
                                    s.IsFinished    = false;
                                }
                            }
                            else break;
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

        public void UpdateRandomModifiers(float volumeMultiplier, float pitchMultiplier)
        {
            foreach (var s in _sources)
            {
                s.BaseVolume = s.ClipData.Volume * volumeMultiplier;
                s.BasePitch = s.ClipData.Pitch * pitchMultiplier;
                
                s.Duration = (s.ClipData.Clip.length - s.ClipData.StartOffset - s.ClipData.EndOffset) / Mathf.Max(0.001f, s.BasePitch);

                if (s.Source != null)
                {
                    s.Source.volume = s.BaseVolume * _currentVolumeMultiplier;
                    s.Source.pitch = s.BasePitch * _currentPitchMultiplier;
                }
            }
        }
    }
}