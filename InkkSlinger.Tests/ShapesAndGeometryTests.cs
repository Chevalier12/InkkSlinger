using Xunit;

namespace InkkSlinger.Tests;

public class ShapesAndGeometryTests
{
    public ShapesAndGeometryTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void PathGeometry_Parse_SupportsCommonCommands()
    {
        var geometry = PathGeometry.Parse("M 0,0 L 10,0 10,10 H 0 V 0 Z");
        var figures = geometry.GetFlattenedFigures();

        Assert.Single(figures);
        var figure = figures[0];
        Assert.True(figure.IsClosed);
        Assert.True(figure.Points.Count >= 5);
    }

    [Fact]
    public void XamlLoader_Loads_Path_WithNestedGeometryObjects()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Path Fill="#204060" Stroke="#AACCFF" StrokeThickness="2">
                                <Path.Data>
                                  <GeometryGroup FillRule="EvenOdd">
                                    <GeometryGroup.Children>
                                      <PathGeometry Data="M 0,0 L 20,0 20,20 Z" />
                                      <CombinedGeometry GeometryCombineMode="Union">
                                        <CombinedGeometry.Geometry1>
                                          <PathGeometry Data="M 3,3 L 8,3 8,8 Z" />
                                        </CombinedGeometry.Geometry1>
                                        <CombinedGeometry.Geometry2>
                                          <PathGeometry Data="M 12,12 L 16,12 16,16 Z" />
                                        </CombinedGeometry.Geometry2>
                                      </CombinedGeometry>
                                    </GeometryGroup.Children>
                                    <Geometry.Transform>
                                      <TranslateTransform X="2" Y="4" />
                                    </Geometry.Transform>
                                  </GeometryGroup>
                                </Path.Data>
                              </Path>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var path = Assert.IsType<PathShape>(view.Content);
        Assert.Equal(2f, path.StrokeThickness, 3);
        Assert.NotNull(path.Data);

        var group = Assert.IsType<GeometryGroup>(path.Data);
        Assert.Equal(FillRule.EvenOdd, group.FillRule);
        Assert.Equal(2, group.Children.Count);
        Assert.IsType<TranslateTransform>(group.Transform);
    }

    [Fact]
    public void XamlLoader_ResolvesShapeAliases()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <StackPanel>
                                <Rectangle Width="40" Height="20" Fill="#203040" Stroke="#80A0C0" />
                                <Ellipse Width="24" Height="24" Fill="#507090" />
                                <Line X1="0" Y1="0" X2="20" Y2="10" Stroke="#CCDDEE" StrokeThickness="2" />
                                <Polygon Points="0,0 10,0 10,10 0,10" Fill="#223344" />
                                <Polyline Points="0,0 5,5 10,0" Stroke="#99BBDD" />
                                <Path Data="M 0,0 L 6,0 6,6 Z" Fill="#112233" />
                              </StackPanel>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var stack = Assert.IsType<StackPanel>(view.Content);
        Assert.IsType<RectangleShape>(stack.Children[0]);
        Assert.IsType<EllipseShape>(stack.Children[1]);
        Assert.IsType<LineShape>(stack.Children[2]);
        Assert.IsType<PolygonShape>(stack.Children[3]);
        Assert.IsType<PolylineShape>(stack.Children[4]);
        Assert.IsType<PathShape>(stack.Children[5]);
    }
}
