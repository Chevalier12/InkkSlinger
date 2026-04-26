using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InkkSlinger.Cli;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsAbstractionTests
{
    [Fact]
    public void DefaultHostConfiguration_UsesReflectionCatalog_ForCurrentHostScripts()
    {
        var configuration = InkkOopsHostConfiguration.CreateDefault(typeof(ControlsCatalogView).Assembly);

        Assert.Contains("controls-catalog-menu-capture", configuration.ScriptCatalog.ListScripts());
        Assert.True(configuration.ScriptCatalog.TryResolve("controls-catalog-sidebar-hover-fps-drop", out var script));
        Assert.NotNull(script);
        Assert.Equal("controls-catalog-sidebar-hover-fps-drop", script!.Name);
    }

    [Fact]
    public void ArtifactNamingPolicy_CanOverride_RunAndRecordingFileNames()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-abstraction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var namingPolicy = new CustomNamingPolicy();

        try
        {
            using (var artifacts = new InkkOopsArtifacts(root, "demo-script", namingPolicy))
            {
                artifacts.WriteResult(new InkkOopsRunResult(InkkOopsRunStatus.Completed, "demo-script", artifacts.DirectoryPath, 0));

                Assert.EndsWith("custom-demo-script", artifacts.DirectoryPath, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(artifacts.DirectoryPath, "summary.json")));
            }

            Assert.True(File.Exists(Path.Combine(root, "custom-demo-script", "action-custom.log")));

            string recordingDirectory;
            using (var recorder = new InkkOopsInteractionRecorder(root, new Point(320, 240), namingPolicy))
            {
                recordingDirectory = recorder.DirectoryPath;
            }

            Assert.EndsWith("session-custom", recordingDirectory, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(recordingDirectory, "session.json")));
            Assert.True(File.Exists(Path.Combine(recordingDirectory, "session.inkkr")));
            Assert.False(File.Exists(Path.Combine(recordingDirectory, "builder.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TelemetryCapture_Writes_Runtime_Summary_Without_VisualTree_Diagnostics()
    {
        var button = new Button
        {
            Name = "Child",
            Content = "Click me",
            Width = 120f,
            Height = 40f,
            Padding = new Thickness(2f),
            BorderThickness = 1f
        };
        var root = new Canvas { Name = "Root" };
        root.AddChild(button);

        using var host = new InkkOopsTestHost(root);
        host.UiRoot.GetTelemetryAndReset();
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Button.GetTelemetryAndReset();
        await host.AdvanceFrameAsync(1);

        button.Measure(new Vector2(120f, 40f));
        button.Arrange(new LayoutRect(0f, 0f, 120f, 40f));
        _ = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 120f, 40f));
        _ = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 120f, 40f));

        var text = await host.CaptureTelemetryAsync("button");

        Assert.Contains("hovered=", text, StringComparison.Ordinal);
        Assert.Contains("focused=", text, StringComparison.Ordinal);
        Assert.Contains("dirty_regions=", text, StringComparison.Ordinal);
        Assert.Contains("uiRootLayoutExecutedFrameCount=", text, StringComparison.Ordinal);
        Assert.Contains("frameworkMeasureCallCount=", text, StringComparison.Ordinal);
        Assert.Contains("buttonRenderCallCount=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("visual_tree_begin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Button#Child", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FrameworkElementTelemetry_Captures_Runtime_And_Aggregate_Data()
    {
        var hostRoot = new Canvas { Name = "HostRoot" };
        using var host = new InkkOopsTestHost(hostRoot);
        var subject = new ProbeFrameworkElement { Name = "Probe" };

        _ = FrameworkElement.GetTelemetryAndReset();

        subject.SetResourceReference(FrameworkElement.WidthProperty, "dynamicWidth");
        subject.Resources["dynamicWidth"] = 120f;
        subject.Measure(new Vector2(200f, 80f));
        subject.Arrange(new LayoutRect(0f, 0f, 200f, 80f));
        subject.UpdateLayout();
        subject.InvalidateMeasure();
        subject.InvalidateArrange();
        subject.InvalidateVisual();
        subject.InvalidateArrangeForDirectLayoutOnly(invalidateRender: false);
        subject.RaiseInitialized();
        subject.RaiseLoaded();
        subject.RaiseLoaded();
        subject.RaiseUnloaded();
        subject.RaiseUnloaded();

        var runtime = subject.GetFrameworkElementSnapshotForDiagnostics();

        Assert.Equal(1, runtime.MeasureCallCount);
        Assert.Equal(1, runtime.ArrangeCallCount);
        Assert.Equal(1, runtime.UpdateLayoutCallCount);
        Assert.Equal(1, runtime.SetResourceReferenceCallCount);
        Assert.True(runtime.UpdateResourceBindingCallCount >= 2);
        Assert.True(runtime.UpdateResourceBindingHitCount >= 1);
        Assert.True(runtime.UpdateResourceBindingMissCount >= 1);
        Assert.True(runtime.InvalidateMeasureCallCount >= 1);
        Assert.True(runtime.InvalidateArrangeCallCount >= 1);
        Assert.True(runtime.InvalidateVisualCallCount >= 1);
        Assert.Equal(1, runtime.InvalidateArrangeDirectLayoutOnlyCallCount);
        Assert.Equal(1, runtime.InvalidateArrangeDirectLayoutOnlyWithoutRenderCount);
        Assert.Equal(1, runtime.RaiseInitializedCallCount);
        Assert.Equal(1, runtime.RaiseLoadedCallCount);
        Assert.Equal(1, runtime.RaiseLoadedNoOpCount);
        Assert.Equal(1, runtime.RaiseUnloadedCallCount);
        Assert.Equal(1, runtime.RaiseUnloadedNoOpCount);
        Assert.True(runtime.MeasureMilliseconds >= 0d);
        Assert.True(runtime.ArrangeMilliseconds >= 0d);

        var aggregate = FrameworkElement.GetTelemetryAndReset();

        Assert.True(aggregate.MeasureCallCount >= 1);
        Assert.True(aggregate.ArrangeCallCount >= 1);
        Assert.True(aggregate.UpdateLayoutCallCount >= 1);
        Assert.True(aggregate.UpdateResourceBindingHitCount >= 1);
        Assert.True(aggregate.UpdateResourceBindingMissCount >= 1);
        Assert.True(aggregate.RaiseLoadedCallCount >= 1);
        Assert.True(aggregate.RaiseLoadedNoOpCount >= 1);
        Assert.True(aggregate.RaiseUnloadedCallCount >= 1);
        Assert.True(aggregate.RaiseUnloadedNoOpCount >= 1);

        var cleared = FrameworkElement.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureCallCount);
        Assert.Equal(0, cleared.ArrangeCallCount);
        Assert.Equal(0, cleared.UpdateLayoutCallCount);
        Assert.Equal(0, cleared.UpdateResourceBindingHitCount);
        Assert.Equal(0, cleared.RaiseLoadedCallCount);
    }


    [Fact]
    public void LaunchTargetResolver_UsesExplicitProjectPath_WhenProvided()
    {
        var resolver = new DefaultInkkOopsLaunchTargetResolver();
        var projectPath = Path.Combine(Environment.CurrentDirectory, "sample", "SampleApp.csproj");
        var target = resolver.Resolve(new Dictionary<string, string>
        {
            ["project"] = projectPath
        });

        Assert.Equal(Path.GetFullPath(projectPath), target.ProjectPath);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(projectPath)), target.WorkingDirectory);
    }

    [Fact]
    public void LaunchTargetResolver_FallsBack_ToRepoDefault()
    {
        var resolver = new DefaultInkkOopsLaunchTargetResolver();
        var target = resolver.Resolve(new Dictionary<string, string>());

        Assert.EndsWith(Path.Combine("InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj"), target.ProjectPath, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(target.ProjectPath));
        Assert.True(Path.IsPathFullyQualified(target.WorkingDirectory));
    }

    private sealed class CustomNamingPolicy : IInkkOopsArtifactNamingPolicy
    {
        public string CreateRunDirectoryName(string scriptName, DateTime timestampUtc) => $"custom-{scriptName}";

        public string CreateRecordingDirectoryName(DateTime timestampUtc) => "session-custom";

        public string GetActionLogFileName() => "action-custom.log";

        public string GetResultFileName() => "summary.json";

        public string GetRecordingJsonFileName() => "session.json";

        public string GetRecordingInkkrFileName() => "session.inkkr";

        public string GetFrameCaptureFileName(string artifactName) => $"frame-{artifactName}.png";

        public string GetTelemetryFileName(string artifactName) => $"telemetry-{artifactName}.txt";

        public string SanitizePathSegment(string value, string fallbackValue) => string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
    }

    private sealed class ProbeFrameworkElement : FrameworkElement
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _ = availableSize;
            return new Vector2(48f, 18f);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            return finalSize;
        }
    }
}
