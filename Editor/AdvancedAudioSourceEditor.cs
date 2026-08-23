#if UNITY_EDITOR
using UnityEditor;

namespace Abb2kTools.AudioSystem.Editor
{
    [CustomEditor(typeof(AdvancedAudioSource))]
    public class AdvancedAudioSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Ignore the script reference field
                if (iterator.name == "m_Script")
                {
                    continue;
                }

                // Force the custom property drawer to remain expanded
                if (iterator.name == "sound")
                {
                    iterator.isExpanded = true;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif