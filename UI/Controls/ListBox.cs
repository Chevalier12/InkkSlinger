using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public class ListBox : Selector
{
    private static readonly bool EnableListBoxTrace = false;

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ListBox),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(ListBox),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Auto, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty LineScrollAmountProperty =
        DependencyProperty.Register(
            nameof(LineScrollAmount),
            typeof(float),
            typeof(ListBox),
            new FrameworkPropertyMetadata(
                24f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float amount && amount > 0f ? amount : 1f));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ListBox),
            new FrameworkPropertyMetadata(new Color(18, 18, 18), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ListBox),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ListBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    private readonly ScrollViewer _scrollViewer;
    private readonly ItemsPresenter _itemsPresenter;

    public ListBox()
    {
        Focusable = true;

        _itemsPresenter = new ItemsPresenter();
        _scrollViewer = new ScrollViewer
        {
            Content = _itemsPresenter,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            BorderThickness = 0f,
            Background = Color.Transparent
        };

        _scrollViewer.SetVisualParent(this);
        _scrollViewer.SetLogicalParent(this);
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

    public float LineScrollAmount
    {
        get => GetValue<float>(LineScrollAmountProperty);
        set => SetValue(LineScrollAmountProperty, value);
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

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var element in base.GetVisualChildren())
        {
            yield return element;
        }

        yield return _scrollViewer;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var element in base.GetLogicalChildren())
        {
            yield return element;
        }

        yield return _scrollViewer;
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is ListBoxItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        var container = new ListBoxItem();

        if (item is UIElement element)
        {
            container.Content = element;
            return container;
        }

        container.Content = new Label
        {
            Text = item?.ToString() ?? string.Empty
        };

        return container;
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is ListBoxItem listBoxItem)
        {
            listBoxItem.IsSelected = SelectedIndices.Contains(index);
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        base.OnSelectionChanged(args);

        var selectedIndices = new HashSet<int>(SelectedIndices);
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is ListBoxItem listBoxItem)
            {
                listBoxItem.IsSelected = selectedIndices.Contains(i);
            }
        }
    }

    protected override void OnPreviewMouseDown(RoutedMouseButtonEventArgs args)
    {
        base.OnPreviewMouseDown(args);

        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        var controlPressed = (args.Modifiers & ModifierKeys.Control) != 0;
        var shiftPressed = (args.Modifiers & ModifierKeys.Shift) != 0;

        var source = args.OriginalSource;
        var selectedIndex = -1;
        while (source != null && !ReferenceEquals(source, this))
        {
            selectedIndex = IndexFromContainer(source);
            if (selectedIndex >= 0)
            {
                break;
            }

            source = source.VisualParent;
        }

        if (selectedIndex >= 0)
        {
            ApplySelectionFromInput(selectedIndex, shiftPressed, controlPressed);
            Focus();
        }
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);
        Trace($"DependencyChanged {args.Property.Name} old={args.OldValue ?? "null"} new={args.NewValue ?? "null"}");

        if (args.Property == HorizontalScrollBarVisibilityProperty && args.NewValue is ScrollBarVisibility h)
        {
            _scrollViewer.HorizontalScrollBarVisibility = h;
        }
        else if (args.Property == VerticalScrollBarVisibilityProperty && args.NewValue is ScrollBarVisibility v)
        {
            _scrollViewer.VerticalScrollBarVisibility = v;
        }
        else if (args.Property == LineScrollAmountProperty && args.NewValue is float amount)
        {
            _scrollViewer.LineScrollAmount = amount;
        }
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (!IsEnabled)
        {
            return;
        }

        // Ignore auto-repeat navigation pulses so a single press advances one item.
        if (args.IsRepeat)
        {
            return;
        }

        var handled = false;
        var selected = SelectedIndex;
        var shiftPressed = (args.Modifiers & ModifierKeys.Shift) != 0;
        var targetIndex = -1;

        if (args.Key == Keys.Up)
        {
            targetIndex = System.Math.Max(0, selected - 1);
        }
        else if (args.Key == Keys.Down)
        {
            var next = selected < 0 ? 0 : selected + 1;
            targetIndex = System.Math.Min(Items.Count - 1, next);
        }
        else if (args.Key == Keys.Home)
        {
            targetIndex = 0;
        }
        else if (args.Key == Keys.End)
        {
            targetIndex = Items.Count - 1;
        }

        if (targetIndex >= 0)
        {
            if (SelectionMode == SelectionMode.Multiple && shiftPressed)
            {
                var anchor = GetSelectionAnchorIndexInternal();
                if (anchor < 0)
                {
                    anchor = selected >= 0 ? selected : targetIndex;
                    SetSelectionAnchorInternal(anchor);
                }

                SelectRangeInternal(anchor, targetIndex, clearExisting: true);
            }
            else
            {
                if (SelectionMode == SelectionMode.Multiple)
                {
                    SelectRangeInternal(targetIndex, targetIndex, clearExisting: true);
                }
                else
                {
                    SetSelectedIndexInternal(targetIndex);
                }

                SetSelectionAnchorInternal(targetIndex);
            }

            handled = true;
        }

        if (handled)
        {
            args.Handled = true;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        Trace($"Measure start available={availableSize} items={Items.Count} containers={ItemContainers.Count}");
        var desired = base.MeasureOverride(availableSize);

        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - border);

        _scrollViewer.Measure(new Vector2(innerWidth, innerHeight));
        var scrollDesired = _scrollViewer.DesiredSize;

        desired.X = MathF.Max(desired.X, scrollDesired.X + border);
        desired.Y = MathF.Max(desired.Y, scrollDesired.Y + border);
        Trace($"Measure end desired={desired} scrollDesired={scrollDesired}");
        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        Trace($"Arrange start final={finalSize} items={Items.Count} containers={ItemContainers.Count}");
        base.ArrangeOverride(finalSize);

        var border = BorderThickness;
        var width = MathF.Max(0f, finalSize.X - (border * 2f));
        var height = MathF.Max(0f, finalSize.Y - (border * 2f));
        _scrollViewer.Arrange(new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, width, height));
        Trace($"Arrange end viewport=({width:0.##},{height:0.##})");

        return finalSize;
    }

    private void ApplySelectionFromInput(int selectedIndex, bool shiftPressed, bool controlPressed)
    {
        if (SelectionMode != SelectionMode.Multiple)
        {
            SetSelectedIndexInternal(selectedIndex);
            SetSelectionAnchorInternal(selectedIndex);
            return;
        }

        if (shiftPressed)
        {
            var anchor = GetSelectionAnchorIndexInternal();
            if (anchor < 0)
            {
                anchor = SelectedIndex >= 0 ? SelectedIndex : selectedIndex;
                SetSelectionAnchorInternal(anchor);
            }

            SelectRangeInternal(anchor, selectedIndex, clearExisting: !controlPressed);
            return;
        }

        if (controlPressed)
        {
            ToggleSelectedIndexInternal(selectedIndex);
            SetSelectionAnchorInternal(selectedIndex);
            return;
        }

        if (SelectedIndices.Contains(selectedIndex))
        {
            ToggleSelectedIndexInternal(selectedIndex);
            return;
        }

        SelectRangeInternal(selectedIndex, selectedIndex, clearExisting: true);
        SetSelectionAnchorInternal(selectedIndex);
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

    private void Trace(string message)
    {
        if (!EnableListBoxTrace)
        {
            return;
        }

        Console.WriteLine($"[ListBox#{GetHashCode():X8}] t={Environment.TickCount64} {message}");
    }
}
