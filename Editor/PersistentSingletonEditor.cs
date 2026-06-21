using UnityEditor;
using UnityEngine;
using Abb2kTools;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Abb2kTools.EditorScripts 
{
    #if ODIN_INSPECTOR
    
    [CustomEditor(typeof(SingletonBase), true)]
    public class SingletonEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (HasVisibleVariables())
            {
                DrawCustomHeader();
            }
            
            base.OnInspectorGUI();
        }

        private bool HasVisibleVariables()
        {
            int variableCount = 0;
            
            foreach (var prop in Tree.RootProperty.Children)
            {
                if (prop.Name != "m_Script") 
                {
                    variableCount++;
                }
            }
            return variableCount > 0;
        }

        private void DrawCustomHeader()
        {
            SingletonBase singletonTarget = (SingletonBase)target;
            if (singletonTarget.IsPersistent)
                EditorGUILayout.HelpBox("Never assign inspector values on a persistent singleton!!! It is bad practice!", MessageType.Error);
            
            GUILayout.Space(10);
        }
    }

    #else

    [CustomEditor(typeof(SingletonBase), true)]
    public class SingletonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (HasVisibleVariables())
            {
                DrawCustomHeader();
            }

            DrawDefaultInspector();
        }

        private bool HasVisibleVariables()
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            int variableCount = 0;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name != "m_Script")
                {
                    variableCount++;
                }
            }

            return variableCount > 0;
        }

        private void DrawCustomHeader()
        {
            SingletonBase singletonTarget = (SingletonBase)target;
            if (singletonTarget.IsPersistent)
                EditorGUILayout.HelpBox("Never assign inspector values on a persistent singleton!!! It is bad practice!", MessageType.Error);

            GUILayout.Space(10);
        }
    }

    #endif
}