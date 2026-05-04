using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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
    private readonly HierarchicalDataController _hierarchicalData;
    private ScrollViewer? _templatedScrollViewer;
    private ScrollViewer? _viewportSubscribedScrollViewer;
    private float _lastDataHostViewportWidth = float.NaN;
    private float _lastDataHostViewportHeight = float.NaN;
    private int _activeScrollViewerLayoutDepth;
    private Panel _itemsHost;
    private object? _selectedDataItem;

    public TreeView()
    {
        _hierarchicalData = new HierarchicalDataController(this);
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
        get => _hierarchicalData.ItemsSource;
        set => _hierarchicalData.ItemsSource = value;
    }

    public Func<object, IEnumerable<object>>? HierarchicalChildrenSelector
    {
        get => _hierarchicalData.ChildrenSelector;
        set => _hierarchicalData.ChildrenSelector = value;
    }

    public Func<object, bool>? HierarchicalHasChildrenSelector
    {
        get => _hierarchicalData.HasChildrenSelector;
        set => _hierarchicalData.HasChildrenSelector = value;
    }

    public Func<object, string>? HierarchicalHeaderSelector
    {
        get => _hierarchicalData.HeaderSelector;
        set => _hierarchicalData.HeaderSelector = value;
    }

    public Func<object, bool>? HierarchicalExpandedSelector
    {
        get => _hierarchicalData.ExpandedSelector;
        set => _hierarchicalData.ExpandedSelector = value;
    }

    public Func<object, IEnumerable<object>?>? HierarchicalLazyChildrenLoader
    {
        get => _hierarchicalData.LazyChildrenLoader;
        set => _hierarchicalData.LazyChildrenLoader = value;
    }

    public int RealizedHierarchicalContainerCount => _hierarchicalData.RealizedContainerCount;

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
        _diagOnApplyTemplateCallCount++;
        _runtimeOnApplyTemplateCallCount++;
        base.OnApplyTemplate();

        _templatedScrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
        if (_templatedScrollViewer == null)
        {
            _diagOnApplyTemplateFallbackPathCount++;
            RestoreFallbackScrollViewer();
            return;
        }

        _diagOnApplyTemplateTemplatedPathCount++;
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
        _diagInvalidateMeasureCallCount++;
        _runtimeInvalidateMeasureCallCount++;
        if (ShouldConvertStableActiveScrollViewerMeasureInvalidation())
        {
            _diagInvalidateMeasureConvertedToArrangeCount++;
            InvalidateArrange();
            return;
        }

        _diagInvalidateMeasureBasePathCount++;
        base.InvalidateMeasure();
    }

    public bool SelectHierarchicalItem(object item)
    {
        _diagSelectHierarchicalItemCallCount++;
        _runtimeSelectHierarchicalItemCallCount++;
        if (!IsHierarchicalDataMode)
        {
            _diagSelectHierarchicalItemNotHierarchicalCount++;
            return false;
        }

        var rowIndex = FindHierarchicalRowIndex(item);
        if (rowIndex < 0)
        {
            RefreshHierarchicalDataRows();
            rowIndex = FindHierarchicalRowIndex(item);
            if (rowIndex < 0)
            {
                _diagSelectHierarchicalItemNotFoundCount++;
                return false;
            }

            _diagSelectHierarchicalItemRefreshThenFoundCount++;
        }
        else
        {
            _diagSelectHierarchicalItemFoundPathCount++;
        }

        ScrollHierarchicalRowIntoView(rowIndex);
        ApplySelectedItem(RealizeHierarchicalDataContainer(GetHierarchicalRow(rowIndex), rowIndex));
        return true;
    }

    public bool SetHierarchicalItemExpanded(object item, bool isExpanded)
    {
        _diagSetHierarchicalItemExpandedCallCount++;
        if (!IsHierarchicalDataMode)
        {
            return false;
        }

        return _hierarchicalData.SetExpanded(item, isExpanded);
    }

    public bool IsHierarchicalItemExpanded(object item)
    {
        return _hierarchicalData.IsExpanded(item);
    }

    public bool ScrollHierarchicalItemIntoView(object item)
    {
        _diagScrollHierarchicalItemIntoViewCallCount++;
        if (!IsHierarchicalDataMode)
        {
            _diagScrollHierarchicalItemIntoViewNotHierarchicalCount++;
            return false;
        }

        var rowIndex = FindHierarchicalRowIndex(item);
        if (rowIndex < 0)
        {
            RefreshHierarchicalDataRows();
            rowIndex = FindHierarchicalRowIndex(item);
            if (rowIndex < 0)
            {
                _diagScrollHierarchicalItemIntoViewNotFoundCount++;
                return false;
            }

            _diagScrollHierarchicalItemIntoViewRefreshThenFoundCount++;
        }
        else
        {
            _diagScrollHierarchicalItemIntoViewFoundCount++;
        }

        ScrollHierarchicalRowIntoView(rowIndex);
        return true;
    }

    public TreeViewItem? ContainerFromHierarchicalItem(object item)
    {
        _diagContainerFromHierarchicalItemCallCount++;
        return _hierarchicalData.ContainerFromItem(item, _itemsHost);
    }

    internal bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        _diagHandleKeyDownCallCount++;
        _runtimeHandleKeyDownCallCount++;
        _ = modifiers;
        if (!IsEnabled)
        {
            _diagHandleKeyDownDisabledSkippedCount++;
            return false;
        }

        if (IsHierarchicalDataMode)
        {
            _diagHandleKeyDownHierarchicalPathCount++;
            return HandleHierarchicalKeyDown(key);
        }

        _diagHandleKeyDownTreeItemPathCount++;
        return HandleTreeItemKeyDown(key);
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
        _diagMeasureOverrideCallCount++;
        _runtimeMeasureOverrideCallCount++;
        var startTicks = Stopwatch.GetTimestamp();
        var templateDesired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            _diagMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _diagMeasureOverrideTemplatePathCount++;
            return templateDesired;
        }

        _diagMeasureOverrideFallbackPathCount++;
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
        _diagMeasureOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return new Vector2(
            desired.X + (border * 2f) + padding.Horizontal,
            desired.Y + (border * 2f) + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _diagArrangeOverrideCallCount++;
        _runtimeArrangeOverrideCallCount++;
        var startTicks = Stopwatch.GetTimestamp();
        if (HasTemplateRoot)
        {
            _diagArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
            _diagArrangeOverrideTemplatePathCount++;
            return base.ArrangeOverride(finalSize);
        }

        _diagArrangeOverrideFallbackPathCount++;
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
            RememberActiveDataHostViewport();
        }
        finally
        {
            _activeScrollViewerLayoutDepth--;
        }

        _diagArrangeOverrideElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        _diagOnRenderCallCount++;
        _runtimeOnRenderCallCount++;
        if (HasTemplateRoot)
        {
            _diagOnRenderTemplateSkippedCount++;
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
        _diagTryHandleMeasureInvalidationCallCount++;
        _runtimeTryHandleMeasureInvalidationCallCount++;
        _ = source;
        _ = reason;
        // Data-mode scrolling can move the realized row window without changing the viewport.
        // Treat those invalidations as arrange/render work so wheel and thumb scroll stay inside a frame budget.
        if (ReferenceEquals(origin, this) &&
            _activeScrollViewerLayoutDepth > 0 &&
            IsStableHierarchicalDataViewport())
        {
            _diagTryHandleMeasureInvalidationAntiLoopCount++;
            return true;
        }

        if (origin is FrameworkElement descendant && ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(descendant))
        {
            _diagTryHandleMeasureInvalidationArrangeOnlyCount++;
            return true;
        }

        _diagTryHandleMeasureInvalidationBasePathCount++;
        return base.TryHandleMeasureInvalidation(origin, source, reason);
    }

    protected internal override bool ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(FrameworkElement descendant)
    {
        _diagShouldSuppressMeasureInvalidationCallCount++;
        var result = ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(descendant) ||
                     base.ShouldSuppressMeasureInvalidationFromDescendantDuringMeasure(descendant);
        if (result)
        {
            _diagShouldSuppressMeasureInvalidationTrueCount++;
        }

        return result;
    }

    internal ScrollViewer AutomationScrollViewer => ActiveScrollViewer;

    private ScrollViewer ActiveScrollViewer => _templatedScrollViewer ?? _fallbackScrollViewer;

    private bool IsHierarchicalDataMode => _hierarchicalData.IsActive;

    private bool ShouldTreatActiveScrollViewerMeasureAsArrangeOnly(FrameworkElement descendant)
    {
        _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyCallCount++;
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

        var result = AreClose(activeScrollViewer.ViewportWidth, _lastDataHostViewportWidth) &&
                     AreClose(activeScrollViewer.ViewportHeight, _lastDataHostViewportHeight);
        if (result)
        {
            _diagShouldTreatActiveScrollViewerMeasureAsArrangeOnlyTrueCount++;
        }

        return result;
    }

    private bool IsStableHierarchicalDataViewport()
    {
        _diagIsStableHierarchicalDataViewportCallCount++;
        if (!IsHierarchicalDataMode ||
            _itemsHost is not VirtualizingTreeDataHost ||
            float.IsNaN(_lastDataHostViewportWidth) ||
            float.IsNaN(_lastDataHostViewportHeight))
        {
            return false;
        }

        var activeScrollViewer = ActiveScrollViewer;
        var stable = AreClose(activeScrollViewer.ViewportWidth, _lastDataHostViewportWidth) &&
                     AreClose(activeScrollViewer.ViewportHeight, _lastDataHostViewportHeight);
        if (stable)
        {
            _diagIsStableHierarchicalDataViewportTrueCount++;
        }

        return stable;
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
