using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ExpanderView : UserControl
{
    private static readonly Color DefaultForegroundColor = Color.White;
    private static readonly Color DefaultBackgroundColor = new(20, 20, 20);
    private static readonly Color DefaultHeaderBackgroundColor = new(38, 38, 38);
    private static readonly Color DefaultBorderColor = new(94, 94, 94);
    private static readonly Thickness DefaultContentPadding = new(8f);
    private static readonly Thickness DefaultHeaderPadding = new(10f, 6f, 10f, 6f);
    private static readonly Color AccentForegroundColor = new(238, 249, 252);
    private static readonly Color AccentBackgroundColor = new(20, 33, 38);
    private static readonly Color AccentHeaderBackgroundColor = new(30, 72, 84);
    private static readonly Color AccentBorderColor = new(101, 184, 202);

    private Button? _playgroundExpandButton;
    private Button? _playgroundCollapseButton;
    private Button? _playgroundToggleButton;
    private Button? _playgroundResetButton;
    private ComboBox? _playgroundDirectionCombo;
    private CheckBox? _playgroundAccentCheckBox;
    private CheckBox? _playgroundRichHeaderCheckBox;
    private Expander? _playgroundExpander;
    private Expander? _styledExpander;
    private Expander? _accordionPlanningExpander;
    private Expander? _accordionTokensExpander;
    private Expander? _accordionPublishingExpander;
    private TextBlock? _playgroundStateText;
    private TextBlock? _playgroundEventCountsText;
    private TextBlock? _accordionStateText;
    private TextBlock? _accordionEventCountsText;
    private TextBlock? _catalogSummaryText;
    private TextBlock? _directionSummaryText;
    private TextBlock? _stateSummaryText;
    private int _playgroundExpandedCount;
    private int _playgroundCollapsedCount;
    private int _accordionTransitionCount;
    private bool _suppressAccordionTracking;

    public ExpanderView()
    {
        InitializeComponent();

        EnsureReferences();
        PopulateDirectionChoices();
        ApplyStyledHeader();
        WireEvents();
        ResetDemoState();
    }

    private void EnsureReferences()
    {
        _playgroundExpandButton ??= this.FindName("PlaygroundExpandButton") as Button;
        _playgroundCollapseButton ??= this.FindName("PlaygroundCollapseButton") as Button;
        _playgroundToggleButton ??= this.FindName("PlaygroundToggleButton") as Button;
        _playgroundResetButton ??= this.FindName("PlaygroundResetButton") as Button;
        _playgroundDirectionCombo ??= this.FindName("PlaygroundDirectionCombo") as ComboBox;
        _playgroundAccentCheckBox ??= this.FindName("PlaygroundAccentCheckBox") as CheckBox;
        _playgroundRichHeaderCheckBox ??= this.FindName("PlaygroundRichHeaderCheckBox") as CheckBox;
        _playgroundExpander ??= this.FindName("PlaygroundExpander") as Expander;
        _styledExpander ??= this.FindName("StyledExpander") as Expander;
        _accordionPlanningExpander ??= this.FindName("AccordionPlanningExpander") as Expander;
        _accordionTokensExpander ??= this.FindName("AccordionTokensExpander") as Expander;
        _accordionPublishingExpander ??= this.FindName("AccordionPublishingExpander") as Expander;
        _playgroundStateText ??= this.FindName("PlaygroundStateText") as TextBlock;
        _playgroundEventCountsText ??= this.FindName("PlaygroundEventCountsText") as TextBlock;
        _accordionStateText ??= this.FindName("AccordionStateText") as TextBlock;
        _accordionEventCountsText ??= this.FindName("AccordionEventCountsText") as TextBlock;
        _catalogSummaryText ??= this.FindName("CatalogSummaryText") as TextBlock;
        _directionSummaryText ??= this.FindName("DirectionSummaryText") as TextBlock;
        _stateSummaryText ??= this.FindName("StateSummaryText") as TextBlock;
    }

    private void PopulateDirectionChoices()
    {
        if (_playgroundDirectionCombo == null || _playgroundDirectionCombo.Items.Count > 0)
        {
            return;
        }

        _playgroundDirectionCombo.Items.Add(nameof(ExpandDirection.Down));
        _playgroundDirectionCombo.Items.Add(nameof(ExpandDirection.Up));
        _playgroundDirectionCombo.Items.Add(nameof(ExpandDirection.Left));
        _playgroundDirectionCombo.Items.Add(nameof(ExpandDirection.Right));
    }

    private void ApplyStyledHeader()
    {
        if (_styledExpander == null)
        {
            return;
        }

        _styledExpander.Header = BuildHeaderVisual(
            "Delivery lane",
            "Composed header content works without changing the Expander template.",
            new Color(223, 247, 255),
            new Color(174, 221, 235));
    }

    private void WireEvents()
    {
        if (_playgroundExpandButton != null)
        {
            _playgroundExpandButton.Click += OnPlaygroundExpandClicked;
        }

        if (_playgroundCollapseButton != null)
        {
            _playgroundCollapseButton.Click += OnPlaygroundCollapseClicked;
        }

        if (_playgroundToggleButton != null)
        {
            _playgroundToggleButton.Click += OnPlaygroundToggleClicked;
        }

        if (_playgroundResetButton != null)
        {
            _playgroundResetButton.Click += OnPlaygroundResetClicked;
        }

        if (_playgroundDirectionCombo != null)
        {
            _playgroundDirectionCombo.SelectionChanged += OnPlaygroundDirectionChanged;
        }

        if (_playgroundAccentCheckBox != null)
        {
            _playgroundAccentCheckBox.Checked += OnPlaygroundOptionChanged;
            _playgroundAccentCheckBox.Unchecked += OnPlaygroundOptionChanged;
        }

        if (_playgroundRichHeaderCheckBox != null)
        {
            _playgroundRichHeaderCheckBox.Checked += OnPlaygroundOptionChanged;
            _playgroundRichHeaderCheckBox.Unchecked += OnPlaygroundOptionChanged;
        }

        if (_playgroundExpander != null)
        {
            _playgroundExpander.Expanded += OnPlaygroundExpanded;
            _playgroundExpander.Collapsed += OnPlaygroundCollapsed;
        }

        WireAccordionExpander(_accordionPlanningExpander);
        WireAccordionExpander(_accordionTokensExpander);
        WireAccordionExpander(_accordionPublishingExpander);
    }

    private void WireAccordionExpander(Expander? expander)
    {
        if (expander == null)
        {
            return;
        }

        expander.Expanded += OnAccordionExpanded;
        expander.Collapsed += OnAccordionCollapsed;
    }

    private void ResetDemoState()
    {
        _suppressAccordionTracking = true;

        if (_playgroundDirectionCombo != null)
        {
            _playgroundDirectionCombo.SelectedItem = nameof(ExpandDirection.Down);
        }

        if (_playgroundAccentCheckBox != null)
        {
            _playgroundAccentCheckBox.IsChecked = true;
        }

        if (_playgroundRichHeaderCheckBox != null)
        {
            _playgroundRichHeaderCheckBox.IsChecked = true;
        }

        if (_playgroundExpander != null)
        {
            _playgroundExpander.IsExpanded = true;
        }

        if (_accordionPlanningExpander != null)
        {
            _accordionPlanningExpander.IsExpanded = true;
        }

        if (_accordionTokensExpander != null)
        {
            _accordionTokensExpander.IsExpanded = false;
        }

        if (_accordionPublishingExpander != null)
        {
            _accordionPublishingExpander.IsExpanded = false;
        }

        _playgroundExpandedCount = 0;
        _playgroundCollapsedCount = 0;
        _accordionTransitionCount = 0;
        _suppressAccordionTracking = false;

        ApplyPlaygroundOptions();
        UpdateAccordionSummary();
        UpdateCatalogSummary();
    }

    private void OnPlaygroundExpandClicked(object? sender, RoutedSimpleEventArgs args)
    {
        if (_playgroundExpander != null)
        {
            _playgroundExpander.IsExpanded = true;
        }
    }

    private void OnPlaygroundCollapseClicked(object? sender, RoutedSimpleEventArgs args)
    {
        if (_playgroundExpander != null)
        {
            _playgroundExpander.IsExpanded = false;
        }
    }

    private void OnPlaygroundToggleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        if (_playgroundExpander != null)
        {
            _playgroundExpander.IsExpanded = !_playgroundExpander.IsExpanded;
        }
    }

    private void OnPlaygroundResetClicked(object? sender, RoutedSimpleEventArgs args)
    {
        ResetDemoState();
    }

    private void OnPlaygroundDirectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyPlaygroundOptions();
    }

    private void OnPlaygroundOptionChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ApplyPlaygroundOptions();
    }

    private void OnPlaygroundExpanded(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _playgroundExpandedCount++;
        UpdatePlaygroundSummary();
        UpdateCatalogSummary();
    }

    private void OnPlaygroundCollapsed(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _playgroundCollapsedCount++;
        UpdatePlaygroundSummary();
        UpdateCatalogSummary();
    }

    private void OnAccordionExpanded(object? sender, RoutedSimpleEventArgs args)
    {
        _ = args;
        if (sender is not Expander expanded)
        {
            return;
        }

        if (_suppressAccordionTracking)
        {
            UpdateAccordionSummary();
            UpdateCatalogSummary();
            return;
        }

        _suppressAccordionTracking = true;
        CollapseAccordionSibling(_accordionPlanningExpander, expanded);
        CollapseAccordionSibling(_accordionTokensExpander, expanded);
        CollapseAccordionSibling(_accordionPublishingExpander, expanded);
        _suppressAccordionTracking = false;

        _accordionTransitionCount++;
        UpdateAccordionSummary();
        UpdateCatalogSummary();
    }

    private void OnAccordionCollapsed(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressAccordionTracking)
        {
            UpdateAccordionSummary();
            UpdateCatalogSummary();
            return;
        }

        UpdateAccordionSummary();
        UpdateCatalogSummary();
    }

    private void CollapseAccordionSibling(Expander? candidate, Expander expanded)
    {
        if (candidate != null && !ReferenceEquals(candidate, expanded))
        {
            candidate.IsExpanded = false;
        }
    }

    private void ApplyPlaygroundOptions()
    {
        if (_playgroundExpander == null)
        {
            return;
        }

        var accentPalette = _playgroundAccentCheckBox?.IsChecked == true;
        var richHeader = _playgroundRichHeaderCheckBox?.IsChecked == true;

        _playgroundExpander.ExpandDirection = GetSelectedDirection();
        _playgroundExpander.Foreground = accentPalette ? AccentForegroundColor : DefaultForegroundColor;
        _playgroundExpander.Background = accentPalette ? AccentBackgroundColor : DefaultBackgroundColor;
        _playgroundExpander.HeaderBackground = accentPalette ? AccentHeaderBackgroundColor : DefaultHeaderBackgroundColor;
        _playgroundExpander.BorderBrush = accentPalette ? AccentBorderColor : DefaultBorderColor;
        _playgroundExpander.BorderThickness = 1f;
        _playgroundExpander.Padding = DefaultContentPadding;
        _playgroundExpander.HeaderPadding = accentPalette ? new Thickness(14f, 10f, 14f, 10f) : DefaultHeaderPadding;
        _playgroundExpander.Header = richHeader
            ? BuildHeaderVisual(
                "Release checklist",
                accentPalette
                    ? "Composed header content plus accent colors on the same Expander instance."
                    : "Composed header content using the default Expander palette.",
                accentPalette ? new Color(237, 249, 252) : new Color(224, 224, 224),
                accentPalette ? new Color(188, 233, 241) : new Color(170, 170, 170))
            : "Release checklist";

        UpdatePlaygroundSummary();
        UpdateCatalogSummary();
    }

    private ExpandDirection GetSelectedDirection()
    {
        var selected = _playgroundDirectionCombo?.SelectedItem?.ToString();
        return selected switch
        {
            nameof(ExpandDirection.Up) => ExpandDirection.Up,
            nameof(ExpandDirection.Left) => ExpandDirection.Left,
            nameof(ExpandDirection.Right) => ExpandDirection.Right,
            _ => ExpandDirection.Down
        };
    }

    private UIElement BuildHeaderVisual(string title, string subtitle, Color titleColor, Color subtitleColor)
    {
        var stack = new StackPanel();

        stack.AddChild(new TextBlock
        {
            Text = title,
            Foreground = titleColor,
            FontSize = 15f,
            TextWrapping = TextWrapping.Wrap
        });

        stack.AddChild(new TextBlock
        {
            Text = subtitle,
            Foreground = subtitleColor,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0f, 3f, 0f, 0f)
        });

        return stack;
    }

    private void UpdatePlaygroundSummary()
    {
        if (_playgroundStateText != null)
        {
            var palette = _playgroundAccentCheckBox?.IsChecked == true ? "Accent palette" : "Default palette";
            var headerMode = _playgroundRichHeaderCheckBox?.IsChecked == true ? "Rich header" : "Text header";
            _playgroundStateText.Text = $"State: {(_playgroundExpander?.IsExpanded == true ? "Expanded" : "Collapsed")} | Direction: {GetSelectedDirection()} | {palette} | {headerMode}";
        }

        if (_playgroundEventCountsText != null)
        {
            _playgroundEventCountsText.Text = $"Routed events fired from this playground instance: Expanded {_playgroundExpandedCount}, Collapsed {_playgroundCollapsedCount}.";
        }
    }

    private void UpdateAccordionSummary()
    {
        var activeSection = GetActiveAccordionSection();

        if (_accordionStateText != null)
        {
            _accordionStateText.Text = $"Active accordion section: {activeSection}.";
        }

        if (_accordionEventCountsText != null)
        {
            _accordionEventCountsText.Text = $"Manual accordion transitions: {_accordionTransitionCount}. Expanding one section collapses its siblings in code-behind.";
        }
    }

    private string GetActiveAccordionSection()
    {
        if (_accordionPlanningExpander?.IsExpanded == true)
        {
            return "Planning review";
        }

        if (_accordionTokensExpander?.IsExpanded == true)
        {
            return "Design tokens";
        }

        if (_accordionPublishingExpander?.IsExpanded == true)
        {
            return "Publishing checklist";
        }

        return "None";
    }

    private void UpdateCatalogSummary()
    {
        if (_catalogSummaryText != null)
        {
            _catalogSummaryText.Text = $"Playground is {(_playgroundExpander?.IsExpanded == true ? "expanded" : "collapsed")} and currently opens {GetSelectedDirection().ToString().ToLowerInvariant()}. The same surface also switches between text and element-based headers.";
        }

        if (_directionSummaryText != null)
        {
            _directionSummaryText.Text = "Direction samples on this page intentionally cover Down, Up, Left, and Right because the rest of the catalog previously only exercised the default downward expansion path.";
        }

        if (_stateSummaryText != null)
        {
            _stateSummaryText.Text = $"State coverage includes live toggling, a collapsed-on-load sample, a disabled sample, a permanently rich header sample, and a manual accordion currently focused on {GetActiveAccordionSection()}.";
        }
    }
}




