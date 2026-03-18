using System;
using System.Collections.Generic;

namespace InkkSlinger;

public enum AutomationControlType
{
    Custom,
    Pane,
    Window,
    Button,
    CheckBox,
    RadioButton,
    ComboBox,
    Edit,
    Password,
    List,
    ListItem,
    Tree,
    TreeItem,
    Menu,
    MenuBar,
    MenuItem,
    Tab,
    TabItem,
    ScrollBar,
    Slider,
    ProgressBar,
    Text,
    Image,
    Calendar,
    DataGrid,
    Group,
    Header,
    Separator,
    ToolBar,
    ToolTip,
    Document
}

public enum AutomationPatternType
{
    Invoke,
    Value,
    RangeValue,
    Selection,
    SelectionItem,
    Grid,
    GridItem,
    Table,
    TableItem,
    ExpandCollapse,
    Scroll
}

public enum RowOrColumnMajor
{
    RowMajor,
    ColumnMajor,
    Indeterminate
}

public enum AutomationEventType
{
    FocusChanged,
    StructureChanged,
    PropertyChanged,
    Invoke,
    ValueChanged,
    SelectionChanged,
    ExpandCollapseStateChanged,
    ScrollChanged
}

public enum ExpandCollapseState
{
    Collapsed,
    Expanded,
    LeafNode,
    PartiallyExpanded
}

public sealed class AutomationPropertyChangedEventArgs : EventArgs
{
    public AutomationPropertyChangedEventArgs(string propertyName, object? oldValue, object? newValue)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public string PropertyName { get; }

    public object? OldValue { get; }

    public object? NewValue { get; }
}

public sealed class AutomationEventArgs : EventArgs
{
    public AutomationEventArgs(
        AutomationEventType eventType,
        AutomationPeer peer,
        string? propertyName = null,
        object? oldValue = null,
        object? newValue = null,
        AutomationPeer? oldPeer = null,
        AutomationPeer? newPeer = null)
    {
        EventType = eventType;
        Peer = peer;
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
        OldPeer = oldPeer;
        NewPeer = newPeer;
    }

    public AutomationEventType EventType { get; }

    public AutomationPeer Peer { get; }

    public string? PropertyName { get; }

    public object? OldValue { get; }

    public object? NewValue { get; }

    public AutomationPeer? OldPeer { get; }

    public AutomationPeer? NewPeer { get; }
}

public readonly record struct AutomationEventRecord(
    AutomationEventType EventType,
    int PeerRuntimeId,
    string? PropertyName,
    object? OldValue,
    object? NewValue,
    int? OldPeerRuntimeId,
    int? NewPeerRuntimeId);

public readonly record struct AutomationMetricsSnapshot(
    int PeerCount,
    int TreeRebuildCount,
    int EmittedEventCountLastFrame,
    int CoalescedEventDiscardCountLastFrame);

public interface IInvokeProvider
{
    void Invoke();
}

public interface IValueProvider
{
    bool IsReadOnly { get; }

    string Value { get; }

    void SetValue(string value);
}

public interface IRangeValueProvider
{
    bool IsReadOnly { get; }

    float Minimum { get; }

    float Maximum { get; }

    float Value { get; }

    void SetValue(float value);
}

public interface ISelectionProvider
{
    bool CanSelectMultiple { get; }

    bool IsSelectionRequired { get; }

    IReadOnlyList<AutomationPeer> GetSelection();
}

public interface ISelectionItemProvider
{
    bool IsSelected { get; }

    AutomationPeer? SelectionContainer { get; }

    void Select();

    void AddToSelection();

    void RemoveFromSelection();
}

public interface IGridProvider
{
    int RowCount { get; }

    int ColumnCount { get; }

    AutomationPeer? GetItem(int row, int column);
}

public interface IGridItemProvider
{
    int Row { get; }

    int Column { get; }

    int RowSpan { get; }

    int ColumnSpan { get; }

    AutomationPeer? ContainingGrid { get; }
}

public interface ITableProvider : IGridProvider
{
    RowOrColumnMajor RowOrColumnMajor { get; }

    IReadOnlyList<AutomationPeer> GetColumnHeaders();

    IReadOnlyList<AutomationPeer> GetRowHeaders();
}

public interface ITableItemProvider : IGridItemProvider
{
    IReadOnlyList<AutomationPeer> GetColumnHeaderItems();

    IReadOnlyList<AutomationPeer> GetRowHeaderItems();
}

public interface IExpandCollapseProvider
{
    ExpandCollapseState ExpandCollapseState { get; }

    void Expand();

    void Collapse();
}

public interface IScrollProvider
{
    bool HorizontallyScrollable { get; }

    bool VerticallyScrollable { get; }

    float HorizontalScrollPercent { get; }

    float VerticalScrollPercent { get; }

    void SetScrollPercent(float horizontalPercent, float verticalPercent);
}
