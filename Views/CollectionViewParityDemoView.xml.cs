using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class CollectionViewParityDemoView : UserControl
{
    private static readonly bool CpuDiagnosticsEnabled =
        string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_COLLECTIONVIEW_CPU_LOGS"), "1", StringComparison.Ordinal);
    private static readonly TimeSpan ResetLogThrottle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CurrentChangedLogThrottle = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan StatusRecountThrottle = TimeSpan.FromMilliseconds(80);

    private readonly CollectionViewParityDemoViewModel _viewModel = new();
    private readonly CollectionViewSource _viewSource = new();
    private int _nextId = 1;
    private string _lastSortProperty = string.Empty;
    private ListSortDirection _lastSortDirection = ListSortDirection.Descending;
    private SpriteFont? _font;
    private DateTime _lastResetLogUtc = DateTime.MinValue;
    private DateTime _lastCurrentChangedLogUtc = DateTime.MinValue;
    private DateTime _lastStatusRecountUtc = DateTime.MinValue;
    private int _lastComputedViewCount;

    public CollectionViewParityDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "CollectionViewParityDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
        DataContext = _viewModel;

        SeedSourceItems();
        _viewSource.Source = _viewModel.SourceItems;
        _viewSource.View!.CurrentChanged += OnViewCurrentChanged;
        _viewSource.View.CollectionChanged += OnViewCollectionChanged;

        DemoListBox!.ItemsSource = _viewSource.View;
        DemoListView!.ItemsSource = _viewSource.View;
        DemoDataGrid!.ItemsSource = _viewSource.View;
        GroupPreview!.ItemsSource = _viewSource.View;
        DemoDataGrid.Sorting += (_, args) =>
        {
            AppendLog($"DataGrid sort clicked: {args.Column.BindingPath} => {args.Column.SortDirection}");
            UpdateStatusAndCurrent();
        };

        UpdateStatusAndCurrent();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        _font = font;
        ApplyFontRecursive(this, font);
    }

    private void OnAddItemClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        var name = string.IsNullOrWhiteSpace(NameInput?.Text) ? $"Item {_nextId}" : NameInput!.Text;
        var category = string.IsNullOrWhiteSpace(CategoryInput?.Text) ? "General" : CategoryInput!.Text;
        var priority = int.TryParse(PriorityInput?.Text, out var parsedPriority) ? Math.Clamp(parsedPriority, 1, 5) : 1;
        var item = new CollectionDemoItem(_nextId++, name, category, priority);
        _viewModel.SourceItems.Add(item);
        _viewSource.Refresh();
        AppendLog($"Add: {item}");
        UpdateStatusAndCurrent();
    }

    private void OnRemoveCurrentClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_viewSource.View?.CurrentItem is not CollectionDemoItem current)
        {
            AppendLog("Remove current skipped: no current item.");
            return;
        }

        _viewModel.SourceItems.Remove(current);
        _viewSource.Refresh();
        AppendLog($"Removed current: {current}");
        UpdateStatusAndCurrent();
    }

    private void OnFilterTextChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        var filterText = FilterInput?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(filterText))
        {
            _viewSource.Filter = null;
            AppendLog("Filter cleared.");
        }
        else
        {
            _viewSource.Filter = item =>
            {
                if (item is not CollectionDemoItem entry)
                {
                    return false;
                }

                return entry.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                       entry.Category.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            };
            AppendLog($"Filter set: '{filterText}'");
        }

        _viewSource.Refresh();
        UpdateStatusAndCurrent();
    }

    private void OnSortNameClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ToggleSort(nameof(CollectionDemoItem.Name));
    }

    private void OnSortPriorityClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        ToggleSort(nameof(CollectionDemoItem.Priority));
    }

    private void OnGroupNoneClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.GroupDescriptions.Clear();
        _viewSource.Refresh();
        AppendLog("Group: none");
        UpdateStatusAndCurrent();
    }

    private void OnGroupCategoryClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.GroupDescriptions.Clear();
        _viewSource.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = nameof(CollectionDemoItem.Category) });
        _viewSource.Refresh();
        AppendLog("Group: Category");
        UpdateStatusAndCurrent();
    }

    private void OnGroupPriorityClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.GroupDescriptions.Clear();
        _viewSource.GroupDescriptions.Add(new PropertyGroupDescription { PropertyName = nameof(CollectionDemoItem.Priority) });
        _viewSource.Refresh();
        AppendLog("Group: Priority");
        UpdateStatusAndCurrent();
    }

    private void OnCurrentFirstClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.View?.MoveCurrentToFirst();
        UpdateStatusAndCurrent();
    }

    private void OnCurrentPrevClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.View?.MoveCurrentToPrevious();
        UpdateStatusAndCurrent();
    }

    private void OnCurrentNextClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.View?.MoveCurrentToNext();
        UpdateStatusAndCurrent();
    }

    private void OnCurrentLastClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _viewSource.View?.MoveCurrentToLast();
        UpdateStatusAndCurrent();
    }

    private void OnClearLogClick(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        LogList?.Items.Clear();
        UpdateStatusAndCurrent();
    }

    private void OnViewCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (CpuDiagnosticsEnabled)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastResetLogUtc >= ResetLogThrottle)
        {
            _lastResetLogUtc = now;
            AppendLog("CollectionChanged: Reset");
        }
    }

    private void OnViewCurrentChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (!CpuDiagnosticsEnabled)
        {
            var now = DateTime.UtcNow;
            if (now - _lastCurrentChangedLogUtc >= CurrentChangedLogThrottle)
            {
                _lastCurrentChangedLogUtc = now;
                AppendLog("CurrentChanged");
            }
        }

        UpdateStatusAndCurrent();
    }

    private void ToggleSort(string propertyName)
    {
        var view = _viewSource.View;
        if (view == null)
        {
            return;
        }

        if (string.Equals(_lastSortProperty, propertyName, StringComparison.Ordinal))
        {
            _lastSortDirection = _lastSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _lastSortProperty = propertyName;
            _lastSortDirection = ListSortDirection.Ascending;
        }

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(propertyName, _lastSortDirection));
        view.Refresh();
        AppendLog($"Sort: {propertyName} {_lastSortDirection}");
        UpdateStatusAndCurrent();
    }

    private void UpdateStatusAndCurrent()
    {
        var view = _viewSource.View;
        var now = DateTime.UtcNow;
        if (view != null && (now - _lastStatusRecountUtc >= StatusRecountThrottle || _lastStatusRecountUtc == DateTime.MinValue))
        {
            var count = 0;
            foreach (var _ in view)
            {
                count++;
            }

            _lastComputedViewCount = count;
            _lastStatusRecountUtc = now;
        }

        if (view?.CurrentItem is CollectionDemoItem item)
        {
            _viewModel.CurrentItemText =
                $"Current: #{item.Id} {item.Name} [{item.Category}] P{item.Priority} @ {view.CurrentPosition}";
        }
        else
        {
            _viewModel.CurrentItemText = "Current: none";
        }

        _viewModel.Status =
            $"Items={_lastComputedViewCount} | Groups={view?.Groups.Count ?? 0} | Sorts={view?.SortDescriptions.Count ?? 0} | Filter={(view?.Filter != null ? "On" : "Off")}";
    }

    private void SeedSourceItems()
    {
        AddSeed("Brush Stroke", "Art", 2);
        AddSeed("Layer Panel", "UI", 1);
        AddSeed("Blend Mode", "Art", 3);
        AddSeed("Export PNG", "File", 2);
        AddSeed("Brush Dynamics", "Art", 4);
        AddSeed("Autosave", "File", 1);
        AddSeed("Selection Tool", "UI", 2);
        AddSeed("Palette Sync", "UI", 5);
        AddSeed("History Stack", "UI", 3);
    }

    private void AddSeed(string name, string category, int priority)
    {
        _viewModel.SourceItems.Add(new CollectionDemoItem(_nextId++, name, category, priority));
    }

    private void AppendLog(string message)
    {
        if (LogList == null)
        {
            return;
        }

        var line = new Label
        {
            Text = $"{DateTime.Now:HH:mm:ss} {message}"
        };
        if (_font != null)
        {
            line.Font = _font;
        }

        LogList.Items.Add(line);
        while (LogList.Items.Count > 120)
        {
            LogList.Items.RemoveAt(0);
        }
    }

    private static void ApplyFontRecursive(UIElement? element, SpriteFont font)
    {
        if (element == null)
        {
            return;
        }

        if (element is Label label)
        {
            label.Font = font;
        }

        if (element is TextBlock textBlock)
        {
            textBlock.Font = font;
        }

        if (element is Button button)
        {
            button.Font = font;
        }

        if (element is TextBox textBox)
        {
            textBox.Font = font;
        }

        if (element is DataGrid dataGrid)
        {
            dataGrid.Font = font;
        }

        foreach (var child in element.GetVisualChildren())
        {
            ApplyFontRecursive(child, font);
        }
    }
}
