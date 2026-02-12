using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(TreeView),
            new FrameworkPropertyMetadata(new Color(18, 18, 18), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(TreeView),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(TreeView),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(TreeView),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is TreeView treeView)
                    {
                        treeView.PropagateTypographyFromTree(
                            args.OldValue as SpriteFont,
                            args.NewValue as SpriteFont,
                            null,
                            null);
                    }
                }));

    public static readonly DependencyProperty ForegroundProperty =
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

    public TreeView()
    {
        Focusable = true;
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

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override void OnPreviewMouseDown(RoutedMouseButtonEventArgs args)
    {
        base.OnPreviewMouseDown(args);

        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        if (FindItemFromSource(args.OriginalSource) is not TreeViewItem item)
        {
            return;
        }

        if (item.HitExpander(args.Position))
        {
            item.IsExpanded = !item.IsExpanded;
        }
        else
        {
            ApplySelectedItem(item);
            Focus();
        }

        args.Handled = true;
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        // Ignore auto-repeat pulses so one key press advances a single step.
        if (args.IsRepeat)
        {
            return;
        }

        var visible = GetVisibleItems();
        if (visible.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedItem == null ? -1 : visible.IndexOf(SelectedItem);
        var handled = true;

        switch (args.Key)
        {
            case Keys.Down:
                ApplySelectedItem(visible[System.Math.Min(visible.Count - 1, currentIndex < 0 ? 0 : currentIndex + 1)]);
                break;
            case Keys.Up:
                ApplySelectedItem(visible[System.Math.Max(0, currentIndex <= 0 ? 0 : currentIndex - 1)]);
                break;
            case Keys.Right:
                if (SelectedItem != null)
                {
                    if (SelectedItem.HasChildItems() && !SelectedItem.IsExpanded)
                    {
                        SelectedItem.IsExpanded = true;
                    }
                    else
                    {
                        var firstChild = GetFirstChild(SelectedItem);
                        if (firstChild != null)
                        {
                            ApplySelectedItem(firstChild);
                        }
                    }
                }

                break;
            case Keys.Left:
                if (SelectedItem != null)
                {
                    if (SelectedItem.IsExpanded)
                    {
                        SelectedItem.IsExpanded = false;
                    }
                    else
                    {
                        var parent = GetParentTreeItem(SelectedItem);
                        if (parent != null)
                        {
                            ApplySelectedItem(parent);
                        }
                    }
                }

                break;
            case Keys.Home:
                ApplySelectedItem(visible[0]);
                break;
            case Keys.End:
                ApplySelectedItem(visible[visible.Count - 1]);
                break;
            case Keys.Enter:
            case Keys.Space:
                if (SelectedItem != null && SelectedItem.HasChildItems())
                {
                    SelectedItem.IsExpanded = !SelectedItem.IsExpanded;
                }

                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            args.Handled = true;
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
