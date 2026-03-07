using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ItemsControl : Control
{
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemContainerStyleProperty =
        DependencyProperty.Register(
            nameof(ItemContainerStyle),
            typeof(Style),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(object),
            typeof(ItemsControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ItemsControl itemsControl)
                    {
                        itemsControl.OnItemsSourceChanged(args.OldValue, args.NewValue);
                    }
                }));

    private readonly ItemCollection _items;
    private readonly List<GeneratedItemContainer> _generatedChildren = [];
    private readonly List<UIElement> _itemContainers = [];
    private readonly List<UIElement> _groupContainers = [];
    private readonly ObservableCollection<GroupStyle> _groupStyle = [];
    private readonly Dictionary<Type, DataTemplate?> _implicitTemplateCache = new();
    private readonly Dictionary<UIElement, Style?> _appliedItemContainerStyles = new();
    private UIElement? _activeItemsHost;
    private ICollectionView? _itemsSourceView;
    private CollectionViewSource? _itemsSourceReference;
    private bool _suspendRegeneration;
    private bool _isApplyingGroupProjection;
    private bool _skipNextGroupsRegeneration;

    public ItemsControl()
    {
        _items = new ItemCollection(this);
        _items.CollectionChanged += OnItemsCollectionChanged;
        _groupStyle.CollectionChanged += OnGroupStyleChanged;
    }

    public ObservableCollection<object> Items => _items;

    public object? ItemsSource
    {
        get => GetValue<object>(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => GetValue<DataTemplate>(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public DataTemplateSelector? ItemTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    public Style? ItemContainerStyle
    {
        get => GetValue<Style>(ItemContainerStyleProperty);
        set => SetValue(ItemContainerStyleProperty, value);
    }

    public ObservableCollection<GroupStyle> GroupStyle => _groupStyle;

    protected ICollectionView? ItemsSourceView => _itemsSourceView;

    protected virtual bool IncludeGeneratedChildrenInVisualTree => _activeItemsHost == null;

    protected virtual bool SupportsGroupedVisualProjection => this is not Selector and not DataGrid;

    protected virtual bool CanReconcileProjectedContainersOnReset => true;

    protected IReadOnlyList<UIElement> ItemContainers => _itemContainers;

    internal IReadOnlyList<UIElement> GetItemContainersForPresenter()
    {
        if (IsGroupedVisualProjectionActive)
        {
            return _groupContainers;
        }

        return ItemContainers;
    }

    public void AddItems(IEnumerable<object> items)
    {
        _suspendRegeneration = true;
        try
        {
            foreach (var item in items)
            {
                _items.Add(item);
            }
        }
        finally
        {
            _suspendRegeneration = false;
        }

        RegenerateChildren();
    }

    internal void AttachItemsHost(UIElement host)
    {
        if (ReferenceEquals(_activeItemsHost, host))
        {
            return;
        }

        _activeItemsHost = host;
        ReparentRealizedChildren(host);
        InvalidateMeasure();
    }

    internal void DetachItemsHost(UIElement host)
    {
        if (!ReferenceEquals(_activeItemsHost, host))
        {
            return;
        }

        _activeItemsHost = null;
        ReparentRealizedChildren(this);
        InvalidateMeasure();
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            yield break;
        }

        var source = IsGroupedVisualProjectionActive ? _groupContainers : _itemContainers;
        for (var i = 0; i < source.Count; i++)
        {
            yield return source[i];
        }
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            yield break;
        }

        var source = IsGroupedVisualProjectionActive ? _groupContainers : _itemContainers;
        for (var i = 0; i < source.Count; i++)
        {
            yield return source[i];
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        if (!IncludeGeneratedChildrenInVisualTree)
        {
            return desired;
        }

        var source = IsGroupedVisualProjectionActive ? _groupContainers : _itemContainers;
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i] is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(availableSize);
            desired.X = MathF.Max(desired.X, element.DesiredSize.X);
            desired.Y += element.DesiredSize.Y;
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);
        if (!IncludeGeneratedChildrenInVisualTree)
        {
            return finalSize;
        }

        var source = IsGroupedVisualProjectionActive ? _groupContainers : _itemContainers;
        var y = LayoutSlot.Y;
        for (var i = 0; i < source.Count; i++)
        {
            if (source[i] is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(LayoutSlot.X, y, finalSize.X, height));
            y += height;
        }

        return finalSize;
    }

    protected virtual bool IsItemItsOwnContainerOverride(object item)
    {
        return item is UIElement;
    }

    protected virtual UIElement CreateContainerForItemOverride(object item)
    {
        return new Label
        {
            Text = item?.ToString() ?? string.Empty
        };
    }

    protected virtual void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        ApplyItemContainerStyle(element);
    }

    protected virtual void ClearContainerForItemOverride(UIElement element, object item)
    {
        RemoveItemContainerStyleTracking(element);
    }

    protected virtual void OnItemsChanged()
    {
    }

    protected virtual void OnItemsResetReconciled()
    {
        OnItemsChanged();
    }

    protected virtual bool ShouldInvalidateMeasureOnItemsResetReconciled => true;

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ItemTemplateProperty || args.Property == ItemTemplateSelectorProperty)
        {
            _implicitTemplateCache.Clear();
            RegenerateChildren();
            return;
        }

        if (args.Property == ItemContainerStyleProperty)
        {
            RegenerateChildren();
        }
    }

    protected int IndexFromContainer(UIElement element)
    {
        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            if (ReferenceEquals(_generatedChildren[i].Container, element))
            {
                return i;
            }
        }

        return -1;
    }

    protected object? ItemFromContainer(UIElement element)
    {
        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            if (ReferenceEquals(_generatedChildren[i].Container, element))
            {
                return _generatedChildren[i].Item;
            }
        }

        return null;
    }

    internal bool IsItemsSourceBound()
    {
        return ItemsSource != null;
    }

    internal bool TryGetGeneratedItemInfo(UIElement container, out object? item, out int index)
    {
        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            if (!ReferenceEquals(_generatedChildren[i].Container, container))
            {
                continue;
            }

            item = _generatedChildren[i].Item;
            index = i;
            return true;
        }

        item = null;
        index = -1;
        return false;
    }

    internal bool TryGetGeneratedItemByIndex(int index, out object? item)
    {
        if (index < 0 || index >= _generatedChildren.Count)
        {
            item = null;
            return false;
        }

        item = _generatedChildren[index].Item;
        return true;
    }

    private bool IsGroupedVisualProjectionActive =>
        SupportsGroupedVisualProjection &&
        _itemsSourceView != null &&
        _itemsSourceView.Groups.Count > 0 &&
        _groupStyle.Count > 0 &&
        _groupContainers.Count > 0;

    private void RegenerateChildren()
    {
        if (_suspendRegeneration)
        {
            return;
        }

        for (var i = 0; i < _groupContainers.Count; i++)
        {
            DetachFromCurrentParent(_groupContainers[i]);
        }

        for (var i = 0; i < _generatedChildren.Count; i++)
        {
            var entry = _generatedChildren[i];
            ClearContainerForItemOverride(entry.Container, entry.Item);
            DetachFromCurrentParent(entry.Container);
        }

        _groupContainers.Clear();
        _generatedChildren.Clear();
        _itemContainers.Clear();
        _appliedItemContainerStyles.Clear();

        var sourceItems = GetProjectedItems();
        for (var i = 0; i < sourceItems.Count; i++)
        {
            var item = sourceItems[i];
            var element = BuildContainerForItem(item);
            if (element == null)
            {
                continue;
            }

            PrepareContainerForItemOverride(element, item!, i);
            _generatedChildren.Add(new GeneratedItemContainer(item!, element));
            _itemContainers.Add(element);
        }

        if (ShouldBuildGroupedProjection())
        {
            BuildGroupedContainers();
        }
        else
        {
            var host = _activeItemsHost ?? this;
            for (var i = 0; i < _itemContainers.Count; i++)
            {
                AttachToHost(host, _itemContainers[i], i);
            }
        }

        OnItemsChanged();
        (_activeItemsHost as FrameworkElement)?.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void BuildGroupedContainers()
    {
        if (_itemsSourceView == null || _itemsSourceView.Groups.Count == 0)
        {
            return;
        }

        var host = _activeItemsHost ?? this;
        for (var i = 0; i < _itemsSourceView.Groups.Count; i++)
        {
            var container = BuildGroupContainer(_itemsSourceView.Groups[i], depth: 0);
            _groupContainers.Add(container);
            AttachToHost(host, container, i);
        }
    }

    private GroupItem BuildGroupContainer(CollectionViewGroup group, int depth)
    {
        var style = _groupStyle.Count > 0 ? _groupStyle[Math.Min(depth, _groupStyle.Count - 1)] : _groupStyle[0];
        var headerText = style.HeaderStringFormat != null
            ? string.Format(style.HeaderStringFormat, group.Name, group.ItemCount)
            : $"{group.Name} ({group.ItemCount})";
        var groupItem = new GroupItem
        {
            Header = headerText,
            HeaderTemplate = style.HeaderTemplate
        };

        _isApplyingGroupProjection = true;
        try
        {
            if (group.Subgroups.Count > 0)
            {
                for (var i = 0; i < group.Subgroups.Count; i++)
                {
                    groupItem.Items.Add(BuildGroupContainer(group.Subgroups[i], depth + 1));
                }
            }
            else
            {
                for (var i = 0; i < group.Items.Count; i++)
                {
                    var item = group.Items[i];
                    if (item is UIElement uiElement)
                    {
                        groupItem.Items.Add(uiElement);
                        continue;
                    }

                    groupItem.Items.Add(item ?? string.Empty);
                }
            }
        }
        finally
        {
            _isApplyingGroupProjection = false;
        }

        return groupItem;
    }

    private void ReparentRealizedChildren(UIElement newParent)
    {
        var source = IsGroupedVisualProjectionActive ? _groupContainers : _itemContainers;
        for (var i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i].VisualParent, newParent) && ReferenceEquals(source[i].LogicalParent, newParent))
            {
                continue;
            }

            DetachFromCurrentParent(source[i]);
            AttachToHost(newParent, source[i], i);
        }

        InvalidateMeasure();
    }

    private UIElement? BuildContainerForItem(object? item)
    {
        DataTemplate? selectedTemplate;
        if (ItemTemplate != null || ItemTemplateSelector != null || item == null)
        {
            selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
                this,
                item,
                ItemTemplate,
                ItemTemplateSelector,
                this);
        }
        else
        {
            var itemType = item.GetType();
            if (!_implicitTemplateCache.TryGetValue(itemType, out selectedTemplate))
            {
                selectedTemplate = DataTemplateResolver.ResolveImplicitTemplate(this, item);
                _implicitTemplateCache[itemType] = selectedTemplate;
            }
        }

        if (selectedTemplate != null)
        {
            return selectedTemplate.Build(item, this);
        }

        if (item != null && IsItemItsOwnContainerOverride(item) && item is UIElement uiElement)
        {
            return uiElement;
        }

        return CreateContainerForItemOverride(item ?? string.Empty);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        if (_suspendRegeneration)
        {
            return;
        }

        if (_itemsSourceView != null || ShouldBuildGroupedProjection())
        {
            RegenerateChildren();
            return;
        }

        if (!TryApplyIncrementalItemsChange(e))
        {
            RegenerateChildren();
            return;
        }

        FinalizeItemsIncrementalChange(e);
    }

    private void InsertNewContainers(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null || e.NewItems.Count == 0)
        {
            return;
        }

        var insertIndex = e.NewStartingIndex < 0 ? _generatedChildren.Count : e.NewStartingIndex;
        insertIndex = Math.Clamp(insertIndex, 0, _generatedChildren.Count);
        var host = _activeItemsHost ?? this;

        for (var i = 0; i < e.NewItems.Count; i++)
        {
            var item = e.NewItems[i];
            var element = BuildContainerForItem(item);
            if (element == null)
            {
                continue;
            }

            var index = insertIndex + i;
            PrepareContainerForItemOverride(element, item!, index);
            AttachToHost(host, element, index);
            _generatedChildren.Insert(index, new GeneratedItemContainer(item!, element));
            _itemContainers.Insert(index, element);
        }

        RefreshContainerPreparationFrom(insertIndex);
    }

    private void RemoveOldContainers(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems == null || e.OldItems.Count == 0 || _generatedChildren.Count == 0)
        {
            return;
        }

        var index = e.OldStartingIndex < 0 ? _generatedChildren.Count - 1 : e.OldStartingIndex;
        index = Math.Clamp(index, 0, Math.Max(0, _generatedChildren.Count - 1));

        for (var i = 0; i < e.OldItems.Count; i++)
        {
            if (index < 0 || index >= _generatedChildren.Count)
            {
                break;
            }

            var entry = _generatedChildren[index];
            ClearContainerForItemOverride(entry.Container, entry.Item);
            DetachFromCurrentParent(entry.Container);
            _generatedChildren.RemoveAt(index);
            _itemContainers.RemoveAt(index);
        }

        RefreshContainerPreparationFrom(index);
    }

    private void ReplaceContainers(NotifyCollectionChangedEventArgs e)
    {
        var index = e.OldStartingIndex >= 0 ? e.OldStartingIndex : e.NewStartingIndex;
        if (index < 0)
        {
            RegenerateChildren();
            return;
        }

        RemoveOldContainers(e);
        InsertNewContainers(e);
        RefreshContainerPreparationFrom(index);
    }

    private void MoveContainers(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems == null || e.OldItems.Count == 0 || e.OldStartingIndex < 0 || e.NewStartingIndex < 0)
        {
            RegenerateChildren();
            return;
        }

        if (e.OldStartingIndex == e.NewStartingIndex)
        {
            return;
        }

        var count = e.OldItems.Count;
        var oldIndex = Math.Clamp(e.OldStartingIndex, 0, _generatedChildren.Count - 1);
        var newIndex = Math.Clamp(e.NewStartingIndex, 0, _generatedChildren.Count - 1);
        var movedCount = Math.Min(count, _generatedChildren.Count - oldIndex);
        var movedEntries = _generatedChildren.GetRange(oldIndex, movedCount);
        var movedContainers = _itemContainers.GetRange(oldIndex, movedCount);

        _generatedChildren.RemoveRange(oldIndex, movedEntries.Count);
        _itemContainers.RemoveRange(oldIndex, movedContainers.Count);

        if (newIndex > oldIndex)
        {
            newIndex = Math.Max(0, newIndex - movedEntries.Count);
        }

        _generatedChildren.InsertRange(newIndex, movedEntries);
        _itemContainers.InsertRange(newIndex, movedContainers);
        (_activeItemsHost as Panel)?.MoveChildRange(oldIndex, movedEntries.Count, e.NewStartingIndex);
        RefreshContainerPreparationFrom(Math.Min(oldIndex, newIndex));
    }

    private void RefreshContainerPreparationFrom(int startIndex)
    {
        if (_generatedChildren.Count == 0)
        {
            return;
        }

        var start = Math.Clamp(startIndex, 0, _generatedChildren.Count - 1);
        for (var i = start; i < _generatedChildren.Count; i++)
        {
            var entry = _generatedChildren[i];
            PrepareContainerForItemOverride(entry.Container, entry.Item, i);
        }
    }

    private IReadOnlyList<object?> GetProjectedItems()
    {
        if (_itemsSourceView == null)
        {
            var ownItems = new List<object?>(_items.Count);
            for (var i = 0; i < _items.Count; i++)
            {
                ownItems.Add(_items[i]);
            }

            return ownItems;
        }

        var projected = new List<object?>();
        foreach (var item in _itemsSourceView)
        {
            projected.Add(item);
        }

        return projected;
    }

    private bool ShouldBuildGroupedProjection()
    {
        return SupportsGroupedVisualProjection &&
               _itemsSourceView != null &&
               _itemsSourceView.Groups.Count > 0 &&
               _groupStyle.Count > 0;
    }

    private void OnItemsSourceChanged(object? oldValue, object? newValue)
    {
        _ = oldValue;
        _implicitTemplateCache.Clear();
        DetachItemsSourceReference();
        DetachItemsSourceView();
        _skipNextGroupsRegeneration = false;

        if (newValue is CollectionViewSource collectionViewSource)
        {
            _itemsSourceReference = collectionViewSource;
            _itemsSourceReference.PropertyChanged += OnItemsSourceReferencePropertyChanged;
            _itemsSourceView = collectionViewSource.View;
        }
        else
        {
            _itemsSourceView = CollectionViewFactory.GetDefaultView(newValue);
        }

        AttachItemsSourceView();
        RegenerateChildren();
    }

    private void OnItemsSourceReferencePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (!string.Equals(e.PropertyName, nameof(CollectionViewSource.View), StringComparison.Ordinal))
        {
            return;
        }

        _implicitTemplateCache.Clear();
        DetachItemsSourceView();
        _itemsSourceView = _itemsSourceReference?.View;
        AttachItemsSourceView();
        _skipNextGroupsRegeneration = false;
        RegenerateChildren();
    }

    private void AttachItemsSourceView()
    {
        if (_itemsSourceView == null)
        {
            return;
        }

        _itemsSourceView.CollectionChanged += OnItemsSourceViewChanged;
        _itemsSourceView.PropertyChanged += OnItemsSourceViewPropertyChanged;
    }

    private void DetachItemsSourceView()
    {
        if (_itemsSourceView == null)
        {
            return;
        }

        _itemsSourceView.CollectionChanged -= OnItemsSourceViewChanged;
        _itemsSourceView.PropertyChanged -= OnItemsSourceViewPropertyChanged;
        _itemsSourceView = null;
        _skipNextGroupsRegeneration = false;
    }

    private void OnItemsSourceViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        var grouped = ShouldBuildGroupedProjection();

        if (!grouped &&
            e.Action != NotifyCollectionChangedAction.Reset)
        {
            var applied = TryApplyIncrementalItemsChange(e);

            if (applied)
            {
                _skipNextGroupsRegeneration = true;
                FinalizeItemsIncrementalChange(e);
                return;
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset &&
            CanReconcileProjectedContainersOnReset &&
            !grouped)
        {
            var reconciled = TryReconcileProjectedContainers();

            if (reconciled)
            {
                _skipNextGroupsRegeneration = true;
                return;
            }
        }

        _skipNextGroupsRegeneration = true;
        RegenerateChildren();
    }

    private bool TryApplyIncrementalItemsChange(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertNewContainers(e);
                return true;
            case NotifyCollectionChangedAction.Remove:
                RemoveOldContainers(e);
                return true;
            case NotifyCollectionChangedAction.Replace:
                ReplaceContainers(e);
                return true;
            case NotifyCollectionChangedAction.Move:
                MoveContainers(e);
                return true;
            default:
                return false;
        }
    }

    private void FinalizeItemsIncrementalChange(NotifyCollectionChangedEventArgs e)
    {
        RefreshIncrementalBindingSources(e);
        OnItemsIncrementalChanged(e);
        InvalidateMeasure();
    }

    private void RefreshIncrementalBindingSources(NotifyCollectionChangedEventArgs e)
    {
        if (_generatedChildren.Count == 0)
        {
            return;
        }

        var startIndex = e.Action switch
        {
            NotifyCollectionChangedAction.Add => e.NewStartingIndex < 0 ? _generatedChildren.Count - 1 : e.NewStartingIndex,
            NotifyCollectionChangedAction.Remove => e.OldStartingIndex < 0 ? 0 : e.OldStartingIndex,
            NotifyCollectionChangedAction.Replace => Math.Min(
                e.OldStartingIndex < 0 ? 0 : e.OldStartingIndex,
                e.NewStartingIndex < 0 ? 0 : e.NewStartingIndex),
            NotifyCollectionChangedAction.Move => Math.Min(e.OldStartingIndex, e.NewStartingIndex),
            _ => 0
        };

        startIndex = Math.Clamp(startIndex, 0, _generatedChildren.Count - 1);
        for (var i = startIndex; i < _generatedChildren.Count; i++)
        {
            BindingOperations.NotifyTargetTreeChangedRecursive(_generatedChildren[i].Container);
        }
    }

    protected virtual void OnItemsIncrementalChanged(NotifyCollectionChangedEventArgs e)
    {
        _ = e;
        OnItemsChanged();
    }

    private void OnItemsSourceViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = sender;
        if (string.Equals(e.PropertyName, nameof(ICollectionView.Groups), StringComparison.Ordinal))
        {
            if (_skipNextGroupsRegeneration)
            {
                _skipNextGroupsRegeneration = false;
                return;
            }

            RegenerateChildren();
        }
    }

    private void OnGroupStyleChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RegenerateChildren();
    }

    private void DetachItemsSourceReference()
    {
        if (_itemsSourceReference == null)
        {
            return;
        }

        _itemsSourceReference.PropertyChanged -= OnItemsSourceReferencePropertyChanged;
        _itemsSourceReference = null;
    }

    private readonly struct GeneratedItemContainer
    {
        public GeneratedItemContainer(object item, UIElement container)
        {
            Item = item;
            Container = container;
        }

        public object Item { get; }

        public UIElement Container { get; }
    }

    private static void AttachToHost(UIElement host, UIElement child, int index)
    {
        if (host is Panel panel)
        {
            panel.InsertChild(index, child);
            return;
        }

        child.SetVisualParent(host);
        child.SetLogicalParent(host);
    }

    private static void DetachFromCurrentParent(UIElement child)
    {
        if (child.VisualParent is Panel panel)
        {
            panel.RemoveChild(child);
            return;
        }

        child.SetVisualParent(null);
        child.SetLogicalParent(null);
    }

    private bool TryReconcileProjectedContainers()
    {
        if (_suspendRegeneration || _itemsSourceView == null)
        {
            return false;
        }

        var projectedItems = GetProjectedItems();
        var host = _activeItemsHost ?? this;
        var oldEntries = new List<GeneratedItemContainer>(_generatedChildren);
        var matchedOldEntries = new bool[oldEntries.Count];
        var nextEntries = new List<GeneratedItemContainer>(projectedItems.Count);
        var nextContainers = new List<UIElement>(projectedItems.Count);

        for (var i = 0; i < projectedItems.Count; i++)
        {
            var item = projectedItems[i];
            var matchIndex = FindUnmatchedEntry(oldEntries, matchedOldEntries, item);
            if (matchIndex >= 0)
            {
                var reused = oldEntries[matchIndex];
                matchedOldEntries[matchIndex] = true;
                nextEntries.Add(new GeneratedItemContainer(item!, reused.Container));
                nextContainers.Add(reused.Container);
                continue;
            }

            var created = BuildContainerForItem(item);
            if (created == null)
            {
                continue;
            }

            nextEntries.Add(new GeneratedItemContainer(item!, created));
            nextContainers.Add(created);
        }

        for (var i = 0; i < oldEntries.Count; i++)
        {
            if (matchedOldEntries[i])
            {
                continue;
            }

            var removed = oldEntries[i];
            ClearContainerForItemOverride(removed.Container, removed.Item);
            DetachFromCurrentParent(removed.Container);
        }

        if (host is Panel panel)
        {
            for (var i = 0; i < nextContainers.Count; i++)
            {
                var child = nextContainers[i];
                if (ReferenceEquals(child.VisualParent, panel))
                {
                    var currentIndex = IndexOfPanelChild(panel, child);
                    if (currentIndex >= 0 && currentIndex != i)
                    {
                        panel.MoveChildRange(currentIndex, 1, i);
                    }

                    continue;
                }

                AttachToHost(panel, child, i);
            }
        }
        else
        {
            for (var i = 0; i < nextContainers.Count; i++)
            {
                var child = nextContainers[i];
                if (!ReferenceEquals(child.VisualParent, host))
                {
                    AttachToHost(host, child, i);
                }
            }
        }

        _generatedChildren.Clear();
        _generatedChildren.AddRange(nextEntries);
        _itemContainers.Clear();
        _itemContainers.AddRange(nextContainers);
        _groupContainers.Clear();
        _appliedItemContainerStyles.Clear();

        RefreshContainerPreparationFrom(0);
        OnItemsResetReconciled();
        if (ShouldInvalidateMeasureOnItemsResetReconciled)
        {
            (_activeItemsHost as FrameworkElement)?.InvalidateMeasure();
            InvalidateMeasure();
        }
        else
        {
            (_activeItemsHost as FrameworkElement)?.InvalidateArrange();
            InvalidateArrange();
            InvalidateVisual();
        }
        return true;
    }

    private void ApplyItemContainerStyle(UIElement element)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        if (ItemContainerStyle == null)
        {
            if (_appliedItemContainerStyles.TryGetValue(element, out var trackedStyle))
            {
                if (frameworkElement.GetValueSource(FrameworkElement.StyleProperty) == DependencyPropertyValueSource.Local &&
                    ReferenceEquals(frameworkElement.Style, trackedStyle))
                {
                    frameworkElement.ClearValue(FrameworkElement.StyleProperty);
                }

                _appliedItemContainerStyles.Remove(element);
            }

            return;
        }

        if (_appliedItemContainerStyles.TryGetValue(element, out var previouslyAppliedStyle))
        {
            if (frameworkElement.GetValueSource(FrameworkElement.StyleProperty) == DependencyPropertyValueSource.Local &&
                !ReferenceEquals(frameworkElement.Style, previouslyAppliedStyle))
            {
                _appliedItemContainerStyles.Remove(element);
                return;
            }
        }
        else if (frameworkElement.GetValueSource(FrameworkElement.StyleProperty) == DependencyPropertyValueSource.Local)
        {
            return;
        }

        if (!ReferenceEquals(frameworkElement.Style, ItemContainerStyle))
        {
            frameworkElement.Style = ItemContainerStyle;
        }

        _appliedItemContainerStyles[element] = ItemContainerStyle;
    }

    private void RemoveItemContainerStyleTracking(UIElement element)
    {
        if (element is FrameworkElement frameworkElement &&
            _appliedItemContainerStyles.TryGetValue(element, out var trackedStyle))
        {
            if (frameworkElement.GetValueSource(FrameworkElement.StyleProperty) == DependencyPropertyValueSource.Local &&
                ReferenceEquals(frameworkElement.Style, trackedStyle))
            {
                frameworkElement.ClearValue(FrameworkElement.StyleProperty);
            }
        }

        _appliedItemContainerStyles.Remove(element);
    }

    private static int FindUnmatchedEntry(
        IReadOnlyList<GeneratedItemContainer> entries,
        IReadOnlyList<bool> matchedEntries,
        object? item)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (matchedEntries[i])
            {
                continue;
            }

            if (ItemsMatchForContainerReuse(entries[i].Item, item))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ItemsMatchForContainerReuse(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        var leftType = left.GetType();
        var rightType = right.GetType();
        if (!leftType.IsValueType && !rightType.IsValueType)
        {
            return false;
        }

        return Equals(left, right);
    }

    private static int IndexOfPanelChild(Panel panel, UIElement child)
    {
        var children = panel.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], child))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class ItemCollection : ObservableCollection<object>
    {
        private readonly ItemsControl _owner;

        public ItemCollection(ItemsControl owner)
        {
            _owner = owner;
        }

        protected override void InsertItem(int index, object item)
        {
            ThrowIfItemsSourceBound();
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, object item)
        {
            ThrowIfItemsSourceBound();
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ThrowIfItemsSourceBound();
            base.RemoveItem(index);
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            ThrowIfItemsSourceBound();
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void ClearItems()
        {
            ThrowIfItemsSourceBound();
            base.ClearItems();
        }

        private void ThrowIfItemsSourceBound()
        {
            if (_owner._isApplyingGroupProjection)
            {
                return;
            }

            if (_owner.IsItemsSourceBound())
            {
                throw new InvalidOperationException(
                    "Cannot modify Items when ItemsSource is set. Clear ItemsSource first.");
            }
        }
    }
}
