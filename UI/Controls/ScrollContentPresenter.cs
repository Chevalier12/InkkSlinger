using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class ScrollContentPresenter : FrameworkElement
{
    private UIElement? _content;

    public UIElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            if (_content != null)
            {
                _content.SetVisualParent(null);
                _content.SetLogicalParent(null);
            }

            _content = value;
            if (_content != null)
            {
                _content.SetVisualParent(this);
                _content.SetLogicalParent(this);
            }

            InvalidateMeasure();
        }
    }

    public bool UseScrollInfo { get; set; }

    public float ExtentWidth { get; private set; }

    public float ExtentHeight { get; private set; }

    public float ViewportWidth { get; private set; }

    public float ViewportHeight { get; private set; }

    public float HorizontalOffset { get; set; }

    public float VerticalOffset { get; set; }

    public IScrollInfo? ScrollInfo => _content as IScrollInfo;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_content != null)
        {
            yield return _content;
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        if (_content != null)
        {
            yield return _content;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (_content is not FrameworkElement content)
        {
            ExtentWidth = 0f;
            ExtentHeight = 0f;
            ViewportWidth = 0f;
            ViewportHeight = 0f;
            return Vector2.Zero;
        }

        var finiteWidth = IsFinitePositive(availableSize.X) ? availableSize.X : float.PositiveInfinity;
        var finiteHeight = IsFinitePositive(availableSize.Y) ? availableSize.Y : float.PositiveInfinity;
        var measureSize = UseScrollInfo
            ? new Vector2(finiteWidth, finiteHeight)
            : new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        content.Measure(measureSize);

        var desired = content.DesiredSize;
        if (UseScrollInfo && content is IScrollInfo scrollInfo)
        {
            ExtentWidth = MathF.Max(0f, scrollInfo.ExtentWidth);
            ExtentHeight = MathF.Max(0f, scrollInfo.ExtentHeight);
        }
        else
        {
            ExtentWidth = MathF.Max(0f, desired.X);
            ExtentHeight = MathF.Max(0f, desired.Y);
        }

        ViewportWidth = IsFinitePositive(availableSize.X) ? MathF.Max(0f, availableSize.X) : ExtentWidth;
        ViewportHeight = IsFinitePositive(availableSize.Y) ? MathF.Max(0f, availableSize.Y) : ExtentHeight;

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_content is FrameworkElement content)
        {
            ViewportWidth = MathF.Max(0f, finalSize.X);
            ViewportHeight = MathF.Max(0f, finalSize.Y);
            var arrangedWidth = UseScrollInfo && content is IScrollInfo
                ? ViewportWidth
                : MathF.Max(ViewportWidth, ExtentWidth);
            var arrangedHeight = UseScrollInfo && content is IScrollInfo
                ? ViewportHeight
                : MathF.Max(ViewportHeight, ExtentHeight);
            content.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, arrangedWidth, arrangedHeight));
        }
        else
        {
            ViewportWidth = MathF.Max(0f, finalSize.X);
            ViewportHeight = MathF.Max(0f, finalSize.Y);
        }

        return finalSize;
    }

    protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
    {
        if (HorizontalOffset == 0f && VerticalOffset == 0f)
        {
            transform = Matrix.Identity;
            inverseTransform = Matrix.Identity;
            return false;
        }

        transform = Matrix.CreateTranslation(-HorizontalOffset, -VerticalOffset, 0f);
        inverseTransform = Matrix.CreateTranslation(HorizontalOffset, VerticalOffset, 0f);
        return true;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
