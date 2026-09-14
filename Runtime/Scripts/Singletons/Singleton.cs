using UnityEngine;

namespace Abb2kTools.Singletons
{
    public abstract class SingletonBase : MonoBehaviour
    {
        public abstract bool IsPersistent { get; }
    }

    [DefaultExecutionOrder(-100)]
    public abstract class Singleton<T> : SingletonBase where T : MonoBehaviour
    {
        public override bool IsPersistent => this is PersistentSingleton<T>;

        protected internal static T instance;
        public static T Instance => Get();
        private static readonly object objLock = new();

        private bool createdByGet;
        private static bool isCreatingByGet;

        protected virtual void Awake()
        {
            if (instance == null)
                instance = this as T;
            else if (instance != this)
                Destroy(gameObject);

            if (this is PersistentSingleton<T>) DontDestroyOnLoad(gameObject);

            if (!createdByGet && !isCreatingByGet)
                OnCreation();
        }

        public virtual void OnCreation() { }

        public static bool TryGet(out T result, bool createIfMissing = false)
        {
            lock (objLock)
            {
                if (instance != null)
                {
                    result = instance;
                    return true;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    result = Get();
                    return result != null;
                }
#endif

                if (createIfMissing)
                {
                    result = Get();
                    return result != null;
                }

                result = null;
                return false;
            }
        }

        public static T Get()
        {
            lock (objLock)
            {
                if (!instance)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
#if UNITY_2023_1_OR_NEWER
                        instance = FindAnyObjectByType<T>();
#else
                        instance = FindObjectOfType<T>();
#endif
                        return instance; 
                    }
#endif

                    if (!typeof(PersistentSingleton<T>).IsAssignableFrom(typeof(T)))
                    {
                        return null;
                    }

                    isCreatingByGet = true;

                    GameObject prefab = SingletonPrefabRegistry.GetPrefab(typeof(T).FullName);

                    if (prefab != null)
                    {
                        GameObject go = Instantiate(prefab);
                        go.name = typeof(T).Name;
                    }
                    else
                    {
                        new GameObject(typeof(T).Name).AddComponent<T>();
                    }

                    isCreatingByGet = false;

                    if (instance is Singleton<T> singleton)
                    {
                        singleton.createdByGet = true;
                        singleton.OnCreation();
                    }
                }

                return instance;
            }
        }

        protected virtual void OnDestroy()
        {
            if (instance != this as T) return;
            instance = null;
        }

        public void DestroySingleton()
        {
            Destroy(gameObject);
            instance = null;
        }
    }

    [DefaultExecutionOrder(-100)]
    public abstract class PersistentSingleton<T> : Singleton<T>, IReadOnlyHierarchy where T : MonoBehaviour
    {
       
    }
}