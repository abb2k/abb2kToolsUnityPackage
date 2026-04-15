using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEngine;

namespace Abb2kTools
{
    [System.Serializable]
    public class Audio
    {
        public AudioSource Source {get; private set;}
        public string ID {get; private set;}

        private Audio(){}
        public Audio(AudioSource source, string ID)
        {
            this.Source = source;
            this.ID = ID;
        }

        public bool CompareID(string otherID) => ID.Equals(otherID);
    }

    [System.Serializable]
    public struct SourceSettings
    {
        

        public AudioSource ApplySettings(AudioSource source)
        {
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

        private AudioSource CreateSource(Transform attached, AudioAttachmentType attachType, SourceSettings settings)
        {
            AudioSource source = null;

            ExternalAudioSource obj = null;

            if (attachType == AudioAttachmentType.Direct)
            {
                source = attached.gameObject.AddComponent<AudioSource>();
                if (attached.gameObject.TryGetComponent(out obj))
                    obj = attached.gameObject.AddComponent<ExternalAudioSource>();
            }
            else if (attachType == AudioAttachmentType.External)
            {
                
                if (!objectForTranform.ContainsKey(attached))
                {
                    obj = new GameObject("Audio Source").AddComponent<ExternalAudioSource>();
                    obj.transform.SetParent(transform);
                    obj.Setup(attached);
                    objectForTranform.Add(attached, obj);
                }
                else
                {
                    obj = objectForTranform[attached];
                }

                source = obj.gameObject.AddComponent<AudioSource>();
            }

            if (!source) return source;

            obj.OnDestroyed -= OnSourceKilled;
            obj.OnDestroyed += OnSourceKilled;

            return settings.ApplySettings(source);
        }

        public Audio CreateNewSource(string ID, Transform attached, AudioAttachmentType attachType, SourceSettings settings)
        {
            var source = CreateSource(attached, attachType, settings);
            longLivingSound.Add(ID, new Audio(source, ID));

            return longLivingSound[ID];
        }

        public Audio GetSource(string ID)
        {
            return longLivingSound.ContainsKey(ID) ? longLivingSound[ID] : null;
        }

        public void DestroySource(string ID)
        {
            if (!longLivingSound.ContainsKey(ID)) return;

            Destroy(longLivingSound[ID].Source);

            if (objectForTranform.ContainsKey(longLivingSound[ID].Source.gameObject.transform))
            {
                var audioHolder = objectForTranform[longLivingSound[ID].Source.gameObject.transform];
                
                if (!audioHolder.TryGetComponent(out AudioSource _))
                {
                    objectForTranform.Remove(longLivingSound[ID].Source.gameObject.transform);

                    Destroy(audioHolder);
                }
            }

            longLivingSound.Remove(ID);
        }

        public AudioSource CreateSFX(Transform attached, AudioAttachmentType attachType, SourceSettings settings)
        {
            var source = CreateSource(attached, attachType, settings);
            //
            //
            return source;
        }

        void OnSourceKilled(ExternalAudioSource externalSource)
        {
            if (objectForTranform.ContainsKey(externalSource.transform))
                objectForTranform.Remove(externalSource.transform);

            HashSet<string> IDSToRemove = new();

            var sources = externalSource.GetComponents<AudioSource>();
            
            foreach (var (ID, Audio) in longLivingSound)
            {
                if (sources.Contains(Audio.Source))
                    IDSToRemove.Add(ID);
            }

            IDSToRemove.ForEach(x => longLivingSound.Remove(x));
        }
    }
}
