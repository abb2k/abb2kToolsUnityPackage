#if DOTWEEN
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Core.Enums;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace Abb2kTools
{
    public enum TweenSequanceElementAddType
    {
        Append,
        AppendCallback,
        AppendInterval,
        Join,
        JoinCallback,
        Prepend,
        PrependCallback,
        PrependInterval
    }

    [System.Serializable]
    public struct TweenSequanceElement
    {
        public TweenSequanceElementAddType type;
#if ODIN_INSPECTOR
        [ShowIf("@type == TweenSequanceElementAddType.AppendInterval || type == TweenSequanceElementAddType.PrependInterval")]
#endif
        public float interval;
#if ODIN_INSPECTOR
        [ShowIf("@type == TweenSequanceElementAddType.AppendCallback || type == TweenSequanceElementAddType.PrependCallback || type == TweenSequanceElementAddType.JoinCallback")]
#endif
        public UnityEvent callback;
#if ODIN_INSPECTOR
        [ShowIf("@type == TweenSequanceElementAddType.Append || type == TweenSequanceElementAddType.Prepend || type == TweenSequanceElementAddType.Join")]
#endif
        public TweenFunctionOptions tween;
    }

    [System.Serializable]
    public class TweenSequanceOptions : TweenOptions
    {
        #if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            BoxGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Header("Sequance")]
        public TweenSequanceElement[] sequance;

        protected override string ExtraStr => $"Sequance--{(sequance.Length == 0 ? "No elements" : $"{sequance.Length} elements")}";

        public override Tween Run()
        {
            var seq = DOTween.Sequence();

            foreach (var element in sequance)
            {
                switch (element.type)
                {
                    case TweenSequanceElementAddType.Append:
                        seq.Append(element.tween.Run());
                        break;
                    case TweenSequanceElementAddType.AppendCallback:
                        seq.AppendCallback(() => element.callback.Invoke());
                        break;
                    case TweenSequanceElementAddType.AppendInterval:
                        seq.AppendInterval(element.interval);
                        break;
                    case TweenSequanceElementAddType.Join:
                        seq.Join(element.tween.Run());
                        break;
                    case TweenSequanceElementAddType.JoinCallback:
                        seq.JoinCallback(() => element.callback.Invoke());
                        break;
                    case TweenSequanceElementAddType.Prepend:
                        seq.Prepend(element.tween.Run());
                        break;
                    case TweenSequanceElementAddType.PrependCallback:
                        seq.PrependCallback(() => element.callback.Invoke());
                        break;
                    case TweenSequanceElementAddType.PrependInterval:
                        seq.PrependInterval(element.interval);
                        break;
                }
            }

            return Setup(seq);
        }
    }
}
#endif