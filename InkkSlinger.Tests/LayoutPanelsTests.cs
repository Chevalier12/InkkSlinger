using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class LayoutPanelsTests
{
    public LayoutPanelsTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void Canvas_UsesAttachedLeftTop_ForChildArrange()
    {
        var canvas = new TestCanvas();
        var child = new FixedSizeElement(40f, 20f);
        Canvas.SetLeft(child, 12f);
        Canvas.SetTop(child, 18f);
        canvas.AddChild(child);

        canvas.Measure(new Vector2(300f, 300f));
        canvas.Arrange(new LayoutRect(10f, 20f, 200f, 120f));

        Assert.Equal(52f, canvas.DesiredSize.X, 3);
        Assert.Equal(38f, canvas.DesiredSize.Y, 3);
        Assert.Equal(22f, child.LayoutSlot.X, 3);
        Assert.Equal(38f, child.LayoutSlot.Y, 3);
        Assert.Equal(40f, child.LayoutSlot.Width, 3);
        Assert.Equal(20f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void Canvas_ClipsChildren_ToLayoutSlot()
    {
        var canvas = new TestCanvas();
        canvas.Measure(new Vector2(200f, 120f));
        canvas.Arrange(new LayoutRect(10f, 20f, 200f, 120f));

        var hasClip = canvas.TryGetClipRectForTesting(out var clipRect);
        Assert.True(hasClip);
        Assert.Equal(canvas.LayoutSlot.X, clipRect.X, 3);
        Assert.Equal(canvas.LayoutSlot.Y, clipRect.Y, 3);
        Assert.Equal(canvas.LayoutSlot.Width, clipRect.Width, 3);
        Assert.Equal(canvas.LayoutSlot.Height, clipRect.Height, 3);
    }

    [Fact]
    public void DockPanel_DocksChildren_AndLastChildFill()
    {
        var panel = new DockPanel();

        var left = new FixedSizeElement(30f, 40f);
        DockPanel.SetDock(left, Dock.Left);
        panel.AddChild(left);

        var top = new FixedSizeElement(50f, 20f);
        DockPanel.SetDock(top, Dock.Top);
        panel.AddChild(top);

        var fill = new FixedSizeElement(10f, 10f);
        panel.AddChild(fill);

        panel.Measure(new Vector2(200f, 100f));
        panel.Arrange(new LayoutRect(0f, 0f, 200f, 100f));

        Assert.Equal(0f, left.LayoutSlot.X, 3);
        Assert.Equal(0f, left.LayoutSlot.Y, 3);
        Assert.Equal(30f, left.LayoutSlot.Width, 3);
        Assert.Equal(100f, left.LayoutSlot.Height, 3);

        Assert.Equal(30f, top.LayoutSlot.X, 3);
        Assert.Equal(0f, top.LayoutSlot.Y, 3);
        Assert.Equal(170f, top.LayoutSlot.Width, 3);
        Assert.Equal(20f, top.LayoutSlot.Height, 3);

        Assert.Equal(30f, fill.LayoutSlot.X, 3);
        Assert.Equal(20f, fill.LayoutSlot.Y, 3);
        Assert.Equal(170f, fill.LayoutSlot.Width, 3);
        Assert.Equal(80f, fill.LayoutSlot.Height, 3);
    }

    [Fact]
    public void WrapPanel_WrapsChildren_WhenLineLimitExceeded()
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 40f,
            ItemHeight = 20f
        };

        panel.AddChild(new FixedSizeElement(5f, 5f));
        panel.AddChild(new FixedSizeElement(5f, 5f));
        panel.AddChild(new FixedSizeElement(5f, 5f));

        panel.Measure(new Vector2(85f, 200f));
        panel.Arrange(new LayoutRect(0f, 0f, 85f, 200f));

        var first = Assert.IsType<FixedSizeElement>(panel.Children[0]);
        var second = Assert.IsType<FixedSizeElement>(panel.Children[1]);
        var third = Assert.IsType<FixedSizeElement>(panel.Children[2]);

        Assert.Equal(0f, first.LayoutSlot.X, 3);
        Assert.Equal(0f, first.LayoutSlot.Y, 3);
        Assert.Equal(40f, second.LayoutSlot.X, 3);
        Assert.Equal(0f, second.LayoutSlot.Y, 3);
        Assert.Equal(0f, third.LayoutSlot.X, 3);
        Assert.Equal(20f, third.LayoutSlot.Y, 3);
    }

    [Fact]
    public void UniformGrid_UsesRowsColumnsAndFirstColumn()
    {
        var grid = new UniformGrid
        {
            Rows = 2,
            Columns = 3,
            FirstColumn = 1
        };

        var a = new FixedSizeElement(10f, 10f);
        var b = new FixedSizeElement(10f, 10f);
        var c = new FixedSizeElement(10f, 10f);

        grid.AddChild(a);
        grid.AddChild(b);
        grid.AddChild(c);

        grid.Measure(new Vector2(300f, 100f));
        grid.Arrange(new LayoutRect(0f, 0f, 300f, 100f));

        Assert.Equal(100f, a.LayoutSlot.X, 3);
        Assert.Equal(0f, a.LayoutSlot.Y, 3);

        Assert.Equal(200f, b.LayoutSlot.X, 3);
        Assert.Equal(0f, b.LayoutSlot.Y, 3);

        Assert.Equal(0f, c.LayoutSlot.X, 3);
        Assert.Equal(50f, c.LayoutSlot.Y, 3);
    }

    [Fact]
    public void XamlLoader_ParsesGroupFPanels_AndAttachedLayoutAttributes()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <DockPanel LastChildFill="true">
                                <Canvas DockPanel.Dock="Left">
                                  <Label Text="A" Canvas.Left="8" Canvas.Top="10" />
                                </Canvas>
                                <WrapPanel DockPanel.Dock="Top" Orientation="Horizontal" ItemWidth="30" ItemHeight="12">
                                  <Label Text="B" />
                                  <Label Text="C" />
                                </WrapPanel>
                                <UniformGrid Rows="1" Columns="2" FirstColumn="0">
                                  <Label Text="D" />
                                  <Label Text="E" />
                                </UniformGrid>
                              </DockPanel>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var root = Assert.IsType<DockPanel>(view.Content);
        Assert.Equal(3, root.Children.Count);
        Assert.IsType<Canvas>(root.Children[0]);
        Assert.IsType<WrapPanel>(root.Children[1]);
        Assert.IsType<UniformGrid>(root.Children[2]);
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public FixedSizeElement(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }

    private sealed class TestCanvas : Canvas
    {
        public bool TryGetClipRectForTesting(out LayoutRect clipRect)
        {
            return TryGetClipRect(out clipRect);
        }
    }
}
