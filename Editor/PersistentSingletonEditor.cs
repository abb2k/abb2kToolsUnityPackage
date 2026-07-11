using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement; 

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Abb2kTools
{
#if ODIN_INSPECTOR
    
    [CustomEditor(typeof(SingletonBase), true)]
    public class SingletonEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            SingletonBase singletonTarget = (SingletonBase)target;
            bool isPersistent = singletonTarget.IsPersistent;
            bool isPlaying = Application.isPlaying;

            this.DrawCoolHeader(singletonTarget);

            if (isPersistent && singletonTarget.transform.parent != null)
            {
                this.DrawRootError();
            }

            GameObject prefabAsset = this.GetPrefabAsset(singletonTarget.gameObject);
            bool isPrefab = prefabAsset != null;
            bool hasExtraData = HasVisibleVariables() || HasChildrenOrOtherComponents(singletonTarget);
            
            bool lockInspector = false;

            if (isPersistent && !isPlaying)
            {
                bool isAsset = EditorUtility.IsPersistent(singletonTarget.gameObject);
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(singletonTarget.gameObject);
                
                bool isSceneInstance = !isAsset && stage == null;

                lockInspector = (hasExtraData && !isPrefab) || isSceneInstance;

                if (isSceneInstance)
                {
                    this.DrawSceneWarning();
                }

                if (hasExtraData && !isPrefab)
                {
                    this.DrawPackUI(singletonTarget);
                }
                else if (isPrefab)
                {
                    this.DrawLinkUI(singletonTarget, prefabAsset);
                }
            }
            
            EditorGUI.BeginDisabledGroup(lockInspector);
            base.OnInspectorGUI();
            EditorGUI.EndDisabledGroup();
        }

        private bool HasVisibleVariables()
        {
            int variableCount = 0;
            foreach (var prop in Tree.RootProperty.Children)
            {
                if (prop.Name != "m_Script") variableCount++;
            }
            return variableCount > 0;
        }

        private bool HasChildrenOrOtherComponents(SingletonBase singletonTarget)
        {
            if (singletonTarget.transform.childCount > 0) return true;
            Component[] components = singletonTarget.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null || (comp != singletonTarget && comp.GetType() != typeof(Transform))) return true;
            }
            return false;
        }
    }

#else

    [CustomEditor(typeof(SingletonBase), true)]
    public class SingletonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            SingletonBase singletonTarget = (SingletonBase)target;
            bool isPersistent = singletonTarget.IsPersistent;
            bool isPlaying = Application.isPlaying;

            this.DrawCoolHeader(singletonTarget);

            if (isPersistent && singletonTarget.transform.parent != null)
            {
                this.DrawRootError();
            }

            GameObject prefabAsset = this.GetPrefabAsset(singletonTarget.gameObject);
            bool isPrefab = prefabAsset != null;
            bool hasExtraData = HasVisibleVariables() || HasChildrenOrOtherComponents(singletonTarget);
            
            bool lockInspector = false;

            if (isPersistent && !isPlaying)
            {
                bool isAsset = EditorUtility.IsPersistent(singletonTarget.gameObject);
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(singletonTarget.gameObject);
                
                bool isSceneInstance = !isAsset && stage == null;

                lockInspector = (hasExtraData && !isPrefab) || isSceneInstance;

                if (isSceneInstance)
                {
                    this.DrawSceneWarning();
                }

                if (hasExtraData && !isPrefab)
                {
                    this.DrawPackUI(singletonTarget);
                }
                else if (isPrefab)
                {
                    this.DrawLinkUI(singletonTarget, prefabAsset);
                }
            }

            EditorGUI.BeginDisabledGroup(lockInspector);
            DrawDefaultInspector();
            EditorGUI.EndDisabledGroup();
        }

        private bool HasVisibleVariables()
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            int variableCount = 0;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name != "m_Script") variableCount++;
            }
            return variableCount > 0;
        }

        private bool HasChildrenOrOtherComponents(SingletonBase singletonTarget)
        {
            if (singletonTarget.transform.childCount > 0) return true;
            Component[] components = singletonTarget.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null || (comp != singletonTarget && comp.GetType() != typeof(Transform))) return true;
            }
            return false;
        }
    }

#endif

    public static class SingletonEditorUtils 
    {
        public static GameObject GetPrefabAsset(GameObject go)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(go)) return go;
            
            if (PrefabUtility.IsPartOfPrefabInstance(go)) 
                return PrefabUtility.GetCorrespondingObjectFromSource(go);

