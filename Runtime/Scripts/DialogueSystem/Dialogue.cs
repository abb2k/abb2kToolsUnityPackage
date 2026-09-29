using UnityEngine;

// Root asset for one dialogue graph and the assets that graph displays.
[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptable Objects/Dialogue")]
public class Dialogue : ScriptableObject
{
    // These arrays are the source of truth for which room and transition assets appear in the graph.
    public DialogueData[] rooms;
    public DialogueLink[] links;
}


/* Extension ideas:

- Add dialogue text and a structured way to invoke events or insert parameters.
- Add node presentation and playback settings such as color, auto-skip, and text speed.
- Add side dialogue nodes and multi-selection options.

- Add transition behavior for deciding when and how a link is followed.
- Expose configurable parameters and callbacks to game code.
*/