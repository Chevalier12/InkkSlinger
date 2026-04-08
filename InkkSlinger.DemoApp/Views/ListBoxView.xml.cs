using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ListBoxView : UserControl
{
    private readonly ObservableCollection<CatalogListItem> _catalogItems = CreateCatalogItems();
    private readonly ObservableCollection<CatalogListItem> _templatedItems = CreateTemplatedItems();
    private readonly ObservableCollection<BacklogListItem> _virtualizedItems = CreateVirtualizedItems();

    private ListBox? _basicListBox;
    private ListBox? _catalogListBox;
    private ListBox? _extendedListBox;
    private ListBox? _multipleListBox;
    private ListBox? _templatedListBox;
    private ListBox? _virtualizedListBox;
    private TextBlock? _basicSelectionSummaryText;
    private TextBlock? _catalogSelectionIndexText;
    private TextBlock? _catalogSelectionItemText;
    private TextBlock? _catalogSelectionValueText;
    private TextBlock? _extendedSelectionIndicesText;
    private TextBlock? _extendedSelectionItemsText;
    private TextBlock? _extendedSelectionCountText;
    private TextBlock? _multipleSelectionSummaryText;
    private TextBlock? _templatedSelectionSummaryText;
    private TextBlock? _virtualizedSummaryText;
    private Button? _jumpToStartButton;
    private Button? _jumpToMiddleButton;
    private Button? _jumpToEndButton;

    private int _basicSelectionChanges;
    private int _catalogSelectionChanges;
    private int _extendedSelectionChanges;
    private int _multipleSelectionChanges;
    private int _templatedSelectionChanges;
    private int _virtualizedSelectionChanges;
    private string _virtualizedActionText = "Use the jump buttons to call ScrollIntoView on a larger items source.";

    public ListBoxView()
    {
        InitializeComponent();

        EnsureReferences();
        PopulateInlineSamples();
        BindObjectBackedSamples();
        ApplyListStyling();
        SetInitialState();
        WireEvents();
        UpdateAllReadouts();
    }

    private void EnsureReferences()
    {
        _basicListBox ??= this.FindName("BasicListBox") as ListBox;
        _catalogListBox ??= this.FindName("CatalogListBox") as ListBox;
        _extendedListBox ??= this.FindName("ExtendedListBox") as ListBox;
        _multipleListBox ??= this.FindName("MultipleListBox") as ListBox;
        _templatedListBox ??= this.FindName("TemplatedListBox") as ListBox;
        _virtualizedListBox ??= this.FindName("VirtualizedListBox") as ListBox;
        _basicSelectionSummaryText ??= this.FindName("BasicSelectionSummaryText") as TextBlock;
        _catalogSelectionIndexText ??= this.FindName("CatalogSelectionIndexText") as TextBlock;
        _catalogSelectionItemText ??= this.FindName("CatalogSelectionItemText") as TextBlock;
        _catalogSelectionValueText ??= this.FindName("CatalogSelectionValueText") as TextBlock;
        _extendedSelectionIndicesText ??= this.FindName("ExtendedSelectionIndicesText") as TextBlock;
        _extendedSelectionItemsText ??= this.FindName("ExtendedSelectionItemsText") as TextBlock;
        _extendedSelectionCountText ??= this.FindName("ExtendedSelectionCountText") as TextBlock;
        _multipleSelectionSummaryText ??= this.FindName("MultipleSelectionSummaryText") as TextBlock;
        _templatedSelectionSummaryText ??= this.FindName("TemplatedSelectionSummaryText") as TextBlock;
        _virtualizedSummaryText ??= this.FindName("VirtualizedSummaryText") as TextBlock;
        _jumpToStartButton ??= this.FindName("JumpToStartButton") as Button;
        _jumpToMiddleButton ??= this.FindName("JumpToMiddleButton") as Button;
        _jumpToEndButton ??= this.FindName("JumpToEndButton") as Button;
    }

    private void PopulateInlineSamples()
    {
        if (_basicListBox is { Items.Count: 0 } basicListBox)
        {
            basicListBox.Items.Add("Workspace overview");
            basicListBox.Items.Add("Pointer diagnostics");
            basicListBox.Items.Add("Keyboard navigation");
            basicListBox.Items.Add("Automation coverage");
            basicListBox.Items.Add("Visual polish pass");
        }

        if (_extendedListBox is { Items.Count: 0 } extendedListBox)
        {
            extendedListBox.Items.Add("Validate range selection");
            extendedListBox.Items.Add("Verify Ctrl toggle");
            extendedListBox.Items.Add("Confirm keyboard movement");
            extendedListBox.Items.Add("Check ScrollIntoView sync");
            extendedListBox.Items.Add("Review automation state");
            extendedListBox.Items.Add("Inspect focus restoration");
            extendedListBox.Items.Add("Stress large lists");
        }

        if (_multipleListBox is { Items.Count: 0 } multipleListBox)
        {
            multipleListBox.Items.Add("Layout");
            multipleListBox.Items.Add("Input");
            multipleListBox.Items.Add("Styling");
            multipleListBox.Items.Add("Binding");
            multipleListBox.Items.Add("Automation");
            multipleListBox.Items.Add("Diagnostics");
        }
    }

    private void BindObjectBackedSamples()
    {
        if (_catalogListBox != null)
        {
            _catalogListBox.ItemsSource = _catalogItems;
        }

        if (_templatedListBox != null)
        {
            _templatedListBox.ItemsSource = _templatedItems;
        }

        if (_virtualizedListBox != null)
        {
            _virtualizedListBox.ItemsSource = _virtualizedItems;
        }
    }

    private void ApplyListStyling()
    {
        if (_catalogListBox != null)
        {
            _catalogListBox.Background = new Color(17, 23, 30);
            _catalogListBox.BorderBrush = new Color(70, 96, 118);
        }

        if (_templatedListBox != null)
        {
            _templatedListBox.Background = new Color(14, 20, 26);
            _templatedListBox.BorderBrush = new Color(64, 92, 112);
        }

        if (_virtualizedListBox != null)
        {
            _virtualizedListBox.Background = new Color(18, 22, 18);
            _virtualizedListBox.BorderBrush = new Color(93, 115, 82);
        }
    }

    private void SetInitialState()
    {
        if (_basicListBox != null)
        {
            _basicListBox.SelectedIndex = 1;
        }

        if (_catalogListBox != null)
        {
            _catalogListBox.SelectedIndex = 2;
        }

        if (_extendedListBox != null)
        {
            _extendedListBox.SelectedIndex = 1;
        }

        if (_multipleListBox != null)
        {
            _multipleListBox.SelectedIndex = 0;
        }

        if (_templatedListBox != null)
        {
            _templatedListBox.SelectedIndex = 1;
        }

        if (_virtualizedListBox != null && _virtualizedItems.Count > 44)
        {
            var target = _virtualizedItems[44];
            _virtualizedListBox.SelectedItem = target;
            _virtualizedListBox.ScrollIntoView(target);
            _virtualizedActionText = "Initial state scrolls deeper into the backlog so a larger list is visible immediately.";
        }
    }

    private void WireEvents()
    {
        if (_basicListBox != null)
        {
            _basicListBox.SelectionChanged += OnBasicSelectionChanged;
        }

        if (_catalogListBox != null)
        {
            _catalogListBox.SelectionChanged += OnCatalogSelectionChanged;
        }

        if (_extendedListBox != null)
        {
            _extendedListBox.SelectionChanged += OnExtendedSelectionChanged;
        }

        if (_multipleListBox != null)
        {
            _multipleListBox.SelectionChanged += OnMultipleSelectionChanged;
        }

        if (_templatedListBox != null)
        {
            _templatedListBox.SelectionChanged += OnTemplatedSelectionChanged;
        }

        if (_virtualizedListBox != null)
        {
            _virtualizedListBox.SelectionChanged += OnVirtualizedSelectionChanged;
        }

        if (_jumpToStartButton != null)
        {
            _jumpToStartButton.Click += OnJumpToStartClicked;
        }

        if (_jumpToMiddleButton != null)
        {
            _jumpToMiddleButton.Click += OnJumpToMiddleClicked;
        }

        if (_jumpToEndButton != null)
        {
            _jumpToEndButton.Click += OnJumpToEndClicked;
        }
    }

    private void OnBasicSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _basicSelectionChanges++;
        UpdateBasicSelectionSummary();
    }

    private void OnCatalogSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _catalogSelectionChanges++;
        UpdateCatalogSelectionSummary();
    }

    private void OnExtendedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _extendedSelectionChanges++;
        UpdateExtendedSelectionSummary();
    }

    private void OnMultipleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _multipleSelectionChanges++;
        UpdateMultipleSelectionSummary();
    }

    private void OnTemplatedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _templatedSelectionChanges++;
        UpdateTemplatedSelectionSummary();
    }

    private void OnVirtualizedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _virtualizedSelectionChanges++;
        UpdateVirtualizedSummary();
    }

    private void OnJumpToStartClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollVirtualizedToIndex(0, "Jumped to the first projected item via ScrollIntoView.");
    }

    private void OnJumpToMiddleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollVirtualizedToIndex(_virtualizedItems.Count / 2, "Jumped to the middle of the backlog via ScrollIntoView.");
    }

    private void OnJumpToEndClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ScrollVirtualizedToIndex(_virtualizedItems.Count - 1, "Jumped to the final backlog item via ScrollIntoView.");
    }

    private void ScrollVirtualizedToIndex(int index, string actionText)
    {
        if (_virtualizedListBox == null || _virtualizedItems.Count == 0)
        {
            return;
        }

        var clampedIndex = Math.Clamp(index, 0, _virtualizedItems.Count - 1);
        var target = _virtualizedItems[clampedIndex];
        _virtualizedActionText = actionText;
        _virtualizedListBox.SelectedItem = target;
        _virtualizedListBox.ScrollIntoView(target);
        UpdateVirtualizedSummary();
    }

    private void UpdateAllReadouts()
    {
        UpdateBasicSelectionSummary();
        UpdateCatalogSelectionSummary();
        UpdateExtendedSelectionSummary();
        UpdateMultipleSelectionSummary();
        UpdateTemplatedSelectionSummary();
        UpdateVirtualizedSummary();
    }

    private void UpdateBasicSelectionSummary()
    {
        if (_basicSelectionSummaryText == null || _basicListBox == null)
        {
            return;
        }

        var selectedItem = _basicListBox.SelectedItem?.ToString() ?? "-";
        _basicSelectionSummaryText.Text =
            $"SelectedIndex: {_basicListBox.SelectedIndex} | SelectedItem: {selectedItem} | SelectionChanged fired: {_basicSelectionChanges}";
    }

    private void UpdateCatalogSelectionSummary()
    {
        var selectedItem = _catalogListBox?.SelectedItem as CatalogListItem;

        if (_catalogSelectionIndexText != null && _catalogListBox != null)
        {
            _catalogSelectionIndexText.Text =
                $"SelectedIndex: {_catalogListBox.SelectedIndex} | SelectionChanged fired: {_catalogSelectionChanges}";
        }

        if (_catalogSelectionItemText != null)
        {
            _catalogSelectionItemText.Text = selectedItem == null
                ? "Selected item: -"
                : $"Selected item: {selectedItem.Title} / {selectedItem.Category} / owner {selectedItem.Owner}";
        }

        if (_catalogSelectionValueText != null)
        {
            var selectedValue = _catalogListBox?.SelectedValue?.ToString() ?? "-";
            _catalogSelectionValueText.Text = selectedItem == null
                ? $"SelectedValue: {selectedValue}"
                : $"SelectedValue: {selectedValue} | Status: {selectedItem.State} | {selectedItem.Summary}";
        }
    }

    private void UpdateExtendedSelectionSummary()
    {
        if (_extendedListBox == null)
        {
            return;
        }

        if (_extendedSelectionIndicesText != null)
        {
            _extendedSelectionIndicesText.Text =
                $"SelectedIndices: {FormatIndices(_extendedListBox.SelectedIndices)}";
        }

        if (_extendedSelectionItemsText != null)
        {
            _extendedSelectionItemsText.Text =
                $"SelectedItems: {FormatItems(_extendedListBox.SelectedItems, 5)}";
        }

        if (_extendedSelectionCountText != null)
        {
            _extendedSelectionCountText.Text =
                $"SelectionChanged fired: {_extendedSelectionChanges}. Try Shift+Click, Ctrl+Click, or Ctrl+A.";
        }
    }

    private void UpdateMultipleSelectionSummary()
    {
        if (_multipleSelectionSummaryText == null || _multipleListBox == null)
        {
            return;
        }

        _multipleSelectionSummaryText.Text =
            $"SelectedItems: {FormatItems(_multipleListBox.SelectedItems, 5)} | Plain click toggles items in SelectionMode.Multiple | SelectionChanged fired: {_multipleSelectionChanges}";
    }

    private void UpdateTemplatedSelectionSummary()
    {
        if (_templatedSelectionSummaryText == null || _templatedListBox == null)
        {
            return;
        }

        var selectedItem = _templatedListBox.SelectedItem as CatalogListItem;
        _templatedSelectionSummaryText.Text = selectedItem == null
            ? $"Selected template row: - | SelectionChanged fired: {_templatedSelectionChanges}"
            : $"Selected template row: {selectedItem.Title} / {selectedItem.State} / owner {selectedItem.Owner} | SelectedValue: {_templatedListBox.SelectedValue} | SelectionChanged fired: {_templatedSelectionChanges}";
    }

    private void UpdateVirtualizedSummary()
    {
        if (_virtualizedSummaryText == null || _virtualizedListBox == null)
        {
            return;
        }

        var selectedItem = _virtualizedListBox.SelectedItem as BacklogListItem;
        _virtualizedSummaryText.Text = selectedItem == null
            ? _virtualizedActionText
            : $"{_virtualizedActionText} Current selection: {selectedItem.Label} / {selectedItem.Track} | SelectionChanged fired: {_virtualizedSelectionChanges}";
    }

    private static string FormatIndices(IReadOnlyList<int> indices)
    {
        if (indices.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", indices);
    }

    private static string FormatItems(IReadOnlyList<object> items, int maxItems)
    {
        if (items.Count == 0)
        {
            return "none";
        }

        var shown = new List<string>(Math.Min(items.Count, maxItems));
        for (var i = 0; i < items.Count && i < maxItems; i++)
        {
            shown.Add(items[i]?.ToString() ?? "null");
        }

        if (items.Count > maxItems)
        {
            shown.Add($"+{items.Count - maxItems} more");
        }

        return string.Join(", ", shown);
    }

    private static ObservableCollection<CatalogListItem> CreateCatalogItems()
    {
        return
        [
            new CatalogListItem { Id = 101, Title = "Controls Catalog shell", Category = "Navigation", Owner = "Framework", State = "Ready", Summary = "Hosts the full demo surface for selector controls." },
            new CatalogListItem { Id = 104, Title = "Selection telemetry", Category = "Diagnostics", Owner = "Input", State = "Review", Summary = "Tracks AddedItems and RemovedItems during interaction." },
            new CatalogListItem { Id = 108, Title = "Keyboard focus path", Category = "Accessibility", Owner = "Focus", State = "Active", Summary = "Keeps arrow-key navigation and selection aligned." },
            new CatalogListItem { Id = 112, Title = "Automation bridge", Category = "Automation", Owner = "InkkOops", State = "Ready", Summary = "Exposes list state to UI automation and regression scripts." },
            new CatalogListItem { Id = 118, Title = "Virtualized backlog", Category = "Performance", Owner = "Layout", State = "Active", Summary = "Exercises larger item counts without leaving the catalog." },
            new CatalogListItem { Id = 124, Title = "Templated work queue", Category = "ItemsControl", Owner = "Styling", State = "Planned", Summary = "Validates richer item visuals inside a selector." },
        ];
    }

    private static ObservableCollection<CatalogListItem> CreateTemplatedItems()
    {
        return
        [
            new CatalogListItem { Id = 201, Title = "Navigation parity review", Category = "Selection", Owner = "Avery", State = "In Progress", Summary = "Verify pointer and keyboard selection stay in sync while the list scrolls." },
            new CatalogListItem { Id = 204, Title = "Automation selectors", Category = "Testing", Owner = "Morgan", State = "Queued", Summary = "Keep semantic targets stable so catalog repros survive layout changes." },
            new CatalogListItem { Id = 209, Title = "Template density pass", Category = "Styling", Owner = "Jordan", State = "Review", Summary = "Ensure richer item visuals still feel selectable and keep hover contrast visible." },
            new CatalogListItem { Id = 214, Title = "Large list smoke test", Category = "Performance", Owner = "Riley", State = "Ready", Summary = "Exercise virtualization and ScrollIntoView with a production-sized item source." },
        ];
    }

    private static ObservableCollection<BacklogListItem> CreateVirtualizedItems()
    {
        var items = new ObservableCollection<BacklogListItem>();
        for (var i = 1; i <= 120; i++)
        {
            items.Add(new BacklogListItem
            {
                Id = i,
                Label = $"Backlog item {i:000}",
                Track = (i % 3) switch
                {
                    0 => "Layout",
                    1 => "Input",
                    _ => "Styling"
                }
            });
        }

        return items;
    }

    private sealed class CatalogListItem
    {
        public required int Id { get; init; }

        public required string Title { get; init; }

        public required string Category { get; init; }

        public required string Owner { get; init; }

        public required string State { get; init; }

        public required string Summary { get; init; }

        public override string ToString()
        {
            return Title;
        }
    }

    private sealed class BacklogListItem
    {
        public required int Id { get; init; }

        public required string Label { get; init; }

        public required string Track { get; init; }

        public override string ToString()
        {
            return Label;
        }
    }
}




