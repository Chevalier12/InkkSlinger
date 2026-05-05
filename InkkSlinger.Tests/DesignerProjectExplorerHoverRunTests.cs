using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InkkSlinger.Designer;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerProjectExplorerHoverRunTests
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task RecentProject_ProjectExplorerHover_RuntimeRun_CompletesAndWritesEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-run", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-hover-run");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerHoverRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerHoverRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var actionLogPath = Path.Combine(runDirectory, "action.log");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerHoverRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var actionLog = File.ReadAllText(actionLogPath);
            Assert.Contains("RecentProjectsItemsControl", actionLog);
            Assert.Contains("ProjectExplorerTree", actionLog);
            Assert.Contains("pointer over", actionLog);

            Assert.True(File.Exists(Path.Combine(runDirectory, "start-page.png")), "Start page frame capture should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-before-hover.png")), "Workspace pre-hover frame capture should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-hover.png")), "Workspace post-hover frame capture should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "before-hover.txt")), "Pre-hover telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-hover.txt")), "Post-hover telemetry should exist.");

            var afterHoverTelemetry = File.ReadAllText(Path.Combine(runDirectory, "after-hover.txt"));
            Assert.Contains("artifact_name=after-hover", afterHoverTelemetry);
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerHover_Action14Telemetry_WritesImmediateEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-action14", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-hover-action14");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerHoverAction14TelemetryRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerHoverAction14TelemetryRuntimeScenario.ScriptName);
            var actionLog = File.ReadAllText(Path.Combine(runDirectory, "action.log"));
            Assert.Contains("anchor: Offset(80, 158)", actionLog);
            Assert.Contains("before-action14", actionLog);
            Assert.Contains("after-action14-immediate", actionLog);

            Assert.True(File.Exists(Path.Combine(runDirectory, "before-action14.txt")), "Pre-action telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-action14-immediate.txt")), "Immediate post-action telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-action14-one-frame.txt")), "One-frame post-action telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-action14-idle.txt")), "Idle post-action telemetry should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerHover_RepeatedRuntimeRun_CompletesAndWritesEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-repeated", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-hover-repeated");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerRepeatedHoverRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerRepeatedHoverRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var actionLogPath = Path.Combine(runDirectory, "action.log");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerRepeatedHoverRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var actionLog = File.ReadAllText(actionLogPath);
            Assert.True(
                CountOccurrences(actionLog, "MovePointerTo(Name('ProjectExplorerTree'), anchor: Offset(80, 158)") >= 6,
                "Expected repeated hover sweeps to revisit the action-14 row multiple times.");
            Assert.True(
                CountOccurrences(actionLog, "pointer over") >= 16,
                "Expected the repeated sweep to generate many pointer-over actions.");

            Assert.True(File.Exists(Path.Combine(runDirectory, "before-repeated-hover.txt")), "Repeated hover baseline telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-repeated-hover-sweep-1.txt")), "First repeated hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-repeated-hover-sweep-6.txt")), "Last repeated hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-repeated-hover-idle.txt")), "Repeated hover idle telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-repeated-hover.png")), "Repeated hover frame capture should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerHover_ScrolledRepeatedRuntimeRun_CompletesAndWritesEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-scrolled-repeated", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-hover-scrolled-repeated");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerScrolledRepeatedHoverRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerScrolledRepeatedHoverRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var actionLogPath = Path.Combine(runDirectory, "action.log");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerScrolledRepeatedHoverRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var actionLog = File.ReadAllText(actionLogPath);
            var firstScrollIndex = actionLog.IndexOf("Wheel(Name('ProjectExplorerTree'), delta: -720", StringComparison.Ordinal);
            var afterScrollTelemetryIndex = actionLog.IndexOf("after-scroll-before-hover", StringComparison.Ordinal);
            var firstScrolledHoverIndex = actionLog.IndexOf("MovePointerTo(Name('ProjectExplorerTree'), anchor: Offset(80, 14)", StringComparison.Ordinal);
            Assert.True(firstScrollIndex >= 0, "Expected the script to wheel the project explorer down before hover sweeps.");
            Assert.True(afterScrollTelemetryIndex > firstScrollIndex, "Expected after-scroll telemetry after project explorer wheel actions.");
            Assert.True(firstScrolledHoverIndex > afterScrollTelemetryIndex, "Expected hover sweeps only after the after-scroll checkpoint.");
            Assert.True(
                CountOccurrences(actionLog, "Wheel(Name('ProjectExplorerTree'), delta: -720") >= 2,
                "Expected multiple project explorer wheel actions before hovering newly visible rows.");
            Assert.True(
                CountOccurrences(actionLog, "MovePointerTo(Name('ProjectExplorerTree'), anchor: Offset(80, 158)") >= 6,
                "Expected repeated hover sweeps to revisit a later viewport row after scrolling.");
            Assert.True(
                CountOccurrences(actionLog, "pointer over") >= 16,
                "Expected the scrolled repeated sweep to generate many pointer-over actions.");

            Assert.True(File.Exists(Path.Combine(runDirectory, "before-scroll.txt")), "Scrolled hover baseline telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-scroll-before-hover.txt")), "After-scroll telemetry should exist before hover sweeps.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-scrolled-repeated-hover-sweep-1.txt")), "First scrolled repeated hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-scrolled-repeated-hover-sweep-6.txt")), "Last scrolled repeated hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-scrolled-repeated-hover-idle.txt")), "Scrolled repeated hover idle telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-scroll-before-hover.png")), "After-scroll frame capture should exist before hover sweeps.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-scrolled-repeated-hover.png")), "Scrolled repeated hover frame capture should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerHover_PrepareCommitMsgAtTopRuntimeRun_CompletesAndWritesEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-prepare-commit-msg-top", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-hover-prepare-commit-msg-top");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerPrepareCommitScrolledHoverRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerPrepareCommitScrolledHoverRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var actionLogPath = Path.Combine(runDirectory, "action.log");
            var evidencePath = Path.Combine(runDirectory, "prepare-commit-msg-at-top-before-hover.txt");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerPrepareCommitScrolledHoverRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var actionLog = File.ReadAllText(actionLogPath);
            var scrollIndex = actionLog.IndexOf("ScrollTreeViewItemToTop(Name('ProjectExplorerTree'), item: prepare-commit-msg.sample", StringComparison.Ordinal);
            var evidenceTelemetryIndex = actionLog.IndexOf("after-prepare-commit-msg-scroll", StringComparison.Ordinal);
            var firstHoverIndex = actionLog.IndexOf("MovePointerTo(Name('ProjectExplorerTree'), anchor: Offset(80, 14)", StringComparison.Ordinal);
            Assert.True(scrollIndex >= 0, "Expected the script to align prepare-commit-msg.sample at the Project Explorer viewport top before hovering.");
            Assert.True(evidenceTelemetryIndex > scrollIndex, "Expected after-scroll telemetry after prepare-commit-msg.sample alignment.");
            Assert.True(firstHoverIndex > evidenceTelemetryIndex, "Expected hover sweeps only after prepare-commit-msg.sample was aligned and telemetry was written.");
            Assert.True(
                CountOccurrences(actionLog, "MovePointerTo(Name('ProjectExplorerTree'), anchor: Offset(80, 158)") >= 6,
                "Expected repeated hover sweeps to revisit a later viewport row after prepare-commit-msg.sample alignment.");
            Assert.True(
                CountOccurrences(actionLog, "pointer over") >= 16,
                "Expected the prepare-commit-msg top sweep to generate many pointer-over actions.");

            Assert.True(File.Exists(evidencePath), "Prepare-commit-msg viewport evidence should exist before hover sweeps.");
            var evidence = File.ReadAllText(evidencePath);
            Assert.Contains("target_item=prepare-commit-msg.sample", evidence);
            Assert.Contains("target_top_delta=0", evidence);
            Assert.Contains("target_is_near_top=True", evidence);
            Assert.Contains("header=prepare-commit-msg.sample normalized=prepare-commit-msg.sample", evidence);
            Assert.True(File.Exists(Path.Combine(runDirectory, "before-prepare-commit-msg-scroll.txt")), "Prepare-commit-msg baseline telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-prepare-commit-msg-scroll.txt")), "Prepare-commit-msg after-scroll telemetry should exist before hover sweeps.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-prepare-commit-msg-hover-sweep-1.txt")), "First prepare-commit-msg hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-prepare-commit-msg-hover-sweep-6.txt")), "Last prepare-commit-msg hover sweep telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-prepare-commit-msg-hover-idle.txt")), "Prepare-commit-msg hover idle telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-prepare-commit-msg-at-top-before-hover.png")), "Prepare-commit-msg at-top frame capture should exist before hover sweeps.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-prepare-commit-msg-hover.png")), "Prepare-commit-msg post-hover frame capture should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerPrePushClick_RuntimeRun_SelectsPrePushSample()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-pre-push-click", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-pre-push-click");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerPrePushClickRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerPrePushClickRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var evidencePath = Path.Combine(runDirectory, "after-pre-push-click.txt");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerPrePushClickRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            Assert.True(File.Exists(evidencePath), "Pre-push click selection evidence should exist.");
            var evidence = File.ReadAllText(evidencePath);
            Assert.Contains("target_item=pre-push.sample", evidence);
            Assert.Contains("selected_normalized=pre-push.sample", evidence);
            Assert.Contains("selected_matches_target=True", evidence);
            Assert.True(File.Exists(Path.Combine(runDirectory, "pre-push-at-top-before-click.txt")), "Pre-push viewport evidence should exist before click.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-pre-push-click-idle.txt")), "Post-click idle telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-pre-push-click.png")), "Post-click frame capture should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    [Fact]
    public async Task RecentProject_ProjectExplorerClaudeCollapse_RuntimeRun_DoesNotLeaveStaleDescendantRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-claude-collapse", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-project-explorer-claude-collapse");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerProjectExplorerClaudeCollapseRuntimeScenario.ScriptName);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerProjectExplorerClaudeCollapseRuntimeScenario.ScriptName);
            var resultJsonPath = Path.Combine(runDirectory, "result.json");
            var evidencePath = Path.Combine(runDirectory, "after-claude-collapse.txt");
            var resultJson = File.ReadAllText(resultJsonPath);
            using var resultDocument = JsonDocument.Parse(resultJson);
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerProjectExplorerClaudeCollapseRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var actionLog = File.ReadAllText(Path.Combine(runDirectory, "action.log"));
            Assert.Contains("ClickTreeViewItemAndAssertSelected(Name('ProjectExplorerTree'), item: InkkSlinger", actionLog);
            Assert.Contains("CollapseTreeViewItemAndAssertNoStaleDescendants(Name('ProjectExplorerTree'), item: .claude", actionLog);

            Assert.True(File.Exists(evidencePath), ".claude collapse evidence should exist.");
            var evidence = File.ReadAllText(evidencePath);
            Assert.Contains("target_item=.claude", evidence);
            Assert.Contains("target_is_expanded=False", evidence);
            Assert.Contains("stale_descendant_count=0", evidence);
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-project-root-click.txt")), "Project-root selection evidence should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "before-claude-collapse.txt")), "Pre-collapse telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-claude-collapse-idle.txt")), "Post-collapse idle telemetry should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-before-claude-collapse.png")), "Pre-collapse frame capture should exist.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "workspace-after-claude-collapse.png")), "Post-collapse frame capture should exist.");
        }
        finally
        {
            TryDeleteDirectory(tempAppData);
        }
    }

    private static async Task<ProcessRunResult> RunDesignerScenarioAsync(
        string workingDirectory,
        string designerProjectPath,
        string artifactRoot,
        string tempAppData,
        string scriptName)
    {
        Directory.CreateDirectory(artifactRoot);
        var arguments = string.Join(
            ' ',
            "run",
            "--project",
            Quote(designerProjectPath),
            "--configuration",
            "Debug",
            "--no-build",
            "--",
            "--inkkoops-script",
            Quote(scriptName),
            "--inkkoops-artifacts",
            Quote(artifactRoot));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["APPDATA"] = tempAppData;
        startInfo.Environment["INKKSLINGER_INKKOOPS_RUN_KIND"] = "designer-project-explorer-hover";

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        string stdout;
        string stderr;
        try
        {
            using var timeout = new CancellationTokenSource(RunTimeout);
            await process!.WaitForExitAsync(timeout.Token);
            stdout = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            stderr = await process.StandardError.ReadToEndAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process!.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            stdout = await process!.StandardOutput.ReadToEndAsync();
            stderr = await process.StandardError.ReadToEndAsync();
            Assert.Fail(
                $"Designer hover RUN timed out after {RunTimeout.TotalSeconds:0} seconds.{Environment.NewLine}" +
                $"Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            return new ProcessRunResult(-1, stdout, stderr);
        }

        Assert.True(
            process!.ExitCode == 0,
            $"Designer hover RUN failed.{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}" +
            $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");

        return new ProcessRunResult(process.ExitCode, stdout, stderr);
    }

    private static void SeedRecentProject(string tempAppData, string projectPath)
    {
        var recentDirectory = Path.Combine(tempAppData, "InkkSlinger", "Designer");
        Directory.CreateDirectory(recentDirectory);
        var payload = JsonSerializer.Serialize(
            new[]
            {
                new
                {
                    Path = projectPath,
                    DisplayName = "InkkSlinger",
                    LastOpenedAt = DateTimeOffset.UtcNow
                }
            },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(recentDirectory, "recent-projects.json"), payload);
    }

    private static string GetLatestRunDirectory(string artifactRoot, string scriptName)
    {
        var directory = Directory.GetDirectories(artifactRoot)
            .Where(path => Path.GetFileName(path).EndsWith(scriptName, StringComparison.Ordinal))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .FirstOrDefault();

        return directory ?? throw new DirectoryNotFoundException($"No artifact directory found for script '{scriptName}' under '{artifactRoot}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "InkkSlinger.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate InkkSlinger.sln from test assembly base directory.");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
}
