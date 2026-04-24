using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBox
{
    [Flags]
    private enum ViewportMetricChangeMask
    {
        None = 0,
        HorizontalOffset = 1 << 0,
        VerticalOffset = 1 << 1,
        ViewportWidth = 1 << 2,
        ViewportHeight = 1 << 3,
        ExtentWidth = 1 << 4,
        ExtentHeight = 1 << 5,
    }

    private enum ViewportNotificationSource
    {
        Unknown,
        ContentHostViewportChanged,
        SetScrollOffsets,
        PendingFlush,
    }

    private static readonly Lazy<Style> DefaultRichTextBoxStyle = new(BuildDefaultRichTextBoxStyle);
    private static int _diagContentHostViewportChangedCallCount;
    private static long _diagContentHostViewportChangedElapsedTicks;
    private static long _diagContentHostViewportChangedApplyPendingElapsedTicks;
    private static long _diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks;
    private static long _diagContentHostViewportChangedNotifyViewportElapsedTicks;
    private static int _diagContentHostViewportChangedNoMetricChangeCount;
    private static int _diagContentHostViewportChangedVerticalOffsetChangedCount;
    private static int _diagContentHostViewportChangedViewportHeightChangedCount;
    private static int _diagContentHostViewportChangedExtentHeightChangedCount;
    private static int _diagContentHostViewportChangedOnlyExtentHeightChangedCount;
    private static int _diagContentHostViewportChangedOnlyViewportHeightChangedCount;
    private static int _diagContentHostViewportChangedViewportAndExtentHeightChangedCount;
    private static int _diagApplyPendingContentHostScrollOffsetsCallCount;
    private static int _diagApplyPendingContentHostScrollOffsetsAppliedCount;
    private static int _diagApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount;
    private static int _diagApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount;
    private static int _diagApplyPendingContentHostScrollOffsetsSkippedNoPendingCount;
    private static long _diagApplyPendingContentHostScrollOffsetsElapsedTicks;
    private static int _diagEnsureHostedDocumentChildLayoutCallCount;
    private static int _diagEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount;
    private static int _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount;
    private static long _diagEnsureHostedDocumentChildLayoutElapsedTicks;
    private static int _diagQueueViewportChangedNotificationCallCount;
    private static int _diagQueueViewportChangedNotificationAlreadyPendingCount;
    private static int _diagFlushPendingViewportChangedNotificationCallCount;
    private static int _diagFlushPendingViewportChangedNotificationSkippedNoPendingCount;
    private static long _diagFlushPendingViewportChangedNotificationElapsedTicks;
    private static long _diagFlushPendingViewportChangedNotificationNotifyElapsedTicks;
    private static int _diagHostedScrollContentMeasureCallCount;
    private static long _diagHostedScrollContentMeasureElapsedTicks;
    private static int _diagHostedScrollContentArrangeCallCount;
    private static long _diagHostedScrollContentArrangeElapsedTicks;
    private static int _diagHostedRootRenderCallCount;
    private static long _diagHostedRootRenderElapsedTicks;
    private static long _diagHostedRootRenderLayoutResolveElapsedTicks;
    private static int _diagHostedScrollContentRenderCallCount;
    private static long _diagHostedScrollContentRenderElapsedTicks;
    private static long _diagHostedScrollContentRenderLayoutResolveElapsedTicks;
    private static long _diagHostedScrollContentRenderDocumentSurfaceElapsedTicks;
    private static int _diagCanReuseHostedContentMeasureCallCount;
    private static int _diagCanReuseHostedContentMeasureTrueCount;
    private static int _diagCanReuseHostedContentMeasureEquivalentWidthTrueCount;
    private static int _diagCanReuseHostedContentMeasureLayoutReuseTrueCount;
    private static int _diagNotifyViewportChangedCallCount;
    private static int _diagNotifyViewportChangedRaisedCount;
    private static int _diagNotifyViewportChangedSkippedNoChangeCount;
    private static int _diagNotifyViewportChangedFromContentHostCallCount;
    private static int _diagNotifyViewportChangedFromSetScrollOffsetsCallCount;
    private static int _diagNotifyViewportChangedFromPendingFlushCallCount;
    private static int _diagNotifyViewportChangedRaisedFromContentHostCount;
    private static int _diagNotifyViewportChangedRaisedFromSetScrollOffsetsCount;
    private static int _diagNotifyViewportChangedRaisedFromPendingFlushCount;
    private static int _diagNotifyViewportChangedRaisedVerticalOffsetChangedCount;
    private static int _diagNotifyViewportChangedRaisedViewportHeightChangedCount;
    private static int _diagNotifyViewportChangedRaisedExtentHeightChangedCount;
    private static int _diagNotifyViewportChangedRaisedOnlyExtentHeightChangedCount;
    private static int _diagNotifyViewportChangedRaisedOnlyViewportHeightChangedCount;
    private static int _diagNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount;
    private static long _diagNotifyViewportChangedElapsedTicks;
    private static long _diagNotifyViewportChangedSubscriberElapsedTicks;

    private readonly RichTextBoxScrollContentPresenter _scrollContentPresenter;
    private ScrollViewer? _contentHost;
    private bool _hasPendingContentHostScrollOffsets;
    private float _lastContentHostViewportChangedHorizontalOffset = float.NaN;
    private float _lastContentHostViewportChangedVerticalOffset = float.NaN;
    private float _lastContentHostViewportChangedViewportWidth = float.NaN;
    private float _lastContentHostViewportChangedViewportHeight = float.NaN;
    private float _lastContentHostViewportChangedExtentWidth = float.NaN;
    private float _lastContentHostViewportChangedExtentHeight = float.NaN;
    private int _runtimeContentHostViewportChangedCallCount;
    private long _runtimeContentHostViewportChangedElapsedTicks;
    private long _runtimeContentHostViewportChangedApplyPendingElapsedTicks;
    private long _runtimeContentHostViewportChangedEnsureHostedLayoutElapsedTicks;
    private long _runtimeContentHostViewportChangedNotifyViewportElapsedTicks;
    private int _runtimeContentHostViewportChangedNoMetricChangeCount;
    private int _runtimeContentHostViewportChangedVerticalOffsetChangedCount;
    private int _runtimeContentHostViewportChangedViewportHeightChangedCount;
    private int _runtimeContentHostViewportChangedExtentHeightChangedCount;
    private int _runtimeContentHostViewportChangedOnlyExtentHeightChangedCount;
    private int _runtimeContentHostViewportChangedOnlyViewportHeightChangedCount;
    private int _runtimeContentHostViewportChangedViewportAndExtentHeightChangedCount;
    private float _runtimeContentHostViewportChangedMaxVerticalOffsetDelta;
    private float _runtimeContentHostViewportChangedMaxViewportHeightDelta;
    private float _runtimeContentHostViewportChangedMaxExtentHeightDelta;
    private string _runtimeLastContentHostViewportChangedMask = "none";
    private int _runtimeApplyPendingContentHostScrollOffsetsCallCount;
    private int _runtimeApplyPendingContentHostScrollOffsetsAppliedCount;
    private int _runtimeApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount;
    private int _runtimeApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount;
    private int _runtimeApplyPendingContentHostScrollOffsetsSkippedNoPendingCount;
    private long _runtimeApplyPendingContentHostScrollOffsetsElapsedTicks;
    private int _runtimeEnsureHostedDocumentChildLayoutCallCount;
    private int _runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount;
    private int _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount;
    private long _runtimeEnsureHostedDocumentChildLayoutElapsedTicks;
    private int _runtimeQueueViewportChangedNotificationCallCount;
    private int _runtimeQueueViewportChangedNotificationAlreadyPendingCount;
    private int _runtimeFlushPendingViewportChangedNotificationCallCount;
    private int _runtimeFlushPendingViewportChangedNotificationSkippedNoPendingCount;
    private long _runtimeFlushPendingViewportChangedNotificationElapsedTicks;
    private long _runtimeFlushPendingViewportChangedNotificationNotifyElapsedTicks;
    private int _runtimeHostedScrollContentMeasureCallCount;
    private long _runtimeHostedScrollContentMeasureElapsedTicks;
    private float _runtimeLastHostedScrollContentMeasureAvailableWidth;
    private float _runtimeLastHostedScrollContentMeasureLayoutWidth;
    private float _runtimeLastHostedScrollContentMeasureDesiredWidth;
    private int _runtimeHostedScrollContentArrangeCallCount;
    private long _runtimeHostedScrollContentArrangeElapsedTicks;
    private float _runtimeLastHostedScrollContentArrangeWidth;
    private float _runtimeLastHostedScrollContentArrangeHeight;
    private int _runtimeHostedRootRenderCallCount;
    private long _runtimeHostedRootRenderElapsedTicks;
    private long _runtimeHostedRootRenderLayoutResolveElapsedTicks;
    private int _runtimeHostedScrollContentRenderCallCount;
    private long _runtimeHostedScrollContentRenderElapsedTicks;
    private long _runtimeHostedScrollContentRenderLayoutResolveElapsedTicks;
    private long _runtimeHostedScrollContentRenderDocumentSurfaceElapsedTicks;
    private int _runtimeCanReuseHostedContentMeasureCallCount;
    private int _runtimeCanReuseHostedContentMeasureTrueCount;
    private int _runtimeCanReuseHostedContentMeasureEquivalentWidthTrueCount;
    private int _runtimeCanReuseHostedContentMeasureLayoutReuseTrueCount;
    private float _runtimeLastCanReuseHostedContentMeasurePreviousWidth;
    private float _runtimeLastCanReuseHostedContentMeasureNextWidth;
    private float _runtimeLastCanReuseHostedContentMeasurePreviousLayoutWidth;
    private float _runtimeLastCanReuseHostedContentMeasureNextLayoutWidth;
    private bool _runtimeLastCanReuseHostedContentMeasureResult;
    private bool _runtimeLastCanReuseHostedContentMeasureEquivalentWidth;
    private int _runtimeNotifyViewportChangedCallCount;
    private int _runtimeNotifyViewportChangedRaisedCount;
    private int _runtimeNotifyViewportChangedSkippedNoChangeCount;
    private int _runtimeNotifyViewportChangedFromContentHostCallCount;
    private int _runtimeNotifyViewportChangedFromSetScrollOffsetsCallCount;
    private int _runtimeNotifyViewportChangedFromPendingFlushCallCount;
    private int _runtimeNotifyViewportChangedRaisedFromContentHostCount;
    private int _runtimeNotifyViewportChangedRaisedFromSetScrollOffsetsCount;
    private int _runtimeNotifyViewportChangedRaisedFromPendingFlushCount;
    private int _runtimeNotifyViewportChangedRaisedVerticalOffsetChangedCount;
    private int _runtimeNotifyViewportChangedRaisedViewportHeightChangedCount;
    private int _runtimeNotifyViewportChangedRaisedExtentHeightChangedCount;
    private int _runtimeNotifyViewportChangedRaisedOnlyExtentHeightChangedCount;
    private int _runtimeNotifyViewportChangedRaisedOnlyViewportHeightChangedCount;
    private int _runtimeNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount;
    private float _runtimeNotifyViewportChangedMaxVerticalOffsetDelta;
    private float _runtimeNotifyViewportChangedMaxViewportHeightDelta;
    private float _runtimeNotifyViewportChangedMaxExtentHeightDelta;
    private string _runtimeLastNotifyViewportChangedSource = "unknown";
    private string _runtimeLastNotifyViewportChangedMask = "none";
    private long _runtimeNotifyViewportChangedElapsedTicks;
    private long _runtimeNotifyViewportChangedSubscriberElapsedTicks;

    protected override Style? GetFallbackStyle()
    {
        return DefaultRichTextBoxStyle.Value;
    }

    public override void OnApplyTemplate()
    {
        DetachContentHost();
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_ContentHost") is not ScrollViewer contentHost)
        {
            return;
        }

        _contentHost = contentHost;
        _contentHost.ViewportChanged += OnContentHostViewportChanged;
        _contentHost.Content = _scrollContentPresenter;
        SyncContentHostProperties();
        ApplyPendingScrollOffsetsToContentHost();
        EnsureHostedDocumentChildLayout();
        QueueViewportChangedNotification();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (ReferenceEquals(args.Property, HorizontalScrollBarVisibilityProperty) ||
            ReferenceEquals(args.Property, VerticalScrollBarVisibilityProperty))
        {
            SyncContentHostProperties();
            _contentHost?.InvalidateScrollInfo();
            QueueViewportChangedNotification();
        }
    }

    private void DetachContentHost()
    {
        if (_contentHost == null)
        {
            return;
        }

        _contentHost.ViewportChanged -= OnContentHostViewportChanged;
        if (ReferenceEquals(_contentHost.Content, _scrollContentPresenter))
        {
            _contentHost.Content = null;
        }

        _contentHost = null;
    }

    private void OnContentHostViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        var startTicks = Stopwatch.GetTimestamp();
        _diagContentHostViewportChangedCallCount++;
        _runtimeContentHostViewportChangedCallCount++;
        var metrics = GetScrollMetrics();
        var changeMask = GetViewportMetricChangeMask(
            metrics,
            _lastContentHostViewportChangedHorizontalOffset,
            _lastContentHostViewportChangedVerticalOffset,
            _lastContentHostViewportChangedViewportWidth,
            _lastContentHostViewportChangedViewportHeight,
            _lastContentHostViewportChangedExtentWidth,
            _lastContentHostViewportChangedExtentHeight);
        TrackContentHostViewportChange(changeMask, metrics);
        _lastContentHostViewportChangedHorizontalOffset = metrics.HorizontalOffset;
        _lastContentHostViewportChangedVerticalOffset = metrics.VerticalOffset;
        _lastContentHostViewportChangedViewportWidth = metrics.ViewportWidth;
        _lastContentHostViewportChangedViewportHeight = metrics.ViewportHeight;
        _lastContentHostViewportChangedExtentWidth = metrics.ExtentWidth;
        _lastContentHostViewportChangedExtentHeight = metrics.ExtentHeight;

        var applyPendingStart = Stopwatch.GetTimestamp();
        ApplyPendingScrollOffsetsToContentHost();
        var applyPendingElapsed = Stopwatch.GetTimestamp() - applyPendingStart;
        _diagContentHostViewportChangedApplyPendingElapsedTicks += applyPendingElapsed;
        _runtimeContentHostViewportChangedApplyPendingElapsedTicks += applyPendingElapsed;

        var ensureHostedLayoutStart = Stopwatch.GetTimestamp();
        EnsureHostedDocumentChildLayout();
        var ensureHostedLayoutElapsed = Stopwatch.GetTimestamp() - ensureHostedLayoutStart;
        _diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks += ensureHostedLayoutElapsed;
        _runtimeContentHostViewportChangedEnsureHostedLayoutElapsedTicks += ensureHostedLayoutElapsed;

        var notifyViewportStart = Stopwatch.GetTimestamp();
    NotifyViewportChangedIfNeeded(ViewportNotificationSource.ContentHostViewportChanged);
        var notifyViewportElapsed = Stopwatch.GetTimestamp() - notifyViewportStart;
        _diagContentHostViewportChangedNotifyViewportElapsedTicks += notifyViewportElapsed;
        _runtimeContentHostViewportChangedNotifyViewportElapsedTicks += notifyViewportElapsed;

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagContentHostViewportChangedElapsedTicks += elapsedTicks;
        _runtimeContentHostViewportChangedElapsedTicks += elapsedTicks;
    }

    private void TrackContentHostViewportChange(ViewportMetricChangeMask changeMask, RichTextBoxScrollMetrics metrics)
    {
        if (changeMask == ViewportMetricChangeMask.None)
        {
            _diagContentHostViewportChangedNoMetricChangeCount++;
            _runtimeContentHostViewportChangedNoMetricChangeCount++;
        }

        if ((changeMask & ViewportMetricChangeMask.VerticalOffset) != 0)
        {
            _diagContentHostViewportChangedVerticalOffsetChangedCount++;
            _runtimeContentHostViewportChangedVerticalOffsetChangedCount++;
        }

        if ((changeMask & ViewportMetricChangeMask.ViewportHeight) != 0)
        {
            _diagContentHostViewportChangedViewportHeightChangedCount++;
            _runtimeContentHostViewportChangedViewportHeightChangedCount++;
        }

        if ((changeMask & ViewportMetricChangeMask.ExtentHeight) != 0)
        {
            _diagContentHostViewportChangedExtentHeightChangedCount++;
            _runtimeContentHostViewportChangedExtentHeightChangedCount++;
        }

        if (changeMask == ViewportMetricChangeMask.ExtentHeight)
        {
            _diagContentHostViewportChangedOnlyExtentHeightChangedCount++;
            _runtimeContentHostViewportChangedOnlyExtentHeightChangedCount++;
        }

        if (changeMask == ViewportMetricChangeMask.ViewportHeight)
        {
            _diagContentHostViewportChangedOnlyViewportHeightChangedCount++;
            _runtimeContentHostViewportChangedOnlyViewportHeightChangedCount++;
        }

        if (changeMask == (ViewportMetricChangeMask.ViewportHeight | ViewportMetricChangeMask.ExtentHeight))
        {
            _diagContentHostViewportChangedViewportAndExtentHeightChangedCount++;
            _runtimeContentHostViewportChangedViewportAndExtentHeightChangedCount++;
        }

        _runtimeContentHostViewportChangedMaxVerticalOffsetDelta = MathF.Max(
            _runtimeContentHostViewportChangedMaxVerticalOffsetDelta,
            MathF.Abs(metrics.VerticalOffset - _lastContentHostViewportChangedVerticalOffset));
        _runtimeContentHostViewportChangedMaxViewportHeightDelta = MathF.Max(
            _runtimeContentHostViewportChangedMaxViewportHeightDelta,
            MathF.Abs(metrics.ViewportHeight - _lastContentHostViewportChangedViewportHeight));
        _runtimeContentHostViewportChangedMaxExtentHeightDelta = MathF.Max(
            _runtimeContentHostViewportChangedMaxExtentHeightDelta,
            MathF.Abs(metrics.ExtentHeight - _lastContentHostViewportChangedExtentHeight));
        _runtimeLastContentHostViewportChangedMask = FormatViewportMetricChangeMask(changeMask);
    }

    private void SyncContentHostProperties()
    {
        if (_contentHost == null)
        {
            return;
        }

        _contentHost.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        _contentHost.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
    }

    private void ApplyPendingScrollOffsetsToContentHost()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagApplyPendingContentHostScrollOffsetsCallCount++;
        _runtimeApplyPendingContentHostScrollOffsetsCallCount++;

        if (_contentHost == null)
        {
            _diagApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount++;
            _runtimeApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount++;
            AccumulateApplyPendingScrollOffsetsElapsed(startTicks);
            return;
        }

        if (!HasUsableContentHostMetrics())
        {
            _diagApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount++;
            _runtimeApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount++;
            AccumulateApplyPendingScrollOffsetsElapsed(startTicks);
            return;
        }

        if (!_hasPendingContentHostScrollOffsets)
        {
            _diagApplyPendingContentHostScrollOffsetsSkippedNoPendingCount++;
            _runtimeApplyPendingContentHostScrollOffsetsSkippedNoPendingCount++;
            AccumulateApplyPendingScrollOffsetsElapsed(startTicks);
            return;
        }

        _contentHost.ScrollToHorizontalOffset(_horizontalOffset);
        _contentHost.ScrollToVerticalOffset(_verticalOffset);
        _horizontalOffset = _contentHost.HorizontalOffset;
        _verticalOffset = _contentHost.VerticalOffset;
        _hasPendingContentHostScrollOffsets = false;
        _diagApplyPendingContentHostScrollOffsetsAppliedCount++;
        _runtimeApplyPendingContentHostScrollOffsetsAppliedCount++;
        AccumulateApplyPendingScrollOffsetsElapsed(startTicks);
    }

    private void AccumulateApplyPendingScrollOffsetsElapsed(long startTicks)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagApplyPendingContentHostScrollOffsetsElapsedTicks += elapsedTicks;
        _runtimeApplyPendingContentHostScrollOffsetsElapsedTicks += elapsedTicks;
    }

    private float GetEffectiveHorizontalOffset()
    {
        return HasUsableContentHostMetrics() ? _contentHost!.HorizontalOffset : _horizontalOffset;
    }

    private float GetEffectiveVerticalOffset()
    {
        return HasUsableContentHostMetrics() ? _contentHost!.VerticalOffset : _verticalOffset;
    }

    private bool HasUsableContentHostMetrics()
    {
         return _contentHost != null &&
             (_contentHost.ViewportWidth > 0f || _contentHost.ViewportHeight > 0f) &&
             (_contentHost.ExtentWidth > 0f || _contentHost.ExtentHeight > 0f);
    }

    private float ResolveHostedContentLayoutWidth(float fallbackWidth)
    {
        if (TextWrapping == TextWrapping.NoWrap)
        {
            return float.PositiveInfinity;
        }

        if (float.IsFinite(fallbackWidth) && fallbackWidth > 0f)
        {
            return fallbackWidth;
        }

        if (_contentHost != null && _contentHost.ViewportWidth > 0f)
        {
            return _contentHost.ViewportWidth;
        }

        return Math.Max(0f, fallbackWidth);
    }

    private Vector2 MeasureHostedScrollContent(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagHostedScrollContentMeasureCallCount++;
        _runtimeHostedScrollContentMeasureCallCount++;
        _runtimeLastHostedScrollContentMeasureAvailableWidth = availableSize.X;
        var layoutWidth = ResolveHostedContentLayoutWidth(availableSize.X);
        _runtimeLastHostedScrollContentMeasureLayoutWidth = layoutWidth;
        var layout = BuildOrGetLayout(layoutWidth);
        _lastMeasuredLayout = layout;
        var desired = new Vector2(Math.Max(0f, layout.ContentWidth), Math.Max(0f, layout.ContentHeight));
        _runtimeLastHostedScrollContentMeasureDesiredWidth = desired.X;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagHostedScrollContentMeasureElapsedTicks += elapsedTicks;
        _runtimeHostedScrollContentMeasureElapsedTicks += elapsedTicks;
        return desired;
    }

    private bool CanReuseHostedContentMeasure(float previousFallbackWidth, float nextFallbackWidth)
    {
        _diagCanReuseHostedContentMeasureCallCount++;
        _runtimeCanReuseHostedContentMeasureCallCount++;
        _runtimeLastCanReuseHostedContentMeasurePreviousWidth = previousFallbackWidth;
        _runtimeLastCanReuseHostedContentMeasureNextWidth = nextFallbackWidth;
        var previousLayoutWidth = ResolveHostedContentLayoutWidth(previousFallbackWidth);
        var nextLayoutWidth = ResolveHostedContentLayoutWidth(nextFallbackWidth);
        _runtimeLastCanReuseHostedContentMeasurePreviousLayoutWidth = previousLayoutWidth;
        _runtimeLastCanReuseHostedContentMeasureNextLayoutWidth = nextLayoutWidth;
        var equivalentWidth = AreEquivalentDocumentLayoutWidths(previousLayoutWidth, nextLayoutWidth);
        _runtimeLastCanReuseHostedContentMeasureEquivalentWidth = equivalentWidth;
        if (equivalentWidth)
        {
            _diagCanReuseHostedContentMeasureTrueCount++;
            _runtimeCanReuseHostedContentMeasureTrueCount++;
            _diagCanReuseHostedContentMeasureEquivalentWidthTrueCount++;
            _runtimeCanReuseHostedContentMeasureEquivalentWidthTrueCount++;
            _runtimeLastCanReuseHostedContentMeasureResult = true;
            return true;
        }

        var reused = CanReuseDocumentLayoutForWidthChange(_lastMeasuredLayout ?? _lastRenderedLayout, previousLayoutWidth, nextLayoutWidth);
        if (reused)
        {
            _diagCanReuseHostedContentMeasureTrueCount++;
            _runtimeCanReuseHostedContentMeasureTrueCount++;
            _diagCanReuseHostedContentMeasureLayoutReuseTrueCount++;
            _runtimeCanReuseHostedContentMeasureLayoutReuseTrueCount++;
        }

        _runtimeLastCanReuseHostedContentMeasureResult = reused;
        return reused;
    }

    private static bool CanReuseDocumentLayoutForWidthChange(DocumentLayoutResult? layout, float previousWidth, float nextWidth)
    {
        if (layout == null)
        {
            return false;
        }

        if (AreEquivalentDocumentLayoutWidths(previousWidth, nextWidth))
        {
            return true;
        }

        if (!float.IsFinite(previousWidth) || !float.IsFinite(nextWidth))
        {
            return false;
        }

        if (nextWidth > previousWidth + 0.01f)
        {
            return false;
        }

        if (layout.HasConstrainedWrapping)
        {
            return false;
        }

        return layout.ContentWidth <= nextWidth + 0.01f;
    }

    private static bool AreEquivalentDocumentLayoutWidths(float left, float right)
    {
        if (float.IsPositiveInfinity(left) || float.IsPositiveInfinity(right))
        {
            return float.IsPositiveInfinity(left) == float.IsPositiveInfinity(right);
        }

        if (float.IsNaN(left) || float.IsNaN(right))
        {
            return false;
        }

        return MathF.Abs(left - right) <= 0.01f;
    }

    internal bool TryGetViewportLayoutSnapshot(out RichTextBoxViewportLayoutSnapshot snapshot)
    {
        var textRect = GetTextRect();
        if (textRect.Width <= 0f || textRect.Height <= 0f)
        {
            snapshot = default;
            return false;
        }

        var layout = BuildOrGetLayout(textRect.Width);
        ClampScrollOffsets(layout, textRect);
        snapshot = new RichTextBoxViewportLayoutSnapshot(
            layout,
            textRect,
            GetEffectiveHorizontalOffset(),
            GetEffectiveVerticalOffset());
        return true;
    }

    private void RenderHostedScrollContent(SpriteBatch spriteBatch, LayoutRect slot)
    {
        var renderStartTicks = Stopwatch.GetTimestamp();
        _diagHostedScrollContentRenderCallCount++;
        _runtimeHostedScrollContentRenderCallCount++;

        var layoutResolveStartTicks = Stopwatch.GetTimestamp();
        var layout = BuildOrGetLayout(ResolveHostedContentLayoutWidth(slot.Width));
        var layoutResolveElapsedTicks = Stopwatch.GetTimestamp() - layoutResolveStartTicks;
        _diagHostedScrollContentRenderLayoutResolveElapsedTicks += layoutResolveElapsedTicks;
        _runtimeHostedScrollContentRenderLayoutResolveElapsedTicks += layoutResolveElapsedTicks;

        var drawStartTicks = Stopwatch.GetTimestamp();
        RenderDocumentSurface(spriteBatch, slot, layout, 0f, 0f, includeHostedChildren: false);
        var drawElapsedTicks = Stopwatch.GetTimestamp() - drawStartTicks;
        _diagHostedScrollContentRenderDocumentSurfaceElapsedTicks += drawElapsedTicks;
        _runtimeHostedScrollContentRenderDocumentSurfaceElapsedTicks += drawElapsedTicks;

        var renderElapsedTicks = Stopwatch.GetTimestamp() - renderStartTicks;
        _diagHostedScrollContentRenderElapsedTicks += renderElapsedTicks;
        _runtimeHostedScrollContentRenderElapsedTicks += renderElapsedTicks;
    }

    private static Style BuildDefaultRichTextBoxStyle()
    {
        var style = new Style(typeof(RichTextBox));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultRichTextBoxTemplate()));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(202, 202, 202)));

        var focusedTrigger = new Trigger(IsFocusedProperty, true);
        focusedTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(94, 168, 255)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(168, 168, 168)));
        disabledTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(102, 102, 102)));

        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(focusedTrigger);
        style.Triggers.Add(disabledTrigger);
        return style;
    }

    private static ControlTemplate BuildDefaultRichTextBoxTemplate()
    {
        var template = new ControlTemplate(static _ =>
        {
            var border = new Border
            {
                Name = "PART_Border"
            };

            border.Child = new ScrollViewer
            {
                Name = "PART_ContentHost",
                Focusable = false
            };

            return border;
        })
        {
            TargetType = typeof(RichTextBox)
        };

        template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_ContentHost", FrameworkElement.MarginProperty, PaddingProperty);
        template.BindTemplate("PART_ContentHost", ScrollViewer.HorizontalScrollBarVisibilityProperty, HorizontalScrollBarVisibilityProperty);
        template.BindTemplate("PART_ContentHost", ScrollViewer.VerticalScrollBarVisibilityProperty, VerticalScrollBarVisibilityProperty);

        return template;
    }

    internal RichTextBoxRuntimeDiagnosticsSnapshot GetRichTextBoxSnapshotForDiagnostics()
    {
        var metrics = GetScrollMetrics();
        return new RichTextBoxRuntimeDiagnosticsSnapshot(
            HasContentHost: _contentHost != null,
            HasUsableContentHostMetrics: HasUsableContentHostMetrics(),
            HasPendingContentHostScrollOffsets: _hasPendingContentHostScrollOffsets,
            HorizontalOffset: metrics.HorizontalOffset,
            VerticalOffset: metrics.VerticalOffset,
            ViewportWidth: metrics.ViewportWidth,
            ViewportHeight: metrics.ViewportHeight,
            ExtentWidth: metrics.ExtentWidth,
            ExtentHeight: metrics.ExtentHeight,
            HostedDocumentVisualChildCount: _documentHostedVisualChildren.Count,
            ContentHostViewportChangedCallCount: _runtimeContentHostViewportChangedCallCount,
            ContentHostViewportChangedMilliseconds: TicksToMilliseconds(_runtimeContentHostViewportChangedElapsedTicks),
            ContentHostViewportChangedApplyPendingMilliseconds: TicksToMilliseconds(_runtimeContentHostViewportChangedApplyPendingElapsedTicks),
            ContentHostViewportChangedEnsureHostedLayoutMilliseconds: TicksToMilliseconds(_runtimeContentHostViewportChangedEnsureHostedLayoutElapsedTicks),
            ContentHostViewportChangedNotifyViewportMilliseconds: TicksToMilliseconds(_runtimeContentHostViewportChangedNotifyViewportElapsedTicks),
            ContentHostViewportChangedNoMetricChangeCount: _runtimeContentHostViewportChangedNoMetricChangeCount,
            ContentHostViewportChangedVerticalOffsetChangedCount: _runtimeContentHostViewportChangedVerticalOffsetChangedCount,
            ContentHostViewportChangedViewportHeightChangedCount: _runtimeContentHostViewportChangedViewportHeightChangedCount,
            ContentHostViewportChangedExtentHeightChangedCount: _runtimeContentHostViewportChangedExtentHeightChangedCount,
            ContentHostViewportChangedOnlyExtentHeightChangedCount: _runtimeContentHostViewportChangedOnlyExtentHeightChangedCount,
            ContentHostViewportChangedOnlyViewportHeightChangedCount: _runtimeContentHostViewportChangedOnlyViewportHeightChangedCount,
            ContentHostViewportChangedViewportAndExtentHeightChangedCount: _runtimeContentHostViewportChangedViewportAndExtentHeightChangedCount,
            ContentHostViewportChangedMaxVerticalOffsetDelta: _runtimeContentHostViewportChangedMaxVerticalOffsetDelta,
            ContentHostViewportChangedMaxViewportHeightDelta: _runtimeContentHostViewportChangedMaxViewportHeightDelta,
            ContentHostViewportChangedMaxExtentHeightDelta: _runtimeContentHostViewportChangedMaxExtentHeightDelta,
            LastContentHostViewportChangedMask: _runtimeLastContentHostViewportChangedMask,
            ApplyPendingContentHostScrollOffsetsCallCount: _runtimeApplyPendingContentHostScrollOffsetsCallCount,
            ApplyPendingContentHostScrollOffsetsAppliedCount: _runtimeApplyPendingContentHostScrollOffsetsAppliedCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount: _runtimeApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount: _runtimeApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoPendingCount: _runtimeApplyPendingContentHostScrollOffsetsSkippedNoPendingCount,
            ApplyPendingContentHostScrollOffsetsMilliseconds: TicksToMilliseconds(_runtimeApplyPendingContentHostScrollOffsetsElapsedTicks),
            EnsureHostedDocumentChildLayoutCallCount: _runtimeEnsureHostedDocumentChildLayoutCallCount,
            EnsureHostedDocumentChildLayoutSkippedZeroTextRectCount: _runtimeEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount,
            EnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount: _runtimeEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount,
            EnsureHostedDocumentChildLayoutMilliseconds: TicksToMilliseconds(_runtimeEnsureHostedDocumentChildLayoutElapsedTicks),
            QueueViewportChangedNotificationCallCount: _runtimeQueueViewportChangedNotificationCallCount,
            QueueViewportChangedNotificationAlreadyPendingCount: _runtimeQueueViewportChangedNotificationAlreadyPendingCount,
            FlushPendingViewportChangedNotificationCallCount: _runtimeFlushPendingViewportChangedNotificationCallCount,
            FlushPendingViewportChangedNotificationSkippedNoPendingCount: _runtimeFlushPendingViewportChangedNotificationSkippedNoPendingCount,
            FlushPendingViewportChangedNotificationMilliseconds: TicksToMilliseconds(_runtimeFlushPendingViewportChangedNotificationElapsedTicks),
            FlushPendingViewportChangedNotificationNotifyMilliseconds: TicksToMilliseconds(_runtimeFlushPendingViewportChangedNotificationNotifyElapsedTicks),
            HostedScrollContentMeasureCallCount: _runtimeHostedScrollContentMeasureCallCount,
            HostedScrollContentMeasureMilliseconds: TicksToMilliseconds(_runtimeHostedScrollContentMeasureElapsedTicks),
            LastHostedScrollContentMeasureAvailableWidth: _runtimeLastHostedScrollContentMeasureAvailableWidth,
            LastHostedScrollContentMeasureLayoutWidth: _runtimeLastHostedScrollContentMeasureLayoutWidth,
            LastHostedScrollContentMeasureDesiredWidth: _runtimeLastHostedScrollContentMeasureDesiredWidth,
            HostedScrollContentArrangeCallCount: _runtimeHostedScrollContentArrangeCallCount,
            HostedScrollContentArrangeMilliseconds: TicksToMilliseconds(_runtimeHostedScrollContentArrangeElapsedTicks),
            LastHostedScrollContentArrangeWidth: _runtimeLastHostedScrollContentArrangeWidth,
            LastHostedScrollContentArrangeHeight: _runtimeLastHostedScrollContentArrangeHeight,
            HostedRootRenderCallCount: _runtimeHostedRootRenderCallCount,
            HostedRootRenderMilliseconds: TicksToMilliseconds(_runtimeHostedRootRenderElapsedTicks),
            HostedRootRenderLayoutResolveMilliseconds: TicksToMilliseconds(_runtimeHostedRootRenderLayoutResolveElapsedTicks),
            HostedScrollContentRenderCallCount: _runtimeHostedScrollContentRenderCallCount,
            HostedScrollContentRenderMilliseconds: TicksToMilliseconds(_runtimeHostedScrollContentRenderElapsedTicks),
            HostedScrollContentRenderLayoutResolveMilliseconds: TicksToMilliseconds(_runtimeHostedScrollContentRenderLayoutResolveElapsedTicks),
            HostedScrollContentRenderDocumentSurfaceMilliseconds: TicksToMilliseconds(_runtimeHostedScrollContentRenderDocumentSurfaceElapsedTicks),
            CanReuseHostedContentMeasureCallCount: _runtimeCanReuseHostedContentMeasureCallCount,
            CanReuseHostedContentMeasureTrueCount: _runtimeCanReuseHostedContentMeasureTrueCount,
            CanReuseHostedContentMeasureEquivalentWidthTrueCount: _runtimeCanReuseHostedContentMeasureEquivalentWidthTrueCount,
            CanReuseHostedContentMeasureLayoutReuseTrueCount: _runtimeCanReuseHostedContentMeasureLayoutReuseTrueCount,
            LastCanReuseHostedContentMeasurePreviousWidth: _runtimeLastCanReuseHostedContentMeasurePreviousWidth,
            LastCanReuseHostedContentMeasureNextWidth: _runtimeLastCanReuseHostedContentMeasureNextWidth,
            LastCanReuseHostedContentMeasurePreviousLayoutWidth: _runtimeLastCanReuseHostedContentMeasurePreviousLayoutWidth,
            LastCanReuseHostedContentMeasureNextLayoutWidth: _runtimeLastCanReuseHostedContentMeasureNextLayoutWidth,
            LastCanReuseHostedContentMeasureResult: _runtimeLastCanReuseHostedContentMeasureResult,
            LastCanReuseHostedContentMeasureEquivalentWidth: _runtimeLastCanReuseHostedContentMeasureEquivalentWidth,
            NotifyViewportChangedCallCount: _runtimeNotifyViewportChangedCallCount,
            NotifyViewportChangedRaisedCount: _runtimeNotifyViewportChangedRaisedCount,
            NotifyViewportChangedSkippedNoChangeCount: _runtimeNotifyViewportChangedSkippedNoChangeCount,
                NotifyViewportChangedFromContentHostCallCount: _runtimeNotifyViewportChangedFromContentHostCallCount,
                NotifyViewportChangedFromSetScrollOffsetsCallCount: _runtimeNotifyViewportChangedFromSetScrollOffsetsCallCount,
                NotifyViewportChangedFromPendingFlushCallCount: _runtimeNotifyViewportChangedFromPendingFlushCallCount,
                NotifyViewportChangedRaisedFromContentHostCount: _runtimeNotifyViewportChangedRaisedFromContentHostCount,
                NotifyViewportChangedRaisedFromSetScrollOffsetsCount: _runtimeNotifyViewportChangedRaisedFromSetScrollOffsetsCount,
                NotifyViewportChangedRaisedFromPendingFlushCount: _runtimeNotifyViewportChangedRaisedFromPendingFlushCount,
                NotifyViewportChangedRaisedVerticalOffsetChangedCount: _runtimeNotifyViewportChangedRaisedVerticalOffsetChangedCount,
                NotifyViewportChangedRaisedViewportHeightChangedCount: _runtimeNotifyViewportChangedRaisedViewportHeightChangedCount,
                NotifyViewportChangedRaisedExtentHeightChangedCount: _runtimeNotifyViewportChangedRaisedExtentHeightChangedCount,
                NotifyViewportChangedRaisedOnlyExtentHeightChangedCount: _runtimeNotifyViewportChangedRaisedOnlyExtentHeightChangedCount,
                NotifyViewportChangedRaisedOnlyViewportHeightChangedCount: _runtimeNotifyViewportChangedRaisedOnlyViewportHeightChangedCount,
                NotifyViewportChangedRaisedViewportAndExtentHeightChangedCount: _runtimeNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount,
                NotifyViewportChangedMaxVerticalOffsetDelta: _runtimeNotifyViewportChangedMaxVerticalOffsetDelta,
                NotifyViewportChangedMaxViewportHeightDelta: _runtimeNotifyViewportChangedMaxViewportHeightDelta,
                NotifyViewportChangedMaxExtentHeightDelta: _runtimeNotifyViewportChangedMaxExtentHeightDelta,
                LastNotifyViewportChangedSource: _runtimeLastNotifyViewportChangedSource,
                LastNotifyViewportChangedMask: _runtimeLastNotifyViewportChangedMask,
            NotifyViewportChangedMilliseconds: TicksToMilliseconds(_runtimeNotifyViewportChangedElapsedTicks),
            NotifyViewportChangedSubscriberMilliseconds: TicksToMilliseconds(_runtimeNotifyViewportChangedSubscriberElapsedTicks));
    }

    internal new static RichTextBoxTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal new static RichTextBoxTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private static RichTextBoxTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        var snapshot = new RichTextBoxTelemetrySnapshot(
            ContentHostViewportChangedCallCount: _diagContentHostViewportChangedCallCount,
            ContentHostViewportChangedMilliseconds: TicksToMilliseconds(_diagContentHostViewportChangedElapsedTicks),
            ContentHostViewportChangedApplyPendingMilliseconds: TicksToMilliseconds(_diagContentHostViewportChangedApplyPendingElapsedTicks),
            ContentHostViewportChangedEnsureHostedLayoutMilliseconds: TicksToMilliseconds(_diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks),
            ContentHostViewportChangedNotifyViewportMilliseconds: TicksToMilliseconds(_diagContentHostViewportChangedNotifyViewportElapsedTicks),
            ContentHostViewportChangedNoMetricChangeCount: _diagContentHostViewportChangedNoMetricChangeCount,
            ContentHostViewportChangedVerticalOffsetChangedCount: _diagContentHostViewportChangedVerticalOffsetChangedCount,
            ContentHostViewportChangedViewportHeightChangedCount: _diagContentHostViewportChangedViewportHeightChangedCount,
            ContentHostViewportChangedExtentHeightChangedCount: _diagContentHostViewportChangedExtentHeightChangedCount,
            ContentHostViewportChangedOnlyExtentHeightChangedCount: _diagContentHostViewportChangedOnlyExtentHeightChangedCount,
            ContentHostViewportChangedOnlyViewportHeightChangedCount: _diagContentHostViewportChangedOnlyViewportHeightChangedCount,
            ContentHostViewportChangedViewportAndExtentHeightChangedCount: _diagContentHostViewportChangedViewportAndExtentHeightChangedCount,
            ApplyPendingContentHostScrollOffsetsCallCount: _diagApplyPendingContentHostScrollOffsetsCallCount,
            ApplyPendingContentHostScrollOffsetsAppliedCount: _diagApplyPendingContentHostScrollOffsetsAppliedCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount: _diagApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount: _diagApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount,
            ApplyPendingContentHostScrollOffsetsSkippedNoPendingCount: _diagApplyPendingContentHostScrollOffsetsSkippedNoPendingCount,
            ApplyPendingContentHostScrollOffsetsMilliseconds: TicksToMilliseconds(_diagApplyPendingContentHostScrollOffsetsElapsedTicks),
            EnsureHostedDocumentChildLayoutCallCount: _diagEnsureHostedDocumentChildLayoutCallCount,
            EnsureHostedDocumentChildLayoutSkippedZeroTextRectCount: _diagEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount,
            EnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount: _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount,
            EnsureHostedDocumentChildLayoutMilliseconds: TicksToMilliseconds(_diagEnsureHostedDocumentChildLayoutElapsedTicks),
            QueueViewportChangedNotificationCallCount: _diagQueueViewportChangedNotificationCallCount,
            QueueViewportChangedNotificationAlreadyPendingCount: _diagQueueViewportChangedNotificationAlreadyPendingCount,
            FlushPendingViewportChangedNotificationCallCount: _diagFlushPendingViewportChangedNotificationCallCount,
            FlushPendingViewportChangedNotificationSkippedNoPendingCount: _diagFlushPendingViewportChangedNotificationSkippedNoPendingCount,
            FlushPendingViewportChangedNotificationMilliseconds: TicksToMilliseconds(_diagFlushPendingViewportChangedNotificationElapsedTicks),
            FlushPendingViewportChangedNotificationNotifyMilliseconds: TicksToMilliseconds(_diagFlushPendingViewportChangedNotificationNotifyElapsedTicks),
            HostedScrollContentMeasureCallCount: _diagHostedScrollContentMeasureCallCount,
            HostedScrollContentMeasureMilliseconds: TicksToMilliseconds(_diagHostedScrollContentMeasureElapsedTicks),
            HostedScrollContentArrangeCallCount: _diagHostedScrollContentArrangeCallCount,
            HostedScrollContentArrangeMilliseconds: TicksToMilliseconds(_diagHostedScrollContentArrangeElapsedTicks),
            HostedRootRenderCallCount: _diagHostedRootRenderCallCount,
            HostedRootRenderMilliseconds: TicksToMilliseconds(_diagHostedRootRenderElapsedTicks),
            HostedRootRenderLayoutResolveMilliseconds: TicksToMilliseconds(_diagHostedRootRenderLayoutResolveElapsedTicks),
            HostedScrollContentRenderCallCount: _diagHostedScrollContentRenderCallCount,
            HostedScrollContentRenderMilliseconds: TicksToMilliseconds(_diagHostedScrollContentRenderElapsedTicks),
            HostedScrollContentRenderLayoutResolveMilliseconds: TicksToMilliseconds(_diagHostedScrollContentRenderLayoutResolveElapsedTicks),
            HostedScrollContentRenderDocumentSurfaceMilliseconds: TicksToMilliseconds(_diagHostedScrollContentRenderDocumentSurfaceElapsedTicks),
            CanReuseHostedContentMeasureCallCount: _diagCanReuseHostedContentMeasureCallCount,
            CanReuseHostedContentMeasureTrueCount: _diagCanReuseHostedContentMeasureTrueCount,
            CanReuseHostedContentMeasureEquivalentWidthTrueCount: _diagCanReuseHostedContentMeasureEquivalentWidthTrueCount,
            CanReuseHostedContentMeasureLayoutReuseTrueCount: _diagCanReuseHostedContentMeasureLayoutReuseTrueCount,
            NotifyViewportChangedCallCount: _diagNotifyViewportChangedCallCount,
            NotifyViewportChangedRaisedCount: _diagNotifyViewportChangedRaisedCount,
            NotifyViewportChangedSkippedNoChangeCount: _diagNotifyViewportChangedSkippedNoChangeCount,
            NotifyViewportChangedFromContentHostCallCount: _diagNotifyViewportChangedFromContentHostCallCount,
            NotifyViewportChangedFromSetScrollOffsetsCallCount: _diagNotifyViewportChangedFromSetScrollOffsetsCallCount,
            NotifyViewportChangedFromPendingFlushCallCount: _diagNotifyViewportChangedFromPendingFlushCallCount,
            NotifyViewportChangedRaisedFromContentHostCount: _diagNotifyViewportChangedRaisedFromContentHostCount,
            NotifyViewportChangedRaisedFromSetScrollOffsetsCount: _diagNotifyViewportChangedRaisedFromSetScrollOffsetsCount,
            NotifyViewportChangedRaisedFromPendingFlushCount: _diagNotifyViewportChangedRaisedFromPendingFlushCount,
            NotifyViewportChangedRaisedVerticalOffsetChangedCount: _diagNotifyViewportChangedRaisedVerticalOffsetChangedCount,
            NotifyViewportChangedRaisedViewportHeightChangedCount: _diagNotifyViewportChangedRaisedViewportHeightChangedCount,
            NotifyViewportChangedRaisedExtentHeightChangedCount: _diagNotifyViewportChangedRaisedExtentHeightChangedCount,
            NotifyViewportChangedRaisedOnlyExtentHeightChangedCount: _diagNotifyViewportChangedRaisedOnlyExtentHeightChangedCount,
            NotifyViewportChangedRaisedOnlyViewportHeightChangedCount: _diagNotifyViewportChangedRaisedOnlyViewportHeightChangedCount,
            NotifyViewportChangedRaisedViewportAndExtentHeightChangedCount: _diagNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount,
            NotifyViewportChangedMilliseconds: TicksToMilliseconds(_diagNotifyViewportChangedElapsedTicks),
            NotifyViewportChangedSubscriberMilliseconds: TicksToMilliseconds(_diagNotifyViewportChangedSubscriberElapsedTicks));

        if (reset)
        {
            _diagContentHostViewportChangedCallCount = 0;
            _diagContentHostViewportChangedElapsedTicks = 0;
            _diagContentHostViewportChangedApplyPendingElapsedTicks = 0;
            _diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks = 0;
            _diagContentHostViewportChangedNotifyViewportElapsedTicks = 0;
            _diagContentHostViewportChangedNoMetricChangeCount = 0;
            _diagContentHostViewportChangedVerticalOffsetChangedCount = 0;
            _diagContentHostViewportChangedViewportHeightChangedCount = 0;
            _diagContentHostViewportChangedExtentHeightChangedCount = 0;
            _diagContentHostViewportChangedOnlyExtentHeightChangedCount = 0;
            _diagContentHostViewportChangedOnlyViewportHeightChangedCount = 0;
            _diagContentHostViewportChangedViewportAndExtentHeightChangedCount = 0;
            _diagApplyPendingContentHostScrollOffsetsCallCount = 0;
            _diagApplyPendingContentHostScrollOffsetsAppliedCount = 0;
            _diagApplyPendingContentHostScrollOffsetsSkippedNoContentHostCount = 0;
            _diagApplyPendingContentHostScrollOffsetsSkippedNoMetricsCount = 0;
            _diagApplyPendingContentHostScrollOffsetsSkippedNoPendingCount = 0;
            _diagApplyPendingContentHostScrollOffsetsElapsedTicks = 0;
            _diagEnsureHostedDocumentChildLayoutCallCount = 0;
            _diagEnsureHostedDocumentChildLayoutSkippedZeroTextRectCount = 0;
            _diagEnsureHostedDocumentChildLayoutSkippedNoHostedChildrenCount = 0;
            _diagEnsureHostedDocumentChildLayoutElapsedTicks = 0;
            _diagQueueViewportChangedNotificationCallCount = 0;
            _diagQueueViewportChangedNotificationAlreadyPendingCount = 0;
            _diagFlushPendingViewportChangedNotificationCallCount = 0;
            _diagFlushPendingViewportChangedNotificationSkippedNoPendingCount = 0;
            _diagFlushPendingViewportChangedNotificationElapsedTicks = 0;
            _diagFlushPendingViewportChangedNotificationNotifyElapsedTicks = 0;
            _diagHostedScrollContentMeasureCallCount = 0;
            _diagHostedScrollContentMeasureElapsedTicks = 0;
            _diagHostedScrollContentArrangeCallCount = 0;
            _diagHostedScrollContentArrangeElapsedTicks = 0;
            _diagHostedRootRenderCallCount = 0;
            _diagHostedRootRenderElapsedTicks = 0;
            _diagHostedRootRenderLayoutResolveElapsedTicks = 0;
            _diagHostedScrollContentRenderCallCount = 0;
            _diagHostedScrollContentRenderElapsedTicks = 0;
            _diagHostedScrollContentRenderLayoutResolveElapsedTicks = 0;
            _diagHostedScrollContentRenderDocumentSurfaceElapsedTicks = 0;
            _diagCanReuseHostedContentMeasureCallCount = 0;
            _diagCanReuseHostedContentMeasureTrueCount = 0;
            _diagCanReuseHostedContentMeasureEquivalentWidthTrueCount = 0;
            _diagCanReuseHostedContentMeasureLayoutReuseTrueCount = 0;
            _diagNotifyViewportChangedCallCount = 0;
            _diagNotifyViewportChangedRaisedCount = 0;
            _diagNotifyViewportChangedSkippedNoChangeCount = 0;
            _diagNotifyViewportChangedFromContentHostCallCount = 0;
            _diagNotifyViewportChangedFromSetScrollOffsetsCallCount = 0;
            _diagNotifyViewportChangedFromPendingFlushCallCount = 0;
            _diagNotifyViewportChangedRaisedFromContentHostCount = 0;
            _diagNotifyViewportChangedRaisedFromSetScrollOffsetsCount = 0;
            _diagNotifyViewportChangedRaisedFromPendingFlushCount = 0;
            _diagNotifyViewportChangedRaisedVerticalOffsetChangedCount = 0;
            _diagNotifyViewportChangedRaisedViewportHeightChangedCount = 0;
            _diagNotifyViewportChangedRaisedExtentHeightChangedCount = 0;
            _diagNotifyViewportChangedRaisedOnlyExtentHeightChangedCount = 0;
            _diagNotifyViewportChangedRaisedOnlyViewportHeightChangedCount = 0;
            _diagNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount = 0;
            _diagNotifyViewportChangedElapsedTicks = 0;
            _diagNotifyViewportChangedSubscriberElapsedTicks = 0;
        }

        return snapshot;
    }

    private static ViewportMetricChangeMask GetViewportMetricChangeMask(
        RichTextBoxScrollMetrics metrics,
        float previousHorizontalOffset,
        float previousVerticalOffset,
        float previousViewportWidth,
        float previousViewportHeight,
        float previousExtentWidth,
        float previousExtentHeight)
    {
        var changeMask = ViewportMetricChangeMask.None;
        if (HasViewportMetricChanged(metrics.HorizontalOffset, previousHorizontalOffset))
        {
            changeMask |= ViewportMetricChangeMask.HorizontalOffset;
        }

        if (HasViewportMetricChanged(metrics.VerticalOffset, previousVerticalOffset))
        {
            changeMask |= ViewportMetricChangeMask.VerticalOffset;
        }

        if (HasViewportMetricChanged(metrics.ViewportWidth, previousViewportWidth))
        {
            changeMask |= ViewportMetricChangeMask.ViewportWidth;
        }

        if (HasViewportMetricChanged(metrics.ViewportHeight, previousViewportHeight))
        {
            changeMask |= ViewportMetricChangeMask.ViewportHeight;
        }

        if (HasViewportMetricChanged(metrics.ExtentWidth, previousExtentWidth))
        {
            changeMask |= ViewportMetricChangeMask.ExtentWidth;
        }

        if (HasViewportMetricChanged(metrics.ExtentHeight, previousExtentHeight))
        {
            changeMask |= ViewportMetricChangeMask.ExtentHeight;
        }

        return changeMask;
    }

    private static bool HasViewportMetricChanged(float currentValue, float previousValue)
    {
        if (!float.IsFinite(previousValue) || !float.IsFinite(currentValue))
        {
            return !float.IsFinite(previousValue) != !float.IsFinite(currentValue) ||
                   (float.IsFinite(currentValue) && !float.IsFinite(previousValue));
        }

        return Math.Abs(currentValue - previousValue) > 0.01f;
    }

    private static string FormatViewportMetricChangeMask(ViewportMetricChangeMask changeMask)
    {
        return changeMask == ViewportMetricChangeMask.None ? "none" : changeMask.ToString();
    }

    private void TrackNotifyViewportChangedCallSource(ViewportNotificationSource source)
    {
        switch (source)
        {
            case ViewportNotificationSource.ContentHostViewportChanged:
                _diagNotifyViewportChangedFromContentHostCallCount++;
                _runtimeNotifyViewportChangedFromContentHostCallCount++;
                break;
            case ViewportNotificationSource.SetScrollOffsets:
                _diagNotifyViewportChangedFromSetScrollOffsetsCallCount++;
                _runtimeNotifyViewportChangedFromSetScrollOffsetsCallCount++;
                break;
            case ViewportNotificationSource.PendingFlush:
                _diagNotifyViewportChangedFromPendingFlushCallCount++;
                _runtimeNotifyViewportChangedFromPendingFlushCallCount++;
                break;
        }
    }

    private void TrackNotifyViewportChangedRaise(
        ViewportNotificationSource source,
        ViewportMetricChangeMask changeMask,
        RichTextBoxScrollMetrics metrics)
    {
        switch (source)
        {
            case ViewportNotificationSource.ContentHostViewportChanged:
                _diagNotifyViewportChangedRaisedFromContentHostCount++;
                _runtimeNotifyViewportChangedRaisedFromContentHostCount++;
                break;
            case ViewportNotificationSource.SetScrollOffsets:
                _diagNotifyViewportChangedRaisedFromSetScrollOffsetsCount++;
                _runtimeNotifyViewportChangedRaisedFromSetScrollOffsetsCount++;
                break;
            case ViewportNotificationSource.PendingFlush:
                _diagNotifyViewportChangedRaisedFromPendingFlushCount++;
                _runtimeNotifyViewportChangedRaisedFromPendingFlushCount++;
                break;
        }

        if ((changeMask & ViewportMetricChangeMask.VerticalOffset) != 0)
        {
            _diagNotifyViewportChangedRaisedVerticalOffsetChangedCount++;
            _runtimeNotifyViewportChangedRaisedVerticalOffsetChangedCount++;
        }

        if ((changeMask & ViewportMetricChangeMask.ViewportHeight) != 0)
        {
            _diagNotifyViewportChangedRaisedViewportHeightChangedCount++;
            _runtimeNotifyViewportChangedRaisedViewportHeightChangedCount++;
        }

        if ((changeMask & ViewportMetricChangeMask.ExtentHeight) != 0)
        {
            _diagNotifyViewportChangedRaisedExtentHeightChangedCount++;
            _runtimeNotifyViewportChangedRaisedExtentHeightChangedCount++;
        }

        if (changeMask == ViewportMetricChangeMask.ExtentHeight)
        {
            _diagNotifyViewportChangedRaisedOnlyExtentHeightChangedCount++;
            _runtimeNotifyViewportChangedRaisedOnlyExtentHeightChangedCount++;
        }

        if (changeMask == ViewportMetricChangeMask.ViewportHeight)
        {
            _diagNotifyViewportChangedRaisedOnlyViewportHeightChangedCount++;
            _runtimeNotifyViewportChangedRaisedOnlyViewportHeightChangedCount++;
        }

        if (changeMask == (ViewportMetricChangeMask.ViewportHeight | ViewportMetricChangeMask.ExtentHeight))
        {
            _diagNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount++;
            _runtimeNotifyViewportChangedRaisedViewportAndExtentHeightChangedCount++;
        }

        _runtimeNotifyViewportChangedMaxVerticalOffsetDelta = MathF.Max(
            _runtimeNotifyViewportChangedMaxVerticalOffsetDelta,
            MathF.Abs(metrics.VerticalOffset - _lastViewportChangedVerticalOffset));
        _runtimeNotifyViewportChangedMaxViewportHeightDelta = MathF.Max(
            _runtimeNotifyViewportChangedMaxViewportHeightDelta,
            MathF.Abs(metrics.ViewportHeight - _lastViewportChangedViewportHeight));
        _runtimeNotifyViewportChangedMaxExtentHeightDelta = MathF.Max(
            _runtimeNotifyViewportChangedMaxExtentHeightDelta,
            MathF.Abs(metrics.ExtentHeight - _lastViewportChangedExtentHeight));
        _runtimeLastNotifyViewportChangedSource = source.ToString();
        _runtimeLastNotifyViewportChangedMask = FormatViewportMetricChangeMask(changeMask);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private sealed class RichTextBoxScrollContentPresenter : FrameworkElement, IHyperlinkHoverHost, IScrollViewerMeasureConstraintProvider
    {
        private readonly RichTextBox _owner;

        public RichTextBoxScrollContentPresenter(RichTextBox owner)
        {
            _owner = owner;
            Focusable = false;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _owner.MeasureHostedScrollContent(availableSize);
        }

        public Vector2 GetScrollViewerMeasureConstraint(
            float viewportWidth,
            float viewportHeight,
            bool canScrollHorizontally,
            bool canScrollVertically)
        {
            var horizontalConstraint = canScrollHorizontally && _owner.TextWrapping == TextWrapping.NoWrap
                ? float.PositiveInfinity
                : MathF.Max(0f, viewportWidth);
            var verticalConstraint = canScrollVertically
                ? float.PositiveInfinity
                : MathF.Max(0f, viewportHeight);
            return new Vector2(horizontalConstraint, verticalConstraint);
        }

        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return _owner.CanReuseHostedContentMeasure(previousAvailableSize.X, nextAvailableSize.X);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var startTicks = Stopwatch.GetTimestamp();
            RichTextBox._diagHostedScrollContentArrangeCallCount++;
            _owner._runtimeHostedScrollContentArrangeCallCount++;
            _owner._runtimeLastHostedScrollContentArrangeWidth = finalSize.X;
            _owner._runtimeLastHostedScrollContentArrangeHeight = finalSize.Y;
            var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            RichTextBox._diagHostedScrollContentArrangeElapsedTicks += elapsedTicks;
            _owner._runtimeHostedScrollContentArrangeElapsedTicks += elapsedTicks;
            return finalSize;
        }

        protected override void OnRender(SpriteBatch spriteBatch)
        {
            _owner.RenderHostedScrollContent(spriteBatch, LayoutSlot);
        }

        public void UpdateHoveredHyperlinkFromPointer(Vector2 pointerPosition)
        {
            _owner.UpdateHoveredHyperlinkFromPointer(pointerPosition);
        }
    }
}

internal readonly record struct RichTextBoxViewportLayoutSnapshot(
    DocumentLayoutResult Layout,
    LayoutRect TextRect,
    float HorizontalOffset,
    float VerticalOffset);
