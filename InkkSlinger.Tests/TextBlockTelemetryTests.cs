using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBlockTelemetryTests
{
    [Fact]
    public void GetTelemetryAndReset_ReportsMeasureCacheAndInvalidationPaths()
    {
        _ = TextBlock.GetTelemetryAndReset();

        var textBlock = new TextBlock
        {
            Text = "Left top alignment keeps a wrapped label reusing the previous width until the layout budget changes.",
            TextWrapping = TextWrapping.Wrap,
            Width = 220f
        };

        textBlock.Measure(new Vector2(220f, 600f));
        textBlock.Arrange(new LayoutRect(0f, 0f, 220f, 600f));
        textBlock.PrimeLayoutCacheForTests(220f);
        textBlock.PrimeLayoutCacheForTests(220f);
        textBlock.Measure(new Vector2(120f, 600f));
        textBlock.Arrange(new LayoutRect(0f, 0f, 120f, 600f));
        textBlock.Text = "Changed wrapped text keeps the desired-size optimization honest while still invalidating the cached layout.";
        textBlock.FontSize = 20f;
        textBlock.TextWrapping = TextWrapping.NoWrap;
        _ = textBlock.HasAvailableIndependentDesiredSizeForUniformGrid();
        textBlock.Measure(new Vector2(400f, 600f));

        var runtime = textBlock.GetTextBlockSnapshotForDiagnostics();

        Assert.True(runtime.MeasureOverrideCallCount > 0);
        Assert.True(runtime.ResolveLayoutCallCount > 0);
        Assert.True(runtime.ResolveLayoutCacheHitCount > 0);
        Assert.True(runtime.ResolveLayoutCacheMissCount > 0);
        Assert.True(runtime.ResolveIntrinsicNoWrapTextSizeCallCount > 0);
        Assert.True(runtime.CanUseIntrinsicMeasureCallCount > 0);
        Assert.True(runtime.TryMeasureDesiredSizeForTextChangeCallCount > 0);
        Assert.True(runtime.TextPropertyChangeCount > 0);
        Assert.True(runtime.LayoutAffectingPropertyChangeCount > 0);
        Assert.True(runtime.LayoutCacheInvalidationCount > 0);
        Assert.True(runtime.IntrinsicMeasureInvalidationCount > 0);
        Assert.True(runtime.HasAvailableIndependentDesiredSizeForUniformGridCallCount > 0);

        var telemetry = TextBlock.GetTelemetryAndReset();

        Assert.True(telemetry.MeasureOverrideCallCount > 0);
        Assert.True(telemetry.ResolveLayoutCallCount > 0);
        Assert.True(telemetry.ResolveLayoutCacheHitCount > 0);
        Assert.True(telemetry.ResolveLayoutCacheMissCount > 0);
        Assert.True(telemetry.ResolveIntrinsicNoWrapTextSizeCallCount > 0);
        Assert.True(telemetry.TextPropertyChangeCount > 0);
        Assert.True(telemetry.LayoutAffectingPropertyChangeCount > 0);
        Assert.True(telemetry.LayoutCacheInvalidationCount > 0);
        Assert.True(telemetry.IntrinsicMeasureInvalidationCount > 0);
        Assert.True(telemetry.HasAvailableIndependentDesiredSizeForUniformGridCallCount > 0);

        var cleared = TextBlock.GetTelemetryAndReset();

        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.ResolveLayoutCallCount);
        Assert.Equal(0, cleared.TextPropertyChangeCount);
        Assert.Equal(0, cleared.LayoutCacheInvalidationCount);
    }

    [Fact]
    public async Task DiagnosticsPipeline_Includes_TextBlockContributorFacts()
    {
        _ = TextBlock.GetTelemetryAndReset();

        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var textBlock = new TextBlock
        {
            Name = "Probe",
            Width = 180f,
            Text = "Telemetry contributor output should include both runtime and aggregate TextBlock facts.",
            TextWrapping = TextWrapping.Wrap
        };
        root.AddChild(textBlock);

        using var host = new InkkOopsTestHost(root);
        await host.AdvanceFrameAsync(1);

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsTextBlockDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = null,
                FocusedElement = null,
                ArtifactName = "textblock-diagnostics"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("TextBlock#Probe", text);
        Assert.Contains("hasLayoutCache=", text);
        Assert.Contains("runtimeResolveLayoutMs=", text);
        Assert.Contains("runtimeCanReuseMeasureCalls=", text);
        Assert.Contains("telemetryMeasureOverrideCalls=", text);
        Assert.Contains("telemetryResolveLayoutCalls=", text);
        Assert.Contains("telemetryRenderCalls=", text);
        Assert.Contains("text=Telemetry contributor output should include both runtime and aggregate TextBlock facts.", text);
    }
}