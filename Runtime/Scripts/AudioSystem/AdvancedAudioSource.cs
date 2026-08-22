using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/AdvancedAudioSource.png")]
    [AddComponentMenu("Abb2kTools/Audio/Advanced Audio Source")]
    public class AdvancedAudioSource : MonoBehaviour
    {
        public Sound sound = new Sound();
        public bool playOnAwake = true;

        private SoundHandle _currentHandle;

        public SoundHandle ActiveHandle => _currentHandle;
        
        public SoundModificationBase SoundAsset 
        { 
            get => sound.sound; 
            set => sound.sound = value; 
        }
        
        public string SoundID 
        { 
            get => sound.soundID; 
            set => sound.soundID = value; 
        }
        
        public bool Loop 
        { 
            get => sound.loop; 
            set => sound.loop = value; 
        }

        public SoundBase.MixerGroupPreference MixerPreference 
        { 
            get => sound.mixerGroupPreference; 
            set => sound.mixerGroupPreference = value; 
        }
        
        public AudioMixerGroup SpecificMixerGroup 
        { 
            get => sound.specificMixerGroup; 
            set => sound.specificMixerGroup = value; 
        }

        public float Volume 
        { 
            get => sound.volume; 
            set => sound.volume = value; 
        }
        
        public float Pitch 
        { 
            get => sound.pitch; 
            set => sound.pitch = value; 
        }
        
        public float PanStereo 
        { 
            get => sound.panStereo; 
            set => sound.panStereo = value; 
        }
        
        public float SpatialBlend 
        { 
            get => sound.spatialBlend; 
            set => sound.spatialBlend = value; 
        }
        
        public float ReverbZoneMix 
        { 
            get => sound.reverbZoneMix; 
            set => sound.reverbZoneMix = value; 
        }
        
        public int Priority 
        { 
            get => sound.prio; 
            set => sound.prio = value; 
        }

        public float DopplerLevel 
        { 
            get => sound.dopplerLevel; 
            set => sound.dopplerLevel = value; 
        }
        
        public float Spread 
        { 
            get => sound.spread; 
            set => sound.spread = value; 
        }
        
        public AudioRolloffMode Rolloff 
        { 
            get => sound.rolloff; 
            set => sound.rolloff = value; 
        }
        
        public float MinDistance 
        { 
            get => sound.minDist; 
            set => sound.minDist = value; 
        }
        
        public float MaxDistance 
        { 
            get => sound.maxDist; 
            set => sound.maxDist = value; 
        }

        private void Reset()
        {
            if (sound == null) sound = new Sound();
            GenerateUniqueID();
        }

        private void Awake()
        {
            GenerateUniqueID();
        }

        private void Start()
        {
            if (playOnAwake)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (_currentHandle != null)
            {
                _currentHandle.DestroyHandle();
                _currentHandle = null;
            }
        }

        private void GenerateUniqueID()
        {
            if (sound != null && string.IsNullOrEmpty(sound.soundID))
            {
                sound.soundID = "SND_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            }
        }

        public void Play()
        {
            if (sound == null || sound.sound == null)
            {
                Debug.LogWarning($"[AdvancedAudioSource] Cannot play. No Sound assigned on {gameObject.name}.");
                return;
            }

            if (_currentHandle == null)
            {
                _currentHandle = sound.GetHandle(transform, AudioAttachmentType.Direct);
            }

            if (_currentHandle != null)
            {
                _currentHandle.Play();
            }
        }

        public void Pause()
        {
            if (_currentHandle != null) _currentHandle.Pause();
        }

        public void Resume()
        {
            if (_currentHandle != null) _currentHandle.Resume();
        }

        public void Stop()
        {
            if (_currentHandle != null) _currentHandle.Stop();
        }

        public void Reinitialize()
        {
            if (_currentHandle != null)
            {
                _currentHandle.DestroyHandle();
                _currentHandle = null;
            }
        }


        private void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "AudioSource Icon", true);
        }

        private void OnDrawGizmosSelected()
        {
            if (sound == null) return;

            if (sound.spatialBlend > 0f)
            {
                Color minColor = new Color(0.5f, 0.7f, 1f, 1f); 
                Color maxColor = new Color(0.3f, 0.5f, 0.8f, 0.5f); 

                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

                Gizmos.color = minColor;
                Gizmos.DrawWireSphere(Vector3.zero, sound.minDist);

                Gizmos.color = maxColor;
                Gizmos.DrawWireSphere(Vector3.zero, sound.maxDist);

                Gizmos.matrix = oldMatrix;
            }
        }
    }
}