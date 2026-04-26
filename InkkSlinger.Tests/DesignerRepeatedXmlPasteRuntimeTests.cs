using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerRepeatedXmlPasteRuntimeTests
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(4);

    [Fact]
    public async Task RuntimeRun_Designer_SourceAndAppResources_RepeatedXmlPaste_Preserves_Artifacts()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", "inkkoops", DesignerSourceAndAppResourcesRepeatedXmlPasteScenario.ScriptName);

        var run = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            DesignerSourceAndAppResourcesRepeatedXmlPasteScenario.ScriptName,
            projectPath,
            artifactsRoot);

        Assert.Equal(nameof(InkkOopsRunStatus.Completed), run.Status);
        Assert.True(Directory.Exists(run.ArtifactDirectory), $"Expected runtime artifacts under '{run.ArtifactDirectory}'.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "result.json")), "Expected result.json to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "action.log")), "Expected action.log to be written.");

        var expectedTelemetryArtifacts = new[]
        {
            "source-paste-1.txt",
            "source-paste-2.txt",
            "source-paste-3.txt",
            "app-resources-paste-1.txt",
            "app-resources-paste-2.txt",
            "app-resources-paste-3.txt"
        };

        foreach (var artifactName in expectedTelemetryArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, artifactName)), $"Expected telemetry artifact '{artifactName}' to be written.");
        }

        var actionLog = await File.ReadAllTextAsync(Path.Combine(run.ArtifactDirectory, "action.log"));
        Assert.Contains("SourceEditorPane", actionLog, StringComparison.Ordinal);
        Assert.Contains("AppResourcesEditorPane", actionLog, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(actionLog, "set clipboard text"));
        Assert.Equal(6, CountOccurrences(actionLog, "KeyDown(V)"));
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

        var testAssemblyPath = typeof(DesignerRepeatedXmlPasteRuntimeTests).Assembly.Location;
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

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex < text.Length)
        {
            var index = text.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            startIndex = index + value.Length;
        }

        return count;
    }

    private sealed record RuntimeRunArtifacts(
        string Status,
        string ArtifactDirectory,
        int ExitCode,
        string StandardOutput,
        string StandardError);
}

public sealed class DesignerSourceAndAppResourcesRepeatedXmlPasteScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-source-appresources-repeated-xml-paste";

    private const string PastePayload = """
<UserControl xmlns="urn:inkkslinger-ui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="#111827">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="18" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <TextBlock Text="Designer Preview"
                   Foreground="#E7EDF5"
                   FontSize="22"
                   FontWeight="SemiBold" />

        <Border Grid.Row="2"
                Background="#182230"
                BorderBrush="#35506B"
                BorderThickness="1"
                CornerRadius="12"
                Padding="18">
            <StackPanel>
                <TextBlock Text="Manual refresh is enabled."
                           Foreground="#E7EDF5"
                           FontSize="18" />
                <TextBlock Text="Edit the XML below, then press F5 or the toolbar button."
                           Foreground="#8AA3B8"
                           Margin="0,6,0,12" />
                <Button x:Name="PreviewButton"
                        Content="Preview Action"
                        Width="180"
                        Height="40"
                        Background="#1F8EFA"
                        BorderBrush="#56A7F7"
                        BorderThickness="1" />
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
""";

    private static readonly InkkOopsTargetSelector SourceEditor = InkkOopsTargetSelector.DescendantOf(
        InkkOopsTargetSelector.Name("SourceEditorPane"),
        InkkOopsTargetSelector.Name("SourceEditor"));

    private static readonly InkkOopsTargetSelector AppResourcesEditor = InkkOopsTargetSelector.DescendantOf(
        InkkOopsTargetSelector.Name("AppResourcesEditorPane"),
        InkkOopsTargetSelector.Name("SourceEditor"));

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var builder = new InkkOopsScriptBuilder(ScriptName)
            .ResizeWindow(1440, 900)
            .WaitForInteractive(SourceEditor, 240)
            .Click(SourceEditor)
            .SetClipboardText(PastePayload);

        Paste(builder, "source-paste-1");
        Paste(builder, "source-paste-2");
        Paste(builder, "source-paste-3");

        ClickAppResourcesTabHeader(builder)
            .WaitForInteractive(AppResourcesEditor, 240)
            .Click(AppResourcesEditor)
            .SetClipboardText(PastePayload);

        Paste(builder, "app-resources-paste-1");
        Paste(builder, "app-resources-paste-2");
        Paste(builder, "app-resources-paste-3");

        return builder
            .CaptureFrame("after-repeated-xml-paste")
            .Build();
    }

    private static void Paste(InkkOopsScriptBuilder builder, string telemetryArtifactName)
    {
        builder
            .KeyDown(Keys.LeftControl)
            .KeyDown(Keys.V)
            .KeyUp(Keys.V)
            .KeyUp(Keys.LeftControl)
            .WaitFrames(3)
            .DumpTelemetry(telemetryArtifactName);
    }

    private static InkkOopsScriptBuilder ClickAppResourcesTabHeader(InkkOopsScriptBuilder builder)
    {
        var point = new System.Numerics.Vector2(150f, 698f);
        return builder
            .MovePointer(point)
            .PointerDown(point)
            .PointerUp(point);
    }
}