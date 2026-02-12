using System.Collections.Generic;

namespace InkkSlinger;

public class Selector : ItemsControl
{
    public static readonly RoutedEvent SelectionChangedEvent =
        new(nameof(SelectionChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                -1,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Selector selector && args.NewValue is int selectedIndex)
                    {
                        selector.OnSelectedIndexPropertyChanged(selectedIndex);
                    }
                }));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Selector selector)
                    {
                        selector.OnSelectedItemPropertyChanged(args.NewValue);
                    }
                }));

    public static readonly DependencyProperty SelectionModeProperty =
        DependencyProperty.Register(
            nameof(SelectionMode),
            typeof(SelectionMode),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                SelectionMode.Single,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Selector selector && args.NewValue is SelectionMode mode)
                    {
                        selector._selectionModel.Mode = mode;
                    }
                }));

    private readonly SelectionModel _selectionModel = new();
    private bool _isSynchronizingSelection;

    protected Selector()
    {
        _selectionModel.Changed += OnSelectionModelChanged;
    }

    public event System.EventHandler<SelectionChangedEventArgs> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public int SelectedIndex
    {
        get => GetValue<int>(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => GetValue<SelectionMode>(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public IReadOnlyList<int> SelectedIndices => _selectionModel.SelectedIndices;

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        _selectionModel.ReplaceItems(Items);
        SyncSelectionPropertiesFromModel();
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs args)
    {
    }

    protected void SetSelectedIndexInternal(int index)
    {
        _selectionModel.SelectIndex(index);
    }

    protected void ToggleSelectedIndexInternal(int index)
    {
        _selectionModel.ToggleIndex(index);
    }

    protected void SelectRangeInternal(int anchorIndex, int targetIndex, bool clearExisting)
    {
        _selectionModel.SelectRange(anchorIndex, targetIndex, clearExisting);
    }

    protected void SetSelectionAnchorInternal(int index)
    {
        _selectionModel.SetAnchorIndex(index);
    }

    protected int GetSelectionAnchorIndexInternal()
    {
        return _selectionModel.AnchorIndex;
    }

    private void OnSelectedIndexPropertyChanged(int selectedIndex)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _selectionModel.SelectIndex(selectedIndex);
    }

    private void OnSelectedItemPropertyChanged(object? selectedItem)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _selectionModel.SelectItem(selectedItem);
    }

    private void OnSelectionModelChanged(object? sender, SelectionModelChangedEventArgs args)
    {
        SyncSelectionPropertiesFromModel();

        var routedArgs = new SelectionChangedEventArgs(
            SelectionChangedEvent,
            args.RemovedItems,
            args.AddedItems);
        RaiseRoutedEvent(SelectionChangedEvent, routedArgs);
        OnSelectionChanged(routedArgs);
    }

    private void SyncSelectionPropertiesFromModel()
    {
        _isSynchronizingSelection = true;
        try
        {
            SelectedIndex = _selectionModel.SelectedIndex;
            SelectedItem = _selectionModel.SelectedItem;
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }
}
