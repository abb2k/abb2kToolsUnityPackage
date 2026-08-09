using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public abstract class SoundBase
    {   
        internal abstract float? Length { get; }

        internal abstract AudioSource ApplySettings(AudioSource source);
    }
}