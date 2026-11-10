using System.Collections.Generic;
using Abb2kTools.Singletons;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/SoundManager.png")]
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        private readonly Dictionary<string, SoundHandle> _persistentSounds = new();
        private readonly Dictionary<Transform, ExternalAudioSource> _externalHolders = new();
        private readonly HashSet<SoundHandle> _parameterizedHandles = new();
        private readonly HashSet<SoundHandle> _playingParameterizedHandles = new();
        private readonly Dictionary<SoundParameter, int> _activeSoundParameterCounts = new();

        private AudioListener _mainListener;
        public AudioListener MainListener
        {
            get
            {
                if (!_mainListener)
                    _mainListener = FindAnyObjectByType<AudioListener>();
                return _mainListener;
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            foreach (SoundHandle handle in _parameterizedHandles)
            {
                handle.UpdateParameterSources(deltaTime);
                handle.UpdateParameterTransitions(deltaTime);
            }
        }

        public SoundHandle PlaySFX(SoundEffect sfxSettings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return CreateSoundHandle(sfxSettings, attached, attachType, sfxSettings != null && sfxSettings.loop, true);
        }

        internal SoundHandle CreateSoundHandle(SoundBase settings, Transform attached, AudioAttachmentType attachType, bool loop, bool startImmediately)
        {
            if (settings == null || settings.sound == null) return null;

            List<PlayableClipData> clipsToPlay = new();
            settings.sound.CollectPlayableClips(clipsToPlay);
            if (clipsToPlay.Count == 0) return null;

            SoundBasePlaybackSettings playbackSettings = settings.SamplePlaybackSettings();
            bool useNativeLoop = loop &&
                settings is SoundEffectLooping &&
                settings.sound is not SoundCoding &&
                clipsToPlay.Count == 1 &&
                clipsToPlay[0].Delay <= 0f &&
                clipsToPlay[0].StartOffset <= 0f &&
                clipsToPlay[0].EndOffset <= 0f;

            ExternalAudioSource holder = GetOrCreateHolder(attached, attachType);
            SoundHandle handle = new(null, holder, isPersistent: false, scaleWithTimeScale: settings.scaleWithTimeScale)
            {
                SequenceLoop = loop && !useNativeLoop
            };
            handle.SetParameterSourceTransform(attached != null ? attached : transform);

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                settings.ApplyBaseSettings(source, clipData, useNativeLoop, 1f, 1f, playbackSettings);
                handle.AddSource(source, clipData, playbackSettings.volume, playbackSettings.pitch, settings.parameterBindings);
            }

            RegisterParameterizedHandle(handle);

            if (settings.sound is SoundCoding coding)
                handle.InitializeCoding(coding);

            if (startImmediately)
                handle.Play();
            else
                handle.PrepareForPlayback();

            return handle;
        }

        public SoundHandle GetOrCreatePersistentSound(Sound soundSettings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            if (soundSettings == null || soundSettings.sound == null) return null;

            if (_persistentSounds.TryGetValue(soundSettings.soundID, out var existingHandle))
                return existingHandle;

            List<PlayableClipData> clipsToPlay = new();
            soundSettings.sound.CollectPlayableClips(clipsToPlay);
            if (clipsToPlay.Count == 0) return null;

            SoundBasePlaybackSettings playbackSettings = soundSettings.SamplePlaybackSettings();
            bool hasOffsets = clipsToPlay.Count > 0 && (clipsToPlay[0].StartOffset > 0f || clipsToPlay[0].EndOffset > 0f);
            
            bool useNativeLoop = soundSettings.loop && clipsToPlay.Count == 1 && clipsToPlay[0].Delay <= 0f && !hasOffsets;
            bool useSequenceLoop = soundSettings.loop && !useNativeLoop;

            ExternalAudioSource holder = GetOrCreateHolder(attached, attachType);
            
            SoundHandle handle = new(soundSettings.soundID, holder, isPersistent: true, scaleWithTimeScale: soundSettings.scaleWithTimeScale)
            {
                SequenceLoop = useSequenceLoop
            };
            handle.SetParameterSourceTransform(attached != null ? attached : transform);

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                soundSettings.ApplyBaseSettings(source, clipData, useNativeLoop, 1f, 1f, playbackSettings);
                handle.AddSource(source, clipData, playbackSettings.volume, playbackSettings.pitch, soundSettings.parameterBindings);
            }

            RegisterParameterizedHandle(handle);

            if (soundSettings.sound is SoundCoding coding)
                handle.InitializeCoding(coding);

            if (!string.IsNullOrEmpty(soundSettings.soundID))
                _persistentSounds[soundSettings.soundID] = handle;

            return handle;
        }

        public SoundHandle GetPersistentSound(string soundID)
        {
            return _persistentSounds.TryGetValue(soundID, out var handle) ? handle : null;
        }

        public void RemovePersistentSound(string soundID)
        {
            if (_persistentSounds.ContainsKey(soundID))
            {
                _persistentSounds.Remove(soundID);
            }
        }

        internal void NotifyParameterChanged(SoundParameter parameter)
        {
            if (parameter == null) return;

            foreach (SoundHandle handle in _parameterizedHandles)
                if (handle.UsesParameter(parameter))
                    handle.RefreshParameterValues(true);
        }

        private void RegisterParameterizedHandle(SoundHandle handle)
        {
            if (!handle.HasParameterBindings) return;

            _parameterizedHandles.Add(handle);
            handle.SetParameterLifecycleCallbacks(
                OnParameterizedHandleStarted,
                OnParameterizedHandleStopped,
                OnParameterizedHandleDestroyed);

            foreach (SoundParameter parameter in handle.GetActiveCountParameters())
            {
                if (_activeSoundParameterCounts.ContainsKey(parameter)) continue;
                _activeSoundParameterCounts.Add(parameter, 0);
                parameter.SetActiveSoundCount(0);
            }

            handle.RefreshParameterValues();
        }

        private void OnParameterizedHandleStarted(SoundHandle handle)
        {
            if (!_playingParameterizedHandles.Add(handle)) return;

            foreach (SoundParameter parameter in handle.GetActiveCountParameters())
            {
                _activeSoundParameterCounts.TryGetValue(parameter, out int currentCount);
                currentCount++;
                _activeSoundParameterCounts[parameter] = currentCount;
                parameter.SetActiveSoundCount(currentCount);
            }

            RefreshParameterizedHandles(true);
        }

        private void OnParameterizedHandleStopped(SoundHandle handle)
        {
            if (!_playingParameterizedHandles.Remove(handle)) return;

            foreach (SoundParameter parameter in handle.GetActiveCountParameters())
            {
                _activeSoundParameterCounts.TryGetValue(parameter, out int currentCount);
                currentCount = Mathf.Max(0, currentCount - 1);
                _activeSoundParameterCounts[parameter] = currentCount;
                parameter.SetActiveSoundCount(currentCount);
            }

            RefreshParameterizedHandles(true);
        }

        private void OnParameterizedHandleDestroyed(SoundHandle handle)
        {
            OnParameterizedHandleStopped(handle);
            _parameterizedHandles.Remove(handle);
        }

        private void RefreshParameterizedHandles(bool transition)
        {
            foreach (SoundHandle handle in _parameterizedHandles)
                handle.RefreshParameterValues(transition);
        }

        private ExternalAudioSource GetOrCreateHolder(Transform attached, AudioAttachmentType attachType)
        {
            if (attached == null) attached = transform;
            
            ExternalAudioSource holder;

            if (attachType == AudioAttachmentType.Direct)
            {
                if (!attached.gameObject.TryGetComponent(out holder))
                    holder = attached.gameObject.AddComponent<ExternalAudioSource>();
            }
            else
            {
                if (!_externalHolders.TryGetValue(attached, out holder) || holder == null)
                {
                    var holderGO = new GameObject($"AudioSourceHolder_[{attached.name}]");
                    holderGO.transform.SetParent(transform);
                    holder = holderGO.AddComponent<ExternalAudioSource>();
                    holder.SetAttached(attached);
                    holder.DestroyEntireObjectOnDeplete = true;

                    _externalHolders[attached] = holder;
                }
            }
            return holder;
        }
    }
}