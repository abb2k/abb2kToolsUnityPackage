using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class SoundEffect : SoundBase
    {
        [Header("SFX Playback")]
        public bool loop = false;

        public SoundHandle Play(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return SoundManager.Instance.PlaySFX(this, attachedTransform, attachType);
        }
    }

    [System.Serializable]
    public class SoundEffectOneShot : SoundBase
    {
        public SoundHandle Play(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return SoundManager.Instance.CreateSoundHandle(this, attachedTransform, attachType, false, true);
        }
    }
}