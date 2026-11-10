#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [InitializeOnLoad]
    internal static class AudioClipCreateSoundButton
    {
        static AudioClipCreateSoundButton()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawButton;
        }

        private static void DrawButton(UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1 || editor.target is not AudioClip clip) return;

            EditorGUILayout.Space(4f);
            if (!GUILayout.Button("Create Sound")) return;

            string clipAssetPath = AssetDatabase.GetAssetPath(clip);
            string folder = Path.GetDirectoryName(clipAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets"))
                folder = "Assets";

            string defaultName = $"{clip.name}_Sound";
            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Sound",
                defaultName,
                "asset",
                "Choose where to save the Sound asset.",
                folder);
            if (string.IsNullOrEmpty(assetPath)) return;

            SoundModification sound = ScriptableObject.CreateInstance<SoundModification>();
            SerializedObject serializedSound = new(sound);
            SerializedProperty clipProperty = serializedSound.FindProperty("clip");
            if (clipProperty == null)
            {
                Object.DestroyImmediate(sound);
                Debug.LogError("Could not find the AudioClip field on SoundModification.");
                return;
            }

            clipProperty.objectReferenceValue = clip;
            serializedSound.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(sound, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = sound;
            EditorGUIUtility.PingObject(sound);
        }
    }
}
#endif
