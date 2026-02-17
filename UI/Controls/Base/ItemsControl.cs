using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
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

    private readonly ObservableCollection<object> _items = new();
    private readonly List<GeneratedItemContainer> _generatedChildren = new();
    private readonly List<UIElement> _itemContainers = new();
    private UIElement? _activeItemsHost;
    private bool _suspendRegeneration;

    public ItemsControl()
    {
        _items.CollectionChanged += OnItemsCollectionChanged;
    }

    public ObservableCollection<object> Items => _items;

    public void AddItems(IEnumerable<object> items)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        var addedCount = 0;
        _suspendRegeneration = true;
        try
        {
            foreach (var item in items)
            {
                _items.Add(item);
                addedCount++;
            }
        }
        finally
        {
            _suspendRegeneration = false;
        }

        RegenerateChildren();
        UiFrameworkFileLoadDiagnostics.Observe(
            $"{GetType().Name}.AddItems",
            Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
            addedCount);
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

    protected virtual bool IncludeGeneratedChildrenInVisualTree => _activeItemsHost == null;

    protected IReadOnlyList<UIElement> ItemContainers
    {
        get
        {
            return _itemContainers;
        }
    }

    internal IReadOnlyList<UIElement> GetItemContainersForPresenter()
    {
        return ItemContainers;
    }

    internal void AttachItemsHost(UIElement host)
    {
        if (ReferenceEquals(_activeItemsHost, host))
        {
            return;
        }

        _activeItemsHost = host;
        ReparentGeneratedChildren(host);
        InvalidateMeasure();
    }

    internal void DetachItemsHost(UIElement host)
    {
        if (!ReferenceEquals(_activeItemsHost, host))
        {
            return;
        }

        _activeItemsHost = null;
        ReparentGeneratedChildren(this);
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

        foreach (var generated in _generatedChildren)
        {
            yield return generated.Container;
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

        foreach (var generated in _generatedChildren)
        {
            yield return generated.Container;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);

        if (!IncludeGeneratedChildrenInVisualTree)
        {
            return desired;
        }

        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            if (child is not FrameworkElement element)
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

        var currentY = LayoutSlot.Y;
        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
            currentY += height;
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
    }

    protected virtual void ClearContainerForItemOverride(UIElement element, object item)
    {
    }

    protected virtual void OnItemsChanged()
    {
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        if (args.Property == ItemTemplateProperty || args.Property == ItemTemplateSelectorProperty)
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

    private void RegenerateChildren()
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        var phaseStart = diagnosticsStart;
        if (_suspendRegeneration)
        {
            return;
        }

        foreach (var entry in _generatedChildren)
        {
            var child = entry.Container;
            ClearContainerForItemOverride(child, entry.Item);
            DetachFromCurrentParent(child);
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.Regenerate.ClearExisting",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            _generatedChildren.Count);

        phaseStart = Stopwatch.GetTimestamp();
        _generatedChildren.Clear();
        _itemContainers.Clear();
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.Regenerate.ClearLists",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var element = BuildContainerForItem(item);
            if (element == null)
            {
                continue;
            }

            PrepareContainerForItemOverride(element, item, i);
            var host = _activeItemsHost ?? this;
            AttachToHost(host, element, i);
            _generatedChildren.Add(new GeneratedItemContainer(item, element));
            _itemContainers.Add(element);
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.Regenerate.BuildPrepareAttach",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            _items.Count);

        phaseStart = Stopwatch.GetTimestamp();
        OnItemsChanged();
        (_activeItemsHost as FrameworkElement)?.InvalidateMeasure();
        InvalidateMeasure();
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.Regenerate.ItemsChangedInvalidate",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
        UiFrameworkFileLoadDiagnostics.Observe(
            $"{GetType().Name}.RegenerateChildren",
            Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
            _generatedChildren.Count);
    }

    private void ReparentGeneratedChildren(UIElement newParent)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < _itemContainers.Count; i++)
        {
            var child = _itemContainers[i];
            if (ReferenceEquals(child.VisualParent, newParent) && ReferenceEquals(child.LogicalParent, newParent))
            {
                continue;
            }

            DetachFromCurrentParent(child);
            AttachToHost(newParent, child, i);
        }

        InvalidateMeasure();
        UiFrameworkFileLoadDiagnostics.Observe(
            $"{GetType().Name}.ReparentGeneratedChildren",
            Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
            _itemContainers.Count);
    }

    private UIElement? BuildContainerForItem(object item)
    {
        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            item,
            ItemTemplate,
            ItemTemplateSelector,
            this);

        if (selectedTemplate != null)
        {
            return selectedTemplate.Build(item, this);
        }

        if (IsItemItsOwnContainerOverride(item) && item is UIElement uiElement)
        {
            return uiElement;
        }

        return CreateContainerForItemOverride(item);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var diagnosticsStart = Stopwatch.GetTimestamp();
        var changedCount = Math.Max(e.NewItems?.Count ?? 0, e.OldItems?.Count ?? 0);
        if (_suspendRegeneration)
        {
            return;
        }

        // Keep behavior equivalent to the old implementation (which rebuilt everything),
        // but do it incrementally to avoid per-add hitches for large item collections.
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertNewContainers(e);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveOldContainers(e);
                break;
            case NotifyCollectionChangedAction.Replace:
                ReplaceContainers(e);
                break;
            case NotifyCollectionChangedAction.Move:
                MoveContainers(e);
                break;
            case NotifyCollectionChangedAction.Reset:
                RegenerateChildren();
                UiFrameworkFileLoadDiagnostics.Observe(
                    $"{GetType().Name}.ItemsChanged.Reset",
                    Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
                    _items.Count);
                return;
            default:
                RegenerateChildren();
                UiFrameworkFileLoadDiagnostics.Observe(
                    $"{GetType().Name}.ItemsChanged.Other",
                    Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
                    changedCount);
                return;
        }

        OnItemsChanged();
        (_activeItemsHost as FrameworkElement)?.InvalidateMeasure();
        InvalidateMeasure();
        UiFrameworkFileLoadDiagnostics.Observe(
            $"{GetType().Name}.ItemsChanged.{e.Action}",
            Stopwatch.GetElapsedTime(diagnosticsStart).TotalMilliseconds,
            changedCount);
    }

    private void InsertNewContainers(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null || e.NewItems.Count == 0)
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        var insertIndex = e.NewStartingIndex < 0 ? _generatedChildren.Count : e.NewStartingIndex;
        insertIndex = Math.Clamp(insertIndex, 0, _generatedChildren.Count);

        var host = _activeItemsHost ?? this;
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsAdd.ResolveInsertPoint",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < e.NewItems.Count; i++)
        {
            var item = e.NewItems[i]!;
            var element = BuildContainerForItem(item);
            if (element == null)
            {
                continue;
            }

            var index = insertIndex + i;
            PrepareContainerForItemOverride(element, item, index);
            AttachToHost(host, element, index);

            _generatedChildren.Insert(index, new GeneratedItemContainer(item, element));
            _itemContainers.Insert(index, element);
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsAdd.BuildPrepareAttachInsert",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            e.NewItems.Count);

        phaseStart = Stopwatch.GetTimestamp();
        RefreshContainerPreparationFrom(insertIndex);
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsAdd.RefreshPreparation",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            _generatedChildren.Count - insertIndex);
    }

    private void RemoveOldContainers(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems == null || e.OldItems.Count == 0)
        {
            return;
        }

        var phaseStart = Stopwatch.GetTimestamp();
        var index = e.OldStartingIndex < 0 ? _generatedChildren.Count - 1 : e.OldStartingIndex;
        index = Math.Clamp(index, 0, Math.Max(0, _generatedChildren.Count - 1));

        var host = _activeItemsHost ?? this;
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsRemove.ResolveIndex",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);

        phaseStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < e.OldItems.Count; i++)
        {
            if (index < 0 || index >= _generatedChildren.Count)
            {
                break;
            }

            var entry = _generatedChildren[index];
            ClearContainerForItemOverride(entry.Container, entry.Item);
            DetachFromHost(host, entry.Container, index);

            _generatedChildren.RemoveAt(index);
            _itemContainers.RemoveAt(index);
        }
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsRemove.ClearDetachRemove",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds,
            e.OldItems.Count);

        phaseStart = Stopwatch.GetTimestamp();
        RefreshContainerPreparationFrom(index);
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{GetType().Name}.ItemsRemove.RefreshPreparation",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
    }

    private void ReplaceContainers(NotifyCollectionChangedEventArgs e)
    {
        var index = e.OldStartingIndex >= 0 ? e.OldStartingIndex : e.NewStartingIndex;
        if (index < 0)
        {
            RegenerateChildren();
            return;
        }

        // Remove old at index, then insert new at same position.
        RemoveOldContainers(e);
        InsertNewContainers(e);
        RefreshContainerPreparationFrom(index);
    }

    private void MoveContainers(NotifyCollectionChangedEventArgs e)
    {
        // ObservableCollection move is typically single-item; handle generically for Count.
        if (e.OldItems == null || e.OldItems.Count == 0)
        {
            return;
        }

        if (e.OldStartingIndex < 0 || e.NewStartingIndex < 0)
        {
            RegenerateChildren();
            return;
        }

        var count = e.OldItems.Count;
        var oldIndex = e.OldStartingIndex;
        var newIndex = e.NewStartingIndex;
        var originalNewIndex = newIndex;

        if (oldIndex == newIndex)
        {
            return;
        }

        oldIndex = Math.Clamp(oldIndex, 0, _generatedChildren.Count - 1);
        newIndex = Math.Clamp(newIndex, 0, _generatedChildren.Count - 1);

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

        (_activeItemsHost as Panel)?.MoveChildRange(oldIndex, movedEntries.Count, originalNewIndex);

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
        var phaseStart = Stopwatch.GetTimestamp();
        if (host is Panel panel)
        {
            panel.InsertChild(index, child);
            UiFrameworkPopulationPhaseDiagnostics.Observe(
                $"{host.GetType().Name}.AttachToHost",
                Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
            return;
        }

        child.SetVisualParent(host);
        child.SetLogicalParent(host);
        UiFrameworkPopulationPhaseDiagnostics.Observe(
            $"{host.GetType().Name}.AttachToHost",
            Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds);
    }

    private static void DetachFromHost(UIElement host, UIElement child, int index)
    {
        if (host is Panel panel)
        {
            // Best-effort index removal first, falling back to instance removal.
            if (!panel.RemoveChildAt(index))
            {
                panel.RemoveChild(child);
            }

            return;
        }

        child.SetVisualParent(null);
        child.SetLogicalParent(null);
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
}
