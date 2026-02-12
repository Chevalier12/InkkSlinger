using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class DataGridColumnHeader : Button
{
    public static readonly DependencyProperty SortDirectionProperty =
        DependencyProperty.Register(
            nameof(SortDirection),
            typeof(DataGridSortDirection),
            typeof(DataGridColumnHeader),
            new FrameworkPropertyMetadata(DataGridSortDirection.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public DataGridSortDirection SortDirection
    {
        get => GetValue<DataGridSortDirection>(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    internal int ColumnIndex { get; set; }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        base.OnRender(spriteBatch);

        if (SortDirection == DataGridSortDirection.None || Font == null)
        {
            return;
        }

        var glyph = SortDirection == DataGridSortDirection.Ascending ? "^" : "v";
        var width = FontStashTextRenderer.MeasureWidth(Font, glyph);
        var x = LayoutSlot.X + LayoutSlot.Width - width - 4f;
        var y = LayoutSlot.Y + ((LayoutSlot.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, glyph, new Vector2(x, y), Foreground * Opacity);
    }
}
