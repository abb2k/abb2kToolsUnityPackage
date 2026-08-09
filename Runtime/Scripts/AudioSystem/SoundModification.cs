using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [CreateAssetMenu(fileName = "Sound", menuName = "Audio/Sound")]
    public class SoundModification : SoundModificationBase
    {
        [Header("Options")]
        public AudioClip clip;
        [Min(0)]
        public float volume = 1;
        public float pitch = 1;

        internal override float? Length => clip.length;

        protected override AudioSource ApplySettings(AudioSource source)
        {
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;

            return source;
        }
    }
}