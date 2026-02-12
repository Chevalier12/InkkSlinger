using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class RadioButton : ToggleButton
{
    private static readonly Lazy<Style> DefaultRadioButtonStyle = new(BuildDefaultRadioButtonStyle);

    public static readonly DependencyProperty GroupNameProperty =
        DependencyProperty.Register(
            nameof(GroupName),
            typeof(string),
            typeof(RadioButton),
            new FrameworkPropertyMetadata(string.Empty));

    public string GroupName
    {
        get => GetValue<string>(GroupNameProperty) ?? string.Empty;
        set => SetValue(GroupNameProperty, value);
    }

    protected override void OnToggle()
    {
        if (IsChecked != true)
        {
            IsChecked = true;
        }
    }

    protected override void OnIsCheckedChanged(bool? isChecked)
    {
        base.OnIsCheckedChanged(isChecked);

        if (isChecked != true)
        {
            return;
        }

        foreach (var peer in EnumerateGroupPeers())
        {
            if (!ReferenceEquals(peer, this) && peer.IsChecked == true)
            {
                peer.IsChecked = false;
            }
        }
    }

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
        var radius = glyphSize / 2f;
        var center = new Vector2(
            slot.X + padding.Left + radius,
            slot.Y + (slot.Height / 2f));

        var borderColor = IsEnabled ? BorderBrush : new Color(112, 112, 112);
        var fillColor = IsEnabled ? Background : new Color(56, 56, 56);
        var dotColor = IsEnabled ? Foreground : new Color(170, 170, 170);

        UiDrawing.DrawFilledCircle(spriteBatch, center, radius, fillColor, opacity);
        UiDrawing.DrawCircleStroke(spriteBatch, center, radius, 1f, borderColor, opacity);

        if (IsChecked == true)
        {
            UiDrawing.DrawFilledCircle(spriteBatch, center, MathF.Max(2f, radius * 0.45f), dotColor, opacity);
        }

        DrawText(spriteBatch, slot, glyphSize);
    }

    protected override Style? GetFallbackStyle()
    {
        return DefaultRadioButtonStyle.Value;
    }

    private IEnumerable<RadioButton> EnumerateGroupPeers()
    {
        var groupName = GroupName;

        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var root = GetTreeRoot();
            if (root == null)
            {
                yield break;
            }

            var pending = new Stack<UIElement>();
            var visited = new HashSet<UIElement>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (current is RadioButton radio &&
                    string.Equals(radio.GroupName, groupName, StringComparison.Ordinal))
                {
                    yield return radio;
                }

                foreach (var child in current.GetVisualChildren())
                {
                    pending.Push(child);
                }

                foreach (var child in current.GetLogicalChildren())
                {
                    pending.Push(child);
                }
            }

            yield break;
        }

        var parent = LogicalParent ?? VisualParent;
        if (parent == null)
        {
            yield return this;
            yield break;
        }

        foreach (var child in parent.GetLogicalChildren())
        {
            if (child is RadioButton radio)
            {
                yield return radio;
            }
        }
    }

    private UIElement? GetTreeRoot()
    {
        UIElement? root = this;
        while (root?.VisualParent != null)
        {
            root = root.VisualParent;
        }

        return root;
    }

    private static Style BuildDefaultRadioButtonStyle()
    {
        var style = new Style(typeof(RadioButton));

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

    private float GetGlyphSize() => 14f;

    private float GetGlyphSpacing() => 8f;

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
}
