using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class AccessText : TextBlock
{
    public static readonly DependencyProperty TargetNameProperty =
        DependencyProperty.Register(
            nameof(TargetName),
            typeof(string),
            typeof(AccessText),
            new FrameworkPropertyMetadata(string.Empty));

    private AccessTextParser.AccessTextParseResult _parsedText = AccessTextParser.AccessTextParseResult.Empty;

    public AccessText()
    {
        UpdateParsedText();
    }

    public string TargetName
    {
        get => GetValue<string>(TargetNameProperty) ?? string.Empty;
        set => SetValue(TargetNameProperty, value);
    }

    internal char? AccessKey => _parsedText.AccessKey;

    internal int AccessKeyDisplayIndex => _parsedText.AccessKeyDisplayIndex;

    internal string DisplayText => _parsedText.DisplayText;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var availableWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : availableSize.X;

        return TextLayout.Layout(DisplayText, Font, availableWidth, TextWrapping).Size;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (string.IsNullOrEmpty(DisplayText))
        {
            return;
        }

        var renderWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : RenderSize.X;
        var layout = TextLayout.Layout(DisplayText, Font, renderWidth, TextWrapping);
        var lineSpacing = FontStashTextRenderer.GetLineHeight(Font);
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var position = new Vector2(LayoutSlot.X, LayoutSlot.Y + (i * lineSpacing));
            FontStashTextRenderer.DrawString(spriteBatch, Font, line, position, Foreground * Opacity);
        }

        if (AccessKeyDisplayIndex < 0 || AccessKey == null)
        {
            return;
        }

        if (!TryMapAccessKeyToLineAndColumn(layout.Lines, AccessKeyDisplayIndex, out var lineIndex, out var columnIndex))
        {
            return;
        }

        var keyLine = layout.Lines[lineIndex];
        if (columnIndex < 0 || columnIndex >= keyLine.Length)
        {
            return;
        }

        var prefix = keyLine[..columnIndex];
        var glyph = keyLine[columnIndex].ToString();
        var prefixWidth = FontStashTextRenderer.MeasureWidth(Font, prefix);
        var glyphWidth = MathF.Max(1f, FontStashTextRenderer.MeasureWidth(Font, glyph));
        var lineY = LayoutSlot.Y + (lineIndex * lineSpacing);
        var underlineY = lineY + lineSpacing - 2f;
        var underlineRect = new LayoutRect(LayoutSlot.X + prefixWidth, underlineY, glyphWidth, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, underlineRect, Foreground * Opacity, 1f);
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (ReferenceEquals(args.Property, TextProperty))
        {
            UpdateParsedText();
        }
    }

    private void UpdateParsedText()
    {
        _parsedText = AccessTextParser.Parse(Text);
    }

    internal static bool TryMapAccessKeyToLineAndColumn(
        System.Collections.Generic.IReadOnlyList<string> lines,
        int displayIndex,
        out int lineIndex,
        out int columnIndex)
    {
        lineIndex = -1;
        columnIndex = -1;
        if (displayIndex < 0)
        {
            return false;
        }

        var remaining = displayIndex;
        for (var i = 0; i < lines.Count; i++)
        {
            // TextLayout emits logical content-only lines (no trailing line terminators),
            // so display-index mapping can use direct per-line lengths safely.
            var lineLength = lines[i].Length;
            if (remaining < lineLength)
            {
                lineIndex = i;
                columnIndex = remaining;
                return true;
            }

            remaining -= lineLength;
        }

        return false;
    }
}
