using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/SoundModification.png")]
    [CreateAssetMenu(fileName = "Sound", menuName = "Audio/Sound")]
    public class SoundModification : SoundModificationBase
    {
        [Header("Options")]
        [SerializeField] private AudioClip clip;
        [Min(0)]
        [SerializeField] private float volume = 1f;
        [SerializeField] private float pitch  = 1f;
        [SerializeField] private AudioMixerGroup preferredMixerGroup;

        public AudioClip Clip => clip;
        public float Volume   => volume;
        public float Pitch    => pitch;
        public AudioMixerGroup PreferredMixerGroup => preferredMixerGroup;

        public override void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f)
        {
            if (clip == null) return;

            result.Add(new PlayableClipData
            {
                Clip = clip,
                Volume = volume * currentVolume,
                Pitch = pitch * currentPitch,
                Delay = currentDelay,
                PreferredMixerGroup = preferredMixerGroup
            });
        }
    }
}