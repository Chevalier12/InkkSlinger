using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridColumnHeader : Button
{
    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(DataGridColumnHeader),
            new FrameworkPropertyMetadata(Thickness.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SortDirectionProperty =
        DependencyProperty.Register(
            nameof(SortDirection),
            typeof(DataGridSortDirection),
            typeof(DataGridColumnHeader),
            new FrameworkPropertyMetadata(DataGridSortDirection.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public new Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public DataGridSortDirection SortDirection
    {
        get => GetValue<DataGridSortDirection>(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    internal DataGrid? Owner { get; set; }

    internal int ColumnIndex { get; set; }

    internal void BindState(DataGridColumnState columnState, SpriteFont? font)
    {
        ColumnIndex = columnState.DisplayIndex;
        Text = columnState.HeaderText;
        Font = font;
        SortDirection = columnState.SortDirection;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return desired;
        }

        var padding = Padding;
        var border = BorderThickness;
        var fontSize = ResolveFontSize();
        var text = Text ?? string.Empty;
        var textWidth = string.IsNullOrEmpty(text)
            ? 0f
            : FontStashTextRenderer.MeasureWidth(text, fontSize);
        var glyphWidth = SortDirection == DataGridSortDirection.None
            ? 0f
            : FontStashTextRenderer.MeasureWidth("^", fontSize) + 6f;
        var textHeight = fontSize;

        desired.X = System.MathF.Max(desired.X, textWidth + glyphWidth + padding.Horizontal + border.Horizontal);
        desired.Y = System.MathF.Max(desired.Y, System.MathF.Max(16f, textHeight) + padding.Vertical + border.Vertical);
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (HasTemplateRoot)
        {
            base.OnRender(spriteBatch);
            DrawSortGlyph(spriteBatch);
            return;
        }

        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        var border = BorderThickness;
        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, border.Left, slot.Height), BorderBrush, Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height), BorderBrush, Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, slot.Width, border.Top), BorderBrush, Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom), BorderBrush, Opacity);
        }

        var padding = Padding;
        var fontSize = ResolveFontSize();
        var text = Text ?? string.Empty;
        var glyph = SortDirection == DataGridSortDirection.None
            ? string.Empty
            : (SortDirection == DataGridSortDirection.Ascending ? "^" : "v");
        var glyphWidth = string.IsNullOrEmpty(glyph)
            ? 0f
            : FontStashTextRenderer.MeasureWidth(glyph, fontSize);

        var textLeft = slot.X + border.Left + padding.Left;
        var textRight = slot.X + slot.Width - border.Right - padding.Right - (glyphWidth > 0f ? glyphWidth + 6f : 0f);
        if (!string.IsNullOrEmpty(text) && textRight > textLeft)
        {
            var lineHeight = fontSize;
            var textY = slot.Y + ((slot.Height - lineHeight) / 2f);
            var textColor = Foreground * Opacity;
            DrawHeaderString(spriteBatch, text, new Vector2(textLeft, textY), textColor, fontSize);
        }

        DrawSortGlyph(spriteBatch);
    }

    private void DrawSortGlyph(SpriteBatch spriteBatch)
    {
        if (SortDirection == DataGridSortDirection.None)
        {
            return;
        }

        var glyph = SortDirection == DataGridSortDirection.Ascending ? "^" : "v";
        var fontSize = ResolveFontSize();
        var width = FontStashTextRenderer.MeasureWidth(glyph, fontSize);
        var x = LayoutSlot.X + LayoutSlot.Width - BorderThickness.Right - Padding.Right - width;
        var y = LayoutSlot.Y + ((LayoutSlot.Height - fontSize) / 2f);
        var color = Foreground * Opacity;
        DrawHeaderString(spriteBatch, glyph, new Vector2(x, y), color, fontSize);
    }

    private void DrawHeaderString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float fontSize)
    {
        FontStashTextRenderer.DrawString(spriteBatch, text, position, color, fontSize);

        var weight = FontWeight ?? string.Empty;
        if (string.Equals(weight, "Bold", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(weight, "SemiBold", System.StringComparison.OrdinalIgnoreCase))
        {
            FontStashTextRenderer.DrawString(spriteBatch, text, new Vector2(position.X + 0.75f, position.Y), color * 0.5f, fontSize);
        }
    }

    private float ResolveFontSize()
    {
        return System.MathF.Max(8f, FontSize);
    }
}
