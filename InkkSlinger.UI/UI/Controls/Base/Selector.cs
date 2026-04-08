using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(
            nameof(SelectedValue),
            typeof(object),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Selector selector)
                    {
                        selector.OnSelectedValuePropertyChanged(args.NewValue);
                    }
                }));

    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register(
            nameof(SelectedValuePath),
            typeof(string),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is Selector selector)
                    {
                        selector.SyncSelectedValueFromModel();
                    }
                }));

    public static readonly DependencyProperty IsSynchronizedWithCurrentItemProperty =
        DependencyProperty.Register(
            nameof(IsSynchronizedWithCurrentItem),
            typeof(bool),
            typeof(Selector),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.None,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is Selector selector && args.NewValue is bool isEnabled)
                    {
                        selector.OnIsSynchronizedWithCurrentItemChanged(isEnabled);
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
    private IReadOnlyList<object> _selectedItems = Array.Empty<object>();
    private bool _isSynchronizingSelection;
    private bool _isSynchronizingSelectedValue;
    private bool _isSynchronizingCurrentItem;
    private ICollectionView? _observedItemsSourceView;

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

    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public string SelectedValuePath
    {
        get => GetValue<string>(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    public bool IsSynchronizedWithCurrentItem
    {
        get => GetValue<bool>(IsSynchronizedWithCurrentItemProperty);
        set => SetValue(IsSynchronizedWithCurrentItemProperty, value);
    }

    public SelectionMode SelectionMode
    {
        get => GetValue<SelectionMode>(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public IReadOnlyList<int> SelectedIndices => _selectionModel.SelectedIndices;

    public IReadOnlyList<object> SelectedItems => _selectedItems;

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        UpdateObservedItemsSourceViewSubscription();
        if (ItemsSourceView != null)
        {
            var projectedItems = new List<object>();
            foreach (var item in ItemsSourceView)
            {
                projectedItems.Add(item!);
            }

            _selectionModel.ReplaceItems(projectedItems);
        }
        else
        {
            _selectionModel.ReplaceItems(Items);
        }

        if (ShouldPullSelectionFromCurrentItem())
        {
            PullSelectionFromCurrentItem();
        }

        SyncSelectionPropertiesFromModel();
    }

    protected override void OnItemsIncrementalChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSourceView != null &&
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add &&
            e.NewItems != null &&
            e.NewItems.Count > 0)
        {
            var insertIndex = e.NewStartingIndex < 0 ? _selectionModel.Count : e.NewStartingIndex;
            insertIndex = Math.Clamp(insertIndex, 0, _selectionModel.Count);
            _selectionModel.InsertItems(insertIndex, e.NewItems);
            SyncSelectionPropertiesFromModel();
            return;
        }

        OnItemsChanged();
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs args)
    {
    }

    protected void SetSelectedIndexInternal(int index)
    {
        _selectionModel.SelectIndex(index);
    }

    protected void SelectOnlyIndexInternal(int index)
    {
        _selectionModel.SelectOnlyIndex(index);
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

    protected void SelectAllInternal()
    {
        _selectionModel.SelectAll();
    }

    protected void ClearSelectionInternal()
    {
        _selectionModel.Clear();
    }

    protected void ReplaceSelectionItemsInternal(IEnumerable<object?> items)
    {
        _selectionModel.ReplaceItems(items.Select(item => item!));
        SyncSelectionPropertiesFromModel();
    }

    protected bool IsSelectedIndexInternal(int index)
    {
        return _selectionModel.IsSelected(index);
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

        _selectionModel.SelectOnlyItem(selectedItem);
    }

    private void OnSelectedValuePropertyChanged(object? selectedValue)
    {
        if (_isSynchronizingSelectedValue)
        {
            return;
        }

        if (selectedValue == null)
        {
            _selectionModel.Clear();
            return;
        }

        var selectedIndex = FindIndexBySelectedValue(selectedValue);
        if (selectedIndex >= 0)
        {
            _selectionModel.SelectOnlyIndex(selectedIndex);
        }
    }

    private void OnIsSynchronizedWithCurrentItemChanged(bool isEnabled)
    {
        if (!isEnabled)
        {
            return;
        }

        if (ShouldPullSelectionFromCurrentItem())
        {
            PullSelectionFromCurrentItem();
            return;
        }

        PushCurrentItemFromSelection();
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

        _selectedItems = new ReadOnlyCollection<object>(new List<object>(_selectionModel.SelectedItems));
        SyncSelectedValueFromModel();
        PushCurrentItemFromSelection();
    }

    private void SyncSelectedValueFromModel()
    {
        _isSynchronizingSelectedValue = true;
        try
        {
            SelectedValue = ResolveSelectedValue(SelectedItem);
        }
        finally
        {
            _isSynchronizingSelectedValue = false;
        }
    }

    private object? ResolveSelectedValue(object? selectedItem)
    {
        if (selectedItem == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(SelectedValuePath))
        {
            return selectedItem;
        }

        return BindingExpressionUtilities.ResolvePathValue(selectedItem, SelectedValuePath);
    }

    private int FindIndexBySelectedValue(object? selectedValue)
    {
        if (selectedValue == null)
        {
            return -1;
        }

        var projectedItems = ItemsSourceView != null
            ? EnumerateProjectedItems()
            : new List<object?>(Items);
        for (var i = 0; i < projectedItems.Count; i++)
        {
            var candidateValue = string.IsNullOrWhiteSpace(SelectedValuePath)
                ? projectedItems[i]
                : BindingExpressionUtilities.ResolvePathValue(projectedItems[i]!, SelectedValuePath);
            if (Equals(candidateValue, selectedValue))
            {
                return i;
            }
        }

        return -1;
    }

    private List<object?> EnumerateProjectedItems()
    {
        var items = new List<object?>();
        if (ItemsSourceView == null)
        {
            return items;
        }

        foreach (var item in ItemsSourceView)
        {
            items.Add(item);
        }

        return items;
    }

    private void UpdateObservedItemsSourceViewSubscription()
    {
        if (ReferenceEquals(_observedItemsSourceView, ItemsSourceView))
        {
            return;
        }

        if (_observedItemsSourceView != null)
        {
            _observedItemsSourceView.CurrentChanged -= OnItemsSourceViewCurrentChanged;
        }

        _observedItemsSourceView = ItemsSourceView;
        if (_observedItemsSourceView != null)
        {
            _observedItemsSourceView.CurrentChanged += OnItemsSourceViewCurrentChanged;
        }
    }

    private void OnItemsSourceViewCurrentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        if (!ShouldPullSelectionFromCurrentItem())
        {
            return;
        }

        PullSelectionFromCurrentItem();
    }

    private bool ShouldPullSelectionFromCurrentItem()
    {
        return IsSynchronizedWithCurrentItem &&
               !_isSynchronizingCurrentItem &&
               ItemsSourceView != null &&
               SelectionMode == SelectionMode.Single;
    }

    private void PullSelectionFromCurrentItem()
    {
        if (ItemsSourceView == null)
        {
            return;
        }

        _isSynchronizingCurrentItem = true;
        try
        {
            if (ItemsSourceView.CurrentItem == null)
            {
                _selectionModel.Clear();
            }
            else
            {
                _selectionModel.SelectOnlyItem(ItemsSourceView.CurrentItem);
            }
        }
        finally
        {
            _isSynchronizingCurrentItem = false;
        }
    }

    private void PushCurrentItemFromSelection()
    {
        if (!IsSynchronizedWithCurrentItem ||
            _isSynchronizingCurrentItem ||
            ItemsSourceView == null ||
            SelectionMode != SelectionMode.Single)
        {
            return;
        }

        _isSynchronizingCurrentItem = true;
        try
        {
            if (SelectedItem == null)
            {
                _ = ItemsSourceView.MoveCurrentToPosition(-1);
            }
            else
            {
                _ = ItemsSourceView.MoveCurrentTo(SelectedItem);
            }
        }
        finally
        {
            _isSynchronizingCurrentItem = false;
        }
    }
}
