using System;
using UnityEngine;
#if DOTWEEN
using DG.Tweening;
#endif

namespace Abb2kTools.AudioSystem
{
    public enum SoundParameterSource
    {
        Manual,
        ActiveSoundCount,
        DistanceToListener,
        EmitterSpeed,
        PlaybackTime,
        RigidbodySpeed,
        Rigidbody2DSpeed
    }

    public enum SoundParameterTarget
    {
        VolumeMultiplier,
        PitchMultiplier,
        PanStereo,
        SpatialBlend,
        ReverbZoneMix,
        DopplerLevel,
        Spread,
        MinDistance,
        MaxDistance
    }

    [CreateAssetMenu(fileName = "SoundParameter", menuName = "Audio/Sound Parameter")]
    public sealed class SoundParameter : ScriptableObject
    {
        [SerializeField, Tooltip("Manual values are set with SetValue. Active Sound Count tracks playing handles that reference this asset.")]
        private SoundParameterSource source;
        [SerializeField, Tooltip("Initial value used for manual parameters. Active Sound Count parameters start at zero.")]
        private float defaultValue;

        [NonSerialized] private float _currentValue;
        [NonSerialized] private bool _hasCurrentValue;

        public SoundParameterSource Source => source;
        public float CurrentValue => _hasCurrentValue ? _currentValue : defaultValue;

        private void OnEnable()
        {
            _currentValue = defaultValue;
            _hasCurrentValue = true;
        }

        public void SetValue(float value)
        {
            if (source != SoundParameterSource.Manual)
                throw new InvalidOperationException($"Sound Parameter '{name}' is driven by {source} and cannot be set manually.");

            SetCurrentValue(value);
            if (Application.isPlaying && SoundManager.TryGet(out SoundManager manager))
                manager.NotifyParameterChanged(this);
        }

#if DOTWEEN
        public Tween DOValue(float endValue, float duration, Ease ease = Ease.Linear)
        {
            if (source != SoundParameterSource.Manual)
                throw new InvalidOperationException($"Sound Parameter '{name}' is driven by {source} and cannot be tweened manually.");

            return DOTween.To(() => CurrentValue, SetValue, endValue, duration).SetEase(ease);
        }
#endif

        internal void SetActiveSoundCount(int count)
        {
            if (source != SoundParameterSource.ActiveSoundCount) return;
            SetCurrentValue(count);
        }

        private void SetCurrentValue(float value)
        {
            _currentValue = value;
            _hasCurrentValue = true;
        }
    }

    [Serializable]
    public sealed class SoundParameterBinding
    {
        public SoundParameter parameter;
        [Tooltip("Multiple bindings to the same target multiply their curve outputs.")]
        public SoundParameterTarget target;
        [Tooltip("Volume and pitch outputs are multipliers. Other targets use the curve output as their value.")]
        public AnimationCurve mapping = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [Min(0f), Tooltip("Time in seconds to blend from the current multiplier to the new value.")]
        public float transitionDuration = 0.1f;

        public float Evaluate()
        {
            return parameter == null ? 1f : Evaluate(parameter.CurrentValue);
        }

        public float Evaluate(float value)
        {
            if (mapping == null || mapping.length == 0) return 1f;

            Keyframe[] keys = mapping.keys;
            float clampedValue = Mathf.Clamp(value, keys[0].time, keys[keys.Length - 1].time);
            return mapping.Evaluate(clampedValue);
        }
    }
}
