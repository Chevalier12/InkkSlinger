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

    private static void AssertVector(Vector2 actual, float expectedX, float expectedY, float tolerance = 0.001f)
    {
        Assert.InRange(actual.X, expectedX - tolerance, expectedX + tolerance);
        Assert.InRange(actual.Y, expectedY - tolerance, expectedY + tolerance);
    }
}
