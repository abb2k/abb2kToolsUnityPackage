using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public struct SoundFadeSettings
    {
        public bool enabled;
        [Min(0f)] public float duration;
        public AnimationCurve curve;
    }

    [System.Serializable]
    public struct SoundFadeEnvelope
    {
        public float StartTime;
        public float EndTime;
        public SoundFadeSettings FadeIn;
        public SoundFadeSettings FadeOut;

        public float Evaluate(float time)
        {
            float multiplier = 1f;
            if (FadeIn.enabled && FadeIn.duration > 0f)
            {
                float fadeInTime = Mathf.Clamp01((time - StartTime) / FadeIn.duration);
                multiplier *= EvaluateCurve(FadeIn.curve, fadeInTime, fadeInTime);
            }

            if (FadeOut.enabled && FadeOut.duration > 0f)
            {
                float effectiveDuration = Mathf.Min(FadeOut.duration, Mathf.Max(0f, EndTime - StartTime));
                if (effectiveDuration > 0f)
                {
                    float fadeOutStart = EndTime - effectiveDuration;
                    if (time >= fadeOutStart)
                    {
                        float fadeOutTime = Mathf.Clamp01((time - fadeOutStart) / effectiveDuration);
                        multiplier *= EvaluateCurve(FadeOut.curve, fadeOutTime, 1f - fadeOutTime);
                    }
                }
            }

            return multiplier;
        }

        private static float EvaluateCurve(AnimationCurve curve, float time, float fallback)
        {
            return curve == null || curve.length == 0
                ? fallback
                : Mathf.Clamp01(curve.Evaluate(time));
        }
    }

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
        public List<SoundParameterBinding> ParameterBindings;
        public List<SoundFadeEnvelope> FadeEnvelopes;
    }
    
    public abstract class SoundModificationBase : ScriptableObject
    {
        public List<SoundParameterBinding> parameterBindings = new();
        [SerializeField] private SoundFadeSettings fadeIn = new()
        {
            duration = 0.25f,
            curve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };
        [SerializeField] private SoundFadeSettings fadeOut = new()
        {
            duration = 0.25f,
            curve = AnimationCurve.Linear(0f, 1f, 1f, 0f)
        };

        public abstract AudioFilterSettings Filters { get; }

        /// <summary>
        /// Recursively gathers all clips.
        /// </summary>
        public abstract void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f);

        protected void AppendFadeEnvelope(List<PlayableClipData> result, int startIndex, float startTime)
        {
            if ((!fadeIn.enabled || fadeIn.duration <= 0f) &&
                (!fadeOut.enabled || fadeOut.duration <= 0f))
                return;

            float endTime = startTime;
            for (int i = startIndex; i < result.Count; i++)
            {
                PlayableClipData clip = result[i];
                if (clip.Clip == null)
                    continue;

                float duration = (clip.Clip.length - clip.StartOffset - clip.EndOffset) /
                                 Mathf.Max(0.001f, clip.Pitch);
                endTime = Mathf.Max(endTime, clip.Delay + duration);
            }

            if (endTime <= startTime)
                return;

            SoundFadeEnvelope envelope = new()
            {
                StartTime = startTime,
                EndTime = endTime,
                FadeIn = fadeIn,
                FadeOut = fadeOut
            };

            for (int i = startIndex; i < result.Count; i++)
            {
                PlayableClipData clip = result[i];
                clip.FadeEnvelopes ??= new List<SoundFadeEnvelope>();
                clip.FadeEnvelopes.Add(envelope);
                result[i] = clip;
            }
        }

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