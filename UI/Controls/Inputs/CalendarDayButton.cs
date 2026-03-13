using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed class CalendarDayButton : Button
{
    public static readonly DependencyProperty DayTextProperty =
        DependencyProperty.Register(
            nameof(DayText),
            typeof(string),
            typeof(CalendarDayButton),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public string DayText
    {
        get => GetValue<string>(DayTextProperty) ?? string.Empty;
        set => SetValue(DayTextProperty, value);
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (HasTemplateRoot)
        {
            DrawTemplateVisualTree(spriteBatch);
        }
        else
        {
            base.OnRender(spriteBatch);
        }

        if (string.IsNullOrEmpty(DayText))
        {
            return;
        }

        var slot = LayoutSlot;
        var padding = Padding;
        var left = slot.X + padding.Left + BorderThickness;
        var right = slot.X + slot.Width - padding.Right - BorderThickness;
        var top = slot.Y + padding.Top + BorderThickness;
        var bottom = slot.Y + slot.Height - padding.Bottom - BorderThickness;
        var maxTextWidth = MathF.Max(0f, right - left);
        var maxTextHeight = MathF.Max(0f, bottom - top);
        if (maxTextWidth <= 0f || maxTextHeight <= 0f)
        {
            return;
        }

        var textWidth = UiTextRenderer.MeasureWidth(this, DayText, FontSize);
        var lineHeight = UiTextRenderer.GetLineHeight(this, FontSize);
        var position = new Vector2(
            left + ((maxTextWidth - textWidth) / 2f),
            top + ((maxTextHeight - lineHeight) / 2f));
        UiTextRenderer.DrawString(spriteBatch, this, DayText, position, Foreground * Opacity, FontSize, opaqueBackground: true);
    }

    protected override bool ShouldAutoDrawVisualChildren => !HasTemplateRoot;

    internal override IEnumerable<UIElement> GetRetainedRenderChildren()
    {
        if (HasTemplateRoot)
        {
            yield break;
        }

        foreach (var child in base.GetRetainedRenderChildren())
        {
            yield return child;
        }
    }

    private void DrawTemplateVisualTree(SpriteBatch spriteBatch)
    {
        foreach (var child in base.GetVisualChildren())
        {
            child.Draw(spriteBatch);
        }
    }
}
