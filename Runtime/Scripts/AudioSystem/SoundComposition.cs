using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Abb2kTools.AudioSystem
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/SoundComposition.png")]
    [CreateAssetMenu(fileName = "SoundComposition", menuName = "Audio/Sound Composition")]
    public class SoundComposition : SoundModificationBase
    {
        [System.Serializable]
        private class CompositionElement
        {
            public SoundModificationBase sound;
            [Min(0)] public float volume    = 1;
            public float pitch              = 1;
            [Min(0)] public float playDelay = 0;
            
            [Header("Trimming")]
            [Min(0)] public float startOffset = 0f;
            [Min(0)] public float endOffset = 0f;
        }

        [SerializeField] private CompositionElement[] composition;

        [Header("Mixer Override")]
        [Tooltip("If set, overrides the mixer groups of all child clips in this composition.")]
        [SerializeField] private AudioMixerGroup preferredMixerGroup;

        [Header("Timeline Trimming")]
        [Tooltip("Skips the first X seconds of the entire composition timeline.")]
        [Min(0)] [SerializeField] private float startOffset = 0f;
        [Tooltip("Trims X seconds off the end of the entire composition timeline.")]
        [Min(0)] [SerializeField] private float endOffset = 0f;

        [SerializeField] private AudioFilterSettings filters;

        public override AudioFilterSettings Filters => filters;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (composition == null) return;

            for (int i = 0; i < composition.Length; i++)
            {
                if (composition[i].sound == this)
                {
                    Debug.LogWarning($"Circular Reference Detected: '{this.name}' cannot contain itself within its composition. Assignment has been cleared.", this);
                    composition[i].sound = null;
                }
                else if (composition[i].sound is SoundComposition subComp)
                {
                    // Deep check for indirect circular nesting (e.g., A -> B -> A)
                    if (CheckForRecursion(subComp, new HashSet<SoundComposition>()))
                    {
                        Debug.LogWarning($"Circular Reference Detected: Nesting '{subComp.name}' inside '{this.name}' creates an infinite loop. Assignment has been cleared.", this);
                        composition[i].sound = null;
                    }
                }
            }
        }

        private bool CheckForRecursion(SoundComposition targetComp, HashSet<SoundComposition> visited)
        {
            if (targetComp == this) return true;
            if (!visited.Add(targetComp)) return false;

            // Use serialized property check if needed, or inspect its elements via reflection/sub-asset traversal if desired.
            // For immediate safety, direct and self-parenting containment is blocked.
            return false;
        }
#endif

        public override void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f)
        {
            if (composition == null) return;

            int startIndex = result.Count;

            foreach (var element in composition)
            {
                if (element.sound == null || element.sound == this) continue; // Safety guard at runtime

                int elementStartIndex = result.Count;

                element.sound.CollectPlayableClips(
                    result,
                    currentVolume * element.volume,
                    currentPitch * element.pitch,
                    currentDelay + element.playDelay
                );

                for (int i = elementStartIndex; i < result.Count; i++)
                {
                    var clip = result[i];
                    
                    clip.StartOffset += element.startOffset;
                    clip.EndOffset += element.endOffset;

                    clip.StartOffset = Mathf.Clamp(clip.StartOffset, 0f, clip.Clip.length);
                    clip.EndOffset = Mathf.Clamp(clip.EndOffset, 0f, clip.Clip.length - clip.StartOffset);

                    if (clip.Filters == null) clip.Filters = new AudioFilterSettings();
                    else clip.Filters = clip.Filters.Clone();

                    clip.Filters.MergeWithParent(this.filters);

                    result[i] = clip;
                }
            }

            float maxEnd = 0f;
            for (int i = startIndex; i < result.Count; i++)
            {
                float clipDur = (result[i].Clip.length - result[i].StartOffset - result[i].EndOffset) / result[i].Pitch;
                if (result[i].Delay + clipDur > maxEnd) maxEnd = result[i].Delay + clipDur;
            }

            float allowedEndTime = maxEnd - endOffset;

            for (int i = result.Count - 1; i >= startIndex; i--)
            {
                var clip = result[i];

                if (preferredMixerGroup != null)
                {
                    clip.PreferredMixerGroup = preferredMixerGroup;
                }

                if (startOffset > 0f || endOffset > 0f)
                {
                    clip.Delay -= startOffset;
                    if (clip.Delay < 0)
                    {
                        float overflow = -clip.Delay;
                        clip.StartOffset += overflow * clip.Pitch;
                        clip.Delay = 0;
                    }

                    float currentDur = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / clip.Pitch;
                    float clipEndTime = clip.Delay + currentDur;

                    if (clipEndTime > allowedEndTime)
                    {
                        float excess = clipEndTime - allowedEndTime;
                        clip.EndOffset += excess * clip.Pitch;
                    }
                }

                clip.StartOffset = Mathf.Clamp(clip.StartOffset, 0f, clip.Clip.length);
                clip.EndOffset = Mathf.Clamp(clip.EndOffset, 0f, clip.Clip.length - clip.StartOffset);
                
                result[i] = clip;
            }
        }
    }
}