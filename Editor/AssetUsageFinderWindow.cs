#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    public class AssetUsageFinderWindow : EditorWindow
    {
        private sealed class Usage
        {
            public string AssetPath;
            public string PropertyPath;
            public UnityEngine.Object Owner;
            public bool IsDirect;
        }

        private UnityEngine.Object _asset;
        private List<Usage> _usages = new();
        private Vector2 _scrollPosition;
        private bool _searched;
        private bool _searchCancelled;
        private string _searchMessage;

        [MenuItem("Tools/Abb2k/Find Asset Usages")]
        private static void OpenWindow()
        {
            GetWindow<AssetUsageFinderWindow>("Asset Usages");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Find Asset Usages", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _asset = EditorGUILayout.ObjectField("Asset", _asset, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                _usages.Clear();
                _searched = false;
                _searchMessage = null;
            }

            EditorGUILayout.HelpBox(
                "Drop any project asset here to find assets, prefabs, scenes, and loaded objects that reference it.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("Find Usages"))
                    FindUsages();
            }

            if (!_searched)
                return;
            if (!string.IsNullOrEmpty(_searchMessage))
            {
                EditorGUILayout.HelpBox(_searchMessage, MessageType.Warning);
                return;
            }
            if (_searchCancelled)
            {
                EditorGUILayout.HelpBox("The search was cancelled.", MessageType.Warning);
                return;
            }
            if (_usages.Count == 0)
            {
                EditorGUILayout.HelpBox("No usages were found.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_usages.Count} usages", EditorStyles.miniBoldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (Usage usage in _usages)
            {
                string relation = usage.IsDirect ? "Direct" : "Via reference";
                string displayName = usage.Owner is Component component
                    ? $"{component.gameObject.name} ({component.GetType().Name})"
                    : usage.Owner != null
                        ? usage.Owner.name
                        : System.IO.Path.GetFileNameWithoutExtension(usage.AssetPath);

                if (GUILayout.Button($"{displayName} ({relation})", EditorStyles.linkLabel))
                    NavigateToUsage(usage);

                string location = string.IsNullOrEmpty(usage.PropertyPath)
                    ? usage.AssetPath
                    : $"{usage.AssetPath} - {usage.PropertyPath}";
                EditorGUILayout.LabelField(location, EditorStyles.miniLabel);
                EditorGUILayout.Space(2f);
            }
            EditorGUILayout.EndScrollView();
        }

        private void FindUsages()
        {
            _usages.Clear();
            _searched = true;
            _searchCancelled = false;
            _searchMessage = null;

            string targetPath = AssetDatabase.GetAssetPath(_asset);
            if (string.IsNullOrEmpty(targetPath))
            {
                _searchMessage = "Choose an asset saved in the project.";
                return;
            }

            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            HashSet<string> loadedObjectsWithUsages = new();
            try
            {
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string path = assetPaths[i];
                    if (path == targetPath ||
                        !(path.StartsWith("Assets/") || path.StartsWith("Packages/")) ||
                        string.IsNullOrEmpty(System.IO.Path.GetExtension(path)))
                        continue;

                    if (i % 20 == 0 &&
                        EditorUtility.DisplayCancelableProgressBar(
                            "Find Asset Usages",
                            path,
                            i / (float)assetPaths.Length))
                    {
                        _searchCancelled = true;
                        break;
                    }

                    string[] dependencies = AssetDatabase.GetDependencies(path, true);
                    if (System.Array.IndexOf(dependencies, targetPath) < 0)
                        continue;

                    bool isDirect = System.Array.IndexOf(
                        AssetDatabase.GetDependencies(path, false),
                        targetPath) >= 0;
                    _usages.Add(new Usage
                    {
                        AssetPath = path,
                        Owner = AssetDatabase.LoadMainAssetAtPath(path),
                        IsDirect = isDirect
                    });
                }

                Dictionary<string, bool> dependencyCache = new();
                List<Usage> loadedObjectUsages = FindLoadedObjectUsages(_asset, targetPath, dependencyCache);
                foreach (Usage usage in loadedObjectUsages)
                {
                    _usages.Add(usage);
                    loadedObjectsWithUsages.Add(usage.AssetPath);
                }

                _usages.RemoveAll(usage =>
                    (System.IO.Path.GetExtension(usage.AssetPath) == ".unity" ||
                     System.IO.Path.GetExtension(usage.AssetPath) == ".prefab") &&
                    loadedObjectsWithUsages.Contains(usage.AssetPath) &&
                    usage.Owner is not Component);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _usages.Sort((left, right) =>
            {
                int pathOrder = string.Compare(
                    left.AssetPath,
                    right.AssetPath,
                    System.StringComparison.OrdinalIgnoreCase);
                return pathOrder != 0
                    ? pathOrder
                    : string.Compare(
                        left.PropertyPath,
                        right.PropertyPath,
                        System.StringComparison.OrdinalIgnoreCase);
            });
        }

        private static List<Usage> FindLoadedObjectUsages(
            UnityEngine.Object targetAsset,
            string targetPath,
            Dictionary<string, bool> dependencyCache)
        {
            List<Usage> usages = new();
            foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component == null || component.gameObject == null)
                    continue;

                UnityEngine.SceneManagement.Scene scene = component.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (!TryFindObjectUsage(
                        component,
                        targetAsset,
                        targetPath,
                        dependencyCache,
                        out string propertyPath,
                        out bool isDirect))
                    continue;

                usages.Add(new Usage
                {
                    AssetPath = scene.path,
                    PropertyPath = propertyPath,
                    Owner = component,
                    IsDirect = isDirect
                });
            }

            return usages;
        }

        private static bool TryFindObjectUsage(
            UnityEngine.Object owner,
            UnityEngine.Object targetAsset,
            string targetPath,
            Dictionary<string, bool> dependencyCache,
            out string propertyPath,
            out bool isDirect)
        {
            propertyPath = null;
            isDirect = false;
            SerializedObject serializedOwner = new(owner);
            SerializedProperty iterator = serializedOwner.GetIterator();
            string indirectPath = null;
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                UnityEngine.Object referencedObject = iterator.objectReferenceValue;
                if (referencedObject == targetAsset)
                {
                    propertyPath = iterator.propertyPath;
                    isDirect = true;
                    return true;
                }

                string referencedPath = AssetDatabase.GetAssetPath(referencedObject);
                if (string.IsNullOrEmpty(referencedPath) || referencedPath == targetPath)
                    continue;

                if (!dependencyCache.TryGetValue(referencedPath, out bool usesTarget))
                {
                    usesTarget = System.Array.IndexOf(
                        AssetDatabase.GetDependencies(referencedPath, true),
                        targetPath) >= 0;
                    dependencyCache.Add(referencedPath, usesTarget);
                }

                if (usesTarget && indirectPath == null)
                    indirectPath = iterator.propertyPath;
            }

            if (indirectPath == null)
                return false;

            propertyPath = indirectPath;
            return true;
        }

        private void NavigateToUsage(Usage usage)
        {
            if (usage.Owner is Component component)
            {
                SelectObject(component);
                return;
            }

            string extension = System.IO.Path.GetExtension(usage.AssetPath);
            if (extension == ".unity" && usage.Owner is UnityEditor.SceneAsset)
            {
                UnityEngine.SceneManagement.Scene scene =
                    UnityEngine.SceneManagement.SceneManager.GetSceneByPath(usage.AssetPath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        usage.AssetPath,
                        UnityEditor.SceneManagement.OpenSceneMode.Additive);
                }

                string scenePath = usage.AssetPath;
                UnityEngine.Object targetAsset = _asset;
                EditorApplication.delayCall += () => SelectFirstUsageInScene(scenePath, targetAsset);
                return;
            }

            if (extension == ".prefab" && usage.Owner is GameObject)
            {
                SelectObject(usage.Owner);
                AssetDatabase.OpenAsset(usage.Owner);
                string prefabPath = usage.AssetPath;
                UnityEngine.Object targetAsset = _asset;
                EditorApplication.delayCall += () => SelectFirstUsageInPrefab(prefabPath, targetAsset);
                return;
            }

            SelectObject(usage.Owner != null
                ? usage.Owner
                : AssetDatabase.LoadMainAssetAtPath(usage.AssetPath));
        }

        private static void SelectFirstUsageInScene(string scenePath, UnityEngine.Object targetAsset)
        {
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            string targetPath = AssetDatabase.GetAssetPath(targetAsset);
            Dictionary<string, bool> dependencyCache = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null &&
                        TryFindObjectUsage(component, targetAsset, targetPath, dependencyCache, out _, out _))
                    {
                        SelectObject(component);
                        return;
                    }
                }
            }

            SelectObject(AssetDatabase.LoadMainAssetAtPath(scenePath));
        }

        private static void SelectFirstUsageInPrefab(string prefabPath, UnityEngine.Object targetAsset)
        {
            UnityEditor.SceneManagement.PrefabStage stage =
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != prefabPath || stage.prefabContentsRoot == null)
                return;

            string targetPath = AssetDatabase.GetAssetPath(targetAsset);
            Dictionary<string, bool> dependencyCache = new();
            foreach (Component component in stage.prefabContentsRoot.GetComponentsInChildren<Component>(true))
            {
                if (component != null &&
                    TryFindObjectUsage(component, targetAsset, targetPath, dependencyCache, out _, out _))
                {
                    SelectObject(component);
                    return;
                }
            }
        }

        private static void SelectObject(UnityEngine.Object selectedObject)
        {
            if (selectedObject == null)
                return;

            Selection.activeObject = selectedObject;
            EditorGUIUtility.PingObject(selectedObject);
        }
    }
}
#endif
