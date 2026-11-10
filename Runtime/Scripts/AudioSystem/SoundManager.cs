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

            Ranged volumeRange = default;
            Ranged pitchRange = default;
            bool hasRandomization = false;

            if (settings is SoundEffect sfxSettings)
            {
                volumeRange = sfxSettings.volumeRange;
                pitchRange = sfxSettings.pitchRange;
                hasRandomization = true;
            }
            else if (settings is SoundEffectOneShot oneShotSettings)
            {
                volumeRange = oneShotSettings.volumeRange;
                pitchRange = oneShotSettings.pitchRange;
                hasRandomization = true;
            }
            else if (settings is SoundEffectLooping loopingSettings)
            {
                volumeRange = loopingSettings.volumeRange;
                pitchRange = loopingSettings.pitchRange;
                hasRandomization = true;
            }

            float randVol = hasRandomization ? volumeRange.Random() : 1f;
            float randPitch = hasRandomization ? pitchRange.Random() : 1f;

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                settings.ApplyBaseSettings(source, clipData, useNativeLoop, randVol, randPitch);
                handle.AddSource(source, clipData, settings.volume * randVol, settings.pitch * randPitch);
            }

            if (loop && hasRandomization)
            {
                handle.OnLoopRestart += () =>
                {
                    float newRandVol = volumeRange.Random();
                    float newRandPitch = pitchRange.Random();
                    handle.UpdateRandomModifiers(settings.volume * newRandVol, settings.pitch * newRandPitch);
                };
            }

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

            bool hasOffsets = clipsToPlay.Count > 0 && (clipsToPlay[0].StartOffset > 0f || clipsToPlay[0].EndOffset > 0f);
            
            bool useNativeLoop = soundSettings.loop && clipsToPlay.Count == 1 && clipsToPlay[0].Delay <= 0f && !hasOffsets;
            bool useSequenceLoop = soundSettings.loop && !useNativeLoop;

            ExternalAudioSource holder = GetOrCreateHolder(attached, attachType);
            
            SoundHandle handle = new(soundSettings.soundID, holder, isPersistent: true, scaleWithTimeScale: soundSettings.scaleWithTimeScale)
            {
                SequenceLoop = useSequenceLoop
            };

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                soundSettings.ApplyBaseSettings(source, clipData, useNativeLoop, 1f, 1f);
                handle.AddSource(source, clipData, soundSettings.volume, soundSettings.pitch);
            }

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