using System;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public sealed class Label : ContentControl
{
    private static readonly Lazy<Style> DefaultLabelStyle = new(BuildDefaultLabelStyle);
    private static long _diagConstructorCallCount;
    private static long _diagResolveAccessKeyTargetCallCount;
    private static long _diagResolveAccessKeyTargetElapsedTicks;
    private static long _diagResolveAccessKeyTargetReturnedTargetCount;
    private static long _diagResolveAccessKeyTargetReturnedSelfCount;
    private static long _diagGetAutomationContentTextCallCount;
    private static long _diagGetAutomationContentTextElapsedTicks;
    private static long _diagGetFallbackStyleCallCount;
    private static long _diagGetFallbackStyleElapsedTicks;
    private static long _diagGetFallbackStyleCacheHitCount;
    private static long _diagGetFallbackStyleCacheMissCount;
    private static long _diagOnTargetChangedCallCount;
    private static long _diagOnTargetChangedElapsedTicks;
    private static long _diagOnTargetChangedClearedOldTargetCount;
    private static long _diagOnTargetChangedSkippedClearOldTargetCount;
    private static long _diagOnTargetChangedAttachedNewTargetCount;
    private static long _diagOnTargetChangedNoNewTargetCount;
    private static long _diagBuildDefaultLabelStyleCallCount;
    private static long _diagBuildDefaultLabelStyleElapsedTicks;
    private static long _diagBuildDefaultLabelTemplateCallCount;
    private static long _diagBuildDefaultLabelTemplateElapsedTicks;
    private static long _diagBuildDefaultLabelTemplateBindCount;
    private static long _diagExtractAutomationTextCallCount;
    private static long _diagExtractAutomationTextElapsedTicks;
    private static long _diagExtractAutomationTextNullPathCount;
    private static long _diagExtractAutomationTextStringPathCount;
    private static long _diagExtractAutomationTextAccessTextPathCount;
    private static long _diagExtractAutomationTextLabelPathCount;
    private static long _diagExtractAutomationTextTextBlockPathCount;
    private static long _diagExtractAutomationTextContentControlPathCount;
    private static long _diagExtractAutomationTextFallbackPathCount;

    private long _runtimeResolveAccessKeyTargetCallCount;
    private long _runtimeResolveAccessKeyTargetElapsedTicks;
    private long _runtimeResolveAccessKeyTargetReturnedTargetCount;
    private long _runtimeResolveAccessKeyTargetReturnedSelfCount;
    private long _runtimeGetAutomationContentTextCallCount;
    private long _runtimeGetAutomationContentTextElapsedTicks;
    private long _runtimeGetFallbackStyleCallCount;
    private long _runtimeGetFallbackStyleElapsedTicks;
    private long _runtimeGetFallbackStyleCacheHitCount;
    private long _runtimeGetFallbackStyleCacheMissCount;
    private long _runtimeOnTargetChangedCallCount;
    private long _runtimeOnTargetChangedElapsedTicks;
    private long _runtimeOnTargetChangedClearedOldTargetCount;
    private long _runtimeOnTargetChangedSkippedClearOldTargetCount;
    private long _runtimeOnTargetChangedAttachedNewTargetCount;
    private long _runtimeOnTargetChangedNoNewTargetCount;

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(
            nameof(Target),
            typeof(UIElement),
            typeof(Label),
            new FrameworkPropertyMetadata(
                null,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Label label)
                    {
                        label.OnTargetChanged(args.OldValue as UIElement, args.NewValue as UIElement);
                    }
                }));

    public Label()
    {
        IncrementAggregate(ref _diagConstructorCallCount);
        Focusable = false;
    }

    public UIElement? Target
    {
        get => GetValue<UIElement>(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    internal UIElement? ResolveAccessKeyTarget()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeResolveAccessKeyTargetCallCount++;
        IncrementAggregate(ref _diagResolveAccessKeyTargetCallCount);
        try
        {
            if (Target is UIElement target)
            {
                _runtimeResolveAccessKeyTargetReturnedTargetCount++;
                IncrementAggregate(ref _diagResolveAccessKeyTargetReturnedTargetCount);
                return target;
            }

            _runtimeResolveAccessKeyTargetReturnedSelfCount++;
            IncrementAggregate(ref _diagResolveAccessKeyTargetReturnedSelfCount);
            return this;
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            _runtimeResolveAccessKeyTargetElapsedTicks += elapsed;
            AddAggregate(ref _diagResolveAccessKeyTargetElapsedTicks, elapsed);
        }
    }

    internal string GetAutomationContentText()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeGetAutomationContentTextCallCount++;
        IncrementAggregate(ref _diagGetAutomationContentTextCallCount);
        try
        {
            return ExtractAutomationText(Content);
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            _runtimeGetAutomationContentTextElapsedTicks += elapsed;
            AddAggregate(ref _diagGetAutomationContentTextElapsedTicks, elapsed);
        }
    }

    protected override Style? GetFallbackStyle()
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeGetFallbackStyleCallCount++;
        IncrementAggregate(ref _diagGetFallbackStyleCallCount);
        try
        {
            if (DefaultLabelStyle.IsValueCreated)
            {
                _runtimeGetFallbackStyleCacheHitCount++;
                IncrementAggregate(ref _diagGetFallbackStyleCacheHitCount);
            }
            else
            {
                _runtimeGetFallbackStyleCacheMissCount++;
                IncrementAggregate(ref _diagGetFallbackStyleCacheMissCount);
            }

            return DefaultLabelStyle.Value;
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            _runtimeGetFallbackStyleElapsedTicks += elapsed;
            AddAggregate(ref _diagGetFallbackStyleElapsedTicks, elapsed);
        }
    }

    private void OnTargetChanged(UIElement? oldTarget, UIElement? newTarget)
    {
        var start = Stopwatch.GetTimestamp();
        _runtimeOnTargetChangedCallCount++;
        IncrementAggregate(ref _diagOnTargetChangedCallCount);
        try
        {
            if (oldTarget != null)
            {
                if (ReferenceEquals(AutomationProperties.GetLabeledBy(oldTarget), this))
                {
                    _runtimeOnTargetChangedClearedOldTargetCount++;
                    IncrementAggregate(ref _diagOnTargetChangedClearedOldTargetCount);
                    AutomationProperties.SetLabeledBy(oldTarget, null);
                }
                else
                {
                    _runtimeOnTargetChangedSkippedClearOldTargetCount++;
                    IncrementAggregate(ref _diagOnTargetChangedSkippedClearOldTargetCount);
                }
            }

            if (newTarget != null)
            {
                _runtimeOnTargetChangedAttachedNewTargetCount++;
                IncrementAggregate(ref _diagOnTargetChangedAttachedNewTargetCount);
                AutomationProperties.SetLabeledBy(newTarget, this);
            }
            else
            {
                _runtimeOnTargetChangedNoNewTargetCount++;
                IncrementAggregate(ref _diagOnTargetChangedNoNewTargetCount);
            }
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            _runtimeOnTargetChangedElapsedTicks += elapsed;
            AddAggregate(ref _diagOnTargetChangedElapsedTicks, elapsed);
        }
    }

    private static Style BuildDefaultLabelStyle()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagBuildDefaultLabelStyleCallCount);
        try
        {
            var style = new Style(typeof(Label));
            style.Setters.Add(new Setter(TemplateProperty, BuildDefaultLabelTemplate()));
            return style;
        }
        finally
        {
            AddAggregate(ref _diagBuildDefaultLabelStyleElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private static ControlTemplate BuildDefaultLabelTemplate()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagBuildDefaultLabelTemplateCallCount);
        try
        {
            var template = new ControlTemplate(static _ =>
            {
                var border = new Border
                {
                    Name = "PART_Border"
                };

                border.Child = new ContentPresenter
                {
                    Name = "PART_ContentPresenter",
                    ContentSource = nameof(Content),
                    RecognizesAccessKey = true
                };

                return border;
            })
            {
                TargetType = typeof(Label)
            };

            template.BindTemplate("PART_Border", Border.BackgroundProperty, BackgroundProperty);
            template.BindTemplate("PART_Border", Border.BorderBrushProperty, BorderBrushProperty);
            template.BindTemplate("PART_Border", Border.BorderThicknessProperty, BorderThicknessProperty);
            template.BindTemplate("PART_Border", Border.PaddingProperty, PaddingProperty);
            template.BindTemplate("PART_ContentPresenter", ContentPresenter.HorizontalContentAlignmentProperty, HorizontalContentAlignmentProperty);
            template.BindTemplate("PART_ContentPresenter", ContentPresenter.VerticalContentAlignmentProperty, VerticalContentAlignmentProperty);
            AddAggregate(ref _diagBuildDefaultLabelTemplateBindCount, 6);

            return template;
        }
        finally
        {
            AddAggregate(ref _diagBuildDefaultLabelTemplateElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    internal static string ExtractAutomationText(object? value)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagExtractAutomationTextCallCount);
        try
        {
            return ExtractAutomationTextCore(value);
        }
        finally
        {
            AddAggregate(ref _diagExtractAutomationTextElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    internal LabelRuntimeDiagnosticsSnapshot GetLabelSnapshotForDiagnostics()
    {
        return new LabelRuntimeDiagnosticsSnapshot(
            Target is not null,
            Target?.GetType().Name ?? string.Empty,
            Content is not null,
            Content?.GetType().Name ?? string.Empty,
            Focusable,
            ExtractAutomationTextCore(Content),
            _runtimeResolveAccessKeyTargetCallCount,
            TicksToMilliseconds(_runtimeResolveAccessKeyTargetElapsedTicks),
            _runtimeResolveAccessKeyTargetReturnedTargetCount,
            _runtimeResolveAccessKeyTargetReturnedSelfCount,
            _runtimeGetAutomationContentTextCallCount,
            TicksToMilliseconds(_runtimeGetAutomationContentTextElapsedTicks),
            _runtimeGetFallbackStyleCallCount,
            TicksToMilliseconds(_runtimeGetFallbackStyleElapsedTicks),
            _runtimeGetFallbackStyleCacheHitCount,
            _runtimeGetFallbackStyleCacheMissCount,
            _runtimeOnTargetChangedCallCount,
            TicksToMilliseconds(_runtimeOnTargetChangedElapsedTicks),
            _runtimeOnTargetChangedClearedOldTargetCount,
            _runtimeOnTargetChangedSkippedClearOldTargetCount,
            _runtimeOnTargetChangedAttachedNewTargetCount,
            _runtimeOnTargetChangedNoNewTargetCount);
    }

    internal new static LabelTelemetrySnapshot GetTelemetryAndReset()
    {
        return new LabelTelemetrySnapshot(
            ResetAggregate(ref _diagConstructorCallCount),
            ResetAggregate(ref _diagResolveAccessKeyTargetCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagResolveAccessKeyTargetElapsedTicks)),
            ResetAggregate(ref _diagResolveAccessKeyTargetReturnedTargetCount),
            ResetAggregate(ref _diagResolveAccessKeyTargetReturnedSelfCount),
            ResetAggregate(ref _diagGetAutomationContentTextCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagGetAutomationContentTextElapsedTicks)),
            ResetAggregate(ref _diagGetFallbackStyleCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagGetFallbackStyleElapsedTicks)),
            ResetAggregate(ref _diagGetFallbackStyleCacheHitCount),
            ResetAggregate(ref _diagGetFallbackStyleCacheMissCount),
            ResetAggregate(ref _diagOnTargetChangedCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagOnTargetChangedElapsedTicks)),
            ResetAggregate(ref _diagOnTargetChangedClearedOldTargetCount),
            ResetAggregate(ref _diagOnTargetChangedSkippedClearOldTargetCount),
            ResetAggregate(ref _diagOnTargetChangedAttachedNewTargetCount),
            ResetAggregate(ref _diagOnTargetChangedNoNewTargetCount),
            ResetAggregate(ref _diagBuildDefaultLabelStyleCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagBuildDefaultLabelStyleElapsedTicks)),
            ResetAggregate(ref _diagBuildDefaultLabelTemplateCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagBuildDefaultLabelTemplateElapsedTicks)),
            ResetAggregate(ref _diagBuildDefaultLabelTemplateBindCount),
            ResetAggregate(ref _diagExtractAutomationTextCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagExtractAutomationTextElapsedTicks)),
            ResetAggregate(ref _diagExtractAutomationTextNullPathCount),
            ResetAggregate(ref _diagExtractAutomationTextStringPathCount),
            ResetAggregate(ref _diagExtractAutomationTextAccessTextPathCount),
            ResetAggregate(ref _diagExtractAutomationTextLabelPathCount),
            ResetAggregate(ref _diagExtractAutomationTextTextBlockPathCount),
            ResetAggregate(ref _diagExtractAutomationTextContentControlPathCount),
            ResetAggregate(ref _diagExtractAutomationTextFallbackPathCount));
    }

    internal new static LabelTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateAggregateTelemetrySnapshot();
    }

    private static string ExtractAutomationTextCore(object? value)
    {
        return value switch
        {
            null => RecordExtractAutomationTextNullPath(),
            string text => RecordExtractAutomationTextStringPath(text),
            AccessText accessText => RecordExtractAutomationTextAccessTextPath(accessText),
            Label label => RecordExtractAutomationTextLabelPath(label),
            TextBlock textBlock => RecordExtractAutomationTextTextBlockPath(textBlock),
            ContentControl contentControl => RecordExtractAutomationTextContentControlPath(contentControl),
            _ => RecordExtractAutomationTextFallbackPath(value)
        };
    }

    private static LabelTelemetrySnapshot CreateAggregateTelemetrySnapshot()
    {
        return new LabelTelemetrySnapshot(
            ReadAggregate(ref _diagConstructorCallCount),
            ReadAggregate(ref _diagResolveAccessKeyTargetCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagResolveAccessKeyTargetElapsedTicks)),
            ReadAggregate(ref _diagResolveAccessKeyTargetReturnedTargetCount),
            ReadAggregate(ref _diagResolveAccessKeyTargetReturnedSelfCount),
            ReadAggregate(ref _diagGetAutomationContentTextCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetAutomationContentTextElapsedTicks)),
            ReadAggregate(ref _diagGetFallbackStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetFallbackStyleElapsedTicks)),
            ReadAggregate(ref _diagGetFallbackStyleCacheHitCount),
            ReadAggregate(ref _diagGetFallbackStyleCacheMissCount),
            ReadAggregate(ref _diagOnTargetChangedCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagOnTargetChangedElapsedTicks)),
            ReadAggregate(ref _diagOnTargetChangedClearedOldTargetCount),
            ReadAggregate(ref _diagOnTargetChangedSkippedClearOldTargetCount),
            ReadAggregate(ref _diagOnTargetChangedAttachedNewTargetCount),
            ReadAggregate(ref _diagOnTargetChangedNoNewTargetCount),
            ReadAggregate(ref _diagBuildDefaultLabelStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagBuildDefaultLabelStyleElapsedTicks)),
            ReadAggregate(ref _diagBuildDefaultLabelTemplateCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagBuildDefaultLabelTemplateElapsedTicks)),
            ReadAggregate(ref _diagBuildDefaultLabelTemplateBindCount),
            ReadAggregate(ref _diagExtractAutomationTextCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagExtractAutomationTextElapsedTicks)),
            ReadAggregate(ref _diagExtractAutomationTextNullPathCount),
            ReadAggregate(ref _diagExtractAutomationTextStringPathCount),
            ReadAggregate(ref _diagExtractAutomationTextAccessTextPathCount),
            ReadAggregate(ref _diagExtractAutomationTextLabelPathCount),
            ReadAggregate(ref _diagExtractAutomationTextTextBlockPathCount),
            ReadAggregate(ref _diagExtractAutomationTextContentControlPathCount),
            ReadAggregate(ref _diagExtractAutomationTextFallbackPathCount));
    }

    private static string RecordExtractAutomationTextNullPath()
    {
        IncrementAggregate(ref _diagExtractAutomationTextNullPathCount);
        return string.Empty;
    }

    private static string RecordExtractAutomationTextStringPath(string text)
    {
        IncrementAggregate(ref _diagExtractAutomationTextStringPathCount);
        return MenuAccessText.StripAccessMarkers(text);
    }

    private static string RecordExtractAutomationTextAccessTextPath(AccessText accessText)
    {
        IncrementAggregate(ref _diagExtractAutomationTextAccessTextPathCount);
        return accessText.DisplayText;
    }

    private static string RecordExtractAutomationTextLabelPath(Label label)
    {
        IncrementAggregate(ref _diagExtractAutomationTextLabelPathCount);
        return label.GetAutomationContentText();
    }

    private static string RecordExtractAutomationTextTextBlockPath(TextBlock textBlock)
    {
        IncrementAggregate(ref _diagExtractAutomationTextTextBlockPathCount);
        return textBlock.Text;
    }

    private static string RecordExtractAutomationTextContentControlPath(ContentControl contentControl)
    {
        IncrementAggregate(ref _diagExtractAutomationTextContentControlPathCount);
        return ExtractAutomationText(contentControl.Content);
    }

    private static string RecordExtractAutomationTextFallbackPath(object value)
    {
        IncrementAggregate(ref _diagExtractAutomationTextFallbackPathCount);
        return value.ToString() ?? string.Empty;
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

    private static double TicksToMilliseconds(long elapsedTicks)
    {
        return (double)elapsedTicks * 1000d / Stopwatch.Frequency;
    }
}
