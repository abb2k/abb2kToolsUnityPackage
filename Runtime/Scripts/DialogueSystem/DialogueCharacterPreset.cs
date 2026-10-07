using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterPreset", menuName = "Abb2kTools/Character Preset")]
public sealed class DialogueCharacterPreset : ScriptableObject
{
    public DialogueContentValue content = new();
    public List<DialogueCharacterPresetVariant> variants = new();

    public DialogueContentValue GetContent(DialogueCharacterPresetVariant variant)
    {
        if (variant != null)
            foreach (var candidate in variants ?? new List<DialogueCharacterPresetVariant>())
                if (candidate == variant && variant.content != null)
                    return variant.content;

        return content;
    }
}