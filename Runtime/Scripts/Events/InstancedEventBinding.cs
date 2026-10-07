using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Abb2kTools.Events
{
    [Serializable]
    public class InstancedEventBinding
    {
        [SerializeField]
        public string eventTypeAssemblyQualifiedName;
        [SerializeField]
        public UnityEngine.Object targetObject;
        public string methodName;
        [SerializeField]
        public ListenerResult voidMethodResult = ListenerResult.Propagate;
        [SerializeField]
        public int priority = 0;
        [SerializeField]
        public bool autoBindToHolder = true;
        [SerializeField]
        public bool activeInEditor = false;

        [SerializeField] 
        public ListenerHandle activeHandle;

        [SerializeField]
        private MonoBehaviour holder;

        public void Uninitialize()
        {
            if (activeHandle != null)
            {
                activeHandle.SetEnabled(false);
                activeHandle.Destroy();
                activeHandle = null;
            }
        }

        public ListenerHandle Initialize(MonoBehaviour holder)
        {
            Uninitialize();

            if (string.IsNullOrEmpty(eventTypeAssemblyQualifiedName) || targetObject == null || string.IsNullOrEmpty(methodName))
            {
                Debug.LogWarning("SerializedEventBinding is incomplete. Cannot initialize listener.");
                return null;
            }

            Type eventType = Type.GetType(eventTypeAssemblyQualifiedName);
            if (eventType == null) 
            {
                Debug.LogError($"Could not find type: {eventTypeAssemblyQualifiedName}");
                return null;
            }

            MethodInfo listenMethod = eventType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(m => m.Name == "Listen");

            if (listenMethod == null) 
            {
                Debug.LogError($"Could not find static 'Listen' method on {eventType.Name}.");
                return null;
            }

            Type delegateType = listenMethod.GetParameters()[0].ParameterType;
            
            MethodInfo delegateSignature = delegateType.GetMethod("Invoke");
            ParameterInfo[] delegateParams = delegateSignature.GetParameters();
            Type[] requiredParamTypes = delegateParams.Select(p => p.ParameterType).ToArray();

            Type targetType = targetObject.GetType();
            BindingFlags targetMethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo targetMethod = targetType.GetMethod(
                methodName, 
                targetMethodFlags,
                null,
                requiredParamTypes,
                null
            );
            bool ignoresEventParameters = false;

            if (targetMethod == null && requiredParamTypes.Length > 0)
            {
                MethodInfo parameterlessMethod = targetType.GetMethod(
                    methodName,
                    targetMethodFlags,
                    null,
                    Type.EmptyTypes,
                    null);

                if (CanIgnoreEventParameters(parameterlessMethod))
                {
                    targetMethod = parameterlessMethod;
                    ignoresEventParameters = true;
                }
            }

            if (targetMethod == null) 
            {
                Debug.LogError($"Could not find method '{methodName}' on '{targetObject.name}' that matches the required signature of {delegateType.Name}.");
                return null;
            }

            Delegate callback = targetMethod.ReturnType == typeof(void)
                ? CreateVoidCallback(targetObject, targetMethod, requiredParamTypes, ignoresEventParameters)
                : Delegate.CreateDelegate(delegateType, targetObject, targetMethod);

            activeHandle = (ListenerHandle)listenMethod.Invoke(null, new object[] { callback, priority });
            
            if (activeHandle != null)
            {
                activeHandle.activeInEditor = this.activeInEditor;
            }

            if (autoBindToHolder && holder != null)
            {
                activeHandle.BindTo(holder);
            }

            if (!Application.isPlaying)
            {
                this.holder = holder;
            }

            activeHandle.onRestored.RemoveListener(InitRestore);
            activeHandle.onRestored.AddListener(InitRestore);

            return activeHandle;
        }

        private Delegate CreateVoidCallback(UnityEngine.Object target, MethodInfo targetMethod, Type[] eventParameterTypes, bool ignoresEventParameters)
        {
            Type[] parameterTypes = targetMethod.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Type actionType;
            switch (parameterTypes.Length)
            {
                case 0:
                    actionType = typeof(Action);
                    break;
                case 1:
                    actionType = typeof(Action<>).MakeGenericType(parameterTypes);
                    break;
                case 2:
                    actionType = typeof(Action<,>).MakeGenericType(parameterTypes);
                    break;
                case 3:
                    actionType = typeof(Action<,,>).MakeGenericType(parameterTypes);
                    break;
                case 4:
                    actionType = typeof(Action<,,,>).MakeGenericType(parameterTypes);
                    break;
                default:
                    throw new InvalidOperationException("Void event callbacks support up to four parameters.");
            }

            Delegate voidCallback = Delegate.CreateDelegate(actionType, target, targetMethod);
            string adapterName = ignoresEventParameters
                ? $"WrapVoidNoArgs{eventParameterTypes.Length}"
                : $"WrapVoid{eventParameterTypes.Length}";
            MethodInfo adapterMethod = typeof(InstancedEventBinding).GetMethod(
                adapterName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (eventParameterTypes.Length > 0)
            {
                adapterMethod = adapterMethod.MakeGenericMethod(eventParameterTypes);
            }

            return (Delegate)adapterMethod.Invoke(null, new object[] { voidCallback, voidMethodResult });
        }

        private static bool CanIgnoreEventParameters(MethodInfo method)
        {
            return method != null
                && method.ReturnType == typeof(void)
                && method.GetParameters().Length == 0
                && method.DeclaringType.Assembly != typeof(MonoBehaviour).Assembly;
        }

        private static Func<ListenerResult> WrapVoid0(Action callback, ListenerResult result)
        {
            return () =>
            {
                callback();
                return result;
            };
        }

        private static Func<T1, ListenerResult> WrapVoid1<T1>(Action<T1> callback, ListenerResult result)
        {
            return parameter1 =>
            {
                callback(parameter1);
                return result;
            };
        }

        private static Func<T1, T2, ListenerResult> WrapVoid2<T1, T2>(Action<T1, T2> callback, ListenerResult result)
        {
            return (parameter1, parameter2) =>
            {
                callback(parameter1, parameter2);
                return result;
            };
        }

        private static Func<T1, T2, T3, ListenerResult> WrapVoid3<T1, T2, T3>(Action<T1, T2, T3> callback, ListenerResult result)
        {
            return (parameter1, parameter2, parameter3) =>
            {
                callback(parameter1, parameter2, parameter3);
                return result;
            };
        }

        private static Func<T1, T2, T3, T4, ListenerResult> WrapVoid4<T1, T2, T3, T4>(Action<T1, T2, T3, T4> callback, ListenerResult result)
        {
            return (parameter1, parameter2, parameter3, parameter4) =>
            {
                callback(parameter1, parameter2, parameter3, parameter4);
                return result;
            };
        }

        private static Func<T1, ListenerResult> WrapVoidNoArgs1<T1>(Action callback, ListenerResult result)
        {
            return _ =>
            {
                callback();
                return result;
            };
        }

        private static Func<T1, T2, ListenerResult> WrapVoidNoArgs2<T1, T2>(Action callback, ListenerResult result)
        {
            return (_, _) =>
            {
                callback();
                return result;
            };
        }

        private static Func<T1, T2, T3, ListenerResult> WrapVoidNoArgs3<T1, T2, T3>(Action callback, ListenerResult result)
        {
            return (_, _, _) =>
            {
                callback();
                return result;
            };
        }

        private static Func<T1, T2, T3, T4, ListenerResult> WrapVoidNoArgs4<T1, T2, T3, T4>(Action callback, ListenerResult result)
        {
            return (_, _, _, _) =>
            {
                callback();
                return result;
            };
        }

        void InitRestore()
        {
            Initialize(holder);
        }

        ~InstancedEventBinding()
        {
            Uninitialize();
        }
    }
}