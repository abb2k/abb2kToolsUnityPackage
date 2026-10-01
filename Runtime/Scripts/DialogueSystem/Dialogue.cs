using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DialogueExecPortData
{
    public string id = Guid.NewGuid().ToString("N");
    public string label = "Exec";
}

[Serializable]
public class DialogueEntryData
{
    public string id = Guid.NewGuid().ToString("N");
    public string name = "Entry";
    public DialogueExecPortData output = new() { label = "Start" };
    public Vector2 position;
}

[Serializable]
public class DialogueExitData
{
    public string id = Guid.NewGuid().ToString("N");
    public string name = "Exit";
    public DialogueExecPortData input = new() { label = "Exit" };
    public Vector2 position;
}

// Root asset for one dialogue graph and the assets that graph displays.
[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptable Objects/Dialogue")]
public class Dialogue : ScriptableObject
{
    public DialogueExecPortData startOutput = new() { label = "Start" };
    public DialogueExecPortData exitInput = new() { label = "Exit" };
    public Vector2 startNodePosition = new(-360f, 0f);
    public Vector2 exitNodePosition = new(520f, 0f);

    // These arrays are the source of truth for which room and transition assets appear in the graph.
    public DialogueData[] rooms;
    public DialogueLink[] links;
    public List<DialogueEntryData> additionalEntries = new();
    public List<DialogueExitData> additionalExits = new();
    public List<DialogueExposedVariable> variables = new();
}


/* Extension ideas:

- Add dialogue text and a structured way to invoke events or insert parameters.
- Add node presentation and playback settings such as color, auto-skip, and text speed.
- Add side dialogue nodes and multi-selection options.

- Add transition behavior for deciding when and how a link is followed.
- Expose configurable parameters and callbacks to game code.
*/