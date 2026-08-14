namespace Abb2kTools.AudioSystem
{
    public struct SoundAudioInfo
    {
        public float TotalDuration;
        public float DurationWithTails;
        public int TotalClips;
        public bool ContainsFilters;
        public UnityEngine.Audio.AudioMixerGroup DominantMixerGroup;
    }
}