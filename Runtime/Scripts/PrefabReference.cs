#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abb2kTools
{
    [System.Serializable]
    public abstract class PrefabReferenceBase<T> where T : Component
    {
        [SerializeField]
        private T _component;

        public T Component => _component;
        public GameObject GameObject => _component != null ? _component.gameObject : null;

        public bool IsValid()
        {
            return Component && GameObject;
        }

        public static implicit operator bool(PrefabReferenceBase<T> prefab)
        {
            return prefab.IsValid();
        }

        public UnityEngine.Object InstantiateObject(Vector3 position, Quaternion rotation) => UnityEngine.Object.Instantiate(GameObject, position, rotation);
        public UnityEngine.Object InstantiateObject(Vector3 position, Quaternion rotation, Transform parent) => UnityEngine.Object.Instantiate(GameObject, position, rotation, parent);
        public UnityEngine.Object InstantiateObject() => UnityEngine.Object.Instantiate(GameObject);
        public UnityEngine.Object InstantiateObject(Scene scene) => UnityEngine.Object.Instantiate(GameObject, scene);
        public UnityEngine.Object InstantiateObject(Transform parent) => UnityEngine.Object.Instantiate(GameObject, parent, false);
        public UnityEngine.Object InstantiateObject(Transform parent, bool instantiateInWorldSpace) => UnityEngine.Object.Instantiate(GameObject, parent, instantiateInWorldSpace);

        protected T InstantiateInternal(InstantiateParameters parameters) => UnityEngine.Object.Instantiate<T>(Component, parameters);
        protected T InstantiateInternal(Vector3 position, Quaternion rotation, InstantiateParameters parameters) => UnityEngine.Object.Instantiate<T>(Component, position, rotation, parameters);
        protected T InstantiateInternal() => UnityEngine.Object.Instantiate<T>(Component);
        protected T InstantiateInternal(Vector3 position, Quaternion rotation) => UnityEngine.Object.Instantiate<T>(Component, position, rotation);
        protected T InstantiateInternal(Vector3 position, Quaternion rotation, Transform parent) => UnityEngine.Object.Instantiate<T>(Component, position, rotation, parent);
        protected T InstantiateInternal(Transform parent) => UnityEngine.Object.Instantiate<T>(Component, parent, false);
        protected T InstantiateInternal(Transform parent, bool worldPositionStays) => UnityEngine.Object.Instantiate<T>(Component, parent, worldPositionStays);

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY! Instantiates a packed prefab as a raw Object
        /// </summary>
        public UnityEngine.Object InstantiatePrefabObject() => PrefabUtility.InstantiatePrefab(GameObject);
        public UnityEngine.Object InstantiatePrefabObject(Scene destinationScene) => PrefabUtility.InstantiatePrefab(GameObject, destinationScene);
        public UnityEngine.Object InstantiatePrefabObject(Transform parent) => PrefabUtility.InstantiatePrefab(GameObject, parent);

        /// <summary>
        /// EDITOR ONLY! Instantiates a packed prefab as type T
        /// </summary>
        protected T InstantiatePrefabInternal() => (PrefabUtility.InstantiatePrefab(GameObject) as GameObject)?.GetComponent<T>();
        protected T InstantiatePrefabInternal(Scene destinationScene) => (PrefabUtility.InstantiatePrefab(GameObject, destinationScene) as GameObject)?.GetComponent<T>();
        protected T InstantiatePrefabInternal(Transform parent) => (PrefabUtility.InstantiatePrefab(GameObject, parent) as GameObject)?.GetComponent<T>();
#endif
    }

    [System.Serializable]
    public class PrefabReference<T> : PrefabReferenceBase<T> where T : Component
    {
        // Cached reflection flags (Evaluated only once per type T)
        private static readonly bool RequiresDataInit;
        private static readonly bool HasParameterlessInit;
        private static readonly Type ExpectedDataType;

        static PrefabReference()
        {
            Type tType = typeof(T);
            HasParameterlessInit = typeof(IInitializable).IsAssignableFrom(tType);
            
            var genericInitInterface = tType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IInitializable<>));
            if (genericInitInterface != null)
            {
                RequiresDataInit = !HasParameterlessInit; // Only strictly require data if parameterless isn't also implemented
                ExpectedDataType = genericInitInterface.GetGenericArguments()[0];
            }
        }

        private T Initialize(T instance)
        {
            if (RequiresDataInit)
            {
                throw new InvalidOperationException($"[PrefabReference] '{typeof(T).Name}' requires data to initialize! You MUST use Instantiate<{ExpectedDataType.Name}>(data).");
            }

            if (HasParameterlessInit && instance is IInitializable initializable)
            {
                initializable.Init();
            }
                
            return instance;
        }

        private T Initialize<D>(T instance, D data)
        {
            if (instance is IInitializable<D> initializable)
            {
                initializable.Init(data);
            }
            else if (RequiresDataInit)
            {
                throw new InvalidCastException($"[PrefabReference] '{typeof(T).Name}' expects initialization data of type '{ExpectedDataType?.Name}', but you passed '{typeof(D).Name}'.");
            }
                
            return instance;
        }

        // --- Standard Initialization ---
        public T Instantiate() => Initialize(InstantiateInternal());
        public T Instantiate(Vector3 position, Quaternion rotation) => Initialize(InstantiateInternal(position, rotation));
        public T Instantiate(Vector3 position, Quaternion rotation, Transform parent) => Initialize(InstantiateInternal(position, rotation, parent));
        public T Instantiate(Transform parent) => Initialize(InstantiateInternal(parent));
        public T Instantiate(Transform parent, bool worldPositionStays) => Initialize(InstantiateInternal(parent, worldPositionStays));
        public T Instantiate(InstantiateParameters parameters) => Initialize(InstantiateInternal(parameters));
        public T Instantiate(Vector3 position, Quaternion rotation, InstantiateParameters parameters) => Initialize(InstantiateInternal(position, rotation, parameters));

        // --- Data Initialization ---
        public T Instantiate<D>(D data) => Initialize(InstantiateInternal(), data);
        public T Instantiate<D>(Vector3 position, Quaternion rotation, D data) => Initialize(InstantiateInternal(position, rotation), data);
        public T Instantiate<D>(Vector3 position, Quaternion rotation, Transform parent, D data) => Initialize(InstantiateInternal(position, rotation, parent), data);
        public T Instantiate<D>(Transform parent, D data) => Initialize(InstantiateInternal(parent), data);
        public T Instantiate<D>(Transform parent, bool worldPositionStays, D data) => Initialize(InstantiateInternal(parent, worldPositionStays), data);
        public T Instantiate<D>(InstantiateParameters parameters, D data) => Initialize(InstantiateInternal(parameters), data);
        public T Instantiate<D>(Vector3 position, Quaternion rotation, InstantiateParameters parameters, D data) => Initialize(InstantiateInternal(position, rotation, parameters), data);

#if UNITY_EDITOR
        public T InstantiatePrefab() => Initialize(InstantiatePrefabInternal());
        public T InstantiatePrefab(Scene destinationScene) => Initialize(InstantiatePrefabInternal(destinationScene));
        public T InstantiatePrefab(Transform parent) => Initialize(InstantiatePrefabInternal(parent));

        public T InstantiatePrefab<D>(D data) => Initialize(InstantiatePrefabInternal(), data);
        public T InstantiatePrefab<D>(Scene destinationScene, D data) => Initialize(InstantiatePrefabInternal(destinationScene), data);
        public T InstantiatePrefab<D>(Transform parent, D data) => Initialize(InstantiatePrefabInternal(parent), data);
#endif
    }
}

