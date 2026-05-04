using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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
    private readonly Queue<TreeViewItem> _recycledHierarchicalDataContainers = new();
    private readonly Dictionary<object, bool> _hierarchicalExpansionOverrides = new();
    private readonly Dictionary<object, IReadOnlyList<object>> _lazyLoadedHierarchicalChildren = new();
    private readonly HashSet<INotifyCollectionChanged> _subscribedHierarchicalCollections = new();
    private ScrollViewer? _templatedScrollViewer;
    private ScrollViewer? _viewportSubscribedScrollViewer;
    private float _lastDataHostViewportWidth = float.NaN;
    private float _lastDataHostViewportHeight = float.NaN;
    private int _activeScrollViewerLayoutDepth;
    private Panel _itemsHost;
    private object? _hierarchicalItemsSource;
    private Func<object, IEnumerable<object>>? _hierarchicalChildrenSelector;
    private Func<object, bool>? _hierarchicalHasChildrenSelector;
    private Func<object, string>? _hierarchicalHeaderSelector;
    private Func<object, bool>? _hierarchicalExpandedSelector;
    private Func<object, IEnumerable<object>?>? _hierarchicalLazyChildrenLoader;
    private object? _selectedDataItem;

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

        Focusable = true;
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, OnMouseLeftButtonDownSelectItem);
        AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, OnPreviewMouseLeftButtonUpRefreshDeferredScroll);
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
            _recycledHierarchicalDataContainers.Clear();
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

    public Func<object, IEnumerable<object>?>? HierarchicalLazyChildrenLoader
    {
        get => _hierarchicalLazyChildrenLoader;
        set => _hierarchicalLazyChildrenLoader = value;
    }

    public int RealizedHierarchicalContainerCount => _hierarchicalDataContainers.Count;

    public object? SelectedDataItem
    {
        get => _selectedDataItem;
        set
        {
            if (value == null)
            {
                ApplySelectedItem(null);
                return;
            }

            if (!SelectHierarchicalItem(value))
            {
                _selectedDataItem = value;
            }
        }
    }

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

    public override void InvalidateMeasure()
    {
        if (ShouldConvertStableActiveScrollViewerMeasureInvalidation())
        {
            InvalidateArrange();
            return;
        }

        base.InvalidateMeasure();
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

        ScrollHierarchicalRowIntoView(rowIndex);
        ApplySelectedItem(RealizeHierarchicalDataContainer(_hierarchicalDataRows[rowIndex], rowIndex));
        return true;
    }

    public bool SetHierarchicalItemExpanded(object item, bool isExpanded)
    {
        if (!IsHierarchicalDataMode)
        {
            return false;
        }

        if (isExpanded)
        {
            EnsureLazyHierarchicalChildrenLoaded(item);
        }

        _hierarchicalExpansionOverrides[item] = isExpanded;
        RefreshHierarchicalDataRows();
        return true;
    }

    public bool IsHierarchicalItemExpanded(object item)
    {
        return IsHierarchicalDataMode && IsHierarchicalItemExpandedCore(item);
    }

    public bool ScrollHierarchicalItemIntoView(object item)
    {
        if (!IsHierarchicalDataMode)
        {
            return false;
        }

        var rowIndex = FindHierarchicalRowIndex(item);
        if (rowIndex < 0)
        {
            RefreshHierarchicalDataRows();
            rowIndex = FindHierarchicalRowIndex(item);
        }

        if (rowIndex < 0)
        {
            return false;
        }

        ScrollHierarchicalRowIntoView(rowIndex);
        return true;
    }

    public TreeViewItem? ContainerFromHierarchicalItem(object item)
    {
        if (!_hierarchicalDataContainers.TryGetValue(item, out var container))
        {
            return null;
        }

        return container.VirtualizedTreeRowIndex >= 0 && ReferenceEquals(container.VisualParent, _itemsHost)
            ? container
            : null;
    }

    internal bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        _ = modifiers;
        if (!IsEnabled)
        {
            return false;
        }

        return IsHierarchicalDataMode
            ? HandleHierarchicalKeyDown(key)
            : HandleTreeItemKeyDown(key);
    }

    private bool HandleHierarchicalKeyDown(Keys key)
    {
        if (_hierarchicalDataRows.Count == 0)
        {
            return false;
        }

        var selectedIndex = _selectedDataItem == null ? -1 : FindHierarchicalRowIndex(_selectedDataItem);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        switch (key)
        {
            case Keys.Up:
                return SelectHierarchicalRow(Math.Max(0, selectedIndex - 1));
            case Keys.Down:
                return SelectHierarchicalRow(Math.Min(_hierarchicalDataRows.Count - 1, selectedIndex + 1));
            case Keys.Home:
                return SelectHierarchicalRow(0);
            case Keys.End:
                return SelectHierarchicalRow(_hierarchicalDataRows.Count - 1);
            case Keys.PageUp:
                return SelectHierarchicalRow(Math.Max(0, selectedIndex - EstimateHierarchicalPageStep()));
            case Keys.PageDown:
                return SelectHierarchicalRow(Math.Min(_hierarchicalDataRows.Count - 1, selectedIndex + EstimateHierarchicalPageStep()));
            case Keys.Right:
                return ExpandOrEnterHierarchicalRow(selectedIndex);
            case Keys.Left:
                return CollapseOrSelectHierarchicalParent(selectedIndex);
            default:
                return false;
        }
    }

    private bool HandleTreeItemKeyDown(Keys key)
    {
        var visibleItems = GetVisibleItems();
        if (visibleItems.Count == 0)
        {
            return false;
        }

        var selectedIndex = SelectedItem == null ? -1 : visibleItems.FindIndex(item => ReferenceEquals(item, SelectedItem));
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        switch (key)
        {
            case Keys.Up:
                ApplySelectedItem(visibleItems[Math.Max(0, selectedIndex - 1)]);
                return true;
            case Keys.Down:
                ApplySelectedItem(visibleItems[Math.Min(visibleItems.Count - 1, selectedIndex + 1)]);
                return true;
            case Keys.Home:
                ApplySelectedItem(visibleItems[0]);
                return true;
            case Keys.End:
                ApplySelectedItem(visibleItems[^1]);
                return true;
            case Keys.Right:
                if (SelectedItem is { } selected && selected.HasChildItems())
                {
                    if (!selected.IsExpanded)
                    {
                        selected.IsExpanded = true;
                        RefreshVirtualizedItemsHost();
                    }
                    else if (GetFirstChild(selected) is { } child)
                    {
                        ApplySelectedItem(child);
                    }

                    return true;
                }

                return false;
            case Keys.Left:
                if (SelectedItem is { } current)
                {
                    if (current.IsExpanded)
                    {
                        current.IsExpanded = false;
                        RefreshVirtualizedItemsHost();
                        return true;
                    }

                    if (GetParentTreeItem(current) is { } parent)
                    {
                        ApplySelectedItem(parent);
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
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
        else if ((args.Property == ItemTemplateProperty || args.Property == ItemTemplateSelectorProperty) &&
                 IsHierarchicalDataMode)
        {
            RefreshHierarchicalDataRows();
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
        _activeScrollViewerLayoutDepth++;
        try
        {
            scrollViewer.Measure(innerSize);
        }
        finally
        {
            _activeScrollViewerLayoutDepth--;
        }

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

        _activeScrollViewerLayoutDepth++;
        try
        {
            ActiveScrollViewer.Arrange(new LayoutRect(innerX, innerY, innerWidth, innerHeight));
        }
        finally
        {
            _activeScrollViewerLayoutDepth--;
        }

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

    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        _ = source;
        _ = reason;
        if (ReferenceEquals(origin, this) &&
            _activeScrollViewerLayoutDepth > 0 &&
            IsStableHierarchicalDataViewport())
        {
            return true;
        }

        if (origin is FrameworkElement descendant && ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(descendant))
        {
            return true;
        }

        return base.TryHandleMeasureInvalidation(origin, source, reason);
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        return ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(descendant) ||
               base.ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(descendant);
    }

    internal ScrollViewer AutomationScrollViewer => ActiveScrollViewer;

    private ScrollViewer ActiveScrollViewer => _templatedScrollViewer ?? _fallbackScrollViewer;

    private bool IsHierarchicalDataMode => _hierarchicalItemsSource != null && _hierarchicalChildrenSelector != null;

    private bool ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(FrameworkElement descendant)
    {
        if (!IsStableHierarchicalDataViewport())
        {
            return false;
        }

        var activeScrollViewer = ActiveScrollViewer;
        if (!ReferenceEquals(descendant, activeScrollViewer) &&
            !IsDescendantOrSelf(activeScrollViewer, descendant))
        {
            return false;
        }

        return AreClose(activeScrollViewer.ViewportWidth, _lastDataHostViewportWidth) &&
               AreClose(activeScrollViewer.ViewportHeight, _lastDataHostViewportHeight);
    }

    private bool IsStableHierarchicalDataViewport()
    {
        if (!IsHierarchicalDataMode ||
            _itemsHost is not VirtualizingTreeDataHost ||
            float.IsNaN(_lastDataHostViewportWidth) ||
            float.IsNaN(_lastDataHostViewportHeight))
        {
            return false;
        }

        var activeScrollViewer = ActiveScrollViewer;
        return AreClose(activeScrollViewer.ViewportWidth, _lastDataHostViewportWidth) &&
               AreClose(activeScrollViewer.ViewportHeight, _lastDataHostViewportHeight);
    }

    private bool ShouldConvertStableActiveScrollViewerMeasureInvalidation()
    {
        if (!IsStableHierarchicalDataViewport())
        {
            return false;
        }

        var activeScrollViewer = ActiveScrollViewer;
        return activeScrollViewer.NeedsMeasure ||
               activeScrollViewer.NeedsArrange ||
               _itemsHost.NeedsArrange;
    }

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

        UnsubscribeHierarchicalCollections();
        _hierarchicalDataRows.Clear();
        foreach (var item in GetHierarchicalRootItems())
        {
            AddHierarchicalDataRow(item, depth: 0);
        }

        dataHost.SetRows(_hierarchicalDataRows);
        SyncSelectedHierarchicalContainer();
    }

    private void AddHierarchicalDataRow(object item, int depth)
    {
        var hasChildren = HasHierarchicalChildren(item);
        var expanded = hasChildren && IsHierarchicalItemExpandedCore(item);
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
        if (_lazyLoadedHierarchicalChildren.TryGetValue(item, out var lazyChildren))
        {
            return lazyChildren;
        }

        if (_hierarchicalChildrenSelector?.Invoke(item) is not { } children)
        {
            return Array.Empty<object>();
        }

        if (children is INotifyCollectionChanged notifying)
        {
            SubscribeHierarchicalCollection(notifying);
        }

        return children as IReadOnlyList<object> ?? children.ToArray();
    }

    private bool IsHierarchicalItemExpandedCore(object item)
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

    private int FindHierarchicalRowIndex(object item)
    {
        return _hierarchicalDataRows.FindIndex(row => ReferenceEquals(row.Item, item) || Equals(row.Item, item));
    }

    private void ScrollHierarchicalRowIntoView(int rowIndex)
    {
        if (_itemsHost is not VirtualizingTreeDataHost dataHost)
        {
            return;
        }

        var viewer = ActiveScrollViewer;
        var rowTop = dataHost.GetRowOffset(rowIndex);
        var rowHeight = dataHost.GetRowHeight(rowIndex);
        var rowBottom = rowTop + rowHeight;
        var viewportTop = viewer.VerticalOffset;
        var viewportBottom = viewportTop + MathF.Max(0f, viewer.ViewportHeight);

        if (rowTop < viewportTop)
        {
            viewer.ScrollToVerticalOffset(rowTop);
        }
        else if (rowBottom > viewportBottom)
        {
            viewer.ScrollToVerticalOffset(rowBottom - MathF.Max(rowHeight, viewer.ViewportHeight));
        }
    }

    private void EnsureLazyHierarchicalChildrenLoaded(object item)
    {
        if (_hierarchicalLazyChildrenLoader == null ||
            _lazyLoadedHierarchicalChildren.ContainsKey(item) ||
            GetHierarchicalChildren(item).Count > 0)
        {
            return;
        }

        var loaded = _hierarchicalLazyChildrenLoader(item);
        if (loaded == null)
        {
            return;
        }

        var children = loaded as IReadOnlyList<object> ?? loaded.ToArray();
        if (!TryAppendLazyChildrenToSource(item, children))
        {
            _lazyLoadedHierarchicalChildren[item] = children;
        }
    }

    private bool TryAppendLazyChildrenToSource(object item, IReadOnlyList<object> children)
    {
        if (children.Count == 0 ||
            _hierarchicalChildrenSelector?.Invoke(item) is not { } existing)
        {
            return false;
        }

        var addMethod = existing.GetType().GetMethod("Add", [children[0].GetType()]);
        if (addMethod == null)
        {
            return false;
        }

        for (var i = 0; i < children.Count; i++)
        {
            addMethod.Invoke(existing, [children[i]]);
        }

        return true;
    }

    private void ApplyHierarchicalItemTemplate(TreeViewItem container, object item)
    {
        var selectedTemplate = DataTemplateResolver.ResolveTemplateForContent(
            this,
            item,
            ItemTemplate,
            ItemTemplateSelector,
            container);
        if (selectedTemplate == null)
        {
            container.SetVirtualizedHeaderElement(null);
            return;
        }

        container.SetVirtualizedHeaderElement(selectedTemplate.Build(item, this));
    }

    private void SubscribeHierarchicalCollection(INotifyCollectionChanged collection)
    {
        if (_subscribedHierarchicalCollections.Add(collection))
        {
            collection.CollectionChanged += OnHierarchicalCollectionChanged;
        }
    }

    private void UnsubscribeHierarchicalCollections()
    {
        foreach (var collection in _subscribedHierarchicalCollections)
        {
            collection.CollectionChanged -= OnHierarchicalCollectionChanged;
        }

        _subscribedHierarchicalCollections.Clear();
    }

    private void OnHierarchicalCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        RefreshHierarchicalDataRows();
    }

    private TreeViewItem RealizeHierarchicalDataContainer(VisibleTreeDataEntry row, int rowIndex)
    {
        if (!_hierarchicalDataContainers.TryGetValue(row.Item, out var container))
        {
            container = _recycledHierarchicalDataContainers.Count > 0
                ? _recycledHierarchicalDataContainers.Dequeue()
                : new TreeViewItem();
            _hierarchicalDataContainers[row.Item] = container;
            ApplyTypographyToItem(container, null, Foreground);
        }

        ApplyHierarchicalDataContainer(container, row, rowIndex);
        return container;
    }

    private void ApplyHierarchicalDataContainer(TreeViewItem container, VisibleTreeDataEntry row, int rowIndex)
    {
        container.ClearVirtualizedDisplaySnapshot();
        container.Header = GetHierarchicalHeader(row.Item);
        container.Tag = row.Item;
        container.DataContext = row.Item;
        container.IsExpanded = row.IsExpanded;
        container.UseVirtualizedTreeLayout = true;
        container.VirtualizedTreeDepth = row.Depth;
        container.VirtualizedTreeRowIndex = rowIndex;
        container.HasVirtualizedChildItems = row.HasChildren;
        container.IsSelected = IsHierarchicalDataItemSelected(row.Item);
        PrepareContainerForItemOverride(container, row.Item, rowIndex);
        ApplyHierarchicalItemTemplate(container, row.Item);
        if (container.IsSelected)
        {
            SelectedItem = container;
        }
    }

    private void RecycleHierarchicalDataContainer(TreeViewItem container)
    {
        if (ReferenceEquals(container, SelectedItem))
        {
            return;
        }

        if (container.Tag is { } item)
        {
            _hierarchicalDataContainers.Remove(item);
        }

        container.IsSelected = false;
        container.ClearVirtualizedDisplaySnapshot();
        container.SetVirtualizedHeaderElement(null);
        container.Tag = null;
        container.DataContext = null;
        container.VirtualizedTreeRowIndex = -1;
        _recycledHierarchicalDataContainers.Enqueue(container);
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

        FocusManager.SetFocus(this);
        if (clickedItem.HitExpander(GetExpanderHitTestPoint(clickedItem, args.Position)))
        {
            if (IsHierarchicalDataMode && clickedItem.Tag != null)
            {
                if (!clickedItem.IsExpanded)
                {
                    EnsureLazyHierarchicalChildrenLoaded(clickedItem.Tag);
                }

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

    private void OnPreviewMouseLeftButtonUpRefreshDeferredScroll(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            dataHost.RefreshPendingStableViewportOffsetChange();
        }
    }

    private bool SelectHierarchicalRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _hierarchicalDataRows.Count)
        {
            return false;
        }

        var row = _hierarchicalDataRows[rowIndex];
        ScrollHierarchicalRowIntoView(rowIndex);
        ApplySelectedItem(RealizeHierarchicalDataContainer(row, rowIndex));
        return true;
    }

    private bool ExpandOrEnterHierarchicalRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _hierarchicalDataRows.Count)
        {
            return false;
        }

        var row = _hierarchicalDataRows[rowIndex];
        if (!row.HasChildren)
        {
            return false;
        }

        if (!row.IsExpanded)
        {
            SetHierarchicalItemExpanded(row.Item, true);
            SelectHierarchicalRow(FindHierarchicalRowIndex(row.Item));
            return true;
        }

        var next = rowIndex + 1;
        if (next < _hierarchicalDataRows.Count && _hierarchicalDataRows[next].Depth > row.Depth)
        {
            return SelectHierarchicalRow(next);
        }

        return true;
    }

    private bool CollapseOrSelectHierarchicalParent(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _hierarchicalDataRows.Count)
        {
            return false;
        }

        var row = _hierarchicalDataRows[rowIndex];
        if (row.HasChildren && row.IsExpanded)
        {
            SetHierarchicalItemExpanded(row.Item, false);
            SelectHierarchicalRow(FindHierarchicalRowIndex(row.Item));
            return true;
        }

        for (var i = rowIndex - 1; i >= 0; i--)
        {
            if (_hierarchicalDataRows[i].Depth < row.Depth)
            {
                return SelectHierarchicalRow(i);
            }
        }

        return true;
    }

    private int EstimateHierarchicalPageStep()
    {
        if (_itemsHost is VirtualizingTreeDataHost dataHost)
        {
            return Math.Max(1, (int)MathF.Floor(ActiveScrollViewer.ViewportHeight / MathF.Max(1f, dataHost.AverageRowHeight)));
        }

        return 10;
    }

    private bool IsActiveScrollViewerThumbCaptured()
    {
        if (FocusManager.GetCapturedPointerElement() is not Thumb capturedThumb)
        {
            return false;
        }

        var activeViewer = ActiveScrollViewer;
        for (UIElement? current = capturedThumb; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, activeViewer))
            {
                return true;
            }
        }

        return false;
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

    private void ApplySelectedItem(TreeViewItem? item)
    {
        if (ReferenceEquals(item, SelectedItem))
        {
            if (item == null)
            {
                _selectedDataItem = null;
            }

            return;
        }

        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = false;
        }

        SelectedItem = item;
        _selectedDataItem = IsHierarchicalDataMode ? item?.Tag : item;
        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = true;
        }

        RaiseRoutedEvent(SelectedItemChangedEvent, new RoutedSimpleEventArgs(SelectedItemChangedEvent));
    }

    private void SyncSelectedHierarchicalContainer()
    {
        if (_selectedDataItem == null)
        {
            return;
        }

        var rowIndex = FindHierarchicalRowIndex(_selectedDataItem);
        if (rowIndex < 0)
        {
            if (SelectedItem != null)
            {
                SelectedItem.IsSelected = false;
            }

            SelectedItem = null;
            return;
        }

        if (_hierarchicalDataContainers.TryGetValue(_selectedDataItem, out var container))
        {
            container.IsSelected = true;
            SelectedItem = container;
        }
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
            var activeScrollViewer = ActiveScrollViewer;
            var viewportWidth = activeScrollViewer.ViewportWidth;
            var viewportHeight = activeScrollViewer.ViewportHeight;
            var viewportSizeChanged =
                !AreClose(_lastDataHostViewportWidth, viewportWidth) ||
                !AreClose(_lastDataHostViewportHeight, viewportHeight);

            _lastDataHostViewportWidth = viewportWidth;
            _lastDataHostViewportHeight = viewportHeight;
            if (viewportSizeChanged)
            {
                dataHost.InvalidateMeasure();
                dataHost.InvalidateArrange();
            }
            else
            {
                dataHost.RefreshForStableViewportOffsetChange();
            }
        }
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f;
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
        private readonly List<float> _rowHeights = new();
        private readonly List<float> _rowOffsets = new();
        private bool _rowOffsetsDirty = true;
        private int _firstRealizedIndex = -1;
        private int _lastRealizedIndex = -1;
        private bool _pendingDeferredOffsetRefresh;
        private float _averageRowHeight = FallbackRowHeight;
        private float _rowHeightTotal;
        private float _estimatedExtentWidth;

        public VirtualizingTreeDataHost(TreeView owner)
        {
            _owner = owner;
            Background = Color.Transparent;
        }

        public bool OwnsHorizontalScrollOffset => true;

        public bool OwnsVerticalScrollOffset => true;

        public float AverageRowHeight => _averageRowHeight;

        public void SetRows(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            _rows = rows;
            _rowHeights.Clear();
            _rowOffsets.Clear();
            _rowHeightTotal = 0f;
            _rowOffsetsDirty = true;
            EnsureRowMetricStorage();
            _firstRealizedIndex = -1;
            _lastRealizedIndex = -1;
            _pendingDeferredOffsetRefresh = false;
            _estimatedExtentWidth = EstimateExtentWidth(rows);
            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            RealizeRows(availableSize.Y, invalidateMeasureForChildMutations: true);
            var childConstraint = new Vector2(availableSize.X, float.PositiveInfinity);
            foreach (var child in Children)
            {
                if (child is FrameworkElement element)
                {
                    element.Measure(childConstraint);
                    if (child is TreeViewItem item && item.VirtualizedTreeRowIndex >= 0)
                    {
                        UpdateMeasuredRowMetric(item.VirtualizedTreeRowIndex, element.DesiredSize);
                    }
                }
            }

            var viewportWidth = float.IsFinite(availableSize.X) ? MathF.Max(0f, availableSize.X) : 0f;
            return new Vector2(MathF.Max(viewportWidth, _estimatedExtentWidth), GetTotalExtentHeight());
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            RealizeRows(finalSize.Y, invalidateMeasureForChildMutations: false);
            var childConstraint = new Vector2(finalSize.X, float.PositiveInfinity);
            foreach (var child in Children)
            {
                if (child is not TreeViewItem item || item.VirtualizedTreeRowIndex < 0)
                {
                    continue;
                }

                if (item.NeedsMeasure)
                {
                    item.Measure(childConstraint);
                    UpdateMeasuredRowMetric(item.VirtualizedTreeRowIndex, item.DesiredSize);
                }

                var index = item.VirtualizedTreeRowIndex;
                var rowHeight = GetRowHeight(index);
                item.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + GetRowOffset(index),
                    finalSize.X,
                    rowHeight));
            }

            return finalSize;
        }

        public void RefreshForStableViewportOffsetChange()
        {
            RefreshForStableViewportOffsetChange(forceRealization: false);
        }

        public void RefreshPendingStableViewportOffsetChange()
        {
            if (!_pendingDeferredOffsetRefresh)
            {
                return;
            }

            _pendingDeferredOffsetRefresh = false;
            RefreshForStableViewportOffsetChange(forceRealization: true);
        }

        private void RefreshForStableViewportOffsetChange(bool forceRealization)
        {
            if (!IsMeasureValidForTests ||
                !IsArrangeValidForTests ||
                LayoutSlot.Width <= 0f ||
                LayoutSlot.Height <= 0f)
            {
                InvalidateArrangeForDirectLayoutOnly();
                return;
            }

            var range = CalculateRealizedRange(LayoutSlot.Height);
            if (!forceRealization &&
                _owner.IsActiveScrollViewerThumbCaptured() &&
                range.First > 0)
            {
                RetargetRealizedRowsForThumbDrag(range.First, range.Last);
                _pendingDeferredOffsetRefresh = true;
                UiRoot.Current?.NotifyDirectRenderInvalidation(this);
                return;
            }

            if (RealizeRows(LayoutSlot.Height, invalidateMeasureForChildMutations: false, suppressLayoutInvalidations: true))
            {
                InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
                Arrange(LayoutSlot);
            }

            UiRoot.Current?.NotifyDirectRenderInvalidation(this);
        }

        private bool ShouldDeferRealizationDuringThumbDrag(int first, int last)
        {
            if (_firstRealizedIndex < 0 || _lastRealizedIndex < _firstRealizedIndex)
            {
                return false;
            }

            var overlapFirst = Math.Max(first, _firstRealizedIndex);
            var overlapLast = Math.Min(last, _lastRealizedIndex);
            var overlapCount = overlapLast >= overlapFirst ? overlapLast - overlapFirst + 1 : 0;
            var currentCount = _lastRealizedIndex - _firstRealizedIndex + 1;
            return overlapCount < currentCount / 2;
        }

        private void RetargetRealizedRowsForThumbDrag(int first, int last)
        {
            if (Children.Count == 0 || first > last)
            {
                return;
            }

            var childConstraint = new Vector2(LayoutSlot.Width, float.PositiveInfinity);
            var rowIndex = first;
            foreach (var child in Children)
            {
                if (rowIndex > last || child is not TreeViewItem item)
                {
                    continue;
                }

                var row = _rows[rowIndex];
                item.ApplyVirtualizedDisplaySnapshot(
                    _owner.GetHierarchicalHeader(row.Item),
                    row.HasChildren,
                    row.IsExpanded,
                    _owner.IsHierarchicalDataItemSelected(row.Item),
                    row.Depth,
                    rowIndex);

                if (item.NeedsMeasure)
                {
                    item.Measure(childConstraint);
                }

                var rowHeight = GetRowHeight(rowIndex);
                item.Arrange(new LayoutRect(
                    LayoutSlot.X,
                    LayoutSlot.Y + GetRowOffset(rowIndex),
                    LayoutSlot.Width,
                    rowHeight));
                rowIndex++;
            }
        }

        private bool RealizeRows(
            float viewportHeight,
            bool invalidateMeasureForChildMutations,
            bool suppressLayoutInvalidations = false)
        {
            var range = CalculateRealizedRange(viewportHeight);
            var first = range.First;
            var last = range.Last;

            if (first == _firstRealizedIndex &&
                last == _lastRealizedIndex &&
                HasCurrentRealizedRows(first, last))
            {
                return false;
            }

            _firstRealizedIndex = first;
            _lastRealizedIndex = last;
            using (suppressLayoutInvalidations
                       ? DeferChildMutationLayoutInvalidations()
                       : invalidateMeasureForChildMutations
                           ? DeferChildMutationInvalidations()
                           : DeferChildMutationArrangeInvalidations())
            {
                for (var childIndex = Children.Count - 1; childIndex >= 0; childIndex--)
                {
                    var child = Children[childIndex];
                    if (child is TreeViewItem treeItem &&
                        ShouldKeepRealizedChild(treeItem, first, last))
                    {
                        if (!treeItem.HasVirtualizedDisplaySnapshot)
                        {
                            var keptRowIndex = treeItem.VirtualizedTreeRowIndex;
                            _owner.ApplyHierarchicalDataContainer(treeItem, _rows[keptRowIndex], keptRowIndex);
                        }

                        continue;
                    }

                    RemoveChildAt(childIndex);
                    if (child is TreeViewItem removedTreeItem)
                    {
                        _owner.RecycleHierarchicalDataContainer(removedTreeItem);
                    }
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

            return true;
        }

        private bool HasCurrentRealizedRows(int first, int last)
        {
            if (first > last)
            {
                return Children.Count == 0;
            }

            var expectedCount = last - first + 1;
            if (Children.Count != expectedCount)
            {
                return false;
            }

            foreach (var child in Children)
            {
                if (child is not TreeViewItem item ||
                    item.HasVirtualizedDisplaySnapshot ||
                    !ShouldKeepRealizedChild(item, first, last))
                {
                    return false;
                }
            }

            return true;
        }

        private (int First, int Last) CalculateRealizedRange(float viewportHeight)
        {
            var viewer = _owner.ActiveScrollViewer;
            var offset = MathF.Max(0f, viewer.VerticalOffset);
            var viewport = float.IsFinite(viewportHeight) && viewportHeight > 0f
                ? viewportHeight
                : MathF.Max(viewer.ViewportHeight, FallbackRowHeight);
            var cacheHeight = MathF.Max(viewport, _averageRowHeight * 4f);
            var first = Math.Max(0, FindRowIndexAtOffset(MathF.Max(0f, offset - cacheHeight)));
            var last = Math.Min(_rows.Count - 1, FindRowIndexAtOffset(offset + viewport + cacheHeight));
            return (first, last);
        }

        public float GetRowOffset(int rowIndex)
        {
            EnsureRowOffsetsCurrent();
            return rowIndex <= 0 ? 0f : _rowOffsets[Math.Clamp(rowIndex, 0, _rowOffsets.Count - 1)];
        }

        public float GetRowHeight(int rowIndex)
        {
            EnsureRowMetricStorage();
            if ((uint)rowIndex >= (uint)_rowHeights.Count)
            {
                return _averageRowHeight;
            }

            return MathF.Max(1f, _rowHeights[rowIndex]);
        }

        private void EnsureRowMetricStorage()
        {
            var changed = false;
            while (_rowHeights.Count < _rows.Count)
            {
                _rowHeights.Add(_averageRowHeight);
                _rowOffsets.Add(0f);
                _rowHeightTotal += _averageRowHeight;
                changed = true;
            }

            if (_rowHeights.Count > _rows.Count)
            {
                for (var i = _rows.Count; i < _rowHeights.Count; i++)
                {
                    _rowHeightTotal -= _rowHeights[i];
                }

                _rowHeights.RemoveRange(_rows.Count, _rowHeights.Count - _rows.Count);
                _rowOffsets.RemoveRange(_rows.Count, _rowOffsets.Count - _rows.Count);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            if (_rowHeights.Count == 0)
            {
                _rowHeightTotal = 0f;
            }
            else
            {
                _averageRowHeight = _rowHeightTotal / _rowHeights.Count;
            }

            _rowOffsetsDirty = true;
        }

        private void UpdateMeasuredRowMetric(int rowIndex, Vector2 desiredSize)
        {
            if ((uint)rowIndex >= (uint)_rowHeights.Count)
            {
                return;
            }

            var height = MathF.Max(1f, desiredSize.Y);
            var previousHeight = _rowHeights[rowIndex];
            if (AreClose(previousHeight, height))
            {
                return;
            }

            _rowHeights[rowIndex] = height;
            _rowHeightTotal += height - previousHeight;
            if (_rowHeights.Count > 0)
            {
                _averageRowHeight = _rowHeightTotal / _rowHeights.Count;
            }

            _rowOffsetsDirty = true;
        }

        private float EstimateExtentWidth(IReadOnlyList<VisibleTreeDataEntry> rows)
        {
            var maxWidth = 0f;
            for (var i = 0; i < rows.Count; i++)
            {
                maxWidth = MathF.Max(maxWidth, EstimateRowWidth(rows[i]));
            }

            return maxWidth;
        }

        private float EstimateRowWidth(VisibleTreeDataEntry row)
        {
            var header = _owner.GetHierarchicalHeader(row.Item);
            if (string.IsNullOrEmpty(header))
            {
                return row.Depth * 16f + 26f;
            }

            var depthOffset = MathF.Max(0f, row.Depth) * 16f;
            var glyphAndPadding = row.HasChildren ? 20f : 10f;
            return depthOffset + glyphAndPadding + UiTextRenderer.MeasureWidth(_owner, header, _owner.FontSize);
        }

        private void EnsureRowOffsetsCurrent()
        {
            EnsureRowMetricStorage();
            if (!_rowOffsetsDirty)
            {
                return;
            }

            var offset = 0f;
            for (var i = 0; i < _rowOffsets.Count; i++)
            {
                _rowOffsets[i] = offset;
                offset += MathF.Max(1f, _rowHeights[i]);
            }

            _rowOffsetsDirty = false;
        }

        private float GetTotalExtentHeight()
        {
            if (_rowHeights.Count == 0)
            {
                return 0f;
            }

            EnsureRowOffsetsCurrent();
            var last = _rowHeights.Count - 1;
            return _rowOffsets[last] + MathF.Max(1f, _rowHeights[last]);
        }

        private int FindRowIndexAtOffset(float offset)
        {
            EnsureRowOffsetsCurrent();
            if (_rowOffsets.Count == 0)
            {
                return -1;
            }

            var low = 0;
            var high = _rowOffsets.Count - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var rowStart = _rowOffsets[mid];
                var rowEnd = rowStart + GetRowHeight(mid);
                if (offset < rowStart)
                {
                    high = mid - 1;
                }
                else if (offset >= rowEnd)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return Math.Clamp(low, 0, _rowOffsets.Count - 1);
        }

        private bool ShouldKeepRealizedChild(TreeViewItem item, int first, int last)
        {
            var rowIndex = item.VirtualizedTreeRowIndex;
            return rowIndex >= first &&
                   rowIndex <= last &&
                   rowIndex < _rows.Count &&
                   ReferenceEquals(item.Tag, _rows[rowIndex].Item);
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

    private bool IsHierarchicalDataItemSelected(object item)
    {
        return _selectedDataItem is { } selectedItem &&
               (ReferenceEquals(selectedItem, item) || Equals(selectedItem, item));
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
