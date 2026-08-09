using System.Collections.Generic;
using System.Linq;
using Abb2kTools.Singletons;

#if ODIN_INSPECTOR
using Sirenix.Utilities;
#endif
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    public class SoundManager : PersistentSingleton<SoundManager>
    {
        private Dictionary<string, SourceRef> longLivingSound = new();
        private Dictionary<Transform, ExternalAudioSource> objectForTranform = new();

        private AudioListener _mainListener;
        private AudioListener MainListener
        {
            get => GrabListener();
            set => _mainListener = value;
        }

        private AudioListener GrabListener()
        {
            if (!_mainListener)
                _mainListener = FindAnyObjectByType<AudioListener>();

            return _mainListener;
        }

        private SourceRef CreateSource(SoundBase settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            AudioSource source = null;

            ExternalAudioSource obj = null;

            if (attached == null)
                attached = transform;

            if (attachType == AudioAttachmentType.Direct)
            {
                if (!attached.gameObject.TryGetComponent(out obj))
                    obj = attached.gameObject.AddComponent<ExternalAudioSource>();
                source = obj.AddAudioSource();
            }
            else if (attachType == AudioAttachmentType.External)
            {
                
                if (!objectForTranform.ContainsKey(attached))
                {
                    obj = new GameObject("Audio Source").AddComponent<ExternalAudioSource>();
                    obj.transform.SetParent(transform);
                    obj.SetAttached(attached);
                    obj.DestroyEntireObjectOnDeplete = true;
                    objectForTranform.Add(attached, obj);
                }
                else
                {
                    obj = objectForTranform[attached];
                }

                source = obj.AddAudioSource();
            }

            if (!source) return new SourceRef(source, null, obj);

            obj.OnDestroyed -= OnSourceKilled;
            obj.OnDestroyed += OnSourceKilled;

            return new SourceRef(settings.ApplySettings(source), null, obj);
        }

        public SourceRef CreateNewSource(string ID, SoundBase settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            var source = CreateSource(settings, attached, attachType);
            longLivingSound.Add(ID, new SourceRef(source.Source, ID, source.Holder));

            return longLivingSound[ID];
        }

        public SourceRef GetSource(string ID)
        {
            return longLivingSound.ContainsKey(ID) ? longLivingSound[ID] : null;
        }

        public void DestroySource(string ID)
        {
            if (!longLivingSound.ContainsKey(ID)) return;

            longLivingSound[ID].Holder.DeleteSource(longLivingSound[ID].Source);

            if (objectForTranform.ContainsKey(longLivingSound[ID].Holder.attached))
            {                
                if (longLivingSound[ID].Holder.AddedSources.Count == 0)
                {
                    objectForTranform.Remove(longLivingSound[ID].Holder.attached);

                    Destroy(longLivingSound[ID].Holder.gameObject);
                }
            }

            longLivingSound.Remove(ID);
        }

        public AudioSource CreateSFX(SoundBase settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            var source = CreateSource(settings, attached, attachType);
            source.Holder.SetKillTimerForSource(source.Source, settings.Length.HasValue ? settings.Length.Value : (source.Source.clip ? source.Source.clip.length : 0));
            source.Source.Play();
            return settings.ApplySettings(source.Source);
        }

        void OnSourceKilled(ExternalAudioSource externalSource)
        {
            if (objectForTranform.ContainsKey(externalSource.transform))
                objectForTranform.Remove(externalSource.transform);
            if (externalSource.attached != null && objectForTranform.ContainsKey(externalSource.attached))
                objectForTranform.Remove(externalSource.attached);

            HashSet<string> IDSToRemove = new();

            var sources = externalSource.AddedSources;
            
            foreach (var (ID, Audio) in longLivingSound)
            {
                if (sources.Contains(Audio.Source))
                    IDSToRemove.Add(ID);
            }

            foreach (var x in IDSToRemove)
                longLivingSound.Remove(x);
        }
    }
}
