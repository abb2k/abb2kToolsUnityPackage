using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class Sound : SoundBase
    {
        [Header("Persistent Options")]
        public string soundID;
        public bool loop = true;

        public SoundHandle GetHandle(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return SoundManager.Instance.GetOrCreatePersistentSound(this, attachedTransform, attachType);
        }

        public SoundHandle Play(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            var handle = GetHandle(attachedTransform, attachType);
            handle.Play();
            return handle;
        }
    }
}