namespace InkkSlinger;

public enum DataGridSelectionMode
{
    Single,
    Extended
}

public enum DataGridSelectionUnit
{
    FullRow,
    Cell,
    CellOrRowHeader
}

public enum DataGridSortDirection
{
    None,
    Ascending,
    Descending
}

public enum DataGridGridLinesVisibility
{
    None,
    Horizontal,
    Vertical,
    All
}

public enum DataGridClipboardCopyMode
{
    None,
    ExcludeHeader,
    IncludeHeader
}

public enum DataGridHeadersVisibility
{
    None,
    Column,
    Row,
    All
}

public enum DataGridRowDetailsVisibilityMode
{
    Collapsed,
    Visible,
    VisibleWhenSelected
}

public enum DataGridEditAction
{
    Commit,
    Cancel
}

public enum DataGridEditingUnit
{
    Cell,
    Row
}

public enum DataGridEditTriggerSource
{
    None,
    Keyboard,
    Pointer,
    TextInput
}
