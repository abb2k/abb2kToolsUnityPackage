using UnityEngine;

// A single dialogue beat/node. CreateAssetMenu also allows making one from Unity's Project window.
[CreateAssetMenu(fileName = "DialogueData", menuName = "Scriptable Objects/DialogueData")]
public class DialogueData : ScriptableObject
{
    // References to the links that can be followed from this data node.
    // Extension point: add dialogue text, speaker metadata, timing, or presentation settings here.
    public DialogueLink[] transitions;
}
