namespace InkkSlinger;

public enum DataGridSelectionUnit
{
    FullRow,
    Cell
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

public enum DataGridEditTriggerSource
{
    None,
    Keyboard,
    Pointer,
    TextInput
}
