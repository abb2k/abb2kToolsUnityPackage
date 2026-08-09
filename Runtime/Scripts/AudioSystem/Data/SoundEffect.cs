using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class SoundEffect : SoundBase
    {   
        [Header("Options")]
        public SoundModificationBase sound;
        public AudioMixerGroup output;

        internal override float? Length => sound.Length;

        override internal AudioSource ApplySettings(AudioSource source) => sound.ApplySettings(source, s =>
        {
            s.outputAudioMixerGroup = output;

            return s;
        });
    }
}