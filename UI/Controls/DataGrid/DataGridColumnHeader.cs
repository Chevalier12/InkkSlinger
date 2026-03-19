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

    internal void BindState(DataGridColumnState columnState)
    {
        ColumnIndex = columnState.DisplayIndex;
        Content = columnState.HeaderText;
        SortDirection = columnState.SortDirection;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return desired;
        }

        if (Owner != null &&
            float.IsFinite(availableSize.X) &&
            float.IsFinite(availableSize.Y))
        {
            desired.X = System.MathF.Max(desired.X, System.MathF.Max(0f, availableSize.X));
            desired.Y = System.MathF.Max(desired.Y, System.MathF.Max(0f, availableSize.Y));
            return desired;
        }

        var padding = Padding;
        var border = BorderThickness;
        var fontSize = ResolveFontSize();
        var text = GetDisplayContentText();
        var textWidth = string.IsNullOrEmpty(text)
            ? 0f
            : UiTextRenderer.MeasureWidth(this, text, fontSize);
        var glyphWidth = SortDirection == DataGridSortDirection.None
            ? 0f
            : UiTextRenderer.MeasureWidth(this, "^", fontSize) + 6f;
        var textHeight = UiTextRenderer.GetLineHeight(this, fontSize);

        desired.X = System.MathF.Max(desired.X, textWidth + glyphWidth + padding.Horizontal + border.Horizontal);
        desired.Y = System.MathF.Max(desired.Y, System.MathF.Max(16f, textHeight) + padding.Vertical + border.Vertical);
        return desired;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        if (Owner != null && Owner.TryGetScrollableHeaderClipRect(ColumnIndex, out clipRect))
        {
            return true;
        }

        return base.TryGetClipRect(out clipRect);
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
        var text = GetDisplayContentText();
        var glyph = SortDirection == DataGridSortDirection.None
            ? string.Empty
            : (SortDirection == DataGridSortDirection.Ascending ? "^" : "v");
        var glyphWidth = string.IsNullOrEmpty(glyph)
            ? 0f
            : UiTextRenderer.MeasureWidth(this, glyph, fontSize);

        var textLeft = slot.X + border.Left + padding.Left;
        var textRight = slot.X + slot.Width - border.Right - padding.Right - (glyphWidth > 0f ? glyphWidth + 6f : 0f);
        if (!string.IsNullOrEmpty(text) && textRight > textLeft)
        {
            var lineHeight = UiTextRenderer.GetLineHeight(this, fontSize);
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
        var width = UiTextRenderer.MeasureWidth(this, glyph, fontSize);
        var x = LayoutSlot.X + LayoutSlot.Width - BorderThickness.Right - Padding.Right - width;
        var y = LayoutSlot.Y + ((LayoutSlot.Height - UiTextRenderer.GetLineHeight(this, fontSize)) / 2f);
        var color = Foreground * Opacity;
        DrawHeaderString(spriteBatch, glyph, new Vector2(x, y), color, fontSize);
    }

    private void DrawHeaderString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float fontSize)
    {
        UiTextRenderer.DrawString(spriteBatch, this, text, position, color, fontSize, opaqueBackground: true);
    }

    private float ResolveFontSize()
    {
        return System.MathF.Max(8f, FontSize);
    }
}
