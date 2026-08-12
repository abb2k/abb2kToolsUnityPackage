using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public abstract class SoundBase
    {
        public SoundModificationBase sound;
        
        public enum MixerGroupPreference { Preferred, Specific }
        public MixerGroupPreference mixerGroupPreference;
        public AudioMixerGroup specificMixerGroup;

        [Range(0f, 1f)] public float volume = 1f;
        [Range(-3f, 3f)] public float pitch = 1f;
        [Range(-1f, 1f)] public float panStereo = 0f;
        [Range(0f, 1f)] public float spatialBlend = 0f;
        [Range(0f, 1.1f)] public float reverbZoneMix = 1f;
        [Range(0, 256)] public int prio = 128;

        [Range(0f, 5f)] public float dopplerLevel = 1f;
        [Range(0, 360)] public float spread = 0f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Min(0)] public float minDist = 1f;
        [Min(0)] public float maxDist = 500f;

        public void ApplyBaseSettings(AudioSource audioSource, PlayableClipData clipData, bool nativeLoop, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            audioSource.playOnAwake = false; 
            audioSource.loop = nativeLoop; 
            
            audioSource.clip = clipData.Clip;
            audioSource.volume = volume * clipData.Volume * volumeMultiplier;
            audioSource.pitch = pitch * clipData.Pitch * pitchMultiplier;
            
            audioSource.panStereo = panStereo;
            audioSource.spatialBlend = spatialBlend;
            audioSource.reverbZoneMix = reverbZoneMix;
            audioSource.priority = prio;

            audioSource.dopplerLevel = dopplerLevel;
            audioSource.spread = spread;
            audioSource.rolloffMode = rolloff;
            audioSource.minDistance = minDist;
            audioSource.maxDistance = maxDist;

            if (mixerGroupPreference == MixerGroupPreference.Specific && specificMixerGroup != null)
                audioSource.outputAudioMixerGroup = specificMixerGroup;
            else
                audioSource.outputAudioMixerGroup = clipData.PreferredMixerGroup;
        }
    }
}