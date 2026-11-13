using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [DisallowMultipleComponent]
    public sealed class AudioSourceGain : MonoBehaviour
    {
        private volatile float gain = 1f;
        private volatile float fadeMultiplier = 1f;

        public static void SetVolume(AudioSource source, float volume)
        {
            if (source == null) return;

            float nonNegativeVolume = Mathf.Max(0f, volume);
            var gainFilter = source.GetComponent<AudioSourceGain>();
            if (nonNegativeVolume > 1f && gainFilter == null)
            {
                gainFilter = source.gameObject.AddComponent<AudioSourceGain>();
            }

            source.volume = Mathf.Min(1f, nonNegativeVolume);
            if (gainFilter != null)
            {
                gainFilter.gain = Mathf.Max(1f, nonNegativeVolume);
            }
        }

        public static void SetFadeMultiplier(AudioSource source, float multiplier)
        {
            if (source == null) return;

            AudioSourceGain gainFilter = source.GetComponent<AudioSourceGain>();
            float clampedMultiplier = Mathf.Clamp01(multiplier);
            if (gainFilter == null)
            {
                if (Mathf.Approximately(clampedMultiplier, 1f))
                    return;

                gainFilter = source.gameObject.AddComponent<AudioSourceGain>();
            }

            gainFilter.fadeMultiplier = clampedMultiplier;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float currentGain = gain * fadeMultiplier;
            if (currentGain == 1f) return;

            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= currentGain;
            }
        }
    }
}
