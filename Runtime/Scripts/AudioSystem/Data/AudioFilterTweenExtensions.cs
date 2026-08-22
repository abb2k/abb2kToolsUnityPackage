#if DOTWEEN
using DG.Tweening;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    public static class AudioFilterTweenExtensions
    {
        // NO DOKill() calls are used here, ensuring your other tweens remain safe.

        public static Tweener DODistortionLevel(this AudioSource source, float endValue, float duration)
        {
            var filter = source.GetComponent<AudioDistortionFilter>();
            if (filter == null) filter = source.gameObject.AddComponent<AudioDistortionFilter>();
            
            return DOTween.To(() => filter.distortionLevel, x => filter.distortionLevel = x, endValue, duration)
                .SetTarget(filter);
        }

        public static Tweener DOEchoDelay(this AudioSource source, float endValue, float duration)
        {
            var filter = source.GetComponent<AudioEchoFilter>();
            if (filter == null) filter = source.gameObject.AddComponent<AudioEchoFilter>();

            return DOTween.To(() => filter.delay, x => filter.delay = x, endValue, duration)
                .SetTarget(filter);
        }

        public static Tweener DOEchoDecayRatio(this AudioSource source, float endValue, float duration)
        {
            var filter = source.GetComponent<AudioEchoFilter>();
            if (filter == null) filter = source.gameObject.AddComponent<AudioEchoFilter>();

            return DOTween.To(() => filter.decayRatio, x => filter.decayRatio = x, endValue, duration)
                .SetTarget(filter);
        }
    }
}
#endif