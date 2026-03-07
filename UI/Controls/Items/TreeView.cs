using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

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

    public new static readonly DependencyProperty FontProperty = Control.FontProperty;

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
                            null,
                            null,
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

    public TreeView()
    {
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnMouseDownSelectItem);
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

    public new SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
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
            ApplyTypographyToItem(treeViewItem, null, Font, null, Foreground);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var padding = Padding;
        var border = BorderThickness;
        var innerSize = new Vector2(
            MathF.Max(0f, availableSize.X - (border * 2f) - padding.Horizontal),
            MathF.Max(0f, availableSize.Y - (border * 2f) - padding.Vertical));

        var desired = base.MeasureOverride(innerSize);
        return new Vector2(
            desired.X + (border * 2f) + padding.Horizontal,
            desired.Y + (border * 2f) + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var padding = Padding;
        var innerX = LayoutSlot.X + border + padding.Left;
        var innerY = LayoutSlot.Y + border + padding.Top;
        var innerWidth = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);
        var innerHeight = MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical);

        var y = innerY;
        foreach (var child in ItemContainers)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            var height = element.DesiredSize.Y;
            element.Arrange(new LayoutRect(innerX, y, innerWidth, height));
            y += height;
            if (y > innerY + innerHeight)
            {
                break;
            }
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }
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

    private void OnMouseDownSelectItem(object? sender, MouseRoutedEventArgs args)
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

        if (clickedItem.HitExpander(args.Position))
        {
            clickedItem.IsExpanded = !clickedItem.IsExpanded;
        }

        ApplySelectedItem(clickedItem);
        args.Handled = true;
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

    private List<TreeViewItem> GetVisibleItems()
    {
        var result = new List<TreeViewItem>();
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem root)
            {
                continue;
            }

            AddVisible(root, result);
        }

        return result;
    }

    private static void AddVisible(TreeViewItem item, IList<TreeViewItem> output)
    {
        output.Add(item);

        if (!item.IsExpanded)
        {
            return;
        }

        foreach (var childItem in item.GetChildTreeItems())
        {
            AddVisible(childItem, output);
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
        SpriteFont? oldFont,
        SpriteFont? newFont,
        Color? oldForeground,
        Color? newForeground)
    {
        foreach (var container in ItemContainers)
        {
            if (container is not TreeViewItem item)
            {
                continue;
            }

            ApplyTypographyRecursive(item, oldFont, newFont, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyRecursive(
        TreeViewItem item,
        SpriteFont? oldFont,
        SpriteFont? newFont,
        Color? oldForeground,
        Color? newForeground)
    {
        ApplyTypographyToItem(item, oldFont, newFont, oldForeground, newForeground);
        foreach (var child in item.GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldFont, newFont, oldForeground, newForeground);
        }
    }

    private static void ApplyTypographyToItem(
        TreeViewItem item,
        SpriteFont? oldFont,
        SpriteFont? newFont,
        Color? oldForeground,
        Color? newForeground)
    {
        if (newFont != null || oldFont != null)
        {
            if (!item.HasLocalValue(TreeViewItem.FontProperty) || Equals(item.Font, oldFont))
            {
                if (newFont != null)
                {
                    item.Font = newFont;
                }
                else
                {
                    item.ClearValue(TreeViewItem.FontProperty);
                }
            }
        }

        if (newForeground.HasValue && oldForeground.HasValue)
        {
            if (!item.HasLocalValue(TreeViewItem.ForegroundProperty) || item.Foreground == oldForeground.Value)
            {
                item.Foreground = newForeground.Value;
            }
        }
    }
}

