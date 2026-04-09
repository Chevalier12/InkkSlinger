using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InkkSlinger.UI.Telemetry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class AppStyledButtonDiagnosticsTests
{
    [Fact]
    public void HoverAndClick_AppStyledButton_WritesTelemetryReport()
    {
        var backup = CaptureApplicationResources();
        try
        {
            AnimationManager.Current.ResetForTests();
            AnimationValueSink.ResetTelemetryForTests();
            Freezable.ResetTelemetryForTests();
            Button.ResetTimingForTests();
            DropShadowEffect.ResetTimingForTests();
            UiTextRenderer.ResetTimingForTests();
            TextLayout.ResetMetricsForTests();

            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 640f,
                Height = 240f
            };

            var clickCount = 0;
            var button = new Button
            {
                Name = "AppStyleProbeButton",
                Content = "Hover + Click Probe",
                Width = 220f,
                Height = 52f
            };
            Canvas.SetLeft(button, 210f);
            Canvas.SetTop(button, 94f);
            button.Click += (_, _) => clickCount++;
            host.AddChild(button);

            var uiRoot = new UiRoot(host);
            var viewport = new Viewport(0, 0, 640, 240);

            AdvanceFrame(uiRoot, viewport, 16);
            AdvanceFrame(uiRoot, viewport, 32);

            var chrome = FindDescendant<Border>(button);
            var shadow = Assert.IsType<DropShadowEffect>(chrome.Effect);

            ResetTelemetry(uiRoot);
            var baseline = CapturePhase("baseline", uiRoot, button, shadow, clickCount);

            var center = Center(button.LayoutSlot);

            uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, pointerMoved: true));
            AdvanceFrames(uiRoot, viewport, startMs: 48, frameCount: 14, frameStepMs: 16);
            var hover = CapturePhase("hover", uiRoot, button, shadow, clickCount);

            ResetTelemetry(uiRoot);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftPressed: true));
            AdvanceFrame(uiRoot, viewport, 288);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(center, leftReleased: true));
            AdvanceFrames(uiRoot, viewport, startMs: 304, frameCount: 14, frameStepMs: 16);
            var click = CapturePhase("click", uiRoot, button, shadow, clickCount);

            Assert.True(hover.Animation.BeginStoryboardCallCount > 0 || hover.Animation.ActiveStoryboardCount > 0);
            Assert.True(hover.Freezable.OnChangedCallCount > 0);
            Assert.True(hover.Shadow.BlurRadius > 0f);
            Assert.True(hover.ButtonScaleX > 1f);
            Assert.Equal(1, click.ClickCount);

            var reportPath = WriteReport(baseline, hover, click);
            Assert.True(File.Exists(reportPath), $"Expected diagnostics report at '{reportPath}'.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static PhaseCapture CapturePhase(string name, UiRoot uiRoot, Button button, DropShadowEffect shadow, int clickCount)
    {
        return new PhaseCapture(
            name,
            clickCount,
            button.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1f,
            button.RenderTransform is ScaleTransform scaleY ? scaleY.ScaleY : 1f,
            shadow.BlurRadius,
            shadow.Opacity,
            uiRoot.GetMetricsSnapshot(),
            uiRoot.GetPerformanceTelemetrySnapshotForTests(),
            uiRoot.GetRenderTelemetrySnapshotForTests(),
            uiRoot.GetRenderInvalidationDebugSnapshotForTests(),
            uiRoot.GetPointerMoveTelemetrySnapshotForTests(),
            uiRoot.GetDirtyBoundsEventTraceForTests(),
            AnimationManager.Current.GetTelemetrySnapshotForTests(),
            AnimationValueSink.GetTelemetrySnapshotForTests(),
            Freezable.GetTelemetrySnapshotForTests(),
            Button.GetTimingSnapshotForTests(),
            DropShadowEffect.GetTimingSnapshotForTests(),
            UiTextRenderer.GetTimingSnapshotForTests(),
            TextLayout.GetMetricsSnapshot());
    }

    private static string WriteReport(PhaseCapture baseline, PhaseCapture hover, PhaseCapture click)
    {
        var repoRoot = TestApplicationResources.GetRepositoryRoot();
        var artifactsDir = Path.Combine(repoRoot, "artifacts");
        Directory.CreateDirectory(artifactsDir);
        var path = Path.Combine(artifactsDir, "app-styled-button-diagnostics.txt");

        var builder = new StringBuilder();
        builder.AppendLine("APP_STYLED_BUTTON_DIAGNOSTICS");
        builder.AppendLine($"generated_utc={DateTime.UtcNow:O}");
        builder.AppendLine($"repo_root={repoRoot}");
        builder.AppendLine();
        AppendPhase(builder, baseline);
        AppendPhase(builder, hover);
        AppendPhase(builder, click);

        builder.AppendLine("summary");
        builder.AppendLine($"hover_begin_calls={hover.Animation.BeginStoryboardCallCount}");
        builder.AppendLine($"hover_compose_ms={hover.Animation.ComposeMilliseconds:0.###}");
        builder.AppendLine($"hover_apply_ms={hover.Animation.ComposeApplyMilliseconds:0.###}");
        builder.AppendLine($"hover_clr_sets={hover.AnimationSink.ClrPropertySetValueCount}");
        builder.AppendLine($"hover_clr_set_ms={hover.AnimationSink.ClrPropertySetValueMilliseconds:0.###}");
        builder.AppendLine($"hover_freezable_onchanged_calls={hover.Freezable.OnChangedCallCount}");
        builder.AppendLine($"hover_render_invalidation_source={hover.RenderInvalidation.EffectiveSourceType}#{hover.RenderInvalidation.EffectiveSourceName}");
        builder.AppendLine($"hover_dirty_rects={hover.UiRoot.LastDirtyRectCount}");
        builder.AppendLine($"hover_dirty_area_pct={hover.UiRoot.LastDirtyAreaPercentage:0.###}");
        builder.AppendLine($"click_begin_calls={click.Animation.BeginStoryboardCallCount}");
        builder.AppendLine($"click_compose_ms={click.Animation.ComposeMilliseconds:0.###}");
        builder.AppendLine($"click_apply_ms={click.Animation.ComposeApplyMilliseconds:0.###}");
        builder.AppendLine($"click_clr_sets={click.AnimationSink.ClrPropertySetValueCount}");
        builder.AppendLine($"click_count={click.ClickCount}");

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static void AppendPhase(StringBuilder builder, PhaseCapture phase)
    {
        builder.AppendLine($"[{phase.Name}]");
        builder.AppendLine($"click_count={phase.ClickCount}");
        builder.AppendLine($"button_scale=({phase.ButtonScaleX:0.###},{phase.ButtonScaleY:0.###})");
        builder.AppendLine($"shadow_blur={phase.Shadow.BlurRadius:0.###}");
        builder.AppendLine($"shadow_opacity={phase.Shadow.Opacity:0.###}");
        builder.AppendLine($"ui_root={phase.UiRoot}");
        builder.AppendLine($"performance={phase.Performance}");
        builder.AppendLine($"render={phase.Render}");
        builder.AppendLine($"render_invalidation={phase.RenderInvalidation}");
        builder.AppendLine($"pointer={phase.Pointer}");
        builder.AppendLine($"animation={phase.Animation}");
        builder.AppendLine($"animation_sink={phase.AnimationSink}");
        builder.AppendLine($"freezable={phase.Freezable}");
        builder.AppendLine($"button={phase.Button}");
        builder.AppendLine($"effect={phase.Effect}");
        builder.AppendLine($"text_renderer={phase.TextRenderer}");
        builder.AppendLine($"text_layout={phase.TextLayout}");
        builder.AppendLine("dirty_bounds_trace_begin");
        foreach (var entry in phase.DirtyBoundsTrace)
        {
            builder.AppendLine(entry);
        }

        builder.AppendLine("dirty_bounds_trace_end");
        builder.AppendLine();
    }

    private static void ResetTelemetry(UiRoot uiRoot)
    {
        AnimationManager.Current.ResetTelemetryForTests();
        AnimationValueSink.ResetTelemetryForTests();
        Freezable.ResetTelemetryForTests();
        Button.ResetTimingForTests();
        DropShadowEffect.ResetTimingForTests();
        UiTextRenderer.ResetTimingForTests();
        TextLayout.ResetMetricsForTests();
        uiRoot.ClearDirtyBoundsEventTraceForTests();
    }

    private static void AdvanceFrames(UiRoot uiRoot, Viewport viewport, int startMs, int frameCount, int frameStepMs)
    {
        for (var i = 0; i < frameCount; i++)
        {
            AdvanceFrame(uiRoot, viewport, startMs + (i * frameStepMs));
        }
    }

    private static void AdvanceFrame(UiRoot uiRoot, Viewport viewport, int totalMilliseconds)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(totalMilliseconds), TimeSpan.FromMilliseconds(16)),
            viewport);
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved = false, bool leftPressed = false, bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static Vector2 Center(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static TElement FindDescendant<TElement>(UIElement root)
        where TElement : UIElement
    {
        var pending = new Stack<UIElement>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is TElement typed)
            {
                return typed;
            }

            foreach (var child in current.GetVisualChildren())
            {
                pending.Push(child);
            }
        }

        throw new InvalidOperationException($"Could not find descendant of type '{typeof(TElement).Name}'.");
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadRootAppResources()
    {
        TestApplicationResources.LoadDemoAppResources();
    }

    private sealed record PhaseCapture(
        string Name,
        int ClickCount,
        float ButtonScaleX,
        float ButtonScaleY,
        float ShadowBlurRadius,
        float ShadowOpacity,
        UiRootMetricsSnapshot UiRoot,
        UiRootPerformanceTelemetrySnapshot Performance,
        UiRenderTelemetrySnapshot Render,
        UiRenderInvalidationDebugSnapshot RenderInvalidation,
        UiPointerMoveTelemetrySnapshot Pointer,
        IReadOnlyList<string> DirtyBoundsTrace,
        AnimationTelemetrySnapshot Animation,
        AnimationSinkTelemetrySnapshot AnimationSink,
        FreezableTelemetrySnapshot Freezable,
        ButtonTimingSnapshot Button,
        DropShadowEffectTimingSnapshot Effect,
        UiTextRendererTimingSnapshot TextRenderer,
        TextLayoutMetricsSnapshot TextLayout)
    {
        public DropShadowState Shadow => new(ShadowBlurRadius, ShadowOpacity);
    }

    private readonly record struct DropShadowState(float BlurRadius, float Opacity);
}