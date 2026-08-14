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
        public float StartOffset;
        public float EndOffset;
        public AudioMixerGroup PreferredMixerGroup;
        public AudioFilterSettings Filters;
    }
    
    public abstract class SoundModificationBase : ScriptableObject
    {
        public abstract AudioFilterSettings Filters { get; }

        /// <summary>
        /// Recursively gathers all clips.
        /// </summary>
        public abstract void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f);

        /// <summary>
        /// Analyzes the entire composition/sound hierarchy and calculates the exact timeline metadata.
        /// </summary>
        public SoundAudioInfo GetAudioInfo(float globalVolumeMult = 1f, float globalPitchMult = 1f)
        {
            var info = new SoundAudioInfo();
            var clips = new List<PlayableClipData>();
            
            CollectPlayableClips(clips, globalVolumeMult, globalPitchMult, 0f);

            info.TotalClips = clips.Count;
            info.ContainsFilters = this.Filters != null && (this.Filters.enableDistortion || this.Filters.enableEcho || this.Filters.enableReverb);

            var mixerCounts = new Dictionary<AudioMixerGroup, int>();

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