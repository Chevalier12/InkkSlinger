using System.Collections;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public partial class DataGridView : UserControl
{
    private const float StackedLayoutBreakpoint = 760f;
    private const float InfoRailWidth = 320f;
    private const float StackedInfoRailHeight = 180f;
    private const float StackedDemoGridMinHeight = 240f;
    private const float DefaultDemoGridMinHeight = 300f;
    private static readonly Thickness DefaultDemoGridMargin = new(0f, 12f, 0f, 12f);
    private static readonly Thickness StackedDemoGridMargin = new(0f, 12f, 0f, 28f);

    private Grid? _contentGrid;
    private Border? _bodyBorder;
    private ScrollViewer? _infoScrollViewer;
    private DataGrid? _demoGrid;
    private bool _isStackedLayout;

    public DataGridView()
    {
        DemoRows = ControlDemoSupport.CreateSampleDataGridItemsSource();
        InitializeComponent();
        DataContext = this;
    }

    public IEnumerable DemoRows { get; }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateResponsiveLayout(availableSize.X);
        return base.MeasureOverride(availableSize);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        UpdateResponsiveLayout(finalSize.X);
        return base.ArrangeOverride(finalSize);
    }

    private void UpdateResponsiveLayout(float availableWidth)
    {
        _contentGrid ??= this.FindName("DataGridViewContentGrid") as Grid;
        _bodyBorder ??= this.FindName("DataGridViewBodyBorder") as Border;
        _infoScrollViewer ??= this.FindName("DataGridViewInfoScrollViewer") as ScrollViewer;
        _demoGrid ??= this.FindName("DemoGrid") as DataGrid;

        if (_contentGrid == null ||
            _bodyBorder == null ||
            _infoScrollViewer == null ||
            _demoGrid == null ||
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
            _demoGrid.MinHeight = StackedDemoGridMinHeight;
            _demoGrid.Margin = StackedDemoGridMargin;
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
            _demoGrid.MinHeight = DefaultDemoGridMinHeight;
            _demoGrid.Margin = DefaultDemoGridMargin;
        }

        _contentGrid.InvalidateMeasure();
        _contentGrid.InvalidateArrange();
        _bodyBorder.InvalidateMeasure();
        _infoScrollViewer.InvalidateMeasure();
    }
}
