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
        var configuration = InkkOopsHostConfiguration.CreateDefault(typeof(Game1).Assembly);

        Assert.Contains("buttons-resize-hover-repro", configuration.ScriptCatalog.ListScripts());
        Assert.True(configuration.ScriptCatalog.TryResolve("sidebar-button-richtextbox", out var script));
        Assert.NotNull(script);
        Assert.Equal("sidebar-button-richtextbox", script!.Name);
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
                Assert.True(File.Exists(Path.Combine(artifacts.DirectoryPath, "commands-custom.log")));
            }

            string recordingDirectory;
            using (var recorder = new InkkOopsInteractionRecorder(root, new Point(320, 240), namingPolicy))
            {
                recordingDirectory = recorder.DirectoryPath;
            }

            Assert.EndsWith("session-custom", recordingDirectory, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(recordingDirectory, "session.json")));
            Assert.True(File.Exists(Path.Combine(recordingDirectory, "builder.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplayPostamblePolicy_IsInjectable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"inkkoops-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var jsonPath = Path.Combine(root, "recording.json");
            File.WriteAllText(
                jsonPath,
                """
                {
                  "actions": [
                    { "Kind": 0, "FrameCount": 2 }
                  ]
                }
                """);

            var script = InkkOopsRecordedSessionLoader.LoadFromJson(
                jsonPath,
                new DefaultInkkOopsArtifactNamingPolicy(),
                new CustomReplayPostamblePolicy());

            var descriptions = script.Commands.Select(static command => command.Describe()).ToArray();
            Assert.Equal(["WaitFrames(2)", "CaptureFrame(custom-tail)"], descriptions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsPipeline_WorksWith_GenericContributor_Only()
    {
        var root = new Canvas { Name = "Root" };
        root.AddChild(new Button { Name = "Child", Content = "Click me" });
        using var host = new InkkOopsTestHost(root);
        var diagnostics = new InkkOopsVisualTreeDiagnostics([new InkkOopsGenericElementDiagnosticsContributor()]);
        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = null,
                FocusedElement = null,
                ArtifactName = "generic"
            });
        var serializer = new DefaultInkkOopsDiagnosticsSerializer();
        var text = serializer.SerializeVisualTree(snapshot);

        Assert.Contains("Canvas#Root hovered=False focused=False", text);
        Assert.Contains("Button#Child hovered=False focused=False", text);
        Assert.DoesNotContain("slot=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsPipeline_Emits_ButtonContributorFacts()
    {
        _ = FrameworkElement.GetTelemetryAndReset();
        _ = Button.GetTelemetryAndReset();

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

        button.Measure(new Vector2(120f, 40f));
        button.Arrange(new LayoutRect(0f, 0f, 120f, 40f));
        _ = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 120f, 40f));
        _ = button.PrepareTextRenderPlanForTests(new LayoutRect(0f, 0f, 120f, 40f));
        button.SetMouseOverFromInput(true);
        button.SetPressedFromInput(true);

        var diagnostics = new InkkOopsVisualTreeDiagnostics(
        [
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsButtonDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = button,
                FocusedElement = null,
                ArtifactName = "button"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("Button#Child", text);
        Assert.Contains("buttonDisplayText=Click me", text);
        Assert.Contains("buttonHasTextLayoutCache=True", text);
        Assert.Contains("buttonIsMouseOver=True", text);
        Assert.Contains("buttonIsPressed=True", text);
        Assert.Contains("buttonRuntimeTextLayoutCacheHits=", text);
        Assert.Contains("buttonTextLayoutCacheHits=", text);
        Assert.Contains("buttonTextRenderPlanCacheHits=", text);
        Assert.Contains("frameworkGlobalMeasureCalls=", text);
        Assert.Contains("frameworkGlobalArrangeCalls=", text);
        Assert.Contains("frameworkMeasureMs=", text);
        Assert.Contains("frameworkStylePropertyChanged=", text);
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
    public void DiagnosticsPipeline_UsesContributorOrder_Deterministically()
    {
        var root = new Canvas { Name = "Root" };
        using var host = new InkkOopsTestHost(root);
        var diagnostics = new InkkOopsVisualTreeDiagnostics(
        [
            new OrderedContributor(20, "late", "2"),
            new OrderedContributor(10, "early", "1")
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = null,
                FocusedElement = null,
                ArtifactName = "ordered"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("Canvas#Root early=1 late=2", text);
    }

    [Fact]
    public void DefaultHostConfiguration_Registers_ButtonContributor()
    {
        var configuration = InkkOopsHostConfiguration.CreateDefault(typeof(Game1).Assembly);

        Assert.Contains(configuration.DiagnosticsContributors, static contributor => contributor is InkkOopsButtonDiagnosticsContributor);
    }

    [Fact]
    public void DefaultHostConfiguration_Registers_GridContributor()
    {
        var configuration = InkkOopsHostConfiguration.CreateDefault(typeof(Game1).Assembly);

        Assert.Contains(configuration.DiagnosticsContributors, static contributor => contributor is InkkOopsGridDiagnosticsContributor);
    }

    [Fact]
    public async Task DiagnosticsPipeline_Includes_GridContributorFacts()
    {
        var root = new Canvas { Name = "Root", Width = 400f, Height = 240f };
        var grid = new Grid { Name = "LayoutGrid", Width = 220f, Height = 120f };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "SharedColumn" });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var child = new Border { Width = 48f, Height = 32f, Name = "Child" };
        Grid.SetColumn(child, 0);
        Grid.SetRow(child, 0);
        grid.AddChild(child);
        root.AddChild(grid);

        using var host = new InkkOopsTestHost(root);
        await host.AdvanceFrameAsync(1);

        var diagnostics = new InkkOopsVisualTreeDiagnostics([
            new InkkOopsGenericElementDiagnosticsContributor(),
            new InkkOopsFrameworkElementDiagnosticsContributor(),
            new InkkOopsGridDiagnosticsContributor()
        ]);

        var snapshot = diagnostics.Capture(
            root,
            new InkkOopsDiagnosticsContext
            {
                UiRoot = host.UiRoot,
                Viewport = host.GetViewportBounds(),
                HoveredElement = null,
                FocusedElement = null,
                ArtifactName = "grid-diagnostics"
            });
        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("Grid#LayoutGrid", text);
        Assert.Contains("gridRuntimeColumnDefinitions=", text);
        Assert.Contains("gridRuntimeSharedScopeAttached=", text);
        Assert.Contains("gridGlobalMeasureCalls=", text);
        Assert.Contains("gridGlobalPrepareMetadataCalls=", text);
        Assert.Contains("gridGlobalApplySharedSizesCalls=", text);
        Assert.Contains("columns=", text);
        Assert.Contains("children=", text);
    }

    [Fact]
    public void DefaultDiagnosticsFilterPolicy_Filters_RecordingFinal_Only()
    {
        var policy = new DefaultInkkOopsDiagnosticsFilterPolicy();

        var finalFilter = policy.CreateFilter("recording-final");
        var otherFilter = policy.CreateFilter("menu-workbench-file-open");

        Assert.True(finalFilter.IsActive);
        Assert.Equal(InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors, finalFilter.NodeRetention);
        Assert.False(otherFilter.IsActive);
    }

    [Fact]
    public void DiagnosticsSerializer_Prunes_To_Filtered_Matches_And_Ancestors()
    {
        var snapshot = new InkkOopsVisualTreeSnapshot
        {
            IsFiltered = true,
            NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
            Nodes =
            [
                new InkkOopsVisualTreeNodeSnapshot
                {
                    Depth = 0,
                    DisplayName = "Canvas#Root",
                    Facts = Array.Empty<KeyValuePair<string, string>>(),
                    MatchedFilter = false
                },
                new InkkOopsVisualTreeNodeSnapshot
                {
                    Depth = 1,
                    DisplayName = "StackPanel#Branch",
                    Facts = Array.Empty<KeyValuePair<string, string>>(),
                    MatchedFilter = false
                },
                new InkkOopsVisualTreeNodeSnapshot
                {
                    Depth = 2,
                    DisplayName = "TextBlock#Hotspot",
                    Facts = [new KeyValuePair<string, string>("measureInvalidations", "3")],
                    MatchedFilter = true
                },
                new InkkOopsVisualTreeNodeSnapshot
                {
                    Depth = 1,
                    DisplayName = "Button#ColdLeaf",
                    Facts = Array.Empty<KeyValuePair<string, string>>(),
                    MatchedFilter = false
                }
            ]
        };

        var text = new DefaultInkkOopsDiagnosticsSerializer().SerializeVisualTree(snapshot);

        Assert.Contains("Canvas#Root", text);
        Assert.Contains("  StackPanel#Branch", text);
        Assert.Contains("    TextBlock#Hotspot measureInvalidations=3", text);
        Assert.DoesNotContain("Button#ColdLeaf", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsBuilder_Filters_Facts_Using_Rules()
    {
        var filter = new InkkOopsDiagnosticsFilter
        {
            NodeRetention = InkkOopsDiagnosticsNodeRetention.MatchedNodesAndAncestors,
            Rules =
            [
                new InkkOopsDiagnosticsFactRule
                {
                    Key = "measureWork",
                    Comparison = InkkOopsDiagnosticsComparison.GreaterThan,
                    Value = 1
                }
            ]
        };

        var builder = new InkkOopsElementDiagnosticsBuilder("Grid#Workbench", "Grid", filter);
        builder.Add("desired", "100,200");
        builder.Add("measureWork", 1);
        builder.Add("measureWork", 3);

        Assert.True(builder.MatchedFilter);
        Assert.Single(builder.Facts);
        Assert.Equal("measureWork", builder.Facts[0].Key);
        Assert.Equal("3", builder.Facts[0].Value);
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

        Assert.EndsWith("InkkSlinger.csproj", target.ProjectPath, StringComparison.Ordinal);
        Assert.True(Path.IsPathFullyQualified(target.ProjectPath));
        Assert.True(Path.IsPathFullyQualified(target.WorkingDirectory));
    }

    private sealed class CustomNamingPolicy : IInkkOopsArtifactNamingPolicy
    {
        public string CreateRunDirectoryName(string scriptName, DateTime timestampUtc) => $"custom-{scriptName}";

        public string CreateRecordingDirectoryName(DateTime timestampUtc) => "session-custom";

        public string GetCommandLogFileName() => "commands-custom.log";

        public string GetResultFileName() => "summary.json";

        public string GetRecordingJsonFileName() => "session.json";

        public string GetRecordedScriptFileName() => "builder.txt";

        public string GetFrameCaptureFileName(string artifactName) => $"frame-{artifactName}.png";

        public string GetTelemetryFileName(string artifactName) => $"telemetry-{artifactName}.txt";

        public string CreateReplayScriptName(string recordingPath) => "custom-replay";

        public string CreateReplayFinalArtifactBaseName(string recordingPath) => "custom-final";

        public string SanitizePathSegment(string value, string fallbackValue) => string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
    }

    private sealed class CustomReplayPostamblePolicy : IInkkOopsReplayPostamblePolicy
    {
        public void Apply(InkkOopsScriptBuilder builder, string recordingPath, IInkkOopsArtifactNamingPolicy namingPolicy)
        {
            builder.CaptureFrame("custom-tail");
        }
    }

    private sealed class OrderedContributor : IInkkOopsDiagnosticsContributor
    {
        private readonly string _key;
        private readonly string _value;

        public OrderedContributor(int order, string key, string value)
        {
            Order = order;
            _key = key;
            _value = value;
        }

        public int Order { get; }

        public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
        {
            builder.Add(_key, _value);
        }
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
