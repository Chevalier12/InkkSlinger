using System;

namespace InkkSlinger;

public sealed class DataGridBeginningEditEventArgs : EventArgs
{
    public DataGridBeginningEditEventArgs(object? item, int rowIndex, DataGridColumn column, int columnIndex, DataGridEditTriggerSource triggerSource)
    {
        Item = item;
        RowIndex = rowIndex;
        Column = column;
        ColumnIndex = columnIndex;
        TriggerSource = triggerSource;
    }

    public object? Item { get; }

    public int RowIndex { get; }

    public DataGridColumn Column { get; }

    public int ColumnIndex { get; }

    public DataGridEditTriggerSource TriggerSource { get; }

    public bool Cancel { get; set; }
}

public sealed class DataGridPreparingCellForEditEventArgs : EventArgs
{
    public DataGridPreparingCellForEditEventArgs(object? item, int rowIndex, DataGridColumn column, int columnIndex, UIElement editingElement)
    {
        Item = item;
        RowIndex = rowIndex;
        Column = column;
        ColumnIndex = columnIndex;
        EditingElement = editingElement;
    }

    public object? Item { get; }

    public int RowIndex { get; }

    public DataGridColumn Column { get; }

    public int ColumnIndex { get; }

    public UIElement EditingElement { get; }
}

public sealed class DataGridCellEditEndingEventArgs : EventArgs
{
    public DataGridCellEditEndingEventArgs(object? item, int rowIndex, DataGridColumn column, int columnIndex, UIElement editingElement, DataGridEditAction editAction)
    {
        Item = item;
        RowIndex = rowIndex;
        Column = column;
        ColumnIndex = columnIndex;
        EditingElement = editingElement;
        EditAction = editAction;
    }

    public object? Item { get; }

    public int RowIndex { get; }

    public DataGridColumn Column { get; }

    public int ColumnIndex { get; }

    public UIElement EditingElement { get; }

    public DataGridEditAction EditAction { get; }

    public bool Cancel { get; set; }
}

public sealed class DataGridRowEditEndingEventArgs : EventArgs
{
    public DataGridRowEditEndingEventArgs(object? item, int rowIndex, DataGridEditAction editAction)
    {
        Item = item;
        RowIndex = rowIndex;
        EditAction = editAction;
    }

    public object? Item { get; }

    public int RowIndex { get; }

    public DataGridEditAction EditAction { get; }

    public bool Cancel { get; set; }
}
