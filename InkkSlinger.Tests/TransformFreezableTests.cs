using System;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TransformFreezableTests
{
    [Fact]
    public void FrozenScaleTransform_PropertySet_Throws()
    {
        var transform = new ScaleTransform { ScaleX = 1f, ScaleY = 1f };
        transform.Freeze();

        Assert.Throws<InvalidOperationException>(() => transform.ScaleX = 2f);
    }

    [Fact]
    public void FrozenTransformGroup_ChildrenMutation_Throws()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform());
        group.Freeze();

        Assert.Throws<InvalidOperationException>(() => group.Children.Add(new RotateTransform()));
        Assert.Throws<InvalidOperationException>(() => group.Children.RemoveAt(0));
        Assert.Throws<InvalidOperationException>(group.Children.Clear);
    }

    [Fact]
    public void FrozenTransformGroup_ChildMutation_Throws()
    {
        var child = new ScaleTransform { ScaleX = 1f, ScaleY = 1f };
        var group = new TransformGroup();
        group.Children.Add(child);

        group.Freeze();

        Assert.True(child.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => child.ScaleY = 3f);
    }
}
