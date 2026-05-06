using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

[TemplatePart("PART_Expander", typeof(UIElement))]
public partial class TreeViewItem : ItemsControl
{
    internal event EventHandler? ExpandedStateChanged;
    private bool _isApplyingPropagatedForeground;
    private bool _isApplyingVirtualizedBranchState;
    private UIElement? _virtualizedHeaderElement;
    private float _virtualizedHeaderMinRowHeight;
    private bool _virtualizedTemplateSnapshotResolved;
    private float _virtualizedExpanderColumnWidth;
    private float _virtualizedHeaderFontSize;
    private TextTrimming _virtualizedHeaderTextTrimming;
    private TextWrapping _virtualizedHeaderTextWrapping;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(
            nameof(IsSelected),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(
                Color.White,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is TreeViewItem treeViewItem &&
                        !treeViewItem._isApplyingPropagatedForeground &&
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        treeViewItem.PropagateTypographyToChildren(
                            oldColor,
                            newColor);
                    }
                }));

    public static readonly DependencyProperty SelectedBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedBackground),
            typeof(Color),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(new Color(60, 98, 141), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IndentProperty =
        DependencyProperty.Register(
            nameof(Indent),
            typeof(float),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(16f, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HasItemsProperty =
        DependencyProperty.Register(
            nameof(HasItems),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ShowsBuiltInExpanderProperty =
        DependencyProperty.Register(
            nameof(ShowsBuiltInExpander),
            typeof(bool),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CollapsedExpanderGlyphProperty =
        DependencyProperty.Register(
            nameof(CollapsedExpanderGlyph),
            typeof(string),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(">",
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is TreeViewItem treeViewItem)
                    {
                        treeViewItem.UpdateExpanderPresentation();
                    }
                }));

    public static readonly DependencyProperty ExpandedExpanderGlyphProperty =
        DependencyProperty.Register(
            nameof(ExpandedExpanderGlyph),
            typeof(string),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata("v",
                propertyChangedCallback: static (dependencyObject, _) =>
                {
                    if (dependencyObject is TreeViewItem treeViewItem)
                    {
                        treeViewItem.UpdateExpanderPresentation();
                    }
                }));

    public static readonly DependencyProperty CurrentExpanderGlyphProperty =
        DependencyProperty.Register(
            nameof(CurrentExpanderGlyph),
            typeof(string),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(">"));

    public static readonly DependencyProperty ExpanderGlyphVisibilityProperty =
        DependencyProperty.Register(
            nameof(ExpanderGlyphVisibility),
            typeof(Visibility),
            typeof(TreeViewItem),
            new FrameworkPropertyMetadata(Visibility.Visible));

    protected override bool IncludeGeneratedChildrenInVisualTree => IsExpanded && !UseVirtualizedTreeLayout;

    internal bool UseVirtualizedTreeLayout { get; set; }

    private bool _hasVirtualizedChildItems;
    private bool _suppressExpanderPresentationUpdates;

    internal bool HasVirtualizedChildItems
    {
        get => _hasVirtualizedChildItems;
        set
        {
            if (_hasVirtualizedChildItems == value)
            {
                return;
            }

            _hasVirtualizedChildItems = value;
            UpdateHasItems();
        }
    }

    internal object? VirtualizedTreeDataItem { get; set; }

    public object? HierarchicalDataItem => VirtualizedTreeDataItem;

    internal float RowHitHeightForInput => GetRowHeight();

    internal int VirtualizedTreeDepth { get; set; }

    internal int VirtualizedTreeRowIndex { get; set; } = -1;

    public string Header
    {
        get => GetValue<string>(HeaderProperty) ?? string.Empty;
        set => SetValue(HeaderProperty, value);
    }

    internal string DisplayHeaderForDiagnostics => GetEffectiveHeader();

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        if (_virtualizedHeaderElement != null)
        {
            yield return _virtualizedHeaderElement;
        }

        if (UseVirtualizedTreeLayout)
        {
            yield break;
        }

        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return (UseVirtualizedTreeLayout ? 0 : base.GetVisualChildCountForTraversal()) + (_virtualizedHeaderElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (_virtualizedHeaderElement != null)
        {
            if (index == 0)
            {
                return _virtualizedHeaderElement;
            }

            index--;
        }

        if (UseVirtualizedTreeLayout)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return base.GetVisualChildAtForTraversal(index);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public new bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color SelectedBackground
    {
        get => GetValue<Color>(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
    }

    public float Indent
    {
        get => GetValue<float>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    public bool HasItems
    {
        get => GetValue<bool>(HasItemsProperty);
        internal set => SetValue(HasItemsProperty, value);
    }

    public bool ShowsBuiltInExpander
    {
        get => GetValue<bool>(ShowsBuiltInExpanderProperty);
        set => SetValue(ShowsBuiltInExpanderProperty, value);
    }

    public string CollapsedExpanderGlyph
    {
        get => GetValue<string>(CollapsedExpanderGlyphProperty) ?? ">";
        set => SetValue(CollapsedExpanderGlyphProperty, value);
    }

    public string ExpandedExpanderGlyph
    {
        get => GetValue<string>(ExpandedExpanderGlyphProperty) ?? "v";
        set => SetValue(ExpandedExpanderGlyphProperty, value);
    }

    public string CurrentExpanderGlyph
    {
        get => GetValue<string>(CurrentExpanderGlyphProperty) ?? string.Empty;
        internal set => SetValue(CurrentExpanderGlyphProperty, value);
    }

    public Visibility ExpanderGlyphVisibility
    {
        get => GetValue<Visibility>(ExpanderGlyphVisibilityProperty);
        internal set => SetValue(ExpanderGlyphVisibilityProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateExpanderPresentation();
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == TemplateProperty || args.Property == StyleProperty)
        {
            ResetVirtualizedTemplateSnapshot();
            if (UseVirtualizedTreeLayout && args.Property == TemplateProperty)
            {
                EnsureVirtualizedTemplateSnapshot();
            }
        }

        if (args.Property == ShowsBuiltInExpanderProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }

        if (!_suppressExpanderPresentationUpdates &&
            (args.Property == HasItemsProperty ||
             args.Property == IsExpandedProperty))
        {
            UpdateExpanderPresentation();
        }

        if (args.Property != IsExpandedProperty)
        {
            return;
        }

        ExpandedStateChanged?.Invoke(this, EventArgs.Empty);
        if (UseVirtualizedTreeLayout)
        {
            return;
        }

        UiRoot.Current?.NotifyVisualStructureChanged(this, VisualParent, VisualParent);
    }

    protected override bool ShouldInvalidateMeasureForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (ShouldAbsorbVirtualizedBranchLayoutInvalidation(args.Property))
        {
            return false;
        }

        return base.ShouldInvalidateMeasureForPropertyChange(args, metadata);
    }

    protected override bool ShouldInvalidateArrangeForPropertyChange(
        DependencyPropertyChangedEventArgs args,
        FrameworkPropertyMetadata metadata)
    {
        if (ShouldAbsorbVirtualizedBranchLayoutInvalidation(args.Property))
        {
            return false;
        }

        return base.ShouldInvalidateArrangeForPropertyChange(args, metadata);
    }

    protected override bool TryHandleMeasureInvalidation(UIElement origin, UIElement? source, string reason)
    {
        if (ShouldAbsorbVirtualizedBranchTemplateInvalidation(origin))
        {
            InvalidateVisual();
            return true;
        }

        return base.TryHandleMeasureInvalidation(origin, source, reason);
    }

    protected override bool TryHandleArrangeInvalidation(UIElement origin, UIElement? source, string reason)
    {
        if (ShouldAbsorbVirtualizedBranchTemplateInvalidation(origin))
        {
            InvalidateVisual();
            return true;
        }

        return base.TryHandleArrangeInvalidation(origin, source, reason);
    }

    internal override UIElement ResolveTemplateTriggerInvalidationTarget(UIElement changedTarget)
    {
        return UseVirtualizedTreeLayout ? this : base.ResolveTemplateTriggerInvalidationTarget(changedTarget);
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

        if (element is not TreeViewItem treeViewItem)
        {
            return;
        }

        ApplyTypographyToItem(treeViewItem, null, Foreground);
    }

    protected override void OnItemsIncrementalChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsIncrementalChanged(e);
        UpdateHasItems();
    }

    private void UpdateHasItems()
    {
        HasItems = _hasVirtualizedChildItems || ItemContainers.Count > 0;
    }

    internal void ApplyVirtualizedBranchState(bool hasChildren, bool isExpanded)
    {
        if (_hasVirtualizedChildItems == hasChildren &&
            IsExpanded == isExpanded)
        {
            return;
        }

        _suppressExpanderPresentationUpdates = true;
        _isApplyingVirtualizedBranchState = true;
        try
        {
            HasVirtualizedChildItems = hasChildren;
            IsExpanded = isExpanded;
        }
        finally
        {
            _isApplyingVirtualizedBranchState = false;
            _suppressExpanderPresentationUpdates = false;
        }

        UpdateExpanderPresentation();
    }

    internal void ClearVirtualizedBranchStateForRecycle()
    {
        _suppressExpanderPresentationUpdates = true;
        _isApplyingVirtualizedBranchState = true;
        try
        {
            _hasVirtualizedChildItems = false;
            HasItems = false;
        }
        finally
        {
            _isApplyingVirtualizedBranchState = false;
            _suppressExpanderPresentationUpdates = false;
        }
    }

    private void UpdateExpanderPresentation()
    {
        if (!HasItems)
        {
            ExpanderGlyphVisibility = Visibility.Collapsed;
            return;
        }

        CurrentExpanderGlyph = IsExpanded ? ExpandedExpanderGlyph : CollapsedExpanderGlyph;
        ExpanderGlyphVisibility = Visibility.Visible;
    }

    private bool ShouldAbsorbVirtualizedBranchLayoutInvalidation(DependencyProperty property)
    {
        return _isApplyingVirtualizedBranchState &&
               UseVirtualizedTreeLayout &&
               (property == HasItemsProperty || property == IsExpandedProperty);
    }

    private bool ShouldAbsorbVirtualizedBranchTemplateInvalidation(UIElement origin)
    {
        return _isApplyingVirtualizedBranchState &&
               UseVirtualizedTreeLayout &&
               !ReferenceEquals(origin, this) &&
               IsVisualDescendantOfThis(origin);
    }

    private bool IsVisualDescendantOfThis(UIElement element)
    {
        for (UIElement? current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

}

