using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class UserControl : ContentControl
{
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyChangedElapsedTicks;
    private static long _diagDependencyPropertyChangedRejectedNonUiElementCount;
    private static long _diagDependencyPropertyChangedTemplatePropertyCount;
    private static long _diagDependencyPropertyChangedTemplateDetachCount;
    private static long _diagDependencyPropertyChangedTemplateCacheClearCount;
    private static long _diagDependencyPropertyChangedTemplateRefreshCount;
    private static long _diagDependencyPropertyChangedOtherPropertyCount;
    private static long _diagGetVisualChildrenCallCount;
    private static long _diagGetVisualChildrenTemplatePathCount;
    private static long _diagGetVisualChildrenNonTemplatePathCount;
    private static long _diagGetVisualChildrenFilteredContentCount;
    private static long _diagGetVisualChildrenYieldedChildCount;
    private static long _diagGetVisualChildCountForTraversalCallCount;
    private static long _diagGetVisualChildCountForTraversalTemplatePathCount;
    private static long _diagGetVisualChildCountForTraversalNonTemplatePathCount;
    private static long _diagGetVisualChildCountForTraversalFilteredContentCount;
    private static long _diagGetVisualChildAtForTraversalCallCount;
    private static long _diagGetVisualChildAtForTraversalTemplatePathCount;
    private static long _diagGetVisualChildAtForTraversalNonTemplatePathCount;
    private static long _diagGetVisualChildAtForTraversalFilteredContentCount;
    private static long _diagGetVisualChildAtForTraversalOutOfRangeCount;
    private static long _diagGetLogicalChildrenCallCount;
    private static long _diagGetLogicalChildrenTemplatePathCount;
    private static long _diagGetLogicalChildrenNonTemplatePathCount;
    private static long _diagGetLogicalChildrenFilteredContentCount;
    private static long _diagGetLogicalChildrenYieldedChildCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideTemplatePathCount;
    private static long _diagMeasureOverrideNonTemplatePathCount;
    private static long _diagMeasureOverrideTemplateRootMeasureCount;
    private static long _diagMeasureOverrideTemplateReturnedZeroCount;
    private static long _diagMeasureOverrideNoContentCount;
    private static long _diagMeasureOverrideContentMeasureCount;
    private static long _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;
    private static long _diagArrangeOverrideTemplatePathCount;
    private static long _diagArrangeOverrideNonTemplatePathCount;
    private static long _diagArrangeOverrideTemplateRootArrangeCount;
    private static long _diagArrangeOverrideNoContentCount;
    private static long _diagArrangeOverrideContentArrangeCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderTemplateSkipCount;
    private static long _diagRenderBackgroundDrawCount;
    private static long _diagRenderBorderEdgeDrawCount;
    private static long _diagGetChromeThicknessCallCount;
    private static long _diagEnsureTemplateAppliedIfNeededCallCount;
    private static long _diagEnsureTemplateAppliedIfNeededElapsedTicks;
    private static long _diagEnsureTemplateAppliedApplyTemplateCount;
    private static long _diagEnsureTemplateAppliedRefreshCachedRootCount;
    private static long _diagEnsureTemplateAppliedNoOpCount;
    private static long _diagRefreshCachedTemplateRootCallCount;
    private static long _diagRefreshCachedTemplateRootElapsedTicks;
    private static long _diagRefreshCachedTemplateRootHitCount;
    private static long _diagRefreshCachedTemplateRootMissCount;
    private static long _diagRefreshCachedTemplateRootEnumeratedChildCount;
    private static long _diagDetachTemplateContentPresentersCallCount;
    private static long _diagDetachTemplateContentPresentersElapsedTicks;
    private static long _diagDetachTemplateContentPresentersFallbackSearchCount;
    private static long _diagDetachTemplateContentPresentersRootNotFoundCount;
    private static long _diagDetachTemplateContentPresentersVisitedElementCount;
    private static long _diagDetachTemplateContentPresentersPresenterDetachCount;
    private static long _diagDetachTemplateContentPresentersTraversedChildCount;

    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyChangedElapsedTicks;
    private long _runtimeDependencyPropertyChangedRejectedNonUiElementCount;
    private long _runtimeDependencyPropertyChangedTemplatePropertyCount;
    private long _runtimeDependencyPropertyChangedTemplateDetachCount;
    private long _runtimeDependencyPropertyChangedTemplateCacheClearCount;
    private long _runtimeDependencyPropertyChangedTemplateRefreshCount;
    private long _runtimeDependencyPropertyChangedOtherPropertyCount;
    private long _runtimeGetVisualChildrenCallCount;
    private long _runtimeGetVisualChildrenTemplatePathCount;
    private long _runtimeGetVisualChildrenNonTemplatePathCount;
    private long _runtimeGetVisualChildrenFilteredContentCount;
    private long _runtimeGetVisualChildrenYieldedChildCount;
    private long _runtimeGetVisualChildCountForTraversalCallCount;
    private long _runtimeGetVisualChildCountForTraversalTemplatePathCount;
    private long _runtimeGetVisualChildCountForTraversalNonTemplatePathCount;
    private long _runtimeGetVisualChildCountForTraversalFilteredContentCount;
    private long _runtimeGetVisualChildAtForTraversalCallCount;
    private long _runtimeGetVisualChildAtForTraversalTemplatePathCount;
    private long _runtimeGetVisualChildAtForTraversalNonTemplatePathCount;
    private long _runtimeGetVisualChildAtForTraversalFilteredContentCount;
    private long _runtimeGetVisualChildAtForTraversalOutOfRangeCount;
    private long _runtimeGetLogicalChildrenCallCount;
    private long _runtimeGetLogicalChildrenTemplatePathCount;
    private long _runtimeGetLogicalChildrenNonTemplatePathCount;
    private long _runtimeGetLogicalChildrenFilteredContentCount;
    private long _runtimeGetLogicalChildrenYieldedChildCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideTemplatePathCount;
    private long _runtimeMeasureOverrideNonTemplatePathCount;
    private long _runtimeMeasureOverrideTemplateRootMeasureCount;
    private long _runtimeMeasureOverrideTemplateReturnedZeroCount;
    private long _runtimeMeasureOverrideNoContentCount;
    private long _runtimeMeasureOverrideContentMeasureCount;
    private long _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;
    private long _runtimeArrangeOverrideTemplatePathCount;
    private long _runtimeArrangeOverrideNonTemplatePathCount;
    private long _runtimeArrangeOverrideTemplateRootArrangeCount;
    private long _runtimeArrangeOverrideNoContentCount;
    private long _runtimeArrangeOverrideContentArrangeCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderTemplateSkipCount;
    private long _runtimeRenderBackgroundDrawCount;
    private long _runtimeRenderBorderEdgeDrawCount;
    private long _runtimeGetChromeThicknessCallCount;
    private long _runtimeEnsureTemplateAppliedIfNeededCallCount;
    private long _runtimeEnsureTemplateAppliedIfNeededElapsedTicks;
    private long _runtimeEnsureTemplateAppliedApplyTemplateCount;
    private long _runtimeEnsureTemplateAppliedRefreshCachedRootCount;
    private long _runtimeEnsureTemplateAppliedNoOpCount;
    private long _runtimeRefreshCachedTemplateRootCallCount;
    private long _runtimeRefreshCachedTemplateRootElapsedTicks;
    private long _runtimeRefreshCachedTemplateRootHitCount;
    private long _runtimeRefreshCachedTemplateRootMissCount;
    private long _runtimeRefreshCachedTemplateRootEnumeratedChildCount;
    private long _runtimeDetachTemplateContentPresentersCallCount;
    private long _runtimeDetachTemplateContentPresentersElapsedTicks;
    private long _runtimeDetachTemplateContentPresentersFallbackSearchCount;
    private long _runtimeDetachTemplateContentPresentersRootNotFoundCount;
    private long _runtimeDetachTemplateContentPresentersVisitedElementCount;
    private long _runtimeDetachTemplateContentPresentersPresenterDetachCount;
    private long _runtimeDetachTemplateContentPresentersTraversedChildCount;

    private UIElement? _cachedTemplateRoot;

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(UserControl),
            new FrameworkPropertyMetadata(Color.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(UserControl),
            new FrameworkPropertyMetadata(
                Thickness.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // WPF-like mental model: UserControl hosts a single visual root.
    public new UIElement? Content
    {
        get => base.Content as UIElement;
        set => base.Content = value;
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeDependencyPropertyChangedCallCount++;
        try
        {
            if (args.Property == ContentProperty &&
                args.NewValue != null &&
                args.NewValue is not UIElement)
            {
                _runtimeDependencyPropertyChangedRejectedNonUiElementCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedRejectedNonUiElementCount);
                throw new InvalidOperationException(
                    "UserControl.Content must be a UIElement. Wrap non-visual data in a visual element.");
            }

            if (args.Property == TemplateProperty)
            {
                _runtimeDependencyPropertyChangedTemplatePropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplatePropertyCount);
                _runtimeDependencyPropertyChangedTemplateDetachCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplateDetachCount);
                DetachTemplateContentPresenters();
                // Clear early so any re-entrant layout during template clear/rebuild cannot observe a stale root.
                _cachedTemplateRoot = null;
                _runtimeDependencyPropertyChangedTemplateCacheClearCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplateCacheClearCount);
            }
            else
            {
                _runtimeDependencyPropertyChangedOtherPropertyCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedOtherPropertyCount);
            }

            base.OnDependencyPropertyChanged(args);

            if (args.Property == TemplateProperty && HasTemplateAssigned())
            {
                RefreshCachedTemplateRoot();
                _runtimeDependencyPropertyChangedTemplateRefreshCount++;
                IncrementAggregate(ref _diagDependencyPropertyChangedTemplateRefreshCount);
            }
        }
        finally
        {
            _runtimeDependencyPropertyChangedElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedElapsedTicks, start);
        }
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        _runtimeGetVisualChildrenCallCount++;
        IncrementAggregate(ref _diagGetVisualChildrenCallCount);
        var hasTemplateAssigned = HasTemplateAssigned();
        if (!hasTemplateAssigned)
        {
            _runtimeGetVisualChildrenNonTemplatePathCount++;
            IncrementAggregate(ref _diagGetVisualChildrenNonTemplatePathCount);
            foreach (var child in base.GetVisualChildren())
            {
                _runtimeGetVisualChildrenYieldedChildCount++;
                IncrementAggregate(ref _diagGetVisualChildrenYieldedChildCount);
                yield return child;
            }

            yield break;
        }

        _runtimeGetVisualChildrenTemplatePathCount++;
        IncrementAggregate(ref _diagGetVisualChildrenTemplatePathCount);

        foreach (var child in base.GetVisualChildren())
        {
            if (ReferenceEquals(child, ContentElement))
            {
                _runtimeGetVisualChildrenFilteredContentCount++;
                IncrementAggregate(ref _diagGetVisualChildrenFilteredContentCount);
                continue;
            }

            _runtimeGetVisualChildrenYieldedChildCount++;
            IncrementAggregate(ref _diagGetVisualChildrenYieldedChildCount);
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        _runtimeGetVisualChildCountForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildCountForTraversalCallCount);
        if (!HasTemplateAssigned())
        {
            _runtimeGetVisualChildCountForTraversalNonTemplatePathCount++;
            IncrementAggregate(ref _diagGetVisualChildCountForTraversalNonTemplatePathCount);
            return base.GetVisualChildCountForTraversal();
        }

        _runtimeGetVisualChildCountForTraversalTemplatePathCount++;
        IncrementAggregate(ref _diagGetVisualChildCountForTraversalTemplatePathCount);

        var count = 0;
        var baseCount = base.GetVisualChildCountForTraversal();
        for (var i = 0; i < baseCount; i++)
        {
            if (ReferenceEquals(base.GetVisualChildAtForTraversal(i), ContentElement))
            {
                _runtimeGetVisualChildCountForTraversalFilteredContentCount++;
                IncrementAggregate(ref _diagGetVisualChildCountForTraversalFilteredContentCount);
                continue;
            }

            count++;
        }

        return count;
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        _runtimeGetVisualChildAtForTraversalCallCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalCallCount);
        if (!HasTemplateAssigned())
        {
            _runtimeGetVisualChildAtForTraversalNonTemplatePathCount++;
            IncrementAggregate(ref _diagGetVisualChildAtForTraversalNonTemplatePathCount);
            return base.GetVisualChildAtForTraversal(index);
        }

        _runtimeGetVisualChildAtForTraversalTemplatePathCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalTemplatePathCount);

        var traversalIndex = 0;
        var baseCount = base.GetVisualChildCountForTraversal();
        for (var i = 0; i < baseCount; i++)
        {
            var child = base.GetVisualChildAtForTraversal(i);
            if (ReferenceEquals(child, ContentElement))
            {
                _runtimeGetVisualChildAtForTraversalFilteredContentCount++;
                IncrementAggregate(ref _diagGetVisualChildAtForTraversalFilteredContentCount);
                continue;
            }

            if (traversalIndex == index)
            {
                return child;
            }

            traversalIndex++;
        }

        _runtimeGetVisualChildAtForTraversalOutOfRangeCount++;
        IncrementAggregate(ref _diagGetVisualChildAtForTraversalOutOfRangeCount);
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        _runtimeGetLogicalChildrenCallCount++;
        IncrementAggregate(ref _diagGetLogicalChildrenCallCount);
        var hasTemplateAssigned = HasTemplateAssigned();
        if (!hasTemplateAssigned)
        {
            _runtimeGetLogicalChildrenNonTemplatePathCount++;
            IncrementAggregate(ref _diagGetLogicalChildrenNonTemplatePathCount);
            foreach (var child in base.GetLogicalChildren())
            {
                _runtimeGetLogicalChildrenYieldedChildCount++;
                IncrementAggregate(ref _diagGetLogicalChildrenYieldedChildCount);
                yield return child;
            }

            yield break;
        }

        _runtimeGetLogicalChildrenTemplatePathCount++;
        IncrementAggregate(ref _diagGetLogicalChildrenTemplatePathCount);

        foreach (var child in base.GetLogicalChildren())
        {
            if (ReferenceEquals(child, ContentElement))
            {
                _runtimeGetLogicalChildrenFilteredContentCount++;
                IncrementAggregate(ref _diagGetLogicalChildrenFilteredContentCount);
                continue;
            }

            _runtimeGetLogicalChildrenYieldedChildCount++;
            IncrementAggregate(ref _diagGetLogicalChildrenYieldedChildCount);
            yield return child;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeMeasureOverrideCallCount++;
        try
        {
            if (HasTemplateAssigned())
            {
                _runtimeMeasureOverrideTemplatePathCount++;
                IncrementAggregate(ref _diagMeasureOverrideTemplatePathCount);
                EnsureTemplateAppliedIfNeeded();

                if (_cachedTemplateRoot is FrameworkElement templateRoot)
                {
                    _runtimeMeasureOverrideTemplateRootMeasureCount++;
                    IncrementAggregate(ref _diagMeasureOverrideTemplateRootMeasureCount);
                    templateRoot.Measure(availableSize);
                    return templateRoot.DesiredSize;
                }

                _runtimeMeasureOverrideTemplateReturnedZeroCount++;
                IncrementAggregate(ref _diagMeasureOverrideTemplateReturnedZeroCount);
                return Vector2.Zero;
            }

            _runtimeMeasureOverrideNonTemplatePathCount++;
            IncrementAggregate(ref _diagMeasureOverrideNonTemplatePathCount);

            var chrome = GetChromeThickness();
            if (ContentElement is not FrameworkElement content)
            {
                _runtimeMeasureOverrideNoContentCount++;
                IncrementAggregate(ref _diagMeasureOverrideNoContentCount);
                return new Vector2(chrome.Horizontal, chrome.Vertical);
            }

            _runtimeMeasureOverrideContentMeasureCount++;
            IncrementAggregate(ref _diagMeasureOverrideContentMeasureCount);
            var contentAvailableSize = new Vector2(
                MathF.Max(0f, availableSize.X - chrome.Horizontal),
                MathF.Max(0f, availableSize.Y - chrome.Vertical));
            content.Measure(contentAvailableSize);
            return new Vector2(
                content.DesiredSize.X + chrome.Horizontal,
                content.DesiredSize.Y + chrome.Vertical);
        }
        finally
        {
            _runtimeMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagMeasureOverrideCallCount, ref _diagMeasureOverrideElapsedTicks, start);
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeArrangeOverrideCallCount++;
        try
        {
            if (HasTemplateAssigned())
            {
                _runtimeArrangeOverrideTemplatePathCount++;
                IncrementAggregate(ref _diagArrangeOverrideTemplatePathCount);
                EnsureTemplateAppliedIfNeeded();

                if (_cachedTemplateRoot is FrameworkElement templateRoot)
                {
                    _runtimeArrangeOverrideTemplateRootArrangeCount++;
                    IncrementAggregate(ref _diagArrangeOverrideTemplateRootArrangeCount);
                    templateRoot.Arrange(new LayoutRect(LayoutSlot.X, LayoutSlot.Y, finalSize.X, finalSize.Y));
                }

                return finalSize;
            }

            _runtimeArrangeOverrideNonTemplatePathCount++;
            IncrementAggregate(ref _diagArrangeOverrideNonTemplatePathCount);

            if (ContentElement is FrameworkElement content)
            {
                _runtimeArrangeOverrideContentArrangeCount++;
                IncrementAggregate(ref _diagArrangeOverrideContentArrangeCount);
                var chrome = GetChromeThickness();
                content.Arrange(new LayoutRect(
                    LayoutSlot.X + chrome.Left,
                    LayoutSlot.Y + chrome.Top,
                    MathF.Max(0f, finalSize.X - chrome.Horizontal),
                    MathF.Max(0f, finalSize.Y - chrome.Vertical)));
            }
            else
            {
                _runtimeArrangeOverrideNoContentCount++;
                IncrementAggregate(ref _diagArrangeOverrideNoContentCount);
            }

            return finalSize;
        }
        finally
        {
            _runtimeArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagArrangeOverrideCallCount, ref _diagArrangeOverrideElapsedTicks, start);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeRenderCallCount++;
        try
        {
            base.OnRender(spriteBatch);

            if (HasTemplateAssigned())
            {
                _runtimeRenderTemplateSkipCount++;
                IncrementAggregate(ref _diagRenderTemplateSkipCount);
                return;
            }

            var slot = LayoutSlot;
            var border = BorderThickness;
            _runtimeRenderBackgroundDrawCount++;
            IncrementAggregate(ref _diagRenderBackgroundDrawCount);
            UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

            if (border.Left > 0f)
            {
                _runtimeRenderBorderEdgeDrawCount++;
                IncrementAggregate(ref _diagRenderBorderEdgeDrawCount);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(slot.X, slot.Y, border.Left, slot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Right > 0f)
            {
                _runtimeRenderBorderEdgeDrawCount++;
                IncrementAggregate(ref _diagRenderBorderEdgeDrawCount);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height),
                    BorderBrush,
                    Opacity);
            }

            if (border.Top > 0f)
            {
                _runtimeRenderBorderEdgeDrawCount++;
                IncrementAggregate(ref _diagRenderBorderEdgeDrawCount);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(slot.X, slot.Y, slot.Width, border.Top),
                    BorderBrush,
                    Opacity);
            }

            if (border.Bottom > 0f)
            {
                _runtimeRenderBorderEdgeDrawCount++;
                IncrementAggregate(ref _diagRenderBorderEdgeDrawCount);
                UiDrawing.DrawFilledRect(
                    spriteBatch,
                    new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom),
                    BorderBrush,
                    Opacity);
            }
        }
        finally
        {
            _runtimeRenderElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagRenderCallCount, ref _diagRenderElapsedTicks, start);
        }
    }

    internal UserControlRuntimeDiagnosticsSnapshot GetUserControlSnapshotForDiagnostics()
    {
        var border = BorderThickness;
        var padding = Padding;
        return new UserControlRuntimeDiagnosticsSnapshot(
            HasTemplateAssigned(),
            HasTemplateRoot,
            _cachedTemplateRoot != null,
            _cachedTemplateRoot?.GetType().Name ?? string.Empty,
            ContentElement != null,
            ContentElement?.GetType().Name ?? string.Empty,
            LayoutSlot.Width,
            LayoutSlot.Height,
            border.Left,
            border.Top,
            border.Right,
            border.Bottom,
            padding.Left,
            padding.Top,
            padding.Right,
            padding.Bottom,
            _runtimeDependencyPropertyChangedCallCount,
            TicksToMilliseconds(_runtimeDependencyPropertyChangedElapsedTicks),
            _runtimeDependencyPropertyChangedRejectedNonUiElementCount,
            _runtimeDependencyPropertyChangedTemplatePropertyCount,
            _runtimeDependencyPropertyChangedTemplateDetachCount,
            _runtimeDependencyPropertyChangedTemplateCacheClearCount,
            _runtimeDependencyPropertyChangedTemplateRefreshCount,
            _runtimeDependencyPropertyChangedOtherPropertyCount,
            _runtimeGetVisualChildrenCallCount,
            _runtimeGetVisualChildrenTemplatePathCount,
            _runtimeGetVisualChildrenNonTemplatePathCount,
            _runtimeGetVisualChildrenFilteredContentCount,
            _runtimeGetVisualChildrenYieldedChildCount,
            _runtimeGetVisualChildCountForTraversalCallCount,
            _runtimeGetVisualChildCountForTraversalTemplatePathCount,
            _runtimeGetVisualChildCountForTraversalNonTemplatePathCount,
            _runtimeGetVisualChildCountForTraversalFilteredContentCount,
            _runtimeGetVisualChildAtForTraversalCallCount,
            _runtimeGetVisualChildAtForTraversalTemplatePathCount,
            _runtimeGetVisualChildAtForTraversalNonTemplatePathCount,
            _runtimeGetVisualChildAtForTraversalFilteredContentCount,
            _runtimeGetVisualChildAtForTraversalOutOfRangeCount,
            _runtimeGetLogicalChildrenCallCount,
            _runtimeGetLogicalChildrenTemplatePathCount,
            _runtimeGetLogicalChildrenNonTemplatePathCount,
            _runtimeGetLogicalChildrenFilteredContentCount,
            _runtimeGetLogicalChildrenYieldedChildCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideTemplatePathCount,
            _runtimeMeasureOverrideNonTemplatePathCount,
            _runtimeMeasureOverrideTemplateRootMeasureCount,
            _runtimeMeasureOverrideTemplateReturnedZeroCount,
            _runtimeMeasureOverrideNoContentCount,
            _runtimeMeasureOverrideContentMeasureCount,
            _runtimeArrangeOverrideCallCount,
            TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            _runtimeArrangeOverrideTemplatePathCount,
            _runtimeArrangeOverrideNonTemplatePathCount,
            _runtimeArrangeOverrideTemplateRootArrangeCount,
            _runtimeArrangeOverrideNoContentCount,
            _runtimeArrangeOverrideContentArrangeCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderTemplateSkipCount,
            _runtimeRenderBackgroundDrawCount,
            _runtimeRenderBorderEdgeDrawCount,
            _runtimeGetChromeThicknessCallCount,
            _runtimeEnsureTemplateAppliedIfNeededCallCount,
            TicksToMilliseconds(_runtimeEnsureTemplateAppliedIfNeededElapsedTicks),
            _runtimeEnsureTemplateAppliedApplyTemplateCount,
            _runtimeEnsureTemplateAppliedRefreshCachedRootCount,
            _runtimeEnsureTemplateAppliedNoOpCount,
            _runtimeRefreshCachedTemplateRootCallCount,
            TicksToMilliseconds(_runtimeRefreshCachedTemplateRootElapsedTicks),
            _runtimeRefreshCachedTemplateRootHitCount,
            _runtimeRefreshCachedTemplateRootMissCount,
            _runtimeRefreshCachedTemplateRootEnumeratedChildCount,
            _runtimeDetachTemplateContentPresentersCallCount,
            TicksToMilliseconds(_runtimeDetachTemplateContentPresentersElapsedTicks),
            _runtimeDetachTemplateContentPresentersFallbackSearchCount,
            _runtimeDetachTemplateContentPresentersRootNotFoundCount,
            _runtimeDetachTemplateContentPresentersVisitedElementCount,
            _runtimeDetachTemplateContentPresentersPresenterDetachCount,
            _runtimeDetachTemplateContentPresentersTraversedChildCount);
    }

    internal new static UserControlTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot(reset: false);
    }

    internal new static UserControlTelemetrySnapshot GetTelemetrySnapshotForDiagnostics()
    {
        return GetAggregateTelemetrySnapshotForDiagnostics();
    }

    internal new static UserControlTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateAggregateTelemetrySnapshot(reset: true);
    }

    private Thickness GetChromeThickness()
    {
        _runtimeGetChromeThicknessCallCount++;
        IncrementAggregate(ref _diagGetChromeThicknessCallCount);
        var border = BorderThickness;
        var padding = Padding;
        return new Thickness(
            border.Left + padding.Left,
            border.Top + padding.Top,
            border.Right + padding.Right,
            border.Bottom + padding.Bottom);
    }

    private bool HasTemplateAssigned()
    {
        return Template != null;
    }

    private void EnsureTemplateAppliedIfNeeded()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeEnsureTemplateAppliedIfNeededCallCount++;
        try
        {
            if (Template != null && !HasTemplateRoot)
            {
                _runtimeEnsureTemplateAppliedApplyTemplateCount++;
                IncrementAggregate(ref _diagEnsureTemplateAppliedApplyTemplateCount);
                _cachedTemplateRoot = null;
                ApplyTemplate();
                RefreshCachedTemplateRoot();
                return;
            }

            if (HasTemplateAssigned() && _cachedTemplateRoot == null && HasTemplateRoot)
            {
                _runtimeEnsureTemplateAppliedRefreshCachedRootCount++;
                IncrementAggregate(ref _diagEnsureTemplateAppliedRefreshCachedRootCount);
                RefreshCachedTemplateRoot();
                return;
            }

            _runtimeEnsureTemplateAppliedNoOpCount++;
            IncrementAggregate(ref _diagEnsureTemplateAppliedNoOpCount);
        }
        finally
        {
            _runtimeEnsureTemplateAppliedIfNeededElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagEnsureTemplateAppliedIfNeededCallCount, ref _diagEnsureTemplateAppliedIfNeededElapsedTicks, start);
        }
    }

    private void RefreshCachedTemplateRoot()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeRefreshCachedTemplateRootCallCount++;
        try
        {
            _cachedTemplateRoot = null;
            foreach (var child in base.GetVisualChildren())
            {
                _runtimeRefreshCachedTemplateRootEnumeratedChildCount++;
                IncrementAggregate(ref _diagRefreshCachedTemplateRootEnumeratedChildCount);
                if (!ReferenceEquals(child, ContentElement))
                {
                    _cachedTemplateRoot = child;
                    _runtimeRefreshCachedTemplateRootHitCount++;
                    IncrementAggregate(ref _diagRefreshCachedTemplateRootHitCount);
                    return;
                }
            }

            _runtimeRefreshCachedTemplateRootMissCount++;
            IncrementAggregate(ref _diagRefreshCachedTemplateRootMissCount);
        }
        finally
        {
            _runtimeRefreshCachedTemplateRootElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagRefreshCachedTemplateRootCallCount, ref _diagRefreshCachedTemplateRootElapsedTicks, start);
        }
    }

    private void DetachTemplateContentPresenters()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeDetachTemplateContentPresentersCallCount++;
        try
        {
            var templateRoot = _cachedTemplateRoot;
            if (templateRoot == null)
            {
                _runtimeDetachTemplateContentPresentersFallbackSearchCount++;
                IncrementAggregate(ref _diagDetachTemplateContentPresentersFallbackSearchCount);
                foreach (var child in base.GetVisualChildren())
                {
                    _runtimeDetachTemplateContentPresentersTraversedChildCount++;
                    IncrementAggregate(ref _diagDetachTemplateContentPresentersTraversedChildCount);
                    if (!ReferenceEquals(child, ContentElement))
                    {
                        templateRoot = child;
                        break;
                    }
                }
            }

            if (templateRoot == null)
            {
                _runtimeDetachTemplateContentPresentersRootNotFoundCount++;
                IncrementAggregate(ref _diagDetachTemplateContentPresentersRootNotFoundCount);
                return;
            }

            var pending = new Stack<UIElement>();
            var visited = new HashSet<UIElement>();
            pending.Push(templateRoot);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                _runtimeDetachTemplateContentPresentersVisitedElementCount++;
                IncrementAggregate(ref _diagDetachTemplateContentPresentersVisitedElementCount);

                if (current is ContentPresenter presenter)
                {
                    _runtimeDetachTemplateContentPresentersPresenterDetachCount++;
                    IncrementAggregate(ref _diagDetachTemplateContentPresentersPresenterDetachCount);
                    DetachContentPresenter(presenter);
                }

                foreach (var child in current.GetVisualChildren().Concat(current.GetLogicalChildren()))
                {
                    _runtimeDetachTemplateContentPresentersTraversedChildCount++;
                    IncrementAggregate(ref _diagDetachTemplateContentPresentersTraversedChildCount);
                    pending.Push(child);
                }
            }
        }
        finally
        {
            _runtimeDetachTemplateContentPresentersElapsedTicks += Stopwatch.GetTimestamp() - start;
            RecordAggregateElapsed(ref _diagDetachTemplateContentPresentersCallCount, ref _diagDetachTemplateContentPresentersElapsedTicks, start);
        }
    }

    private static UserControlTelemetrySnapshot CreateAggregateTelemetrySnapshot(bool reset)
    {
        return new UserControlTelemetrySnapshot(
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagDependencyPropertyChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagDependencyPropertyChangedRejectedNonUiElementCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplatePropertyCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplateDetachCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplateCacheClearCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedTemplateRefreshCount, reset),
            ReadOrReset(ref _diagDependencyPropertyChangedOtherPropertyCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenNonTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenFilteredContentCount, reset),
            ReadOrReset(ref _diagGetVisualChildrenYieldedChildCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalNonTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildCountForTraversalFilteredContentCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalCallCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalNonTemplatePathCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalFilteredContentCount, reset),
            ReadOrReset(ref _diagGetVisualChildAtForTraversalOutOfRangeCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenCallCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenTemplatePathCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenNonTemplatePathCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenFilteredContentCount, reset),
            ReadOrReset(ref _diagGetLogicalChildrenYieldedChildCount, reset),
            ReadOrReset(ref _diagMeasureOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagMeasureOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagMeasureOverrideTemplatePathCount, reset),
            ReadOrReset(ref _diagMeasureOverrideNonTemplatePathCount, reset),
            ReadOrReset(ref _diagMeasureOverrideTemplateRootMeasureCount, reset),
            ReadOrReset(ref _diagMeasureOverrideTemplateReturnedZeroCount, reset),
            ReadOrReset(ref _diagMeasureOverrideNoContentCount, reset),
            ReadOrReset(ref _diagMeasureOverrideContentMeasureCount, reset),
            ReadOrReset(ref _diagArrangeOverrideCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagArrangeOverrideElapsedTicks, reset)),
            ReadOrReset(ref _diagArrangeOverrideTemplatePathCount, reset),
            ReadOrReset(ref _diagArrangeOverrideNonTemplatePathCount, reset),
            ReadOrReset(ref _diagArrangeOverrideTemplateRootArrangeCount, reset),
            ReadOrReset(ref _diagArrangeOverrideNoContentCount, reset),
            ReadOrReset(ref _diagArrangeOverrideContentArrangeCount, reset),
            ReadOrReset(ref _diagRenderCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRenderElapsedTicks, reset)),
            ReadOrReset(ref _diagRenderTemplateSkipCount, reset),
            ReadOrReset(ref _diagRenderBackgroundDrawCount, reset),
            ReadOrReset(ref _diagRenderBorderEdgeDrawCount, reset),
            ReadOrReset(ref _diagGetChromeThicknessCallCount, reset),
            ReadOrReset(ref _diagEnsureTemplateAppliedIfNeededCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagEnsureTemplateAppliedIfNeededElapsedTicks, reset)),
            ReadOrReset(ref _diagEnsureTemplateAppliedApplyTemplateCount, reset),
            ReadOrReset(ref _diagEnsureTemplateAppliedRefreshCachedRootCount, reset),
            ReadOrReset(ref _diagEnsureTemplateAppliedNoOpCount, reset),
            ReadOrReset(ref _diagRefreshCachedTemplateRootCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagRefreshCachedTemplateRootElapsedTicks, reset)),
            ReadOrReset(ref _diagRefreshCachedTemplateRootHitCount, reset),
            ReadOrReset(ref _diagRefreshCachedTemplateRootMissCount, reset),
            ReadOrReset(ref _diagRefreshCachedTemplateRootEnumeratedChildCount, reset),
            ReadOrReset(ref _diagDetachTemplateContentPresentersCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagDetachTemplateContentPresentersElapsedTicks, reset)),
            ReadOrReset(ref _diagDetachTemplateContentPresentersFallbackSearchCount, reset),
            ReadOrReset(ref _diagDetachTemplateContentPresentersRootNotFoundCount, reset),
            ReadOrReset(ref _diagDetachTemplateContentPresentersVisitedElementCount, reset),
            ReadOrReset(ref _diagDetachTemplateContentPresentersPresenterDetachCount, reset),
            ReadOrReset(ref _diagDetachTemplateContentPresentersTraversedChildCount, reset));
    }

    private static void IncrementAggregate(ref long counter)
    {
        Interlocked.Increment(ref counter);
    }

    private static void AddAggregate(ref long counter, long value)
    {
        Interlocked.Add(ref counter, value);
    }

    private static long ReadAggregate(ref long counter)
    {
        return Interlocked.Read(ref counter);
    }

    private static long ResetAggregate(ref long counter)
    {
        return Interlocked.Exchange(ref counter, 0);
    }

    private static long ReadOrReset(ref long counter, bool reset)
    {
        return reset ? ResetAggregate(ref counter) : ReadAggregate(ref counter);
    }

    private static void RecordAggregateElapsed(ref long callCount, ref long elapsedTicks, long start)
    {
        IncrementAggregate(ref callCount);
        AddAggregate(ref elapsedTicks, Stopwatch.GetTimestamp() - start);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }
}
