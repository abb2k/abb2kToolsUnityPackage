using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DialogueSelectionOptionData
{
    public string id = Guid.NewGuid().ToString("N");
    public string text = "Option";
    public DialogueContentValue dialogueContent = new();

    public object ResolveContent(Func<string, object> resolveVariable = null)
    {
        var contentType = DialogueContentProjectSettings.Load()?.DefaultContentType;
        if (contentType == null) return null;

        if (dialogueContent?.ContentType != contentType)
        {
            dialogueContent = new DialogueContentValue();
            dialogueContent.SetType(contentType);
        }

        return dialogueContent.Resolve(resolveVariable);
    }
}

// A single dialogue beat/node. CreateAssetMenu also allows making one from Unity's Project window.
[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptable Objects/DialogueData")]
public class DialogueData : ScriptableObject
{
    public DialogueExecPortData execInput = new() { label = "In" };
    public DialogueExecPortData execOutput = new() { label = "Out" };
    public bool hasSelection;
    public List<DialogueSelectionOptionData> selectionOptions = new();
    public DialogueContentValue dialogueContent = new();
    [SerializeField, HideInInspector] private Vector2 graphNodeSize;
    [SerializeField, HideInInspector] private bool graphNodeSizeWasUserSpecified;

    public Vector2 GraphNodeSize
    {
        get => graphNodeSize;
        set => graphNodeSize = value;
    }

    public bool GraphNodeSizeWasUserSpecified
    {
        get => graphNodeSizeWasUserSpecified;
        set => graphNodeSizeWasUserSpecified = value;
    }

    // References to the links that can be followed from this data node.
    // Extension point: add dialogue text, speaker metadata, timing, or presentation settings here.
    public DialogueLink[] transitions;

    public object ResolveContent(Func<string, object> resolveVariable = null)
    {
        var contentType = DialogueContentProjectSettings.Load()?.DefaultContentType;
        if (contentType == null) return null;

        if (dialogueContent?.ContentType != contentType)
        {
            dialogueContent = new DialogueContentValue();
            dialogueContent.SetType(contentType);
        }

        return dialogueContent.Resolve(resolveVariable);
    }
}

// A standalone branch node: exposes only the selection options, no dialogue content fields.
public class DialogueSelectorData : DialogueData
{
    private void OnEnable() => hasSelection = true;
}