#if UNITY_2021_2_OR_NEWER
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(go);
#else
            PrefabStage stage = PrefabStage.GetPrefabStage(go);
#endif
            if (stage != null)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            }

            return null;
        }

        public static void DrawCoolHeader(SingletonBase singletonTarget)
        {
            bool isPersistent = singletonTarget.IsPersistent;
            string title = isPersistent ? "⚡ PERSISTENT SINGLETON ⚡" : "❖ STANDARD SINGLETON ❖";
            
            Color bgColor = isPersistent ? new Color(0.15f, 0.45f, 0.85f, 1f) : new Color(0.2f, 0.6f, 0.3f, 1f);

            float height = 28f;
            Rect rect = GUILayoutUtility.GetRect(0f, height);
            
            rect.xMin = 0f;
            rect.width = EditorGUIUtility.currentViewWidth;

            EditorGUI.DrawRect(rect, bgColor);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            GUI.Label(rect, title, style);
            
            GUILayout.Space(8);
        }

        public static void DrawRootError()
        {
            EditorGUILayout.HelpBox("A persistent singleton MUST be a root object, not doing so can cause some unexpected issues", MessageType.Error);
            GUILayout.Space(5);
        }

        public static void DrawSceneWarning()
        {
            EditorGUILayout.HelpBox("having a persistent singleton exist in the game scene is not recommended! inspector is disabled.", MessageType.Warning);
            GUILayout.Space(5);
        }

        public static void DrawPackUI(SingletonBase singletonTarget)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.HelpBox("External values wont save with a persistent singleton unless you pack your object into a prefab! this includes inspector values, components, and children.", MessageType.Error);
            
            if (GUILayout.Button("Pack", GUILayout.ExpandHeight(true), GUILayout.Width(60)))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Prefab", singletonTarget.gameObject.name, "prefab", "Save singleton as a prefab");
                if (!string.IsNullOrEmpty(path))
                {
                    GameObject newPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(singletonTarget.gameObject, path, InteractionMode.UserAction);
                    SingletonPrefabRegistry.RegisterPrefab(singletonTarget.GetType().FullName, newPrefab);
                    Debug.Log($"Successfully packed and linked {singletonTarget.GetType().Name}!");
                }
            }
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        public static void DrawLinkUI(SingletonBase singletonTarget, GameObject prefabAsset)
        {
            string typeName = singletonTarget.GetType().FullName;
            GameObject linkedPrefab = SingletonPrefabRegistry.GetPrefab(typeName);
            bool isLinked = linkedPrefab == prefabAsset;

            EditorGUILayout.BeginHorizontal();

            if (isLinked)
            {
                EditorGUILayout.HelpBox("This prefab is linked as the main singleton for " + singletonTarget.GetType().Name + ".", MessageType.Info);
                
                if (GUILayout.Button("Unlink", GUILayout.ExpandHeight(true), GUILayout.Width(60)))
                {
                    SingletonPrefabRegistry.UnlinkPrefab(typeName);
                }
            }
            else
            {
                if (linkedPrefab != null)
                {
                    EditorGUILayout.HelpBox("Another prefab is currently linked as the main singleton for " + singletonTarget.GetType().Name + ". Link this one instead?", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("This prefab is not currently linked to the singleton registry. Link it to allow auto-spawning.", MessageType.None);
                }

                if (GUILayout.Button("Link as\nMain", GUILayout.ExpandHeight(true), GUILayout.Width(60)))
                {
                    SingletonPrefabRegistry.RegisterPrefab(typeName, prefabAsset);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
    }

    public static class SingletonEditorExtensions 
    {
        public static void DrawCoolHeader(this UnityEditor.Editor editor, SingletonBase target) => SingletonEditorUtils.DrawCoolHeader(target);
        public static GameObject GetPrefabAsset(this UnityEditor.Editor editor, GameObject go) => SingletonEditorUtils.GetPrefabAsset(go);
        public static void DrawRootError(this UnityEditor.Editor editor) => SingletonEditorUtils.DrawRootError();
        public static void DrawSceneWarning(this UnityEditor.Editor editor) => SingletonEditorUtils.DrawSceneWarning();
        public static void DrawPackUI(this UnityEditor.Editor editor, SingletonBase target) => SingletonEditorUtils.DrawPackUI(target);
        public static void DrawLinkUI(this UnityEditor.Editor editor, SingletonBase target, GameObject prefab) => SingletonEditorUtils.DrawLinkUI(target, prefab);
    }
}