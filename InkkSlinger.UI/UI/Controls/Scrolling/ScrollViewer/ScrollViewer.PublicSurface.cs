using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return _isReconcilingDescendantMeasureInvalidation && IsContentDescendant(descendant);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        AttachTemplatePartPresenter(GetTemplateChild("PART_ScrollContentPresenter") as ScrollContentPresenter);
        AttachTemplateScrollBars(
            GetTemplateChild("PART_HorizontalScrollBar") as ScrollBar,
            GetTemplateChild("PART_VerticalScrollBar") as ScrollBar);
    }

    public new Color Background
    {
        get => ResolveBackgroundColor();
        set => SetValue(BackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => ResolveBorderBrushColor();
        set => SetValue(BorderBrushProperty, value);
    }

    public new float BorderThickness
    {
        get => ResolveBorderThicknessValue();
        set => SetValue(BorderThicknessProperty, value);
    }

    public static bool GetUseTransformContentScrolling(UIElement element)
    {
        return element.GetValue<bool>(UseTransformContentScrollingProperty);
    }

    public static void SetUseTransformContentScrolling(UIElement element, bool value)
    {
        element.SetValue(UseTransformContentScrollingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (_showHorizontalBar)
        {
            yield return _horizontalBar;
        }

        if (_showVerticalBar)
        {
            yield return _verticalBar;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() +
               (_showHorizontalBar ? 1 : 0) +
               (_showVerticalBar ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        var extraIndex = index - baseCount;
        if (_showHorizontalBar)
        {
            if (extraIndex == 0)
            {
                return _horizontalBar;
            }

            extraIndex--;
        }

        if (_showVerticalBar && extraIndex == 0)
        {
            return _verticalBar;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    internal ScrollBar AutomationVerticalScrollBar => _verticalBar;

    internal ScrollBar AutomationHorizontalScrollBar => _horizontalBar;

    internal FrameworkElement? ContentElementForScrollPresenter => ContentElement as FrameworkElement;

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (_showHorizontalBar)
        {
            yield return _horizontalBar;
        }

        if (_showVerticalBar)
        {
            yield return _verticalBar;
        }
    }

    public void ScrollToHorizontalOffset(float offset)
    {
        _diagScrollToHorizontalOffsetCallCount++;
        _runtimeScrollToHorizontalOffsetCallCount++;
        SetOffsets(offset, VerticalOffset);
    }

    public void ScrollToVerticalOffset(float offset)
    {
        _diagScrollToVerticalOffsetCallCount++;
        _runtimeScrollToVerticalOffsetCallCount++;
        SetOffsets(HorizontalOffset, offset);
    }

    public void InvalidateScrollInfo()
    {
        _diagInvalidateScrollInfoCallCount++;
        _runtimeInvalidateScrollInfoCallCount++;
        if (LogicalScrollInfo is { } scrollInfo)
        {
            _ = ApplyScrollMetrics(
                scrollInfo.ExtentWidth,
                scrollInfo.ExtentHeight,
                scrollInfo.ViewportWidth,
                scrollInfo.ViewportHeight,
                publishViewportMetrics: true);
            CoerceOffsetsToCurrentMetrics(closeAnchoredPopups: false);
            UpdateScrollBars();
        }

        InvalidateMeasure();
    }

    public bool MakeVisible(UIElement child, LayoutRect rectangle)
    {
        if (!IsContentDescendant(child))
        {
            return false;
        }

        if (LogicalScrollInfo is { } scrollInfo)
        {
            var visibleRect = scrollInfo.MakeVisible(child, rectangle);
            SetOffsets(scrollInfo.HorizontalOffset, scrollInfo.VerticalOffset);
            _ = visibleRect;
            return true;
        }

        var target = child.TransformRectToRoot(rectangle);
        var viewport = TransformRectToRoot(_contentViewportRect);
        var nextHorizontal = HorizontalOffset;
        var nextVertical = VerticalOffset;
        if (target.X < viewport.X)
        {
            nextHorizontal -= viewport.X - target.X;
        }
        else if (target.X + target.Width > viewport.X + viewport.Width)
        {
            nextHorizontal += target.X + target.Width - (viewport.X + viewport.Width);
        }

        if (target.Y < viewport.Y)
        {
            nextVertical -= viewport.Y - target.Y;
        }
        else if (target.Y + target.Height > viewport.Y + viewport.Height)
        {
            nextVertical += target.Y + target.Height - (viewport.Y + viewport.Height);
        }

        SetOffsets(nextHorizontal, nextVertical);
        return true;
    }

    internal bool TryGetContentViewportClipRect(out LayoutRect clipRect)
    {
        clipRect = _contentViewportRect;
        return clipRect.Width > 0f && clipRect.Height > 0f;
    }

    internal bool ShouldUseTransformScrollViewportDirtyHint()
    {
        var uiRoot = UiRoot.Current;
        if (uiRoot == null)
        {
            return false;
        }

        return _lastTransformScrollDirtyHintDrawCount >= uiRoot.DrawExecutedFrameCount;
    }

}
