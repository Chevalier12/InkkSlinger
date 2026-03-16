using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
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
                        _ = isVirtualizing;
                        listBox.UpdateItemsHost();
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

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ListBox),
            new FrameworkPropertyMetadata(new Color(18, 18, 18), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ListBox),
            new FrameworkPropertyMetadata(new Color(88, 88, 88), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ListBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

    private readonly ScrollViewer _fallbackScrollViewer;
    private ScrollViewer? _templatedScrollViewer;
    private Panel _itemsHost;

    public ListBox()
    {
        _itemsHost = CreateItemsHost();
        AttachItemsHost(_itemsHost);
        _fallbackScrollViewer = new ScrollViewer
        {
            Content = _itemsHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineScrollAmount = 24f,
            BorderThickness = 0f,
            Background = Color.Transparent
        };

        _fallbackScrollViewer.SetVisualParent(this);
        _fallbackScrollViewer.SetLogicalParent(this);

        AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnMouseDownSelectItem);
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
            Content = ResolveDisplayTextForItem(item)
        };

        return container;
    }

    protected override UIElement? BuildContainerForTemplatedItemOverride(object? item, DataTemplate selectedTemplate)
    {
        var container = CreateContainerForItemOverride(item ?? string.Empty);
        if (container is not ContentControl contentControl)
        {
            return null;
        }

        contentControl.ContentTemplate = selectedTemplate;
        contentControl.Content = item;
        return container;
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is ListBoxItem listBoxItem)
        {
            listBoxItem.IsSelected = IsSelectedIndex(SelectedIndices, index);

            if (listBoxItem.Content is Label label)
            {
                // Fast-path for the common text item template.
                label.FontFamily = FontFamily;
                label.FontSize = FontSize;
                label.FontWeight = FontWeight;
                label.FontStyle = FontStyle;
                return;
            }
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

        EnsureSelectedItemIsFullyVisible();
    }

    internal bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsEnabled || ItemContainers.Count == 0)
        {
            return false;
        }

        var shift = (modifiers & ModifierKeys.Shift) != 0;
        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var selectionIndex = SelectedIndex;
        if (selectionIndex < 0)
        {
            selectionIndex = 0;
        }

        switch (key)
        {
            case Keys.Up:
                return MoveSelectionByDelta(-1, shift, ctrl);
            case Keys.Down:
                return MoveSelectionByDelta(1, shift, ctrl);
            case Keys.Home:
                return MoveSelectionToIndex(0, shift, ctrl);
            case Keys.End:
                return MoveSelectionToIndex(ItemContainers.Count - 1, shift, ctrl);
            case Keys.PageUp:
                return MoveSelectionByDelta(-EstimatePageStep(), shift, ctrl);
            case Keys.PageDown:
                return MoveSelectionByDelta(EstimatePageStep(), shift, ctrl);
            case Keys.Space:
                if ((SelectionMode == SelectionMode.Multiple || SelectionMode == SelectionMode.Extended) && ctrl)
                {
                    ToggleSelectedIndexInternal(selectionIndex);
                    SetSelectionAnchorInternal(selectionIndex);
                    EnsureItemIsFullyVisible(selectionIndex);
                    return true;
                }

                return false;
            case Keys.A:
                if (ctrl && SelectionMode != SelectionMode.Single)
                {
                    SelectAllInternal();
                    EnsureSelectedItemIsFullyVisible();
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    public void ScrollIntoView(object? item)
    {
        if (item == null)
        {
            return;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            if (Equals(Items[i], item))
            {
                EnsureItemIsFullyVisible(i);
                return;
            }
        }

        if (ItemsSourceView != null)
        {
            var index = 0;
            foreach (var projectedItem in ItemsSourceView)
            {
                if (Equals(projectedItem, item))
                {
                    EnsureItemIsFullyVisible(index);
                    return;
                }

                index++;
            }
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
        else if (args.Property == LineScrollAmountProperty && args.NewValue is float amount)
        {
            ActiveScrollViewer.LineScrollAmount = amount;
        }
        else if (args.Property == ItemsPanelProperty)
        {
            UpdateItemsHost();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var templateDesired = base.MeasureOverride(availableSize);
        if (HasTemplateRoot)
        {
            return templateDesired;
        }

        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - border);

        var scrollViewer = ActiveScrollViewer;
        scrollViewer.Measure(new Vector2(innerWidth, innerHeight));
        var scrollDesired = scrollViewer.DesiredSize;
        return new Vector2(
            MathF.Max(0f, scrollDesired.X + border),
            MathF.Max(0f, scrollDesired.Y + border));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (HasTemplateRoot)
        {
            return base.ArrangeOverride(finalSize);
        }

        var border = BorderThickness;
        var width = MathF.Max(0f, finalSize.X - (border * 2f));
        var height = MathF.Max(0f, finalSize.Y - (border * 2f));
        ActiveScrollViewer.Arrange(new LayoutRect(LayoutSlot.X + border, LayoutSlot.Y + border, width, height));

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

    private void EnsureSelectedItemIsFullyVisible()
    {
        EnsureItemIsFullyVisible(SelectedIndex);
    }

    private void EnsureItemIsFullyVisible(int index)
    {
        if (index < 0 || index >= ItemContainers.Count)
        {
            return;
        }

        if (ItemContainers[index] is not FrameworkElement selectedContainer)
        {
            return;
        }

        var scrollViewer = ActiveScrollViewer;
        var viewportHeight = scrollViewer.ViewportHeight;
        if (viewportHeight <= 0f)
        {
            return;
        }

        var itemTop = selectedContainer.LayoutSlot.Y - _itemsHost.LayoutSlot.Y;
        var itemBottom = itemTop + selectedContainer.LayoutSlot.Height;
        var viewportTop = scrollViewer.VerticalOffset;
        var viewportBottom = viewportTop + viewportHeight;

        if (itemTop < viewportTop)
        {
            scrollViewer.ScrollToVerticalOffset(itemTop);
            return;
        }

        if (itemBottom > viewportBottom)
        {
            scrollViewer.ScrollToVerticalOffset(itemBottom - viewportHeight);
        }
    }

    private ScrollViewer ActiveScrollViewer => _templatedScrollViewer ?? _fallbackScrollViewer;

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
        else if (SelectionMode == SelectionMode.Multiple)
        {
            ToggleSelectedIndexInternal(index);
            SetSelectionAnchorInternal(index);
        }
        else
        {
            var shift = (args.Modifiers & ModifierKeys.Shift) != 0;
            var ctrl = (args.Modifiers & ModifierKeys.Control) != 0;
            var anchor = GetSelectionAnchorIndexInternal();
            if (shift)
            {
                if (anchor < 0)
                {
                    anchor = SelectedIndex >= 0 ? SelectedIndex : index;
                    SetSelectionAnchorInternal(anchor);
                }

                SelectRangeInternal(anchor, index, clearExisting: !ctrl);
            }
            else if (ctrl)
            {
                ToggleSelectedIndexInternal(index);
                SetSelectionAnchorInternal(index);
            }
            else
            {
                SelectOnlyIndexInternal(index);
            }
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

    private Panel CreateItemsHost()
    {
        if (ItemsPanel != null)
        {
            return ItemsPanel.Build(this);
        }

        if (IsVirtualizing)
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

    private void ConfigureScrollViewer(ScrollViewer viewer)
    {
        viewer.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
        viewer.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        viewer.LineScrollAmount = LineScrollAmount;
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

    private bool MoveSelectionByDelta(int delta, bool extendSelection, bool preserveExisting)
    {
        var currentIndex = SelectedIndex;
        if (extendSelection && SelectedIndices.Count > 0)
        {
            currentIndex = delta >= 0
                ? SelectedIndices[SelectedIndices.Count - 1]
                : SelectedIndices[0];
        }

        if (currentIndex < 0)
        {
            currentIndex = delta >= 0 ? 0 : ItemContainers.Count - 1;
        }

        return MoveSelectionToIndex(currentIndex + delta, extendSelection, preserveExisting);
    }

    private bool MoveSelectionToIndex(int targetIndex, bool extendSelection, bool preserveExisting)
    {
        if (ItemContainers.Count == 0)
        {
            return false;
        }

        var clampedIndex = Math.Clamp(targetIndex, 0, ItemContainers.Count - 1);
        if (SelectionMode == SelectionMode.Single)
        {
            SelectOnlyIndexInternal(clampedIndex);
            EnsureItemIsFullyVisible(clampedIndex);
            return true;
        }

        if (extendSelection)
        {
            var anchor = GetSelectionAnchorIndexInternal();
            if (anchor < 0)
            {
                anchor = SelectedIndex >= 0 ? SelectedIndex : clampedIndex;
                SetSelectionAnchorInternal(anchor);
            }

            SelectRangeInternal(anchor, clampedIndex, clearExisting: !preserveExisting);
            EnsureItemIsFullyVisible(clampedIndex);
            return true;
        }

        if (SelectionMode == SelectionMode.Multiple && preserveExisting)
        {
            EnsureItemIsFullyVisible(clampedIndex);
            return true;
        }

        SelectOnlyIndexInternal(clampedIndex);
        EnsureItemIsFullyVisible(clampedIndex);
        return true;
    }

    private int EstimatePageStep()
    {
        if (ItemContainers.Count == 0)
        {
            return 1;
        }

        var sampleHeight = 0f;
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is FrameworkElement element && element.LayoutSlot.Height > 0f)
            {
                sampleHeight = element.LayoutSlot.Height;
                break;
            }
        }

        if (sampleHeight <= 0f)
        {
            sampleHeight = LineScrollAmount > 0f ? LineScrollAmount : 24f;
        }

        return Math.Max(1, (int)MathF.Floor(ActiveScrollViewer.ViewportHeight / sampleHeight));
    }

    private sealed class ScrollContentStackPanel : StackPanel, IScrollTransformContent
    {
        protected override bool TryGetLocalRenderTransform(out Matrix transform, out Matrix inverseTransform)
        {
            var hasBaseTransform = base.TryGetLocalRenderTransform(out var baseTransform, out var baseInverseTransform);
            var viewer = FindAncestorScrollViewer();
            if (viewer == null)
            {
                transform = baseTransform;
                inverseTransform = baseInverseTransform;
                return hasBaseTransform;
            }

            var offsetX = -viewer.HorizontalOffset;
            var offsetY = -viewer.VerticalOffset;
            if (MathF.Abs(offsetX) <= 0.01f && MathF.Abs(offsetY) <= 0.01f)
            {
                transform = baseTransform;
                inverseTransform = baseInverseTransform;
                return hasBaseTransform;
            }

            var localScrollTransform = Matrix.CreateTranslation(offsetX, offsetY, 0f);
            var localScrollInverseTransform = Matrix.CreateTranslation(-offsetX, -offsetY, 0f);
            return TryComposeLocalTransforms(
                hasPrimaryTransform: localScrollTransform != Matrix.Identity,
                primaryTransform: localScrollTransform,
                primaryInverse: localScrollInverseTransform,
                hasSecondaryTransform: hasBaseTransform,
                secondaryTransform: baseTransform,
                secondaryInverse: baseInverseTransform,
                out transform,
                out inverseTransform);
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
