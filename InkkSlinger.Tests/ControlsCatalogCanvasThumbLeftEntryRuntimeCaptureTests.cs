using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogCanvasThumbLeftEntryRuntimeCaptureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ControlsCatalogCanvasThumbLeftEntry_RuntimeScript_CapturesRealAppFailure()
    {
        var repoRoot = FindRepositoryRoot();
        var artifactRoot = Path.Combine(Path.GetTempPath(), $"inkkoops-controls-catalog-canvas-thumb-left-entry-{Guid.NewGuid():N}");
        var traceLogPath = Path.Combine(repoRoot, "artifacts", "diagnostics", "canvas-thumb-catalog-investigation.log");
        Directory.CreateDirectory(artifactRoot);
        if (File.Exists(traceLogPath))
        {
            File.Delete(traceLogPath);
        }

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
                            Quote("controls-catalog-canvas-thumb-left-entry-capture"),
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

            process.StartInfo.Environment["INKKSLINGER_CANVAS_THUMB_DIAGNOSTICS"] = "1";
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            Assert.True(
                process.WaitForExit(180000),
                $"Timed out waiting for controls-catalog-canvas-thumb-left-entry-capture runtime script to complete.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");

            Assert.Equal(0, process.ExitCode);

            var runDirectory = Directory.GetDirectories(artifactRoot).Single();
            var resultPath = Path.Combine(runDirectory, "result.json");
            var commandLogPath = Path.Combine(runDirectory, "commands.log");
            var beforeFramePath = Path.Combine(runDirectory, "canvas-thumb-left-entry-before-drag.png");
            var afterFramePath = Path.Combine(runDirectory, "canvas-thumb-left-entry-after-drag-attempt.png");
            var beforeTelemetryPath = Path.Combine(runDirectory, "canvas-thumb-left-entry-before-drag.txt");
            var afterTelemetryPath = Path.Combine(runDirectory, "canvas-thumb-left-entry-after-drag-attempt.txt");

            Assert.True(File.Exists(resultPath));
            Assert.True(File.Exists(commandLogPath));
            Assert.True(File.Exists(beforeFramePath));
            Assert.True(File.Exists(afterFramePath));
            Assert.True(File.Exists(beforeTelemetryPath));
            Assert.True(File.Exists(afterTelemetryPath));
            Assert.True(File.Exists(traceLogPath));

            var result = JsonSerializer.Deserialize<RuntimeResult>(File.ReadAllText(resultPath), JsonOptions);
            Assert.NotNull(result);
            Assert.Equal("Completed", result!.Status);
            Assert.Equal("controls-catalog-canvas-thumb-left-entry-capture", result.ScriptName);
            Assert.True(string.IsNullOrEmpty(result.FailureMessage));

            var commandLog = File.ReadAllText(commandLogPath);
            Assert.Contains("MovePointer(485, 600)", commandLog, StringComparison.Ordinal);
            Assert.Contains("MovePointer(503, 569)", commandLog, StringComparison.Ordinal);
            Assert.Contains("MovePointer(529, 569)", commandLog, StringComparison.Ordinal);
            Assert.Contains("PointerDown(529, 569)", commandLog, StringComparison.Ordinal);
            Assert.Contains("MovePointer(557, 587)", commandLog, StringComparison.Ordinal);
            Assert.Contains("PointerUp(557, 587)", commandLog, StringComparison.Ordinal);
            Assert.Contains("CaptureFrame(canvas-thumb-left-entry-before-drag)", commandLog, StringComparison.Ordinal);
            Assert.Contains("CaptureFrame(canvas-thumb-left-entry-after-drag-attempt)", commandLog, StringComparison.Ordinal);

            var trace = File.ReadAllText(traceLogPath);
            Assert.Contains("HoveredHostSubtree", trace, StringComparison.Ordinal);
            Assert.Contains("PreciseClickSubtree", trace, StringComparison.Ordinal);
            Assert.Contains("Thumb#CanvasSceneDragThumb", trace, StringComparison.Ordinal);
            Assert.Contains("path=HoveredSubtreeHitTest", trace, StringComparison.Ordinal);
            Assert.DoesNotContain("DragStarted", trace, StringComparison.Ordinal);
            Assert.DoesNotContain("CanvasDrag", trace, StringComparison.Ordinal);
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

        public string FailureMessage { get; set; } = string.Empty;
    }
}