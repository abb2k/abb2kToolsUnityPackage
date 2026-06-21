#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public abstract class PrefabReferenceBase<T> where T : MonoBehaviour
{
    [SerializeField]
    private T _component;

    public T Component => _component;
    public GameObject GameObject => _component != null ? _component.gameObject : null;

    public Object InstantiateObject(Vector3 position, Quaternion rotation) => Object.Instantiate(GameObject, position, rotation);
    public Object InstantiateObject(Vector3 position, Quaternion rotation, Transform parent) => Object.Instantiate(GameObject, position, rotation, parent);
    public Object InstantiateObject() => Object.Instantiate(GameObject);
    public Object InstantiateObject(Scene scene) => Object.Instantiate(GameObject, scene);
    public Object InstantiateObject(Transform parent) => Object.Instantiate(GameObject, parent, false);
    public Object InstantiateObject(Transform parent, bool instantiateInWorldSpace) => Object.Instantiate(GameObject, parent, instantiateInWorldSpace);

    protected T InstantiateInternal(InstantiateParameters parameters) => Object.Instantiate<T>(Component, parameters);
    protected T InstantiateInternal(Vector3 position, Quaternion rotation, InstantiateParameters parameters) => Object.Instantiate<T>(Component, position, rotation, parameters);
    protected T InstantiateInternal() => Object.Instantiate<T>(Component);
    protected T InstantiateInternal(Vector3 position, Quaternion rotation) => Object.Instantiate<T>(Component, position, rotation);
    protected T InstantiateInternal(Vector3 position, Quaternion rotation, Transform parent) => Object.Instantiate<T>(Component, position, rotation, parent);
    protected T InstantiateInternal(Transform parent) => Object.Instantiate<T>(Component, parent, false);
    protected T InstantiateInternal(Transform parent, bool worldPositionStays) => Object.Instantiate<T>(Component, parent, worldPositionStays);

#if UNITY_EDITOR
    /// <summary>
    /// EDITOR ONLY! Instantiates a packed prefab as a raw Object
    /// </summary>
    public Object InstantiatePrefabObject() => PrefabUtility.InstantiatePrefab(GameObject);
    public Object InstantiatePrefabObject(Scene destinationScene) => PrefabUtility.InstantiatePrefab(GameObject, destinationScene);
    public Object InstantiatePrefabObject(Transform parent) => PrefabUtility.InstantiatePrefab(GameObject, parent);

    /// <summary>
    /// EDITOR ONLY! Instantiates a packed prefab as type T
    /// </summary>
    protected T InstantiatePrefabInternal() => (PrefabUtility.InstantiatePrefab(GameObject) as GameObject)?.GetComponent<T>();
    protected T InstantiatePrefabInternal(Scene destinationScene) => (PrefabUtility.InstantiatePrefab(GameObject, destinationScene) as GameObject)?.GetComponent<T>();
    protected T InstantiatePrefabInternal(Transform parent) => (PrefabUtility.InstantiatePrefab(GameObject, parent) as GameObject)?.GetComponent<T>();
#endif
}

[System.Serializable]
public class PrefabReference<T> : PrefabReferenceBase<T> where T : MonoBehaviour, IInitializable
{
    private T Initialize(T instance)
    {
        if (instance != null)
            instance.Init();
            
        return instance;
    }

    public T Instantiate() => Initialize(InstantiateInternal());
    public T Instantiate(Vector3 position, Quaternion rotation) => Initialize(InstantiateInternal(position, rotation));
    public T Instantiate(Vector3 position, Quaternion rotation, Transform parent) => Initialize(InstantiateInternal(position, rotation, parent));
    public T Instantiate(Transform parent) => Initialize(InstantiateInternal(parent));
    public T Instantiate(Transform parent, bool worldPositionStays) => Initialize(InstantiateInternal(parent, worldPositionStays));
    public T Instantiate(InstantiateParameters parameters) => Initialize(InstantiateInternal(parameters));
    public T Instantiate(Vector3 position, Quaternion rotation, InstantiateParameters parameters) => Initialize(InstantiateInternal(position, rotation, parameters));

#if UNITY_EDITOR
    public T InstantiatePrefab() => Initialize(InstantiatePrefabInternal());
    public T InstantiatePrefab(Scene destinationScene) => Initialize(InstantiatePrefabInternal(destinationScene));
    public T InstantiatePrefab(Transform parent) => Initialize(InstantiatePrefabInternal(parent));
#endif
}

[System.Serializable]
public class PrefabReference<T, D> : PrefabReferenceBase<T> where T : MonoBehaviour, IInitializable<D>
{
    private T Initialize(T instance, D data)
    {
        if (instance != null)
            instance.Init(data);
        
        return instance;
    }

    public T Instantiate(D data) => Initialize(InstantiateInternal(), data);
    public T Instantiate(Vector3 position, Quaternion rotation, D data) => Initialize(InstantiateInternal(position, rotation), data);
    public T Instantiate(Vector3 position, Quaternion rotation, Transform parent, D data) => Initialize(InstantiateInternal(position, rotation, parent), data);
    public T Instantiate(Transform parent, D data) => Initialize(InstantiateInternal(parent), data);
    public T Instantiate(Transform parent, bool worldPositionStays, D data) => Initialize(InstantiateInternal(parent, worldPositionStays), data);
    public T Instantiate(InstantiateParameters parameters, D data) => Initialize(InstantiateInternal(parameters), data);
    public T Instantiate(Vector3 position, Quaternion rotation, InstantiateParameters parameters, D data) => Initialize(InstantiateInternal(position, rotation, parameters), data);

#if UNITY_EDITOR
    public T InstantiatePrefab(D data) => Initialize(InstantiatePrefabInternal(), data);
    public T InstantiatePrefab(Scene destinationScene, D data) => Initialize(InstantiatePrefabInternal(destinationScene), data);
    public T InstantiatePrefab(Transform parent, D data) => Initialize(InstantiatePrefabInternal(parent), data);
#endif
}