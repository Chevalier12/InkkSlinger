using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class GridSplitterTests
{
    public GridSplitterTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Drag_Columns_CurrentAndNext_ResizesDefinitions()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40f) });

        var splitter = new TestGridSplitter
        {
            Width = 6f,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.CurrentAndNext
        };
        Grid.SetColumn(splitter, 0);
        Grid.SetRow(splitter, 0);
        grid.AddChild(splitter);

        grid.Measure(new Vector2(200f, 40f));
        grid.Arrange(new LayoutRect(0f, 0f, 200f, 40f));

        var start = new Vector2(splitter.LayoutSlot.X + 2f, splitter.LayoutSlot.Y + 2f);
        splitter.SimulateMouseDown(start);
        splitter.SimulateMouseMove(new Vector2(start.X + 20f, start.Y));
        splitter.SimulateMouseUp(new Vector2(start.X + 20f, start.Y));

        Assert.True(grid.ColumnDefinitions[0].Width.IsPixel);
        Assert.True(grid.ColumnDefinitions[1].Width.IsPixel);
        Assert.Equal(120f, grid.ColumnDefinitions[0].Width.Value, 3);
        Assert.Equal(80f, grid.ColumnDefinitions[1].Width.Value, 3);
    }

    [Fact]
    public void Drag_Columns_RespectsDefinitionMinimums()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f), MinWidth = 90f });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f), MinWidth = 95f });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40f) });

        var splitter = new TestGridSplitter
        {
            Width = 6f,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.CurrentAndNext
        };
        Grid.SetColumn(splitter, 0);
        Grid.SetRow(splitter, 0);
        grid.AddChild(splitter);

        grid.Measure(new Vector2(200f, 40f));
        grid.Arrange(new LayoutRect(0f, 0f, 200f, 40f));

        var start = new Vector2(splitter.LayoutSlot.X + 2f, splitter.LayoutSlot.Y + 2f);
        splitter.SimulateMouseDown(start);
        splitter.SimulateMouseMove(new Vector2(start.X + 20f, start.Y));
        splitter.SimulateMouseUp(new Vector2(start.X + 20f, start.Y));

        Assert.Equal(105f, grid.ColumnDefinitions[0].Width.Value, 3);
        Assert.Equal(95f, grid.ColumnDefinitions[1].Width.Value, 3);
    }

    [Fact]
    public void Keyboard_Rows_ResizesWithIncrement()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100f) });

        var splitter = new TestGridSplitter
        {
            Height = 6f,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.CurrentAndNext,
            KeyboardIncrement = 15f
        };

        Grid.SetRow(splitter, 0);
        Grid.SetColumn(splitter, 0);
        grid.AddChild(splitter);

        grid.Measure(new Vector2(100f, 200f));
        grid.Arrange(new LayoutRect(0f, 0f, 100f, 200f));

        splitter.SimulateKeyDown(Keys.Down);

        Assert.Equal(115f, grid.RowDefinitions[0].Height.Value, 3);
        Assert.Equal(85f, grid.RowDefinitions[1].Height.Value, 3);
    }

    [Fact]
    public void XamlLoader_ParsesGridSplitterProperties()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid>
                                <Grid.ColumnDefinitions>
                                  <ColumnDefinition Width="*" />
                                  <ColumnDefinition Width="*" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                  <RowDefinition Height="*" />
                                </Grid.RowDefinitions>
                                <GridSplitter Grid.Column="0"
                                              Width="6"
                                              HorizontalAlignment="Right"
                                              ResizeDirection="Columns"
                                              ResizeBehavior="CurrentAndNext"
                                              KeyboardIncrement="12"
                                              DragIncrement="3" />
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var grid = Assert.IsType<Grid>(view.Content);
        var splitter = Assert.IsType<GridSplitter>(grid.Children[0]);
        Assert.Equal(GridResizeDirection.Columns, splitter.ResizeDirection);
        Assert.Equal(GridResizeBehavior.CurrentAndNext, splitter.ResizeBehavior);
        Assert.Equal(12f, splitter.KeyboardIncrement, 3);
        Assert.Equal(3f, splitter.DragIncrement, 3);
    }

    [Fact]
    public void StyleTrigger_ChangesCursor_WhenMouseOver()
    {
        var splitter = new TestGridSplitter();
        var style = new Style(typeof(GridSplitter));
        var hoverTrigger = new Trigger(GridSplitter.IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(UIElement.CursorProperty, UiCursor.SizeWE));
        style.Triggers.Add(hoverTrigger);
        splitter.Style = style;

        Assert.Equal(UiCursor.Arrow, splitter.Cursor);
        splitter.SimulateMouseEnter(new Vector2(5f, 5f));
        Assert.Equal(UiCursor.SizeWE, splitter.Cursor);
        splitter.SimulateMouseLeave(new Vector2(20f, 5f));
        Assert.Equal(UiCursor.Arrow, splitter.Cursor);
    }

    private sealed class TestGridSplitter : GridSplitter
    {
        public void SimulateMouseDown(Vector2 position)
        {
            OnMouseLeftButtonDown(new RoutedMouseButtonEventArgs(
                UIElement.MouseLeftButtonDownEvent,
                position,
                MouseButton.Left,
                1,
                ModifierKeys.None));
        }

        public void SimulateMouseMove(Vector2 position)
        {
            OnMouseMove(new RoutedMouseEventArgs(
                UIElement.MouseMoveEvent,
                position,
                ModifierKeys.None));
        }

        public void SimulateMouseEnter(Vector2 position)
        {
            OnMouseEnter(new RoutedMouseEventArgs(
                UIElement.MouseEnterEvent,
                position,
                ModifierKeys.None));
        }

        public void SimulateMouseLeave(Vector2 position)
        {
            OnMouseLeave(new RoutedMouseEventArgs(
                UIElement.MouseLeaveEvent,
                position,
                ModifierKeys.None));
        }

        public void SimulateMouseUp(Vector2 position)
        {
            OnMouseLeftButtonUp(new RoutedMouseButtonEventArgs(
                UIElement.MouseLeftButtonUpEvent,
                position,
                MouseButton.Left,
                1,
                ModifierKeys.None));
        }

        public void SimulateKeyDown(Keys key)
        {
            OnKeyDown(new RoutedKeyEventArgs(
                UIElement.KeyDownEvent,
                key,
                false,
                ModifierKeys.None));
        }
    }
}
