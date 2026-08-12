using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class Sound : SoundBase
    {
        [Header("Persistent Options")]
        public string soundID;
        public bool loop = true;

        /// <summary>
        /// Retrieves (or creates if missing) the persistent handle for this sound.
        /// </summary>
        public SoundHandle GetHandle(Transform attachedTransform = null, AudioAttachmentType attachType = AudioAttachmentType.Direct)
        {
            return SoundManager.Instance.GetOrCreatePersistentSound(this, attachedTransform, attachType);
        }

        /// <summary>
        /// Plays this sound if an active handle exists.
        /// </summary>
        public void Play()
        {
            var handle = GetActiveHandle();
            handle?.Play();
        }

        /// <summary>
        /// Pauses this sound if an active handle exists.
        /// </summary>
        public void Pause()
        {
            var handle = GetActiveHandle();
            handle?.Pause();
        }

        /// <summary>
        /// Resumes this sound if an active handle exists.
        /// </summary>
        public void Resume()
        {
            var handle = GetActiveHandle();
            handle?.Resume();
        }

        /// <summary>
        /// Stops this sound if an active handle exists.
        /// </summary>
        public void Stop()
        {
            var handle = GetActiveHandle();
            handle?.Stop();
        }

        /// <summary>
        /// Forcefully destroys this sound's handle, cleaning up all audio sources and holders.
        /// </summary>
        public void Destroy()
        {
            var handle = GetActiveHandle();
            handle?.DestroyHandle();
        }

        /// <summary>
        /// Helper to quickly query the SoundManager for an existing handle via soundID without spawning a new one.
        /// </summary>
        private SoundHandle GetActiveHandle()
        {
            if (string.IsNullOrEmpty(soundID)) return null;
            return SoundManager.Instance.GetPersistentSound(soundID);
        }
    }
}