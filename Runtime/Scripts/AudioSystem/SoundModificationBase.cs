using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    public struct PlayableClipData
    {
        public AudioClip Clip;
        public float Volume;
        public float Pitch;
        public float Delay;
        public AudioMixerGroup PreferredMixerGroup;
    }
    
    public abstract class SoundModificationBase : ScriptableObject
    {
        /// <summary>
        /// Recursively gathers all clips.
        /// </summary>
        public abstract void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f);
    }
}