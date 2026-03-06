using System;

namespace InkkSlinger;

public class DataGridColumn
{
    public string Header { get; set; } = string.Empty;

    public string BindingPath { get; set; } = string.Empty;

    public int DisplayIndex { get; set; } = -1;

    public float Width { get; set; } = float.NaN;

    public float MinWidth { get; set; } = 40f;

    public bool CanUserSort { get; set; } = true;

    public bool CanUserResize { get; set; } = true;

    public bool CanUserReorder { get; set; } = true;

    public bool IsReadOnly { get; set; }

    public DataGridSortDirection SortDirection { get; internal set; }

    internal Func<object?, DataGrid, UIElement?>? EditingElementFactory { get; set; }

    internal float GetResolvedWidth()
    {
        if (!float.IsNaN(Width) && Width > 0f)
        {
            return MathF.Max(MinWidth, Width);
        }

        return MathF.Max(MinWidth, 120f);
    }

    internal UIElement? CreateEditingElement(object? item, DataGrid owner)
    {
        if (EditingElementFactory != null)
        {
            return EditingElementFactory(item, owner);
        }

        var editor = new TextBox
        {
            Text = owner.ResolveCellContent(item, this)?.ToString() ?? string.Empty,
            Font = owner.Font,
            Foreground = owner.Foreground,
            DataContext = item
        };

        return editor;
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
