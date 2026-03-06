using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal sealed class DataGridRowHeaderLaneCoordinator
{
    public void ConfigureHeader(DataGridRowHeader header, DataGrid owner, DataGridRowState rowState, bool isVisible)
    {
        header.Text = (rowState.RowIndex + 1).ToString();
        header.Font = owner.Font;
        header.IsVisible = isVisible;
    }

    public float ResolveWidth(DataGrid? owner)
    {
        return owner?.RowHeadersVisibleForLayout == true ? owner.RowHeaderWidth : 0f;
    }

    public void MeasureHeader(DataGridRowHeader header, float rowHeaderWidth, float rowHeight)
    {
        header.Measure(new Vector2(rowHeaderWidth, rowHeight));
    }

    public void ArrangeHeader(DataGridRowHeader header, float x, float y, float rowHeaderWidth, float rowHeight)
    {
        header.Arrange(new LayoutRect(x, y, rowHeaderWidth, rowHeight));
    }
}
