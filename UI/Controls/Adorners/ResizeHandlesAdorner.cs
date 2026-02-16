using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public enum ResizeHandlePosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public sealed class ResizeHandleDragEventArgs : EventArgs
{
    public ResizeHandleDragEventArgs(ResizeHandlePosition handle, float horizontalChange, float verticalChange)
    {
        Handle = handle;
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }

    public ResizeHandlePosition Handle { get; }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }
}

public class ResizeHandlesAdorner : Adorner
{
    private readonly Dictionary<ResizeHandlePosition, Thumb> _thumbs = new();

    public static readonly DependencyProperty HandleSizeProperty =
        DependencyProperty.Register(
            nameof(HandleSize),
            typeof(float),
            typeof(ResizeHandlesAdorner),
            new FrameworkPropertyMetadata(
                8f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 4f ? v : 4f));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Color),
            typeof(ResizeHandlesAdorner),
            new FrameworkPropertyMetadata(new Color(117, 190, 255), FrameworkPropertyMetadataOptions.AffectsRender));

    public ResizeHandlesAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = true;

    }

    public event EventHandler<ResizeHandleDragEventArgs>? HandleDragDelta;

    public float HandleSize
    {
        get => GetValue<float>(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }

    public Color Stroke
    {
        get => GetValue<Color>(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    internal IReadOnlyDictionary<ResizeHandlePosition, Thumb> HandlesForTesting => _thumbs;

    internal void RaiseHandleDragForTesting(ResizeHandlePosition handle, float dx, float dy)
    {
        HandleDragDelta?.Invoke(this, new ResizeHandleDragEventArgs(handle, dx, dy));
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in _thumbs.Values)
        {
            yield return child;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in _thumbs.Values)
        {
            yield return child;
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

        // Only handles are interactive. Rectangle area should pass through.
        return false;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var bounds = AdornedElement.LayoutSlot;
        var size = HandleSize;

        foreach (var thumb in _thumbs.Values)
        {
            thumb.Width = size;
            thumb.Height = size;
            thumb.Measure(new Vector2(size, size));
        }

        return new Vector2(bounds.Width + size, bounds.Height + size);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var bounds = AdornedElement.LayoutSlot;
        var half = HandleSize / 2f;

        ArrangeThumb(ResizeHandlePosition.TopLeft, bounds.X - half, bounds.Y - half);
        ArrangeThumb(ResizeHandlePosition.TopRight, bounds.X + bounds.Width - half, bounds.Y - half);
        ArrangeThumb(ResizeHandlePosition.BottomLeft, bounds.X - half, bounds.Y + bounds.Height - half);
        ArrangeThumb(ResizeHandlePosition.BottomRight, bounds.X + bounds.Width - half, bounds.Y + bounds.Height - half);

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawRectStroke(spriteBatch, AdornedElement.LayoutSlot, 1f, Stroke, Opacity);
    }


    private void ArrangeThumb(ResizeHandlePosition position, float x, float y)
    {
        var thumb = _thumbs[position];
        thumb.Arrange(new LayoutRect(x, y, HandleSize, HandleSize));
    }
}
