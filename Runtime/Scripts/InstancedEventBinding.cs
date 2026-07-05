using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Abb2kTools
{
    [Serializable]
    public class InstancedEventBinding
    {
        // Data saved by the Unity Inspector
        public string eventTypeAssemblyQualifiedName;
        public UnityEngine.Object targetObject;
        public string methodName;
        public int priority = 0;
        public bool autoBindToHolder = true;
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
            // NEW: Ensure we clear any old listener before creating a new one
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

            // 1. Find the Listen method.
            // FlattenHierarchy is REQUIRED here. Because Listen is a static method on the 
            // base generic class (InstancedEventBase), reflecting on the child class won't see it otherwise.
            MethodInfo listenMethod = eventType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(m => m.Name == "Listen");

            if (listenMethod == null) 
            {
                Debug.LogError($"Could not find static 'Listen' method on {eventType.Name}.");
                return null;
            }

            // Extract the required delegate type (e.g., Func<ListenerResult> or Func<T1, ListenerResult>)
            Type delegateType = listenMethod.GetParameters()[0].ParameterType;
            
            // 2. Safely find the target method.
            // We extract the exact parameter types the delegate requires (T1, T2, etc.) so we can 
            // find the exact matching method on the target object, avoiding AmbiguousMatchExceptions.
            MethodInfo delegateSignature = delegateType.GetMethod("Invoke");
            ParameterInfo[] delegateParams = delegateSignature.GetParameters();
            Type[] requiredParamTypes = delegateParams.Select(p => p.ParameterType).ToArray();

            MethodInfo targetMethod = targetObject.GetType().GetMethod(
                methodName, 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                requiredParamTypes,
                null
            );

            if (targetMethod == null) 
            {
                Debug.LogError($"Could not find method '{methodName}' on '{targetObject.name}' that matches the required signature of {delegateType.Name}.");
                return null;
            }

            // Create the delegate linked to the target object and method
            Delegate callback = Delegate.CreateDelegate(delegateType, targetObject, targetMethod);

            // Invoke the Listen method via reflection
            activeHandle = (ListenerHandle)listenMethod.Invoke(null, new object[] { callback, priority });
            
            // <--- NEW: Pass the editor state down to the handle
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

        void InitRestore()
        {
            Initialize(holder);
        }
    }
}