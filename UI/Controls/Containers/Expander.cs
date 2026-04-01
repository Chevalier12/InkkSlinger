using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class Expander : ContentControl
{
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagHeaderMeasureCount;
    private static long _diagHeaderMeasureElapsedTicks;
    private static long _diagHeaderMeasureElementPathCount;
    private static long _diagHeaderMeasureTextPathCount;
    private static long _diagHeaderMeasureEmptyTextCount;
    private static long _diagContentMeasuredWhenExpandedCount;
    private static long _diagContentMeasuredWhenCollapsedCount;
    private static long _diagContentMeasureSkippedWithoutContentCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeHeaderMeasureCacheHitCount;
    private static long _diagArrangeHeaderMeasureCacheMissCount;
    private static long _diagArrangeExpandedContentCount;
    private static long _diagArrangeCollapsedContentCount;
    private static long _diagArrangeNoContentCount;
    private static long _diagExpandDirectionDownCount;
    private static long _diagExpandDirectionUpCount;
    private static long _diagExpandDirectionLeftCount;
    private static long _diagExpandDirectionRightCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagHeaderBackgroundStyleOverrideCount;
    private static long _diagHeaderBackgroundInheritedCount;
    private static long _diagRenderHeaderTextCount;
    private static long _diagRenderHeaderTextSkippedEmptyCount;
    private static long _diagRenderHeaderElementCount;
    private static long _diagExpandCount;
    private static long _diagCollapseCount;
    private static long _diagHeaderPointerDownCount;
    private static long _diagHeaderPointerDownMissCount;
    private static long _diagHeaderPointerUpToggleCount;
    private static long _diagHeaderPointerUpMissCount;
    private static long _diagHeaderPointerUpReleaseOutsideCount;
    private static long _diagHeaderUpdateCount;
    public static readonly RoutedEvent ExpandedEvent =
        new(nameof(Expanded), RoutingStrategy.Bubble);

    public static readonly RoutedEvent CollapsedEvent =
        new(nameof(Collapsed), RoutingStrategy.Bubble);

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) =>
                {
                    if (d is Expander expander)
                    {
                        expander.UpdateHeaderElement(args.OldValue, args.NewValue);
                    }
                }));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) =>
                {
                    if (d is Expander expander && args.NewValue is bool expanded)
                    {
                        if (expanded)
                        {
                            IncrementAggregate(ref _diagExpandCount);
                            expander._runtimeExpandCount++;
                        }
                        else
                        {
                            IncrementAggregate(ref _diagCollapseCount);
                            expander._runtimeCollapseCount++;
                        }

                        expander.RaiseRoutedEvent(
                            expanded ? ExpandedEvent : CollapsedEvent,
                            new RoutedSimpleEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
                        UiRoot.Current?.NotifyVisualStructureChanged(expander, expander.VisualParent, expander.VisualParent);
                    }
                }));

    public static readonly DependencyProperty ExpandDirectionProperty =
        DependencyProperty.Register(
            nameof(ExpandDirection),
            typeof(ExpandDirection),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                ExpandDirection.Down,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(HeaderBackground),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(38, 38, 38), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Expander),
            new FrameworkPropertyMetadata(new Color(94, 94, 94), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                new Thickness(8f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderPaddingProperty =
        DependencyProperty.Register(
            nameof(HeaderPadding),
            typeof(Thickness),
            typeof(Expander),
            new FrameworkPropertyMetadata(
                new Thickness(10f, 6f, 10f, 6f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private UIElement? _headerElement;
    private LayoutRect _headerRect;
    private LayoutRect _contentRect;
    private Vector2 _measuredHeaderSize;
    private bool _isHeaderPressed;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeHeaderMeasureCount;
    private long _runtimeHeaderMeasureElapsedTicks;
    private long _runtimeHeaderMeasureElementPathCount;
    private long _runtimeHeaderMeasureTextPathCount;
    private long _runtimeHeaderMeasureEmptyTextCount;
    private long _runtimeContentMeasuredWhenExpandedCount;
    private long _runtimeContentMeasuredWhenCollapsedCount;
    private long _runtimeContentMeasureSkippedWithoutContentCount;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeHeaderMeasureCacheHitCount;
    private long _runtimeArrangeHeaderMeasureCacheMissCount;
    private long _runtimeArrangeExpandedContentCount;
    private long _runtimeArrangeCollapsedContentCount;
    private long _runtimeArrangeNoContentCount;
    private long _runtimeExpandDirectionDownCount;
    private long _runtimeExpandDirectionUpCount;
    private long _runtimeExpandDirectionLeftCount;
    private long _runtimeExpandDirectionRightCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeHeaderBackgroundStyleOverrideCount;
    private long _runtimeHeaderBackgroundInheritedCount;
    private long _runtimeRenderHeaderTextCount;
    private long _runtimeRenderHeaderTextSkippedEmptyCount;
    private long _runtimeRenderHeaderElementCount;
    private long _runtimeExpandCount;
    private long _runtimeCollapseCount;
    private long _runtimeHeaderPointerDownCount;
    private long _runtimeHeaderPointerDownMissCount;
    private long _runtimeHeaderPointerUpToggleCount;
    private long _runtimeHeaderPointerUpMissCount;
    private long _runtimeHeaderPointerUpReleaseOutsideCount;
    private long _runtimeHeaderUpdateCount;
    private long _runtimeHeaderUpdateAttachElementCount;
    private long _runtimeHeaderUpdateDetachElementCount;
    private long _runtimeHeaderUpdateTextHeaderCount;

    public Expander()
    {
    }

    public event EventHandler<RoutedSimpleEventArgs> Expanded
    {
        add => AddHandler(ExpandedEvent, value);
        remove => RemoveHandler(ExpandedEvent, value);
    }

    public event EventHandler<RoutedSimpleEventArgs> Collapsed
    {
        add => AddHandler(CollapsedEvent, value);
        remove => RemoveHandler(CollapsedEvent, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ExpandDirection ExpandDirection
    {
        get => GetValue<ExpandDirection>(ExpandDirectionProperty);
        set => SetValue(ExpandDirectionProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color HeaderBackground
    {
        get => GetValue<Color>(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Thickness HeaderPadding
    {
        get => GetValue<Thickness>(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            if (!IsExpanded && ContentElement != null && ReferenceEquals(element, ContentElement))
            {
                continue;
            }

            yield return element;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        var count = 0;
        var baseCount = base.GetVisualChildCountForTraversal();
        for (var i = 0; i < baseCount; i++)
        {
            var child = base.GetVisualChildAtForTraversal(i);
            if (!IsExpanded && ContentElement != null && ReferenceEquals(child, ContentElement))
            {
                continue;
            }

            count++;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            count++;
        }

        return count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var traversalIndex = 0;
        var baseCount = base.GetVisualChildCountForTraversal();
        for (var i = 0; i < baseCount; i++)
        {
            var child = base.GetVisualChildAtForTraversal(i);
            if (!IsExpanded && ContentElement != null && ReferenceEquals(child, ContentElement))
            {
                continue;
            }

            if (traversalIndex == index)
            {
                return child;
            }

            traversalIndex++;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement) && traversalIndex == index)
        {
            return _headerElement;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (_headerElement != null && !ReferenceEquals(_headerElement, ContentElement))
        {
            yield return _headerElement;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var headerSize = MeasureHeader(availableSize);
        _measuredHeaderSize = headerSize;
        var contentSize = Vector2.Zero;

        if (IsExpanded && ContentElement is FrameworkElement content)
        {
            var contentAvailable = GetContentAvailableSize(availableSize, headerSize);
            content.Measure(contentAvailable);
            contentSize = content.DesiredSize;
            IncrementAggregate(ref _diagContentMeasuredWhenExpandedCount);
            _runtimeContentMeasuredWhenExpandedCount++;
        }
        else
        {
            IncrementAggregate(ref _diagContentMeasuredWhenCollapsedCount);
            _runtimeContentMeasuredWhenCollapsedCount++;
            if (ContentElement is not FrameworkElement)
            {
                IncrementAggregate(ref _diagContentMeasureSkippedWithoutContentCount);
                _runtimeContentMeasureSkippedWithoutContentCount++;
            }
        }

        RecordElapsed(ref _diagMeasureOverrideCallCount, ref _diagMeasureOverrideElapsedTicks, startTicks);
        _runtimeMeasureOverrideCallCount++;
        _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return ComposeDesiredSize(headerSize, contentSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var headerSize = _measuredHeaderSize;
        if (headerSize == Vector2.Zero)
        {
            IncrementAggregate(ref _diagArrangeHeaderMeasureCacheMissCount);
            _runtimeArrangeHeaderMeasureCacheMissCount++;
            headerSize = MeasureHeader(finalSize);
        }
        else
        {
            IncrementAggregate(ref _diagArrangeHeaderMeasureCacheHitCount);
            _runtimeArrangeHeaderMeasureCacheHitCount++;
        }

        var arrangedSize = IsExpanded
            ? finalSize
            : ExpandDirection switch
            {
                ExpandDirection.Down or ExpandDirection.Up => new Vector2(finalSize.X, headerSize.Y),
                _ => new Vector2(headerSize.X, finalSize.Y)
            };
        var slots = ComputeSlots(arrangedSize, headerSize);
        _headerRect = slots.HeaderRect;
        _contentRect = slots.ContentRect;

        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
                IncrementAggregate(ref _diagExpandDirectionDownCount);
                _runtimeExpandDirectionDownCount++;
                break;
            case ExpandDirection.Up:
                IncrementAggregate(ref _diagExpandDirectionUpCount);
                _runtimeExpandDirectionUpCount++;
                break;
            case ExpandDirection.Left:
                IncrementAggregate(ref _diagExpandDirectionLeftCount);
                _runtimeExpandDirectionLeftCount++;
                break;
            case ExpandDirection.Right:
                IncrementAggregate(ref _diagExpandDirectionRightCount);
                _runtimeExpandDirectionRightCount++;
                break;
        }

        if (_headerElement is FrameworkElement header)
        {
            header.Arrange(_headerRect);
        }

        if (ContentElement is FrameworkElement content)
        {
            if (IsExpanded)
            {
                IncrementAggregate(ref _diagArrangeExpandedContentCount);
                _runtimeArrangeExpandedContentCount++;
                content.Arrange(_contentRect);
            }
            else
            {
                IncrementAggregate(ref _diagArrangeCollapsedContentCount);
                _runtimeArrangeCollapsedContentCount++;
                content.Arrange(new LayoutRect(_contentRect.X, _contentRect.Y, 0f, 0f));
            }
        }
        else
        {
            IncrementAggregate(ref _diagArrangeNoContentCount);
            _runtimeArrangeNoContentCount++;
        }

        RecordElapsed(ref _diagArrangeOverrideCallCount, ref _diagArrangeOverrideElapsedTicks, startTicks);
        _runtimeArrangeOverrideCallCount++;
        _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return arrangedSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }

        var hasStyleDrivenHeaderBackground = GetValueSource(HeaderBackgroundProperty) != DependencyPropertyValueSource.Default;
        var headerFill = hasStyleDrivenHeaderBackground ? HeaderBackground : Background;
        if (hasStyleDrivenHeaderBackground)
        {
            IncrementAggregate(ref _diagHeaderBackgroundStyleOverrideCount);
            _runtimeHeaderBackgroundStyleOverrideCount++;
        }
        else
        {
            IncrementAggregate(ref _diagHeaderBackgroundInheritedCount);
            _runtimeHeaderBackgroundInheritedCount++;
        }

        UiDrawing.DrawFilledRect(spriteBatch, _headerRect, headerFill, Opacity);

        DrawChevron(spriteBatch, _headerRect);
        if (_headerElement == null)
        {
            DrawHeaderText(spriteBatch, _headerRect);
        }
        else
        {
            IncrementAggregate(ref _diagRenderHeaderElementCount);
            _runtimeRenderHeaderElementCount++;
        }

        RecordElapsed(ref _diagRenderCallCount, ref _diagRenderElapsedTicks, startTicks);
        _runtimeRenderCallCount++;
        _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }





    private Vector2 MeasureHeader(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        var padding = HeaderPadding;
        if (_headerElement is FrameworkElement header)
        {
            header.Measure(availableSize);
            IncrementAggregate(ref _diagHeaderMeasureCount);
            IncrementAggregate(ref _diagHeaderMeasureElementPathCount);
            RecordElapsed(ref _diagHeaderMeasureElapsedTicks, startTicks);
            _runtimeHeaderMeasureCount++;
            _runtimeHeaderMeasureElementPathCount++;
            _runtimeHeaderMeasureElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            return new Vector2(
                header.DesiredSize.X + padding.Horizontal + 16f,
                MathF.Max(16f, header.DesiredSize.Y) + padding.Vertical);
        }

        var text = Header?.ToString() ?? string.Empty;
        IncrementAggregate(ref _diagHeaderMeasureCount);
        IncrementAggregate(ref _diagHeaderMeasureTextPathCount);
        _runtimeHeaderMeasureCount++;
        _runtimeHeaderMeasureTextPathCount++;
        if (text.Length == 0)
        {
            IncrementAggregate(ref _diagHeaderMeasureEmptyTextCount);
            _runtimeHeaderMeasureEmptyTextCount++;
        }

        var textWidth = UiTextRenderer.MeasureWidth(this, text, FontSize);
        var textHeight = UiTextRenderer.GetLineHeight(this, FontSize);
        RecordElapsed(ref _diagHeaderMeasureElapsedTicks, startTicks);
        _runtimeHeaderMeasureElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return new Vector2(
            textWidth + padding.Horizontal + 16f,
            MathF.Max(16f, textHeight) + padding.Vertical);
    }

    private Vector2 GetContentAvailableSize(Vector2 availableSize, Vector2 headerSize)
    {
        var padding = Padding;
        var available = availableSize;
        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
            case ExpandDirection.Up:
                available.Y = MathF.Max(0f, available.Y - headerSize.Y);
                available.X = MathF.Max(0f, available.X - padding.Horizontal);
                available.Y = MathF.Max(0f, available.Y - padding.Vertical);
                break;
            case ExpandDirection.Left:
            case ExpandDirection.Right:
                available.X = MathF.Max(0f, available.X - headerSize.X);
                available.X = MathF.Max(0f, available.X - padding.Horizontal);
                available.Y = MathF.Max(0f, available.Y - padding.Vertical);
                break;
        }

        return available;
    }

    private Vector2 ComposeDesiredSize(Vector2 headerSize, Vector2 contentSize)
    {
        var padding = Padding;
        var paddedContent = IsExpanded
            ? new Vector2(contentSize.X + padding.Horizontal, contentSize.Y + padding.Vertical)
            : Vector2.Zero;

        return ExpandDirection switch
        {
            ExpandDirection.Down or ExpandDirection.Up => new Vector2(
                MathF.Max(headerSize.X, paddedContent.X),
                headerSize.Y + paddedContent.Y),
            _ => new Vector2(
                headerSize.X + paddedContent.X,
                MathF.Max(headerSize.Y, paddedContent.Y))
        };
    }

    private (LayoutRect HeaderRect, LayoutRect ContentRect) ComputeSlots(Vector2 finalSize, Vector2 headerSize)
    {
        var slot = LayoutSlot;
        var padding = Padding;
        switch (ExpandDirection)
        {
            case ExpandDirection.Up:
            {
                var contentHeight = MathF.Max(0f, finalSize.Y - headerSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - padding.Horizontal),
                    MathF.Max(0f, contentHeight - padding.Vertical));
                var headerRect = new LayoutRect(slot.X, slot.Y + contentHeight, finalSize.X, headerSize.Y);
                return (headerRect, contentRect);
            }
            case ExpandDirection.Left:
            {
                var contentWidth = MathF.Max(0f, finalSize.X - headerSize.X);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, contentWidth - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - padding.Vertical));
                var headerRect = new LayoutRect(slot.X + contentWidth, slot.Y, headerSize.X, finalSize.Y);
                return (headerRect, contentRect);
            }
            case ExpandDirection.Right:
            {
                var headerRect = new LayoutRect(slot.X, slot.Y, headerSize.X, finalSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + headerSize.X + padding.Left,
                    slot.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - headerSize.X - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - padding.Vertical));
                return (headerRect, contentRect);
            }
            default:
            {
                var headerRect = new LayoutRect(slot.X, slot.Y, finalSize.X, headerSize.Y);
                var contentRect = new LayoutRect(
                    slot.X + padding.Left,
                    slot.Y + headerSize.Y + padding.Top,
                    MathF.Max(0f, finalSize.X - padding.Horizontal),
                    MathF.Max(0f, finalSize.Y - headerSize.Y - padding.Vertical));
                return (headerRect, contentRect);
            }
        }
    }

    private void DrawHeaderText(SpriteBatch spriteBatch, LayoutRect headerRect)
    {
        var text = Header?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            IncrementAggregate(ref _diagRenderHeaderTextSkippedEmptyCount);
            _runtimeRenderHeaderTextSkippedEmptyCount++;
            return;
        }

        IncrementAggregate(ref _diagRenderHeaderTextCount);
        _runtimeRenderHeaderTextCount++;

        var padding = HeaderPadding;
        var textX = headerRect.X + padding.Left + 14f;
        var textY = headerRect.Y + padding.Top;
        UiTextRenderer.DrawString(
            spriteBatch,
            this,
            text,
            new Vector2(textX, textY),
            Foreground * Opacity,
            FontSize);
    }

    private void DrawChevron(SpriteBatch spriteBatch, LayoutRect headerRect)
    {
        var center = new Vector2(headerRect.X + 8f, headerRect.Y + (headerRect.Height / 2f));
        var color = Foreground;
        var arm = 3f;
        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
                if (IsExpanded)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - arm, 2f, arm), color, Opacity);
                }
                else
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                }

                break;
            case ExpandDirection.Up:
                if (IsExpanded)
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y, 2f, arm), color, Opacity);
                }
                else
                {
                    UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                }

                break;
            case ExpandDirection.Left:
            case ExpandDirection.Right:
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - arm, 2f, arm * 2f), color, Opacity);
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - arm, center.Y - 1f, arm * 2f, 2f), color, Opacity);
                break;
        }
    }

    private void UpdateHeaderElement(object? oldHeader, object? newHeader)
    {
        if (oldHeader is UIElement oldElement && ReferenceEquals(_headerElement, oldElement))
        {
            oldElement.SetVisualParent(null);
            oldElement.SetLogicalParent(null);
            _headerElement = null;
            _runtimeHeaderUpdateDetachElementCount++;
        }

        if (newHeader is UIElement newElement)
        {
            _headerElement = newElement;
            _headerElement.SetVisualParent(this);
            _headerElement.SetLogicalParent(this);
            _runtimeHeaderUpdateAttachElementCount++;
        }
        else
        {
            _headerElement = null;
            _runtimeHeaderUpdateTextHeaderCount++;
        }

        IncrementAggregate(ref _diagHeaderUpdateCount);
        _runtimeHeaderUpdateCount++;
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        _isHeaderPressed = Contains(_headerRect, pointerPosition);
        if (_isHeaderPressed)
        {
            IncrementAggregate(ref _diagHeaderPointerDownCount);
            _runtimeHeaderPointerDownCount++;
        }
        else
        {
            IncrementAggregate(ref _diagHeaderPointerDownMissCount);
            _runtimeHeaderPointerDownMissCount++;
        }

        return _isHeaderPressed;
    }

    internal bool HandlePointerUpFromInput(Vector2 pointerPosition)
    {
        var wasHeaderPressed = _isHeaderPressed;
        var releasedInsideHeader = Contains(_headerRect, pointerPosition);
        var shouldToggle = wasHeaderPressed && releasedInsideHeader;
        _isHeaderPressed = false;
        if (!shouldToggle)
        {
            IncrementAggregate(ref _diagHeaderPointerUpMissCount);
            _runtimeHeaderPointerUpMissCount++;
            if (wasHeaderPressed && !releasedInsideHeader)
            {
                IncrementAggregate(ref _diagHeaderPointerUpReleaseOutsideCount);
                _runtimeHeaderPointerUpReleaseOutsideCount++;
            }

            return false;
        }

        IncrementAggregate(ref _diagHeaderPointerUpToggleCount);
        _runtimeHeaderPointerUpToggleCount++;
        IsExpanded = !IsExpanded;
        return true;
    }

    internal ExpanderRuntimeDiagnosticsSnapshot GetExpanderSnapshotForDiagnostics()
    {
        return new ExpanderRuntimeDiagnosticsSnapshot(
            IsExpanded,
            ExpandDirection,
            _isHeaderPressed,
            _headerElement != null,
            ContentElement != null,
            _measuredHeaderSize.X,
            _measuredHeaderSize.Y,
            _headerRect.X,
            _headerRect.Y,
            _headerRect.Width,
            _headerRect.Height,
            _contentRect.X,
            _contentRect.Y,
            _contentRect.Width,
            _contentRect.Height,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeHeaderMeasureCount,
            TicksToMilliseconds(_runtimeHeaderMeasureElapsedTicks),
            _runtimeHeaderMeasureElementPathCount,
            _runtimeHeaderMeasureTextPathCount,
            _runtimeHeaderMeasureEmptyTextCount,
            _runtimeContentMeasuredWhenExpandedCount,
            _runtimeContentMeasuredWhenCollapsedCount,
            _runtimeContentMeasureSkippedWithoutContentCount,
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeHeaderMeasureCacheHitCount,
            _runtimeArrangeHeaderMeasureCacheMissCount,
            _runtimeArrangeExpandedContentCount,
            _runtimeArrangeCollapsedContentCount,
            _runtimeArrangeNoContentCount,
            _runtimeExpandDirectionDownCount,
            _runtimeExpandDirectionUpCount,
            _runtimeExpandDirectionLeftCount,
            _runtimeExpandDirectionRightCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeHeaderBackgroundStyleOverrideCount,
            _runtimeHeaderBackgroundInheritedCount,
            _runtimeRenderHeaderTextCount,
            _runtimeRenderHeaderTextSkippedEmptyCount,
            _runtimeRenderHeaderElementCount,
            _runtimeExpandCount,
            _runtimeCollapseCount,
            _runtimeHeaderPointerDownCount,
            _runtimeHeaderPointerDownMissCount,
            _runtimeHeaderPointerUpToggleCount,
            _runtimeHeaderPointerUpMissCount,
            _runtimeHeaderPointerUpReleaseOutsideCount,
            _runtimeHeaderUpdateCount,
            _runtimeHeaderUpdateAttachElementCount,
            _runtimeHeaderUpdateDetachElementCount,
            _runtimeHeaderUpdateTextHeaderCount);
    }

    internal static ExpanderTimingSnapshot GetTelemetryAndReset()
    {
        var snapshot = CreateAggregateTelemetrySnapshot();
        _diagMeasureOverrideCallCount = 0;
        _diagMeasureOverrideElapsedTicks = 0;
        _diagHeaderMeasureCount = 0;
        _diagHeaderMeasureElapsedTicks = 0;
        _diagHeaderMeasureElementPathCount = 0;
        _diagHeaderMeasureTextPathCount = 0;
        _diagHeaderMeasureEmptyTextCount = 0;
        _diagContentMeasuredWhenExpandedCount = 0;
        _diagContentMeasuredWhenCollapsedCount = 0;
        _diagContentMeasureSkippedWithoutContentCount = 0;
        _diagArrangeOverrideCallCount = 0;
        _diagArrangeOverrideElapsedTicks = 0;
        _diagArrangeHeaderMeasureCacheHitCount = 0;
        _diagArrangeHeaderMeasureCacheMissCount = 0;
        _diagArrangeExpandedContentCount = 0;
        _diagArrangeCollapsedContentCount = 0;
        _diagArrangeNoContentCount = 0;
        _diagExpandDirectionDownCount = 0;
        _diagExpandDirectionUpCount = 0;
        _diagExpandDirectionLeftCount = 0;
        _diagExpandDirectionRightCount = 0;
        _diagRenderCallCount = 0;
        _diagRenderElapsedTicks = 0;
        _diagHeaderBackgroundStyleOverrideCount = 0;
        _diagHeaderBackgroundInheritedCount = 0;
        _diagRenderHeaderTextCount = 0;
        _diagRenderHeaderTextSkippedEmptyCount = 0;
        _diagRenderHeaderElementCount = 0;
        _diagExpandCount = 0;
        _diagCollapseCount = 0;
        _diagHeaderPointerDownCount = 0;
        _diagHeaderPointerDownMissCount = 0;
        _diagHeaderPointerUpToggleCount = 0;
        _diagHeaderPointerUpMissCount = 0;
        _diagHeaderPointerUpReleaseOutsideCount = 0;
        _diagHeaderUpdateCount = 0;
        return snapshot;
    }

    internal static ExpanderTimingSnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    private static ExpanderTimingSnapshot CreateAggregateTelemetrySnapshot()
    {
        return new ExpanderTimingSnapshot(
            _diagMeasureOverrideCallCount,
            TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            _diagHeaderMeasureCount,
            TicksToMilliseconds(_diagHeaderMeasureElapsedTicks),
            _diagHeaderMeasureElementPathCount,
            _diagHeaderMeasureTextPathCount,
            _diagHeaderMeasureEmptyTextCount,
            _diagContentMeasuredWhenExpandedCount,
            _diagContentMeasuredWhenCollapsedCount,
            _diagContentMeasureSkippedWithoutContentCount,
            _diagArrangeOverrideCallCount,
            TicksToMilliseconds(_diagArrangeOverrideElapsedTicks),
            _diagArrangeHeaderMeasureCacheHitCount,
            _diagArrangeHeaderMeasureCacheMissCount,
            _diagArrangeExpandedContentCount,
            _diagArrangeCollapsedContentCount,
            _diagArrangeNoContentCount,
            _diagExpandDirectionDownCount,
            _diagExpandDirectionUpCount,
            _diagExpandDirectionLeftCount,
            _diagExpandDirectionRightCount,
            _diagRenderCallCount,
            TicksToMilliseconds(_diagRenderElapsedTicks),
            _diagHeaderBackgroundStyleOverrideCount,
            _diagHeaderBackgroundInheritedCount,
            _diagRenderHeaderTextCount,
            _diagRenderHeaderTextSkippedEmptyCount,
            _diagRenderHeaderElementCount,
            _diagExpandCount,
            _diagCollapseCount,
            _diagHeaderPointerDownCount,
            _diagHeaderPointerDownMissCount,
            _diagHeaderPointerUpToggleCount,
            _diagHeaderPointerUpMissCount,
            _diagHeaderPointerUpReleaseOutsideCount,
            _diagHeaderUpdateCount);
    }

    private static void IncrementAggregate(ref long counter)
    {
        System.Threading.Interlocked.Increment(ref counter);
    }

    private static void RecordElapsed(ref long callCount, ref long elapsedTicks, long startTicks)
    {
        IncrementAggregate(ref callCount);
        System.Threading.Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private static void RecordElapsed(ref long elapsedTicks, long startTicks)
    {
        System.Threading.Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - startTicks);
    }

    private static double TicksToMilliseconds(long elapsedTicks)
    {
        return (double)elapsedTicks * 1000d / Stopwatch.Frequency;
    }
}


