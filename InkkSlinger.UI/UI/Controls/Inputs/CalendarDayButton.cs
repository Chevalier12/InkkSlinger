using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public sealed class CalendarDayButton : Button
{
    private static readonly Lazy<Style> DefaultCalendarDayButtonStyle = new(BuildDefaultCalendarDayButtonStyle);
    private static long _renderElapsedTicks;
    private static int _renderCallCount;
    private static int _nonEmptyRenderCallCount;
    private CalendarDayTextPresenter? _dayTextPresenter;

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
        base.OnDependencyPropertyChanged(args);

        if (args.Property == ContentProperty && !_isSynchronizingDayTextToContent)
        {
            SyncDayTextFromContent(args.NewValue);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        _renderCallCount++;
        try
        {
            base.OnRender(spriteBatch);
            if (!string.IsNullOrEmpty(DayText))
            {
                _nonEmptyRenderCallCount++;
            }
        }
        finally
        {
            _renderElapsedTicks += Stopwatch.GetTimestamp() - start;
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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _dayTextPresenter = GetTemplateChild("PART_DayText") as CalendarDayTextPresenter;
        UpdateTemplateDayText();
        if (_dayTextPresenter == null)
        {
            SyncContentFromDayText(DayText);
        }
    }

    private bool _isSynchronizingDayTextToContent;

    private void OnDayTextChanged(DependencyPropertyChangedEventArgs args)
    {
        var dayText = args.NewValue as string ?? string.Empty;
        UpdateTemplateDayText();
        if (!_isSynchronizingDayTextToContent && !UsesDedicatedDayTextPresenter())
        {
            SyncContentFromDayText(dayText);
        }
    }

    private bool UsesDedicatedDayTextPresenter()
    {
        return _dayTextPresenter != null;
    }

    private void UpdateTemplateDayText()
    {
        if (_dayTextPresenter != null)
        {
            _dayTextPresenter.Text = DayText;
        }
    }

    private void SyncContentFromDayText(string dayText)
    {
        if (Content is string contentText && string.Equals(contentText, dayText, StringComparison.Ordinal))
        {
            return;
        }

        if (Content == null && dayText.Length == 0)
        {
            return;
        }

        _isSynchronizingDayTextToContent = true;
        try
        {
            Content = dayText;
        }
        finally
        {
            _isSynchronizingDayTextToContent = false;
        }
    }

    private void SyncDayTextFromContent(object? content)
    {
        if (content is not string contentText)
        {
            if (content != null)
            {
                return;
            }

            contentText = string.Empty;
        }

        if (string.Equals(DayText, contentText, StringComparison.Ordinal))
        {
            return;
        }

        _isSynchronizingDayTextToContent = true;
        try
        {
            SetValue(DayTextProperty, contentText);
        }
        finally
        {
            _isSynchronizingDayTextToContent = false;
        }
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

