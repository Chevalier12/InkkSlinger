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

    public static bool GetIsTransformContentLayerStable(UIElement element)
    {
        return element.GetValue<bool>(IsTransformContentLayerStableProperty);
    }

    public static void SetIsTransformContentLayerStable(UIElement element, bool value)
    {
        element.SetValue(IsTransformContentLayerStableProperty, value);
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
        if (TryRefreshTransformContentScrollInfo())
        {
            return;
        }

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

    private bool TryRefreshTransformContentScrollInfo()
    {
        if (!_contentPresenter.TryGetTransformContentExtent(out var extentWidth, out var extentHeight))
        {
            return false;
        }

        var previousHorizontalOffset = HorizontalOffset;
        var previousVerticalOffset = VerticalOffset;
        var metricsChanged = ApplyScrollMetrics(
            extentWidth,
            extentHeight,
            ViewportWidth,
            ViewportHeight,
            publishViewportMetrics: true);
        var scrollBarVisibilityChanged = metricsChanged && HasTransformContentScrollBarVisibilityChange();
        var offsetsChanged = CoerceOffsetsToCurrentMetrics(closeAnchoredPopups: false);
        UpdateScrollBars();

        if (metricsChanged || offsetsChanged)
        {
            ViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        if (scrollBarVisibilityChanged)
        {
            InvalidateArrangeForDirectLayoutOnly();
            return true;
        }

        if (offsetsChanged)
        {
            ApplyOffsetToContent(
                previousHorizontalOffset,
                HorizontalOffset,
                previousVerticalOffset,
                VerticalOffset,
                canApplyOffsetWithoutDeferredLayout: true);
        }
        else if (TryGetContentViewportClipRect(out var contentViewport))
        {
            NotifyTransformContentRenderChanged(contentViewport);
        }

        return true;
    }

    private bool HasTransformContentScrollBarVisibilityChange()
    {
        var border = MathF.Max(0f, BorderThickness);
        var bounds = new LayoutRect(
            LayoutSlot.X + border,
            LayoutSlot.Y + border,
            MathF.Max(0f, LayoutSlot.Width - (border * 2f)),
            MathF.Max(0f, LayoutSlot.Height - (border * 2f)));
        var horizontalBarThickness = ResolveHorizontalBarThicknessForLayout();
        var verticalBarThickness = ResolveVerticalBarThicknessForLayout();
        var showHorizontal = ResolveInitialHorizontalScrollBarVisibility();
        var showVertical = ResolveInitialVerticalScrollBarVisibility();
        for (var i = 0; i < 3; i++)
        {
            var viewportWidth = MathF.Max(0f, bounds.Width - GetVerticalBarReservation(showVertical, verticalBarThickness));
            var viewportHeight = MathF.Max(0f, bounds.Height - GetHorizontalBarReservation(showHorizontal, horizontalBarThickness));
            var nextShowHorizontal = HorizontalScrollBarVisibility == ScrollBarVisibility.Auto
                ? ExtentWidth > viewportWidth + 0.01f
                : showHorizontal;
            var nextShowVertical = VerticalScrollBarVisibility == ScrollBarVisibility.Auto
                ? ExtentHeight > viewportHeight + 0.01f
                : showVertical;

            if (nextShowHorizontal == showHorizontal && nextShowVertical == showVertical)
            {
                break;
            }

            showHorizontal = nextShowHorizontal;
            showVertical = nextShowVertical;
        }

        return showHorizontal != _showHorizontalBar ||
               showVertical != _showVerticalBar;
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

    internal bool HasTransformStableLayerContent()
    {
        return ContentElement is UIElement contentElement &&
               _contentPresenter.UsesTransformBasedScrolling() &&
               GetIsTransformContentLayerStable(contentElement);
    }

}
