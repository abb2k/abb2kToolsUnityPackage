using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    public enum SoundParameterMode
    {
        Constant,
        RandomBetween
    }

    [System.Serializable]
    public struct SoundParameterRandomization
    {
        public SoundParameterMode mode;
        public float min;
        public float max;

        public SoundParameterRandomization(float value)
        {
            mode = SoundParameterMode.Constant;
            min = value;
            max = value;
        }

        public float Sample(float constantValue)
        {
            return mode == SoundParameterMode.RandomBetween
                ? Random.Range(min, max)
                : constantValue;
        }

        public int Sample(int constantValue)
        {
            return mode == SoundParameterMode.RandomBetween
                ? Mathf.RoundToInt(Random.Range(min, max))
                : constantValue;
        }
    }

    public struct SoundBasePlaybackSettings
    {
        public float volume;
        public float pitch;
        public float panStereo;
        public float spatialBlend;
        public float reverbZoneMix;
        public int prio;
        public float dopplerLevel;
        public float spread;
        public float minDist;
        public float maxDist;
    }

    [System.Serializable]
    public abstract class SoundBase
    {
        public SoundModificationBase sound;
        
        public enum MixerGroupPreference { Preferred, Specific }
        public MixerGroupPreference mixerGroupPreference;
        public AudioMixerGroup specificMixerGroup;

        [Range(0f, 1f)]
        public float volume        = 1f;
        [SerializeField, HideInInspector] private SoundParameterRandomization volumeRandomization = new(1f);
        [Range(-3f, 3f)]
        public float pitch         = 1f;
        [SerializeField, HideInInspector] private SoundParameterRandomization pitchRandomization = new(1f);
        public bool scaleWithTimeScale = true;
        [Range(-1f, 1f)]
        public float panStereo     = 0f;
        [SerializeField, HideInInspector] private SoundParameterRandomization panStereoRandomization = new(0f);
        [Range(0f, 1f)]
        public float spatialBlend  = 0f;
        [SerializeField, HideInInspector] private SoundParameterRandomization spatialBlendRandomization = new(0f);
        [Range(0f, 1.1f)]
        public float reverbZoneMix = 1f;
        [SerializeField, HideInInspector] private SoundParameterRandomization reverbZoneMixRandomization = new(1f);
        [Range(0, 256)]
        public int prio            = 128;
        [SerializeField, HideInInspector] private SoundParameterRandomization prioRandomization = new(128f);

        [Range(0f, 5f)]
        public float dopplerLevel = 1f;
        [SerializeField, HideInInspector] private SoundParameterRandomization dopplerLevelRandomization = new(1f);
        [Range(0, 360)]
        public float spread       = 0f;
        [SerializeField, HideInInspector] private SoundParameterRandomization spreadRandomization = new(0f);
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Min(0)]
        public float minDist      = 1f;
        [SerializeField, HideInInspector] private SoundParameterRandomization minDistRandomization = new(1f);
        [Min(0)]
        public float maxDist      = 500f;
        [SerializeField, HideInInspector] private SoundParameterRandomization maxDistRandomization = new(500f);

        public SoundBasePlaybackSettings SamplePlaybackSettings()
        {
            return new SoundBasePlaybackSettings
            {
                volume = volumeRandomization.Sample(volume),
                pitch = pitchRandomization.Sample(pitch),
                panStereo = panStereoRandomization.Sample(panStereo),
                spatialBlend = spatialBlendRandomization.Sample(spatialBlend),
                reverbZoneMix = reverbZoneMixRandomization.Sample(reverbZoneMix),
                prio = prioRandomization.Sample(prio),
                dopplerLevel = dopplerLevelRandomization.Sample(dopplerLevel),
                spread = spreadRandomization.Sample(spread),
                minDist = minDistRandomization.Sample(minDist),
                maxDist = maxDistRandomization.Sample(maxDist)
            };
        }

        public void ApplyBaseSettings(AudioSource audioSource, PlayableClipData clipData, bool nativeLoop, float volumeMultiplier = 1f, float pitchMultiplier = 1f)
        {
            ApplyBaseSettings(audioSource, clipData, nativeLoop, volumeMultiplier, pitchMultiplier, SamplePlaybackSettings());
        }

        public void ApplyBaseSettings(AudioSource audioSource, PlayableClipData clipData, bool nativeLoop, float volumeMultiplier, float pitchMultiplier, SoundBasePlaybackSettings settings)
        {
            audioSource.playOnAwake = false; 
            audioSource.loop = nativeLoop; 
            
            audioSource.clip   = clipData.Clip;
            audioSource.volume = settings.volume * clipData.Volume * volumeMultiplier;
            audioSource.pitch  = settings.pitch * clipData.Pitch * pitchMultiplier * (scaleWithTimeScale ? Time.timeScale : 1f);
            
            audioSource.panStereo     = settings.panStereo;
            audioSource.spatialBlend  = settings.spatialBlend;
            audioSource.reverbZoneMix = settings.reverbZoneMix;
            audioSource.priority      = settings.prio;

            audioSource.dopplerLevel = settings.dopplerLevel;
            audioSource.spread       = settings.spread;
            audioSource.rolloffMode  = rolloff;
            audioSource.minDistance  = settings.minDist;
            audioSource.maxDistance  = settings.maxDist;

            if (mixerGroupPreference == MixerGroupPreference.Specific && specificMixerGroup != null)
                audioSource.outputAudioMixerGroup = specificMixerGroup;
            else
                audioSource.outputAudioMixerGroup = clipData.PreferredMixerGroup;
        }

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