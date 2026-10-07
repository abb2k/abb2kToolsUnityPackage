using System;
using UnityEngine;

[Serializable]
[DialogueContent]
public class DefaultDialogueContent
{
	[DialoguePresetOverride] public string name;
	[TextArea(3, 3)] public string text;
	[DialoguePresetOverride] public Sprite characterIcon;
}
