using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class CheckBox : ToggleButton
{
    private static readonly Lazy<Style> DefaultCheckBoxStyle = new(BuildDefaultCheckBoxStyle);

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var glyphSize = GetGlyphSize();
        var textSize = MeasureText(availableSize.X, glyphSize);
        var padding = Padding;

        var width = padding.Horizontal + glyphSize + (textSize.X > 0f ? GetGlyphSpacing() + textSize.X : 0f);
        var height = padding.Vertical + MathF.Max(glyphSize, textSize.Y);

        desired.X = MathF.Max(desired.X, width);
        desired.Y = MathF.Max(desired.Y, height);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
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

    protected override Style? GetFallbackStyle()
    {
        return DefaultCheckBoxStyle.Value;
    }

    protected virtual float GetGlyphSize() => 14f;

    protected virtual float GetGlyphSpacing() => 8f;

    private Vector2 MeasureText(float availableWidth, float glyphSize)
    {
        if (Font == null || string.IsNullOrEmpty(Text))
        {
            return Vector2.Zero;
        }

        var maxTextWidth = MathF.Max(0f, availableWidth - Padding.Horizontal - glyphSize - GetGlyphSpacing());
        var textAvailableWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : maxTextWidth;
        return TextLayout.Layout(Text, Font, textAvailableWidth, TextWrapping).Size;
    }

    private void DrawText(SpriteBatch spriteBatch, LayoutRect slot, float glyphSize)
    {
        if (Font == null || string.IsNullOrEmpty(Text))
        {
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
            return;
        }

        var textAvailableWidth = TextWrapping == TextWrapping.NoWrap
            ? float.PositiveInfinity
            : maxTextWidth;
        var layout = TextLayout.Layout(Text, Font, textAvailableWidth, TextWrapping);

        var textY = top + ((maxTextHeight - layout.Size.Y) / 2f);
        var foreground = (IsEnabled ? Foreground : new Color(170, 170, 170)) * Opacity;
        var lineSpacing = FontStashTextRenderer.GetLineHeight(Font);

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var linePosition = new Vector2(left, textY + (i * lineSpacing));
            FontStashTextRenderer.DrawString(spriteBatch, Font, line, linePosition, foreground);
        }
    }

    private static Style BuildDefaultCheckBoxStyle()
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
}
