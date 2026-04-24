using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class RedrawSchedulingRegressionTests
{
    public RedrawSchedulingRegressionTests()
    {
        AnimationManager.Current.ResetForTests();
    }

    [Fact]
    public void IdleFrame_KeepsScheduledReasonsNone()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.False(shouldDraw);
        Assert.Equal(UiRedrawReason.None, uiRoot.GetScheduledDrawReasonsForTests());
    }

    [Fact]
    public void ViewportChange_SetsResizeReason()
    {
        var uiRoot = new UiRoot(new Panel());
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1024, 600));

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.Resize) != 0);
    }

    [Fact]
    public void GraphicsDeviceChange_SetsResizeReason()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        var graphicsDevice = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport,
            graphicsDevice);

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.Resize) != 0);
    }

    [Fact]
    public void GraphicsDeviceChange_ReleasesPreviousUiDrawingDeviceCaches()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        var firstDevice = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));
        var secondDevice = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport,
            firstDevice);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        UiDrawing.ConfigureDrawingStateForTests(
            firstDevice,
            new[] { new Rectangle(0, 0, 4, 4) },
            new[] { Matrix.CreateTranslation(8f, 0f, 0f) },
            includePolygonBuffer: true);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport,
            secondDevice);

        var drawingState = UiDrawing.GetDrawingStateInfoForTests(firstDevice);
        Assert.Equal(0, drawingState.ClipCount);
        Assert.Equal(0, drawingState.TransformCount);
        Assert.False(drawingState.HasPolygonBuffer);
    }

    [Fact]
    public void IdleAfterStateReset_EmitsNoReasonsAndSkipsDraw()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.False(shouldDraw);
        Assert.Equal(UiRedrawReason.None, uiRoot.LastShouldDrawReasons);
        Assert.Equal(1, uiRoot.DrawSkippedFrameCount);
    }

    [Fact]
    public void ForcedSurfaceResetDraw_ReclassifiesScheduledSkipAsExecutedResizeDraw()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.False(shouldDraw);
        Assert.Equal(1, uiRoot.DrawExecutedFrameCount);
        Assert.Equal(1, uiRoot.DrawSkippedFrameCount);

        uiRoot.RecordForcedDrawForSurfaceReset();

        Assert.Equal(2, uiRoot.DrawExecutedFrameCount);
        Assert.Equal(0, uiRoot.DrawSkippedFrameCount);
        Assert.Equal(UiRedrawReason.Resize, uiRoot.LastShouldDrawReasons);
        Assert.Equal(UiRedrawReason.Resize, uiRoot.GetScheduledDrawReasonsForTests());
    }


    [Fact]
    public void DrawExecutedCounter_TracksCompletedDraws_NotRepeatedShouldDrawChecks()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);

        Assert.True(uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport));
        Assert.True(uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport));

        Assert.Equal(0, uiRoot.DrawExecutedFrameCount);

        uiRoot.CompleteDrawStateForTests();

        Assert.Equal(1, uiRoot.DrawExecutedFrameCount);
    }

    [Fact]
    public void MeasureArrangeInvalidation_SetsLayoutInvalidatedReason()
    {
        var root = new Border();
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        root.InvalidateMeasure();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.LayoutInvalidated) != 0);
    }

    [Fact]
    public void RenderInvalidation_SetsRenderInvalidatedReason()
    {
        var root = new Border();
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        root.InvalidateVisual();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    [Fact]
    public void CaretBlinkInvalidation_SetsCaretBlinkReason()
    {
        var textBox = new TextBox();
        var uiRoot = new UiRoot(textBox);
        var viewport = new Viewport(0, 0, 800, 600);

        textBox.SetFocusedFromInput(true);
        uiRoot.SetFocusedElementForTests(textBox);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        uiRoot.NotifyInvalidation(UiInvalidationType.Render, textBox);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.CaretBlinkActive) != 0);
    }

    [Fact]
    public void DisablingConditionalDrawScheduling_ForcesRenderInvalidatedReason()
    {
        var uiRoot = new UiRoot(new Panel())
        {
            UseConditionalDrawScheduling = false
        };

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    [Fact]
    public void AlwaysDrawCompatibilityMode_ForcesRenderInvalidatedReason()
    {
        var uiRoot = new UiRoot(new Panel())
        {
            AlwaysDrawCompatibilityMode = true
        };

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    [Fact]
    public void ScheduledReasons_FollowShouldDrawAndClearWithCompleteDrawState()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        Assert.Equal(uiRoot.LastShouldDrawReasons, uiRoot.GetScheduledDrawReasonsForTests());

        uiRoot.CompleteDrawStateForTests();

        Assert.Equal(UiRedrawReason.None, uiRoot.GetScheduledDrawReasonsForTests());
        var snapshot = uiRoot.GetMetricsSnapshot();
        Assert.Equal(uiRoot.LastShouldDrawReasons, snapshot.LastShouldDrawReasons);
    }

    [Fact]
    public void Counters_TrackExecutedAndSkippedFramesAcrossTransitions()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.NotifyInvalidation(UiInvalidationType.Render, null);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.Equal(1, uiRoot.DrawExecutedFrameCount);
        Assert.Equal(1, uiRoot.DrawSkippedFrameCount);
    }

    [Fact]
    public void MultipleReasonSources_CombineIntoSingleReasonMask()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        uiRoot.SetAnimationActiveForTests(true);
        uiRoot.NotifyInvalidation(UiInvalidationType.Measure, null);

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 1024, 600));

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.Resize) != 0);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.LayoutInvalidated) != 0);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.AnimationActive) != 0);
    }

    [Fact]
    public void RenderInvalidationWithNullSource_StillSchedulesRenderReason()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        uiRoot.NotifyInvalidation(UiInvalidationType.Render, null);
        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport);

        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.RenderInvalidated) != 0);
    }

    [Fact]
    public void GraphicsDeviceStableAcrossFrames_DoesNotReAddResizeReasonWhenIdle()
    {
        var uiRoot = new UiRoot(new Panel());
        var viewport = new Viewport(0, 0, 800, 600);
        var graphicsDevice = (GraphicsDevice)RuntimeHelpers.GetUninitializedObject(typeof(GraphicsDevice));

        _ = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)),
            viewport,
            graphicsDevice);
        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();

        var shouldDraw = uiRoot.ShouldDrawThisFrame(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            viewport,
            graphicsDevice);

        Assert.False(shouldDraw);
        Assert.True((uiRoot.LastShouldDrawReasons & UiRedrawReason.Resize) == 0);
    }
}
