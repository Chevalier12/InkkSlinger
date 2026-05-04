using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class PathMarkupParserParityTests
{
    [Fact]
    public void SmoothCubic_ContinuesFromPreviousCubic_ReflectsControlPoint()
    {
        var geometry = PathGeometry.Parse("M 0,0 C 10,0 20,10 30,10 S 50,20 60,10");
        var figure = Assert.Single(geometry.Figures);

        var smoothStart = figure.Points[16];
        var smoothFirstSample = figure.Points[17];
        AssertVector(smoothStart, 30f, 10f);
        AssertVector(smoothFirstSample, 31.875f, 10.11f, 0.01f);
        AssertVector(figure.Points[^1], 60f, 10f);
    }

    [Fact]
    public void SmoothCubic_WithoutPriorCubic_UsesCurrentPointAsFirstControl()
    {
        var geometry = PathGeometry.Parse("M 0,0 L 10,10 S 20,20 30,10");
        var figure = Assert.Single(geometry.Figures);

        var smoothStart = figure.Points[1];
        var smoothFirstSample = figure.Points[2];
        AssertVector(smoothStart, 10f, 10f);
        AssertVector(smoothFirstSample, 10.112f, 10.112f, 0.01f);
        AssertVector(figure.Points[^1], 30f, 10f);
    }

    [Fact]
    public void SmoothQuadratic_ContinuesFromPreviousQuadratic_ReflectsControlPoint()
    {
        var geometry = PathGeometry.Parse("M 0,0 Q 10,0 20,10 T 40,10");
        var figure = Assert.Single(geometry.Figures);

        var smoothStart = figure.Points[12];
        var smoothFirstSample = figure.Points[13];
        AssertVector(smoothStart, 20f, 10f);
        AssertVector(smoothFirstSample, 21.667f, 11.528f, 0.01f);
        AssertVector(figure.Points[^1], 40f, 10f);
    }

    [Fact]
    public void SmoothQuadratic_WithoutPriorQuadratic_UsesCurrentPointAsControl()
    {
        var geometry = PathGeometry.Parse("M 0,0 L 10,10 T 20,0");
        var figure = Assert.Single(geometry.Figures);

        var smoothStart = figure.Points[1];
        var smoothFirstSample = figure.Points[2];
        AssertVector(smoothStart, 10f, 10f);
        AssertVector(smoothFirstSample, 10.069f, 9.931f, 0.01f);
        AssertVector(figure.Points[^1], 20f, 0f);
    }

    [Fact]
    public void Arc_Absolute_ParsesAndProducesFigureEndingAtExpectedPoint()
    {
        var geometry = PathGeometry.Parse("M 0,0 A 10,5 0 0 1 20,0");
        var figure = Assert.Single(geometry.Figures);

        Assert.True(figure.Points.Count > 2);
        AssertVector(figure.Points[^1], 20f, 0f);
        Assert.False(figure.IsClosed);
    }

    [Fact]
    public void Arc_Relative_ParsesAndProducesRelativeEndpoint()
    {
        var geometry = PathGeometry.Parse("M 10,10 a 5,5 0 0 0 10,-10");
        var figure = Assert.Single(geometry.Figures);

        Assert.True(figure.Points.Count > 2);
        AssertVector(figure.Points[^1], 20f, 0f);
    }

    [Fact]
    public void Arc_ZeroRadius_FallsBackToLineSegment()
    {
        var geometry = PathGeometry.Parse("M 0,0 A 0,10 0 0 1 10,10");
        var figure = Assert.Single(geometry.Figures);

        Assert.Equal(2, figure.Points.Count);
        AssertVector(figure.Points[^1], 10f, 10f);
    }

    [Fact]
    public void Arc_InvalidFlag_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(() => PathGeometry.Parse("M 0,0 A 10,10 0 2 1 20,20"));
        Assert.Contains("Arc flag must be 0 or 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arc_LargeArcSweepFlags_SelectDifferentTraversal()
    {
        var smallArc = PathGeometry.Parse("M 0,0 A 10,10 0 0 1 10,0");
        var largeArc = PathGeometry.Parse("M 0,0 A 10,10 0 1 1 10,0");

        var smallFigure = Assert.Single(smallArc.Figures);
        var largeFigure = Assert.Single(largeArc.Figures);

        AssertVector(smallFigure.Points[^1], 10f, 0f);
        AssertVector(largeFigure.Points[^1], 10f, 0f);
        Assert.NotEqual(smallFigure.Points.Count, largeFigure.Points.Count);
    }

    [Fact]
    public void MixedCommands_WithS_T_A_ProducesSingleValidFigure()
    {
        var geometry = PathGeometry.Parse("M 0,0 C 10,0 20,10 30,10 S 50,20 60,10 T 80,0 A 12,6 30 0 1 100,0");
        var figure = Assert.Single(geometry.Figures);

        Assert.True(figure.Points.Count > 30);
        Assert.False(figure.IsClosed);
        AssertVector(figure.Points[^1], 100f, 0f);
    }

    [Fact]
    public void Xaml_PathShapeData_WithA_S_T_ParsesThroughLoader()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid>
                                <Path x:Name="Probe"
                                      Data="M 0,0 A 10,5 0 0 1 20,0 C 30,0 40,10 50,0 S 70,-10 80,0 T 100,0" />
                              </Grid>
                            </UserControl>
                            """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var path = Assert.IsType<PathShape>(root.FindName("Probe"));
        var geometry = Assert.IsType<PathGeometry>(path.Data);
        var figure = Assert.Single(geometry.Figures);

        Assert.True(figure.Points.Count > 30);
        AssertVector(figure.Points[^1], 100f, 0f);
    }

    [Fact]
    public void SvgShoppingCartPath_ParsesCurvesAndOpenSubpaths()
    {
        var geometry = PathGeometry.Parse(ShoppingCartPathData);

        Assert.Equal(5, geometry.Figures.Count);
        AssertVector(geometry.Figures[0].Points[0], 8f, 17.5f);
        AssertVector(geometry.Figures[0].Points[^1], 3f, 5.5f);
        AssertVector(geometry.Figures[1].Points[^1], 17f, 17.5f);
        Assert.True(geometry.Figures[2].Points.Count > 40);
        Assert.True(geometry.Figures[3].Points.Count > 40);
        AssertVector(geometry.Figures[^1].Points[^1], 6f, 7.5f);
        Assert.All(geometry.Figures, static figure => Assert.False(figure.IsClosed));
    }

    [Fact]
    public void SvgSkullPath_ParsesCompoundClosedFigures()
    {
        var geometry = PathGeometry.Parse(SkullPathData);

        Assert.Equal(4, geometry.Figures.Count);
        Assert.All(geometry.Figures, static figure => Assert.True(figure.IsClosed));
        Assert.True(geometry.Figures[0].Points.Count > 100);
        AssertVector(geometry.Figures[0].Points[0], 11f, 18f);
        Assert.True(geometry.Figures[1].Points.Count > 30);
        Assert.True(geometry.Figures[2].Points.Count > 30);
        Assert.True(geometry.Figures[3].Points.Count > 30);
    }

    [Fact]
    public void Xaml_PathShapeStrokeProperties_ParseThroughLoader()
    {
        var xaml = $$"""
                     <UserControl xmlns="urn:inkkslinger-ui"
                                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                       <Grid>
                         <Path x:Name="Probe"
                               Data="{{ShoppingCartPathData}}"
                               Fill="Transparent"
                               Stroke="#121923"
                               StrokeThickness="1.2"
                               StrokeLineJoin="Miter"
                               StrokeStartLineCap="Round"
                               StrokeEndLineCap="Square"
                               StrokeDashCap="Triangle"
                               StrokeMiterLimit="4"
                               StrokeDashArray="2,1"
                               StrokeDashOffset="0.5"
                               Stretch="Uniform" />
                       </Grid>
                     </UserControl>
                     """;

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var path = Assert.IsType<PathShape>(root.FindName("Probe"));

        Assert.IsType<PathGeometry>(path.Data);
        Assert.Equal(new Color(0x12, 0x19, 0x23), path.Stroke);
        Assert.Equal(1.2f, path.StrokeThickness);
        Assert.Equal(StrokeLineJoin.Miter, path.StrokeLineJoin);
        Assert.Equal(StrokeLineCap.Round, path.StrokeStartLineCap);
        Assert.Equal(StrokeLineCap.Square, path.StrokeEndLineCap);
        Assert.Equal(StrokeLineCap.Triangle, path.StrokeDashCap);
        Assert.Equal(4f, path.StrokeMiterLimit);
        Assert.Equal(0.5f, path.StrokeDashOffset);
        var dashArray = Assert.IsType<DoubleCollection>(path.StrokeDashArray);
        Assert.Equal([2d, 1d], dashArray);
    }

    private const string ShoppingCartPathData =
        "M8 17.5L5.81763 6.26772C5.71013 5.81757 5.30779 5.5 4.84498 5.5H3" +
        "M8 17.5H17" +
        "M8 17.5C8.82843 17.5 9.5 18.1716 9.5 19C9.5 19.8284 8.82843 20.5 8 20.5C7.17157 20.5 6.5 19.8284 6.5 19C6.5 18.1716 7.17157 17.5 8 17.5" +
        "M17 17.5C17.8284 17.5 18.5 18.1716 18.5 19C18.5 19.8284 17.8284 20.5 17 20.5C16.1716 20.5 15.5 19.8284 15.5 19C15.5 18.1716 16.1716 17.5 17 17.5" +
        "M7.78357 14.5H17.5L19 7.5H6";

    private const string SkullPathData =
        "M11 18h2a1 1 0 0 0 1-1v-2.07l1.065-.563A5.498 5.498 0 0 0 18 9.5v-2A5.5 5.5 0 0 0 12.5 2h-5A5.5 5.5 0 0 0 2 7.5v2a5.498 5.498 0 0 0 2.935 4.867L6 14.93V17a1 1 0 0 0 1 1h2v-2a1 1 0 0 1 2 0v2z" +
        "m5-1a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3v-.865A7.499 7.499 0 0 1 0 9.5v-2A7.5 7.5 0 0 1 7.5 0h5A7.5 7.5 0 0 1 20 7.5v2a7.499 7.499 0 0 1-4 6.635V17z" +
        "M6 11a2 2 0 1 1 0-4 2 2 0 0 1 0 4z" +
        "m8 0a2 2 0 1 1 0-4 2 2 0 0 1 0 4z";

    private static void AssertVector(Vector2 actual, float expectedX, float expectedY, float tolerance = 0.001f)
    {
        Assert.InRange(actual.X, expectedX - tolerance, expectedX + tolerance);
        Assert.InRange(actual.Y, expectedY - tolerance, expectedY + tolerance);
    }
}
