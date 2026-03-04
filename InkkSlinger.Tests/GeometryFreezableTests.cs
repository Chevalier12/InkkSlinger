using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class GeometryFreezableTests
{
    [Fact]
    public void FrozenPathGeometry_FiguresMutation_Throws()
    {
        var geometry = new PathGeometry("M 0,0 L 10,10");
        geometry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            geometry.Figures.Add(new GeometryFigure(new[] { new Microsoft.Xna.Framework.Vector2(0f, 0f), new Microsoft.Xna.Framework.Vector2(1f, 1f) }, false)));
    }

    [Fact]
    public void FrozenGeometryGroup_ChildrenMutation_Throws()
    {
        var geometryGroup = new GeometryGroup();
        geometryGroup.Children.Add(new PathGeometry("M 0,0 L 10,0"));
        geometryGroup.Freeze();

        Assert.Throws<InvalidOperationException>(() => geometryGroup.Children.Add(new PathGeometry("M 1,1 L 2,2")));
        Assert.Throws<InvalidOperationException>(geometryGroup.Children.Clear);
    }

    [Fact]
    public void FrozenCombinedGeometry_GeometryAssignment_Throws()
    {
        var combined = new CombinedGeometry
        {
            Geometry1 = new PathGeometry("M 0,0 L 10,0"),
            Geometry2 = new PathGeometry("M 1,1 L 2,2")
        };
        combined.Freeze();

        Assert.Throws<InvalidOperationException>(() => combined.Geometry1 = new PathGeometry("M 4,4 L 5,5"));
        Assert.Throws<InvalidOperationException>(() => combined.Geometry2 = new PathGeometry("M 6,6 L 7,7"));
    }
}
