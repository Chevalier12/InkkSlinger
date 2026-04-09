using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class InkkOopsTestScriptUsageTests
{
    private const string SidebarScrollViewerName = "CatalogSidebarScrollViewer";
    private const string ButtonHostName = "ControlButtonsHost";
    private const string CalendarPreviousMonthButtonName = "CalendarPreviousMonthButton";
    private const string CalendarNextMonthButtonName = "CalendarNextMonthButton";
    private const string GridSplitterViewRootName = "GridSplitterViewRootGrid";
    private const string GridSplitterWorkbenchScrollViewerName = "GridSplitterWorkbenchScrollViewer";
    private const string NavigationSplitterName = "NavigationSplitter";

    [Fact]
    public async Task RuntimePlayback_Opens_Real_App_And_Writes_Result()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-replay-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        var recordingPath = Path.Combine(runtimeRoot, "recording.json");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            File.WriteAllText(
                recordingPath,
                """
                {
                  "actions": [
                    { "kind": 0, "frameCount": 2 }
                  ]
                }
                """);

            var runDirectory = await RunRuntimeRecordingAsync(recordingPath, artifactsRoot);
            var resultPath = Path.Combine(runDirectory, "result.json");

            Assert.True(File.Exists(resultPath));

            var resultJson = File.ReadAllText(resultPath);
            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"recording-playback\"", resultJson);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeScenario_Opens_Real_App_From_Test_Assembly()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-scenario-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
                "runtime-test-gridsplitter-scenario",
                artifactsRoot);

            var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));

            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"runtime-test-gridsplitter-scenario\"", resultJson);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeScenario_Opens_App_And_Drags_Navigation_Splitter_Back_And_Forth()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-splitter-drag-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
                "runtime-test-navigation-splitter-drag-scenario",
                artifactsRoot);

            var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
            var actionLogPath = Path.Combine(runDirectory, "action.log");

            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.Contains("\"scriptName\": \"runtime-test-navigation-splitter-drag-scenario\"", resultJson);
            Assert.True(File.Exists(actionLogPath));
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuntimeRun_GridSplitter_Sidebar_Drag_Path_Passes_And_Preserves_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-grid-splitter-sidebar-drag-fps-drop");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
            "runtime-grid-splitter-sidebar-drag-fps-drop-scenario",
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-grid-splitter-sidebar-drag-fps-drop-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains("NavigationSplitter", actionLog);
    }

    [Fact]
    public async Task RuntimeRun_Calendar_Month_Navigation_Path_Passes_And_Preserves_Artifacts()
    {
        var artifactsRoot = CreatePreservedArtifactsRoot("runtime-calendar-month-navigation-fps-drop");
        var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
            "runtime-calendar-month-navigation-fps-drop-scenario",
            artifactsRoot);

        var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
        var actionLogPath = Path.Combine(runDirectory, "action.log");
        var actionLog = File.ReadAllText(actionLogPath);

        Assert.Contains("\"status\": \"Completed\"", resultJson);
        Assert.Contains("\"scriptName\": \"runtime-calendar-month-navigation-fps-drop-scenario\"", resultJson);
        Assert.True(File.Exists(actionLogPath));
        Assert.Contains(CalendarNextMonthButtonName, actionLog);
        Assert.Contains(CalendarPreviousMonthButtonName, actionLog);
    }

    [Fact]
    public async Task RuntimeScenario_ApplicationMainWindowWidth_Changes_Live_App_WindowSize()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-runtime-mainwindow-width-{Guid.NewGuid():N}");
        var artifactsRoot = Path.Combine(runtimeRoot, "artifacts");
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(artifactsRoot);

        try
        {
            var runDirectory = await RunRuntimeScenarioFromTestAssemblyAsync(
                "runtime-test-mainwindow-width-scenario",
                artifactsRoot,
                new Dictionary<string, string>
                {
                    ["INKKSLINGER_TEST_MAINWINDOW_WIDTH"] = "1377"
                });

            var resultJson = File.ReadAllText(Path.Combine(runDirectory, "result.json"));
            var telemetryPath = Path.Combine(runDirectory, "mainwindow-width.txt");
            var telemetry = File.ReadAllText(telemetryPath);

            Assert.Contains("\"status\": \"Completed\"", resultJson);
            Assert.True(File.Exists(telemetryPath));
            Assert.Contains("window_client=1377x", telemetry);
            Assert.Contains("window_backbuffer=1377x", telemetry);
            Assert.Contains("viewport=1377x", telemetry);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAsync(string scriptName, string artifactsRoot)
    {
        return await RunRuntimeScenarioFromTestAssemblyAsync(scriptName, artifactsRoot, environmentVariables: null);
    }

    private static async Task<string> RunRuntimeScenarioFromTestAssemblyAsync(
        string scriptName,
        string artifactsRoot,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");
        var testAssemblyPath = typeof(InkkOopsTestScriptUsageTests).Assembly.Location;
        await RunDotNetProcessAsync(
            repositoryRoot,
            $"run --project \"{projectPath}\" --no-restore -- --inkkoops-script-assembly \"{testAssemblyPath}\" --inkkoops-script \"{scriptName}\" --inkkoops-artifacts \"{artifactsRoot}\"",
            "Runtime scenario launch failed.",
            environmentVariables);

        return Assert.Single(Directory.GetDirectories(artifactsRoot));
    }

    private static async Task<string> RunRuntimeRecordingAsync(string recordingPath, string artifactsRoot)
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repositoryRoot, "InkkSlinger.DemoApp", "InkkSlinger.DemoApp.csproj");

        await RunDotNetProcessAsync(
            repositoryRoot,
            $"run --project \"{projectPath}\" --no-restore -- --inkkoops-recording \"{recordingPath}\" --inkkoops-artifacts \"{artifactsRoot}\"",
            "Runtime replay launch failed.");

        return Assert.Single(Directory.GetDirectories(artifactsRoot));
    }

    private static async Task RunDotNetProcessAsync(string workingDirectory, string arguments, string failurePrefix)
    {
        await RunDotNetProcessAsync(workingDirectory, arguments, failurePrefix, environmentVariables: null);
    }

    private static async Task RunDotNetProcessAsync(
        string workingDirectory,
        string arguments,
        string failurePrefix,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (environmentVariables != null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = Process.Start(startInfo);

        Assert.NotNull(process);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);

        var stdout = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = await process.StandardError.ReadToEndAsync(timeout.Token);

        Assert.True(
            process.ExitCode == 0,
            $"{failurePrefix}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
    }

    private static string CreatePreservedArtifactsRoot(string scenarioSlug)
    {
        var repositoryRoot = FindRepositoryRoot();
        var artifactsRoot = Path.Combine(
            repositoryRoot,
            "artifacts",
            "inkkoops-runtime-runs",
            scenarioSlug,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);
        return artifactsRoot;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "InkkSlinger.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root for the InkkOops runtime launch test.");
    }

    public sealed class RuntimeGridSplitterScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-gridsplitter-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));

            builder
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .Hover(gridSplitterButton, dwellFrames: 1);
        }
    }

    public sealed class RuntimeNavigationSplitterDragScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-navigation-splitter-drag-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));

            builder
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .WaitFrames(10)
                .ScrollIntoView(
                    InkkOopsTargetSelector.Name(GridSplitterWorkbenchScrollViewerName),
                    InkkOopsTargetSelector.Name(NavigationSplitterName),
                    padding: 24f)
                .WaitForInteractive(NavigationSplitterName)
                .Drag(NavigationSplitterName, 140f, 0f)
                .Drag(NavigationSplitterName, -220f, 0f)
                .Drag(NavigationSplitterName, 180f, 0f)
                .Drag(NavigationSplitterName, -120f, 0f);
        }
    }

    public sealed class RuntimeGridSplitterSidebarDragFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-grid-splitter-sidebar-drag-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [10, 11, 12, 13, 14];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var gridSplitterButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("GridSplitter").WithIndex(0));
            var dragMotion = InkkOopsPointerMotion.WithTravelFrames(14, stepDistance: 8f);

            builder
                .ResizeWindow(1600, 960)
                .WaitFrames(6)
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), gridSplitterButton, padding: 12f)
                .WaitForInteractive(gridSplitterButton)
                .Click(gridSplitterButton)
                .WaitForElement(GridSplitterViewRootName)
                .WaitFrames(10)
                .ScrollIntoView(
                    InkkOopsTargetSelector.Name(GridSplitterWorkbenchScrollViewerName),
                    InkkOopsTargetSelector.Name(NavigationSplitterName),
                    padding: 24f)
                .WaitForInteractive(NavigationSplitterName)
                .Drag(NavigationSplitterName, 180f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, -240f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, 220f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, -200f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .Drag(NavigationSplitterName, 160f, 0f, InkkOopsPointerAnchor.Center, MouseButton.Left, dragMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeCalendarMonthNavigationFpsDropScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-calendar-month-navigation-fps-drop-scenario";

        protected override IEnumerable<int>? ActionDiagnosticsIndexes => [7, 8, 9, 10, 11];

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            var calendarButton = InkkOopsTargetSelector.Within(
                InkkOopsTargetSelector.Name(ButtonHostName),
                InkkOopsTargetSelector.AutomationName("Calendar").WithIndex(0));
            var clickMotion = InkkOopsPointerMotion.WithTravelFrames(8, stepDistance: 5f);

            builder
                .ResizeWindow(1600, 960)
                .WaitFrames(6)
                .WaitForElement(SidebarScrollViewerName)
                .ScrollIntoView(InkkOopsTargetSelector.Name(SidebarScrollViewerName), calendarButton, padding: 12f)
                .WaitForInteractive(calendarButton)
                .Click(calendarButton)
                .WaitForInteractive(CalendarNextMonthButtonName)
                .Click(CalendarNextMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(6)
                .Click(CalendarNextMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(6)
                .Click(CalendarPreviousMonthButtonName, InkkOopsPointerAnchor.Center, clickMotion)
                .WaitFrames(12);
        }
    }

    public sealed class RuntimeMainWindowWidthScenario : InkkOopsRuntimeScenario
    {
        public override string Name => "runtime-test-mainwindow-width-scenario";

        protected override void Build(InkkOopsScriptBuilder builder)
        {
            builder
                .WaitFrames(8)
                .DumpTelemetry("mainwindow-width");
        }
    }
}