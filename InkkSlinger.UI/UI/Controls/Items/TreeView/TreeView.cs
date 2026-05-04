using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
public partial class TreeView : ItemsControl
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

}
