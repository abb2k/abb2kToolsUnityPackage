#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abb2kTools.AudioSystem.Editor
{
    [FilePath("ProjectSettings/Abb2kToolsSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AudioWaveformProjectSettings : ScriptableSingleton<AudioWaveformProjectSettings>
    {
        [SerializeField] private float normalVolume = 1f;

        internal static float NormalizeGain(float gain)
        {
            return Mathf.Max(0f, gain) / Mathf.Max(0.001f, instance.normalVolume);
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/abb2k Tools/Audio", SettingsScope.Project)
            {
                label = "Audio",
                guiHandler = _ =>
                {
                    EditorGUILayout.LabelField("Waveform Display", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    float newNormalVolume = EditorGUILayout.FloatField(
                        new GUIContent(
                            "Normal Volume",
                            "Reference volume used to scale waveform previews. A sound at this volume is displayed at its natural waveform size."),
                        instance.normalVolume);
                    if (EditorGUI.EndChangeCheck())
                    {
                        instance.normalVolume = Mathf.Max(0.001f, newNormalVolume);
                        instance.Save(true);
                    }
                }
            };
        }
    }
}
#endif
