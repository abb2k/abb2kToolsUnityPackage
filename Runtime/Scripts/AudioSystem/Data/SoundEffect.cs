using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class SoundEffect : SoundBase
    {
        [Header("SFX Playback")]
        public bool loop = false;

        [Header("SFX Randomization Modifiers")]
        [Tooltip("Multiplies against the base volume.")]
        public Ranged volumeRange = new(0.9f, 1.1f);
        [Tooltip("Multiplies against the base pitch.")]
        public Ranged pitchRange = new(0.9f, 1.1f);

        public SoundHandle Play(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return SoundManager.Instance.PlaySFX(this, attachedTransform, attachType);
        }
    }
}