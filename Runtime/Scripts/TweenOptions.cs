#if DOTWEEN
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Core.Enums;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
#endif
using UnityEngine;
using UnityEngine.Events;


namespace Abb2kTools
{
#if ODIN_INSPECTOR
    [InlineProperty, HideLabel]
#endif
    public abstract class TweenOptions
    {
        protected virtual string ExtraStr => null;
#if ODIN_INSPECTOR

        protected const string GENERAL_DISPLAY_GROUP_NAME = "@GetDisplayName($property.ParentValueProperty)";
        string GetDisplayName(InspectorProperty property)
        {
            string extra = "";
            if (ExtraStr != null)
            {
                extra = " (" + ExtraStr + ")";
            }

            return property.NiceName + extra;
        }
        protected const string GENERAL_GROUP_NAME = "general";
        protected const string OPTIONS_GROUP_NAME = "Options";
        protected const string EVENTS_GROUP_NAME = "Events";
#endif
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Header("General")]
        public float duration = 0;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        public bool useCurve = false;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME),
            DisableIf("useCurve")
        ]
#endif
        public Ease easing = Ease.Linear;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME),
            EnableIf("useCurve")
        ]
#endif
        public AnimationCurve easingCurve = AnimationCurve.Constant(0, 1, .5f);
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        public bool isRelative = false;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Space]
        public bool isInverted = false;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Space]
        public bool isIgnoringTimescale = false;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Space]
        public SpecialStartupMode specialStartupMode = SpecialStartupMode.None;

#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        [Header("Loop")]
        public bool isInfiniteLoop = false;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME),
            DisableIf("isInfiniteLoop"),
            MinValue(1)
        ]
#else
    [Min(1)]
#endif
        public int loops = 1;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME)
        ]
#endif
        public LoopType loopType = LoopType.Restart;

#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME),
            HorizontalGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME + "/" + "SE")
        ]
#endif
        public UnityEvent OnStart;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME)
        ]
#endif
        public UnityEvent OnUpdate;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME)
        ]
#endif
        public UnityEvent OnKill;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME),
            HorizontalGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME + "/" + "SE")
        ]
#endif
        public UnityEvent OnComplete;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME),
            HorizontalGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME + "/" + "PP")
        ]
#endif
        public UnityEvent OnPlay;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME),
            HorizontalGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME + "/" + "PP")
        ]
#endif
        public UnityEvent OnPause;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME)
        ]
#endif
        public UnityEvent OnStepComplete;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME)
        ]
#endif
        public UnityEvent OnRewind;
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + EVENTS_GROUP_NAME)
        ]
#endif
        public UnityEvent<int> OnWaypointChange;

        public abstract Tween Invoke();

        protected Tween Setup(Tween t)
        {
            if (t == null) return null;

            t.SetLoops(isInfiniteLoop ? -1 : loops, loopType)
                .SetInverted(isInverted)
                .SetUpdate(isIgnoringTimescale)
                .SetRelative(isRelative)
                .OnComplete(() => OnComplete.Invoke())
                .OnKill(() => OnKill.Invoke())
                .OnStepComplete(() => OnStepComplete.Invoke())
                .OnStart(() => OnStart.Invoke())
                .OnUpdate(() => OnUpdate.Invoke())
                .OnPlay(() => OnPlay.Invoke())
                .OnPause(() => OnPause.Invoke())
                .OnWaypointChange(num => OnWaypointChange.Invoke(num))
                .SetSpecialStartupMode(specialStartupMode);

            if (useCurve)
                t.SetEase(easingCurve);
            else
                t.SetEase(easing);

            return t;
        }
    }
}
#endif