using System.Collections.Generic;
using UnityEngine;

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
            [Min(0)]
            public float volume    = 1;
            public float pitch     = 1;
            [Min(0)]
            public float playDelay = 0;
        }

        [SerializeField] private CompositionElement[] composition;

        public override void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f)
        {
            if (composition == null) return;

            foreach (var element in composition)
            {
                if (element.sound == null) continue;

                element.sound.CollectPlayableClips(
                    result,
                    currentVolume * element.volume,
                    currentPitch * element.pitch,
                    currentDelay + element.playDelay
                );
            }
        }
    }
}