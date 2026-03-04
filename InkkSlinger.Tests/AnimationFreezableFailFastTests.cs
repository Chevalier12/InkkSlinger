using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AnimationFreezableFailFastTests
{
    [Fact]
    public void Storyboard_TargetingFrozenTransform_ThrowsWithContext()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button();
            var transform = new ScaleTransform { ScaleX = 1f, ScaleY = 1f };
            transform.Freeze();
            button.RenderTransform = transform;

            var storyboard = new Storyboard();
            storyboard.Children.Add(new DoubleAnimation
            {
                TargetProperty = "RenderTransform.ScaleX",
                To = 1.5f,
                Duration = TimeSpan.FromMilliseconds(100)
            });

            manager.BeginStoryboard(
                storyboard,
                button,
                controlName: null,
                resolveTargetByName: null,
                isControllable: false,
                handoff: HandoffBehavior.SnapshotAndReplace);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                manager.Update(new GameTime(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(16))));

            Assert.Contains("ScaleTransform.ScaleX", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            manager.ResetForTests();
        }
    }
}
