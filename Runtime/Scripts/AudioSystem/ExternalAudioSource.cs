using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/ExternalAudioSource.png")]
    public class ExternalAudioSource : MonoBehaviour
    {
        public Transform attached;
        public bool DestroyEntireObjectOnDeplete = false;

        public event Action<ExternalAudioSource> OnDestroyed;

        private readonly List<AudioSource> _addedSources = new();

        public void SetAttached(Transform target) => attached = target;

        public AudioSource AddAudioSource()
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            _addedSources.Add(newSource);
            return newSource;
        }

        public void DeleteSource(AudioSource source)
        {
            if (source == null) return;
            
            source.Stop();
            source.clip = null; 

            if (_addedSources.Contains(source))
            {
                _addedSources.Remove(source);
            }

            Destroy(source);

            if (DestroyEntireObjectOnDeplete && _addedSources.Count == 0)
            {
                OnDestroyed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!attached) return;

            transform.position = attached.position;
            transform.rotation = attached.rotation;
        }

        private void OnDestroy() => OnDestroyed?.Invoke(this);
    }
}