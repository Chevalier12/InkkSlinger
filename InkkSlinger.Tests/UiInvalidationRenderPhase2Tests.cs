using System;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class UiInvalidationRenderPhase2Tests
{
    public UiInvalidationRenderPhase2Tests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
        InputManager.ResetForTests();
        FocusManager.ResetForTests();
        FrameLoopDiagnostics.Reset();
    }

    [Fact]
    public void DirtyRegionInvalidation_TracksRequestedRegion_AndDrawCounters()
    {
        var root = new UiRoot(new Panel());

        try
        {
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.False(root.ExecuteDrawPassForTesting());

            root.MarkVisualDirty(new LayoutRect(16f, 24f, 40f, 30f));

            Assert.Equal(1, root.DirtyVisualRegionCount);
            Assert.False(root.IsFullFrameVisualDirty);
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.Equal(2, root.DrawExecutedFrameCount);
            Assert.Equal(1, root.DrawSkippedFrameCount);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void DirtyRegions_ThatOverlap_AreMerged()
    {
        var root = new UiRoot(new Panel());

        try
        {
            root.MarkVisualDirty(new LayoutRect(0f, 0f, 50f, 50f));
            root.MarkVisualDirty(new LayoutRect(40f, 10f, 50f, 50f));

            Assert.Equal(1, root.DirtyVisualRegionCount);
            Assert.False(root.IsFullFrameVisualDirty);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void DirtyRegionOverflow_FallsBackToFullFrame()
    {
        var root = new UiRoot(new Panel());

        try
        {
            for (var i = 0; i < 64; i++)
            {
                root.MarkVisualDirty(new LayoutRect(i * 3f, 0f, 1f, 1f));
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
    public void LayoutCleanViewportStable_SkipsLayout()
    {
        var root = new UiRoot(new Panel());

        try
        {
            root.Update(CreateGameTime(16), new Vector2(1280f, 720f));
            Assert.True(root.LastLayoutPassExecuted);

            root.Update(CreateGameTime(32), new Vector2(1280f, 720f));
            Assert.False(root.LastLayoutPassExecuted);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void ViewportResize_ForcesLayout_AndFullScopeRedraw()
    {
        var root = new UiRoot(new Panel());

        try
        {
            root.Update(CreateGameTime(16), new Vector2(800f, 600f));
            Assert.True(root.LastLayoutPassExecuted);

            root.Update(CreateGameTime(32), new Vector2(1024f, 768f));
            Assert.True(root.LastLayoutPassExecuted);

            root.MarkViewportResizedForTesting();
            Assert.True(root.ExecuteDrawPassForTesting());
            Assert.True((root.LastForceRedrawReasons & UiRedrawReason.ViewportResized) != 0);
            Assert.Equal(UiRedrawScope.Full, root.LastForceRedrawScope);
        }
        finally
        {
            root.Shutdown();
        }
    }

    [Fact]
    public void Diagnostics_ReportsAnimationHoverAndFocusReasons()
    {
        FrameLoopDiagnostics.Reset();

        var timing1 = new UiRootDrawTiming(
            0d,
            0d,
            0.1d,
            1,
            0,
            0d,
            1,
            100d,
            0.1d,
            0,
            UiRedrawReason.AnimationActive | UiRedrawReason.HoverChanged);

        var timing2 = new UiRootDrawTiming(
            0d,
            0d,
            0.1d,
            2,
            0,
            0d,
            1,
            100d,
            0.1d,
            0,
            UiRedrawReason.FocusChanged);

        FrameLoopDiagnostics.RecordDraw(1, TimeSpan.FromMilliseconds(16), 0.1d, 0d, 0d, 0.1d, 0d, 0d, timing1);
        FrameLoopDiagnostics.RecordDraw(2, TimeSpan.FromMilliseconds(32), 0.1d, 0d, 0d, 0.1d, 0d, 0d, timing2);

        var snapshot = FrameLoopDiagnostics.GetSnapshot();
        Assert.Contains(nameof(UiRedrawReason.AnimationActive), snapshot.TopRedrawReasonsText);
        Assert.Contains(nameof(UiRedrawReason.HoverChanged), snapshot.TopRedrawReasonsText);
        Assert.Contains(nameof(UiRedrawReason.FocusChanged), snapshot.TopRedrawReasonsText);
    }

    private static GameTime CreateGameTime(double totalMilliseconds)
    {
        var total = TimeSpan.FromMilliseconds(totalMilliseconds);
        return new GameTime(total, TimeSpan.FromMilliseconds(16));
    }
}
