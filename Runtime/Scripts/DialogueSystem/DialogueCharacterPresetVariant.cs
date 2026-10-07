using UnityEngine;

public sealed class DialogueCharacterPresetVariant : ScriptableObject
{
    public string variantName = "Variant";
    public DialogueContentValue content = new();
}
