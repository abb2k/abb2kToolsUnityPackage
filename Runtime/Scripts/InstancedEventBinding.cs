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
        [SerializeField]
        public string eventTypeAssemblyQualifiedName;
        [SerializeField]
        public UnityEngine.Object targetObject;
        public string methodName;
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

            Delegate callback = Delegate.CreateDelegate(delegateType, targetObject, targetMethod);

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