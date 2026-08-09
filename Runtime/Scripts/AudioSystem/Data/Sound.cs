using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class Sound : SoundBase
    {   
        [Header("Options")]
        public SoundModificationBase sound;
        public AudioMixerGroup output;
        public bool loop;
        [Range(-1, 1)]
        public float panStereo = 0;
        [Range(0, 1)]
        public float spatialBlend = 0;
        [Range(0, 1.1f)]
        public float reverbZoneMix = 0;
        [Range(0, 256)]
        public int prio = 0;
        [Header("3D")]
        [Range(0, 5)]
        public float dopplerLevel = 0;
        [Range(0, 360)]
        public float spread = 0;
        public AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Min(0)]
        public float minDist = 1;
        [Min(0)]
        public float maxDist = 500;

        internal override float? Length => sound.Length;

        override internal AudioSource ApplySettings(AudioSource source) => sound.ApplySettings(source, s =>
        {
            s.loop = loop;
            s.panStereo = panStereo;
            s.playOnAwake = false;
            s.spatialBlend = spatialBlend;
            s.reverbZoneMix = reverbZoneMix;
            s.priority = prio;
            s.dopplerLevel = dopplerLevel;
            s.rolloffMode = rolloff;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.spread = spread;
            s.outputAudioMixerGroup = output;

            return s;
        });
    }
}