#if UNITY_EDITOR
using System.Collections.Generic;
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
            if (GUILayout.Button("Create Sound"))
            {
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
                if (!string.IsNullOrEmpty(assetPath))
                {
                    SoundModification sound = ScriptableObject.CreateInstance<SoundModification>();
                    SerializedObject serializedSound = new(sound);
                    SerializedProperty clipProperty = serializedSound.FindProperty("clip");
                    if (clipProperty == null)
                    {
                        Object.DestroyImmediate(sound);
                        Debug.LogError("Could not find the AudioClip field on SoundModification.");
                    }
                    else
                    {
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

            DrawSoundModificationReferences(clip);
        }

        private static void DrawSoundModificationReferences(AudioClip clip)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Sound Modifications Using This Clip", EditorStyles.boldLabel);

            string[] guids = AssetDatabase.FindAssets("t:SoundModification");
            List<SoundModification> references = new();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SoundModification sound = AssetDatabase.LoadAssetAtPath<SoundModification>(path);
                if (sound != null && sound.Clip == clip)
                    references.Add(sound);
            }

            if (references.Count == 0)
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
                return;
            }

            foreach (SoundModification sound in references)
            {
                Rect referenceRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.ObjectField(referenceRect, sound, typeof(SoundModification), false);
                EditorGUI.EndDisabledGroup();

                Event currentEvent = Event.current;
                if (currentEvent.type != EventType.MouseDown ||
                    currentEvent.button != 0 ||
                    !referenceRect.Contains(currentEvent.mousePosition))
                    continue;

                Selection.activeObject = sound;
                EditorGUIUtility.PingObject(sound);
                currentEvent.Use();
            }
        }
    }
}
#endif
