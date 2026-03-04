using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class FreezableCoreTests
{
    [Fact]
    public void Freeze_SetsIsFrozen_AndPreventsMutation()
    {
        var brush = new SolidColorBrush(new Microsoft.Xna.Framework.Color(10, 20, 30));

        Assert.False(brush.IsFrozen);
        Assert.True(brush.CanFreeze);

        brush.Freeze();

        Assert.True(brush.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => brush.Color = new Microsoft.Xna.Framework.Color(1, 2, 3));
    }

    [Fact]
    public void CanFreeze_False_WhenChildCannotFreeze()
    {
        var freezable = new NonFreezableLeaf();

        Assert.False(freezable.CanFreeze);
        Assert.Throws<InvalidOperationException>(freezable.Freeze);
    }

    [Fact]
    public void Clone_ReturnsDeepUnfrozenCopy()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform { ScaleX = 1.5f, ScaleY = 1.7f });
        group.Freeze();

        var clone = Assert.IsType<TransformGroup>(group.Clone());

        Assert.False(clone.IsFrozen);
        Assert.Single(clone.Children);
        Assert.NotSame(group.Children[0], clone.Children[0]);
        Assert.IsType<ScaleTransform>(clone.Children[0]);

        var cloneScale = (ScaleTransform)clone.Children[0];
        cloneScale.ScaleX = 2f;
        Assert.Equal(1.5f, ((ScaleTransform)group.Children[0]).ScaleX, 3);
    }

    [Fact]
    public void CloneCurrentValue_ReturnsDeepUnfrozenCopy()
    {
        var combined = new CombinedGeometry
        {
            Geometry1 = new PathGeometry("M 0,0 L 10,0"),
            Geometry2 = new PathGeometry("M 1,1 L 3,3")
        };
        combined.Freeze();

        var clone = Assert.IsType<CombinedGeometry>(combined.CloneCurrentValue());

        Assert.False(clone.IsFrozen);
        Assert.NotSame(combined.Geometry1, clone.Geometry1);
        Assert.NotSame(combined.Geometry2, clone.Geometry2);
        clone.Geometry1 = new PathGeometry("M 9,9 L 11,11");
        Assert.NotNull(combined.Geometry1);
    }

    private sealed class NonFreezableLeaf : Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new NonFreezableLeaf();
        }

        protected override bool FreezeCore(bool isChecking)
        {
            return false;
        }
    }
}
