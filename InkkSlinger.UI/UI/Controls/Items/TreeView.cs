using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
public class TreeView : ItemsControl
{
    public static readonly RoutedEvent SelectedItemChangedEvent =
        new(nameof(SelectedItemChanged), RoutingStrategy.Bubble);

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(TreeViewItem),
            typeof(TreeView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(TreeView),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(TreeView),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(TreeView),
            new FrameworkPropertyMetadata(new Color(18, 18, 18), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(TreeView),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(TreeView),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TreeView),
            new FrameworkPropertyMetadata(
                Color.White,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is TreeView treeView &&
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        treeView.PropagateTypographyFromTree(
                            oldColor,
                            newColor);
                    }
                }));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(TreeView),
            new FrameworkPropertyMetadata(new Thickness(0f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    private readonly ScrollViewer _fallbackScrollViewer;
    private readonly HashSet<TreeViewItem> _trackedTreeItems = new();
    private readonly List<VisibleTreeDataEntry> _hierarchicalDataRows = new();
    private readonly Dictionary<object, TreeViewItem> _hierarchicalDataContainers = new();
    private readonly Dictionary<object, bool> _hierarchicalExpansionOverrides = new();
    private ScrollViewer? _templatedScrollViewer;
    private ScrollViewer? _viewportSubscribedScrollViewer;
    private Panel _itemsHost;
    private object? _hierarchicalItemsSource;
    private Func<object, IEnumerable<object>>? _hierarchicalChildrenSelector;
    private Func<object, bool>? _hierarchicalHasChildrenSelector;
    private Func<object, string>? _hierarchicalHeaderSelector;
    private Func<object, bool>? _hierarchicalExpandedSelector;

    public TreeView()
    {
        _itemsHost = CreateItemsHost();
        AttachItemsHost(_itemsHost);

        _fallbackScrollViewer = new ScrollViewer
        {
            Content = _itemsHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = 0f,
            Background = Color.Transparent
        };

        _fallbackScrollViewer.SetVisualParent(this);
        _fallbackScrollViewer.SetLogicalParent(this);
        UpdateScrollViewerViewportSubscription(_fallbackScrollViewer);

        AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, OnMouseLeftButtonDownSelectItem);
    }

    public object? HierarchicalItemsSource
    {
        get => _hierarchicalItemsSource;
        set
        {
            if (ReferenceEquals(_hierarchicalItemsSource, value))
            {
                return;
            }

            _hierarchicalItemsSource = value;
            _hierarchicalExpansionOverrides.Clear();
            _hierarchicalDataContainers.Clear();
            RefreshHierarchicalDataMode();
        }
    }

    public Func<object, IEnumerable<object>>? HierarchicalChildrenSelector
    {
        get => _hierarchicalChildrenSelector;
        set
        {
            _hierarchicalChildrenSelector = value;
            RefreshHierarchicalDataMode();
        }
    }

    public Func<object, bool>? HierarchicalHasChildrenSelector
    {
        get => _hierarchicalHasChildrenSelector;
        set
        {
            _hierarchicalHasChildrenSelector = value;
            RefreshHierarchicalDataMode();
        }
    }

    public Func<object, string>? HierarchicalHeaderSelector
    {
        get => _hierarchicalHeaderSelector;
        set
        {
            _hierarchicalHeaderSelector = value;
            RefreshHierarchicalDataMode();
        }
    }

    public Func<object, bool>? HierarchicalExpandedSelector
    {
        get => _hierarchicalExpandedSelector;
        set
        {
            _hierarchicalExpandedSelector = value;
            RefreshHierarchicalDataMode();
        }
    }

    public int RealizedHierarchicalContainerCount => _hierarchicalDataContainers.Count;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _templatedScrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        if (_templatedScrollViewer == null)
        {
            RestoreFallbackScrollViewer();
            return;
        }

        DetachFallbackScrollViewer();
        ConfigureScrollViewer(_templatedScrollViewer);
        AttachItemsHostToActiveScrollViewer();
    }

    public event System.EventHandler<RoutedSimpleEventArgs> SelectedItemChanged
    {
        add => AddHandler(SelectedItemChangedEvent, value);
        remove => RemoveHandler(SelectedItemChangedEvent, value);
    }

    public TreeViewItem? SelectedItem
    {
        get => GetValue<TreeViewItem>(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public void SelectItem(TreeViewItem item)
    {
        ApplySelectedItem(item);
    }

    public bool SelectHierarchicalItem(object item)
    {
        if (!IsHierarchicalDataMode)
        {
            return false;
        }

        var rowIndex = _hierarchicalDataRows.FindIndex(row => ReferenceEquals(row.Item, item) || Equals(row.Item, item));
        if (rowIndex < 0)
        {
            RefreshHierarchicalDataRows();
            rowIndex = _hierarchicalDataRows.FindIndex(row => ReferenceEquals(row.Item, item) || Equals(row.Item, item));
            if (rowIndex < 0)
            {
                return false;
            }
        }

        ApplySelectedItem(RealizeHierarchicalDataContainer(_hierarchicalDataRows[rowIndex], rowIndex));
        return true;
    }

    public bool SetHierarchicalItemExpanded(object item, bool isExpanded)
    {
        if (!IsHierarchicalDataMode)
        {
            return false;
        }

        _hierarchicalExpansionOverrides[item] = isExpanded;
        RefreshHierarchicalDataRows();
        return true;
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        if (HasTemplateRoot)
        {
            yield break;
        }

        yield return _fallbackScrollViewer;
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() + (HasTemplateRoot ? 0 : 1);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        if (!HasTemplateRoot && index == baseCount)
        {
            return _fallbackScrollViewer;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        if (HasTemplateRoot)
        {
            yield break;
        }

        yield return _fallbackScrollViewer;
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is TreeViewItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        return new TreeViewItem
        {
            Header = item?.ToString() ?? string.Empty
        };
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is TreeViewItem treeViewItem)
        {
            ApplyTypographyToItem(treeViewItem, null, Foreground);
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == HorizontalScrollBarVisibilityProperty && args.NewValue is ScrollBarVisibility h)
        {
            ActiveScrollViewer.HorizontalScrollBarVisibility = h;
        }
        else if (args.Property == VerticalScrollBarVisibilityProperty && args.NewValue is ScrollBarVisibility v)
        {
            ActiveScrollViewer.VerticalScrollBarVisibility = v;
        }
        else if (args.Property == ItemsPanelProperty)
        {
            UpdateItemsHost();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var templateDesired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return templateDesired;
        }

        var padding = Padding;
        var border = BorderThickness;
        var innerSize = new Vector2(
            MathF.Max(0f, availableSize.X - (border * 2f) - padding.Horizontal),
            MathF.Max(0f, availableSize.Y - (border * 2f) - padding.Vertical));

        var scrollViewer = ActiveScrollViewer;
        scrollViewer.Measure(innerSize);
        var desired = scrollViewer.DesiredSize;
        return new Vector2(
            desired.X + (border * 2f) + padding.Horizontal,
            desired.Y + (border * 2f) + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (HasTemplateRoot)
        {
            return base.ArrangeOverride(finalSize);
        }

        var border = BorderThickness;
        var padding = Padding;
        var innerX = LayoutSlot.X + border + padding.Left;
        var innerY = LayoutSlot.Y + border + padding.Top;
        var innerWidth = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);
        var innerHeight = MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical);

        ActiveScrollViewer.Arrange(new LayoutRect(innerX, innerY, innerWidth, innerHeight));

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        if (HasTemplateRoot)
        {
            return;
        }

        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        if (HasTemplateRoot)
        {
            return base.TryGetClipRect(out clipRect);
        }

        clipRect = LayoutSlot;
        return true;
    }

    private ScrollViewer ActiveScrollViewer => _templatedScrollViewer ?? _fallbackScrollViewer;

    private bool IsHierarchicalDataMode => _hierarchicalItemsSource != null && _hierarchicalChildrenSelector != null;

    private void RefreshHierarchicalDataMode()
    {
        var needsDataHost = IsHierarchicalDataMode;
        if (needsDataHost != (_itemsHost is VirtualizingTreeDataHost))
        {
            UpdateItemsHost();
            return;
        }

        if (needsDataHost)
        {
            RefreshHierarchicalDataRows();
        }
    }

    private IEnumerable<object> GetHierarchicalRootItems()
    {
        if (_hierarchicalItemsSource is IEnumerable<object> enumerable)
        {
            return enumerable;
        }

        if (_hierarchicalItemsSource is System.Collections.IEnumerable nonGeneric && _hierarchicalItemsSource is not string)
        {
            return nonGeneric.Cast<object>();
        }

        return _hierarchicalItemsSource != null
            ? new[] { _hierarchicalItemsSource }
            : Array.Empty<object>();
    }

    private void RefreshHierarchicalDataRows()
    {
        if (_itemsHost is not VirtualizingTreeDataHost dataHost || _hierarchicalChildrenSelector == null)
        {
            return;
        }

        _hierarchicalDataRows.Clear();
        foreach (var item in GetHierarchicalRootItems())
        {
            AddHierarchicalDataRow(item, depth: 0);
        }

        dataHost.SetRows(_hierarchicalDataRows);
    }

    private void AddHierarchicalDataRow(object item, int depth)
    {
        var hasChildren = HasHierarchicalChildren(item);
        var expanded = hasChildren && IsHierarchicalItemExpanded(item);
        _hierarchicalDataRows.Add(new VisibleTreeDataEntry(item, depth, hasChildren, expanded));

        if (!expanded)
        {
            return;
        }

        var children = GetHierarchicalChildren(item);
        foreach (var child in children)
        {
            AddHierarchicalDataRow(child, depth + 1);
        }
    }

    private bool HasHierarchicalChildren(object item)
    {
        if (_hierarchicalHasChildrenSelector != null)
        {
            return _hierarchicalHasChildrenSelector(item);
        }

        return GetHierarchicalChildren(item).Count > 0;
    }

    private IReadOnlyList<object> GetHierarchicalChildren(object item)
    {
        if (_hierarchicalChildrenSelector?.Invoke(item) is not { } children)
        {
            return Array.Empty<object>();
        }

        return children as IReadOnlyList<object> ?? children.ToArray();
    }

    private bool IsHierarchicalItemExpanded(object item)
    {
        if (_hierarchicalExpansionOverrides.TryGetValue(item, out var expanded))
        {
            return expanded;
        }

        return _hierarchicalExpandedSelector?.Invoke(item) ?? false;
    }

    private string GetHierarchicalHeader(object item)
    {
        return _hierarchicalHeaderSelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
    }

    private TreeViewItem RealizeHierarchicalDataContainer(VisibleTreeDataEntry row, int rowIndex)
    {
        if (!_hierarchicalDataContainers.TryGetValue(row.Item, out var container))
        {
            container = new TreeViewItem();
            _hierarchicalDataContainers[row.Item] = container;
            ApplyTypographyToItem(container, null, Foreground);
        }

        container.Header = GetHierarchicalHeader(row.Item);
        container.Tag = row.Item;
        container.IsExpanded = row.IsExpanded;
        container.UseVirtualizedTreeLayout = true;
        container.VirtualizedTreeDepth = row.Depth;
        container.VirtualizedTreeRowIndex = rowIndex;
        container.HasVirtualizedChildItems = row.HasChildren;
        PrepareContainerForItemOverride(container, row.Item, rowIndex);
        return container;
    }

    private TreeViewItem? FindItemFromSource(UIElement? source)
    {
        for (var current = source; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TreeViewItem item)
            {
                return item;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return null;
    }

    private void OnMouseLeftButtonDownSelectItem(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        var clickedItem = FindItemFromSource(args.OriginalSource as UIElement);
        if (clickedItem == null)
        {
            return;
        }

        if (clickedItem.HitExpander(GetExpanderHitTestPoint(clickedItem, args.Position)))
        {
            if (IsHierarchicalDataMode && clickedItem.Tag != null)
            {
                _hierarchicalExpansionOverrides[clickedItem.Tag] = !clickedItem.IsExpanded;
                RefreshHierarchicalDataRows();
            }
            else
            {
                clickedItem.IsExpanded = !clickedItem.IsExpanded;
                RefreshVirtualizedItemsHost();
            }
        }

        ApplySelectedItem(clickedItem);
        args.Handled = true;
    }

    private Vector2 GetExpanderHitTestPoint(TreeViewItem item, Vector2 pointerPosition)
    {
        if (item.HitExpander(pointerPosition))
        {
            return pointerPosition;
        }

        var scrollViewer = ActiveScrollViewer;
        if (scrollViewer.Content is not UIElement content ||
            content is not IScrollTransformContent ||
            !ScrollViewer.GetUseTransformContentScrolling(content) ||
            !IsDescendantOrSelf(content, item))
        {
            return pointerPosition;
        }

        return new Vector2(
            pointerPosition.X + scrollViewer.HorizontalOffset,
            pointerPosition.Y + scrollViewer.VerticalOffset);
    }

    private static bool IsDescendantOrSelf(UIElement ancestor, UIElement candidate)
    {
        for (UIElement? current = candidate; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplySelectedItem(TreeViewItem item)
    {
        if (ReferenceEquals(item, SelectedItem))
        {
            return;
        }

        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = false;
        }

        SelectedItem = item;
        SelectedItem.IsSelected = true;
        RaiseRoutedEvent(SelectedItemChangedEvent, new RoutedSimpleEventArgs(SelectedItemChangedEvent));
    }

    private Panel CreateItemsHost()
    {
        if (IsHierarchicalDataMode)
        {
            return new VirtualizingTreeDataHost(this);
        }

        if (ItemsPanel != null)
        {
            return ItemsPanel.Build(this);
        }

        return new VirtualizingTreeItemsHost
        {
            Orientation = Orientation.Vertical,
            CacheLength = 2f
        };
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
            return;
        }

        RefreshTreeItemTracking();
        RefreshVirtualizedItemsHost();
    }

    private void ConfigureScrollViewer(ScrollViewer viewer)
    {
        viewer.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        viewer.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        UpdateScrollViewerViewportSubscription(viewer);
    }

    private void UpdateScrollViewerViewportSubscription(ScrollViewer viewer)
    {
        if (ReferenceEquals(_viewportSubscribedScrollViewer, viewer))
        {
            return;
        }

        if (_viewportSubscribedScrollViewer != null)
        {
            _viewportSubscribedScrollViewer.ViewportChanged -= OnActiveScrollViewerViewportChanged;
        }

        _viewportSubscribedScrollViewer = viewer;
        _viewportSubscribedScrollViewer.ViewportChanged += OnActiveScrollViewerViewportChanged;
    }

    private void OnActiveScrollViewerViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            dataHost.InvalidateMeasure();
            dataHost.InvalidateArrange();
        }
    }

    private void UpdateItemsHost()
    {
        var nextHost = CreateItemsHost();
        if (ReferenceEquals(nextHost, _itemsHost))
        {
            return;
        }

        _itemsHost = nextHost;
        ActiveScrollViewer.Content = _itemsHost;
        AttachItemsHost(_itemsHost);
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
        }
        else
        {
            RefreshTreeItemTracking();
            RefreshVirtualizedItemsHost();
        }
        InvalidateMeasure();
    }

    private void AttachItemsHostToActiveScrollViewer()
    {
        if (!ReferenceEquals(_fallbackScrollViewer, ActiveScrollViewer) && ReferenceEquals(_fallbackScrollViewer.Content, _itemsHost))
        {
            _fallbackScrollViewer.Content = null;
        }

        if (_templatedScrollViewer != null &&
            !ReferenceEquals(_templatedScrollViewer, ActiveScrollViewer) &&
            ReferenceEquals(_templatedScrollViewer.Content, _itemsHost))
        {
            _templatedScrollViewer.Content = null;
        }

        if (!ReferenceEquals(ActiveScrollViewer.Content, _itemsHost))
        {
            ActiveScrollViewer.Content = _itemsHost;
        }

        AttachItemsHost(_itemsHost);
        if (IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
        }
        else
        {
            RefreshTreeItemTracking();
            RefreshVirtualizedItemsHost();
        }
    }

    private void RestoreFallbackScrollViewer()
    {
        _templatedScrollViewer = null;
        if (!ReferenceEquals(_fallbackScrollViewer.VisualParent, this))
        {
            _fallbackScrollViewer.SetVisualParent(this);
            _fallbackScrollViewer.SetLogicalParent(this);
        }

        ConfigureScrollViewer(_fallbackScrollViewer);
        AttachItemsHostToActiveScrollViewer();
    }

    private void DetachFallbackScrollViewer()
    {
        if (ReferenceEquals(_fallbackScrollViewer.Content, _itemsHost))
        {
            _fallbackScrollViewer.Content = null;
        }

        _fallbackScrollViewer.SetVisualParent(null);
        _fallbackScrollViewer.SetLogicalParent(null);
    }

    private void OnTrackedTreeItemExpandedStateChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshVirtualizedItemsHost();
    }

    private void RefreshTreeItemTracking()
    {
        var current = EnumerateAllTreeItems().ToHashSet();
        foreach (var removed in _trackedTreeItems.Where(item => !current.Contains(item)).ToArray())
        {
            removed.ExpandedStateChanged -= OnTrackedTreeItemExpandedStateChanged;
            removed.UseVirtualizedTreeLayout = false;
            removed.VirtualizedTreeDepth = 0;
            _trackedTreeItems.Remove(removed);
        }

        foreach (var item in current)
        {
            if (_trackedTreeItems.Add(item))
            {
                item.ExpandedStateChanged += OnTrackedTreeItemExpandedStateChanged;
            }
        }
    }

    private IEnumerable<TreeViewItem> EnumerateAllTreeItems()
    {
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem root)
            {
                continue;
            }

            foreach (var item in EnumerateAllTreeItems(root))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<TreeViewItem> EnumerateAllTreeItems(TreeViewItem item)
    {
        yield return item;
        foreach (var child in item.GetChildTreeItems())
        {
            foreach (var descendant in EnumerateAllTreeItems(child))
            {
                yield return descendant;
            }
        }
    }

    private void RefreshVirtualizedItemsHost()
    {
        if (_itemsHost is not VirtualizingTreeItemsHost virtualizingHost)
        {
            foreach (var item in _trackedTreeItems)
            {
                item.UseVirtualizedTreeLayout = false;
                item.VirtualizedTreeDepth = 0;
            }

            return;
        }

        var visibleItems = GetVisibleItemEntries();
        var visibleSet = new HashSet<TreeViewItem>();
        foreach (var entry in visibleItems)
        {
            entry.Item.UseVirtualizedTreeLayout = true;
            entry.Item.VirtualizedTreeDepth = entry.Depth;
            visibleSet.Add(entry.Item);
        }

        foreach (var item in _trackedTreeItems)
        {
            if (visibleSet.Contains(item))
            {
                continue;
            }

            item.UseVirtualizedTreeLayout = true;
            item.VirtualizedTreeDepth = 0;
            DetachVirtualizedTreeItem(item);
        }

        virtualizingHost.SetVisibleItems(visibleItems);
    }

    private void DetachVirtualizedTreeItem(TreeViewItem item)
    {
        if (ReferenceEquals(item.VisualParent, _itemsHost))
        {
            return;
        }

        if (item.VisualParent is Panel visualPanel)
        {
            visualPanel.RemoveChild(item);
        }
        else if (item.VisualParent != null)
        {
            item.SetVisualParent(null);
        }

        item.InvalidateMeasure();
        item.InvalidateArrange();
    }

    private List<TreeViewItem> GetVisibleItems()
    {
        return GetVisibleItemEntries().Select(static entry => entry.Item).ToList();
    }

    private List<VisibleTreeItemEntry> GetVisibleItemEntries()
    {
        var result = new List<VisibleTreeItemEntry>();
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem root)
            {
                continue;
            }

            AddVisible(root, depth: 0, result);
        }

        return result;
    }

    private static void AddVisible(TreeViewItem item, int depth, IList<VisibleTreeItemEntry> output)
    {
        output.Add(new VisibleTreeItemEntry(item, depth));

        if (!item.IsExpanded)
        {
            return;
        }

        foreach (var childItem in item.GetChildTreeItems())
        {
            AddVisible(childItem, depth + 1, output);
        }
    }

    private static TreeViewItem? GetFirstChild(TreeViewItem item)
    {
        foreach (var childItem in item.GetChildTreeItems())
        {
            return childItem;
        }

        return null;
    }

    private static TreeViewItem? GetParentTreeItem(TreeViewItem item)
    {
        for (var current = item.VisualParent ?? item.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TreeViewItem parent)
            {
                return parent;
            }
        }

        return null;
    }

    private void PropagateTypographyFromTree(
        Color? oldForeground,
        Color? newForeground)
    {
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem item)
            {
                continue;
            }

            ApplyTypographyRecursive(item, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyRecursive(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        ApplyTypographyToItem(item, oldForeground, newForeground);
        foreach (var child in item.GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyToItem(
        TreeViewItem item,
        Color? oldForeground,
        Color? newForeground)
    {
        item.ApplyPropagatedForeground(oldForeground, newForeground);
    }

    private sealed class ScrollContentStackPanel : StackPanel, IScrollTransformContent
    {
    }

    private readonly record struct VisibleTreeItemEntry(TreeViewItem Item, int Depth);

    private readonly record struct VisibleTreeDataEntry(object Item, int Depth, bool HasChildren, bool IsExpanded);

    private sealed class VirtualizingTreeDataHost : Panel, IScrollTransformContent, IScrollViewerVirtualizedContent
    {
        private const float FallbackRowHeight = 22f;
        private readonly TreeView _owner;
        private IReadOnlyList<VisibleTreeDataEntry> _rows = Array.Empty<VisibleTreeDataEntry>();
        private int _firstRealizedIndex = -1;
        private int _lastRealizedIndex = -1;

        public VirtualizingTreeDataHost(TreeView owner)
        {
            _owner = owner;
            Background = Color.Transparent;
        }

        public bool OwnsHorizontalScrollOffset => false;

        public bool OwnsVerticalScrollOffset => true;

        public void SetRows(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            _rows = rows;
            _firstRealizedIndex = -1;
            _lastRealizedIndex = -1;
            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            RealizeRows(availableSize.Y);
            var childConstraint = new Vector2(availableSize.X, FallbackRowHeight);
            foreach (var child in Children)
            {
                if (child is FrameworkElement element)
                {
                    element.Measure(childConstraint);
                }
            }

            var width = float.IsFinite(availableSize.X) ? availableSize.X : 0f;
            return new Vector2(width, _rows.Count * FallbackRowHeight);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            RealizeRows(finalSize.Y);
            foreach (var child in Children)
            {
                if (child is not TreeViewItem item || item.VirtualizedTreeRowIndex < 0)
                {
                    continue;
                }

                var index = item.VirtualizedTreeRowIndex;
                item.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + (index * FallbackRowHeight),
                    finalSize.X,
                    FallbackRowHeight));
            }

            return finalSize;
        }

        private void RealizeRows(float viewportHeight)
        {
            var viewer = _owner.ActiveScrollViewer;
            var offset = MathF.Max(0f, viewer.VerticalOffset);
            var viewport = float.IsFinite(viewportHeight) && viewportHeight > 0f
                ? viewportHeight
                : MathF.Max(viewer.ViewportHeight, FallbackRowHeight);
            var cacheRows = Math.Max(4, (int)MathF.Ceiling(viewport / FallbackRowHeight));
            var first = Math.Max(0, (int)MathF.Floor(offset / FallbackRowHeight) - cacheRows);
            var last = Math.Min(_rows.Count - 1, (int)MathF.Ceiling((offset + viewport) / FallbackRowHeight) + cacheRows);

            if (first == _firstRealizedIndex && last == _lastRealizedIndex)
            {
                return;
            }

            _firstRealizedIndex = first;
            _lastRealizedIndex = last;
            using (DeferChildMutationInvalidations())
            {
                for (var childIndex = Children.Count - 1; childIndex >= 0; childIndex--)
                {
                    var child = Children[childIndex];
                    if (child is TreeViewItem treeItem &&
                        treeItem.VirtualizedTreeRowIndex >= first &&
                        treeItem.VirtualizedTreeRowIndex <= last)
                    {
                        continue;
                    }

                    RemoveChildAt(childIndex);
                }

                for (var rowIndex = first; rowIndex <= last; rowIndex++)
                {
                    var row = _rows[rowIndex];
                    var container = _owner.RealizeHierarchicalDataContainer(row, rowIndex);
                    if (IndexOfChild(container) < 0)
                    {
                        InsertChild(Children.Count, container);
                    }
                }
            }
        }

        private int IndexOfChild(UIElement child)
        {
            for (var index = 0; index < Children.Count; index++)
            {
                if (ReferenceEquals(Children[index], child))
                {
                    return index;
                }
            }

            return -1;
        }
    }

    private sealed class VirtualizingTreeItemsHost : VirtualizingStackPanel
    {
        public override IEnumerable<UIElement> GetVisualChildren()
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                foreach (var child in Children)
                {
                    yield return child;
                }

                yield break;
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            for (var index = first; index <= last; index++)
            {
                yield return Children[index];
            }
        }

        internal override int GetVisualChildCountForTraversal()
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                return Children.Count;
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            return Math.Max(0, last - first + 1);
        }

        internal override UIElement GetVisualChildAtForTraversal(int index)
        {
            if (!IsVirtualizationActive || FirstRealizedIndex < 0 || LastRealizedIndex < FirstRealizedIndex)
            {
                if ((uint)index < (uint)Children.Count)
                {
                    return Children[index];
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var first = Math.Max(0, FirstRealizedIndex);
            var last = Math.Min(Children.Count - 1, LastRealizedIndex);
            var count = Math.Max(0, last - first + 1);
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Children[first + index];
        }

        public void SetVisibleItems(IReadOnlyList<VisibleTreeItemEntry> visibleItems)
        {
            var visibleSet = new HashSet<TreeViewItem>(visibleItems.Select(static entry => entry.Item));
            var changed = false;
            using (DeferChildMutationInvalidations())
            {
                for (var index = Children.Count - 1; index >= 0; index--)
                {
                    var child = Children[index];
                    if (child is TreeViewItem treeItem && visibleSet.Contains(treeItem))
                    {
                        continue;
                    }

                    changed |= RemoveChildAt(index);
                }

                if (Children.Count == 0)
                {
                    for (var index = 0; index < visibleItems.Count; index++)
                    {
                        var item = visibleItems[index].Item;
                        DetachFromCurrentParent(item);
                        InsertChild(index, item);
                        changed = true;
                    }
                }
                else
                {
                    for (var index = 0; index < visibleItems.Count; index++)
                    {
                        var item = visibleItems[index].Item;
                        var currentIndex = IndexOfChild(item);
                        if (currentIndex == index)
                        {
                            continue;
                        }

                        if (currentIndex >= 0)
                        {
                            changed |= MoveChildRange(currentIndex, 1, index);
                            continue;
                        }

                        DetachFromCurrentParent(item);
                        InsertChild(index, item);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private int IndexOfChild(UIElement child)
        {
            for (var index = 0; index < Children.Count; index++)
            {
                if (ReferenceEquals(Children[index], child))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DetachFromCurrentParent(UIElement child)
        {
            if (child.VisualParent is Panel visualPanel)
            {
                visualPanel.RemoveChild(child);
            }
            else
            {
                child.SetVisualParent(null);
            }

            if (child.LogicalParent is Panel logicalPanel)
            {
                logicalPanel.RemoveChild(child);
            }
            else
            {
                child.SetLogicalParent(null);
            }
        }
    }
}
