using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    public class ExternalAudioSource : MonoBehaviour
    {
        public Transform attached;
        public bool DestroyEntireObjectOnDeplete = false;

        public event Action<ExternalAudioSource> OnDestroyed;

        private readonly List<AudioSource> _addedSources = new();

        public void SetAttached(Transform target) => attached = target;

        public AudioSource AddAudioSource()
        {
            // Create a fresh AudioSource component for guaranteed clean playback
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            _addedSources.Add(newSource);
            return newSource;
        }

        public void DeleteSource(AudioSource source)
        {
            if (source == null) return;
            
            source.Stop();
            source.clip = null; 

            // 1. Remove it from our tracking list
            if (_addedSources.Contains(source))
            {
                _addedSources.Remove(source);
            }

            // 2. Destroy the actual component so it doesn't clutter the GameObject
            Destroy(source);

            // 3. Auto-cleanup the entire empty holder object if required
            if (DestroyEntireObjectOnDeplete && _addedSources.Count == 0)
            {
                OnDestroyed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Follow the attached transform if this is an external holder
            if (attached != null)
            {
                transform.position = attached.position;
                transform.rotation = attached.rotation;
            }
        }

        private void OnDestroy() => OnDestroyed?.Invoke(this);
    }
}