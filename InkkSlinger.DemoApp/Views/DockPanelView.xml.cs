using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class DockPanelView : UserControl
{
    private const float StackedLayoutBreakpoint = 760f;
    private const float InfoRailWidth = 320f;
    private const float StackedInfoRailHeight = 240f;
    private const float DefaultWorkbenchMinHeight = 360f;
    private const float StackedWorkbenchMinHeight = 320f;
    private static readonly Thickness DefaultWorkbenchMargin = new(0f, 0f, 0f, 12f);
    private static readonly Thickness StackedWorkbenchMargin = new(0f, 0f, 0f, 18f);

    private Grid? _contentGrid;
    private Border? _bodyBorder;
    private ScrollViewer? _infoScrollViewer;
    private ScrollViewer? _workbenchScrollViewer;
    private DockPanel? _workbenchPanel;
    private Border? _workbenchStageBorder;
    private Border? _topRailCard;
    private Border? _bottomRailCard;
    private Border? _leftRailCard;
    private Border? _rightRailCard;
    private Border? _centerCanvasCard;
    private Border? _leftRailInboxCard;
    private Border? _leftRailPinnedViewsCard;
    private Border? _leftRailFiltersCard;
    private Border? _rightRailMetadataCard;
    private Border? _rightRailDiagnosticsCard;
    private Border? _centerModeCard;
    private Border? _centerBehaviorCard;
    private Border? _centerTelemetryCard;
    private Border? _centerFooterBadge;
    private Border? _dockOrderBorder;
    private CheckBox? _lastChildFillCheckBox;
    private CheckBox? _wideNavigationCheckBox;
    private CheckBox? _tallChromeCheckBox;
    private TextBlock? _topRailSummaryText;
    private TextBlock? _topRailBadgeText;
    private TextBlock? _bottomRailSummaryText;
    private TextBlock? _bottomRailBadgeText;
    private TextBlock? _leftRailSummaryText;
    private TextBlock? _rightRailSummaryText;
    private TextBlock? _centerHeadlineText;
    private TextBlock? _centerSummaryText;
    private TextBlock? _centerModeText;
    private TextBlock? _centerBehaviorText;
    private TextBlock? _centerTelemetryText;
    private TextBlock? _centerFooterText;
    private TextBlock? _dockOrderSummaryText;
    private TextBlock? _layoutModeValueText;
    private TextBlock? _fillBehaviorValueText;
    private TextBlock? _edgeMetricsText;
    private TextBlock? _centerMetricsText;
    private TextBlock? _sequenceValueText;
    private bool _isStackedLayout;

    public DockPanelView()
    {
        InitializeComponent();
        EnsureReferences();

        if (_lastChildFillCheckBox != null)
        {
            _lastChildFillCheckBox.Checked += HandleWorkbenchToggleChanged;
            _lastChildFillCheckBox.Unchecked += HandleWorkbenchToggleChanged;
        }

        if (_wideNavigationCheckBox != null)
        {
            _wideNavigationCheckBox.Checked += HandleWorkbenchToggleChanged;
            _wideNavigationCheckBox.Unchecked += HandleWorkbenchToggleChanged;
        }

        if (_tallChromeCheckBox != null)
        {
            _tallChromeCheckBox.Checked += HandleWorkbenchToggleChanged;
            _tallChromeCheckBox.Unchecked += HandleWorkbenchToggleChanged;
        }

        ApplyWorkbenchState();
        UpdateTelemetry();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateResponsiveLayout(availableSize.X);
        ApplyWorkbenchState();
        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        UpdateResponsiveLayout(finalSize.X);
        var arranged = base.ArrangeOverride(finalSize);
        UpdateTelemetry();
        return arranged;
    }

    private void HandleWorkbenchToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        ApplyWorkbenchState();
    }

    private void EnsureReferences()
    {
        _contentGrid ??= this.FindName("DockPanelViewContentGrid") as Grid;
        _bodyBorder ??= this.FindName("DockPanelViewBodyBorder") as Border;
        _infoScrollViewer ??= this.FindName("DockPanelViewInfoScrollViewer") as ScrollViewer;
        _workbenchScrollViewer ??= this.FindName("DockWorkbenchScrollViewer") as ScrollViewer;
        _workbenchPanel ??= this.FindName("DockWorkbenchPanel") as DockPanel;
        _workbenchStageBorder ??= this.FindName("DockWorkbenchStageBorder") as Border;
        _topRailCard ??= this.FindName("TopRailCard") as Border;
        _bottomRailCard ??= this.FindName("BottomRailCard") as Border;
        _leftRailCard ??= this.FindName("LeftRailCard") as Border;
        _rightRailCard ??= this.FindName("RightRailCard") as Border;
        _centerCanvasCard ??= this.FindName("CenterCanvasCard") as Border;
        _leftRailInboxCard ??= this.FindName("LeftRailInboxCard") as Border;
        _leftRailPinnedViewsCard ??= this.FindName("LeftRailPinnedViewsCard") as Border;
        _leftRailFiltersCard ??= this.FindName("LeftRailFiltersCard") as Border;
        _rightRailMetadataCard ??= this.FindName("RightRailMetadataCard") as Border;
        _rightRailDiagnosticsCard ??= this.FindName("RightRailDiagnosticsCard") as Border;
        _centerModeCard ??= this.FindName("CenterModeCard") as Border;
        _centerBehaviorCard ??= this.FindName("CenterBehaviorCard") as Border;
        _centerTelemetryCard ??= this.FindName("CenterTelemetryCard") as Border;
        _centerFooterBadge ??= this.FindName("CenterFooterBadge") as Border;
        _dockOrderBorder ??= this.FindName("DockOrderBorder") as Border;
        _lastChildFillCheckBox ??= this.FindName("LastChildFillCheckBox") as CheckBox;
        _wideNavigationCheckBox ??= this.FindName("WideNavigationCheckBox") as CheckBox;
        _tallChromeCheckBox ??= this.FindName("TallChromeCheckBox") as CheckBox;
        _topRailSummaryText ??= this.FindName("TopRailSummaryText") as TextBlock;
        _topRailBadgeText ??= this.FindName("TopRailBadgeText") as TextBlock;
        _bottomRailSummaryText ??= this.FindName("BottomRailSummaryText") as TextBlock;
        _bottomRailBadgeText ??= this.FindName("BottomRailBadgeText") as TextBlock;
        _leftRailSummaryText ??= this.FindName("LeftRailSummaryText") as TextBlock;
        _rightRailSummaryText ??= this.FindName("RightRailSummaryText") as TextBlock;
        _centerHeadlineText ??= this.FindName("CenterHeadlineText") as TextBlock;
        _centerSummaryText ??= this.FindName("CenterSummaryText") as TextBlock;
        _centerModeText ??= this.FindName("CenterModeText") as TextBlock;
        _centerBehaviorText ??= this.FindName("CenterBehaviorText") as TextBlock;
        _centerTelemetryText ??= this.FindName("CenterTelemetryText") as TextBlock;
        _centerFooterText ??= this.FindName("CenterFooterText") as TextBlock;
        _dockOrderSummaryText ??= this.FindName("DockOrderSummaryText") as TextBlock;
        _layoutModeValueText ??= this.FindName("LayoutModeValueText") as TextBlock;
        _fillBehaviorValueText ??= this.FindName("FillBehaviorValueText") as TextBlock;
        _edgeMetricsText ??= this.FindName("EdgeMetricsText") as TextBlock;
        _centerMetricsText ??= this.FindName("CenterMetricsText") as TextBlock;
        _sequenceValueText ??= this.FindName("SequenceValueText") as TextBlock;
    }

    private void ApplyWorkbenchState()
    {
        EnsureReferences();

        if (_workbenchPanel == null ||
            _topRailCard == null ||
            _bottomRailCard == null ||
            _leftRailCard == null ||
            _rightRailCard == null ||
            _centerCanvasCard == null ||
            _leftRailInboxCard == null ||
            _leftRailPinnedViewsCard == null ||
            _leftRailFiltersCard == null ||
            _rightRailMetadataCard == null ||
            _rightRailDiagnosticsCard == null ||
            _centerModeCard == null ||
            _centerBehaviorCard == null ||
            _centerTelemetryCard == null ||
            _centerFooterBadge == null ||
            _dockOrderBorder == null)
        {
            return;
        }

        var lastChildFill = _lastChildFillCheckBox?.IsChecked == true;
        var wideNavigation = _wideNavigationCheckBox?.IsChecked == true;
        var tallChrome = _tallChromeCheckBox?.IsChecked == true;
        var compactDockedLaneMode = !lastChildFill;
        var stageViewportWidth = ResolveWorkbenchViewportWidth();
        var laneWidths = ResolveLaneWidths(stageViewportWidth, wideNavigation, lastChildFill);
        var layoutChanged = false;

        _workbenchPanel.LastChildFill = lastChildFill;
        layoutChanged |= SetWidth(_leftRailCard, laneWidths.LeftWidth);
        layoutChanged |= SetWidth(_rightRailCard, laneWidths.RightWidth);
        layoutChanged |= SetWidth(_centerCanvasCard, lastChildFill ? float.NaN : laneWidths.CenterWidth);
        layoutChanged |= SetThickness(_leftRailCard, compactDockedLaneMode ? new Thickness(10f) : new Thickness(12f), static border => border.Padding, static (border, value) => border.Padding = value);
        layoutChanged |= SetThickness(_rightRailCard, compactDockedLaneMode ? new Thickness(10f) : new Thickness(12f), static border => border.Padding, static (border, value) => border.Padding = value);
        layoutChanged |= SetThickness(_centerCanvasCard, compactDockedLaneMode ? new Thickness(12f) : new Thickness(16f), static border => border.Padding, static (border, value) => border.Padding = value);
        layoutChanged |= SetThickness(_dockOrderBorder, compactDockedLaneMode ? new Thickness(10f) : new Thickness(12f), static border => border.Padding, static (border, value) => border.Padding = value);

        layoutChanged |= SetThickness(
            _topRailCard,
            tallChrome ? new Thickness(18f, 16f, 18f, 16f) : new Thickness(16f, 12f, 16f, 12f),
            static border => border.Padding,
            static (border, value) => border.Padding = value);
        layoutChanged |= SetThickness(
            _bottomRailCard,
            tallChrome ? new Thickness(14f, 12f, 14f, 12f) : new Thickness(14f, 8f, 14f, 8f),
            static border => border.Padding,
            static (border, value) => border.Padding = value);

        SetText(
            _topRailSummaryText,
            tallChrome
                ? "Taller chrome exaggerates top and bottom consumption so the remaining center rectangle changes more visibly."
                : "Top and bottom edges typically host shell chrome such as title areas, tabs, or status surfaces.");
        SetText(_topRailBadgeText, tallChrome ? "Expanded chrome" : "Primary chrome");
        SetText(
            _bottomRailSummaryText,
            tallChrome
                ? "Bottom status stays docked to the edge even as the top chrome grows, proving the center only gets what remains."
                : "Bottom rail can reserve persistent status or command surfaces without changing the center host.");
        SetText(_bottomRailBadgeText, tallChrome ? "Tall status lane" : "Status lane");
        SetText(
            _leftRailSummaryText,
            compactDockedLaneMode
                ? "Compact rail mode keeps only the key navigation lane visible so dock-left behavior stays readable at this viewport height."
                : wideNavigation
                ? "The wider navigation rail claims more of the remaining rectangle before the right rail and center workspace are arranged."
                : "Navigation or a tool rail usually docks first so the rest of the shell can size around it.");
        SetText(
            _rightRailSummaryText,
            compactDockedLaneMode
                ? "Compact inspector mode trims optional diagnostics so the docked right lane remains fully readable without spilling out of its card."
                : wideNavigation
                ? "The right rail still keeps its own edge after the wider left rail docks first, which makes the center visibly narrower."
                : "Inspector content keeps its own edge while the center stays focused on the main workspace.");
        SetText(_centerHeadlineText, lastChildFill ? "Center workspace" : "Final child becomes another docked lane");
        SetText(
            _centerSummaryText,
            lastChildFill
                ? "With LastChildFill on, the final child consumes the remaining rectangle after the four edge lanes finish docking."
                : "With LastChildFill off, the final child stops filling and instead docks using its own attached edge, which in this demo is Left.");
        SetText(_centerModeText, lastChildFill ? "Fill mode active" : "Fill mode disabled");
        SetText(
            _centerBehaviorText,
            lastChildFill
                ? "The workspace expands to the leftover rectangle created by the docked rails."
                : "The final child now participates in the same left-to-right docking sequence instead of owning the leftover rectangle.");
        SetText(
            _centerFooterText,
            lastChildFill
                ? "Declared last so it can fill by default"
                : "Declared last, but now docking left because fill is off");
        SetText(
            _dockOrderSummaryText,
            lastChildFill
                ? "The workbench declares children as Top, Bottom, Left, Right, then Center. With LastChildFill on, the final child takes whatever rectangle survives those four edge passes."
                : "The workbench still declares children as Top, Bottom, Left, Right, then Center. Turning LastChildFill off means the final child no longer fills and instead docks on its own edge like the rest of the sequence.");

        layoutChanged |= SetVisibility(_leftRailInboxCard, Visibility.Visible);
        layoutChanged |= SetVisibility(_leftRailPinnedViewsCard, compactDockedLaneMode ? Visibility.Collapsed : Visibility.Visible);
        layoutChanged |= SetVisibility(_leftRailFiltersCard, compactDockedLaneMode ? Visibility.Collapsed : Visibility.Visible);
        layoutChanged |= SetVisibility(_rightRailMetadataCard, Visibility.Visible);
        layoutChanged |= SetVisibility(_rightRailDiagnosticsCard, compactDockedLaneMode ? Visibility.Collapsed : Visibility.Visible);
        layoutChanged |= SetVisibility(_centerTelemetryCard, compactDockedLaneMode ? Visibility.Collapsed : Visibility.Visible);
        layoutChanged |= SetVisibility(_centerFooterBadge, compactDockedLaneMode ? Visibility.Collapsed : Visibility.Visible);

        layoutChanged |= SetThickness(_centerModeCard, compactDockedLaneMode ? new Thickness(0f, 0f, 0f, 6f) : new Thickness(0f, 0f, 0f, 8f), static border => border.Margin, static (border, value) => border.Margin = value);
        layoutChanged |= SetThickness(_centerBehaviorCard, compactDockedLaneMode ? Thickness.Empty : new Thickness(0f, 0f, 0f, 8f), static border => border.Margin, static (border, value) => border.Margin = value);

        if (compactDockedLaneMode)
        {
            SetText(_centerSummaryText, "The final child now docks left, so the demo switches to a compact lane summary that fits inside the same preview height.");
            SetText(_centerBehaviorText, "Compact mode keeps the dock-left state readable without lane content bleeding past the card bounds.");
        }

        if (layoutChanged)
        {
            _workbenchPanel.InvalidateMeasure();
            _workbenchPanel.InvalidateArrange();
        }

        UpdateTelemetry();
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
        EnsureReferences();

        if (_contentGrid == null ||
            _bodyBorder == null ||
            _infoScrollViewer == null ||
            _workbenchPanel == null ||
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
            _workbenchPanel.MinHeight = StackedWorkbenchMinHeight;
            _workbenchPanel.Margin = StackedWorkbenchMargin;
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
            _workbenchPanel.MinHeight = DefaultWorkbenchMinHeight;
            _workbenchPanel.Margin = DefaultWorkbenchMargin;
        }

        _contentGrid.InvalidateMeasure();
        _contentGrid.InvalidateArrange();
        _bodyBorder.InvalidateMeasure();
        _infoScrollViewer.InvalidateMeasure();
    }

    private void UpdateTelemetry()
    {
        EnsureReferences();

        if (_workbenchPanel == null ||
            _topRailCard == null ||
            _bottomRailCard == null ||
            _leftRailCard == null ||
            _rightRailCard == null ||
            _centerCanvasCard == null)
        {
            return;
        }

        var lastChildFill = _workbenchPanel.LastChildFill;

        SetText(
            _layoutModeValueText,
            $"Layout mode: {(_isStackedLayout ? "stacked info rail" : "wide info rail")}. The same DockPanel stays in place while the catalog chrome moves around it.");
        SetText(
            _fillBehaviorValueText,
            lastChildFill
                ? "Fill behavior: the last child owns the remaining rectangle after the four docked rails finish arranging."
                : "Fill behavior: the last child no longer fills and instead docks using its attached edge, so the shell becomes a full sequence of docked lanes.");
        SetText(
            _edgeMetricsText,
            $"Edges: Top={_topRailCard.ActualHeight:0}h | Bottom={_bottomRailCard.ActualHeight:0}h | Left={_leftRailCard.ActualWidth:0}w | Right={_rightRailCard.ActualWidth:0}w.");
        SetText(
            _centerMetricsText,
            $"Center: {_centerCanvasCard.ActualWidth:0} x {_centerCanvasCard.ActualHeight:0}. DockPanel: {_workbenchPanel.ActualWidth:0} x {_workbenchPanel.ActualHeight:0}.");
        SetText(
            _sequenceValueText,
            lastChildFill
                ? "Sequence: Top -> Bottom -> Left -> Right -> Center(fill)."
                : "Sequence: Top -> Bottom -> Left -> Right -> Center(dock left)."
        );
        SetText(
            _centerTelemetryText,
            $"Arranged workspace size is {_centerCanvasCard.ActualWidth:0} by {_centerCanvasCard.ActualHeight:0}. Toggle wide nav or tall chrome to see which edge consumes the remaining rectangle first.");
    }

    private float ResolveWorkbenchViewportWidth()
    {
        EnsureReferences();

        if (_workbenchScrollViewer != null && _workbenchScrollViewer.ViewportWidth > 0f)
        {
            return _workbenchScrollViewer.ViewportWidth;
        }

        if (_workbenchStageBorder != null && _workbenchStageBorder.ActualWidth > 24f)
        {
            return _workbenchStageBorder.ActualWidth - 24f;
        }

        return 0f;
    }

    private static DockWorkbenchLaneWidths ResolveLaneWidths(float viewportWidth, bool wideNavigation, bool lastChildFill)
    {
        var baseLeftFootprint = wideNavigation ? 246f : 186f;
        var baseRightFootprint = wideNavigation ? 214f : 198f;
        const float minLeftFootprint = 142f;
        const float minRightFootprint = 150f;
        const float minCenterWidth = 160f;
        const float baseCenterWidth = 252f;
        const float laneGap = 10f;

        if (viewportWidth <= 0f)
        {
            return new DockWorkbenchLaneWidths(
                baseLeftFootprint - laneGap,
                baseRightFootprint - laneGap,
                lastChildFill ? float.NaN : baseCenterWidth);
        }

        if (lastChildFill)
        {
            const float minimumRemainingWidth = 160f;
            var maxSideFootprint = MathF.Max(minLeftFootprint + minRightFootprint, viewportWidth - minimumRemainingWidth);
            var baseSideFootprint = baseLeftFootprint + baseRightFootprint;
            var factor = baseSideFootprint <= 0f
                ? 1f
                : MathF.Min(1f, maxSideFootprint / baseSideFootprint);

            var leftFootprint = MathF.Max(minLeftFootprint, baseLeftFootprint * factor);
            var rightFootprint = MathF.Max(minRightFootprint, baseRightFootprint * factor);

            return new DockWorkbenchLaneWidths(
                MathF.Max(96f, leftFootprint - laneGap),
                MathF.Max(104f, rightFootprint - laneGap),
                float.NaN);
        }

        var minTotal = minLeftFootprint + minRightFootprint + minCenterWidth;
        var baseTotal = baseLeftFootprint + baseRightFootprint + baseCenterWidth;
        var interpolation = baseTotal <= minTotal || viewportWidth >= baseTotal
            ? 1f
            : viewportWidth <= minTotal
                ? 0f
                : (viewportWidth - minTotal) / (baseTotal - minTotal);

        var leftResponsiveFootprint = Lerp(minLeftFootprint, baseLeftFootprint, interpolation);
        var rightResponsiveFootprint = Lerp(minRightFootprint, baseRightFootprint, interpolation);
        var centerResponsiveWidth = Lerp(minCenterWidth, baseCenterWidth, interpolation);

        return new DockWorkbenchLaneWidths(
            MathF.Max(96f, leftResponsiveFootprint - laneGap),
            MathF.Max(104f, rightResponsiveFootprint - laneGap),
            MathF.Max(minCenterWidth, centerResponsiveWidth));
    }

    private static float Lerp(float start, float end, float amount)
    {
        return start + ((end - start) * amount);
    }

    private static bool SetWidth(FrameworkElement? target, float value)
    {
        if (target == null || AreFloatValuesEqual(target.Width, value))
        {
            return false;
        }

        target.Width = value;
        return true;
    }

    private static bool SetVisibility(UIElement? target, Visibility value)
    {
        if (target == null || target.Visibility == value)
        {
            return false;
        }

        target.Visibility = value;
        return true;
    }

    private static bool SetThickness<TElement>(TElement? target, Thickness value, Func<TElement, Thickness> getter, Action<TElement, Thickness> setter)
        where TElement : class
    {
        if (target == null || AreThicknessValuesEqual(getter(target), value))
        {
            return false;
        }

        setter(target, value);
        return true;
    }

    private static bool AreFloatValuesEqual(float left, float right)
    {
        return (float.IsNaN(left) && float.IsNaN(right)) || MathF.Abs(left - right) < 0.01f;
    }

    private static bool AreThicknessValuesEqual(Thickness left, Thickness right)
    {
        return AreFloatValuesEqual(left.Left, right.Left) &&
               AreFloatValuesEqual(left.Top, right.Top) &&
               AreFloatValuesEqual(left.Right, right.Right) &&
               AreFloatValuesEqual(left.Bottom, right.Bottom);
    }

    private static void SetText(TextBlock? target, string value)
    {
        if (target == null || string.Equals(target.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        target.Text = value;
    }

    private readonly record struct DockWorkbenchLaneWidths(float LeftWidth, float RightWidth, float CenterWidth);
}




