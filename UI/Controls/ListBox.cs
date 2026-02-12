using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;

namespace InkkSlinger;

public class ListBox : Selector
{
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

    public ListBox()
    {
        Focusable = true;
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
}
