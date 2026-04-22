using System;
using System.Collections.Generic;
using System.Diagnostics;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class IDEEditorLineNumberPresenter : Panel
{
    private static int _diagUpdateVisibleRangeCallCount;
    private static int _diagUpdateVisibleRangeChangedCount;
    private static int _diagUpdateVisibleRangeTextUpdateCount;
    private static int _diagUpdateVisibleRangeVisibleLineTotal;
    private static long _diagUpdateVisibleRangeElapsedTicks;
    private static int _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static int _diagArrangeOverrideCallCount;
    private static long _diagArrangeOverrideElapsedTicks;

    private readonly List<TextBlock> _lineTextBlocks = [];
    private readonly List<string> _visibleLineTexts = [];
    private float _lineHeight = 1f;
    private float _verticalLineOffset;
    private Color _lineForeground = Color.White;
    private int _runtimeUpdateVisibleRangeCallCount;
    private int _runtimeUpdateVisibleRangeChangedCount;
    private int _runtimeUpdateVisibleRangeTextUpdateCount;
    private int _runtimeUpdateVisibleRangeVisibleLineTotal;
    private long _runtimeUpdateVisibleRangeElapsedTicks;
    private int _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private int _runtimeArrangeOverrideCallCount;
    private long _runtimeArrangeOverrideElapsedTicks;

    public IDEEditorLineNumberPresenter()
    {
        IsHitTestVisible = false;
    }

    public float LineHeight
    {
        get => _lineHeight;
        set
        {
            var clamped = Math.Max(1f, value);
            if (Math.Abs(_lineHeight - clamped) <= 0.01f)
            {
                return;
            }

            _lineHeight = clamped;
            InvalidateVisual();
        }
    }

    public float VerticalLineOffset
    {
        get => _verticalLineOffset;
        set
        {
            var clamped = Math.Max(0f, value);
            if (Math.Abs(_verticalLineOffset - clamped) <= 0.01f)
            {
                return;
            }

            _verticalLineOffset = clamped;
            InvalidateArrange();
        }
    }

    public Color LineForeground
    {
        get => _lineForeground;
        set
        {
            if (_lineForeground == value)
            {
                return;
            }

            _lineForeground = value;
            ApplyTextStyle();
        }
    }

    public FontFamily FontFamily
    {
        get => FrameworkElement.GetFontFamily(this);
        set => FrameworkElement.SetFontFamily(this, value);
    }

    public float FontSize
    {
        get => FrameworkElement.GetFontSize(this);
        set => FrameworkElement.SetFontSize(this, value);
    }

    public IReadOnlyList<string> VisibleLineTexts => _visibleLineTexts;

    public int FirstVisibleLine { get; private set; }

    public int VisibleLineCount => _visibleLineTexts.Count;

    public void UpdateVisibleRange(int firstVisibleLine, int visibleLineCount)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagUpdateVisibleRangeCallCount++;
        _runtimeUpdateVisibleRangeCallCount++;
        var clampedFirstVisibleLine = Math.Max(0, firstVisibleLine);
        var clampedVisibleLineCount = Math.Max(0, visibleLineCount);
        var changed = FirstVisibleLine != clampedFirstVisibleLine || _visibleLineTexts.Count != clampedVisibleLineCount;
        var textUpdateCount = 0;

        FirstVisibleLine = clampedFirstVisibleLine;

        while (_lineTextBlocks.Count < clampedVisibleLineCount)
        {
            var textBlock = CreateLineTextBlock();
            _lineTextBlocks.Add(textBlock);
            AddChild(textBlock);
            changed = true;
        }

        while (_lineTextBlocks.Count > clampedVisibleLineCount)
        {
            var textBlock = _lineTextBlocks[^1];
            RemoveChild(textBlock);
            _lineTextBlocks.RemoveAt(_lineTextBlocks.Count - 1);
            changed = true;
        }

        while (_visibleLineTexts.Count < clampedVisibleLineCount)
        {
            _visibleLineTexts.Add(string.Empty);
        }

        while (_visibleLineTexts.Count > clampedVisibleLineCount)
        {
            _visibleLineTexts.RemoveAt(_visibleLineTexts.Count - 1);
        }

        for (var lineIndex = 0; lineIndex < clampedVisibleLineCount; lineIndex++)
        {
            var text = (clampedFirstVisibleLine + lineIndex + 1).ToString();
            if (string.Equals(_visibleLineTexts[lineIndex], text, StringComparison.Ordinal))
            {
                continue;
            }

            _visibleLineTexts[lineIndex] = text;
            _lineTextBlocks[lineIndex].Text = text;
            changed = true;
            textUpdateCount++;
        }

        if (changed)
        {
            ApplyTextStyle();
            InvalidateMeasure();
            _diagUpdateVisibleRangeChangedCount++;
            _runtimeUpdateVisibleRangeChangedCount++;
        }

        _diagUpdateVisibleRangeTextUpdateCount += textUpdateCount;
        _runtimeUpdateVisibleRangeTextUpdateCount += textUpdateCount;
        _diagUpdateVisibleRangeVisibleLineTotal += clampedVisibleLineCount;
        _runtimeUpdateVisibleRangeVisibleLineTotal += clampedVisibleLineCount;
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagUpdateVisibleRangeElapsedTicks += elapsedTicks;
        _runtimeUpdateVisibleRangeElapsedTicks += elapsedTicks;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagMeasureOverrideCallCount++;
        _runtimeMeasureOverrideCallCount++;
        ApplyTextStyle();
        foreach (var textBlock in _lineTextBlocks)
        {
            textBlock.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        }

        var measured = new Vector2(
            MathF.Max(0f, availableSize.X),
            float.IsFinite(availableSize.Y)
                ? MathF.Max(0f, availableSize.Y)
                : MathF.Max(0f, VisibleLineCount * LineHeight));
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagMeasureOverrideElapsedTicks += elapsedTicks;
        _runtimeMeasureOverrideElapsedTicks += elapsedTicks;
        return measured;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var startTicks = Stopwatch.GetTimestamp();
        _diagArrangeOverrideCallCount++;
        _runtimeArrangeOverrideCallCount++;
        for (var lineIndex = 0; lineIndex < _lineTextBlocks.Count; lineIndex++)
        {
            var textBlock = _lineTextBlocks[lineIndex];
            var childSize = textBlock.DesiredSize;
            var centeredYOffset = MathF.Max(0f, (LineHeight - childSize.Y) / 2f);
            var x = LayoutSlot.X + MathF.Max(0f, finalSize.X - childSize.X);
            var y = LayoutSlot.Y - VerticalLineOffset + (lineIndex * LineHeight) + centeredYOffset;
            textBlock.Arrange(new LayoutRect(x, y, childSize.X, childSize.Y));
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        _diagArrangeOverrideElapsedTicks += elapsedTicks;
        _runtimeArrangeOverrideElapsedTicks += elapsedTicks;
        return finalSize;
    }

    internal IDEEditorLineNumberPresenterRuntimeDiagnosticsSnapshot GetIDEEditorLineNumberPresenterSnapshotForDiagnostics()
    {
        return new IDEEditorLineNumberPresenterRuntimeDiagnosticsSnapshot(
            UpdateVisibleRangeCallCount: _runtimeUpdateVisibleRangeCallCount,
            UpdateVisibleRangeChangedCount: _runtimeUpdateVisibleRangeChangedCount,
            UpdateVisibleRangeTextUpdateCount: _runtimeUpdateVisibleRangeTextUpdateCount,
            UpdateVisibleRangeVisibleLineTotal: _runtimeUpdateVisibleRangeVisibleLineTotal,
            UpdateVisibleRangeMilliseconds: TicksToMilliseconds(_runtimeUpdateVisibleRangeElapsedTicks),
            MeasureOverrideCallCount: _runtimeMeasureOverrideCallCount,
            MeasureOverrideMilliseconds: TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            ArrangeOverrideCallCount: _runtimeArrangeOverrideCallCount,
            ArrangeOverrideMilliseconds: TicksToMilliseconds(_runtimeArrangeOverrideElapsedTicks),
            FirstVisibleLine: FirstVisibleLine,
            VisibleLineCount: VisibleLineCount,
            LineHeight: LineHeight,
            VerticalLineOffset: VerticalLineOffset);
    }

    internal new static IDEEditorLineNumberPresenterTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return CreateTelemetrySnapshot(reset: false);
    }

    internal new static IDEEditorLineNumberPresenterTelemetrySnapshot GetTelemetryAndReset()
    {
        return CreateTelemetrySnapshot(reset: true);
    }

    private static IDEEditorLineNumberPresenterTelemetrySnapshot CreateTelemetrySnapshot(bool reset)
    {
        var snapshot = new IDEEditorLineNumberPresenterTelemetrySnapshot(
            UpdateVisibleRangeCallCount: _diagUpdateVisibleRangeCallCount,
            UpdateVisibleRangeChangedCount: _diagUpdateVisibleRangeChangedCount,
            UpdateVisibleRangeTextUpdateCount: _diagUpdateVisibleRangeTextUpdateCount,
            UpdateVisibleRangeVisibleLineTotal: _diagUpdateVisibleRangeVisibleLineTotal,
            UpdateVisibleRangeMilliseconds: TicksToMilliseconds(_diagUpdateVisibleRangeElapsedTicks),
            MeasureOverrideCallCount: _diagMeasureOverrideCallCount,
            MeasureOverrideMilliseconds: TicksToMilliseconds(_diagMeasureOverrideElapsedTicks),
            ArrangeOverrideCallCount: _diagArrangeOverrideCallCount,
            ArrangeOverrideMilliseconds: TicksToMilliseconds(_diagArrangeOverrideElapsedTicks));

        if (reset)
        {
            _diagUpdateVisibleRangeCallCount = 0;
            _diagUpdateVisibleRangeChangedCount = 0;
            _diagUpdateVisibleRangeTextUpdateCount = 0;
            _diagUpdateVisibleRangeVisibleLineTotal = 0;
            _diagUpdateVisibleRangeElapsedTicks = 0;
            _diagMeasureOverrideCallCount = 0;
            _diagMeasureOverrideElapsedTicks = 0;
            _diagArrangeOverrideCallCount = 0;
            _diagArrangeOverrideElapsedTicks = 0;
        }

        return snapshot;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private TextBlock CreateLineTextBlock()
    {
        return new TextBlock
        {
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.NoWrap
        };
    }

    private void ApplyTextStyle()
    {
        foreach (var textBlock in _lineTextBlocks)
        {
            textBlock.FontFamily = FontFamily;
            textBlock.FontSize = FontSize;
            textBlock.Foreground = LineForeground;
        }
    }
}