using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System;

// GraphView group whose title, bounds, and members are persisted by DialogueGraphLayout.
public class DialogueGroup : Group
{
    public readonly DialogueGraphLayout.GroupData data;
    private readonly Action<Vector2> _onMoved;

    public DialogueGroup(DialogueGraphLayout.GroupData data, Action<Vector2> onMoved)
    {
        // The data object is the saved representation; this Group is only its current visual.
        this.data = data;
        _onMoved = onMoved;
        title = data.title;
    }

    public override void SetPosition(Rect newPos)
    {
        // Group changes can come from dragging or resizing, so save both values on every update.
        base.SetPosition(newPos);
        if (data.position == newPos.position && data.size == newPos.size) return;

        var delta = newPos.position - data.position;
        data.position = newPos.position;
        data.size = newPos.size;
        if (delta != Vector2.zero)
            _onMoved?.Invoke(delta);
        DialogueGraphLayout.instance.MarkDirty();
    }
}
