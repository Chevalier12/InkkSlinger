using System;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class CalendarDayButton : Button
{
    private static long _renderElapsedTicks;
    private static int _renderCallCount;
    private static int _nonEmptyRenderCallCount;

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

    private bool _isSynchronizingDayTextToContent;

    private void OnDayTextChanged(DependencyPropertyChangedEventArgs args)
    {
        var dayText = args.NewValue as string ?? string.Empty;
        if (!_isSynchronizingDayTextToContent)
        {
            SyncContentFromDayText(dayText);
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
}

internal readonly record struct CalendarDayButtonTimingSnapshot(
    long RenderElapsedTicks,
    int RenderCallCount,
    int NonEmptyRenderCallCount);
