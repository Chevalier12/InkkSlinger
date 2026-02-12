using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class PaintShellViewLayoutTests
{
    public PaintShellViewLayoutTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void PaintShellView_LoadsAndArranges_WithCoreSurfaceAndDataGrids()
    {
        var view = new PaintShellView();
        view.Measure(new Vector2(1600f, 950f));
        view.Arrange(new LayoutRect(0f, 0f, 1600f, 950f));

        var drawingCanvas = GetPrivateProperty<Canvas>(view, "DrawingCanvas");
        var layersGrid = GetPrivateProperty<DataGrid>(view, "LayersGrid");
        var historyGrid = GetPrivateProperty<DataGrid>(view, "HistoryGrid");
        var statusItem = GetPrivateProperty<StatusBarItem>(view, "StatusTextItem");

        Assert.NotNull(drawingCanvas);
        Assert.NotNull(layersGrid);
        Assert.NotNull(historyGrid);
        Assert.NotNull(statusItem);

        Assert.True(drawingCanvas!.LayoutSlot.Width > 500f);
        Assert.True(drawingCanvas.LayoutSlot.Height > 400f);
        Assert.True(layersGrid!.Items.Count >= 3);
        Assert.True(historyGrid!.Items.Count >= 3);
        Assert.Equal("Paint shell initialized.", statusItem!.Content as string);
    }

    [Fact]
    public void PaintShellView_AttachesSelectionAdorners_ToSelectedShape()
    {
        var view = new PaintShellView();
        view.Measure(new Vector2(1600f, 950f));
        view.Arrange(new LayoutRect(0f, 0f, 1600f, 950f));

        var decorator = GetPrivateProperty<AdornerDecorator>(view, "CanvasAdornerRoot");
        var selectedShape = GetPrivateProperty<Border>(view, "SelectedShape");

        Assert.NotNull(decorator);
        Assert.NotNull(selectedShape);

        var adorners = decorator!.AdornerLayer.GetAdorners(selectedShape!);
        Assert.True(adorners.Count >= 2);
    }

    private static T? GetPrivateProperty<T>(object instance, string propertyName)
        where T : class
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        return property?.GetValue(instance) as T;
    }
}
