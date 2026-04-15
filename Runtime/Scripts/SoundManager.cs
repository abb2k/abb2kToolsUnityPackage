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

        private (AudioSource, ExternalAudioSource) CreateSource(Transform attached, AudioAttachmentType attachType, SourceSettings settings)
        {
            AudioSource source = null;

            ExternalAudioSource obj = null;

            if (attachType == AudioAttachmentType.Direct)
            {
                if (attached.gameObject.TryGetComponent(out obj))
                    obj = attached.gameObject.AddComponent<ExternalAudioSource>();
                source = obj.AddAudioSource();
            }
            else if (attachType == AudioAttachmentType.External)
            {
                
                if (!objectForTranform.ContainsKey(attached))
                {
                    obj = new GameObject("Audio Source").AddComponent<ExternalAudioSource>();
                    obj.transform.SetParent(transform);
                    obj.SetFollow(attached);
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

        public Audio CreateNewSource(string ID, Transform attached, AudioAttachmentType attachType, SourceSettings settings)
        {
            var source = CreateSource(attached, attachType, settings);
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

            if (objectForTranform.ContainsKey(longLivingSound[ID].Holder.transform))
            {                
                if (longLivingSound[ID].Holder.AddedSources.Count == 0)
                {
                    objectForTranform.Remove(longLivingSound[ID].Source.gameObject.transform);

                    Destroy(longLivingSound[ID].Holder.gameObject);
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

        void CheckForDeletion()
        {
            
        }

        void OnSourceKilled(ExternalAudioSource externalSource)
        {
            if (objectForTranform.ContainsKey(externalSource.transform))
                objectForTranform.Remove(externalSource.transform);

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
