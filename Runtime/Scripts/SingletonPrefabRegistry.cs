using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abb2kTools 
{
    public class SingletonPrefabRegistry : ScriptableObject
    {
        [Serializable]
        public class PrefabMapping
        {
            public string singletonTypeName;
            public GameObject prefab;
        }

        [HideInInspector]
        public List<PrefabMapping> mappings = new List<PrefabMapping>();

        public static GameObject GetPrefab(string typeName)
        {
            SingletonPrefabRegistry instance = Resources.Load<SingletonPrefabRegistry>("SingletonPrefabRegistry");
            if (instance == null) return null;

            foreach (var map in instance.mappings)
            {
                if (map.singletonTypeName == typeName) return map.prefab;
            }
            return null;
        }
        
#if UNITY_EDITOR
        public static void RegisterPrefab(string typeName, GameObject prefab)
        {
            SingletonPrefabRegistry instance = Resources.Load<SingletonPrefabRegistry>("SingletonPrefabRegistry");

            if (instance == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                
                instance = CreateInstance<SingletonPrefabRegistry>();
                AssetDatabase.CreateAsset(instance, "Assets/Resources/SingletonPrefabRegistry.asset");
            }

            instance.mappings.RemoveAll(m => m.singletonTypeName == typeName);
            instance.mappings.Add(new PrefabMapping { singletonTypeName = typeName, prefab = prefab });
            
            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
        }

        public static void UnlinkPrefab(string typeName)
        {
            SingletonPrefabRegistry instance = Resources.Load<SingletonPrefabRegistry>("SingletonPrefabRegistry");
            if (instance != null)
            {
                int removed = instance.mappings.RemoveAll(m => m.singletonTypeName == typeName);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(instance);
                    AssetDatabase.SaveAssets();
                }
            }
        }
#endif
    }
}