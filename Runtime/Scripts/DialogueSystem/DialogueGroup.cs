using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using System;

// GraphView group whose title, bounds, and members are persisted by DialogueGraphLayout.
public class DialogueGroup : Group
{
    public readonly DialogueGraphLayout.GroupData data;
    private readonly Action<Vector2> _onMoved;
    private readonly Action<bool> _onMovingChanged;

    public DialogueGroup(DialogueGraphLayout.GroupData data, Action<Vector2> onMoved, Action<bool> onMovingChanged)
    {
        // The data object is the saved representation; this Group is only its current visual.
        this.data = data;
        _onMoved = onMoved;
        _onMovingChanged = onMovingChanged;
        title = data.title;
    }

    public override void SetPosition(Rect newPos)
    {
        // Group changes can come from dragging or resizing, so save both values on every update.
        var delta = newPos.position - data.position;
        bool isMoving = delta != Vector2.zero;
        if (isMoving)
            _onMovingChanged?.Invoke(true);

        try
        {
            base.SetPosition(newPos);
            if (data.position == newPos.position && data.size == newPos.size) return;

            data.position = newPos.position;
            data.size = newPos.size;
            if (isMoving)
                _onMoved?.Invoke(delta);
            DialogueGraphLayout.instance.MarkDirty();
        }
        finally
        {
            if (isMoving)
                _onMovingChanged?.Invoke(false);
        }
    }
}
