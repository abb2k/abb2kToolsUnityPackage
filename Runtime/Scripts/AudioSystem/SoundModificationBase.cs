using System;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    // [CreateAssetMenu(fileName = "SoundBase", menuName = "Scriptable Objects/SoundBase")]
    public abstract class SoundModificationBase : ScriptableObject
    {
        internal abstract float? Length { get; }

        internal AudioSource ApplySettings(AudioSource source, Func<AudioSource, AudioSource> externalApply)
        {
            return externalApply(ApplySettings(source));
        }
        abstract protected AudioSource ApplySettings(AudioSource source);
    }
}