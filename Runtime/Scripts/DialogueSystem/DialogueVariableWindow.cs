using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DialogueVariableWindow : VisualElement
{
    private readonly VisualElement _host;
    private readonly Action<Vector2> _positionChanged;
    private readonly Action<Vector2> _sizeChanged;
    private readonly Action _close;
    private bool _dragging;
    private bool _resizing;
    private Vector2 _pointerStart;
    private Vector2 _positionStart;
    private Vector2 _sizeStart;

    public DialogueVariableWindow(
        Dialogue dialogue,
        VisualElement host,
        Vector2 position,
        Vector2 size,
        Action<Vector2> positionChanged,
        Action<Vector2> sizeChanged,
        Action close,
        Action<bool> variablesChanged)
    {
        _host = host;
        _positionChanged = positionChanged;
        _sizeChanged = sizeChanged;
        _close = close;

        style.position = Position.Absolute;
        style.left = position.x;
        style.top = position.y;
        style.width = Mathf.Clamp(size.x, 300, 520);
        style.height = Mathf.Clamp(size.y, 320, 640);
        style.flexDirection = FlexDirection.Column;
        style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.98f);
        style.borderLeftWidth = 1;
        style.borderRightWidth = 1;
        style.borderTopWidth = 1;
        style.borderBottomWidth = 1;
        style.borderLeftColor = new Color(0.05f, 0.05f, 0.05f);
        style.borderRightColor = new Color(0.05f, 0.05f, 0.05f);
        style.borderTopColor = new Color(0.05f, 0.05f, 0.05f);
        style.borderBottomColor = new Color(0.05f, 0.05f, 0.05f);

        var header = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 24 } };
        header.Add(new Label($"Variables - {dialogue.name}") { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } });
        header.Add(new Button(() => _close?.Invoke()) { text = "x" });
        header.RegisterCallback<MouseDownEvent>(OnHeaderMouseDown);
        header.RegisterCallback<MouseMoveEvent>(OnPointerMove);
        header.RegisterCallback<MouseUpEvent>(OnPointerUp);
        Add(header);

        var board = new DialogueVariableBoard(dialogue, variablesChanged);
        board.style.flexGrow = 1;
        board.style.minHeight = 0;
        Add(board);

        var resizeHandle = new Label("Resize")
        {
            style =
            {
                height = 18,
                unityTextAlign = TextAnchor.MiddleRight,
                unityFontStyleAndWeight = FontStyle.Italic
            }
        };
        resizeHandle.RegisterCallback<MouseDownEvent>(OnResizeMouseDown);
        resizeHandle.RegisterCallback<MouseMoveEvent>(OnPointerMove);
        resizeHandle.RegisterCallback<MouseUpEvent>(OnPointerUp);
        Add(resizeHandle);
        _host.RegisterCallback<GeometryChangedEvent>(OnHostGeometryChanged);
        schedule.Execute(ClampToHost).ExecuteLater(0);
    }

    private void OnHostGeometryChanged(GeometryChangedEvent evt) => ClampToHost();

    private void ClampToHost()
    {
        if (_host.contentRect.width <= 0 || _host.contentRect.height <= 0) return;

        float maxWidth = Mathf.Max(300, _host.contentRect.width * 0.6f);
        float maxHeight = Mathf.Max(320, _host.contentRect.height * 0.8f);
        float width = Mathf.Min(resolvedStyle.width, maxWidth, Mathf.Max(300, _host.contentRect.width));
        float height = Mathf.Min(resolvedStyle.height, maxHeight, Mathf.Max(320, _host.contentRect.height - 24));
        float left = Mathf.Clamp(resolvedStyle.left, 0, Mathf.Max(0, _host.contentRect.width - width));
        float top = Mathf.Clamp(resolvedStyle.top, 24, Mathf.Max(24, _host.contentRect.height - height));
        style.width = width;
        style.height = height;
        style.left = left;
        style.top = top;
        _positionChanged?.Invoke(new Vector2(left, top));
        _sizeChanged?.Invoke(new Vector2(width, height));
    }

    private void OnHeaderMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || evt.target is Button) return;
        _dragging = true;
        _pointerStart = _host.WorldToLocal(evt.mousePosition);
        _positionStart = new Vector2(resolvedStyle.left, resolvedStyle.top);
        ((VisualElement)evt.currentTarget).CaptureMouse();
        evt.StopPropagation();
    }

    private void OnResizeMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0) return;
        _resizing = true;
        _pointerStart = _host.WorldToLocal(evt.mousePosition);
        _sizeStart = new Vector2(resolvedStyle.width, resolvedStyle.height);
        ((VisualElement)evt.currentTarget).CaptureMouse();
        evt.StopPropagation();
    }

    private void OnPointerMove(MouseMoveEvent evt)
    {
        if (!_dragging && !_resizing) return;
        var pointer = _host.WorldToLocal(evt.mousePosition);
        var delta = pointer - _pointerStart;
        if (_dragging)
        {
            float maxX = Mathf.Max(0, _host.contentRect.width - resolvedStyle.width);
            float maxY = Mathf.Max(24, _host.contentRect.height - resolvedStyle.height);
            var position = new Vector2(
                Mathf.Clamp(_positionStart.x + delta.x, 0, maxX),
                Mathf.Clamp(_positionStart.y + delta.y, 24, maxY));
            style.left = position.x;
            style.top = position.y;
            _positionChanged?.Invoke(position);
        }
        else
        {
            var size = new Vector2(
                Mathf.Clamp(_sizeStart.x + delta.x, 300, Mathf.Max(300, Mathf.Min(520, _host.contentRect.width * 0.6f, _host.contentRect.width - resolvedStyle.left))),
                Mathf.Clamp(_sizeStart.y + delta.y, 320, Mathf.Max(320, Mathf.Min(640, _host.contentRect.height * 0.8f, _host.contentRect.height - resolvedStyle.top))));
            style.width = size.x;
            style.height = size.y;
            _sizeChanged?.Invoke(size);
        }
        evt.StopPropagation();
    }

    private void OnPointerUp(MouseUpEvent evt)
    {
        if (evt.button != 0) return;
        _dragging = false;
        _resizing = false;
        if (evt.currentTarget is VisualElement currentTarget && currentTarget.HasMouseCapture())
            currentTarget.ReleaseMouse();
        evt.StopPropagation();
    }
}