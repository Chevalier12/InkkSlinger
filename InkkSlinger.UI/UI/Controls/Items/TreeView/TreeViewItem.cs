using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class TreeViewItem : ItemsControl
{
    internal event EventHandler? ExpandedStateChanged;
    private bool _isApplyingPropagatedForeground;
    private VirtualizedDisplaySnapshot? _virtualizedDisplaySnapshot;
    private UIElement? _virtualizedHeaderElement;
    private float _virtualizedHeaderMinRowHeight;

    private readonly record struct VirtualizedDisplaySnapshot(
        string Header,
        bool HasChildren,
        bool IsExpanded,
        bool IsSelected);

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

    protected override bool IncludeGeneratedChildrenInVisualTree => IsExpanded && !UseVirtualizedTreeLayout;

    internal bool UseVirtualizedTreeLayout { get; set; }

    internal bool HasVirtualizedChildItems { get; set; }

    internal object? VirtualizedTreeDataItem { get; set; }

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
        if (_virtualizedDisplaySnapshot.HasValue)
        {
            yield break;
        }

        if (_virtualizedHeaderElement != null)
        {
            yield return _virtualizedHeaderElement;
        }

        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return _virtualizedDisplaySnapshot.HasValue
            ? 0
            : base.GetVisualChildCountForTraversal() + (_virtualizedHeaderElement != null ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        if (_virtualizedDisplaySnapshot.HasValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_virtualizedHeaderElement != null)
        {
            if (index == 0)
            {
                return _virtualizedHeaderElement;
            }

            index--;
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

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property != IsExpandedProperty)
        {
            return;
        }

        ExpandedStateChanged?.Invoke(this, EventArgs.Empty);
        UiRoot.Current?.NotifyVisualStructureChanged(this, VisualParent, VisualParent);
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

}

