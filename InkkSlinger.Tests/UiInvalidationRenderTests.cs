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

    private static UiRoot CreateRoot()
    {
        return new UiRoot(new Panel());
    }

    private static GameTime CreateGameTime(double totalMilliseconds)
    {
        var total = TimeSpan.FromMilliseconds(totalMilliseconds);
        return new GameTime(total, TimeSpan.FromMilliseconds(16));
    }
}
