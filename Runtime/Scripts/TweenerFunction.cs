#if DOTWEEN
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using UnityEngine;

namespace Abb2kTools
{
    [System.Serializable]
    public class TweenerFunction
    {
        public GameObject targetObject;
        public Component targetComponent;
        public string methodName;
        public string methodKey;
        public List<ParameterData> parameters = new List<ParameterData>();

        [System.Serializable]
        public class ParameterData
        {
            public string name;
            public string typeName;

            [SerializeReference] 
            public ParameterValue value;

            public object GetValue()
            {
                if (value == null) return null;
                var field = value.GetType().GetField("data");
                return field?.GetValue(value);
            }
        }

        [System.Serializable] public abstract class ParameterValue { }
        
        [System.Serializable]
        public class ParameterValue<T> : ParameterValue
        {
            public T data;
            public bool hasValue = true;

            public ParameterValue(object val)
            {
                if (val == null || val == System.DBNull.Value)
                {
                    hasValue = false;
                    data = default;
                }
                else
                {
                    hasValue = true;
                    data = (T)val;
                }
            }

            public object GetValue() => hasValue ? data : null;
        }

        public Tween Call(float duration)
        {
            if (targetObject == null || targetComponent == null || string.IsNullOrEmpty(methodKey)) return null;

            var method = GetValidMethods(targetComponent.GetType())
                .FirstOrDefault(m => GetMethodKey(m) == methodKey);

            if (method == null) return null;

            bool isExt = method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
            var pInfos = method.GetParameters();
            object[] args = new object[pInfos.Length];

            int dataIndex = 0;
            for (int i = 0; i < pInfos.Length; i++)
            {
                if (isExt && i == 0) { args[i] = targetComponent; continue; }
                
                string pName = pInfos[i].Name.ToLower();

                if (pName.Contains("duration"))
                {
                    args[i] = duration;
                }
                else if (pName == "target" || pName == "t")
                {
                    args[i] = targetComponent;
                }
                else
                {
                    if (dataIndex < parameters.Count)
                    {
                        args[i] = parameters[dataIndex].GetValue();
                        dataIndex++;
                    }
                }
            }

            return method.Invoke(isExt ? null : targetComponent, args) as Tween;
        }

        public static string GetMethodKey(MethodInfo m)
        {
            var str = $"{m.Name}";

            var parameters = m.GetParameters();

            if (parameters == null || parameters.Length == 0) return str;

            str += " (";

            bool isExt = m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);

            int i = 0;

            const string seperator = ", ";
            
            foreach (var parameter in parameters)
            {
                if (isExt && i == 0 || parameter.Name.ToLower().Contains("duration"))
                {
                    i++;
                    if (i == parameters.Length)
                        str = str.Remove(str.Length - seperator.Length, seperator.Length);
                    continue;
                }

                var underlayType = System.Nullable.GetUnderlyingType(parameter.ParameterType);
                str += (underlayType ?? parameter.ParameterType).Name;
                if (underlayType != null)
                    str += "?";

                if (i != parameters.Length - 1)
                    str += seperator;

                i++;
            }

            str += ")";

            return str;
        }

        public static IEnumerable<MethodInfo> GetValidMethods(System.Type type)
        {
            var instance = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => typeof(Tween).IsAssignableFrom(m.ReturnType));

            var extensions = typeof(ShortcutExtensions).Assembly.GetTypes()
                .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType.IsAssignableFrom(type))
                .Where(m => typeof(Tween).IsAssignableFrom(m.ReturnType));

            return instance.Concat(extensions);
        }
    }
}
#endif