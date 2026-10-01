using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DialogueSelectionOptionData
{
    public string id = Guid.NewGuid().ToString("N");
    public string text = "Option";
}

// A single dialogue beat/node. CreateAssetMenu also allows making one from Unity's Project window.
[CreateAssetMenu(fileName = "DialogueData", menuName = "Scriptable Objects/DialogueData")]
public class DialogueData : ScriptableObject
{
    public DialogueExecPortData execInput = new() { label = "In" };
    public DialogueExecPortData execOutput = new() { label = "Out" };
    public bool hasSelection;
    public List<DialogueSelectionOptionData> selectionOptions = new();

    // References to the links that can be followed from this data node.
    // Extension point: add dialogue text, speaker metadata, timing, or presentation settings here.
    public DialogueLink[] transitions;
}

// A standalone branch node: exposes only the selection options, no dialogue content fields.
public class DialogueSelectorData : DialogueData
{
    private void OnEnable() => hasSelection = true;
}
