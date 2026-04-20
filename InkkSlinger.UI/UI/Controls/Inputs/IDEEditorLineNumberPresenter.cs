using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class IDEEditorLineNumberPresenter : Panel
{
    private readonly List<TextBlock> _lineTextBlocks = [];
    private readonly List<string> _visibleLineTexts = [];
    private float _lineHeight = 1f;
    private float _verticalLineOffset;
    private Color _lineForeground = Color.White;

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
        var clampedFirstVisibleLine = Math.Max(0, firstVisibleLine);
        var clampedVisibleLineCount = Math.Max(0, visibleLineCount);
        var changed = FirstVisibleLine != clampedFirstVisibleLine || _visibleLineTexts.Count != clampedVisibleLineCount;

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
        }

        if (changed)
        {
            ApplyTextStyle();
            InvalidateMeasure();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        ApplyTextStyle();
        foreach (var textBlock in _lineTextBlocks)
        {
            textBlock.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        }

        return new Vector2(
            MathF.Max(0f, availableSize.X),
            float.IsFinite(availableSize.Y)
                ? MathF.Max(0f, availableSize.Y)
                : MathF.Max(0f, VisibleLineCount * LineHeight));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        for (var lineIndex = 0; lineIndex < _lineTextBlocks.Count; lineIndex++)
        {
            var textBlock = _lineTextBlocks[lineIndex];
            var childSize = textBlock.DesiredSize;
            var centeredYOffset = MathF.Max(0f, (LineHeight - childSize.Y) / 2f);
            var x = LayoutSlot.X + MathF.Max(0f, finalSize.X - childSize.X);
            var y = LayoutSlot.Y - VerticalLineOffset + (lineIndex * LineHeight) + centeredYOffset;
            textBlock.Arrange(new LayoutRect(x, y, childSize.X, childSize.Y));
        }

        return finalSize;
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