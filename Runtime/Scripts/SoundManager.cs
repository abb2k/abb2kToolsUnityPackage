using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools
{
    [System.Serializable]
    public class Audio
    {
        public AudioSource Source {get; private set;}
        public string ID {get; private set;}
        public ExternalAudioSource Holder {get; private set;}

        private Audio(){}
        public Audio(AudioSource source, string ID, ExternalAudioSource holder)
        {
            this.Source = source;
            this.ID = ID;
            this.Holder = holder;
        }

        public bool CompareID(string otherID) => ID.Equals(otherID);
    }

    [System.Serializable]
    public class SourceSettings
    {   
        [Header("Options")]
        public AudioClip clip;
        public AudioMixerGroup output;
        [Min(0)]
        public float volume = 1;
        public float pitch = 1;
        public bool loop;
        [Range(-1, 1)]
        public float panStereo = 0;
        [Range(0, 1)]
        public float spatialBlend = 0;
        [Range(0, 1.1f)]
        public float reverbZoneMix = 0;
        [Range(0, 256)]
        public int prio = 0;
        [Header("3D")]
        [Range(0, 5)]
        public float dopplerLevel = 0;
        [Range(0, 360)]
        public float spread = 0;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Min(0)]
        public float minDist = 1;
        [Min(0)]
        public float maxDist = 500;

        public AudioSource ApplySettings(AudioSource source)
        {
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = loop;
            source.panStereo = panStereo;
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.reverbZoneMix = reverbZoneMix;
            source.priority = prio;
            source.dopplerLevel = dopplerLevel;
            source.rolloffMode = rolloff;
            source.minDistance = minDist;
            source.maxDistance = maxDist;
            source.spread = spread;
            source.outputAudioMixerGroup = output;

            return source;
        }
    }

    public enum AudioAttachmentType
    {
        Direct,
        External
    }

    public class SoundManager : Singleton<SoundManager>
    {
        private Dictionary<string, Audio> longLivingSound = new();
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

        private (AudioSource, ExternalAudioSource) CreateSource(SourceSettings settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
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

            if (!source) return (source, obj);

            obj.OnDestroyed -= OnSourceKilled;
            obj.OnDestroyed += OnSourceKilled;

            return (settings.ApplySettings(source), obj);
        }

        public Audio CreateNewSource(string ID, SourceSettings settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            var source = CreateSource(settings, attached, attachType);
            longLivingSound.Add(ID, new Audio(source.Item1, ID, source.Item2));

            return longLivingSound[ID];
        }

        public Audio GetSource(string ID)
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

        public AudioSource CreateSFX(SourceSettings settings, Transform attached = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            if (settings.clip == null) return null;

            var source = CreateSource(settings, attached, attachType);
            source.Item2.SetKillTimerForSource(source.Item1, source.Item1.clip.length);
            source.Item1.Play();
            return settings.ApplySettings(source.Item1);
        }

        void OnSourceKilled(ExternalAudioSource externalSource)
        {
            if (objectForTranform.ContainsKey(externalSource.transform))
                objectForTranform.Remove(externalSource.transform);
            if (objectForTranform.ContainsKey(externalSource.attached))
                objectForTranform.Remove(externalSource.attached);

            HashSet<string> IDSToRemove = new();

            var sources = externalSource.AddedSources;
            
            foreach (var (ID, Audio) in longLivingSound)
            {
                if (sources.Contains(Audio.Source))
                    IDSToRemove.Add(ID);
            }

            IDSToRemove.ForEach(x => longLivingSound.Remove(x));
        }
    }
}
