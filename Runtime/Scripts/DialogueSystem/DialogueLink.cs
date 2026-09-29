using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueCurvePointMode
{
    Linear,
    Eased
}

[Serializable]
public class DialogueCurvePoint
{
    public Vector2 position;
    public DialogueCurvePointMode mode = DialogueCurvePointMode.Eased;
}

// A reusable transition target between DialogueData nodes.
public class DialogueLink : ScriptableObject
{
	// The room this transition leads to; a null value represents an incomplete transition.
	public DialogueData destination;
	public List<DialogueCurvePoint> curvePoints = new();

	// Extension point: add conditions or transition behavior data when runtime logic is introduced.
}
