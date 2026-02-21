using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public abstract class HandlesAdornerBase : AnchoredAdorner
{
    private readonly Dictionary<HandleKind, Thumb> _thumbs = new();
    private readonly Dictionary<HandleKind, EventHandler<DragDeltaEventArgs>> _thumbHandlers = new();

    protected HandlesAdornerBase(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = true;
    }

    public virtual float HandleSize { get; set; } = 8f;

    public Color HandleFill { get; set; } = new Color(88, 88, 88);

    public Color HandleStroke { get; set; } = new Color(164, 164, 164);

    public Color Stroke { get; set; } = new Color(117, 190, 255);

    public float StrokeThickness { get; set; } = 1f;

    public event EventHandler<HandleDragDeltaEventArgs>? HandleDragDelta;

    protected virtual HandleSet Handles => HandleSet.Corners;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var thumb in _thumbs.Values)
        {
            yield return thumb;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var thumb in _thumbs.Values)
        {
            yield return thumb;
        }
    }

    public override bool HitTest(Vector2 point)
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        if (!IsPointVisibleThroughClipChain(point))
        {
            return false;
        }

        foreach (var thumb in _thumbs.Values)
        {
            if (thumb.HitTest(point))
            {
                return true;
            }
        }

        return false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        EnsureThumbSet();
        var size = MathF.Max(4f, HandleSize);
        foreach (var thumb in _thumbs.Values)
        {
            thumb.Width = size;
            thumb.Height = size;
            thumb.Background = HandleFill;
            thumb.BorderBrush = HandleStroke;
            thumb.Measure(new Vector2(size, size));
        }

        var rect = LayoutSlot;
        return new Vector2(MathF.Max(0f, rect.Width + size), MathF.Max(0f, rect.Height + size));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        EnsureThumbSet();
        var bounds = LayoutSlot;
        var size = MathF.Max(4f, HandleSize);
        var half = size * 0.5f;

        ArrangeIfPresent(HandleKind.TopLeft, bounds.X - half, bounds.Y - half, size);
        ArrangeIfPresent(HandleKind.Top, bounds.X + (bounds.Width * 0.5f) - half, bounds.Y - half, size);
        ArrangeIfPresent(HandleKind.TopRight, bounds.X + bounds.Width - half, bounds.Y - half, size);
        ArrangeIfPresent(HandleKind.Left, bounds.X - half, bounds.Y + (bounds.Height * 0.5f) - half, size);
        ArrangeIfPresent(HandleKind.Right, bounds.X + bounds.Width - half, bounds.Y + (bounds.Height * 0.5f) - half, size);
        ArrangeIfPresent(HandleKind.BottomLeft, bounds.X - half, bounds.Y + bounds.Height - half, size);
        ArrangeIfPresent(HandleKind.Bottom, bounds.X + (bounds.Width * 0.5f) - half, bounds.Y + bounds.Height - half, size);
        ArrangeIfPresent(HandleKind.BottomRight, bounds.X + bounds.Width - half, bounds.Y + bounds.Height - half, size);

        return finalSize;
    }

    protected override void OnRenderAdorner(SpriteBatch spriteBatch, LayoutRect rect)
    {
        if (StrokeThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, rect, StrokeThickness, Stroke, Opacity);
        }
    }

    protected virtual void OnHandleDrag(HandleKind handle, float horizontalChange, float verticalChange)
    {
    }

    protected virtual (float HorizontalChange, float VerticalChange) TransformHandleDragDelta(
        HandleKind handle,
        float horizontalChange,
        float verticalChange)
    {
        return (horizontalChange, verticalChange);
    }

    private void EnsureThumbSet()
    {
        var desired = new HashSet<HandleKind>(EnumerateHandleKinds(Handles));

        var stale = new List<HandleKind>();
        foreach (var existing in _thumbs.Keys)
        {
            if (!desired.Contains(existing))
            {
                stale.Add(existing);
            }
        }

        foreach (var kind in stale)
        {
            RemoveThumb(kind);
        }

        foreach (var kind in desired)
        {
            if (_thumbs.ContainsKey(kind))
            {
                continue;
            }

            AddThumb(kind);
        }
    }

    private void AddThumb(HandleKind kind)
    {
        var thumb = new Thumb();
        thumb.SetVisualParent(this);
        thumb.SetLogicalParent(this);

        EventHandler<DragDeltaEventArgs> handler = (_, args) =>
        {
            var (horizontalChange, verticalChange) = TransformHandleDragDelta(kind, args.HorizontalChange, args.VerticalChange);
            OnHandleDrag(kind, horizontalChange, verticalChange);
            HandleDragDelta?.Invoke(this, new HandleDragDeltaEventArgs(kind, horizontalChange, verticalChange));
        };

        thumb.DragDelta += handler;
        _thumbHandlers[kind] = handler;
        _thumbs[kind] = thumb;
    }

    private void RemoveThumb(HandleKind kind)
    {
        if (!_thumbs.TryGetValue(kind, out var thumb))
        {
            return;
        }

        if (_thumbHandlers.TryGetValue(kind, out var handler))
        {
            thumb.DragDelta -= handler;
            _thumbHandlers.Remove(kind);
        }

        thumb.SetVisualParent(null);
        thumb.SetLogicalParent(null);
        _thumbs.Remove(kind);
    }

    private void ArrangeIfPresent(HandleKind kind, float x, float y, float size)
    {
        if (!_thumbs.TryGetValue(kind, out var thumb))
        {
            return;
        }

        thumb.Arrange(new LayoutRect(x, y, size, size));
    }

    private static IEnumerable<HandleKind> EnumerateHandleKinds(HandleSet set)
    {
        if ((set & HandleSet.TopLeft) != 0)
        {
            yield return HandleKind.TopLeft;
        }

        if ((set & HandleSet.Top) != 0)
        {
            yield return HandleKind.Top;
        }

        if ((set & HandleSet.TopRight) != 0)
        {
            yield return HandleKind.TopRight;
        }

        if ((set & HandleSet.Left) != 0)
        {
            yield return HandleKind.Left;
        }

        if ((set & HandleSet.Right) != 0)
        {
            yield return HandleKind.Right;
        }

        if ((set & HandleSet.BottomLeft) != 0)
        {
            yield return HandleKind.BottomLeft;
        }

        if ((set & HandleSet.Bottom) != 0)
        {
            yield return HandleKind.Bottom;
        }

        if ((set & HandleSet.BottomRight) != 0)
        {
            yield return HandleKind.BottomRight;
        }
    }
}
