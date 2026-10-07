using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueContentProjectSettings", menuName = "Abb2kTools/Dialogue Content Project Settings")]
public sealed class DialogueContentProjectSettings : ScriptableObject
{
    public const string ResourcesAssetName = "DialogueContentProjectSettings";

    public string defaultContentTypeName;

    public Type DefaultContentType => string.IsNullOrEmpty(defaultContentTypeName)
        ? null
        : Type.GetType(defaultContentTypeName);

    public static DialogueContentProjectSettings Load()
    {
        return Resources.Load<DialogueContentProjectSettings>(ResourcesAssetName);
    }
}