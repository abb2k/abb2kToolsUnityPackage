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

        [Range(0f, 1f)]
        public float volume        = 1f;
        [Range(-3f, 3f)]
        public float pitch         = 1f;
        [Range(-1f, 1f)]
        public float panStereo     = 0f;
        [Range(0f, 1f)]
        public float spatialBlend  = 0f;
        [Range(0f, 1.1f)]
        public float reverbZoneMix = 1f;
        [Range(0, 256)]
        public int prio            = 128;

        [Range(0f, 5f)]
        public float dopplerLevel = 1f;
        [Range(0, 360)]
        public float spread       = 0f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Min(0)]
        public float minDist      = 1f;
        [Min(0)]
        public float maxDist      = 500f;

        public void ApplyBaseSettings(AudioSource audioSource, PlayableClipData clipData, bool nativeLoop, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            audioSource.playOnAwake = false; 
            audioSource.loop = nativeLoop; 
            
            audioSource.clip   = clipData.Clip;
            audioSource.volume = volume * clipData.Volume * volumeMultiplier;
            audioSource.pitch  = pitch * clipData.Pitch * pitchMultiplier;
            
            audioSource.panStereo     = panStereo;
            audioSource.spatialBlend  = spatialBlend;
            audioSource.reverbZoneMix = reverbZoneMix;
            audioSource.priority      = prio;

            audioSource.dopplerLevel = dopplerLevel;
            audioSource.spread       = spread;
            audioSource.rolloffMode  = rolloff;
            audioSource.minDistance  = minDist;
            audioSource.maxDistance  = maxDist;

            if (mixerGroupPreference == MixerGroupPreference.Specific && specificMixerGroup != null)
                audioSource.outputAudioMixerGroup = specificMixerGroup;
            else
                audioSource.outputAudioMixerGroup = clipData.PreferredMixerGroup;
        }

        /// <summary>
        /// Analyzes the asset and calculates the exact timeline metadata of what will play, 
        /// including all randomizations, filters, delays, and offsets.
        /// </summary>
        public SoundAudioInfo GetAudioInfo(float globalVolumeMult = 1f, float globalPitchMult = 1f)
        {
            var info = new SoundAudioInfo();
            var clips = new System.Collections.Generic.List<PlayableClipData>();
            
            sound.CollectPlayableClips(clips, globalVolumeMult, globalPitchMult, 0f);

            info.TotalClips = clips.Count;
            var mixerCounts = new System.Collections.Generic.Dictionary<AudioMixerGroup, int>();

            float maxEndTime = 0f;
            float maxEndTimeWithTails = 0f;

            foreach (var clip in clips)
            {
                if (clip.Clip == null) continue;

                float pitch = Mathf.Max(0.001f, clip.Pitch);
                float actualDuration = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / pitch;
                
                float clipEndTime = clip.Delay + actualDuration;

                if (clipEndTime > maxEndTime) 
                    maxEndTime = clipEndTime;

                float tail = 0f;
                if (clip.Filters != null)
                {
                    if (clip.Filters.enableDistortion || clip.Filters.enableEcho || clip.Filters.enableReverb) 
                        info.ContainsFilters = true;

                    if (clip.Filters.enableEcho) tail = (clip.Filters.echoDelay / 1000f) * 5f; 
                    if (clip.Filters.enableReverb) tail = Mathf.Max(tail, 3f); 
                }

                if (clipEndTime + tail > maxEndTimeWithTails) 
                    maxEndTimeWithTails = clipEndTime + tail;

                if (clip.PreferredMixerGroup != null)
                {
                    if (mixerCounts.ContainsKey(clip.PreferredMixerGroup)) mixerCounts[clip.PreferredMixerGroup]++;
                    else mixerCounts[clip.PreferredMixerGroup] = 1;
                }
            }

            info.TotalDuration = maxEndTime;
            info.DurationWithTails = maxEndTimeWithTails;

            int highestCount = 0;
            foreach (var kvp in mixerCounts)
            {
                if (kvp.Value > highestCount)
                {
                    highestCount = kvp.Value;
                    info.DominantMixerGroup = kvp.Key;
                }
            }

            return info;
        }
    }
}