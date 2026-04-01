using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogExpanderRuntimeCaptureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ControlsCatalogExpander_RuntimeScript_CapturesCollapseExpandRegressionEvidence()
    {
        var repoRoot = FindRepositoryRoot();
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-controls-catalog-expander-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactRoot);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = string.Join(
                        " ",
                        [
                            "run",
                            "--project",
                            Quote(Path.Combine(repoRoot, "InkkOops.Cli", "InkkOops.Cli.csproj")),
                            "--",
                            "run",
                            "--script",
                            Quote("controls-catalog-expander-capture"),
                            "--launch",
                            "--project",
                            Quote(Path.Combine(repoRoot, "InkkSlinger.csproj")),
                            "--artifacts",
                            Quote(artifactRoot)
                        ]),
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            Assert.True(
                process.WaitForExit(180000),
                $"Timed out waiting for controls-catalog-expander-capture runtime script to complete.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");

            Assert.Equal(0, process.ExitCode);

            var runDirectory = Directory.GetDirectories(artifactRoot).Single();
            var resultPath = Path.Combine(runDirectory, "result.json");
            var commandLogPath = Path.Combine(runDirectory, "commands.log");
            var beforeFramePath = Path.Combine(runDirectory, "expander-release-checklist-before-collapse.png");
            var afterFramePath = Path.Combine(runDirectory, "expander-release-checklist-after-reexpand.png");
            var beforeTelemetryPath = Path.Combine(runDirectory, "expander-release-checklist-before-collapse.txt");
            var afterTelemetryPath = Path.Combine(runDirectory, "expander-release-checklist-after-reexpand.txt");

            Assert.True(File.Exists(resultPath));
            Assert.True(File.Exists(commandLogPath));
            Assert.True(File.Exists(beforeFramePath));
            Assert.True(File.Exists(afterFramePath));
            Assert.True(File.Exists(beforeTelemetryPath));
            Assert.True(File.Exists(afterTelemetryPath));

            var result = JsonSerializer.Deserialize<RuntimeResult>(File.ReadAllText(resultPath), JsonOptions);
            Assert.NotNull(result);
            Assert.Equal("Completed", result!.Status);
            Assert.Equal("controls-catalog-expander-capture", result.ScriptName);
            Assert.True(result.CommandCount >= 18, $"Expected the repro script to execute the full capture sequence, but only {result.CommandCount} commands were recorded.");
            Assert.Equal("None", result.FailureCategory);
            Assert.True(string.IsNullOrEmpty(result.FailureMessage));

            var commandLog = File.ReadAllText(commandLogPath);
            Assert.Contains("MaximizeWindow()", commandLog, StringComparison.Ordinal);
            Assert.Contains("Click(Within(Name('CatalogSidebarScrollViewer'), AutomationName('Expander')[0])", commandLog, StringComparison.Ordinal);
            Assert.Contains("Click(Name('PlaygroundExpander'), anchor: Offset(24, 12))", commandLog, StringComparison.Ordinal);
            Assert.Contains("CaptureFrame(expander-release-checklist-before-collapse)", commandLog, StringComparison.Ordinal);
            Assert.Contains("CaptureFrame(expander-release-checklist-after-reexpand)", commandLog, StringComparison.Ordinal);
            Assert.Contains("AssertProperty(Name('SelectedControlLabel'), Content, Selected: Expander)", commandLog, StringComparison.Ordinal);
            Assert.Contains("AssertProperty(Name('PlaygroundEventCountsText'), Text, Routed events fired from this playground instance: Expanded 1, Collapsed 1.)", commandLog, StringComparison.Ordinal);

            var beforeTelemetry = File.ReadAllText(beforeTelemetryPath);
            var afterTelemetry = File.ReadAllText(afterTelemetryPath);
            Assert.False(string.IsNullOrWhiteSpace(beforeTelemetry));
            Assert.False(string.IsNullOrWhiteSpace(afterTelemetry));

            var expanderMatch = Regex.Match(afterTelemetry, @"Expander#PlaygroundExpander.*actual=(\d+),(\d+)");
            Assert.True(expanderMatch.Success, "Expected the after-reexpand telemetry to include PlaygroundExpander layout details.");
            Assert.True(int.Parse(expanderMatch.Groups[2].Value) > 100, $"Expected PlaygroundExpander to recover a substantial expanded height, but after-reexpand telemetry was: {expanderMatch.Value}");
            Assert.DoesNotContain(
                "text=QA pass: verify keyboard and pointer access around the header hit-target. renderLines=1 renderWidth=0",
                afterTelemetry,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "text=Telemetry pass: watch Expanded and Collapsed counts update without recreating the control. renderLines=1 renderWidth=0",
                afterTelemetry,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "text=Direction pass: switch to Up, Left, or Right to see the header consume a different edge before content is arranged. renderLines=1 renderWidth=0",
                afterTelemetry,
                StringComparison.Ordinal);

            Assert.True(new FileInfo(beforeFramePath).Length > 0, "Expected the pre-collapse frame to be non-empty.");
            Assert.True(new FileInfo(afterFramePath).Length > 0, "Expected the post-reexpand frame to be non-empty.");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "InkkSlinger.sln")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new InvalidOperationException("Could not locate repository root for Expander runtime capture test.");
    }

    private static string Quote(string value)
    {
        return "\"" + value + "\"";
    }

    private sealed class RuntimeResult
    {
        public string Status { get; set; } = string.Empty;

        public string ScriptName { get; set; } = string.Empty;

        public int CommandCount { get; set; }

        public string FailureCategory { get; set; } = string.Empty;

        public string FailureMessage { get; set; } = string.Empty;
    }
}