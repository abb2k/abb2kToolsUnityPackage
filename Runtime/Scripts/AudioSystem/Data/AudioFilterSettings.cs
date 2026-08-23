using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class AudioFilterSettings
    {
        [Header("Distortion")]
        public bool enableDistortion = false;
        [Range(0f, 1f)] public float distortionLevel = 0.5f;

        [Header("Echo")]
        public bool enableEcho = false;
        [Range(10f, 5000f)] public float echoDelay = 500f;
        [Range(0f, 1f)] public float echoDecayRatio = 0.5f;

        [Header("Reverb")]
        public bool enableReverb = false;
        public AudioReverbPreset reverbPreset = AudioReverbPreset.Generic;

        // Creates a deep copy so parent compositions don't mutate child scriptable objects
        public AudioFilterSettings Clone()
        {
            return new AudioFilterSettings
            {
                enableDistortion = this.enableDistortion,
                distortionLevel = this.distortionLevel,
                enableEcho = this.enableEcho,
                echoDelay = this.echoDelay,
                echoDecayRatio = this.echoDecayRatio,
                enableReverb = this.enableReverb,
                reverbPreset = this.reverbPreset
            };
        }

        public void ApplyTo(AudioSource source)
        {
            if (source == null) return;
            GameObject go = source.gameObject;

            if (enableDistortion)
            {
                var dist = go.GetComponent<AudioDistortionFilter>();
                if (dist == null) dist = go.AddComponent<AudioDistortionFilter>();
                dist.distortionLevel = distortionLevel;
            }

            if (enableEcho)
            {
                var echo = go.GetComponent<AudioEchoFilter>();
                if (echo == null) echo = go.AddComponent<AudioEchoFilter>();
                echo.delay = echoDelay;
                echo.decayRatio = echoDecayRatio;
            }

            if (enableReverb)
            {
                var reverb = go.GetComponent<AudioReverbFilter>();
                if (reverb == null) reverb = go.AddComponent<AudioReverbFilter>();
                reverb.reverbPreset = reverbPreset;
            }
        }

        public void MergeWithParent(AudioFilterSettings parentSettings)
        {
            if (parentSettings == null) return;
            
            if (parentSettings.enableDistortion)
            {
                enableDistortion = true;
                distortionLevel = parentSettings.distortionLevel;
            }
            if (parentSettings.enableEcho)
            {
                enableEcho = true;
                echoDelay = parentSettings.echoDelay;
                echoDecayRatio = parentSettings.echoDecayRatio;
            }
            if (parentSettings.enableReverb)
            {
                enableReverb = true;
                reverbPreset = parentSettings.reverbPreset;
            }
        }
    }
}