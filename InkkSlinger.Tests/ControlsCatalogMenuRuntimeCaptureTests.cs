using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogMenuRuntimeCaptureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ControlsCatalogMenu_RuntimeScript_CapturesFinalFrame()
    {
        var repoRoot = FindRepositoryRoot();
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-controls-catalog-menu-{Guid.NewGuid():N}");
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
                            Quote("controls-catalog-menu-capture"),
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
                $"Timed out waiting for controls-catalog-menu-capture runtime script to complete.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");

            Assert.Equal(
                0,
                process.ExitCode);

            var runDirectory = Directory.GetDirectories(artifactRoot).Single();
            var resultPath = Path.Combine(runDirectory, "result.json");
            var commandLogPath = Path.Combine(runDirectory, "commands.log");
            var framePath = Path.Combine(runDirectory, "menu-workbench-file-open.png");
            var telemetryPath = Path.Combine(runDirectory, "menu-workbench-file-open.txt");

            Assert.True(File.Exists(resultPath));
            Assert.True(File.Exists(commandLogPath));
            Assert.True(File.Exists(framePath));
            Assert.True(File.Exists(telemetryPath));

            var result = JsonSerializer.Deserialize<RuntimeResult>(File.ReadAllText(resultPath), JsonOptions);
            Assert.NotNull(result);
            Assert.Equal("Completed", result!.Status);
            Assert.Equal("controls-catalog-menu-capture", result.ScriptName);
            Assert.Equal(12, result.CommandCount);
            Assert.Equal("None", result.FailureCategory);
            Assert.True(string.IsNullOrEmpty(result.FailureMessage));

            var commandLog = File.ReadAllText(commandLogPath);
            Assert.Contains("ScrollIntoView(Name('CatalogSidebarScrollViewer')", commandLog, StringComparison.Ordinal);
            Assert.Contains("Click(Within(Name('CatalogSidebarScrollViewer'), AutomationName('Menu')[0])", commandLog, StringComparison.Ordinal);
            Assert.Contains("Click(Within(Name('WorkspaceMenuHost'), AutomationName('File')[0])", commandLog, StringComparison.Ordinal);
            Assert.Contains("CaptureFrame(menu-workbench-file-open)", commandLog, StringComparison.Ordinal);
            Assert.Contains("AssertProperty(Name('SelectedControlLabel'), Content, Selected: Menu)", commandLog, StringComparison.Ordinal);

            var telemetry = File.ReadAllText(telemetryPath);
            Assert.False(string.IsNullOrWhiteSpace(telemetry));
            Assert.Contains("timestamp_utc=", telemetry, StringComparison.Ordinal);

            var frameInfo = new FileInfo(framePath);
            Assert.True(frameInfo.Length > 0, "Expected captured frame to be non-empty.");
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

        throw new InvalidOperationException("Could not locate repository root for runtime capture test.");
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