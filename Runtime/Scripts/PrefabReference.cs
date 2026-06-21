#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class PrefabReference<T> where T : MonoBehaviour
{
    [SerializeField]
    private T _component;

    public T Component => _component;
    public GameObject GameObject => _component != null ? _component.gameObject : null;

    public static implicit operator T(PrefabReference<T> reference) => reference?.Component;
    
    public Object InstantiateObject(Vector3 position, Quaternion rotation)
    {
        return Object.Instantiate(GameObject, position, rotation);
    }

    public Object InstantiateObject(Vector3 position, Quaternion rotation, Transform parent)
    {
        return Object.Instantiate(GameObject, position, rotation, parent);
    }

    public Object InstantiateObject()
    {
        return Object.Instantiate(GameObject);
    }

    public Object InstantiateObject(Scene scene)
    {
        return Object.Instantiate(GameObject, scene);
    }

    public T Instantiate(InstantiateParameters parameters)
    {
        return Object.Instantiate<T>(Component, parameters);
    }

    public T Instantiate(Vector3 position, Quaternion rotation, InstantiateParameters parameters)
    {
        return Object.Instantiate<T>(Component, position, rotation, parameters);
    }

    public Object InstantiateObject(Transform parent)
    {
        return Object.Instantiate(GameObject, parent, false);
    }

    public Object InstantiateObject(Transform parent, bool instantiateInWorldSpace)
    {
        return Object.Instantiate(GameObject, parent, instantiateInWorldSpace);
    }

    public T Instantiate()
    {
        return Object.Instantiate(Component);
    }

    public T Instantiate(Vector3 position, Quaternion rotation)
    {
        return Object.Instantiate<T>(Component, position, rotation);
    }

    public T Instantiate(Vector3 position, Quaternion rotation, Transform parent)
    {
        return Object.Instantiate<T>(Component, position, rotation, parent);
    }

    public T Instantiate(Transform parent)
    {
        return Object.Instantiate<T>(Component, parent, worldPositionStays: false);
    }

    public T Instantiate(Transform parent, bool worldPositionStays)
    {
        return Object.Instantiate<T>(Component, parent, worldPositionStays);
    }

#if UNITY_EDITOR

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public UnityEngine.Object InstantiatePrefabObject()
    {
        return PrefabUtility.InstantiatePrefab(GameObject);
    }

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public UnityEngine.Object InstantiatePrefabObject(Scene destinationScene)
    {
        return PrefabUtility.InstantiatePrefab(GameObject, destinationScene);
    }

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public UnityEngine.Object InstantiatePrefabObject(Transform parent)
    {
        return PrefabUtility.InstantiatePrefab(GameObject, parent);
    }

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public T InstantiatePrefab()
    {
        var instance = PrefabUtility.InstantiatePrefab(GameObject) as GameObject;
        return instance != null ? instance.GetComponent<T>() : null;
    }

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public T InstantiatePrefab(Scene destinationScene)
    {
        var instance = PrefabUtility.InstantiatePrefab(GameObject, destinationScene) as GameObject;
        return instance != null ? instance.GetComponent<T>() : null;
    }

    /// <summary>
    /// EDITOR ONLY!
    /// 
    /// Instantiates a packed prefab
    /// </summary>
    /// <returns></returns>
    public T InstantiatePrefab(Transform parent)
    {
        var instance = PrefabUtility.InstantiatePrefab(GameObject, parent) as GameObject;
        return instance != null ? instance.GetComponent<T>() : null;
    }

#endif
}