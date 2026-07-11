using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class ReadOnlyHierarchyManager
{
    private static HashSet<GameObject> trackedRoots = new();

    static ReadOnlyHierarchyManager()
    {
        EditorApplication.hierarchyChanged += UpdateAllLocks;
        Selection.selectionChanged += UpdateAllLocks;
        
        EditorApplication.playModeStateChanged += (state) => UpdateAllLocks();
        
#if UNITY_2021_2_OR_NEWER
        PrefabStage.prefabStageOpened += (stage) => UpdateAllLocks();
        PrefabStage.prefabStageClosing += (stage) => UpdateAllLocks();
#else
        PrefabStageUtility.prefabStageOpened += (stage) => UpdateAllLocks();
        PrefabStageUtility.prefabStageClosing += (stage) => UpdateAllLocks();
#endif

        EditorApplication.delayCall += UpdateAllLocks;
    }

    private static void UpdateAllLocks()
    {
        var singletonTypes = TypeCache.GetTypesDerivedFrom<IReadOnlyHierarchy>();
        HashSet<GameObject> currentRoots = new();

        foreach (Type type in singletonTypes)
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(Component).IsAssignableFrom(type)) continue;

            var instances = Resources.FindObjectsOfTypeAll(type);

            foreach (Component instance in instances)
            {
                if ((instance.gameObject.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

                var rootGO = instance.gameObject;
                currentRoots.Add(rootGO);

                bool shouldLock = true;

                if (EditorApplication.isPlaying)
                {
                    shouldLock = false;
                }
                else
                {
                    PrefabStage stage = PrefabStageUtility.GetPrefabStage(rootGO);
                    
                    if (stage != null)
                    {
                        shouldLock = stage.prefabContentsRoot != rootGO;
                    }
                    else if (PrefabUtility.IsPartOfPrefabAsset(rootGO))
                    {
                        shouldLock = rootGO.transform.parent != null;
                    }
                }

                ApplyLockState(instance.transform, shouldLock, instance);
            }
        }

        List<GameObject> toRemove = new();
        foreach (GameObject trackedGO in trackedRoots)
        {
            if (trackedGO != null && !currentRoots.Contains(trackedGO))
            {
                ApplyLockState(trackedGO.transform, false, null);
                toRemove.Add(trackedGO);
            }
            else if (trackedGO == null)
            {
                toRemove.Add(trackedGO);
            }
        }

        foreach (GameObject go in toRemove)
            trackedRoots.Remove(go);

        foreach (GameObject go in currentRoots)
            trackedRoots.Add(go);
    }

    private static void ApplyLockState(Transform root, bool isLocked, Component singletonScript)
    {
        var allChildren = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allChildren)
        {
            var go = t.gameObject;
            
            if (go != root.gameObject)
            {
                SetFlags(go, isLocked);
            }

            var components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null || comp == singletonScript) continue;
                SetFlags(comp, isLocked);
            }
        }
    }

    private static void SetFlags(UnityEngine.Object obj, bool isLocked)
    {
        var targetFlags = obj.hideFlags;
        
        if (isLocked) targetFlags |= HideFlags.NotEditable;
        else targetFlags &= ~HideFlags.NotEditable;

        if (obj.hideFlags != targetFlags)
        {
            obj.hideFlags = targetFlags;
        }
    }
}