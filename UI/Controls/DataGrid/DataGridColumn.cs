using System;

namespace InkkSlinger;

public class DataGridColumn
{
    public string Header { get; set; } = string.Empty;

    public string BindingPath { get; set; } = string.Empty;

    public float Width { get; set; } = float.NaN;

    public float MinWidth { get; set; } = 40f;

    public bool CanUserSort { get; set; } = true;

    public DataGridSortDirection SortDirection { get; internal set; }

    internal float GetResolvedWidth()
    {
        if (!float.IsNaN(Width) && Width > 0f)
        {
            return MathF.Max(MinWidth, Width);
        }

        return MathF.Max(MinWidth, 120f);
    }
}

public sealed class DataGridSortingEventArgs : EventArgs
{
    public DataGridSortingEventArgs(DataGridColumn column, int columnIndex)
    {
        Column = column;
        ColumnIndex = columnIndex;
    }

    public DataGridColumn Column { get; }

    public int ColumnIndex { get; }
}
