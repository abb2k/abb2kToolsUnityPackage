
#if DOTWEEN
using DG.Tweening;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Abb2kTools
{
    [System.Serializable]
    public class TweenFunctionOptions : TweenOptions
    {
#if ODIN_INSPECTOR
        [
            FoldoutGroup(groupName: GENERAL_GROUP_NAME, GroupName = GENERAL_DISPLAY_GROUP_NAME),
            FoldoutGroup(GENERAL_GROUP_NAME + "/" + OPTIONS_GROUP_NAME),
            PropertyOrder(-1)
        ]
#endif
        public TweenerFunction function;

        protected override string ExtraStr
        {
            get
            {
                if (function == null || function.targetObject == null) return null;

                string targetName = function.targetObject.name;

                if (function.targetComponent == null) return targetName;

                string compName = function.targetComponent.GetType().Name;

                if (string.IsNullOrEmpty(function.methodName)) return $"{targetName}->{compName}";

                return $"{targetName}->{compName}->{function.methodName}";
            }
        }

        public override Tween Run()
        {
            if (function == null) return null;
            return Setup(function.Call(duration));
        }
    }
}
#endif