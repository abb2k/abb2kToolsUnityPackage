using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [System.Serializable]
    public class SoundCodingSection
    {
        public string id;
        public string name = "New Section";
        public Color color;
        
        [Min(0)] public float startTime = 0f;
        [Min(0)] public float duration = 5f;

        [Header("Playback Flow")]
        [Tooltip("If true, this section loops continuously until a transition interrupts it.")]
        public bool loopSection = true;

        [Tooltip("If looping is false, should it automatically transition to another section or end the song?")]
        public bool isSongEnd = false;
        
        [Tooltip("The section to play automatically when this section finishes (if loop is false and it's not the song end).")]
        public string nextSectionId;

        public SoundCodingSection()
        {
            id = System.Guid.NewGuid().ToString();
            color = new Color(0.2f, 0.6f, 1f, 0.6f); 
        }
    }

    [System.Serializable]
    public class SoundCodingTransition
    {
        public string id;
        public string name = "New Transition";

        public string fromSectionId;
        public string toSectionId;
        
        [Tooltip("Optional bridge section to play between From and To")]
        public string bridgeSectionId; 
        
        [Min(0)] public float fadeOutDuration = 0.5f;
        [Min(0)] public float fadeInDuration = 0.5f;

        public SoundCodingTransition()
        {
            id = System.Guid.NewGuid().ToString();
        }
    }

    [System.Serializable]
    public class TransitionPoint
    {
        public string id;
        public string sectionId;
        
        [Tooltip("Time relative to the start of the section")]
        [Min(0)] public float timeOffset = 0f;
        
        public string targetTransitionId;

        public TransitionPoint()
        {
            id = System.Guid.NewGuid().ToString();
        }
    }

    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/SoundCoding.png")]
    [CreateAssetMenu(fileName = "SoundCoding", menuName = "Audio/Sound Coding")]
    public class SoundCoding : SoundModificationBase
    {
        [Header("Base Audio Input")]
        public SoundModificationBase inputSound;

        [Header("Playback Defaults")]
        [Tooltip("The section this song will automatically start playing from when .Play() is called.")]
        public string defaultSectionId;

        [SerializeField] private AudioFilterSettings filters;
        public override AudioFilterSettings Filters => filters;

        [HideInInspector] public List<SoundCodingSection> sections = new();
        [HideInInspector] public List<SoundCodingTransition> transitions = new();
        [HideInInspector] public List<TransitionPoint> transitionPoints = new();

        public override void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f)
        {
            if (inputSound == null) return;

            int startIndex = result.Count;
            inputSound.CollectPlayableClips(result, currentVolume, currentPitch, currentDelay);

            for (int i = startIndex; i < result.Count; i++)
            {
                var clip = result[i];

                if (clip.Filters == null) clip.Filters = new AudioFilterSettings();
                else clip.Filters = clip.Filters.Clone();

                clip.Filters.MergeWithParent(this.filters);
                result[i] = clip;
            }
        }

        public SoundCodingSection GetSection(string id) => sections.Find(s => s.id == id);
        public SoundCodingTransition GetTransition(string id) => transitions.Find(t => t.id == id);
    }
}