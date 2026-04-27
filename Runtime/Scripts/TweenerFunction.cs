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

        private static readonly Dictionary<System.Type, MethodInfo[]> s_validMethodsCache = new Dictionary<System.Type, MethodInfo[]>();
        private static readonly Dictionary<(System.Type type, string key), MethodInfo> s_methodCache = new Dictionary<(System.Type type, string key), MethodInfo>();
        private static readonly Dictionary<MethodInfo, System.Func<object, object[], object>> s_methodInvokerCache = new Dictionary<MethodInfo, System.Func<object, object[], object>>();
        private static MethodInfo[] s_cachedTweenExtensionMethods;

        public Tween Call(float duration)
        {
            if (targetObject == null || targetComponent == null || string.IsNullOrEmpty(methodKey)) return null;

            var type = targetComponent.GetType();
            var method = GetValidMethod(type, methodKey);
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

            var invoker = GetMethodInvoker(method);
            return invoker(isExt ? null : targetComponent, args) as Tween;
        }

        public static string GetMethodKey(MethodInfo m)
        {
            var str = $"{m.Name}";

            var parameters = m.GetParameters();

            if (parameters == null || parameters.Length == 0) return str;
            bool isExt = m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
            if (isExt && parameters.Length == 1) return str;

            str += " (";

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
        
        private static IEnumerable<System.Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Enumerable.Empty<System.Type>();
            }
        }

        private static IEnumerable<MethodInfo> GetMethodsSafe(System.Type type, BindingFlags flags)
        {
            try
            {
                return type.GetMethods(flags);
            }
            catch
            {
                return Enumerable.Empty<MethodInfo>();
            }
        }

        public static IEnumerable<MethodInfo> GetValidMethods(System.Type type)
        {
            return GetValidMethodsInternal(type);
        }

        private static MethodInfo GetValidMethod(System.Type type, string methodKey)
        {
            var cacheKey = (type, methodKey);
            if (s_methodCache.TryGetValue(cacheKey, out var cachedMethod))
                return cachedMethod;

            var method = GetValidMethodsInternal(type).FirstOrDefault(m => GetMethodKey(m) == methodKey);
            if (method != null)
                s_methodCache[cacheKey] = method;

            return method;
        }

        private static MethodInfo[] GetValidMethodsInternal(System.Type type)
        {
            if (s_validMethodsCache.TryGetValue(type, out var methods))
                return methods;

            var instance = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => typeof(Tween).IsAssignableFrom(m.ReturnType));

            var extensionMethods = GetCachedTweenExtensionMethods()
                .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType.IsAssignableFrom(type));

            methods = instance.Concat(extensionMethods).ToArray();
            s_validMethodsCache[type] = methods;
            return methods;
        }

        private static MethodInfo[] GetCachedTweenExtensionMethods()
        {
            if (s_cachedTweenExtensionMethods != null)
                return s_cachedTweenExtensionMethods;

            s_cachedTweenExtensionMethods = System.AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(GetTypesSafe)
                .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested)
                .SelectMany(t => GetMethodsSafe(t, BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                .Where(m => m.GetParameters().Length > 0)
                .Where(m => typeof(Tween).IsAssignableFrom(m.ReturnType))
                .ToArray();

            return s_cachedTweenExtensionMethods;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PreloadExtensionMethods()
        {
            GetCachedTweenExtensionMethods();
        }

        public static void WarmupMethod(MethodInfo method)
        {
            if (method == null) return;
            GetCachedTweenExtensionMethods();
            GetMethodInvoker(method);
        }

        private static System.Func<object, object[], object> GetMethodInvoker(MethodInfo method)
        {
            if (s_methodInvokerCache.TryGetValue(method, out var invoker))
                return invoker;

            var instanceParameter = System.Linq.Expressions.Expression.Parameter(typeof(object), "target");
            var argumentsParameter = System.Linq.Expressions.Expression.Parameter(typeof(object[]), "args");
            var parameters = method.GetParameters();
            var callArguments = new System.Linq.Expressions.Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                var argumentAccess = System.Linq.Expressions.Expression.ArrayIndex(argumentsParameter, System.Linq.Expressions.Expression.Constant(i));
                callArguments[i] = System.Linq.Expressions.Expression.Convert(argumentAccess, parameterType);
            }

            var instanceCast = method.IsStatic ? null : System.Linq.Expressions.Expression.Convert(instanceParameter, method.DeclaringType);
            var call = method.IsStatic
                ? System.Linq.Expressions.Expression.Call(method, callArguments)
                : System.Linq.Expressions.Expression.Call(instanceCast, method, callArguments);

            System.Linq.Expressions.Expression body;
            if (method.ReturnType == typeof(void))
            {
                body = System.Linq.Expressions.Expression.Block(call, System.Linq.Expressions.Expression.Constant(null, typeof(object)));
            }
            else
            {
                body = System.Linq.Expressions.Expression.Convert(call, typeof(object));
            }

            invoker = System.Linq.Expressions.Expression.Lambda<System.Func<object, object[], object>>(body, instanceParameter, argumentsParameter).Compile();
            s_methodInvokerCache[method] = invoker;
            return invoker;
        }
    }
}
#endif