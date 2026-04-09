using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public sealed class CalendarDayButton : Button
{
    private readonly struct DeferredInvalidationScope : IDisposable
    {
        private readonly CalendarDayButton _owner;

        public DeferredInvalidationScope(CalendarDayButton owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner.EndDeferredInvalidation();
        }
    }

    private static readonly Lazy<Style> DefaultCalendarDayButtonStyle = new(BuildDefaultCalendarDayButtonStyle);
    private static long _renderElapsedTicks;
    private static int _renderCallCount;
    private static int _nonEmptyRenderCallCount;
    private static int _diagDependencyPropertyChangedCallCount;
    private static int _diagDayTextPropertyChangedCount;
    private static int _diagContentPropertyChangedCount;
    private static int _diagOnApplyTemplateCallCount;
    private static int _diagOnApplyTemplateHasPresenterCount;
    private static int _diagOnApplyTemplateFallbackContentSyncCount;
    private static int _diagOnDayTextChangedCallCount;
    private static long _diagOnDayTextChangedElapsedTicks;
    private static int _diagUpdateTemplateDayTextCallCount;
    private static long _diagUpdateTemplateDayTextElapsedTicks;
    private static int _diagUpdateTemplateDayTextPresenterAttachedCount;
    private static int _diagUpdateTemplateDayTextPresenterMissingCount;
    private static int _diagSyncContentFromDayTextCallCount;
    private static long _diagSyncContentFromDayTextElapsedTicks;
    private static int _diagSyncContentFromDayTextNoOpCount;
    private static int _diagSyncContentFromDayTextEmptyNoOpCount;
    private static int _diagSyncContentFromDayTextWriteCount;
    private static int _diagSyncDayTextFromContentCallCount;
    private static long _diagSyncDayTextFromContentElapsedTicks;
    private static int _diagSyncDayTextFromContentIgnoredNonStringCount;
    private static int _diagSyncDayTextFromContentNoOpCount;
    private static int _diagSyncDayTextFromContentWriteCount;
    private int _runtimeDependencyPropertyChangedCallCount;
    private int _runtimeDayTextPropertyChangedCount;
    private int _runtimeContentPropertyChangedCount;
    private int _runtimeOnApplyTemplateCallCount;
    private int _runtimeOnApplyTemplateHasPresenterCount;
    private int _runtimeOnApplyTemplateFallbackContentSyncCount;
    private int _runtimeOnDayTextChangedCallCount;
    private long _runtimeOnDayTextChangedElapsedTicks;
    private int _runtimeUpdateTemplateDayTextCallCount;
    private long _runtimeUpdateTemplateDayTextElapsedTicks;
    private int _runtimeUpdateTemplateDayTextPresenterAttachedCount;
    private int _runtimeUpdateTemplateDayTextPresenterMissingCount;
    private int _runtimeSyncContentFromDayTextCallCount;
    private long _runtimeSyncContentFromDayTextElapsedTicks;
    private int _runtimeSyncContentFromDayTextNoOpCount;
    private int _runtimeSyncContentFromDayTextEmptyNoOpCount;
    private int _runtimeSyncContentFromDayTextWriteCount;
    private int _runtimeSyncDayTextFromContentCallCount;
    private long _runtimeSyncDayTextFromContentElapsedTicks;
    private int _runtimeSyncDayTextFromContentIgnoredNonStringCount;
    private int _runtimeSyncDayTextFromContentNoOpCount;
    private int _runtimeSyncDayTextFromContentWriteCount;
    private long _runtimeRenderElapsedTicks;
    private int _runtimeRenderCallCount;
    private int _runtimeNonEmptyRenderCallCount;
    private CalendarDayTextPresenter? _dayTextPresenter;
    private int _deferredInvalidationDepth;
    private bool _hasDeferredMeasureInvalidation;
    private bool _hasDeferredArrangeInvalidation;
    private bool _hasDeferredVisualInvalidation;

    public static readonly DependencyProperty DayTextProperty =
        DependencyProperty.Register(
            nameof(DayText),
            typeof(string),
            typeof(CalendarDayButton),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (d, args) => ((CalendarDayButton)d).OnDayTextChanged(args)));

    public string DayText
    {
        get => GetValue<string>(DayTextProperty) ?? string.Empty;
        set => SetValue(DayTextProperty, value);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        _runtimeDependencyPropertyChangedCallCount++;
        Interlocked.Increment(ref _diagDependencyPropertyChangedCallCount);
        if (args.Property == DayTextProperty)
        {
            _runtimeDayTextPropertyChangedCount++;
            Interlocked.Increment(ref _diagDayTextPropertyChangedCount);
        }

        if (args.Property == ContentProperty)
        {
            _runtimeContentPropertyChangedCount++;
            Interlocked.Increment(ref _diagContentPropertyChangedCount);
        }

        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty && !_isSynchronizingDayTextToContent)
        {
            SyncDayTextFromContent(args.NewValue);
        }
    }

    protected override bool ShouldInvalidateMeasureForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (args.Property == ContentProperty && _isSynchronizingDayTextToContent)
        {
            return false;
        }

        return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
    }

    protected override bool ShouldInvalidateArrangeForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (args.Property == ContentProperty && _isSynchronizingDayTextToContent)
        {
            return false;
        }

        return base.ShouldInvalidateArrangeForPropertyChange(args, metadata);
    }

    public override void InvalidateMeasure()
    {
        if (_deferredInvalidationDepth > 0)
        {
            _hasDeferredMeasureInvalidation = true;
            return;
        }

        base.InvalidateMeasure();
    }

    public override void InvalidateArrange()
    {
        if (_deferredInvalidationDepth > 0)
        {
            _hasDeferredArrangeInvalidation = true;
            return;
        }

        base.InvalidateArrange();
    }

    public override void InvalidateVisual()
    {
        if (_deferredInvalidationDepth > 0)
        {
            _hasDeferredVisualInvalidation = true;
            return;
        }

        base.InvalidateVisual();
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        _renderCallCount++;
        _runtimeRenderCallCount++;
        try
        {
            base.OnRender(spriteBatch);
            if (!string.IsNullOrEmpty(DayText))
            {
                _nonEmptyRenderCallCount++;
                _runtimeNonEmptyRenderCallCount++;
            }
        }
        finally
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            _renderElapsedTicks += elapsedTicks;
            _runtimeRenderElapsedTicks += elapsedTicks;
        }
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultCalendarDayButtonStyle.Value;
    }

    internal new static CalendarDayButtonTimingSnapshot GetTimingSnapshotForTests()
    {
        return new CalendarDayButtonTimingSnapshot(
            _renderElapsedTicks,
            _renderCallCount,
            _nonEmptyRenderCallCount);
    }

    internal new static void ResetTimingForTests()
    {
        _renderElapsedTicks = 0;
        _renderCallCount = 0;
        _nonEmptyRenderCallCount = 0;
    }

    internal CalendarDayButtonRuntimeDiagnosticsSnapshot GetCalendarDayButtonSnapshotForDiagnostics()
    {
        return new CalendarDayButtonRuntimeDiagnosticsSnapshot(
            DayText,
            Content?.GetType().Name ?? string.Empty,
            _dayTextPresenter != null,
            _runtimeDependencyPropertyChangedCallCount,
            _runtimeDayTextPropertyChangedCount,
            _runtimeContentPropertyChangedCount,
            _runtimeOnApplyTemplateCallCount,
            _runtimeOnApplyTemplateHasPresenterCount,
            _runtimeOnApplyTemplateFallbackContentSyncCount,
            _runtimeOnDayTextChangedCallCount,
            TicksToMilliseconds(_runtimeOnDayTextChangedElapsedTicks),
            _runtimeUpdateTemplateDayTextCallCount,
            TicksToMilliseconds(_runtimeUpdateTemplateDayTextElapsedTicks),
            _runtimeUpdateTemplateDayTextPresenterAttachedCount,
            _runtimeUpdateTemplateDayTextPresenterMissingCount,
            _runtimeSyncContentFromDayTextCallCount,
            TicksToMilliseconds(_runtimeSyncContentFromDayTextElapsedTicks),
            _runtimeSyncContentFromDayTextNoOpCount,
            _runtimeSyncContentFromDayTextEmptyNoOpCount,
            _runtimeSyncContentFromDayTextWriteCount,
            _runtimeSyncDayTextFromContentCallCount,
            TicksToMilliseconds(_runtimeSyncDayTextFromContentElapsedTicks),
            _runtimeSyncDayTextFromContentIgnoredNonStringCount,
            _runtimeSyncDayTextFromContentNoOpCount,
            _runtimeSyncDayTextFromContentWriteCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeNonEmptyRenderCallCount);
    }

    internal new static CalendarDayButtonTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    internal new static CalendarDayButtonTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    public override void OnApplyTemplate()
    {
        _runtimeOnApplyTemplateCallCount++;
        Interlocked.Increment(ref _diagOnApplyTemplateCallCount);
        base.OnApplyTemplate();
        _dayTextPresenter = GetTemplateChild("PART_DayText") as CalendarDayTextPresenter;
        if (_dayTextPresenter == null && HasTemplateRoot)
        {
            _dayTextPresenter = EnsureTemplatedContentDayTextPresenter();
        }

        if (_dayTextPresenter != null)
        {
            _runtimeOnApplyTemplateHasPresenterCount++;
            Interlocked.Increment(ref _diagOnApplyTemplateHasPresenterCount);
        }

        UpdateTemplateDayText();
        if (_dayTextPresenter == null && !HasTemplateRoot)
        {
            _runtimeOnApplyTemplateFallbackContentSyncCount++;
            Interlocked.Increment(ref _diagOnApplyTemplateFallbackContentSyncCount);
            SyncContentFromDayText(DayText);
        }
    }

    internal IDisposable DeferInvalidation()
    {
        _deferredInvalidationDepth++;
        return new DeferredInvalidationScope(this);
    }

    private bool _isSynchronizingDayTextToContent;

    private void EndDeferredInvalidation()
    {
        if (_deferredInvalidationDepth <= 0)
        {
            return;
        }

        _deferredInvalidationDepth--;
        if (_deferredInvalidationDepth > 0)
        {
            return;
        }

        var invalidateMeasure = _hasDeferredMeasureInvalidation;
        var invalidateArrange = _hasDeferredArrangeInvalidation;
        var invalidateVisual = _hasDeferredVisualInvalidation;
        _hasDeferredMeasureInvalidation = false;
        _hasDeferredArrangeInvalidation = false;
        _hasDeferredVisualInvalidation = false;

        if (invalidateMeasure)
        {
            base.InvalidateMeasure();
            return;
        }

        if (invalidateArrange)
        {
            base.InvalidateArrange();
            return;
        }

        if (invalidateVisual)
        {
            base.InvalidateVisual();
        }
    }

    private void OnDayTextChanged(DependencyPropertyChangedEventArgs args)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeOnDayTextChangedCallCount++;
        Interlocked.Increment(ref _diagOnDayTextChangedCallCount);
        var dayText = args.NewValue as string ?? string.Empty;
        UpdateTemplateDayText();
        if (!_isSynchronizingDayTextToContent && !UsesDedicatedDayTextPresenter() && !HasTemplateRoot)
        {
            SyncContentFromDayText(dayText);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeOnDayTextChangedElapsedTicks += elapsedTicks;
        Interlocked.Add(ref _diagOnDayTextChangedElapsedTicks, elapsedTicks);
    }

    private bool UsesDedicatedDayTextPresenter()
    {
        return _dayTextPresenter != null;
    }

    private void UpdateTemplateDayText()
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeUpdateTemplateDayTextCallCount++;
        Interlocked.Increment(ref _diagUpdateTemplateDayTextCallCount);
        if (_dayTextPresenter != null)
        {
            _runtimeUpdateTemplateDayTextPresenterAttachedCount++;
            Interlocked.Increment(ref _diagUpdateTemplateDayTextPresenterAttachedCount);
            _dayTextPresenter.Text = DayText;
        }
        else
        {
            _runtimeUpdateTemplateDayTextPresenterMissingCount++;
            Interlocked.Increment(ref _diagUpdateTemplateDayTextPresenterMissingCount);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeUpdateTemplateDayTextElapsedTicks += elapsedTicks;
        Interlocked.Add(ref _diagUpdateTemplateDayTextElapsedTicks, elapsedTicks);
    }

    private void SyncContentFromDayText(string dayText)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeSyncContentFromDayTextCallCount++;
        Interlocked.Increment(ref _diagSyncContentFromDayTextCallCount);
        if (Content is string contentText && string.Equals(contentText, dayText, StringComparison.Ordinal))
        {
            _runtimeSyncContentFromDayTextNoOpCount++;
            Interlocked.Increment(ref _diagSyncContentFromDayTextNoOpCount);
            RecordSyncContentElapsed(startTicks);
            return;
        }

        if (Content == null && dayText.Length == 0)
        {
            _runtimeSyncContentFromDayTextEmptyNoOpCount++;
            Interlocked.Increment(ref _diagSyncContentFromDayTextEmptyNoOpCount);
            RecordSyncContentElapsed(startTicks);
            return;
        }

        _isSynchronizingDayTextToContent = true;
        try
        {
            Content = dayText;
            _runtimeSyncContentFromDayTextWriteCount++;
            Interlocked.Increment(ref _diagSyncContentFromDayTextWriteCount);
        }
        finally
        {
            _isSynchronizingDayTextToContent = false;
            RecordSyncContentElapsed(startTicks);
        }
    }

    private void SyncDayTextFromContent(object? content)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _runtimeSyncDayTextFromContentCallCount++;
        Interlocked.Increment(ref _diagSyncDayTextFromContentCallCount);
        if (content is not string contentText)
        {
            if (content != null)
            {
                _runtimeSyncDayTextFromContentIgnoredNonStringCount++;
                Interlocked.Increment(ref _diagSyncDayTextFromContentIgnoredNonStringCount);
                RecordSyncDayTextElapsed(startTicks);
                return;
            }

            contentText = string.Empty;
        }

        if (string.Equals(DayText, contentText, StringComparison.Ordinal))
        {
            _runtimeSyncDayTextFromContentNoOpCount++;
            Interlocked.Increment(ref _diagSyncDayTextFromContentNoOpCount);
            RecordSyncDayTextElapsed(startTicks);
            return;
        }

        _isSynchronizingDayTextToContent = true;
        try
        {
            SetValue(DayTextProperty, contentText);
            _runtimeSyncDayTextFromContentWriteCount++;
            Interlocked.Increment(ref _diagSyncDayTextFromContentWriteCount);
        }
        finally
        {
            _isSynchronizingDayTextToContent = false;
            RecordSyncDayTextElapsed(startTicks);
        }
    }

    private void RecordSyncContentElapsed(long startTicks)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeSyncContentFromDayTextElapsedTicks += elapsedTicks;
        Interlocked.Add(ref _diagSyncContentFromDayTextElapsedTicks, elapsedTicks);
    }

    private void RecordSyncDayTextElapsed(long startTicks)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _runtimeSyncDayTextFromContentElapsedTicks += elapsedTicks;
        Interlocked.Add(ref _diagSyncDayTextFromContentElapsedTicks, elapsedTicks);
    }

    private static CalendarDayButtonTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        return new CalendarDayButtonTelemetrySnapshot(
            ReadOrReset(ref _diagDependencyPropertyChangedCallCount, reset),
            ReadOrReset(ref _diagDayTextPropertyChangedCount, reset),
            ReadOrReset(ref _diagContentPropertyChangedCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateCallCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateHasPresenterCount, reset),
            ReadOrReset(ref _diagOnApplyTemplateFallbackContentSyncCount, reset),
            ReadOrReset(ref _diagOnDayTextChangedCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagOnDayTextChangedElapsedTicks, reset)),
            ReadOrReset(ref _diagUpdateTemplateDayTextCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagUpdateTemplateDayTextElapsedTicks, reset)),
            ReadOrReset(ref _diagUpdateTemplateDayTextPresenterAttachedCount, reset),
            ReadOrReset(ref _diagUpdateTemplateDayTextPresenterMissingCount, reset),
            ReadOrReset(ref _diagSyncContentFromDayTextCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagSyncContentFromDayTextElapsedTicks, reset)),
            ReadOrReset(ref _diagSyncContentFromDayTextNoOpCount, reset),
            ReadOrReset(ref _diagSyncContentFromDayTextEmptyNoOpCount, reset),
            ReadOrReset(ref _diagSyncContentFromDayTextWriteCount, reset),
            ReadOrReset(ref _diagSyncDayTextFromContentCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _diagSyncDayTextFromContentElapsedTicks, reset)),
            ReadOrReset(ref _diagSyncDayTextFromContentIgnoredNonStringCount, reset),
            ReadOrReset(ref _diagSyncDayTextFromContentNoOpCount, reset),
            ReadOrReset(ref _diagSyncDayTextFromContentWriteCount, reset),
            ReadOrReset(ref _renderCallCount, reset),
            TicksToMilliseconds(ReadOrReset(ref _renderElapsedTicks, reset)),
            ReadOrReset(ref _nonEmptyRenderCallCount, reset));
    }

    private static int ReadOrReset(ref int field, bool reset)
    {
        return reset
            ? Interlocked.Exchange(ref field, 0)
            : Volatile.Read(ref field);
    }

    private static long ReadOrReset(ref long field, bool reset)
    {
        return reset
            ? Interlocked.Exchange(ref field, 0L)
            : Interlocked.Read(ref field);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private CalendarDayTextPresenter? EnsureTemplatedContentDayTextPresenter()
    {
        var contentPresenter = FindTemplateContentPresenter(this);
        if (contentPresenter == null)
        {
            return null;
        }

        if (contentPresenter.Content is CalendarDayTextPresenter existingPresenter)
        {
            return existingPresenter;
        }

        var presenter = FindExistingDayTextPresenter(contentPresenter) ?? new CalendarDayTextPresenter();
        if (!ReferenceEquals(contentPresenter.Content, presenter))
        {
            contentPresenter.Content = presenter;
        }

        return presenter;
    }

    private static ContentPresenter? FindTemplateContentPresenter(UIElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is ContentPresenter contentPresenter)
            {
                return contentPresenter;
            }

            var nested = FindTemplateContentPresenter(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static CalendarDayTextPresenter? FindExistingDayTextPresenter(UIElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is CalendarDayTextPresenter dayTextPresenter)
            {
                return dayTextPresenter;
            }

            var nested = FindExistingDayTextPresenter(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Style BuildDefaultCalendarDayButtonStyle()
    {
        var style = new Style(typeof(CalendarDayButton));
        style.Setters.Add(new Setter(TemplateProperty, BuildDefaultCalendarDayButtonTemplate()));

        var hoverTrigger = new Trigger(IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(65, 65, 65)));

        var pressedTrigger = new Trigger(IsPressedProperty, true);
        pressedTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(28, 28, 28)));

        var disabledTrigger = new Trigger(IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(BackgroundProperty, new Color(34, 34, 34)));
        disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(180, 180, 180)));

        style.Triggers.Add(hoverTrigger);
        style.Triggers.Add(pressedTrigger);
        style.Triggers.Add(disabledTrigger);
        return style;
    }

    private static ControlTemplate BuildDefaultCalendarDayButtonTemplate()
    {
        var template = new ControlTemplate(static _ =>
        {
            var border = new Border
            {
                Name = "PART_Border",
                CornerRadius = new CornerRadius(2f)
            };

            border.Child = new CalendarDayTextPresenter
            {
                Name = "PART_DayText",
            };

            return border;
        })
        {
            TargetType = typeof(CalendarDayButton)
        };

        template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
        template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
        template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
        template.BindTemplate("PART_Border", Border.PaddingProperty, PaddingProperty);
        template.BindTemplate("PART_DayText", CalendarDayTextPresenter.ForegroundProperty, ForegroundProperty);
        return template;
    }
}

