using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ComboBox : Selector
{
    private static long _diagConstructorCallCount;
    private static long _diagHandlePointerDownCallCount;
    private static long _diagHandlePointerDownElapsedTicks;
    private static long _diagHandlePointerDownHitCount;
    private static long _diagHandlePointerDownMissCount;
    private static long _diagHandlePointerDownOpenToggleCount;
    private static long _diagHandlePointerDownCloseToggleCount;
    private static long _diagItemsChangedCallCount;
    private static long _diagSelectionChangedCallCount;
    private static long _diagSelectionChangedElapsedTicks;
    private static long _diagSelectionChangedContainerScanCount;
    private static long _diagSelectionChangedContainerMatchCount;
    private static long _diagSelectionChangedDropDownSyncCount;
    private static long _diagSelectionChangedDropDownSyncSkippedCount;
    private static long _diagCreateContainerCallCount;
    private static long _diagPrepareContainerCallCount;
    private static long _diagPrepareContainerConfiguredFromItemCount;
    private static long _diagPrepareContainerTypographySyncCount;
    private static long _diagPrepareContainerUnexpectedElementCount;
    private static long _diagDependencyPropertyChangedCallCount;
    private static long _diagDependencyPropertyRefreshTriggerCount;
    private static long _diagMeasureOverrideCallCount;
    private static long _diagMeasureOverrideElapsedTicks;
    private static long _diagMeasureOverrideEmptyTextCount;
    private static long _diagMeasureOverrideTextMeasureCount;
    private static long _diagRenderCallCount;
    private static long _diagRenderElapsedTicks;
    private static long _diagRenderBorderDrawCount;
    private static long _diagRenderEmptyTextSkipCount;
    private static long _diagRenderTextDrawCount;
    private static long _diagDropDownOpenStateChangedCallCount;
    private static long _diagDropDownOpenStateChangedElapsedTicks;
    private static long _diagDropDownOpenStateChangedSyncSkipCount;
    private static long _diagDropDownOpenStateChangedOpenPathCount;
    private static long _diagDropDownOpenStateChangedClosePathCount;
    private static long _diagOpenDropDownCallCount;
    private static long _diagOpenDropDownElapsedTicks;
    private static long _diagOpenDropDownHostMissingCount;
    private static long _diagOpenDropDownPopupShowCount;
    private static long _diagOpenDropDownPopupUnavailableCount;
    private static long _diagCloseDropDownCallCount;
    private static long _diagCloseDropDownPopupMissingCount;
    private static long _diagEnsureDropDownControlsCallCount;
    private static long _diagEnsureDropDownControlsElapsedTicks;
    private static long _diagEnsureDropDownListCreateCount;
    private static long _diagEnsureDropDownListReuseCount;
    private static long _diagEnsureDropDownPopupCreateCount;
    private static long _diagEnsureDropDownPopupReuseCount;
    private static long _diagDropDownPopupClosedEventCount;
    private static long _diagDropDownPopupClosedSyncCloseCount;
    private static long _diagDropDownPopupClosedAlreadyClosedCount;
    private static long _diagDropDownSelectionChangedCallCount;
    private static long _diagDropDownSelectionChangedElapsedTicks;
    private static long _diagDropDownSelectionChangedNullListSkipCount;
    private static long _diagDropDownSelectionChangedSynchronizingSkipCount;
    private static long _diagDropDownSelectionChangedApplySelectionCount;
    private static long _diagRefreshDropDownItemsCallCount;
    private static long _diagRefreshDropDownItemsElapsedTicks;
    private static long _diagRefreshDropDownItemsNullListSkipCount;
    private static long _diagRefreshDropDownItemsProjectedItemCount;
    private static long _diagRefreshDropDownItemsSelectedIndexSyncCount;
    private static long _diagFindHostPanelCallCount;
    private static long _diagFindHostPanelFoundCount;
    private static long _diagFindHostPanelMissingCount;
    private static long _diagGetDisplayTextCallCount;
    private static long _diagGetDisplayTextElapsedTicks;
    private static long _diagGetDisplayTextComboBoxItemTextCount;
    private static long _diagGetDisplayTextComboBoxItemLabelCount;
    private static long _diagGetDisplayTextComboBoxItemContentToStringCount;
    private static long _diagGetDisplayTextListBoxItemLabelCount;
    private static long _diagGetDisplayTextLabelCount;
    private static long _diagGetDisplayTextResolveDisplayPathCount;
    private static long _diagGetDisplayTextEmptyResultCount;
    private static long _diagBuildDropDownContainerCallCount;
    private static long _diagConfigureContainerFromItemCallCount;
    private static long _diagSyncContainerTypographyCallCount;
    private static long _diagSyncContainerTypographyStyleSkipCount;
    private static long _diagSyncContainerTypographyForegroundSetCount;
    private static long _diagSyncContainerTypographyFontFamilySetCount;
    private static long _diagSyncContainerTypographyFontSizeSetCount;
    private static long _diagSyncContainerTypographyFontWeightSetCount;
    private static long _diagSyncContainerTypographyFontStyleSetCount;

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

    public static readonly DependencyProperty DropDownListStyleProperty =
        DependencyProperty.Register(
            nameof(DropDownListStyle),
            typeof(Style),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty DropDownPopupStyleProperty =
        DependencyProperty.Register(
            nameof(DropDownPopupStyle),
            typeof(Style),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(null));

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Color(30, 30, 30), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Color(128, 128, 128), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float thickness && thickness >= 0f ? thickness : 0f));

    public new static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ComboBox),
            new FrameworkPropertyMetadata(new Thickness(8f, 5f, 8f, 5f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private Popup? _dropDownPopup;
    private ListBox? _dropDownList;
    private bool _isDropDownItemsDirty = true;
    private bool _isSynchronizingDropDown;
    private long _runtimeHandlePointerDownCallCount;
    private long _runtimeHandlePointerDownElapsedTicks;
    private long _runtimeHandlePointerDownHitCount;
    private long _runtimeHandlePointerDownMissCount;
    private long _runtimeHandlePointerDownOpenToggleCount;
    private long _runtimeHandlePointerDownCloseToggleCount;
    private long _runtimeItemsChangedCallCount;
    private long _runtimeSelectionChangedCallCount;
    private long _runtimeSelectionChangedElapsedTicks;
    private long _runtimeSelectionChangedContainerScanCount;
    private long _runtimeSelectionChangedContainerMatchCount;
    private long _runtimeSelectionChangedDropDownSyncCount;
    private long _runtimeSelectionChangedDropDownSyncSkippedCount;
    private long _runtimeCreateContainerCallCount;
    private long _runtimePrepareContainerCallCount;
    private long _runtimePrepareContainerConfiguredFromItemCount;
    private long _runtimePrepareContainerTypographySyncCount;
    private long _runtimePrepareContainerUnexpectedElementCount;
    private long _runtimeDependencyPropertyChangedCallCount;
    private long _runtimeDependencyPropertyRefreshTriggerCount;
    private long _runtimeMeasureOverrideCallCount;
    private long _runtimeMeasureOverrideElapsedTicks;
    private long _runtimeMeasureOverrideEmptyTextCount;
    private long _runtimeMeasureOverrideTextMeasureCount;
    private long _runtimeRenderCallCount;
    private long _runtimeRenderElapsedTicks;
    private long _runtimeRenderBorderDrawCount;
    private long _runtimeRenderEmptyTextSkipCount;
    private long _runtimeRenderTextDrawCount;
    private long _runtimeDropDownOpenStateChangedCallCount;
    private long _runtimeDropDownOpenStateChangedElapsedTicks;
    private long _runtimeDropDownOpenStateChangedSyncSkipCount;
    private long _runtimeDropDownOpenStateChangedOpenPathCount;
    private long _runtimeDropDownOpenStateChangedClosePathCount;
    private long _runtimeOpenDropDownCallCount;
    private long _runtimeOpenDropDownElapsedTicks;
    private long _runtimeOpenDropDownHostMissingCount;
    private long _runtimeOpenDropDownPopupShowCount;
    private long _runtimeOpenDropDownPopupUnavailableCount;
    private long _runtimeCloseDropDownCallCount;
    private long _runtimeCloseDropDownPopupMissingCount;
    private long _runtimeEnsureDropDownControlsCallCount;
    private long _runtimeEnsureDropDownControlsElapsedTicks;
    private long _runtimeEnsureDropDownListCreateCount;
    private long _runtimeEnsureDropDownListReuseCount;
    private long _runtimeEnsureDropDownPopupCreateCount;
    private long _runtimeEnsureDropDownPopupReuseCount;
    private long _runtimeDropDownPopupClosedEventCount;
    private long _runtimeDropDownPopupClosedSyncCloseCount;
    private long _runtimeDropDownPopupClosedAlreadyClosedCount;
    private long _runtimeDropDownSelectionChangedCallCount;
    private long _runtimeDropDownSelectionChangedElapsedTicks;
    private long _runtimeDropDownSelectionChangedNullListSkipCount;
    private long _runtimeDropDownSelectionChangedSynchronizingSkipCount;
    private long _runtimeDropDownSelectionChangedApplySelectionCount;
    private long _runtimeRefreshDropDownItemsCallCount;
    private long _runtimeRefreshDropDownItemsElapsedTicks;
    private long _runtimeRefreshDropDownItemsNullListSkipCount;
    private long _runtimeRefreshDropDownItemsProjectedItemCount;
    private long _runtimeRefreshDropDownItemsSelectedIndexSyncCount;
    private long _runtimeFindHostPanelCallCount;
    private long _runtimeFindHostPanelFoundCount;
    private long _runtimeFindHostPanelMissingCount;
    private long _runtimeGetDisplayTextCallCount;
    private long _runtimeGetDisplayTextElapsedTicks;
    private long _runtimeGetDisplayTextComboBoxItemTextCount;
    private long _runtimeGetDisplayTextComboBoxItemLabelCount;
    private long _runtimeGetDisplayTextComboBoxItemContentToStringCount;
    private long _runtimeGetDisplayTextListBoxItemLabelCount;
    private long _runtimeGetDisplayTextLabelCount;
    private long _runtimeGetDisplayTextResolveDisplayPathCount;
    private long _runtimeGetDisplayTextEmptyResultCount;
    private long _runtimeBuildDropDownContainerCallCount;
    private long _runtimeConfigureContainerFromItemCallCount;
    private long _runtimeSyncContainerTypographyCallCount;
    private long _runtimeSyncContainerTypographyStyleSkipCount;
    private long _runtimeSyncContainerTypographyForegroundSetCount;
    private long _runtimeSyncContainerTypographyFontFamilySetCount;
    private long _runtimeSyncContainerTypographyFontSizeSetCount;
    private long _runtimeSyncContainerTypographyFontWeightSetCount;
    private long _runtimeSyncContainerTypographyFontStyleSetCount;

    protected override bool IncludeGeneratedChildrenInVisualTree => false;

    public ComboBox()
    {
        IncrementAggregate(ref _diagConstructorCallCount);
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

    public Style? DropDownListStyle
    {
        get => GetValue<Style>(DropDownListStyleProperty);
        set => SetValue(DropDownListStyleProperty, value);
    }

    public Style? DropDownPopupStyle
    {
        get => GetValue<Style>(DropDownPopupStyleProperty);
        set => SetValue(DropDownPopupStyleProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    public new Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected internal ListBox? DropDownListForTesting => _dropDownList;
    protected internal bool IsDropDownPopupOpenForTesting => _dropDownPopup?.IsOpen ?? false;

    internal bool HandlePointerDownFromInput(Vector2 pointerPosition)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeHandlePointerDownCallCount, ref _diagHandlePointerDownCallCount);
        try
        {
            if (!HitTest(pointerPosition))
            {
                IncrementMetric(ref _runtimeHandlePointerDownMissCount, ref _diagHandlePointerDownMissCount);
                return false;
            }

            IncrementMetric(ref _runtimeHandlePointerDownHitCount, ref _diagHandlePointerDownHitCount);
            if (IsDropDownOpen)
            {
                IncrementMetric(ref _runtimeHandlePointerDownCloseToggleCount, ref _diagHandlePointerDownCloseToggleCount);
            }
            else
            {
                IncrementMetric(ref _runtimeHandlePointerDownOpenToggleCount, ref _diagHandlePointerDownOpenToggleCount);
            }

            IsDropDownOpen = !IsDropDownOpen;
            return true;
        }
        finally
        {
            AddMetric(ref _runtimeHandlePointerDownElapsedTicks, ref _diagHandlePointerDownElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnItemsChanged()
    {
        IncrementMetric(ref _runtimeItemsChangedCallCount, ref _diagItemsChangedCallCount);
        base.OnItemsChanged();
        MarkDropDownItemsDirty();
        RefreshDropDownItems();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeSelectionChangedCallCount, ref _diagSelectionChangedCallCount);
        try
        {
            base.OnSelectionChanged(args);

            for (var i = 0; i < ItemContainers.Count; i++)
            {
                IncrementMetric(ref _runtimeSelectionChangedContainerScanCount, ref _diagSelectionChangedContainerScanCount);
                if (ItemContainers[i] is ComboBoxItem comboBoxItem)
                {
                    IncrementMetric(ref _runtimeSelectionChangedContainerMatchCount, ref _diagSelectionChangedContainerMatchCount);
                    comboBoxItem.IsSelected = i == SelectedIndex;
                }
            }

            if (_dropDownList != null && !_isSynchronizingDropDown)
            {
                IncrementMetric(ref _runtimeSelectionChangedDropDownSyncCount, ref _diagSelectionChangedDropDownSyncCount);
                _dropDownList.SelectedIndex = SelectedIndex;
            }
            else
            {
                IncrementMetric(ref _runtimeSelectionChangedDropDownSyncSkippedCount, ref _diagSelectionChangedDropDownSyncSkippedCount);
            }
        }
        finally
        {
            AddMetric(ref _runtimeSelectionChangedElapsedTicks, ref _diagSelectionChangedElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is ComboBoxItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        IncrementMetric(ref _runtimeCreateContainerCallCount, ref _diagCreateContainerCallCount);
        var container = new ComboBoxItem();
        ConfigureContainerFromItem(container, item);
        return container;
    }

    protected override UIElement? BuildContainerForTemplatedItemOverride(object? item, DataTemplate selectedTemplate)
    {
        var container = new ComboBoxItem
        {
            Content = item,
            ContentTemplate = selectedTemplate
        };
        return container;
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        IncrementMetric(ref _runtimePrepareContainerCallCount, ref _diagPrepareContainerCallCount);
        base.PrepareContainerForItemOverride(element, item, index);
        if (element is not ComboBoxItem comboBoxItem)
        {
            IncrementMetric(ref _runtimePrepareContainerUnexpectedElementCount, ref _diagPrepareContainerUnexpectedElementCount);
            return;
        }

        if (!ReferenceEquals(comboBoxItem, item))
        {
            IncrementMetric(ref _runtimePrepareContainerConfiguredFromItemCount, ref _diagPrepareContainerConfiguredFromItemCount);
            ConfigureContainerFromItem(comboBoxItem, item);
        }
        else
        {
            IncrementMetric(ref _runtimePrepareContainerTypographySyncCount, ref _diagPrepareContainerTypographySyncCount);
            SyncContainerTypography(comboBoxItem);
        }

        comboBoxItem.IsSelected = index == SelectedIndex;
    }

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        IncrementMetric(ref _runtimeDependencyPropertyChangedCallCount, ref _diagDependencyPropertyChangedCallCount);
        base.OnDependencyPropertyChanged(args);

        if (args.Property == MaxDropDownHeightProperty)
        {
            ApplyDropDownSizing();
        }

        if (args.Property == DropDownListStyleProperty && _dropDownList != null)
        {
            _dropDownList.Style = DropDownListStyle;
        }

        if (args.Property == DropDownPopupStyleProperty && _dropDownPopup != null)
        {
            _dropDownPopup.Style = DropDownPopupStyle;
        }

        if (args.Property == ItemContainerStyleProperty ||
            args.Property == ItemContainerStyleSelectorProperty ||
            args.Property == DisplayMemberPathProperty ||
            args.Property == ItemsPanelProperty ||
            args.Property == ItemStringFormatProperty ||
            args.Property == ForegroundProperty ||
            args.Property == FontSizeProperty ||
            args.Property == FontFamilyProperty ||
            args.Property == FontWeightProperty ||
            args.Property == FontStyleProperty ||
            args.Property == DropDownListStyleProperty)
        {
            IncrementMetric(ref _runtimeDependencyPropertyRefreshTriggerCount, ref _diagDependencyPropertyRefreshTriggerCount);
            MarkDropDownItemsDirty();
            RefreshDropDownItems();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeMeasureOverrideCallCount, ref _diagMeasureOverrideCallCount);
        try
        {
            var desired = base.MeasureOverride(availableSize);
            var padding = Padding;
            var border = BorderThickness * 2f;

            var text = GetDisplayText(SelectedItem);
            var textWidth = 0f;
            var textHeight = UiTextRenderer.GetLineHeight(this, FontSize);
            if (string.IsNullOrEmpty(text))
            {
                IncrementMetric(ref _runtimeMeasureOverrideEmptyTextCount, ref _diagMeasureOverrideEmptyTextCount);
            }
            else
            {
                IncrementMetric(ref _runtimeMeasureOverrideTextMeasureCount, ref _diagMeasureOverrideTextMeasureCount);
                textWidth = UiTextRenderer.MeasureWidth(this, text, FontSize);
            }

            desired.X = MathF.Max(desired.X, padding.Horizontal + border + textWidth + 20f);
            desired.Y = MathF.Max(desired.Y, padding.Vertical + border + MathF.Max(textHeight, 16f));
            return desired;
        }
        finally
        {
            AddMetric(ref _runtimeMeasureOverrideElapsedTicks, ref _diagMeasureOverrideElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRenderCallCount, ref _diagRenderCallCount);
        try
        {
            var slot = LayoutSlot;
            UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);
            if (BorderThickness > 0f)
            {
                IncrementMetric(ref _runtimeRenderBorderDrawCount, ref _diagRenderBorderDrawCount);
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

            var text = GetDisplayText(SelectedItem);
            if (string.IsNullOrEmpty(text))
            {
                IncrementMetric(ref _runtimeRenderEmptyTextSkipCount, ref _diagRenderEmptyTextSkipCount);
                return;
            }

            IncrementMetric(ref _runtimeRenderTextDrawCount, ref _diagRenderTextDrawCount);
            UiTextRenderer.DrawString(spriteBatch, this, text, ResolveSelectedTextRenderPosition(slot, text), Foreground * Opacity, FontSize, opaqueBackground: true);
        }
        finally
        {
            AddMetric(ref _runtimeRenderElapsedTicks, ref _diagRenderElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    internal Vector2 GetSelectedTextRenderPositionForTests(string text)
    {
        return ResolveSelectedTextRenderPosition(LayoutSlot, text);
    }

    private Vector2 ResolveSelectedTextRenderPosition(LayoutRect slot, string text)
    {
        var textX = slot.X + Padding.Left + BorderThickness;
        var typography = UiTextRenderer.ResolveTypography(this, FontSize);
        var inkBounds = UiTextRenderer.GetInkBounds(typography, text);
        var textY = inkBounds.Height > 0f
            ? slot.Y + ((slot.Height - inkBounds.Height) / 2f) - inkBounds.Y
            : slot.Y + ((slot.Height - UiTextRenderer.GetLineHeight(typography)) / 2f);
        return new Vector2(textX, textY);
    }

    private void OnIsDropDownOpenChanged(bool isOpen)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeDropDownOpenStateChangedCallCount, ref _diagDropDownOpenStateChangedCallCount);
        try
        {
            if (_isSynchronizingDropDown)
            {
                IncrementMetric(ref _runtimeDropDownOpenStateChangedSyncSkipCount, ref _diagDropDownOpenStateChangedSyncSkipCount);
                return;
            }

            if (isOpen)
            {
                IncrementMetric(ref _runtimeDropDownOpenStateChangedOpenPathCount, ref _diagDropDownOpenStateChangedOpenPathCount);
                OpenDropDown();
                return;
            }

            IncrementMetric(ref _runtimeDropDownOpenStateChangedClosePathCount, ref _diagDropDownOpenStateChangedClosePathCount);
            CloseDropDown();
        }
        finally
        {
            AddMetric(ref _runtimeDropDownOpenStateChangedElapsedTicks, ref _diagDropDownOpenStateChangedElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void OpenDropDown()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeOpenDropDownCallCount, ref _diagOpenDropDownCallCount);
        try
        {
            var host = FindHostPanel();
            if (host == null)
            {
                IncrementMetric(ref _runtimeOpenDropDownHostMissingCount, ref _diagOpenDropDownHostMissingCount);
                _isSynchronizingDropDown = false;
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

            if (_dropDownPopup == null || _dropDownList == null)
            {
                IncrementMetric(ref _runtimeOpenDropDownPopupUnavailableCount, ref _diagOpenDropDownPopupUnavailableCount);
                return;
            }

            RefreshDropDownItems();
            _dropDownList.ResetScrollStateForReuse();

            _dropDownPopup.PlacementTarget = this;
            _dropDownPopup.PlacementMode = PopupPlacementMode.Bottom;
            _dropDownPopup.HorizontalOffset = 0f;
            _dropDownPopup.VerticalOffset = 2f;
            _dropDownPopup.Width = Math.Max(ActualWidth > 0f ? ActualWidth : Width, 80f);
            ApplyDropDownSizing();

            IncrementMetric(ref _runtimeOpenDropDownPopupShowCount, ref _diagOpenDropDownPopupShowCount);
            _dropDownPopup.Show(host);
        }
        finally
        {
            AddMetric(ref _runtimeOpenDropDownElapsedTicks, ref _diagOpenDropDownElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void CloseDropDown()
    {
        IncrementMetric(ref _runtimeCloseDropDownCallCount, ref _diagCloseDropDownCallCount);
        if (_dropDownPopup == null)
        {
            IncrementMetric(ref _runtimeCloseDropDownPopupMissingCount, ref _diagCloseDropDownPopupMissingCount);
        }

        _dropDownPopup?.Close();
    }

    private void EnsureDropDownControls()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeEnsureDropDownControlsCallCount, ref _diagEnsureDropDownControlsCallCount);
        try
        {
            if (_dropDownList == null)
            {
                IncrementMetric(ref _runtimeEnsureDropDownListCreateCount, ref _diagEnsureDropDownListCreateCount);
                _dropDownList = new ListBox
                {
                    Style = DropDownListStyle,
                    SelectionMode = SelectionMode.Single,
                    IsVirtualizing = true
                };
                _dropDownList.IsItemItsOwnContainerOverrideCallback = static _ => false;
                _dropDownList.CreateContainerForItemOverrideCallback = item => BuildDropDownContainer(item, index: -1);
                _dropDownList.BuildContainerForTemplatedItemOverrideCallback = (item, _) => BuildDropDownContainer(item, index: -1);
                _dropDownList.PrepareContainerForItemOverrideCallback = PrepareDropDownContainer;
                _dropDownList.SelectionChanged += OnDropDownSelectionChanged;
            }
            else
            {
                IncrementMetric(ref _runtimeEnsureDropDownListReuseCount, ref _diagEnsureDropDownListReuseCount);
            }

            if (_dropDownPopup != null)
            {
                IncrementMetric(ref _runtimeEnsureDropDownPopupReuseCount, ref _diagEnsureDropDownPopupReuseCount);
                return;
            }

            IncrementMetric(ref _runtimeEnsureDropDownPopupCreateCount, ref _diagEnsureDropDownPopupCreateCount);
            _dropDownPopup = new Popup
            {
                Style = DropDownPopupStyle,
                Title = string.Empty,
                TitleBarHeight = 0f,
                CanClose = false,
                CanDragMove = false,
                DismissOnOutsideClick = true,
                Content = _dropDownList,
                BorderThickness = 1f,
                Padding = new Thickness(0f)
            };

            _dropDownPopup.Closed += OnDropDownPopupClosed;
            ApplyDropDownSizing();
        }
        finally
        {
            AddMetric(ref _runtimeEnsureDropDownControlsElapsedTicks, ref _diagEnsureDropDownControlsElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void ApplyDropDownSizing()
    {
        var popupChromeHeight = 0f;
        if (_dropDownPopup != null)
        {
            _dropDownPopup.Height = float.NaN;
            _dropDownPopup.MaxHeight = MaxDropDownHeight;
            popupChromeHeight = (_dropDownPopup.BorderThickness * 2f) + _dropDownPopup.TitleBarHeight + _dropDownPopup.Padding.Vertical;
        }

        if (_dropDownList != null)
        {
            _dropDownList.MaxHeight = MathF.Max(0f, MaxDropDownHeight - popupChromeHeight);
        }
    }

    private void OnDropDownSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeDropDownSelectionChangedCallCount, ref _diagDropDownSelectionChangedCallCount);
        try
        {
            if (_dropDownList == null)
            {
                IncrementMetric(ref _runtimeDropDownSelectionChangedNullListSkipCount, ref _diagDropDownSelectionChangedNullListSkipCount);
                return;
            }

            if (_isSynchronizingDropDown)
            {
                IncrementMetric(ref _runtimeDropDownSelectionChangedSynchronizingSkipCount, ref _diagDropDownSelectionChangedSynchronizingSkipCount);
                return;
            }

            var selectedIndex = _dropDownList.SelectedIndex;
            _isSynchronizingDropDown = true;
            try
            {
                IncrementMetric(ref _runtimeDropDownSelectionChangedApplySelectionCount, ref _diagDropDownSelectionChangedApplySelectionCount);
                SetSelectedIndexInternal(selectedIndex);
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }

            IsDropDownOpen = false;
        }
        finally
        {
            _ = args;
            AddMetric(ref _runtimeDropDownSelectionChangedElapsedTicks, ref _diagDropDownSelectionChangedElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void RefreshDropDownItems()
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeRefreshDropDownItemsCallCount, ref _diagRefreshDropDownItemsCallCount);
        try
        {
            if (_dropDownList == null)
            {
                IncrementMetric(ref _runtimeRefreshDropDownItemsNullListSkipCount, ref _diagRefreshDropDownItemsNullListSkipCount);
                return;
            }

            _isSynchronizingDropDown = true;
            try
            {
                if (_isDropDownItemsDirty)
                {
                    _dropDownList.ExecuteWithSuspendedRegeneration(() =>
                    {
                        _dropDownList.ItemContainerStyle = ItemContainerStyle;
                        _dropDownList.ItemContainerStyleSelector = ItemContainerStyleSelector;
                        _dropDownList.ItemsPanel = ItemsPanel;

                        var projectedItems = new List<object>(ItemContainers.Count);
                        for (var i = 0; i < ItemContainers.Count; i++)
                        {
                            var container = ItemContainers[i];
                            var item = ItemFromContainer(container);
                            IncrementMetric(ref _runtimeRefreshDropDownItemsProjectedItemCount, ref _diagRefreshDropDownItemsProjectedItemCount);
                            projectedItems.Add(item ?? string.Empty);
                        }

                        _dropDownList.Items.Clear();
                        foreach (var projectedItem in projectedItems)
                        {
                            _dropDownList.Items.Add(projectedItem);
                        }
                    });

                    _isDropDownItemsDirty = false;
                }

                IncrementMetric(ref _runtimeRefreshDropDownItemsSelectedIndexSyncCount, ref _diagRefreshDropDownItemsSelectedIndexSyncCount);
                _dropDownList.SelectedIndex = SelectedIndex;
            }
            finally
            {
                _isSynchronizingDropDown = false;
            }
        }
        finally
        {
            AddMetric(ref _runtimeRefreshDropDownItemsElapsedTicks, ref _diagRefreshDropDownItemsElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private void MarkDropDownItemsDirty()
    {
        _isDropDownItemsDirty = true;
    }

    private void PrepareDropDownContainer(UIElement element, object item, int index)
    {
        if (element is not ComboBoxItem comboBoxItem)
        {
            return;
        }

        comboBoxItem.IsSelected = index == (_dropDownList?.SelectedIndex ?? SelectedIndex);
    }

    private Panel? FindHostPanel()
    {
        IncrementMetric(ref _runtimeFindHostPanelCallCount, ref _diagFindHostPanelCallCount);
        var host = Popup.ResolveOverlayHost(this);
        if (host == null)
        {
            IncrementMetric(ref _runtimeFindHostPanelMissingCount, ref _diagFindHostPanelMissingCount);
        }
        else
        {
            IncrementMetric(ref _runtimeFindHostPanelFoundCount, ref _diagFindHostPanelFoundCount);
        }

        return host;
    }

    private string GetDisplayText(object? item)
    {
        var start = Stopwatch.GetTimestamp();
        IncrementMetric(ref _runtimeGetDisplayTextCallCount, ref _diagGetDisplayTextCallCount);
        try
        {
            string result;
            if (item is ComboBoxItem comboBoxItem)
            {
                if (!string.IsNullOrEmpty(comboBoxItem.Text))
                {
                    IncrementMetric(ref _runtimeGetDisplayTextComboBoxItemTextCount, ref _diagGetDisplayTextComboBoxItemTextCount);
                    result = comboBoxItem.Text;
                }
                else if (comboBoxItem.Content is Label label)
                {
                    IncrementMetric(ref _runtimeGetDisplayTextComboBoxItemLabelCount, ref _diagGetDisplayTextComboBoxItemLabelCount);
                    result = Label.ExtractAutomationText(label.Content);
                }
                else
                {
                    IncrementMetric(ref _runtimeGetDisplayTextComboBoxItemContentToStringCount, ref _diagGetDisplayTextComboBoxItemContentToStringCount);
                    result = comboBoxItem.Content?.ToString() ?? string.Empty;
                }
            }
            else if (item is ListBoxItem listBoxItem &&
                listBoxItem.Content is Label listBoxLabel)
            {
                IncrementMetric(ref _runtimeGetDisplayTextListBoxItemLabelCount, ref _diagGetDisplayTextListBoxItemLabelCount);
                result = Label.ExtractAutomationText(listBoxLabel.Content);
            }
            else if (item is Label itemLabel)
            {
                IncrementMetric(ref _runtimeGetDisplayTextLabelCount, ref _diagGetDisplayTextLabelCount);
                result = Label.ExtractAutomationText(itemLabel.Content);
            }
            else
            {
                IncrementMetric(ref _runtimeGetDisplayTextResolveDisplayPathCount, ref _diagGetDisplayTextResolveDisplayPathCount);
                result = this.ResolveDisplayTextForItem(item);
            }

            if (string.IsNullOrEmpty(result))
            {
                IncrementMetric(ref _runtimeGetDisplayTextEmptyResultCount, ref _diagGetDisplayTextEmptyResultCount);
            }

            return result;
        }
        finally
        {
            AddMetric(ref _runtimeGetDisplayTextElapsedTicks, ref _diagGetDisplayTextElapsedTicks, Stopwatch.GetTimestamp() - start);
        }
    }

    private ComboBoxItem BuildDropDownContainer(object? item, int index)
    {
        IncrementMetric(ref _runtimeBuildDropDownContainerCallCount, ref _diagBuildDropDownContainerCallCount);

        var container = new ComboBoxItem();
        ConfigureContainerFromItem(container, item);
        container.IsSelected = index == SelectedIndex;
        return container;
    }

    private void ConfigureContainerFromItem(ComboBoxItem container, object? item)
    {
        IncrementMetric(ref _runtimeConfigureContainerFromItemCallCount, ref _diagConfigureContainerFromItemCallCount);
        if (item is ComboBoxItem comboBoxItem)
        {
            container.Text = comboBoxItem.Text;
            container.Padding = comboBoxItem.Padding;

            if (comboBoxItem.Content != null)
            {
                container.Content = comboBoxItem.Content;
            }
            else
            {
                container.ClearValue(ContentControl.ContentProperty);
            }
        }
        else
        {
            var selectedTemplate = ItemTemplate != null || ItemTemplateSelector != null
                ? DataTemplateResolver.ResolveTemplateForContent(
                    this,
                    item,
                    ItemTemplate,
                    ItemTemplateSelector,
                    this)
                : null;

            if (selectedTemplate != null)
            {
                container.Text = string.Empty;
                container.Content = item;
                container.ContentTemplate = selectedTemplate;
            }
            else
            {
                container.ClearValue(ContentControl.ContentTemplateProperty);
                container.ClearValue(ContentControl.ContentProperty);
                container.Text = GetDisplayText(item);
            }
        }

        SyncContainerTypography(container);
    }

    private void SyncContainerTypography(ComboBoxItem container)
    {
        IncrementMetric(ref _runtimeSyncContainerTypographyCallCount, ref _diagSyncContainerTypographyCallCount);
        if (ItemContainerStyle != null)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyStyleSkipCount, ref _diagSyncContainerTypographyStyleSkipCount);
            return;
        }

        if (container.GetValueSource(ComboBoxItem.ForegroundProperty) == DependencyPropertyValueSource.Default)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyForegroundSetCount, ref _diagSyncContainerTypographyForegroundSetCount);
            container.Foreground = Foreground;
        }

        if (container.GetValueSource(FrameworkElement.FontFamilyProperty) == DependencyPropertyValueSource.Default)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyFontFamilySetCount, ref _diagSyncContainerTypographyFontFamilySetCount);
            container.FontFamily = FontFamily;
        }

        if (container.GetValueSource(FrameworkElement.FontSizeProperty) == DependencyPropertyValueSource.Default)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyFontSizeSetCount, ref _diagSyncContainerTypographyFontSizeSetCount);
            container.FontSize = FontSize;
        }

        if (container.GetValueSource(FrameworkElement.FontWeightProperty) == DependencyPropertyValueSource.Default)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyFontWeightSetCount, ref _diagSyncContainerTypographyFontWeightSetCount);
            container.FontWeight = FontWeight;
        }

        if (container.GetValueSource(FrameworkElement.FontStyleProperty) == DependencyPropertyValueSource.Default)
        {
            IncrementMetric(ref _runtimeSyncContainerTypographyFontStyleSetCount, ref _diagSyncContainerTypographyFontStyleSetCount);
            container.FontStyle = FontStyle;
        }
    }

    private void OnDropDownPopupClosed(object? sender, EventArgs args)
    {
        IncrementMetric(ref _runtimeDropDownPopupClosedEventCount, ref _diagDropDownPopupClosedEventCount);
        if (!IsDropDownOpen)
        {
            IncrementMetric(ref _runtimeDropDownPopupClosedAlreadyClosedCount, ref _diagDropDownPopupClosedAlreadyClosedCount);
            return;
        }

        IncrementMetric(ref _runtimeDropDownPopupClosedSyncCloseCount, ref _diagDropDownPopupClosedSyncCloseCount);
        _isSynchronizingDropDown = true;
        try
        {
            IsDropDownOpen = false;
        }
        finally
        {
            _isSynchronizingDropDown = false;
        }
    }

    internal ComboBoxRuntimeDiagnosticsSnapshot GetComboBoxSnapshotForDiagnostics()
    {
        return new ComboBoxRuntimeDiagnosticsSnapshot(
            IsDropDownOpen,
            _dropDownPopup != null,
            _dropDownPopup?.IsOpen ?? false,
            _dropDownList != null,
            _isSynchronizingDropDown,
            SelectedIndex,
            ItemContainers.Count,
            _dropDownList?.GetRealizedItemContainerCountForDiagnostics() ?? 0,
            LayoutSlot.Width,
            LayoutSlot.Height,
            GetDisplayText(SelectedItem),
            _runtimeHandlePointerDownCallCount,
            TicksToMilliseconds(_runtimeHandlePointerDownElapsedTicks),
            _runtimeHandlePointerDownHitCount,
            _runtimeHandlePointerDownMissCount,
            _runtimeHandlePointerDownOpenToggleCount,
            _runtimeHandlePointerDownCloseToggleCount,
            _runtimeItemsChangedCallCount,
            _runtimeSelectionChangedCallCount,
            TicksToMilliseconds(_runtimeSelectionChangedElapsedTicks),
            _runtimeSelectionChangedContainerScanCount,
            _runtimeSelectionChangedContainerMatchCount,
            _runtimeSelectionChangedDropDownSyncCount,
            _runtimeSelectionChangedDropDownSyncSkippedCount,
            _runtimeCreateContainerCallCount,
            _runtimePrepareContainerCallCount,
            _runtimePrepareContainerConfiguredFromItemCount,
            _runtimePrepareContainerTypographySyncCount,
            _runtimePrepareContainerUnexpectedElementCount,
            _runtimeDependencyPropertyChangedCallCount,
            _runtimeDependencyPropertyRefreshTriggerCount,
            _runtimeMeasureOverrideCallCount,
            TicksToMilliseconds(_runtimeMeasureOverrideElapsedTicks),
            _runtimeMeasureOverrideEmptyTextCount,
            _runtimeMeasureOverrideTextMeasureCount,
            _runtimeRenderCallCount,
            TicksToMilliseconds(_runtimeRenderElapsedTicks),
            _runtimeRenderBorderDrawCount,
            _runtimeRenderEmptyTextSkipCount,
            _runtimeRenderTextDrawCount,
            _runtimeDropDownOpenStateChangedCallCount,
            TicksToMilliseconds(_runtimeDropDownOpenStateChangedElapsedTicks),
            _runtimeDropDownOpenStateChangedSyncSkipCount,
            _runtimeDropDownOpenStateChangedOpenPathCount,
            _runtimeDropDownOpenStateChangedClosePathCount,
            _runtimeOpenDropDownCallCount,
            TicksToMilliseconds(_runtimeOpenDropDownElapsedTicks),
            _runtimeOpenDropDownHostMissingCount,
            _runtimeOpenDropDownPopupShowCount,
            _runtimeOpenDropDownPopupUnavailableCount,
            _runtimeCloseDropDownCallCount,
            _runtimeCloseDropDownPopupMissingCount,
            _runtimeEnsureDropDownControlsCallCount,
            TicksToMilliseconds(_runtimeEnsureDropDownControlsElapsedTicks),
            _runtimeEnsureDropDownListCreateCount,
            _runtimeEnsureDropDownListReuseCount,
            _runtimeEnsureDropDownPopupCreateCount,
            _runtimeEnsureDropDownPopupReuseCount,
            _runtimeDropDownPopupClosedEventCount,
            _runtimeDropDownPopupClosedSyncCloseCount,
            _runtimeDropDownPopupClosedAlreadyClosedCount,
            _runtimeDropDownSelectionChangedCallCount,
            TicksToMilliseconds(_runtimeDropDownSelectionChangedElapsedTicks),
            _runtimeDropDownSelectionChangedNullListSkipCount,
            _runtimeDropDownSelectionChangedSynchronizingSkipCount,
            _runtimeDropDownSelectionChangedApplySelectionCount,
            _runtimeRefreshDropDownItemsCallCount,
            TicksToMilliseconds(_runtimeRefreshDropDownItemsElapsedTicks),
            _runtimeRefreshDropDownItemsNullListSkipCount,
            _runtimeRefreshDropDownItemsProjectedItemCount,
            _runtimeRefreshDropDownItemsSelectedIndexSyncCount,
            _runtimeFindHostPanelCallCount,
            _runtimeFindHostPanelFoundCount,
            _runtimeFindHostPanelMissingCount,
            _runtimeGetDisplayTextCallCount,
            TicksToMilliseconds(_runtimeGetDisplayTextElapsedTicks),
            _runtimeGetDisplayTextComboBoxItemTextCount,
            _runtimeGetDisplayTextComboBoxItemLabelCount,
            _runtimeGetDisplayTextComboBoxItemContentToStringCount,
            _runtimeGetDisplayTextListBoxItemLabelCount,
            _runtimeGetDisplayTextLabelCount,
            _runtimeGetDisplayTextResolveDisplayPathCount,
            _runtimeGetDisplayTextEmptyResultCount,
            _runtimeBuildDropDownContainerCallCount,
            _runtimeConfigureContainerFromItemCallCount,
            _runtimeSyncContainerTypographyCallCount,
            _runtimeSyncContainerTypographyStyleSkipCount,
            _runtimeSyncContainerTypographyForegroundSetCount,
            _runtimeSyncContainerTypographyFontFamilySetCount,
            _runtimeSyncContainerTypographyFontSizeSetCount,
            _runtimeSyncContainerTypographyFontWeightSetCount,
            _runtimeSyncContainerTypographyFontStyleSetCount);
    }

    internal new static ComboBoxTelemetrySnapshot GetAggregateTelemetrySnapshotForDiagnostics()
    {
        return new ComboBoxTelemetrySnapshot(
            ReadAggregate(ref _diagConstructorCallCount),
            ReadAggregate(ref _diagHandlePointerDownCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagHandlePointerDownElapsedTicks)),
            ReadAggregate(ref _diagHandlePointerDownHitCount),
            ReadAggregate(ref _diagHandlePointerDownMissCount),
            ReadAggregate(ref _diagHandlePointerDownOpenToggleCount),
            ReadAggregate(ref _diagHandlePointerDownCloseToggleCount),
            ReadAggregate(ref _diagItemsChangedCallCount),
            ReadAggregate(ref _diagSelectionChangedCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagSelectionChangedElapsedTicks)),
            ReadAggregate(ref _diagSelectionChangedContainerScanCount),
            ReadAggregate(ref _diagSelectionChangedContainerMatchCount),
            ReadAggregate(ref _diagSelectionChangedDropDownSyncCount),
            ReadAggregate(ref _diagSelectionChangedDropDownSyncSkippedCount),
            ReadAggregate(ref _diagCreateContainerCallCount),
            ReadAggregate(ref _diagPrepareContainerCallCount),
            ReadAggregate(ref _diagPrepareContainerConfiguredFromItemCount),
            ReadAggregate(ref _diagPrepareContainerTypographySyncCount),
            ReadAggregate(ref _diagPrepareContainerUnexpectedElementCount),
            ReadAggregate(ref _diagDependencyPropertyChangedCallCount),
            ReadAggregate(ref _diagDependencyPropertyRefreshTriggerCount),
            ReadAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ReadAggregate(ref _diagMeasureOverrideEmptyTextCount),
            ReadAggregate(ref _diagMeasureOverrideTextMeasureCount),
            ReadAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRenderElapsedTicks)),
            ReadAggregate(ref _diagRenderBorderDrawCount),
            ReadAggregate(ref _diagRenderEmptyTextSkipCount),
            ReadAggregate(ref _diagRenderTextDrawCount),
            ReadAggregate(ref _diagDropDownOpenStateChangedCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDropDownOpenStateChangedElapsedTicks)),
            ReadAggregate(ref _diagDropDownOpenStateChangedSyncSkipCount),
            ReadAggregate(ref _diagDropDownOpenStateChangedOpenPathCount),
            ReadAggregate(ref _diagDropDownOpenStateChangedClosePathCount),
            ReadAggregate(ref _diagOpenDropDownCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagOpenDropDownElapsedTicks)),
            ReadAggregate(ref _diagOpenDropDownHostMissingCount),
            ReadAggregate(ref _diagOpenDropDownPopupShowCount),
            ReadAggregate(ref _diagOpenDropDownPopupUnavailableCount),
            ReadAggregate(ref _diagCloseDropDownCallCount),
            ReadAggregate(ref _diagCloseDropDownPopupMissingCount),
            ReadAggregate(ref _diagEnsureDropDownControlsCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagEnsureDropDownControlsElapsedTicks)),
            ReadAggregate(ref _diagEnsureDropDownListCreateCount),
            ReadAggregate(ref _diagEnsureDropDownListReuseCount),
            ReadAggregate(ref _diagEnsureDropDownPopupCreateCount),
            ReadAggregate(ref _diagEnsureDropDownPopupReuseCount),
            ReadAggregate(ref _diagDropDownPopupClosedEventCount),
            ReadAggregate(ref _diagDropDownPopupClosedSyncCloseCount),
            ReadAggregate(ref _diagDropDownPopupClosedAlreadyClosedCount),
            ReadAggregate(ref _diagDropDownSelectionChangedCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagDropDownSelectionChangedElapsedTicks)),
            ReadAggregate(ref _diagDropDownSelectionChangedNullListSkipCount),
            ReadAggregate(ref _diagDropDownSelectionChangedSynchronizingSkipCount),
            ReadAggregate(ref _diagDropDownSelectionChangedApplySelectionCount),
            ReadAggregate(ref _diagRefreshDropDownItemsCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagRefreshDropDownItemsElapsedTicks)),
            ReadAggregate(ref _diagRefreshDropDownItemsNullListSkipCount),
            ReadAggregate(ref _diagRefreshDropDownItemsProjectedItemCount),
            ReadAggregate(ref _diagRefreshDropDownItemsSelectedIndexSyncCount),
            ReadAggregate(ref _diagFindHostPanelCallCount),
            ReadAggregate(ref _diagFindHostPanelFoundCount),
            ReadAggregate(ref _diagFindHostPanelMissingCount),
            ReadAggregate(ref _diagGetDisplayTextCallCount),
            TicksToMilliseconds(ReadAggregate(ref _diagGetDisplayTextElapsedTicks)),
            ReadAggregate(ref _diagGetDisplayTextComboBoxItemTextCount),
            ReadAggregate(ref _diagGetDisplayTextComboBoxItemLabelCount),
            ReadAggregate(ref _diagGetDisplayTextComboBoxItemContentToStringCount),
            ReadAggregate(ref _diagGetDisplayTextListBoxItemLabelCount),
            ReadAggregate(ref _diagGetDisplayTextLabelCount),
            ReadAggregate(ref _diagGetDisplayTextResolveDisplayPathCount),
            ReadAggregate(ref _diagGetDisplayTextEmptyResultCount),
            ReadAggregate(ref _diagBuildDropDownContainerCallCount),
            ReadAggregate(ref _diagConfigureContainerFromItemCallCount),
            ReadAggregate(ref _diagSyncContainerTypographyCallCount),
            ReadAggregate(ref _diagSyncContainerTypographyStyleSkipCount),
            ReadAggregate(ref _diagSyncContainerTypographyForegroundSetCount),
            ReadAggregate(ref _diagSyncContainerTypographyFontFamilySetCount),
            ReadAggregate(ref _diagSyncContainerTypographyFontSizeSetCount),
            ReadAggregate(ref _diagSyncContainerTypographyFontWeightSetCount),
            ReadAggregate(ref _diagSyncContainerTypographyFontStyleSetCount));
    }

    internal new static ComboBoxTelemetrySnapshot GetTelemetryAndReset()
    {
        return new ComboBoxTelemetrySnapshot(
            ResetAggregate(ref _diagConstructorCallCount),
            ResetAggregate(ref _diagHandlePointerDownCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagHandlePointerDownElapsedTicks)),
            ResetAggregate(ref _diagHandlePointerDownHitCount),
            ResetAggregate(ref _diagHandlePointerDownMissCount),
            ResetAggregate(ref _diagHandlePointerDownOpenToggleCount),
            ResetAggregate(ref _diagHandlePointerDownCloseToggleCount),
            ResetAggregate(ref _diagItemsChangedCallCount),
            ResetAggregate(ref _diagSelectionChangedCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagSelectionChangedElapsedTicks)),
            ResetAggregate(ref _diagSelectionChangedContainerScanCount),
            ResetAggregate(ref _diagSelectionChangedContainerMatchCount),
            ResetAggregate(ref _diagSelectionChangedDropDownSyncCount),
            ResetAggregate(ref _diagSelectionChangedDropDownSyncSkippedCount),
            ResetAggregate(ref _diagCreateContainerCallCount),
            ResetAggregate(ref _diagPrepareContainerCallCount),
            ResetAggregate(ref _diagPrepareContainerConfiguredFromItemCount),
            ResetAggregate(ref _diagPrepareContainerTypographySyncCount),
            ResetAggregate(ref _diagPrepareContainerUnexpectedElementCount),
            ResetAggregate(ref _diagDependencyPropertyChangedCallCount),
            ResetAggregate(ref _diagDependencyPropertyRefreshTriggerCount),
            ResetAggregate(ref _diagMeasureOverrideCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagMeasureOverrideElapsedTicks)),
            ResetAggregate(ref _diagMeasureOverrideEmptyTextCount),
            ResetAggregate(ref _diagMeasureOverrideTextMeasureCount),
            ResetAggregate(ref _diagRenderCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagRenderElapsedTicks)),
            ResetAggregate(ref _diagRenderBorderDrawCount),
            ResetAggregate(ref _diagRenderEmptyTextSkipCount),
            ResetAggregate(ref _diagRenderTextDrawCount),
            ResetAggregate(ref _diagDropDownOpenStateChangedCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagDropDownOpenStateChangedElapsedTicks)),
            ResetAggregate(ref _diagDropDownOpenStateChangedSyncSkipCount),
            ResetAggregate(ref _diagDropDownOpenStateChangedOpenPathCount),
            ResetAggregate(ref _diagDropDownOpenStateChangedClosePathCount),
            ResetAggregate(ref _diagOpenDropDownCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagOpenDropDownElapsedTicks)),
            ResetAggregate(ref _diagOpenDropDownHostMissingCount),
            ResetAggregate(ref _diagOpenDropDownPopupShowCount),
            ResetAggregate(ref _diagOpenDropDownPopupUnavailableCount),
            ResetAggregate(ref _diagCloseDropDownCallCount),
            ResetAggregate(ref _diagCloseDropDownPopupMissingCount),
            ResetAggregate(ref _diagEnsureDropDownControlsCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagEnsureDropDownControlsElapsedTicks)),
            ResetAggregate(ref _diagEnsureDropDownListCreateCount),
            ResetAggregate(ref _diagEnsureDropDownListReuseCount),
            ResetAggregate(ref _diagEnsureDropDownPopupCreateCount),
            ResetAggregate(ref _diagEnsureDropDownPopupReuseCount),
            ResetAggregate(ref _diagDropDownPopupClosedEventCount),
            ResetAggregate(ref _diagDropDownPopupClosedSyncCloseCount),
            ResetAggregate(ref _diagDropDownPopupClosedAlreadyClosedCount),
            ResetAggregate(ref _diagDropDownSelectionChangedCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagDropDownSelectionChangedElapsedTicks)),
            ResetAggregate(ref _diagDropDownSelectionChangedNullListSkipCount),
            ResetAggregate(ref _diagDropDownSelectionChangedSynchronizingSkipCount),
            ResetAggregate(ref _diagDropDownSelectionChangedApplySelectionCount),
            ResetAggregate(ref _diagRefreshDropDownItemsCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagRefreshDropDownItemsElapsedTicks)),
            ResetAggregate(ref _diagRefreshDropDownItemsNullListSkipCount),
            ResetAggregate(ref _diagRefreshDropDownItemsProjectedItemCount),
            ResetAggregate(ref _diagRefreshDropDownItemsSelectedIndexSyncCount),
            ResetAggregate(ref _diagFindHostPanelCallCount),
            ResetAggregate(ref _diagFindHostPanelFoundCount),
            ResetAggregate(ref _diagFindHostPanelMissingCount),
            ResetAggregate(ref _diagGetDisplayTextCallCount),
            TicksToMilliseconds(ResetAggregate(ref _diagGetDisplayTextElapsedTicks)),
            ResetAggregate(ref _diagGetDisplayTextComboBoxItemTextCount),
            ResetAggregate(ref _diagGetDisplayTextComboBoxItemLabelCount),
            ResetAggregate(ref _diagGetDisplayTextComboBoxItemContentToStringCount),
            ResetAggregate(ref _diagGetDisplayTextListBoxItemLabelCount),
            ResetAggregate(ref _diagGetDisplayTextLabelCount),
            ResetAggregate(ref _diagGetDisplayTextResolveDisplayPathCount),
            ResetAggregate(ref _diagGetDisplayTextEmptyResultCount),
            ResetAggregate(ref _diagBuildDropDownContainerCallCount),
            ResetAggregate(ref _diagConfigureContainerFromItemCallCount),
            ResetAggregate(ref _diagSyncContainerTypographyCallCount),
            ResetAggregate(ref _diagSyncContainerTypographyStyleSkipCount),
            ResetAggregate(ref _diagSyncContainerTypographyForegroundSetCount),
            ResetAggregate(ref _diagSyncContainerTypographyFontFamilySetCount),
            ResetAggregate(ref _diagSyncContainerTypographyFontSizeSetCount),
            ResetAggregate(ref _diagSyncContainerTypographyFontWeightSetCount),
            ResetAggregate(ref _diagSyncContainerTypographyFontStyleSetCount));
    }

    private void IncrementMetric(ref long runtimeField, ref long aggregateField)
    {
        runtimeField++;
        IncrementAggregate(ref aggregateField);
    }

    private void AddMetric(ref long runtimeField, ref long aggregateField, long value)
    {
        runtimeField += value;
        AddAggregate(ref aggregateField, value);
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private static void IncrementAggregate(ref long field)
    {
        Interlocked.Increment(ref field);
    }

    private static void AddAggregate(ref long field, long value)
    {
        Interlocked.Add(ref field, value);
    }

    private static long ReadAggregate(ref long field)
    {
        return Interlocked.Read(ref field);
    }

    private static long ResetAggregate(ref long field)
    {
        return Interlocked.Exchange(ref field, 0);
    }
}


