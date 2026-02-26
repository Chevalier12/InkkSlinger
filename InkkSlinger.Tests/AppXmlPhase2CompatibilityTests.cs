using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppXmlPhase2CompatibilityTests
{
    [Fact]
    public void ListBox_ItemContainerStyle_AppliesToGeneratedContainers()
    {
        var listBox = new ListBox
        {
            Width = 280f,
            Height = 180f
        };
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new Color(0x22, 0x44, 0x66)));
        listBox.ItemContainerStyle = itemStyle;
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");

        var uiRoot = BuildUiRootWithSingleChild(listBox, 420, 260);
        RunLayout(uiRoot, 420, 260);

        var hostPanel = FindItemsHostPanel(listBox);
        var firstItem = Assert.IsType<ListBoxItem>(hostPanel.Children[0]);
        Assert.Equal(new Color(0x22, 0x44, 0x66), firstItem.Background);
    }

    [Fact]
    public void ComboBox_DropDown_UsesComboBoxItemContainers_AndHonorsItemContainerStyleTriggers()
    {
        var comboBox = new ComboBox
        {
            Width = 240f,
            Height = 34f
        };
        comboBox.Items.Add("Alpha");
        comboBox.Items.Add("Beta");
        comboBox.Items.Add("Gamma");

        var baseColor = new Color(0x20, 0x30, 0x40);
        var hoverColor = new Color(0x66, 0x77, 0x88);
        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, baseColor));
        var hoverTrigger = new Trigger(ComboBoxItem.IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, hoverColor));
        itemStyle.Triggers.Add(hoverTrigger);
        comboBox.ItemContainerStyle = itemStyle;

        var uiRoot = BuildUiRootWithSingleChild(comboBox, 480, 320);
        RunLayout(uiRoot, 480, 320);

        comboBox.IsDropDownOpen = true;
        RunLayout(uiRoot, 480, 320);

        var dropDown = comboBox.DropDownListForTesting;
        Assert.NotNull(dropDown);
        var hostPanel = FindItemsHostPanel(dropDown!);
        var firstItem = Assert.IsType<ComboBoxItem>(hostPanel.Children[0]);
        Assert.Equal(baseColor, firstItem.Background);

        var itemCenter = GetCenter(firstItem.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(itemCenter, pointerMoved: true));

        Assert.True(firstItem.IsMouseOver);
        Assert.Equal(hoverColor, firstItem.Background);
    }

    [Fact]
    public void DataGrid_HeadersVisibility_Column_HidesRowHeaders_AndKeepsColumnHeaders()
    {
        var grid = CreateTwoColumnGrid();
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;

        var uiRoot = BuildUiRootWithSingleChild(grid, 640, 420);
        RunLayout(uiRoot, 640, 420);

        Assert.True(grid.ColumnHeadersVisibleForTesting);
        Assert.False(grid.RowHeadersVisibleForTesting);

        var rows = grid.RowsForTesting;
        Assert.NotEmpty(rows);
        for (var i = 0; i < rows.Count; i++)
        {
            Assert.False(rows[i].RowHeaderForTesting.IsVisible);
        }
    }

    [Fact]
    public void DataGrid_GridLineProperties_MapToCellRenderConfiguration()
    {
        var grid = CreateTwoColumnGrid();
        var horizontalBrush = new Color(0xA0, 0x30, 0x20);
        var verticalBrush = new Color(0x10, 0xD0, 0x70);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        grid.HorizontalGridLinesBrush = horizontalBrush;
        grid.VerticalGridLinesBrush = verticalBrush;

        var uiRoot = BuildUiRootWithSingleChild(grid, 640, 420);
        RunLayout(uiRoot, 640, 420);

        var row = Assert.Single(grid.RowsForTesting);
        var cell = Assert.IsType<DataGridCell>(row.Cells[0]);
        Assert.True(cell.ShowHorizontalGridLineForTesting);
        Assert.False(cell.ShowVerticalGridLineForTesting);
        Assert.Equal(horizontalBrush, cell.HorizontalGridLineBrushForTesting);
        Assert.Equal(verticalBrush, cell.VerticalGridLineBrushForTesting);
    }

    [Fact]
    public void DataGridRow_HoverTrigger_CanOverrideBackground()
    {
        var grid = CreateTwoColumnGrid();
        var hoverBackground = new Color(0x88, 0x55, 0x22);
        var rowStyle = new Style(typeof(DataGridRow));
        var hoverTrigger = new Trigger(Control.IsMouseOverProperty, true);
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
        rowStyle.Triggers.Add(hoverTrigger);
        grid.ItemContainerStyle = rowStyle;

        var uiRoot = BuildUiRootWithSingleChild(grid, 640, 420);
        RunLayout(uiRoot, 640, 420);

        var row = Assert.Single(grid.RowsForTesting);
        Assert.False(row.IsMouseOver);

        var rowCenter = GetCenter(row.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(rowCenter, pointerMoved: true));

        Assert.True(row.IsMouseOver);
        Assert.Equal(hoverBackground, row.Background);
    }

    private static DataGrid CreateTwoColumnGrid()
    {
        var grid = new DataGrid
        {
            Width = 520f,
            Height = 280f,
            ItemsSource = new ObservableCollection<Row>
            {
                new("Alpha", 1),
            }
        };
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Name",
            BindingPath = nameof(Row.Name)
        });
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Count",
            BindingPath = nameof(Row.Count)
        });
        return grid;
    }

    private static UiRoot BuildUiRootWithSingleChild(FrameworkElement element, int width, int height)
    {
        var host = new Canvas
        {
            Width = width,
            Height = height
        };
        host.AddChild(element);
        Canvas.SetLeft(element, 30f);
        Canvas.SetTop(element, 20f);
        return new UiRoot(host);
    }

    private static Panel FindItemsHostPanel(ListBox listBox)
    {
        foreach (var child in listBox.GetVisualChildren())
        {
            if (child is not ScrollViewer viewer)
            {
                continue;
            }

            foreach (var viewerChild in viewer.GetVisualChildren())
            {
                if (viewerChild is Panel panel)
                {
                    return panel;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve ListBox items host panel.");
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }

    private sealed record Row(string Name, int Count);
}
