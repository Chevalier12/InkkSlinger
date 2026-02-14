using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class UiInvalidationRenderTests
{
    public UiInvalidationRenderTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
    }

    [Fact]
    public void InitialFrame_DrawsAtLeastOnce()
    {
        var root = CreateRoot();

        try
        {
            var drew = root.ExecuteDrawPassForTesting();

            Assert.True(drew);
            Assert.Equal(1, root.DrawExecutedFrameCount);
            Assert.Equal(0, root.DrawSkippedFrameCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void NoInvalidation_AndNoForceRedraw_SkipsDraw()
    {
        var root = CreateRoot();

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting()); // First frame must draw.

            var drew = root.ExecuteDrawPassForTesting();

            Assert.False(drew);
            Assert.Equal(1, root.DrawExecutedFrameCount);
            Assert.Equal(1, root.DrawSkippedFrameCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void InvalidateVisual_TriggersNextFrameDraw()
    {
        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            panel.InvalidateVisual();
            var drew = root.ExecuteDrawPassForTesting();

            Assert.True(drew);
            Assert.Equal(2, root.DrawExecutedFrameCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void InvalidateMeasure_SetsLayoutDirty_AndDraws()
    {
        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            panel.InvalidateMeasure();

            Assert.True(root.IsLayoutDirty);
            Assert.True(root.IsVisualDirty);

            var drew = root.ExecuteDrawPassForTesting();
            Assert.True(drew);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void ActiveAnimation_ForcesDrawWithoutExplicitInvalidation()
    {
        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.IsVisualDirty);

            var storyboard = new Storyboard();
            storyboard.Children.Add(new DoubleAnimation
            {
                TargetProperty = nameof(UIElement.Opacity),
                From = 1f,
                To = 0.5f,
                Duration = new Duration(TimeSpan.FromSeconds(1))
            });

            storyboard.Begin(panel, isControllable: true);
            AnimationManager.Current.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));

            var drew = root.ExecuteDrawPassForTesting();
            Assert.True(drew);
            Assert.Equal(UiRedrawReason.AnimationActive, root.LastForceRedrawReasons);
            Assert.Equal(UiRedrawScope.Full, root.LastForceRedrawScope);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void MarkVisualDirty_WithBounds_TracksDirtyRegion()
    {
        var root = CreateRoot();

        try
        {
            root.MarkVisualDirty(new LayoutRect(10f, 20f, 30f, 40f));

            Assert.True(root.IsVisualDirty);
            Assert.False(root.IsFullFrameVisualDirty);
            Assert.Equal(1, root.DirtyVisualRegionCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void MarkVisualDirty_WithIntersectingBounds_MergesRegions()
    {
        var root = CreateRoot();

        try
        {
            root.MarkVisualDirty(new LayoutRect(0f, 0f, 50f, 50f));
            root.MarkVisualDirty(new LayoutRect(40f, 40f, 50f, 50f));

            Assert.False(root.IsFullFrameVisualDirty);
            Assert.Equal(1, root.DirtyVisualRegionCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void MarkVisualDirty_WithoutBounds_UsesFullFrameFallback()
    {
        var root = CreateRoot();

        try
        {
            root.MarkVisualDirty(new LayoutRect(0f, 0f, 10f, 10f));
            Assert.Equal(1, root.DirtyVisualRegionCount);

            root.MarkVisualDirty();

            Assert.True(root.IsFullFrameVisualDirty);
            Assert.Equal(0, root.DirtyVisualRegionCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void MarkVisualDirty_RegionOverflow_FallsBackToFullFrame()
    {
        var root = CreateRoot();

        try
        {
            for (var i = 0; i < 33; i++)
            {
                root.MarkVisualDirty(new LayoutRect(i * 10f, 0f, 5f, 5f));
            }

            Assert.True(root.IsFullFrameVisualDirty);
            Assert.Equal(0, root.DirtyVisualRegionCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void InvalidateVisual_WithArrangedBounds_UsesBoundedDirtyRegion()
    {
        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            panel.Measure(new Vector2(320f, 200f));
            panel.Arrange(new LayoutRect(0f, 0f, 320f, 200f));
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            panel.InvalidateVisual();

            Assert.True(root.IsVisualDirty);
            Assert.False(root.IsFullFrameVisualDirty);
            Assert.True(root.DirtyVisualRegionCount >= 1);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void ParentTransition_InvalidatesPreviousBoundsOnOwningRoot()
    {
        var host = new Panel();
        var child = new Border
        {
            Width = 80f,
            Height = 40f
        };
        host.AddChild(child);

        var root = new UiRoot(host);

        try
        {
            host.Measure(new Vector2(400f, 300f));
            host.Arrange(new LayoutRect(0f, 0f, 400f, 300f));
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            host.RemoveChild(child);

            Assert.True(root.IsVisualDirty);
            Assert.True(root.DirtyVisualRegionCount >= 1 || root.IsFullFrameVisualDirty);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void Update_WhenLayoutCleanAndViewportStable_SkipsLayoutPass()
    {
        var root = CreateRoot();

        try
        {
            root.Update(CreateGameTime(16), new Vector2(800f, 600f));
            Assert.True(root.LastLayoutPassExecuted);

            root.Update(CreateGameTime(32), new Vector2(800f, 600f));
            Assert.False(root.LastLayoutPassExecuted);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void Update_WhenViewportChanges_ExecutesLayoutPass()
    {
        var root = CreateRoot();

        try
        {
            root.Update(CreateGameTime(16), new Vector2(800f, 600f));
            Assert.True(root.LastLayoutPassExecuted);

            root.Update(CreateGameTime(32), new Vector2(1024f, 768f));
            Assert.True(root.LastLayoutPassExecuted);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void Update_WhenLayoutDirty_ExecutesLayoutPass()
    {
        var panel = new Panel();
        var root = new UiRoot(panel);

        try
        {
            root.Update(CreateGameTime(16), new Vector2(800f, 600f));
            Assert.True(root.LastLayoutPassExecuted);

            root.Update(CreateGameTime(32), new Vector2(800f, 600f));
            Assert.False(root.LastLayoutPassExecuted);

            panel.InvalidateMeasure();
            root.Update(CreateGameTime(48), new Vector2(800f, 600f));
            Assert.True(root.LastLayoutPassExecuted);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void InputVisualFlags_ForceRedrawWithRegionScopeAndReasons()
    {
        var root = CreateRoot();

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            InputManager.SetVisualStateChangeFlagsForTests(
                InputManager.InputVisualStateChangeFlags.HoverChanged |
                InputManager.InputVisualStateChangeFlags.FocusChanged |
                InputManager.InputVisualStateChangeFlags.CursorChanged);

            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.Equal(
                UiRedrawReason.HoverChanged | UiRedrawReason.FocusChanged | UiRedrawReason.CursorChanged,
                root.LastForceRedrawReasons);
            Assert.Equal(UiRedrawScope.Region, root.LastForceRedrawScope);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void HoverIdentityTransition_MarksBoundedOldAndNewRegions_WhenBoundsAvailable()
    {
        var host = new Panel
        {
            Width = 400f,
            Height = 240f
        };

        var list = new ListBox
        {
            Width = 300f,
            Height = 180f
        };
        var first = new PassiveListBoxItem { Height = 24f, Content = new Label { Text = "A" } };
        var second = new PassiveListBoxItem { Height = 24f, Content = new Label { Text = "B" } };
        var third = new PassiveListBoxItem { Height = 24f, Content = new Label { Text = "C" } };
        list.Items.Add(first);
        list.Items.Add(second);
        list.Items.Add(third);
        host.AddChild(list);

        var root = new UiRoot(host);
        try
        {
            host.Measure(new Vector2(400f, 240f));
            host.Arrange(new LayoutRect(0f, 0f, 400f, 240f));

            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            var gameTime = CreateGameTime(16);
            var p1 = ProbePoint(first, 6f, 6f);
            var p3 = ProbePoint(third, 6f, 6f);

            InputManager.UpdateForTesting(host, gameTime, p1);
            Assert.True(root.ExecuteDrawPassForTesting());
            InputManager.SetVisualStateChangeFlagsForTests(InputManager.InputVisualStateChangeFlags.None);
            Assert.False(root.ExecuteDrawPassForTesting());

            InputManager.UpdateForTesting(host, gameTime, p3);

            Assert.True(root.IsVisualDirty);
            Assert.True(root.DirtyVisualRegionCount >= 2);
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.True((root.LastForceRedrawReasons & UiRedrawReason.HoverChanged) != 0);
        }
        finally
        {
            root.Shutdown();
        }
    }

    private static UiRoot CreateRoot()
    {
        return new UiRoot(new Panel());
    }

    private static GameTime CreateGameTime(double totalMilliseconds)
    {
        var total = TimeSpan.FromMilliseconds(totalMilliseconds);
        return new GameTime(total, TimeSpan.FromMilliseconds(16));
    }

    private static Vector2 ProbePoint(FrameworkElement element, float dx, float dy)
    {
        var slot = element.LayoutSlot;
        var x = MathF.Max(slot.X + 1f, MathF.Min(slot.X + slot.Width - 1f, slot.X + dx));
        var y = MathF.Max(slot.Y + 1f, MathF.Min(slot.Y + slot.Height - 1f, slot.Y + dy));
        return new Vector2(x, y);
    }

    private sealed class PassiveListBoxItem : ListBoxItem
    {
        protected override void OnMouseEnter(RoutedMouseEventArgs args)
        {
        }

        protected override void OnMouseLeave(RoutedMouseEventArgs args)
        {
        }
    }
}
