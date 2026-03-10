using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class TreeViewItem : ItemsControl
{
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

    public new static readonly DependencyProperty FontProperty = Control.FontProperty;

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
                        args.OldValue is Color oldColor &&
                        args.NewValue is Color newColor)
                    {
                        treeViewItem.PropagateTypographyToChildren(
                            null,
                            null,
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

    protected override bool IncludeGeneratedChildrenInVisualTree => IsExpanded;

    public string Header
    {
        get => GetValue<string>(HeaderProperty) ?? string.Empty;
        set => SetValue(HeaderProperty, value);
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

        ApplyTypographyToItem(treeViewItem, null, Font, null, Foreground);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var rowHeight = GetRowHeight();
        var rowWidth = MeasureHeaderWidth();

        if (!IsExpanded)
        {
            return new Vector2(rowWidth, rowHeight);
        }

        var maxWidth = rowWidth;
        var totalHeight = rowHeight;

        foreach (var child in ItemContainers)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(new Vector2(MathF.Max(0f, availableSize.X - Indent), availableSize.Y));
            maxWidth = MathF.Max(maxWidth, Indent + element.DesiredSize.X);
            totalHeight += element.DesiredSize.Y;
        }

        return new Vector2(maxWidth, totalHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var currentY = LayoutSlot.Y + GetRowHeight();

        if (IsExpanded)
        {
            foreach (var child in ItemContainers)
            {
                if (child is not FrameworkElement element)
                {
                    continue;
                }

                var height = element.DesiredSize.Y;
                element.Arrange(new LayoutRect(
                    LayoutSlot.X + Indent,
                    currentY,
                    MathF.Max(0f, finalSize.X - Indent),
                    height));
                currentY += height;
            }
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var rowHeight = GetRowHeight();
        var padding = Padding;
        var rowRect = new LayoutRect(LayoutSlot.X, LayoutSlot.Y, LayoutSlot.Width, rowHeight);
        if (IsSelected)
        {
            UiDrawing.DrawFilledRect(spriteBatch, rowRect, SelectedBackground, Opacity);
        }

        if (HasChildItems())
        {
            var glyphX = LayoutSlot.X + padding.Left + 4f;
            var glyphY = LayoutSlot.Y + ((rowHeight - 8f) / 2f);
            UiDrawing.DrawRectStroke(spriteBatch, new LayoutRect(glyphX, glyphY, 8f, 8f), 1f, Foreground, Opacity);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(glyphX + 2f, glyphY + 3f, 4f, 1f), Foreground, Opacity);
            if (!IsExpanded)
            {
                UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(glyphX + 3f, glyphY + 2f, 1f, 4f), Foreground, Opacity);
            }
        }

        if (!string.IsNullOrEmpty(Header))
        {
            var textX = LayoutSlot.X + padding.Left + (HasChildItems() ? 16f : 6f);
            var textY = LayoutSlot.Y + ((rowHeight - FontStashTextRenderer.GetLineHeight(Font, FontSize)) / 2f);
            FontStashTextRenderer.DrawString(spriteBatch, Font, Header, new Vector2(textX, textY), Foreground * Opacity, FontSize);
        }
    }

    public bool HitExpander(Vector2 point)
    {
        if (!HasChildItems())
        {
            return false;
        }

        var rowHeight = GetRowHeight();
        var rect = new LayoutRect(LayoutSlot.X + 4f, LayoutSlot.Y + ((rowHeight - 10f) / 2f), 10f, 10f);
        return point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }

    public bool HasChildItems()
    {
        return ItemContainers.Count > 0;
    }

    public IReadOnlyList<TreeViewItem> GetChildTreeItems()
    {
        var result = new List<TreeViewItem>();
        foreach (var child in ItemContainers)
        {
            if (child is TreeViewItem treeViewItem)
            {
                result.Add(treeViewItem);
            }
        }

        return result;
    }

    private float GetRowHeight()
    {
        var padding = Padding;
        return MathF.Max(18f, FontStashTextRenderer.GetLineHeight(Font, FontSize) + 4f + padding.Vertical);
    }

    private float MeasureHeaderWidth()
    {
        var padding = Padding;
        var textWidth = !string.IsNullOrEmpty(Header)
            ? FontStashTextRenderer.MeasureWidth(Font, Header, FontSize)
            : 0f;
        return padding.Horizontal + (HasChildItems() ? 20f : 10f) + textWidth;
    }

    private void PropagateTypographyToChildren(
        SpriteFont? oldFont,
        SpriteFont? newFont,
        Color? oldForeground,
        Color? newForeground)
    {
        foreach (var child in GetChildTreeItems())
        {
            ApplyTypographyRecursive(child, oldFont, newFont, oldForeground, newForeground);
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
            if (!item.HasLocalValue(FontProperty) || Equals(item.Font, oldFont))
            {
                if (newFont != null)
                {
                    item.Font = newFont;
                }
                else
                {
                    item.ClearValue(FontProperty);
                }
            }
        }

        if (newForeground.HasValue && oldForeground.HasValue)
        {
            if (!item.HasLocalValue(ForegroundProperty) || item.Foreground == oldForeground.Value)
            {
                item.Foreground = newForeground.Value;
            }
        }
    }
}

