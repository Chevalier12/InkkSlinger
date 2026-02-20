using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ComboBox : Selector
{
    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ComboBox comboBox && args.NewValue is bool isOpen)
                    {
                        comboBox.OnIsDropDownOpenChanged(isOpen);
                    }
                }));

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        DependencyProperty.Register(
            nameof(MaxDropDownHeight),
            typeof(float),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                220f,
                FrameworkPropertyMetadataOptions.None,
                coerceValueCallback: static (_, value) => value is float f && f >= 40f ? f : 40f));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Color(30, 30, 30), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Color(128, 128, 128), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Thickness(8f, 5f, 8f, 5f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private Popup? _dropDownPopup;
    private ListBox? _dropDownList;
    private bool _isSynchronizingDropDown;

    protected override bool IncludeGeneratedChildrenInVisualTree => false;

    public ComboBox()
    {
    }

    public bool IsDropDownOpen
    {
        get => GetValue<bool>(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public float MaxDropDownHeight
    {
        get => GetValue<float>(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
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

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected internal ListBox? DropDownListForTesting => _dropDownList;
    protected internal bool IsDropDownPopupOpenForTesting => _dropDownPopup?.IsOpen ?? false;

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        if (!HitTest(pointerPosition))
        {
            return false;
        }

        IsDropDownOpen = !IsDropDownOpen;
        return true;
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();
        RefreshDropDownItems();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        base.OnSelectionChanged(args);

        if (_dropDownList != null && !_isSynchronizingDropDown)
        {
            _dropDownList.SelectedIndex = SelectedIndex;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        var padding = Padding;
        var border = BorderThickness * 2f;

        var text = GetDisplayText(SelectedItem);
        var textWidth = 0f;
        var textHeight = FontStashTextRenderer.GetLineHeight(Font);
        if (Font != null && !string.IsNullOrEmpty(text))
        {
            textWidth = FontStashTextRenderer.MeasureWidth(Font, text);
        }

        desired.X = MathF.Max(desired.X, padding.Horizontal + border + textWidth + 20f);
        desired.Y = MathF.Max(desired.Y, padding.Vertical + border + MathF.Max(textHeight, 16f));
        return desired;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }

        var arrowWidth = 16f;
        var arrowRect = new LayoutRect(slot.X + slot.Width - arrowWidth - 6f, slot.Y + 4f, arrowWidth, MathF.Max(0f, slot.Height - 8f));
        UiDrawing.DrawRectStroke(spriteBatch, arrowRect, 1f, BorderBrush, Opacity);
        var arrowColor = Foreground * Opacity;
        var centerX = arrowRect.X + (arrowRect.Width / 2f);
        var centerY = arrowRect.Y + (arrowRect.Height / 2f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(centerX - 3f, centerY - 1f, 6f, 1f), arrowColor, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(centerX - 2f, centerY, 4f, 1f), arrowColor, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(centerX - 1f, centerY + 1f, 2f, 1f), arrowColor, 1f);

        if (Font == null)
        {
            return;
        }

        var text = GetDisplayText(SelectedItem);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textX = slot.X + Padding.Left + BorderThickness;
        var textY = slot.Y + ((slot.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(textX, textY), Foreground * Opacity);
    }



    private void OnIsDropDownOpenChanged(bool isOpen)
    {
        if (_isSynchronizingDropDown)
        {
            return;
        }

        if (isOpen)
        {
            OpenDropDown();
            return;
        }

        CloseDropDown();
    }

    private void OpenDropDown()
    {
        var host = FindHostPanel();
        if (host == null)
        {
            _isSynchronizingDropDown = true;
            try
            {
                IsDropDownOpen = false;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }

            return;
        }

        EnsureDropDownControls();
        RefreshDropDownItems();

        if (_dropDownPopup == null || _dropDownList == null)
        {
            return;
        }

        _isSynchronizingDropDown = true;
        try
        {
            RefreshDropDownItems();
            _dropDownList.SelectedIndex = SelectedIndex;
        }
        finally
        {
            _isSynchronizingDropDown = false;
        }

        _dropDownPopup.PlacementTarget = this;
        _dropDownPopup.PlacementMode = PopupPlacementMode.Bottom;
        _dropDownPopup.HorizontalOffset = 0f;
        _dropDownPopup.VerticalOffset = 2f;
        _dropDownPopup.Width = Math.Max(ActualWidth > 0f ? ActualWidth : Width, 80f);
        _dropDownPopup.Height = MaxDropDownHeight;

        _dropDownPopup.Show(host);
    }

    private void CloseDropDown()
    {
        _dropDownPopup?.Close();
    }

    private void EnsureDropDownControls()
    {
        if (_dropDownList == null)
        {
            _dropDownList = new ListBox
            {
                SelectionMode = SelectionMode.Single
            };
            _dropDownList.SelectionChanged += OnDropDownSelectionChanged;
        }

        if (_dropDownPopup != null)
        {
            return;
        }

        _dropDownPopup = new Popup
        {
            Title = string.Empty,
            TitleBarHeight = 0f,
            CanClose = false,
            CanDragMove = false,
            DismissOnOutsideClick = true,
            Content = _dropDownList,
            BorderThickness = 1f,
            Padding = new Thickness(0f)
        };

        _dropDownPopup.Closed += (_, _) =>
        {
            if (!IsDropDownOpen)
            {
                return;
            }

            _isSynchronizingDropDown = true;
            try
            {
                IsDropDownOpen = false;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }
        };
    }

    private void OnDropDownSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (_dropDownList == null || _isSynchronizingDropDown)
        {
            return;
        }

        var selectedIndex = _dropDownList.SelectedIndex;
        _isSynchronizingDropDown = true;
        try
        {
            SetSelectedIndexInternal(selectedIndex);
        }
        finally
        {
            _isSynchronizingDropDown = false;
        }

        IsDropDownOpen = false;
    }

    private void RefreshDropDownItems()
    {
        if (_dropDownList == null)
        {
            return;
        }

        _isSynchronizingDropDown = true;
        try
        {
            _dropDownList.Items.Clear();
            foreach (var item in Items)
            {
                _dropDownList.Items.Add(new Label
                {
                    Text = GetDisplayText(item),
                    Font = Font,
                    Foreground = Foreground
                });
            }

            _dropDownList.SelectedIndex = SelectedIndex;
        }
        finally
        {
            _isSynchronizingDropDown = false;
        }
    }

    private Panel? FindHostPanel()
    {
        Panel? host = null;
        for (var current = VisualParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                // Popups should attach to the topmost panel so they render as an overlay
                // and do not participate in local container layout (for example Grid auto rows).
                host = panel;
            }
        }

        return host;
    }

    private static string GetDisplayText(object? item)
    {
        if (item is ComboBoxItem comboBoxItem)
        {
            if (!string.IsNullOrEmpty(comboBoxItem.Text))
            {
                return comboBoxItem.Text;
            }

            if (comboBoxItem.Content is Label label)
            {
                return label.Text;
            }

            return comboBoxItem.Content?.ToString() ?? string.Empty;
        }

        if (item is Label itemLabel)
        {
            return itemLabel.Text;
        }

        return item?.ToString() ?? string.Empty;
    }
}
