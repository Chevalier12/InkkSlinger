using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ListBox : Selector
{
    public static readonly DependencyProperty IsVirtualizingProperty =
        DependencyProperty.Register(
            nameof(IsVirtualizing),
            typeof(bool),
            typeof(ListBox),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ListBox listBox && args.NewValue is bool isVirtualizing)
                    {
                        listBox.UpdateItemsHost(isVirtualizing);
                    }
                }));

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
    private Panel _itemsHost;

    public ListBox()
    {
        _itemsHost = CreateItemsHost(isVirtualizing: false);
        AttachItemsHost(_itemsHost);
        _scrollViewer = new ScrollViewer
        {
            Content = _itemsHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            BorderThickness = 0f,
            Background = Color.Transparent
        };

        _scrollViewer.SetVisualParent(this);
        _scrollViewer.SetLogicalParent(this);

        AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnMouseDownSelectItem);
    }

    public bool IsVirtualizing
    {
        get => GetValue<bool>(IsVirtualizingProperty);
        set => SetValue(IsVirtualizingProperty, value);
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
            listBoxItem.IsSelected = IsSelectedIndex(SelectedIndices, index);
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

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - border);

        _scrollViewer.Measure(new Vector2(innerWidth, innerHeight));
        var scrollDesired = _scrollViewer.DesiredSize;
        return new Vector2(
            MathF.Max(0f, scrollDesired.X + border),
            MathF.Max(0f, scrollDesired.Y + border));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var width = MathF.Max(0f, finalSize.X - (border * 2f));
        var height = MathF.Max(0f, finalSize.Y - (border * 2f));
        _scrollViewer.Arrange(new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, width, height));

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

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }

    private static bool IsSelectedIndex(IReadOnlyList<int> selectedIndices, int index)
    {
        for (var i = 0; i < selectedIndices.Count; i++)
        {
            if (selectedIndices[i] == index)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateItemsHost(bool isVirtualizing)
    {
        var nextHost = CreateItemsHost(isVirtualizing);
        if (ReferenceEquals(nextHost, _itemsHost))
        {
            return;
        }

        _itemsHost = nextHost;
        _scrollViewer.Content = _itemsHost;
        AttachItemsHost(_itemsHost);
        InvalidateMeasure();
    }

    private void OnMouseDownSelectItem(object? sender, MouseRoutedEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        var container = FindContainerFromSource(args.OriginalSource as UIElement);
        if (container == null)
        {
            return;
        }

        var index = IndexFromContainer(container);
        if (index < 0)
        {
            return;
        }

        if (SelectionMode == SelectionMode.Single)
        {
            SetSelectedIndexInternal(index);
        }
        else
        {
            ToggleSelectedIndexInternal(index);
            SetSelectionAnchorInternal(index);
        }
    }

    private ListBoxItem? FindContainerFromSource(UIElement? source)
    {
        for (var current = source; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ListBoxItem listBoxItem && IndexFromContainer(listBoxItem) >= 0)
            {
                return listBoxItem;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return null;
    }

    private static Panel CreateItemsHost(bool isVirtualizing)
    {
        if (isVirtualizing)
        {
            return new VirtualizingStackPanel
            {
                Orientation = Orientation.Vertical
            };
        }

        return new ScrollContentStackPanel
        {
            Orientation = Orientation.Vertical
        };
    }

    private sealed class ScrollContentStackPanel : StackPanel, IScrollTransformContent
    {
        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            var viewer = FindAncestorScrollViewer();
            if (viewer == null)
            {
                transform = Matrix.Identity;
                inverseTransform = Matrix.Identity;
                return false;
            }

            var offsetX = -viewer.HorizontalOffset;
            var offsetY = -viewer.VerticalOffset;
            if (MathF.Abs(offsetX) <= 0.01f && MathF.Abs(offsetY) <= 0.01f)
            {
                transform = Matrix.Identity;
                inverseTransform = Matrix.Identity;
                return false;
            }

            transform = Matrix.CreateTranslation(offsetX, offsetY, 0f);
            inverseTransform = Matrix.CreateTranslation(-offsetX, -offsetY, 0f);
            return true;
        }

        private ScrollViewer? FindAncestorScrollViewer()
        {
            for (var current = VisualParent ?? LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
            {
                if (current is ScrollViewer viewer)
                {
                    return viewer;
                }
            }

            return null;
        }
    }
}
