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
	public string id = Guid.NewGuid().ToString("N");
    public Vector2 position;
    public DialogueCurvePointMode mode = DialogueCurvePointMode.Eased;
}

public enum DialogueConditionValueSource
{
	Variable,
	Method
}

public enum DialogueConditionComparison
{
	Equal,
	NotEqual,
	Greater,
	GreaterOrEqual,
	Less,
	LessOrEqual
}

[Serializable]
public class DialogueTransitionCondition
{
	public string variableId;
	public DialogueConditionValueSource valueSource;
	public string methodSignature;
	public List<string> methodArguments = new();
	public List<UnityEngine.Object> methodObjectArguments = new();
	public DialogueConditionComparison comparison;
	public string expectedValue;
	public UnityEngine.Object expectedObject;
	public bool useTypedValues;
	public DialogueExposedVariable typedExpectedValue = new();
	public List<DialogueExposedVariable> typedMethodArguments = new();
}

// A reusable transition target between DialogueData nodes.
public class DialogueLink : ScriptableObject
{
	public DialogueData source;
	public string sourcePortId;
	// The room this transition leads to; a null value represents an incomplete transition.
	public DialogueData destination;
	public string destinationPortId;
	public bool sourceIsDialogueStart;
	public bool destinationIsDialogueExit;
	public string sourceEntryId;
	public string destinationExitId;
	public List<DialogueCurvePoint> curvePoints = new();
	public List<DialogueTransitionCondition> conditions = new();
}
