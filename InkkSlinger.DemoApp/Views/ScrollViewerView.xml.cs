using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class ScrollViewerView : UserControl
{
    private readonly ScrollViewer _workbenchScrollViewer;
    private readonly ScrollViewer _canvasScrollViewer;
    private readonly ScrollViewer _virtualizedScrollViewer;
    private readonly StackPanel _workbenchDocumentHost;
    private readonly WrapPanel _workbenchRowsHost;
    private readonly VirtualizingStackPanel _virtualizedRowsHost;
    private readonly ComboBox _horizontalVisibilityComboBox;
    private readonly ComboBox _verticalVisibilityComboBox;
    private readonly Slider _scrollBarThicknessSlider;
    private readonly Slider _horizontalOffsetSlider;
    private readonly Slider _verticalOffsetSlider;
    private readonly CheckBox _transformContentScrollingCheckBox;
    private readonly TextBlock _workbenchOffsetText;
    private readonly TextBlock _workbenchExtentText;
    private readonly TextBlock _workbenchViewportText;
    private readonly TextBlock _workbenchMaxOffsetText;
    private readonly TextBlock _viewportEventCountText;
    private readonly TextBlock _scrollBarThicknessText;
    private readonly TextBlock _canvasMetricsText;
    private readonly TextBlock _virtualizedMetricsText;
    private readonly TextBlock _liveStatusText;

    private int _workbenchViewportChangedCount;
    private bool _suppressOffsetSliderChanges;
    private bool _suppressOptionChanges;

    public ScrollViewerView()
    {
        InitializeComponent();

        _workbenchScrollViewer = RequireElement<ScrollViewer>("WorkbenchScrollViewer");
        _canvasScrollViewer = RequireElement<ScrollViewer>("CanvasScrollViewer");
        _virtualizedScrollViewer = RequireElement<ScrollViewer>("VirtualizedScrollViewer");
        _workbenchDocumentHost = RequireElement<StackPanel>("WorkbenchDocumentHost");
        _workbenchRowsHost = RequireElement<WrapPanel>("WorkbenchRowsHost");
        _virtualizedRowsHost = RequireElement<VirtualizingStackPanel>("VirtualizedRowsHost");
        _horizontalVisibilityComboBox = RequireElement<ComboBox>("HorizontalVisibilityComboBox");
        _verticalVisibilityComboBox = RequireElement<ComboBox>("VerticalVisibilityComboBox");
        _scrollBarThicknessSlider = RequireElement<Slider>("ScrollBarThicknessSlider");
        _horizontalOffsetSlider = RequireElement<Slider>("HorizontalOffsetSlider");
        _verticalOffsetSlider = RequireElement<Slider>("VerticalOffsetSlider");
        _transformContentScrollingCheckBox = RequireElement<CheckBox>("TransformContentScrollingCheckBox");
        _workbenchOffsetText = RequireElement<TextBlock>("WorkbenchOffsetText");
        _workbenchExtentText = RequireElement<TextBlock>("WorkbenchExtentText");
        _workbenchViewportText = RequireElement<TextBlock>("WorkbenchViewportText");
        _workbenchMaxOffsetText = RequireElement<TextBlock>("WorkbenchMaxOffsetText");
        _viewportEventCountText = RequireElement<TextBlock>("ViewportEventCountText");
        _scrollBarThicknessText = RequireElement<TextBlock>("ScrollBarThicknessText");
        _canvasMetricsText = RequireElement<TextBlock>("CanvasMetricsText");
        _virtualizedMetricsText = RequireElement<TextBlock>("VirtualizedMetricsText");
        _liveStatusText = RequireElement<TextBlock>("LiveStatusText");

        PopulateVisibilityComboBoxes();
        PopulateWorkbenchRows();
        PopulateVirtualizedRows();
        ApplyWorkbenchOptions();
        UpdateAllReadouts();
    }

    private void PopulateVisibilityComboBoxes()
    {
        AddVisibilityItems(_horizontalVisibilityComboBox);
        AddVisibilityItems(_verticalVisibilityComboBox);

        _suppressOptionChanges = true;
        try
        {
            _horizontalVisibilityComboBox.SelectedItem = nameof(ScrollBarVisibility.Auto);
            _verticalVisibilityComboBox.SelectedItem = nameof(ScrollBarVisibility.Auto);
        }
        finally
        {
            _suppressOptionChanges = false;
        }
    }

    private static void AddVisibilityItems(ComboBox comboBox)
    {
        if (comboBox.Items.Count > 0)
        {
            return;
        }

        comboBox.Items.Add(nameof(ScrollBarVisibility.Auto));
        comboBox.Items.Add(nameof(ScrollBarVisibility.Visible));
        comboBox.Items.Add(nameof(ScrollBarVisibility.Hidden));
        comboBox.Items.Add(nameof(ScrollBarVisibility.Disabled));
    }

    private void PopulateWorkbenchRows()
    {
        if (_workbenchRowsHost.GetVisualChildren().Any())
        {
            return;
        }

        for (var i = 1; i <= 36; i++)
        {
            var row = new Border
            {
                Width = 258f,
                Background = Hex(i % 3 == 0 ? "#1B2635" : "#14212C"),
                BorderBrush = Hex("#31495C"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 10, 10)
            };

            var stack = new StackPanel();
            stack.AddChild(new TextBlock
            {
                Text = $"Viewport row {i:D2}",
                Foreground = Hex("#E7F2FA"),
                FontWeight = "SemiBold"
            });
            stack.AddChild(new TextBlock
            {
                Text = "Overflow content for wheel input, thumb dragging, track clicks, and offset clamping.",
                Foreground = Hex("#BFD4E3"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            row.Child = stack;
            _workbenchRowsHost.AddChild(row);
        }
    }

    private void PopulateVirtualizedRows()
    {
        if (_virtualizedRowsHost.GetVisualChildren().Any())
        {
            return;
        }

        for (var i = 1; i <= 180; i++)
        {
            var row = new Border
            {
                Background = Hex(i % 2 == 0 ? "#151F2A" : "#101922"),
                BorderBrush = Hex("#2F4558"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(8, 6, 8, 0)
            };

            var stack = new StackPanel();
            stack.AddChild(new TextBlock
            {
                Text = $"Virtualized item {i:D3}",
                Foreground = Hex("#E7F2FA"),
                FontWeight = "SemiBold"
            });
            stack.AddChild(new TextBlock
            {
                Text = i % 5 == 0
                    ? "Taller row variant to keep realized range and extent calculations honest."
                    : "Standard row for the ScrollViewer plus VirtualizingStackPanel contract.",
                Foreground = Hex("#BFD4E3"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            row.Child = stack;
            _virtualizedRowsHost.AddChild(row);
        }
    }

    private void OnWorkbenchOptionChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressOptionChanges)
        {
            return;
        }

        ApplyWorkbenchOptions();
        UpdateAllReadouts();
    }

    private void OnVisibilitySelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressOptionChanges)
        {
            return;
        }

        ApplyWorkbenchOptions();
        UpdateAllReadouts();
    }

    private void ApplyWorkbenchOptions()
    {
        _workbenchScrollViewer.HorizontalScrollBarVisibility = GetSelectedVisibility(_horizontalVisibilityComboBox, ScrollBarVisibility.Auto);
        _workbenchScrollViewer.VerticalScrollBarVisibility = GetSelectedVisibility(_verticalVisibilityComboBox, ScrollBarVisibility.Auto);
        _workbenchScrollViewer.ScrollBarThickness = _scrollBarThicknessSlider.Value;
        ScrollViewer.SetUseTransformContentScrolling(
            _workbenchDocumentHost,
            _transformContentScrollingCheckBox.IsChecked == true);
    }

    private static ScrollBarVisibility GetSelectedVisibility(ComboBox comboBox, ScrollBarVisibility fallback)
    {
        return comboBox.SelectedItem is string text &&
               Enum.TryParse(text, ignoreCase: false, out ScrollBarVisibility value)
            ? value
            : fallback;
    }

    private void OnOffsetSliderChanged(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        if (_suppressOffsetSliderChanges)
        {
            return;
        }

        _workbenchScrollViewer.ScrollToHorizontalOffset(_horizontalOffsetSlider.Value);
        _workbenchScrollViewer.ScrollToVerticalOffset(_verticalOffsetSlider.Value);
        UpdateAllReadouts();
    }

    private void OnWorkbenchViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchViewportChangedCount++;
        UpdateAllReadouts();
    }

    private void OnCanvasViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateAllReadouts();
    }

    private void OnVirtualizedViewportChanged(object? sender, EventArgs args)
    {
        _ = sender;
        _ = args;
        UpdateAllReadouts();
    }

    private void OnScrollTopClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToVerticalOffset(0f);
        UpdateAllReadouts();
    }

    private void OnScrollMiddleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToVerticalOffset(GetMaxVerticalOffset(_workbenchScrollViewer) / 2f);
        UpdateAllReadouts();
    }

    private void OnScrollBottomClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToVerticalOffset(GetMaxVerticalOffset(_workbenchScrollViewer));
        UpdateAllReadouts();
    }

    private void OnScrollLeftClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToHorizontalOffset(0f);
        UpdateAllReadouts();
    }

    private void OnScrollCenterClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToHorizontalOffset(GetMaxHorizontalOffset(_workbenchScrollViewer) / 2f);
        UpdateAllReadouts();
    }

    private void OnScrollRightClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _workbenchScrollViewer.ScrollToHorizontalOffset(GetMaxHorizontalOffset(_workbenchScrollViewer));
        UpdateAllReadouts();
    }

    private void OnVirtualizedStartClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _virtualizedScrollViewer.ScrollToVerticalOffset(0f);
        UpdateAllReadouts();
    }

    private void OnVirtualizedMiddleClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _virtualizedScrollViewer.ScrollToVerticalOffset(GetMaxVerticalOffset(_virtualizedScrollViewer) / 2f);
        UpdateAllReadouts();
    }

    private void OnVirtualizedEndClicked(object? sender, RoutedSimpleEventArgs args)
    {
        _ = sender;
        _ = args;
        _virtualizedScrollViewer.ScrollToVerticalOffset(GetMaxVerticalOffset(_virtualizedScrollViewer));
        UpdateAllReadouts();
    }

    private void UpdateAllReadouts()
    {
        UpdateWorkbenchReadouts();
        UpdateSecondaryReadouts();
        UpdateOffsetSliders();
    }

    private void UpdateWorkbenchReadouts()
    {
        var maxHorizontal = GetMaxHorizontalOffset(_workbenchScrollViewer);
        var maxVertical = GetMaxVerticalOffset(_workbenchScrollViewer);

        _workbenchOffsetText.Text = $"Offset: H {Format(_workbenchScrollViewer.HorizontalOffset)} / V {Format(_workbenchScrollViewer.VerticalOffset)}";
        _workbenchExtentText.Text = $"Extent: {Format(_workbenchScrollViewer.ExtentWidth)} x {Format(_workbenchScrollViewer.ExtentHeight)}";
        _workbenchViewportText.Text = $"Viewport: {Format(_workbenchScrollViewer.ViewportWidth)} x {Format(_workbenchScrollViewer.ViewportHeight)}";
        _workbenchMaxOffsetText.Text = $"Max offset: H {Format(maxHorizontal)} / V {Format(maxVertical)}";
        _viewportEventCountText.Text = $"ViewportChanged events: {_workbenchViewportChangedCount}";
        _scrollBarThicknessText.Text = $"Thickness: {Format(_workbenchScrollViewer.ScrollBarThickness)} px";
        _liveStatusText.Text =
            $"Workbench {GetSelectedVisibility(_horizontalVisibilityComboBox, ScrollBarVisibility.Auto)} horizontal, " +
            $"{GetSelectedVisibility(_verticalVisibilityComboBox, ScrollBarVisibility.Auto)} vertical, " +
            $"transform scrolling {(_transformContentScrollingCheckBox.IsChecked == true ? "on" : "off")}.";
    }

    private void UpdateSecondaryReadouts()
    {
        _canvasMetricsText.Text =
            $"Canvas offset H {Format(_canvasScrollViewer.HorizontalOffset)} / V {Format(_canvasScrollViewer.VerticalOffset)}; " +
            $"viewport {Format(_canvasScrollViewer.ViewportWidth)} x {Format(_canvasScrollViewer.ViewportHeight)}; " +
            $"extent {Format(_canvasScrollViewer.ExtentWidth)} x {Format(_canvasScrollViewer.ExtentHeight)}.";

        _virtualizedMetricsText.Text =
            $"Virtualized offset V {Format(_virtualizedScrollViewer.VerticalOffset)} of {Format(GetMaxVerticalOffset(_virtualizedScrollViewer))}; " +
            $"viewport {Format(_virtualizedScrollViewer.ViewportHeight)}; extent {Format(_virtualizedScrollViewer.ExtentHeight)}.";
    }

    private void UpdateOffsetSliders()
    {
        _suppressOffsetSliderChanges = true;
        try
        {
            var maxHorizontal = GetMaxHorizontalOffset(_workbenchScrollViewer);
            var maxVertical = GetMaxVerticalOffset(_workbenchScrollViewer);

            _horizontalOffsetSlider.Maximum = Math.Max(1f, maxHorizontal);
            _verticalOffsetSlider.Maximum = Math.Max(1f, maxVertical);
            _horizontalOffsetSlider.Value = Clamp(_workbenchScrollViewer.HorizontalOffset, 0f, _horizontalOffsetSlider.Maximum);
            _verticalOffsetSlider.Value = Clamp(_workbenchScrollViewer.VerticalOffset, 0f, _verticalOffsetSlider.Maximum);
        }
        finally
        {
            _suppressOffsetSliderChanges = false;
        }
    }

    private static float GetMaxHorizontalOffset(ScrollViewer viewer)
    {
        return Math.Max(0f, viewer.ExtentWidth - viewer.ViewportWidth);
    }

    private static float GetMaxVerticalOffset(ScrollViewer viewer)
    {
        return Math.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static string Format(float value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static Color Hex(string value)
    {
        var text = value[0] == '#' ? value[1..] : value;
        return new Color(
            byte.Parse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private T RequireElement<T>(string name)
        where T : class
    {
        return this.FindName(name) as T
            ?? throw new InvalidOperationException($"{name} was not found or is not a {typeof(T).Name}.");
    }
}
