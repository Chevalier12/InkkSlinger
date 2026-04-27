using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class CanvasPanelAttachedPositionTests
{
    [Fact]
    public void Canvas_RightAndBottom_ShouldArrangeChildFromFarEdges()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        Assert.Equal(350f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(240f, child.LayoutSlot.Y, 0.5f);
    }

    [Fact]
    public void Canvas_LeftAndTop_ShouldTakePrecedenceOverRightAndBottom()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetLeft(child, 16f);
        Canvas.SetTop(child, 24f);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        Assert.Equal(16f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(24f, child.LayoutSlot.Y, 0.5f);
    }

    [Fact]
    public void Canvas_RightAndBottom_ShouldContributeToDesiredSizeWhenPrimaryEdgesAreUnset()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas();
        canvas.AddChild(child);
        Canvas.SetRight(child, 30f);
        Canvas.SetBottom(child, 20f);

        canvas.Measure(new Vector2(800f, 600f));

        Assert.Equal(150f, canvas.DesiredSize.X, 0.5f);
        Assert.Equal(60f, canvas.DesiredSize.Y, 0.5f);
    }

    [Fact]
    public void Canvas_LeftAndTop_ChangedAfterInitialLayout_ShouldRearrangeChildOnNextFrame()
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetLeft(child, 16f);
        Canvas.SetTop(child, 24f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        Assert.Equal(16f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(24f, child.LayoutSlot.Y, 0.5f);

        Canvas.SetLeft(child, 80f);
        Canvas.SetTop(child, 90f);

        RunLayout(uiRoot, 500, 300);

        Assert.Equal(80f, child.LayoutSlot.X, 0.5f);
        Assert.Equal(90f, child.LayoutSlot.Y, 0.5f);
    }

    [Theory]
    [InlineData("Left")]
    [InlineData("Top")]
    public void Canvas_PositionChangedAfterInitialLayout_ShouldInvalidateArrangeOnly(string positionProperty)
    {
        var child = new Border
        {
            Width = 120f,
            Height = 40f
        };

        var canvas = new Canvas
        {
            Width = 500f,
            Height = 300f
        };

        canvas.AddChild(child);
        Canvas.SetLeft(child, 16f);
        Canvas.SetTop(child, 24f);

        var uiRoot = new UiRoot(canvas);
        RunLayout(uiRoot, 500, 300);

        _ = Canvas.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        uiRoot.GetTelemetryAndReset();

        if (positionProperty == "Left")
        {
            Canvas.SetLeft(child, 80f);
        }
        else
        {
            Canvas.SetTop(child, 90f);
        }

        RunLayout(uiRoot, 500, 300);

        var canvasTelemetry = Canvas.GetTelemetryAndReset();
        var frameworkTelemetry = FrameworkElement.GetTelemetryAndReset();
        var canvasSnapshot = canvas.GetFrameworkElementSnapshotForDiagnostics();
        var childSnapshot = child.GetFrameworkElementSnapshotForDiagnostics();

        Assert.True(
            canvasTelemetry.MeasureCallCount == 0 && frameworkTelemetry.InvalidateMeasureCallCount == 0,
            $"Changing Canvas.{positionProperty} after layout should be arrange-only. " +
            $"canvasMeasureCalls={canvasTelemetry.MeasureCallCount}, canvasMeasuredChildren={canvasTelemetry.MeasuredChildCount}, " +
            $"canvasArrangeCalls={canvasTelemetry.ArrangeCallCount}, frameworkMeasureCalls={frameworkTelemetry.MeasureCallCount}, " +
            $"frameworkMeasureCachedReuse={frameworkTelemetry.MeasureCachedReuseCount}, frameworkInvalidateMeasureCalls={frameworkTelemetry.InvalidateMeasureCallCount}, " +
            $"frameworkInvalidateArrangeCalls={frameworkTelemetry.InvalidateArrangeCallCount}, " +
            $"canvasMeasureInvalidations={canvasSnapshot.Invalidation.DirectMeasureInvalidationCount + canvasSnapshot.Invalidation.PropagatedMeasureInvalidationCount}, " +
            $"canvasArrangeInvalidations={canvasSnapshot.Invalidation.DirectArrangeInvalidationCount + canvasSnapshot.Invalidation.PropagatedArrangeInvalidationCount}, " +
            $"canvasMeasureSources={canvasSnapshot.Invalidation.TopMeasureInvalidationSources}, " +
            $"canvasArrangeSources={canvasSnapshot.Invalidation.TopArrangeInvalidationSources}, " +
            $"childMeasureSources={childSnapshot.Invalidation.TopMeasureInvalidationSources}, " +
            $"childArrangeSources={childSnapshot.Invalidation.TopArrangeInvalidationSources}.");
    }

    [Fact]
    public void Canvas_LeftAndTopMetadata_CurrentlyMarksPositionChangesAsMeasureInvalidating()
    {
        var leftMetadata = Canvas.LeftProperty.GetMetadata(typeof(Border));
        var topMetadata = Canvas.TopProperty.GetMetadata(typeof(Border));

        Assert.False(leftMetadata.AffectsMeasure);
        Assert.True(leftMetadata.AffectsArrange);
        Assert.False(topMetadata.AffectsMeasure);
        Assert.True(topMetadata.AffectsArrange);
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}