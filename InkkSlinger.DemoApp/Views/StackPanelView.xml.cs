using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class StackPanelView : UserControl
{
    private const float StackedLayoutBreakpoint = 1024f;
    private const float InfoRailWidth = 320f;
    private const float StackedInfoRailHeight = 280f;
    private const float DefaultStageHeight = 392f;
    private const float TightStageHeight = 296f;

    private readonly List<StackPanelCardSpec> _cards = CreateBaseCards();
    private int _nextInjectedCardNumber = 1;

    private bool _isStackedLayout;
    private bool _suppressEvents;
    private string _lastAction = "Initial state shows a classic vertical StackPanel with a constrained viewport so primary-axis growth is immediately observable.";

    public StackPanelView()
    {
        InitializeComponent();
        PopulateChoiceControls();
        ApplyInitialState();
        RebuildWorkbenchPanel();
        UpdateInspector();
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
        UpdateInspector();
        return arranged;
    }

    private void PopulateChoiceControls()
    {
        if (OrientationComboBox.Items.Count == 0)
        {
            OrientationComboBox.Items.Add(nameof(Orientation.Vertical));
            OrientationComboBox.Items.Add(nameof(Orientation.Horizontal));
        }

        if (CrossAxisAlignmentComboBox.Items.Count == 0)
        {
            CrossAxisAlignmentComboBox.Items.Add(nameof(CrossAxisAlignmentMode.Stretch));
            CrossAxisAlignmentComboBox.Items.Add(nameof(CrossAxisAlignmentMode.Start));
            CrossAxisAlignmentComboBox.Items.Add(nameof(CrossAxisAlignmentMode.Center));
            CrossAxisAlignmentComboBox.Items.Add(nameof(CrossAxisAlignmentMode.End));
        }
    }

    private void ApplyInitialState()
    {
        _suppressEvents = true;

        OrientationComboBox.SelectedItem = nameof(Orientation.Vertical);
        CrossAxisAlignmentComboBox.SelectedItem = nameof(CrossAxisAlignmentMode.Stretch);
        TightViewportCheckBox.IsChecked = true;
        CollapseSecondaryCardCheckBox.IsChecked = false;
        EmphasizeHeroCardCheckBox.IsChecked = false;

        _suppressEvents = false;
    }

    private void OnWorkbenchOptionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressEvents)
        {
            return;
        }

        _lastAction = BuildOptionChangeAction();
        RebuildWorkbenchPanel();
        UpdateInspector();
    }

    private void OnWorkbenchToggleChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_suppressEvents)
        {
            return;
        }

        _lastAction = BuildOptionChangeAction();
        RebuildWorkbenchPanel();
        UpdateInspector();
    }

    private void OnAddChildClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        var spec = CreateInjectedCard(_nextInjectedCardNumber++);
        _cards.Add(spec);

        ApplyWorkbenchProperties();
        WorkbenchPanel.AddChild(BuildWorkbenchCard(spec, _cards.Count - 1, ResolveOrientation(), ResolveCrossAxisAlignmentMode(), IsHeroEmphasized(), IsSecondaryCollapsed()));
        _lastAction = $"Added child {_cards.Count:00}: {spec.Title}. StackPanel child order is now {_cards.Count} items long.";
        UpdateInspector();
    }

    private void OnRemoveLastClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_cards.Count <= 1)
        {
            _lastAction = "Remove last ignored because the workbench keeps at least one child alive for layout inspection.";
            UpdateInspector();
            return;
        }

        var removed = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        WorkbenchPanel.RemoveChildAt(_cards.Count);
        _lastAction = $"Removed the tail child ({removed.Title}). The remaining children keep their declaration order.";
        UpdateInspector();
    }

    private void OnMoveFirstToEndClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        if (_cards.Count <= 1)
        {
            _lastAction = "Move ignored because a single-child StackPanel has no order change to show.";
            UpdateInspector();
            return;
        }

        var first = _cards[0];
        _cards.RemoveAt(0);
        _cards.Add(first);

        if (!WorkbenchPanel.MoveChildRange(0, 1, WorkbenchPanel.Children.Count))
        {
            RebuildWorkbenchPanel();
        }

        _lastAction = $"Moved the first child ({first.Title}) to the tail. StackPanel now renders the same children in the mutated order.";
        UpdateInspector();
    }

    private void OnResetStackClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;

        _cards.Clear();
        _cards.AddRange(CreateBaseCards());
        _nextInjectedCardNumber = 1;
        _lastAction = "Reset restored the baseline child collection so orientation and visibility comparisons are easy to read again.";
        RebuildWorkbenchPanel();
        UpdateInspector();
    }

    private void RebuildWorkbenchPanel()
    {
        ApplyWorkbenchProperties();

        while (WorkbenchPanel.Children.Count > 0)
        {
            WorkbenchPanel.RemoveChildAt(WorkbenchPanel.Children.Count - 1);
        }

        var orientation = ResolveOrientation();
        var alignmentMode = ResolveCrossAxisAlignmentMode();
        var emphasizeHero = IsHeroEmphasized();
        var collapseSecondary = IsSecondaryCollapsed();
        for (var i = 0; i < _cards.Count; i++)
        {
            WorkbenchPanel.AddChild(BuildWorkbenchCard(_cards[i], i, orientation, alignmentMode, emphasizeHero, collapseSecondary));
        }
    }

    private void ApplyWorkbenchProperties()
    {
        WorkbenchPanel.Orientation = ResolveOrientation();
        WorkbenchStageBorder.Height = TightViewportCheckBox.IsChecked == true
            ? TightStageHeight
            : DefaultStageHeight;
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
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
            StackPanelViewContentGrid.ColumnDefinitions[1].Width = new GridLength(0f, GridUnitType.Pixel);
            StackPanelViewContentGrid.RowDefinitions[1].Height = GridLength.Auto;

            StackPanelViewBodyBorder.Margin = new Thickness(0f, 0f, 0f, 10f);
            Grid.SetRow(StackPanelViewBodyBorder, 0);
            Grid.SetColumn(StackPanelViewBodyBorder, 0);

            Grid.SetRow(StackPanelInfoScrollViewer, 1);
            Grid.SetColumn(StackPanelInfoScrollViewer, 0);
            StackPanelInfoScrollViewer.Height = StackedInfoRailHeight;
            return;
        }

        StackPanelViewContentGrid.ColumnDefinitions[1].Width = new GridLength(InfoRailWidth, GridUnitType.Pixel);
        StackPanelViewContentGrid.RowDefinitions[1].Height = new GridLength(0f, GridUnitType.Pixel);

        StackPanelViewBodyBorder.Margin = new Thickness(0f, 0f, 10f, 0f);
        Grid.SetRow(StackPanelViewBodyBorder, 0);
        Grid.SetColumn(StackPanelViewBodyBorder, 0);

        Grid.SetRow(StackPanelInfoScrollViewer, 0);
        Grid.SetColumn(StackPanelInfoScrollViewer, 1);
        StackPanelInfoScrollViewer.Height = float.NaN;
    }

    private void UpdateInspector()
    {
        var orientation = ResolveOrientation();
        var alignmentMode = ResolveCrossAxisAlignmentMode();
        var visibleCount = 0;
        var collapsedCount = 0;
        var primaryDesired = 0f;
        var crossDesired = 0f;

        for (var i = 0; i < WorkbenchPanel.Children.Count; i++)
        {
            if (WorkbenchPanel.Children[i] is not FrameworkElement child)
            {
                continue;
            }

            if (child.Visibility == Visibility.Collapsed)
            {
                collapsedCount++;
                continue;
            }

            visibleCount++;
            if (orientation == Orientation.Vertical)
            {
                primaryDesired += child.DesiredSize.Y;
                crossDesired = MathF.Max(crossDesired, child.DesiredSize.X);
            }
            else
            {
                primaryDesired += child.DesiredSize.X;
                crossDesired = MathF.Max(crossDesired, child.DesiredSize.Y);
            }
        }

        SetText(
            WorkbenchNarrativeText,
            BuildNarrative(orientation, alignmentMode, visibleCount, collapsedCount));
        SetText(WorkbenchMutationText, _lastAction);
        SetText(
            PanelMetricsText,
            $"Orientation: {orientation} | Children: {_cards.Count} | Visible: {visibleCount} | Desired: {WorkbenchPanel.DesiredSize.X:0} x {WorkbenchPanel.DesiredSize.Y:0} | Actual: {WorkbenchPanel.ActualWidth:0} x {WorkbenchPanel.ActualHeight:0}.");
        SetText(ChildOrderText, $"Order: {BuildOrderSummary()}");
        SetText(
            VisibilitySummaryText,
            $"Visible primary span: {primaryDesired:0}. Cross-axis max: {crossDesired:0}. Collapsed children: {collapsedCount}. Collapsed nodes stay in the collection but stop participating in layout.");
        SetText(AlignmentSummaryText, BuildAlignmentSummary(orientation, alignmentMode));
        SetText(ScrollSummaryText, BuildScrollSummary(orientation));
        SetText(ObservationText, BuildObservation(orientation, alignmentMode, visibleCount, collapsedCount));
    }

    private string BuildNarrative(Orientation orientation, CrossAxisAlignmentMode alignmentMode, int visibleCount, int collapsedCount)
    {
        var primaryAxis = orientation == Orientation.Vertical ? "heights accumulate" : "widths accumulate";
        var alignment = alignmentMode switch
        {
            CrossAxisAlignmentMode.Stretch => "children stretch across the cross axis unless their own constraints say otherwise",
            CrossAxisAlignmentMode.Start => "children hug the leading edge of the cross axis",
            CrossAxisAlignmentMode.Center => "children stay centered on the cross axis",
            _ => "children hug the trailing edge of the cross axis"
        };

        return $"{orientation} mode means {primaryAxis}. {alignment}. The workbench currently has {visibleCount} visible children and {collapsedCount} collapsed child{(collapsedCount == 1 ? string.Empty : "ren")}.";
    }

    private string BuildAlignmentSummary(Orientation orientation, CrossAxisAlignmentMode alignmentMode)
    {
        var axisName = orientation == Orientation.Vertical ? "HorizontalAlignment" : "VerticalAlignment";
        var meaning = alignmentMode switch
        {
            CrossAxisAlignmentMode.Stretch => "Each child receives the full cross-axis slot from the StackPanel.",
            CrossAxisAlignmentMode.Start => "Each child keeps a fixed cross-axis size and pins to the leading edge.",
            CrossAxisAlignmentMode.Center => "Each child keeps a fixed cross-axis size and is centered within the slot.",
            _ => "Each child keeps a fixed cross-axis size and pins to the trailing edge."
        };

        return $"Cross-axis mode: {alignmentMode}. In {orientation} orientation this is demonstrated through {axisName}. {meaning}";
    }

    private string BuildScrollSummary(Orientation orientation)
    {
        var viewportWidth = WorkbenchStageBorder.ActualWidth;
        var viewportHeight = WorkbenchStageBorder.ActualHeight;
        var primaryAxis = orientation == Orientation.Vertical ? "vertical" : "horizontal";
        var viewportMode = TightViewportCheckBox.IsChecked == true ? "tight" : "relaxed";

        return $"Viewport: {viewportWidth:0} x {viewportHeight:0} in {viewportMode} mode. The ScrollViewer owns overflow while the StackPanel keeps growing on its {primaryAxis} axis.";
    }

    private string BuildObservation(Orientation orientation, CrossAxisAlignmentMode alignmentMode, int visibleCount, int collapsedCount)
    {
        if (orientation == Orientation.Horizontal)
        {
            return alignmentMode == CrossAxisAlignmentMode.Stretch
                ? $"Horizontal plus stretch reads like a command rail: {visibleCount} visible cards share the full stage height while their widths keep extending the row."
                : $"Horizontal plus {alignmentMode.ToString().ToLowerInvariant()} alignment shows that cross-axis placement belongs to the children, not the StackPanel itself.";
        }

        if (collapsedCount > 0)
        {
            return "The collapsed card proves the usual WPF behavior: the child still exists in the collection, but it no longer consumes measured or arranged space.";
        }

        return alignmentMode == CrossAxisAlignmentMode.Stretch
            ? "Vertical plus stretch is the canonical settings-column layout: order drives the flow, and the children widen to the available surface."
            : "Vertical non-stretch alignment is useful for cards, badges, or inspectors that should not occupy the full available width.";
    }

    private string BuildOrderSummary()
    {
        var parts = new List<string>(_cards.Count);
        var collapseSecondary = IsSecondaryCollapsed();

        for (var i = 0; i < _cards.Count; i++)
        {
            var spec = _cards[i];
            var collapsedSuffix = collapseSecondary && spec.Kind == StackPanelCardKind.Secondary
                ? " (collapsed)"
                : string.Empty;
            parts.Add($"{i + 1}:{spec.ShortLabel}{collapsedSuffix}");
        }

        return string.Join(" -> ", parts);
    }

    private string BuildOptionChangeAction()
    {
        return $"Updated the live StackPanel to {ResolveOrientation().ToString().ToLowerInvariant()} orientation with {ResolveCrossAxisAlignmentMode().ToString().ToLowerInvariant()} cross-axis alignment, {(IsSecondaryCollapsed() ? "the secondary child collapsed" : "all baseline children visible")}, and {(IsHeroEmphasized() ? "an emphasized hero card" : "baseline card sizing")}.";
    }

    private Orientation ResolveOrientation()
    {
        return string.Equals(OrientationComboBox.SelectedItem as string, nameof(Orientation.Horizontal), StringComparison.Ordinal)
            ? Orientation.Horizontal
            : Orientation.Vertical;
    }

    private CrossAxisAlignmentMode ResolveCrossAxisAlignmentMode()
    {
        return (CrossAxisAlignmentComboBox.SelectedItem as string) switch
        {
            nameof(CrossAxisAlignmentMode.Start) => CrossAxisAlignmentMode.Start,
            nameof(CrossAxisAlignmentMode.Center) => CrossAxisAlignmentMode.Center,
            nameof(CrossAxisAlignmentMode.End) => CrossAxisAlignmentMode.End,
            _ => CrossAxisAlignmentMode.Stretch
        };
    }

    private bool IsHeroEmphasized() => EmphasizeHeroCardCheckBox.IsChecked == true;

    private bool IsSecondaryCollapsed() => CollapseSecondaryCardCheckBox.IsChecked == true;

    private static UIElement BuildWorkbenchCard(
        StackPanelCardSpec spec,
        int index,
        Orientation orientation,
        CrossAxisAlignmentMode alignmentMode,
        bool emphasizeHero,
        bool collapseSecondary)
    {
        var heroBoost = emphasizeHero && spec.Kind == StackPanelCardKind.Hero;

        var title = new TextBlock
        {
            Text = spec.Title,
            Foreground = new Color(231, 242, 248),
            FontWeight = "SemiBold",
            TextWrapping = TextWrapping.Wrap
        };

        var summary = new TextBlock
        {
            Text = spec.Summary,
            Foreground = new Color(184, 205, 217),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 4f, 0f, 0f)
        };

        var meta = new TextBlock
        {
            Text = $"{spec.Meta} • child {index + 1:00}",
            Foreground = new Color(143, 177, 196),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 8f, 0f, 0f)
        };

        var chipRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0f, 10f, 0f, 0f)
        };
        chipRow.AddChild(CreatePill(spec.BadgeLabel, spec.BadgeBackground, spec.BadgeBorder, spec.BadgeForeground, new Thickness(0f, 0f, 8f, 0f)));
        chipRow.AddChild(CreatePill(spec.ShortLabel, new Color(36, 49, 60), new Color(90, 114, 128), new Color(220, 236, 246), Thickness.Empty));

        var textStack = new StackPanel();
        textStack.AddChild(title);
        textStack.AddChild(summary);
        textStack.AddChild(meta);
        textStack.AddChild(chipRow);

        var badge = new Border
        {
            Background = spec.BadgeBackground,
            BorderBrush = spec.BadgeBorder,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(12f),
            Padding = new Thickness(10f, 4f, 10f, 4f),
            Margin = new Thickness(12f, 0f, 0f, 0f),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = spec.BadgeLabel,
                Foreground = spec.BadgeForeground,
                FontWeight = "SemiBold",
                TextWrapping = TextWrapping.Wrap
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1f, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(textStack, 0);
        Grid.SetColumn(badge, 1);
        grid.AddChild(textStack);
        grid.AddChild(badge);

        var isHorizontal = orientation == Orientation.Horizontal;
        var margin = isHorizontal
            ? new Thickness(0f, 0f, 10f, 0f)
            : new Thickness(0f, 0f, 0f, 10f);
        var padding = heroBoost
            ? new Thickness(16f, 14f, 16f, 14f)
            : new Thickness(14f, 12f, 14f, 12f);
        var background = heroBoost
            ? Lighten(spec.Surface, 10)
            : spec.Surface;
        var borderBrush = heroBoost
            ? Lighten(spec.Border, 18)
            : spec.Border;

        var card = new Border
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(8f),
            Padding = padding,
            Margin = margin,
            Child = grid,
            Visibility = collapseSecondary && spec.Kind == StackPanelCardKind.Secondary
                ? Visibility.Collapsed
                : Visibility.Visible
        };

        if (isHorizontal)
        {
            card.MinWidth = heroBoost ? spec.HorizontalWidth + 42f : spec.HorizontalWidth;
            card.MinHeight = heroBoost ? spec.HorizontalHeight + 20f : spec.HorizontalHeight;
            ApplyVerticalAlignment(card, alignmentMode);
        }
        else
        {
            card.MinHeight = heroBoost ? spec.VerticalHeight + 28f : spec.VerticalHeight;
            ApplyHorizontalAlignment(card, alignmentMode, heroBoost ? spec.VerticalWidth + 40f : spec.VerticalWidth);
        }

        return card;
    }

    private static void ApplyHorizontalAlignment(Border target, CrossAxisAlignmentMode mode, float fixedWidth)
    {
        target.VerticalAlignment = VerticalAlignment.Top;
        target.Height = float.NaN;
        switch (mode)
        {
            case CrossAxisAlignmentMode.Start:
                target.HorizontalAlignment = HorizontalAlignment.Left;
                target.Width = fixedWidth;
                break;
            case CrossAxisAlignmentMode.Center:
                target.HorizontalAlignment = HorizontalAlignment.Center;
                target.Width = fixedWidth;
                break;
            case CrossAxisAlignmentMode.End:
                target.HorizontalAlignment = HorizontalAlignment.Right;
                target.Width = fixedWidth;
                break;
            default:
                target.HorizontalAlignment = HorizontalAlignment.Stretch;
                target.Width = float.NaN;
                break;
        }
    }

    private static void ApplyVerticalAlignment(Border target, CrossAxisAlignmentMode mode)
    {
        target.HorizontalAlignment = HorizontalAlignment.Left;
        target.Width = float.NaN;
        switch (mode)
        {
            case CrossAxisAlignmentMode.Start:
                target.VerticalAlignment = VerticalAlignment.Top;
                target.Height = float.NaN;
                break;
            case CrossAxisAlignmentMode.Center:
                target.VerticalAlignment = VerticalAlignment.Center;
                target.Height = float.NaN;
                break;
            case CrossAxisAlignmentMode.End:
                target.VerticalAlignment = VerticalAlignment.Bottom;
                target.Height = float.NaN;
                break;
            default:
                target.VerticalAlignment = VerticalAlignment.Stretch;
                target.Height = float.NaN;
                break;
        }
    }

    private static Border CreatePill(string text, Color background, Color borderBrush, Color foreground, Thickness margin)
    {
        return new Border
        {
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1f),
            CornerRadius = new CornerRadius(12f),
            Padding = new Thickness(10f, 4f, 10f, 4f),
            Margin = margin,
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static void SetText(TextBlock? target, string text)
    {
        if (target != null && !string.Equals(target.Text, text, StringComparison.Ordinal))
        {
            target.Text = text;
        }
    }

    private static Color Lighten(Color color, int delta)
    {
        static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);
        return new Color(
            Clamp(color.R + delta),
            Clamp(color.G + delta),
            Clamp(color.B + delta),
            color.A);
    }

    private static List<StackPanelCardSpec> CreateBaseCards()
    {
        return new List<StackPanelCardSpec>
        {
            new(
                StackPanelCardKind.Hero,
                "Hero task lane",
                "Top-level release work stays first in the declaration order so the most important summary appears at the leading edge of the stack.",
                "Owner Avery • Release review",
                "Hero",
                new Color(21, 32, 40),
                new Color(78, 128, 159),
                new Color(27, 54, 69),
                new Color(98, 154, 187),
                new Color(220, 242, 252),
                120f,
                336f,
                292f,
                212f),
            new(
                StackPanelCardKind.Secondary,
                "Secondary review lane",
                "This card exists specifically so the demo can prove that Visibility.Collapsed removes a child from StackPanel measurement and arrangement.",
                "Owner Morgan • Triage",
                "Collapsible",
                new Color(23, 30, 37),
                new Color(70, 100, 120),
                new Color(43, 60, 73),
                new Color(92, 125, 146),
                new Color(218, 235, 244),
                98f,
                292f,
                248f,
                186f),
            new(
                StackPanelCardKind.Standard,
                "Inspector snapshot",
                "A StackPanel often reads best when each child is a self-contained section: title, summary, then a short row of badges or actions.",
                "Owner Riley • Layout diagnostics",
                "Inspector",
                new Color(18, 26, 33),
                new Color(57, 85, 101),
                new Color(35, 56, 69),
                new Color(79, 112, 129),
                new Color(213, 233, 243),
                90f,
                260f,
                228f,
                172f),
            new(
                StackPanelCardKind.Standard,
                "Command footer",
                "Footer chrome is still just another child. StackPanel does not care whether the section is a toolbar, card, or status lane.",
                "Owner Jordan • Commands",
                "Footer",
                new Color(20, 29, 24),
                new Color(71, 103, 78),
                new Color(38, 58, 42),
                new Color(93, 130, 100),
                new Color(224, 243, 225),
                84f,
                244f,
                214f,
                160f)
        };
    }

    private static StackPanelCardSpec CreateInjectedCard(int number)
    {
        var paletteIndex = number % 3;
        return paletteIndex switch
        {
            1 => new StackPanelCardSpec(
                StackPanelCardKind.Injected,
                $"Injected child {number}",
                "A runtime-added section proves that direct child manipulation is part of the control story, not a separate templating feature.",
                "Owner Casey • Runtime insertion",
                "Injected",
                new Color(24, 27, 40),
                new Color(87, 94, 145),
                new Color(42, 49, 82),
                new Color(108, 121, 187),
                new Color(229, 233, 255),
                86f,
                252f,
                222f,
                164f),
            2 => new StackPanelCardSpec(
                StackPanelCardKind.Injected,
                $"Injected child {number}",
                "Runtime insertion also makes it obvious that StackPanel respects collection order exactly as the view author changes it.",
                "Owner Harper • Live diagnostics",
                "Inserted",
                new Color(25, 34, 31),
                new Color(76, 118, 107),
                new Color(41, 66, 59),
                new Color(99, 152, 138),
                new Color(224, 246, 240),
                86f,
                252f,
                222f,
                164f),
            _ => new StackPanelCardSpec(
                StackPanelCardKind.Injected,
                $"Injected child {number}",
                "The new child inherits the current orientation and cross-axis alignment mode because those are panel-level layout decisions.",
                "Owner Taylor • Flow update",
                "Runtime",
                new Color(39, 28, 23),
                new Color(131, 96, 76),
                new Color(77, 53, 41),
                new Color(170, 123, 96),
                new Color(250, 236, 226),
                86f,
                252f,
                222f,
                164f)
        };
    }

    private enum CrossAxisAlignmentMode
    {
        Stretch,
        Start,
        Center,
        End
    }

    private enum StackPanelCardKind
    {
        Hero,
        Secondary,
        Standard,
        Injected
    }

    private sealed record StackPanelCardSpec(
        StackPanelCardKind Kind,
        string Title,
        string Summary,
        string Meta,
        string BadgeLabel,
        Color Surface,
        Color Border,
        Color BadgeBackground,
        Color BadgeBorder,
        Color BadgeForeground,
        float VerticalHeight,
        float VerticalWidth,
        float HorizontalWidth,
        float HorizontalHeight)
    {
        public string ShortLabel => Kind switch
        {
            StackPanelCardKind.Hero => "Hero",
            StackPanelCardKind.Secondary => "Secondary",
            StackPanelCardKind.Injected => "Injected",
            _ => Title
        };
    }
}