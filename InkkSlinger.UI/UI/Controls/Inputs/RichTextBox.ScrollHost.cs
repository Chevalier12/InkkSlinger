using System;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class RichTextBox
{
    private static readonly Lazy<Style> DefaultRichTextBoxStyle = new(BuildDefaultRichTextBoxStyle);
    private static int _diagContentHostViewportChangedCallCount;
    private static long _diagContentHostViewportChangedElapsedTicks;
    private static long _diagContentHostViewportChangedApplyPendingElapsedTicks;
    private static long _diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks;
    private static long _diagContentHostViewportChangedNotifyViewportElapsedTicks;
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
    private static int _diagNotifyViewportChangedCallCount;
    private static int _diagNotifyViewportChangedRaisedCount;
    private static int _diagNotifyViewportChangedSkippedNoChangeCount;
    private static long _diagNotifyViewportChangedElapsedTicks;
    private static long _diagNotifyViewportChangedSubscriberElapsedTicks;

    private readonly RichTextBoxScrollContentPresenter _scrollContentPresenter;
    private ScrollViewer? _contentHost;
    private bool _hasPendingContentHostScrollOffsets;
    private int _runtimeContentHostViewportChangedCallCount;
    private long _runtimeContentHostViewportChangedElapsedTicks;
    private long _runtimeContentHostViewportChangedApplyPendingElapsedTicks;
    private long _runtimeContentHostViewportChangedEnsureHostedLayoutElapsedTicks;
    private long _runtimeContentHostViewportChangedNotifyViewportElapsedTicks;
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
    private int _runtimeNotifyViewportChangedCallCount;
    private int _runtimeNotifyViewportChangedRaisedCount;
    private int _runtimeNotifyViewportChangedSkippedNoChangeCount;
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
        QueueViewportChangedNotification();
        var notifyViewportElapsed = Stopwatch.GetTimestamp() - notifyViewportStart;
        _diagContentHostViewportChangedNotifyViewportElapsedTicks += notifyViewportElapsed;
        _runtimeContentHostViewportChangedNotifyViewportElapsedTicks += notifyViewportElapsed;

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagContentHostViewportChangedElapsedTicks += elapsedTicks;
        _runtimeContentHostViewportChangedElapsedTicks += elapsedTicks;
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
        var layoutWidth = ResolveHostedContentLayoutWidth(availableSize.X);
        var layout = BuildOrGetLayout(layoutWidth);
        _lastMeasuredLayout = layout;
        return new Vector2(Math.Max(0f, layout.ContentWidth), Math.Max(0f, layout.ContentHeight));
    }

    private bool CanReuseHostedContentMeasure(float previousFallbackWidth, float nextFallbackWidth)
    {
        var previousLayoutWidth = ResolveHostedContentLayoutWidth(previousFallbackWidth);
        var nextLayoutWidth = ResolveHostedContentLayoutWidth(nextFallbackWidth);
        if (AreEquivalentDocumentLayoutWidths(previousLayoutWidth, nextLayoutWidth))
        {
            return true;
        }

        return CanReuseDocumentLayoutForWidthChange(_lastMeasuredLayout ?? _lastRenderedLayout, previousLayoutWidth, nextLayoutWidth);
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

    private void RenderHostedScrollContent(SpriteBatch spriteBatch, LayoutRect slot)
    {
        var layout = BuildOrGetLayout(ResolveHostedContentLayoutWidth(slot.Width));
        RenderDocumentSurface(spriteBatch, slot, layout, 0f, 0f, includeHostedChildren: false);
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
            NotifyViewportChangedCallCount: _runtimeNotifyViewportChangedCallCount,
            NotifyViewportChangedRaisedCount: _runtimeNotifyViewportChangedRaisedCount,
            NotifyViewportChangedSkippedNoChangeCount: _runtimeNotifyViewportChangedSkippedNoChangeCount,
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
            NotifyViewportChangedCallCount: _diagNotifyViewportChangedCallCount,
            NotifyViewportChangedRaisedCount: _diagNotifyViewportChangedRaisedCount,
            NotifyViewportChangedSkippedNoChangeCount: _diagNotifyViewportChangedSkippedNoChangeCount,
            NotifyViewportChangedMilliseconds: TicksToMilliseconds(_diagNotifyViewportChangedElapsedTicks),
            NotifyViewportChangedSubscriberMilliseconds: TicksToMilliseconds(_diagNotifyViewportChangedSubscriberElapsedTicks));

        if (reset)
        {
            _diagContentHostViewportChangedCallCount = 0;
            _diagContentHostViewportChangedElapsedTicks = 0;
            _diagContentHostViewportChangedApplyPendingElapsedTicks = 0;
            _diagContentHostViewportChangedEnsureHostedLayoutElapsedTicks = 0;
            _diagContentHostViewportChangedNotifyViewportElapsedTicks = 0;
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
            _diagNotifyViewportChangedCallCount = 0;
            _diagNotifyViewportChangedRaisedCount = 0;
            _diagNotifyViewportChangedSkippedNoChangeCount = 0;
            _diagNotifyViewportChangedElapsedTicks = 0;
            _diagNotifyViewportChangedSubscriberElapsedTicks = 0;
        }

        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return (double)ticks * 1000d / Stopwatch.Frequency;
    }

    private sealed class RichTextBoxScrollContentPresenter : FrameworkElement, IHyperlinkHoverHost
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

        protected override bool CanReuseMeasureForAvailableSizeChange(Vector2 previousAvailableSize, Vector2 nextAvailableSize)
        {
            return _owner.CanReuseHostedContentMeasure(previousAvailableSize.X, nextAvailableSize.X);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
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