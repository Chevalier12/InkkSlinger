using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerRootTemplatePickerRuntimeTests
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task RuntimeRun_Designer_RootTemplateComboBox_OpenDropdown_FrameBaseline_Passes_And_Preserves_Artifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", "inkkoops", DesignerRootTemplateComboBoxOpenFrameBaselineScenario.ScriptName);

        var run = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            DesignerRootTemplateComboBoxOpenFrameBaselineScenario.ScriptName,
            projectPath,
            artifactsRoot);

        Assert.Equal(nameof(InkkOopsRunStatus.Completed), run.Status);
        Assert.True(Directory.Exists(run.ArtifactDirectory), $"Expected runtime artifacts under '{run.ArtifactDirectory}'.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "result.json")), "Expected result.json to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "action.log")), "Expected action.log to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "root-template-dropdown-open.png")), "Expected the dropdown-open frame capture artifact to be written.");

        var actionLog = await File.ReadAllTextAsync(Path.Combine(run.ArtifactDirectory, "action.log"));
        Assert.Contains("RootTemplateComboBox", actionLog, StringComparison.Ordinal);
        Assert.Contains("action[3]", actionLog, StringComparison.Ordinal);
        Assert.Contains("action[4] wait frames", actionLog, StringComparison.Ordinal);
    }

    private static async Task<RuntimeRunArtifacts> RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
        string scriptName,
        string projectPath,
        string artifactsRoot)
    {
        Directory.CreateDirectory(artifactsRoot);
        var existingRunDirectories = new HashSet<string>(
            Directory.GetDirectories(artifactsRoot),
            StringComparer.OrdinalIgnoreCase);

        var testAssemblyPath = typeof(DesignerRootTemplatePickerRuntimeTests).Assembly.Location;
        var repositoryRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--inkkoops-script");
        startInfo.ArgumentList.Add(scriptName);
        startInfo.ArgumentList.Add("--inkkoops-script-assembly");
        startInfo.ArgumentList.Add(testAssemblyPath);
        startInfo.ArgumentList.Add("--inkkoops-artifacts");
        startInfo.ArgumentList.Add(artifactsRoot);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        using var timeout = new CancellationTokenSource(RunTimeout);
        var stdoutTask = process!.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            var timedOutStdout = await ReadTaskSafelyAsync(stdoutTask).ConfigureAwait(false);
            var timedOutStderr = await ReadTaskSafelyAsync(stderrTask).ConfigureAwait(false);
            Assert.Fail(
                $"Runtime RUN timed out after {RunTimeout.TotalSeconds:0} seconds.{Environment.NewLine}" +
                $"Script: {scriptName}{Environment.NewLine}" +
                $"Project: {projectPath}{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{timedOutStdout}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{timedOutStderr}");
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var artifactDirectory = ResolveRunArtifactDirectory(artifactsRoot, existingRunDirectories);
        var resultPath = Path.Combine(artifactDirectory, "result.json");

        Assert.True(
            File.Exists(resultPath),
            $"Expected runtime RUN to write result.json under '{artifactDirectory}'. ExitCode={process.ExitCode}.{Environment.NewLine}" +
            $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}" +
            $"STDERR:{Environment.NewLine}{stderr}");

        var resultText = await File.ReadAllTextAsync(resultPath, timeout.Token).ConfigureAwait(false);
        var status = ReadJsonStringProperty(resultText, "status");
        var resultArtifactDirectory = ReadJsonStringProperty(resultText, "artifactDirectory");
        var failureMessage = ReadJsonStringProperty(resultText, "failureMessage");

        Assert.True(
            string.Equals(status, nameof(InkkOopsRunStatus.Completed), StringComparison.Ordinal),
            $"Runtime RUN failed with status '{status}'. ExitCode={process.ExitCode}.{Environment.NewLine}" +
            $"Artifacts: {resultArtifactDirectory}{Environment.NewLine}" +
            $"Failure: {failureMessage}{Environment.NewLine}" +
            $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}" +
            $"STDERR:{Environment.NewLine}{stderr}");

        return new RuntimeRunArtifacts(status, resultArtifactDirectory, process.ExitCode, stdout, stderr);
    }

    private static string ReadJsonStringProperty(string json, string propertyName)
    {
        var propertyToken = $"\"{propertyName}\"";
        var propertyIndex = json.IndexOf(propertyToken, StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            return string.Empty;
        }

        var colonIndex = json.IndexOf(':', propertyIndex + propertyToken.Length);
        if (colonIndex < 0)
        {
            return string.Empty;
        }

        var valueStart = colonIndex + 1;
        while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= json.Length || json[valueStart] != '"')
        {
            return string.Empty;
        }

        valueStart++;
        var valueEnd = valueStart;
        while (valueEnd < json.Length)
        {
            if (json[valueEnd] == '"' && json[valueEnd - 1] != '\\')
            {
                break;
            }

            valueEnd++;
        }

        if (valueEnd >= json.Length)
        {
            return string.Empty;
        }

        return json[valueStart..valueEnd]
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string ResolveRunArtifactDirectory(string artifactsRoot, HashSet<string> existingRunDirectories)
    {
        var createdDirectories = Directory.GetDirectories(artifactsRoot)
            .Where(directory => !existingRunDirectories.Contains(directory))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .ToArray();

        if (createdDirectories.Length > 0)
        {
            return createdDirectories[0];
        }

        var fallback = Directory.GetDirectories(artifactsRoot)
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .FirstOrDefault();

        return fallback ?? artifactsRoot;
    }

    private static async Task<string> ReadTaskSafelyAsync(Task<string> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "InkkSlinger.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test assembly base directory.");
    }

    private sealed record RuntimeRunArtifacts(
        string Status,
        string ArtifactDirectory,
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

public sealed class DesignerRootTemplateComboBoxOpenFrameBaselineScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-root-template-combobox-open-frame-baseline";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        return new InkkOopsScriptBuilder(ScriptName, [3, 4, 5, 6, 7, 8])
            .ResizeWindow(1440, 900)
            .WaitForInteractive("RootTemplateComboBox", 240)
            .Hover("RootTemplateComboBox", dwellFrames: 1)
            .Click(
                "RootTemplateComboBox",
                InkkOopsPointerAnchor.Center,
                InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitFrames(1)
            .WaitFrames(1)
            .WaitFrames(1)
            .WaitFrames(1)
            .WaitFrames(1)
            .WaitFrames(1)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", true)
            .CaptureFrame("root-template-dropdown-open")
            .Build();
    }
}