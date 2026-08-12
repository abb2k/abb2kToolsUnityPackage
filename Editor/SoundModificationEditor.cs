#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem
{
    [CustomEditor(typeof(SoundModificationBase), true)]
    public class SoundModificationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(15);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("▶ Play Preview", GUILayout.Height(35)))
            {
                EditorAudioPreviewer.PlayPreview((SoundModificationBase)target);
            }

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("■ Stop", GUILayout.Height(35), GUILayout.Width(80)))
            {
                EditorAudioPreviewer.StopPreview();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void OnDisable()
        {
            EditorAudioPreviewer.StopPreview();
        }
    }

    /// <summary>
    /// Handles playing complex audio compositions with delays in Edit Mode using EditorApplication.update.
    /// </summary>
    public static class EditorAudioPreviewer
    {
        private static GameObject _previewGO;
        private static readonly List<AudioSource> _activeSources = new();
        private static readonly List<PreviewClip> _pendingClips = new();
        private static double _startTime;

        private class PreviewClip
        {
            public PlayableClipData Data;
            public bool Played;
            public float VolMult;
            public float PitchMult;
        }

        public static void PlayPreview(SoundModificationBase soundBase, float volMult = 1f, float pitchMult = 1f)
        {
            StopPreview(); 

            if (soundBase == null) return;

            _previewGO = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview_Hidden", HideFlags.HideAndDontSave);

            List<PlayableClipData> clips = new();
            soundBase.CollectPlayableClips(clips);

            foreach (var clip in clips)
            {
                if (clip.Clip != null)
                {
                    _pendingClips.Add(new PreviewClip { Data = clip, Played = false, VolMult = volMult, PitchMult = pitchMult });
                }
            }

            if (_pendingClips.Count == 0)
            {
                StopPreview();
                return;
            }

            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }

        public static void StopPreview()
        {
            EditorApplication.update -= EditorUpdate;

            if (_previewGO != null) GameObject.DestroyImmediate(_previewGO);
            _activeSources.Clear();
            _pendingClips.Clear();
        }

        private static void EditorUpdate()
        {
            if (_previewGO == null)
            {
                StopPreview();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - _startTime;
            bool allClipsStarted = true;

            foreach (var pClip in _pendingClips)
            {
                if (!pClip.Played)
                {
                    allClipsStarted = false;
                    
                    if (elapsed >= pClip.Data.Delay)
                    {
                        var src = _previewGO.AddComponent<AudioSource>();
                        src.clip = pClip.Data.Clip;
                        
                        src.volume = pClip.Data.Volume * pClip.VolMult;
                        src.pitch = pClip.Data.Pitch * pClip.PitchMult;
                        
                        if (pClip.Data.PreferredMixerGroup != null)
                            src.outputAudioMixerGroup = pClip.Data.PreferredMixerGroup;
                        
                        src.Play();
                        _activeSources.Add(src);
                        pClip.Played = true;
                    }
                }
            }

            bool isAnythingStillPlaying = false;
            foreach (var src in _activeSources)
            {
                if (src != null && src.isPlaying)
                {
                    isAnythingStillPlaying = true;
                    break;
                }
            }

            if (allClipsStarted && !isAnythingStillPlaying) StopPreview();
        }
    }
}
#endif