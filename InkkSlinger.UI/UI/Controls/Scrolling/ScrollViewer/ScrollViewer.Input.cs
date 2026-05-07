using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class ScrollViewer
{
    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        return HandlePointerDownFromInput(this, pointerPosition);
    }

    internal bool HandlePointerDownFromInput(UIElement target, Vector2 pointerPosition)
    {
        if (PanningMode == PanningMode.None ||
            !IsEnabled ||
            IsSameOrDescendantOf(target, _horizontalBar) ||
            IsSameOrDescendantOf(target, _verticalBar) ||
            IsWithinButton(target))
        {
            return false;
        }

        _isPointerPanning = true;
        _lastPointerPanPosition = pointerPosition;
        return true;
    }

    internal bool HandlePointerMoveFromInput(Vector2 pointerPosition)
    {
        if (!_isPointerPanning)
        {
            return false;
        }

        var delta = pointerPosition - _lastPointerPanPosition;
        _lastPointerPanPosition = pointerPosition;
        if (AllowsHorizontalPanning(PanningMode) && MathF.Abs(delta.X) > 0.001f)
        {
            ScrollToHorizontalOffset(HorizontalOffset - delta.X);
        }

        if (AllowsVerticalPanning(PanningMode) && MathF.Abs(delta.Y) > 0.001f)
        {
            ScrollToVerticalOffset(VerticalOffset - delta.Y);
        }

        return true;
    }

    internal bool HandlePointerUpFromInput()
    {
        if (!_isPointerPanning)
        {
            return false;
        }

        _isPointerPanning = false;
        _lastPointerPanPosition = Vector2.Zero;
        return true;
    }

    private static bool AllowsHorizontalPanning(PanningMode mode)
    {
        return mode is PanningMode.HorizontalOnly or PanningMode.Both or PanningMode.HorizontalFirst;
    }

    private static bool AllowsVerticalPanning(PanningMode mode)
    {
        return mode is PanningMode.VerticalOnly or PanningMode.Both or PanningMode.VerticalFirst;
    }

    private static bool IsWithinButton(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
    internal bool HandleMouseWheelFromInput(int delta)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagWheelEvents++;
        _runtimeWheelEvents++;
        _diagHandleMouseWheelCallCount++;
        _runtimeHandleMouseWheelCallCount++;
        if (!IsEnabled || delta == 0)
        {
            if (!IsEnabled)
            {
                _diagHandleMouseWheelIgnoredDisabledCount++;
                _runtimeHandleMouseWheelIgnoredDisabledCount++;
            }
            else
            {
                _diagHandleMouseWheelIgnoredZeroDeltaCount++;
                _runtimeHandleMouseWheelIgnoredZeroDeltaCount++;
            }

            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _diagHandleMouseWheelElapsedTicks += elapsedTicks;
            _runtimeHandleMouseWheelElapsedTicks += elapsedTicks;
            return false;
        }

        var beforeHorizontal = HorizontalOffset;
        var beforeVertical = VerticalOffset;
        var amount = DefaultLineScrollStep;
        var direction = delta > 0 ? -1f : 1f;
        var canScrollVertically = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && ScrollableHeight > 0.001f;
        var canScrollHorizontally = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && ScrollableWidth > 0.001f;

        BeginInputScrollMutation();
        try
        {
            if (LogicalScrollInfo is { } scrollInfo)
            {
                if (delta > 0)
                {
                    scrollInfo.MouseWheelUp();
                }
                else
                {
                    scrollInfo.MouseWheelDown();
                }

                SetOffsets(scrollInfo.HorizontalOffset, scrollInfo.VerticalOffset);
            }
            else if (canScrollVertically)
            {
                SetOffsets(HorizontalOffset, VerticalOffset + (direction * amount));
            }
            else if (canScrollHorizontally)
            {
                SetOffsets(HorizontalOffset + (direction * amount), VerticalOffset);
            }
        }
        finally
        {
            EndInputScrollMutation();
        }

        var handled = MathF.Abs(beforeHorizontal - HorizontalOffset) > 0.001f ||
                      MathF.Abs(beforeVertical - VerticalOffset) > 0.001f;
        if (handled)
        {
            _diagWheelHandled++;
            _runtimeWheelHandled++;
            _diagHandleMouseWheelHandledCount++;
            _runtimeHandleMouseWheelHandledCount++;
        }

        var totalElapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagHandleMouseWheelElapsedTicks += totalElapsedTicks;
        _runtimeHandleMouseWheelElapsedTicks += totalElapsedTicks;

        return handled;
    }

}
