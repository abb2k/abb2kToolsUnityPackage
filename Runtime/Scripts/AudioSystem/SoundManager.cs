using System.Collections.Generic;
using Abb2kTools.Singletons;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
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

        /// <summary>
        /// Plays a SoundEffect. Auto-destroys when done.
        /// </summary>
        public SoundHandle PlaySFX(SoundEffect sfxSettings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            if (sfxSettings == null || sfxSettings.sound == null) return null;

            List<PlayableClipData> clipsToPlay = new();
            sfxSettings.sound.CollectPlayableClips(clipsToPlay);
            if (clipsToPlay.Count == 0) return null;

            ExternalAudioSource holder = GetOrCreateHolder(attached, attachType);
            SoundHandle handle = new SoundHandle(null, holder, isPersistent: false);

            float randVol = sfxSettings.volumeRange.GetRandomInRange();
            float randPitch = sfxSettings.pitchRange.GetRandomInRange();

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                sfxSettings.ApplyBaseSettings(source, clipData, false, randVol, randPitch);
                
                handle.AddSource(source, clipData, sfxSettings.volume * randVol, sfxSettings.pitch * randPitch);
            }

            handle.Play();
            return handle;
        }

        /// <summary>
        /// Gets an existing persistent sound by ID, or creates it if it doesn't exist.
        /// </summary>
        public SoundHandle GetOrCreatePersistentSound(Sound soundSettings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            if (soundSettings == null || soundSettings.sound == null) return null;

            if (_persistentSounds.TryGetValue(soundSettings.soundID, out var existingHandle))
                return existingHandle;

            List<PlayableClipData> clipsToPlay = new();
            soundSettings.sound.CollectPlayableClips(clipsToPlay);
            if (clipsToPlay.Count == 0) return null;

            bool useNativeLoop = soundSettings.loop && clipsToPlay.Count == 1 && clipsToPlay[0].Delay <= 0f;
            bool useSequenceLoop = soundSettings.loop && !useNativeLoop;

            ExternalAudioSource holder = GetOrCreateHolder(attached, attachType);
            
            SoundHandle handle = new(soundSettings.soundID, holder, isPersistent: true)
            {
                SequenceLoop = useSequenceLoop
            };

            foreach (var clipData in clipsToPlay)
            {
                AudioSource source = holder.AddAudioSource();
                soundSettings.ApplyBaseSettings(source, clipData, useNativeLoop, 1f, 1f);
                
                handle.AddSource(source, clipData, soundSettings.volume, soundSettings.pitch);
            }

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
            if (attached == null)
                attached = transform;
            
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