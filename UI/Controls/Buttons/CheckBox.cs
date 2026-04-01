using System;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class CheckBox : ToggleButton
{
    private static readonly Lazy<Style> DefaultCheckBoxStyle = new(BuildDefaultCheckBoxStyle);
    private static long _diagConstructorCallCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideTemplateRootPathCount;
    private static long _diagMeasureOverrideSelfLayoutPathCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderTemplateRootSkipCount;
    private static long _diagRenderEnabledStateCount;
    private static long _diagRenderDisabledStateCount;
    private static long _diagRenderCheckedStateCount;
    private static long _diagRenderUncheckedStateCount;
    private static long _diagRenderIndeterminateStateCount;
    private static long _diagGetFallbackStyleCallCount;
    private static long _diagGetFallbackStyleElapsedTicks;
    private static long _diagGetFallbackStyleCacheHitCount;
    private static long _diagGetFallbackStyleCacheMissCount;
    private static long _diagGetGlyphSizeCallCount;
    private static long _diagGetGlyphSpacingCallCount;
    private static long _diagMeasureTextCallCount;
    private static long _diagMeasureTextElapsedTicks;
    private static long _diagMeasureTextEmptyTextCount;
    private static long _diagMeasureTextLayoutCallCount;
    private static long _diagDrawTextCallCount;
    private static long _diagDrawTextElapsedTicks;
    private static long _diagDrawTextEmptyTextCount;
    private static long _diagDrawTextNoSpaceCount;
    private static long _diagDrawTextLayoutCallCount;
    private static long _diagDrawTextLineDrawCount;
    private static long _diagDrawTextSkippedEmptyLineCount;
    private static long _diagBuildDefaultCheckBoxStyleCallCount;
    private static long _diagBuildDefaultCheckBoxStyleElapsedTicks;

    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideTemplateRootPathCount;
    private long _runtimeMeasureOverrideSelfLayoutPathCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderTemplateRootSkipCount;
    private long _runtimeRenderEnabledStateCount;
    private long _runtimeRenderDisabledStateCount;
    private long _runtimeRenderCheckedStateCount;
    private long _runtimeRenderUncheckedStateCount;
    private long _runtimeRenderIndeterminateStateCount;
    private long _runtimeGetFallbackStyleCallCount;
    private long _runtimeGetFallbackStyleElapsedTicks;
    private long _runtimeGetFallbackStyleCacheHitCount;
    private long _runtimeGetFallbackStyleCacheMissCount;
    private long _runtimeGetGlyphSizeCallCount;
    private long _runtimeGetGlyphSpacingCallCount;
    private long _runtimeMeasureTextCallCount;
    private long _runtimeMeasureTextElapsedTicks;
    private long _runtimeMeasureTextEmptyTextCount;
    private long _runtimeMeasureTextLayoutCallCount;
    private long _runtimeDrawTextCallCount;
    private long _runtimeDrawTextElapsedTicks;
    private long _runtimeDrawTextEmptyTextCount;
    private long _runtimeDrawTextNoSpaceCount;
    private long _runtimeDrawTextLayoutCallCount;
    private long _runtimeDrawTextLineDrawCount;
    private long _runtimeDrawTextSkippedEmptyLineCount;

    public CheckBox()
    {
        IncrementAggregate(ref _diagConstructorCallCount);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeMeasureOverrideCallCount, ref _diagMeasureOverrideCallCount);
        try
        {
            var desired = base.MeasureOverride(availableSize);
            if (HasTemplateRoot)
            {
                IncrementMetric(ref _runtimeMeasureOverrideTemplateRootPathCount, ref _diagMeasureOverrideTemplateRootPathCount);
                return desired;
            }

            IncrementMetric(ref _runtimeMeasureOverrideSelfLayoutPathCount, ref _diagMeasureOverrideSelfLayoutPathCount);

            var glyphSize = GetGlyphSize();
            var textSize = MeasureText(availableSize.X, glyphSize);
            var padding = Padding;

            var width = padding.Horizontal + glyphSize + (textSize.X > 0f ? GetGlyphSpacing() + textSize.X : 0f);
            var height = padding.Vertical + MathF.Max(glyphSize, textSize.Y);

            desired.X = MathF.Max(desired.X, width);
            desired.Y = MathF.Max(desired.Y, height);
            return desired;
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            AddMetric(ref _runtimeMeasureOverrideElapsedTicks, ref _diagMeasureOverrideElapsedTicks, elapsed);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRenderCallCount, ref _diagRenderCallCount);
        try
        {
            base.OnRender(spriteBatch);
            if (HasTemplateRoot)
            {
                IncrementMetric(ref _runtimeRenderTemplateRootSkipCount, ref _diagRenderTemplateRootSkipCount);
                return;
            }

            if (IsEnabled)
            {
                IncrementMetric(ref _runtimeRenderEnabledStateCount, ref _diagRenderEnabledStateCount);
            }
            else
            {
                IncrementMetric(ref _runtimeRenderDisabledStateCount, ref _diagRenderDisabledStateCount);
            }

            switch (IsChecked)
            {
                case true:
                    IncrementMetric(ref _runtimeRenderCheckedStateCount, ref _diagRenderCheckedStateCount);
                    break;
                case null:
                    IncrementMetric(ref _runtimeRenderIndeterminateStateCount, ref _diagRenderIndeterminateStateCount);
                    break;
                default:
                    IncrementMetric(ref _runtimeRenderUncheckedStateCount, ref _diagRenderUncheckedStateCount);
                    break;
            }

            var slot = LayoutSlot;
            var opacity = Opacity;

            var padding = Padding;
            var glyphSize = GetGlyphSize();
            var glyphX = slot.X + padding.Left;
            var glyphY = slot.Y + ((slot.Height - glyphSize) / 2f);
            var glyphRect = new LayoutRect(glyphX, glyphY, glyphSize, glyphSize);

            var borderColor = IsEnabled ? BorderBrush : new Color(112, 112, 112);
            var fillColor = IsEnabled ? Background : new Color(56, 56, 56);
            var checkColor = IsEnabled ? Foreground : new Color(170, 170, 170);

            UiDrawing.DrawFilledRect(spriteBatch, glyphRect, fillColor, opacity);
            UiDrawing.DrawRectStroke(spriteBatch, glyphRect, 1f, borderColor, opacity);

            if (IsChecked == true)
            {
                var inset = MathF.Max(2f, glyphSize * 0.25f);
                var innerRect = new LayoutRect(
                    glyphRect.X + inset,
                    glyphRect.Y + inset,
                    MathF.Max(0f, glyphRect.Width - (inset * 2f)),
                    MathF.Max(0f, glyphRect.Height - (inset * 2f)));
                UiDrawing.DrawFilledRect(spriteBatch, innerRect, checkColor, opacity);
            }
            else if (IsChecked == null)
            {
                var insetY = MathF.Max(2f, glyphSize * 0.4f);
                var insetX = MathF.Max(2f, glyphSize * 0.22f);
                var lineRect = new LayoutRect(
                    glyphRect.X + insetX,
                    glyphRect.Y + insetY,
                    MathF.Max(0f, glyphRect.Width - (insetX * 2f)),
                    MathF.Max(1f, glyphRect.Height - (insetY * 2f)));
                UiDrawing.DrawFilledRect(spriteBatch, lineRect, checkColor, opacity);
            }

            DrawText(spriteBatch, slot, glyphSize);
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            AddMetric(ref _runtimeRenderElapsedTicks, ref _diagRenderElapsedTicks, elapsed);
        }
    }

    protected override Style? GetFallbackStyle()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeGetFallbackStyleCallCount, ref _diagGetFallbackStyleCallCount);
        try
        {
            if (DefaultCheckBoxStyle.IsValueCreated)
            {
                IncrementMetric(ref _runtimeGetFallbackStyleCacheHitCount, ref _diagGetFallbackStyleCacheHitCount);
            }
            else
            {
                IncrementMetric(ref _runtimeGetFallbackStyleCacheMissCount, ref _diagGetFallbackStyleCacheMissCount);
            }

            return DefaultCheckBoxStyle.Value;
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            AddMetric(ref _runtimeGetFallbackStyleElapsedTicks, ref _diagGetFallbackStyleElapsedTicks, elapsed);
        }
    }

    protected virtual float GetGlyphSize()
    {
        IncrementMetric(ref _runtimeGetGlyphSizeCallCount, ref _diagGetGlyphSizeCallCount);
        return 14f;
    }

    protected virtual float GetGlyphSpacing()
    {
        IncrementMetric(ref _runtimeGetGlyphSpacingCallCount, ref _diagGetGlyphSpacingCallCount);
        return 8f;
    }

    private Vector2 MeasureText(float availableWidth, float glyphSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeMeasureTextCallCount, ref _diagMeasureTextCallCount);
        try
        {
            var text = GetDisplayContentText();
            if (string.IsNullOrEmpty(text))
            {
                IncrementMetric(ref _runtimeMeasureTextEmptyTextCount, ref _diagMeasureTextEmptyTextCount);
                return Vector2.Zero;
            }

            IncrementMetric(ref _runtimeMeasureTextLayoutCallCount, ref _diagMeasureTextLayoutCallCount);
            _ = availableWidth;
            _ = glyphSize;
            return TextLayout.LayoutForElement(text, this, FontSize, float.PositiveInfinity, TextWrapping.NoWrap).Size;
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            AddMetric(ref _runtimeMeasureTextElapsedTicks, ref _diagMeasureTextElapsedTicks, elapsed);
        }
    }

    private void DrawText(SpriteBatch spriteBatch, LayoutRect slot, float glyphSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeDrawTextCallCount, ref _diagDrawTextCallCount);
        try
        {
            var text = GetDisplayContentText();
            if (string.IsNullOrEmpty(text))
            {
                IncrementMetric(ref _runtimeDrawTextEmptyTextCount, ref _diagDrawTextEmptyTextCount);
                return;
            }

            var padding = Padding;
            var left = slot.X + padding.Left + glyphSize + GetGlyphSpacing();
            var right = slot.X + slot.Width - padding.Right;
            var top = slot.Y + padding.Top;
            var bottom = slot.Y + slot.Height - padding.Bottom;

            var maxTextWidth = MathF.Max(0f, right - left);
            var maxTextHeight = MathF.Max(0f, bottom - top);
            if (maxTextWidth <= 0f || maxTextHeight <= 0f)
            {
                IncrementMetric(ref _runtimeDrawTextNoSpaceCount, ref _diagDrawTextNoSpaceCount);
                return;
            }

            IncrementMetric(ref _runtimeDrawTextLayoutCallCount, ref _diagDrawTextLayoutCallCount);
            var layout = TextLayout.LayoutForElement(text, this, FontSize, float.PositiveInfinity, TextWrapping.NoWrap);

            var textY = top + ((maxTextHeight - layout.Size.Y) / 2f);
            var foreground = (IsEnabled ? Foreground : new Color(170, 170, 170)) * Opacity;
            var lineSpacing = UiTextRenderer.GetLineHeight(this, FontSize);

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                if (line.Length == 0)
                {
                    IncrementMetric(ref _runtimeDrawTextSkippedEmptyLineCount, ref _diagDrawTextSkippedEmptyLineCount);
                    continue;
                }

                IncrementMetric(ref _runtimeDrawTextLineDrawCount, ref _diagDrawTextLineDrawCount);
                var linePosition = new Vector2(left, textY + (i * lineSpacing));
                UiTextRenderer.DrawString(spriteBatch, this, line, linePosition, foreground, FontSize, opaqueBackground: true);
            }
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - start;
            AddMetric(ref _runtimeDrawTextElapsedTicks, ref _diagDrawTextElapsedTicks, elapsed);
        }
    }

    internal CheckBoxRuntimeDiagnosticsSnapshot GetCheckBoxSnapshotForDiagnostics()
    {
        return new CheckBoxRuntimeDiagnosticsSnapshot(
            HasTemplateRoot,
            IsEnabled,
            IsChecked,
            IsThreeState,
            Content?.GetType().Name ?? string.Empty,
            GetDisplayContentText(),
            LayoutSlot.Width,
            LayoutSlot.Height,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideTemplateRootPathCount,
            _runtimeMeasureOverrideSelfLayoutPathCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderTemplateRootSkipCount,
            _runtimeRenderEnabledStateCount,
            _runtimeRenderDisabledStateCount,
            _runtimeRenderCheckedStateCount,
            _runtimeRenderUncheckedStateCount,
            _runtimeRenderIndeterminateStateCount,
            _runtimeGetFallbackStyleCallCount,
            TicksToMilliseconds(_runtimeGetFallbackStyleElapsedTicks),
            _runtimeGetFallbackStyleCacheHitCount,
            _runtimeGetFallbackStyleCacheMissCount,
            _runtimeGetGlyphSizeCallCount,
            _runtimeGetGlyphSpacingCallCount,
            _runtimeMeasureTextCallCount,
            TicksToMilliseconds(_runtimeMeasureTextElapsedTicks),
            _runtimeMeasureTextEmptyTextCount,
            _runtimeMeasureTextLayoutCallCount,
            _runtimeDrawTextCallCount,
            TicksToMilliseconds(_runtimeDrawTextElapsedTicks),
            _runtimeDrawTextEmptyTextCount,
            _runtimeDrawTextNoSpaceCount,
            _runtimeDrawTextLayoutCallCount,
            _runtimeDrawTextLineDrawCount,
            _runtimeDrawTextSkippedEmptyLineCount);
    }

    internal new static CheckBoxTelemetrySnapshot GetTelemetryAndReset()
    {
        return new CheckBoxTelemetrySnapshot(
            ResetAggregate(ref _diagConstructorCallCount),
            ResetAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ResetAggregate(ref _diagMeasureOverrideTemplateRootPathCount),
            ResetAggregate(ref _diagMeasureOverrideSelfLayoutPathCount),
            ResetAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagRenderElapsedTicks)),
            ResetAggregate(ref _diagRenderTemplateRootSkipCount),
            ResetAggregate(ref _diagRenderEnabledStateCount),
            ResetAggregate(ref _diagRenderDisabledStateCount),
            ResetAggregate(ref _diagRenderCheckedStateCount),
            ResetAggregate(ref _diagRenderUncheckedStateCount),
            ResetAggregate(ref _diagRenderIndeterminateStateCount),
            ResetAggregate(ref _diagGetFallbackStyleCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagGetFallbackStyleElapsedTicks)),
            ResetAggregate(ref _diagGetFallbackStyleCacheHitCount),
            ResetAggregate(ref _diagGetFallbackStyleCacheMissCount),
            ResetAggregate(ref _diagGetGlyphSizeCallCount),
            ResetAggregate(ref _diagGetGlyphSpacingCallCount),
            ResetAggregate(ref _diagMeasureTextCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagMeasureTextElapsedTicks)),
            ResetAggregate(ref _diagMeasureTextEmptyTextCount),
            ResetAggregate(ref _diagMeasureTextLayoutCallCount),
            ResetAggregate(ref _diagDrawTextCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagDrawTextElapsedTicks)),
            ResetAggregate(ref _diagDrawTextEmptyTextCount),
            ResetAggregate(ref _diagDrawTextNoSpaceCount),
            ResetAggregate(ref _diagDrawTextLayoutCallCount),
            ResetAggregate(ref _diagDrawTextLineDrawCount),
            ResetAggregate(ref _diagDrawTextSkippedEmptyLineCount),
            ResetAggregate(ref _diagBuildDefaultCheckBoxStyleCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagBuildDefaultCheckBoxStyleElapsedTicks)));
    }

    internal new static CheckBoxTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return new CheckBoxTelemetrySnapshot(
            ReadAggregate(ref _diagConstructorCallCount),
            ReadAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ReadAggregate(ref _diagMeasureOverrideTemplateRootPathCount),
            ReadAggregate(ref _diagMeasureOverrideSelfLayoutPathCount),
            ReadAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderElapsedTicks)),
            ReadAggregate(ref _diagRenderTemplateRootSkipCount),
            ReadAggregate(ref _diagRenderEnabledStateCount),
            ReadAggregate(ref _diagRenderDisabledStateCount),
            ReadAggregate(ref _diagRenderCheckedStateCount),
            ReadAggregate(ref _diagRenderUncheckedStateCount),
            ReadAggregate(ref _diagRenderIndeterminateStateCount),
            ReadAggregate(ref _diagGetFallbackStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetFallbackStyleElapsedTicks)),
            ReadAggregate(ref _diagGetFallbackStyleCacheHitCount),
            ReadAggregate(ref _diagGetFallbackStyleCacheMissCount),
            ReadAggregate(ref _diagGetGlyphSizeCallCount),
            ReadAggregate(ref _diagGetGlyphSpacingCallCount),
            ReadAggregate(ref _diagMeasureTextCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureTextElapsedTicks)),
            ReadAggregate(ref _diagMeasureTextEmptyTextCount),
            ReadAggregate(ref _diagMeasureTextLayoutCallCount),
            ReadAggregate(ref _diagDrawTextCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDrawTextElapsedTicks)),
            ReadAggregate(ref _diagDrawTextEmptyTextCount),
            ReadAggregate(ref _diagDrawTextNoSpaceCount),
            ReadAggregate(ref _diagDrawTextLayoutCallCount),
            ReadAggregate(ref _diagDrawTextLineDrawCount),
            ReadAggregate(ref _diagDrawTextSkippedEmptyLineCount),
            ReadAggregate(ref _diagBuildDefaultCheckBoxStyleCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagBuildDefaultCheckBoxStyleElapsedTicks)));
    }

    private static Style BuildDefaultCheckBoxStyle()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementAggregate(ref _diagBuildDefaultCheckBoxStyleCallCount);
        try
        {
            var style = new Style(typeof(CheckBox));

            style.Setters.Add(new Setter(BackgroundProperty, new Color(36, 36, 36)));
            style.Setters.Add(new Setter(BorderBrushProperty, new Color(186, 186, 186)));
            style.Setters.Add(new Setter(ForegroundProperty, Color.White));

            var hoverTrigger = new Trigger(IsMouseOverProperty, true);
            hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(220, 220, 220)));

            var disabledTrigger = new Trigger(IsEnabledProperty, false);
            disabledTrigger.Setters.Add(new Setter(BorderBrushProperty, new Color(112, 112, 112)));
            disabledTrigger.Setters.Add(new Setter(ForegroundProperty, new Color(170, 170, 170)));

            style.Triggers.Add(hoverTrigger);
            style.Triggers.Add(disabledTrigger);

            return style;
        }
        finally
        {
            AddAggregate(ref _diagBuildDefaultCheckBoxStyleElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void IncrementMetric(ref long runtimeField, ref long aggregateField)
    {
        runtimeField++;
        IncrementAggregate(ref aggregateField);
    }

    private void AddMetric(ref long runtimeField, ref long aggregateField, long value)
    {
        runtimeField += value;
        AddAggregate(ref aggregateField, value);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void IncrementAggregate(ref long field)
    {
        Interlocked.Increment(ref field);
    }

    private static void AddAggregate(ref long field, long value)
    {
        Interlocked.Add(ref field, value);
    }

    private static long ReadAggregate(ref long field)
    {
        return Interlocked.Read(ref field);
    }

    private static long ResetAggregate(ref long field)
    {
        return Interlocked.Exchange(ref field, 0);
    }
}
