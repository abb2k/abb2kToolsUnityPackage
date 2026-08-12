using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    public static class AudioSysExtensions
    {
        /// <summary>
        /// Plays a SoundEffect on this GameObject using Direct attachment.
        /// </summary>
        public static SoundHandle PlaySFX(this GameObject gameObject, SoundEffect sfx)
        {
            if (sfx == null) return null;
            return sfx.Play(gameObject.transform, AudioAttachmentType.Direct);
        }

        /// <summary>
        /// Plays a SoundEffect on this Transform using Direct attachment.
        /// </summary>
        public static SoundHandle PlaySFX(this Transform transform, SoundEffect sfx)
        {
            if (sfx == null) return null;
            return sfx.Play(transform, AudioAttachmentType.Direct);
        }

        /// <summary>
        /// Plays a SoundEffect on this GameObject with a specified attachment type.
        /// </summary>
        public static SoundHandle PlaySFX(this GameObject gameObject, SoundEffect sfx, AudioAttachmentType attachType)
        {
            if (sfx == null) return null;
            return sfx.Play(gameObject.transform, attachType);
        }

        /// <summary>
        /// Plays a SoundEffect on this Transform with a specified attachment type.
        /// </summary>
        public static SoundHandle PlaySFX(this Transform transform, SoundEffect sfx, AudioAttachmentType attachType)
        {
            if (sfx == null) return null;
            return sfx.Play(transform, attachType);
        }
    }
}