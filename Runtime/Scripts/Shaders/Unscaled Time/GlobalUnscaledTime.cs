using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abb2kTools.Shaders
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class GlobalUnscaledTime
    {
        private static readonly int TimeParamsID = Shader.PropertyToID("_UnscaledTimeParams");

#if UNITY_EDITOR
        private static double lastEditorTime;
        private static float editorSmoothDelta;

        static GlobalUnscaledTime()
        {
            lastEditorTime = EditorApplication.timeSinceStartup;

            EditorApplication.update += () =>
            {
                if (Application.isPlaying) return;

                double currentTime = EditorApplication.timeSinceStartup;
                float dt = (float)(currentTime - lastEditorTime);
                lastEditorTime = currentTime;
                
                editorSmoothDelta = Mathf.Lerp(editorSmoothDelta, dt, 0.2f);
                
                Shader.SetGlobalVector(TimeParamsID, new Vector4((float)currentTime, dt, editorSmoothDelta, 0f));
            };
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            var updaterObj = new GameObject("HiddenUnscaledTimeUpdater");
            updaterObj.AddComponent<TimeUpdater>();
            
            Object.DontDestroyOnLoad(updaterObj);
            updaterObj.hideFlags = HideFlags.HideAndDontSave;
        }

        private class TimeUpdater : MonoBehaviour
        {
            private float smoothDt;
            
            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                smoothDt = Mathf.Lerp(smoothDt, dt, 0.2f);
                
                Shader.SetGlobalVector(TimeParamsID, new Vector4(Time.unscaledTime, dt, smoothDt, 0f));
            }
        }
    }
}