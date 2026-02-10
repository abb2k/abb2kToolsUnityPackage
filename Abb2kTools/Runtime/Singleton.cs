using UnityEngine;


namespace Abb2kTools {
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected internal static T instance;
        private static readonly object objLock = new();

        protected new virtual bool DontDestroyOnLoad => true;

        private bool createdByGet;

        protected virtual void Awake()
        {
            if (instance == null)
                instance = this as T;
            else if (instance != this)
                Destroy(gameObject);

            if (DontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            if (!createdByGet)
                OnCreation();
        }

        public virtual void OnCreation() { }

        public static T Get()
        {
            lock (objLock)
            {
                if (!instance)
                {
                    instance = new GameObject(typeof(T).Name).AddComponent<T>();

                    if (instance is Singleton<T> singleton)
                    {
                        if (!singleton.DontDestroyOnLoad)
                        {
                            Destroy(singleton.gameObject);
                            return null;
                        }
                        else
                        {
                            singleton.createdByGet = true;
                            singleton.OnCreation();
                        }
                    }
                }

                return instance;
            }
        }

        void OnDestroy()
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
}