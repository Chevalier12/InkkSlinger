using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class CanvasView : UserControl
{
    private const float StackedLayoutBreakpoint = 900f;
    private const float InfoRailWidth = 320f;
    private const float StackedInfoRailHeight = 250f;
    private const float DefaultWorkbenchMinHeight = 380f;
    private const float StackedWorkbenchMinHeight = 340f;
    private const float MinimumFocusInset = 12f;
    private const float PositionStep = 18f;
    private const float MinimumFocusWidth = 172f;
    private const float MaximumFocusWidth = 296f;
    private const float MinimumFocusHeight = 112f;
    private const float MaximumFocusHeight = 196f;
    private static readonly Thickness DefaultWorkbenchMargin = new(0f, 0f, 0f, 12f);
    private static readonly Thickness StackedWorkbenchMargin = new(0f, 0f, 0f, 18f);

    private Grid? _contentGrid;
    private Border? _bodyBorder;
    private ScrollViewer? _infoScrollViewer;
    private Border? _stageBorder;
    private Canvas? _workbench;
    private Border? _focusCard;
    private Thumb? _focusDragThumb;
    private Border? _badge;
    private Border? _verticalGuide;
    private Border? _horizontalGuide;
    private Border? _guideXLabel;
    private Border? _guideYLabel;
    private CheckBox? _anchorFromRightBottomCheckBox;
    private CheckBox? _bringBadgeToFrontCheckBox;
    private CheckBox? _showGuidesCheckBox;
    private TextBlock? _anchorModeValueText;
    private TextBlock? _mixedAnchorValueText;
    private TextBlock? _positionValueText;
    private TextBlock? _sizeValueText;
    private TextBlock? _layerValueText;
    private TextBlock? _guideValueText;
    private TextBlock? _stageMetricsText;
    private TextBlock? _telemetrySummaryText;
    private TextBlock? _sceneAnchorBadgeText;
    private TextBlock? _sceneBadgeBodyText;
    private TextBlock? _guideXLabelText;
    private TextBlock? _guideYLabelText;
    private TextBlock? _stageMetricsBadgeText;
    private TextBlock? _inspectorDetailText;
    private bool _isStackedLayout;
    private float _focusHorizontalInset = 52f;
    private float _focusVerticalInset = 68f;
    private float _focusWidth = 228f;
    private float _focusHeight = 140f;

    public CanvasView()
    {
        InitializeComponent();
        EnsureReferences();
        WireInteractions();
        ApplySceneState();
        UpdateTelemetry();

        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "CanvasView",
                $"Constructed view workbench={CanvasThumbInvestigationLog.DescribeElement(_workbench)} focusCard={CanvasThumbInvestigationLog.DescribeElement(_focusCard)} thumb={CanvasThumbInvestigationLog.DescribeElement(_focusDragThumb)} badge={CanvasThumbInvestigationLog.DescribeElement(_badge)}");
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateResponsiveLayout(availableSize.X);
        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        UpdateResponsiveLayout(finalSize.X);
        var arranged = base.ArrangeOverride(finalSize);
        SyncOverlayLayout();
        UpdateTelemetry();
        return arranged;
    }

    private void WireInteractions()
    {
        if (_anchorFromRightBottomCheckBox != null)
        {
            _anchorFromRightBottomCheckBox.Checked += HandleToggleChanged;
            _anchorFromRightBottomCheckBox.Unchecked += HandleToggleChanged;
        }

        if (_bringBadgeToFrontCheckBox != null)
        {
            _bringBadgeToFrontCheckBox.Checked += HandleToggleChanged;
            _bringBadgeToFrontCheckBox.Unchecked += HandleToggleChanged;
        }

        if (_showGuidesCheckBox != null)
        {
            _showGuidesCheckBox.Checked += HandleToggleChanged;
            _showGuidesCheckBox.Unchecked += HandleToggleChanged;
        }

        if (_focusDragThumb != null)
        {
            _focusDragThumb.DragDelta += HandleFocusCardDragDelta;
        }

        AttachButton("MoveLeftButton", static view => view.NudgeHorizontal(-PositionStep));
        AttachButton("MoveRightButton", static view => view.NudgeHorizontal(PositionStep));
        AttachButton("MoveUpButton", static view => view.NudgeVertical(-PositionStep));
        AttachButton("MoveDownButton", static view => view.NudgeVertical(PositionStep));
        AttachButton("GrowSelectionButton", static view => view.ResizeFocus(20f, 16f));
        AttachButton("ShrinkSelectionButton", static view => view.ResizeFocus(-20f, -16f));
        AttachButton("ResetSceneButton", static view => view.ResetScene());
    }

    private void AttachButton(string name, Action<CanvasView> action)
    {
        if (this.FindName(name) is Button button)
        {
            button.Click += (_, _) => action(this);
        }
    }

    private void HandleToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        ApplySceneState();
    }

    private void HandleFocusCardDragDelta(object? sender, DragDeltaEventArgs args)
    {
        _ = sender;
        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "CanvasDrag",
                $"DragDelta horizontal={args.HorizontalChange:0.##} vertical={args.VerticalChange:0.##} thumb={CanvasThumbInvestigationLog.DescribeElement(_focusDragThumb)} card={CanvasThumbInvestigationLog.DescribeElement(_focusCard)} workbenchInvalidations=({_workbench?.MeasureInvalidationCount ?? -1},{_workbench?.ArrangeInvalidationCount ?? -1},{_workbench?.RenderInvalidationCount ?? -1})");
        }

        MoveFocusBy(args.HorizontalChange, args.VerticalChange);
    }

    private void NudgeHorizontal(float delta)
    {
        _focusHorizontalInset = ClampFocusHorizontalInset(_focusHorizontalInset + delta);
        ApplySceneState();
    }

    private void NudgeVertical(float delta)
    {
        _focusVerticalInset = ClampFocusVerticalInset(_focusVerticalInset + delta);
        ApplySceneState();
    }

    private void MoveFocusBy(float horizontalDelta, float verticalDelta)
    {
        var useRightBottom = _anchorFromRightBottomCheckBox?.IsChecked == true;
        var horizontalDirection = useRightBottom ? -1f : 1f;
        var verticalDirection = useRightBottom ? -1f : 1f;

        _focusHorizontalInset = ClampFocusHorizontalInset(_focusHorizontalInset + (horizontalDelta * horizontalDirection));
        _focusVerticalInset = ClampFocusVerticalInset(_focusVerticalInset + (verticalDelta * verticalDirection));
        ApplySceneState();
    }

    private void ResizeFocus(float widthDelta, float heightDelta)
    {
        _focusWidth = Clamp(_focusWidth + widthDelta, MinimumFocusWidth, MaximumFocusWidth);
        _focusHeight = Clamp(_focusHeight + heightDelta, MinimumFocusHeight, MaximumFocusHeight);
        ApplySceneState();
    }

    private void ResetScene()
    {
        _focusHorizontalInset = 52f;
        _focusVerticalInset = 68f;
        _focusWidth = 228f;
        _focusHeight = 140f;

        if (_anchorFromRightBottomCheckBox != null)
        {
            _anchorFromRightBottomCheckBox.IsChecked = false;
        }

        if (_bringBadgeToFrontCheckBox != null)
        {
            _bringBadgeToFrontCheckBox.IsChecked = true;
        }

        if (_showGuidesCheckBox != null)
        {
            _showGuidesCheckBox.IsChecked = true;
        }

        ApplySceneState();
    }

    private void EnsureReferences()
    {
        _contentGrid ??= this.FindName("CanvasViewContentGrid") as Grid;
        _bodyBorder ??= this.FindName("CanvasViewBodyBorder") as Border;
        _infoScrollViewer ??= this.FindName("CanvasViewInfoScrollViewer") as ScrollViewer;
        _stageBorder ??= this.FindName("CanvasWorkbenchStageBorder") as Border;
        _workbench ??= this.FindName("CanvasWorkbench") as Canvas;
        _focusCard ??= this.FindName("CanvasSceneRootCard") as Border;
        _focusDragThumb ??= this.FindName("CanvasSceneDragThumb") as Thumb;
        _badge ??= this.FindName("CanvasSceneBadge") as Border;
        _verticalGuide ??= this.FindName("CanvasVerticalGuide") as Border;
        _horizontalGuide ??= this.FindName("CanvasHorizontalGuide") as Border;
        _guideXLabel ??= this.FindName("CanvasGuideXLabel") as Border;
        _guideYLabel ??= this.FindName("CanvasGuideYLabel") as Border;
        _anchorFromRightBottomCheckBox ??= this.FindName("AnchorFromRightBottomCheckBox") as CheckBox;
        _bringBadgeToFrontCheckBox ??= this.FindName("BringBadgeToFrontCheckBox") as CheckBox;
        _showGuidesCheckBox ??= this.FindName("ShowGuidesCheckBox") as CheckBox;
        _anchorModeValueText ??= this.FindName("AnchorModeValueText") as TextBlock;
        _mixedAnchorValueText ??= this.FindName("MixedAnchorValueText") as TextBlock;
        _positionValueText ??= this.FindName("PositionValueText") as TextBlock;
        _sizeValueText ??= this.FindName("SizeValueText") as TextBlock;
        _layerValueText ??= this.FindName("LayerValueText") as TextBlock;
        _guideValueText ??= this.FindName("GuideValueText") as TextBlock;
        _stageMetricsText ??= this.FindName("StageMetricsText") as TextBlock;
        _telemetrySummaryText ??= this.FindName("TelemetrySummaryText") as TextBlock;
        _sceneAnchorBadgeText ??= this.FindName("CanvasSceneAnchorBadgeText") as TextBlock;
        _sceneBadgeBodyText ??= this.FindName("CanvasSceneBadgeBodyText") as TextBlock;
        _guideXLabelText ??= this.FindName("CanvasGuideXLabelText") as TextBlock;
        _guideYLabelText ??= this.FindName("CanvasGuideYLabelText") as TextBlock;
        _stageMetricsBadgeText ??= this.FindName("CanvasStageMetricsBadgeText") as TextBlock;
        _inspectorDetailText ??= this.FindName("CanvasInspectorDetailText") as TextBlock;
    }

    private void ApplySceneState()
    {
        EnsureReferences();

        if (_workbench == null || _focusCard == null || _badge == null)
        {
            return;
        }

        _focusCard.Width = _focusWidth;
        _focusCard.Height = _focusHeight;

        ApplyFocusAnchors();
        ApplyBadgeLayer();
        ApplyGuideVisibility();
        UpdateLiveText();

        _workbench.InvalidateMeasure();
        _workbench.InvalidateArrange();
        UpdateTelemetry();

        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "CanvasState",
                $"ApplySceneState inset=({_focusHorizontalInset:0.##},{_focusVerticalInset:0.##}) size=({_focusWidth:0.##},{_focusHeight:0.##}) thumb={CanvasThumbInvestigationLog.DescribeElement(_focusDragThumb)} card={CanvasThumbInvestigationLog.DescribeElement(_focusCard)} badge={CanvasThumbInvestigationLog.DescribeElement(_badge)} workbenchInvalidations=({_workbench.MeasureInvalidationCount},{_workbench.ArrangeInvalidationCount},{_workbench.RenderInvalidationCount})");
        }
    }

    private void ApplyFocusAnchors()
    {
        if (_focusCard == null)
        {
            return;
        }

        var useRightBottom = _anchorFromRightBottomCheckBox?.IsChecked == true;
        if (useRightBottom)
        {
            Canvas.SetLeft(_focusCard, float.NaN);
            Canvas.SetTop(_focusCard, float.NaN);
            Canvas.SetRight(_focusCard, _focusHorizontalInset);
            Canvas.SetBottom(_focusCard, _focusVerticalInset);
        }
        else
        {
            Canvas.SetRight(_focusCard, float.NaN);
            Canvas.SetBottom(_focusCard, float.NaN);
            Canvas.SetLeft(_focusCard, _focusHorizontalInset);
            Canvas.SetTop(_focusCard, _focusVerticalInset);
        }
    }

    private void ApplyBadgeLayer()
    {
        if (_badge == null || _focusCard == null)
        {
            return;
        }

        Panel.SetZIndex(_focusCard, 2);
        if (_focusDragThumb != null)
        {
            Panel.SetZIndex(_focusDragThumb, 3);
        }

        Panel.SetZIndex(_badge, _bringBadgeToFrontCheckBox?.IsChecked == true ? 4 : 1);
    }

    private void ApplyGuideVisibility()
    {
        var guidesVisible = _showGuidesCheckBox?.IsChecked == true;
        var visibility = guidesVisible ? Visibility.Visible : Visibility.Collapsed;

        if (_verticalGuide != null)
        {
            _verticalGuide.Visibility = visibility;
        }

        if (_horizontalGuide != null)
        {
            _horizontalGuide.Visibility = visibility;
        }

        if (_guideXLabel != null)
        {
            _guideXLabel.Visibility = visibility;
        }

        if (_guideYLabel != null)
        {
            _guideYLabel.Visibility = visibility;
        }
    }

    private void UpdateLiveText()
    {
        var useRightBottom = _anchorFromRightBottomCheckBox?.IsChecked == true;
        SetText(_sceneAnchorBadgeText, useRightBottom ? "Right / Bottom active" : "Left / Top active");
        SetText(
            _sceneBadgeBodyText,
            _bringBadgeToFrontCheckBox?.IsChecked == true ? "ZIndex = 4" : "ZIndex = 1");
        SetText(
            _inspectorDetailText,
            useRightBottom
                ? "Pinned diagnostics remain on the far edges while the focus card now measures its inset from the same edges."
                : "Pinned diagnostics stay on the far edges while the focus card uses direct Left/Top offsets from the canvas origin.");
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
        EnsureReferences();

        if (_contentGrid == null ||
            _bodyBorder == null ||
            _infoScrollViewer == null ||
            _workbench == null ||
            _contentGrid.ColumnDefinitions.Count < 2 ||
            _contentGrid.RowDefinitions.Count < 2)
        {
            return;
        }

        if (availableWidth <= 0f)
        {
            return;
        }

        var shouldStack = availableWidth < StackedLayoutBreakpoint;
        if (_isStackedLayout == shouldStack)
        {
            return;
        }

        _isStackedLayout = shouldStack;

        if (shouldStack)
        {
            _contentGrid.ColumnDefinitions[1].Width = new GridLength(0f, GridUnitType.Pixel);
            _contentGrid.RowDefinitions[1].Height = GridLength.Auto;

            _bodyBorder.Margin = new Thickness(0f, 0f, 0f, 10f);
            Grid.SetRow(_bodyBorder, 0);
            Grid.SetColumn(_bodyBorder, 0);

            Grid.SetRow(_infoScrollViewer, 1);
            Grid.SetColumn(_infoScrollViewer, 0);
            _infoScrollViewer.Height = StackedInfoRailHeight;
            _workbench.MinHeight = StackedWorkbenchMinHeight;
            _workbench.Height = StackedWorkbenchMinHeight;
            _workbench.Margin = StackedWorkbenchMargin;
        }
        else
        {
            _contentGrid.ColumnDefinitions[1].Width = new GridLength(InfoRailWidth, GridUnitType.Pixel);
            _contentGrid.RowDefinitions[1].Height = new GridLength(0f, GridUnitType.Pixel);

            _bodyBorder.Margin = new Thickness(0f, 0f, 10f, 0f);
            Grid.SetRow(_bodyBorder, 0);
            Grid.SetColumn(_bodyBorder, 0);

            Grid.SetRow(_infoScrollViewer, 0);
            Grid.SetColumn(_infoScrollViewer, 1);
            _infoScrollViewer.Height = float.NaN;
            _workbench.MinHeight = DefaultWorkbenchMinHeight;
            _workbench.Height = DefaultWorkbenchMinHeight;
            _workbench.Margin = DefaultWorkbenchMargin;
        }

        _contentGrid.InvalidateMeasure();
        _contentGrid.InvalidateArrange();
        _bodyBorder.InvalidateMeasure();
        _infoScrollViewer.InvalidateMeasure();
    }

    private void SyncOverlayLayout()
    {
        EnsureReferences();

        if (_workbench == null || _focusCard == null || _badge == null)
        {
            return;
        }

        var stageWidth = _workbench.ActualWidth;
        var stageHeight = _workbench.ActualHeight;
        if (stageWidth <= 0f || stageHeight <= 0f)
        {
            return;
        }

        var focusRect = _focusCard.LayoutSlot;
        var focusLocalX = focusRect.X - _workbench.LayoutSlot.X;
        var focusLocalY = focusRect.Y - _workbench.LayoutSlot.Y;
        var focusCenterX = Clamp(focusRect.X - _workbench.LayoutSlot.X + (focusRect.Width * 0.5f), 0f, stageWidth);
        var focusCenterY = Clamp(focusRect.Y - _workbench.LayoutSlot.Y + (focusRect.Height * 0.5f), 0f, stageHeight);

        if (_focusDragThumb != null)
        {
            var dragThumbWidth = ResolveActualWidth(_focusDragThumb);
            var dragThumbHeight = ResolveActualHeight(_focusDragThumb);
            SetCanvasLeft(
                _focusDragThumb,
                Clamp(
                    focusLocalX + focusRect.Width - dragThumbWidth - 14f,
                    12f,
                    MathF.Max(12f, stageWidth - dragThumbWidth - 12f)));
            SetCanvasTop(
                _focusDragThumb,
                Clamp(
                    focusLocalY + 14f,
                    12f,
                    MathF.Max(12f, stageHeight - dragThumbHeight - 12f)));
        }

        var badgeWidth = ResolveActualWidth(_badge);
        SetCanvasLeft(_badge, Clamp(focusCenterX + 32f, 12f, MathF.Max(12f, stageWidth - badgeWidth - 12f)));
        SetCanvasTop(_badge, Clamp(focusCenterY - 42f, 12f, MathF.Max(12f, stageHeight - ResolveActualHeight(_badge) - 12f)));

        if (_verticalGuide != null)
        {
            _verticalGuide.Height = stageHeight;
            SetCanvasLeft(_verticalGuide, Clamp(focusCenterX, 0f, stageWidth));
            SetCanvasTop(_verticalGuide, 0f);
        }

        if (_horizontalGuide != null)
        {
            _horizontalGuide.Width = stageWidth;
            SetCanvasLeft(_horizontalGuide, 0f);
            SetCanvasTop(_horizontalGuide, Clamp(focusCenterY, 0f, stageHeight));
        }

        if (_guideXLabel != null)
        {
            SetCanvasLeft(_guideXLabel, Clamp(focusCenterX + 8f, 8f, MathF.Max(8f, stageWidth - ResolveActualWidth(_guideXLabel) - 8f)));
            SetCanvasTop(_guideXLabel, 12f);
        }

        if (_guideYLabel != null)
        {
            SetCanvasLeft(_guideYLabel, 12f);
            SetCanvasTop(_guideYLabel, Clamp(focusCenterY + 8f, 12f, MathF.Max(12f, stageHeight - ResolveActualHeight(_guideYLabel) - 12f)));
        }

        if (CanvasThumbInvestigationLog.IsEnabled)
        {
            CanvasThumbInvestigationLog.Write(
                "CanvasLayout",
                $"SyncOverlayLayout focusCenter=({focusCenterX:0.##},{focusCenterY:0.##}) thumb={CanvasThumbInvestigationLog.DescribeElement(_focusDragThumb)} badge={CanvasThumbInvestigationLog.DescribeElement(_badge)} card={CanvasThumbInvestigationLog.DescribeElement(_focusCard)}");
        }

        SetText(_guideXLabelText, $"Center X {focusCenterX:0}");
        SetText(_guideYLabelText, $"Center Y {focusCenterY:0}");
    }

    private void UpdateTelemetry()
    {
        EnsureReferences();

        if (_workbench == null || _focusCard == null)
        {
            return;
        }

        var useRightBottom = _anchorFromRightBottomCheckBox?.IsChecked == true;
        var focusRect = _focusCard.LayoutSlot;
        var stageWidth = _workbench.ActualWidth;
        var stageHeight = _workbench.ActualHeight;
        var badgeZIndex = _badge != null ? Panel.GetZIndex(_badge) : 0;

        SetText(
            _anchorModeValueText,
            useRightBottom
                ? $"Focus card: Right={_focusHorizontalInset:0}, Bottom={_focusVerticalInset:0}"
                : $"Focus card: Left={_focusHorizontalInset:0}, Top={_focusVerticalInset:0}");
        SetText(_mixedAnchorValueText, "Mixed anchors: legend = Left+Bottom, chip = Right+Top, inspector = Right+Bottom.");
        SetText(_positionValueText, $"Focus bounds: X={focusRect.X:0}, Y={focusRect.Y:0}, Right={focusRect.X + focusRect.Width:0}, Bottom={focusRect.Y + focusRect.Height:0}.");
        SetText(_sizeValueText, $"Focus size: {_focusCard.ActualWidth:0} x {_focusCard.ActualHeight:0}. Desired size follows the live card instead of a placeholder host.");
        SetText(
            _layerValueText,
            badgeZIndex > Panel.GetZIndex(_focusCard)
                ? $"Layering: badge above focus card (badge ZIndex {badgeZIndex})."
                : $"Layering: badge behind focus card (badge ZIndex {badgeZIndex}).");
        SetText(
            _guideValueText,
            _showGuidesCheckBox?.IsChecked == true
                ? "Guides: visible and aligned to the focus-card center."
                : "Guides: hidden so the workbench reads like a pure overlay scene.");
        SetText(_stageMetricsText, $"Stage metrics: {_workbench.Children.Count} children, viewport {stageWidth:0} x {stageHeight:0}, layout mode {(_isStackedLayout ? "stacked info rail" : "wide side rail")}.");
        SetText(
            _telemetrySummaryText,
            useRightBottom
                ? "Right/Bottom mode makes the focus card measure its inset from the far edges while the pinned inspector keeps its own separate far-edge anchor."
                : "Left/Top mode keeps the focus card measured from the canvas origin while the legend, chip, and inspector continue to exercise mixed edge combinations.");
        SetText(_stageMetricsBadgeText, $"{stageWidth:0} x {stageHeight:0} stage");
    }

    private static float ResolveActualWidth(FrameworkElement element)
    {
        return !float.IsNaN(element.Width) ? element.Width : element.ActualWidth;
    }

    private static float ResolveActualHeight(FrameworkElement element)
    {
        return !float.IsNaN(element.Height) ? element.Height : element.ActualHeight;
    }

    private float ClampFocusHorizontalInset(float value)
    {
        return ClampFocusInset(value, GetAvailableStageWidth(), ResolveFocusWidth());
    }

    private float ClampFocusVerticalInset(float value)
    {
        return ClampFocusInset(value, GetAvailableStageHeight(), ResolveFocusHeight());
    }

    private float GetAvailableStageWidth()
    {
        EnsureReferences();

        if (_workbench == null)
        {
            return 0f;
        }

        if (_workbench.ActualWidth > 0f)
        {
            return _workbench.ActualWidth;
        }

        if (!float.IsNaN(_workbench.Width) && _workbench.Width > 0f)
        {
            return _workbench.Width;
        }

        return _workbench.MinWidth > 0f ? _workbench.MinWidth : 0f;
    }

    private float GetAvailableStageHeight()
    {
        EnsureReferences();

        if (_workbench == null)
        {
            return 0f;
        }

        if (_workbench.ActualHeight > 0f)
        {
            return _workbench.ActualHeight;
        }

        if (!float.IsNaN(_workbench.Height) && _workbench.Height > 0f)
        {
            return _workbench.Height;
        }

        return _workbench.MinHeight > 0f ? _workbench.MinHeight : 0f;
    }

    private float ResolveFocusWidth()
    {
        return _focusCard != null && ResolveActualWidth(_focusCard) > 0f
            ? ResolveActualWidth(_focusCard)
            : _focusWidth;
    }

    private float ResolveFocusHeight()
    {
        return _focusCard != null && ResolveActualHeight(_focusCard) > 0f
            ? ResolveActualHeight(_focusCard)
            : _focusHeight;
    }

    private static float ClampFocusInset(float value, float stageExtent, float focusExtent)
    {
        if (stageExtent <= 0f || focusExtent <= 0f)
        {
            return MathF.Max(MinimumFocusInset, value);
        }

        var maxInset = MathF.Max(MinimumFocusInset, stageExtent - focusExtent - MinimumFocusInset);
        return Clamp(value, MinimumFocusInset, maxInset);
    }

    private static void SetCanvasLeft(FrameworkElement element, float value)
    {
        if (!AreClose(Canvas.GetLeft(element), value))
        {
            Canvas.SetLeft(element, value);
        }
    }

    private static void SetCanvasTop(FrameworkElement element, float value)
    {
        if (!AreClose(Canvas.GetTop(element), value))
        {
            Canvas.SetTop(element, value);
        }
    }

    private static void SetText(TextBlock? target, string value)
    {
        if (target == null || string.Equals(target.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        target.Text = value;
    }

    private static bool AreClose(float left, float right)
    {
        return MathF.Abs(left - right) <= 0.01f || (float.IsNaN(left) && float.IsNaN(right));
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}




