using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TreeViewExpandHangCaptureTests
{
    private const string WorkerEnabledEnvironmentVariable = "INKKSLINGER_TREEVIEW_EXPAND_HANG_CAPTURE_WORKER";
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task TreeView_ExpandingNestedItem_WorkerProcess_CompletesWithoutHang()
    {
        await RunWorkerAsync(nameof(TreeView_ExpandingNestedItem_Worker), "TreeView expand hang capture worker failed.");
    }

    [Fact]
    public void TreeView_ExpandingNestedItem_Worker()
    {
        if (!IsWorkerEnabled())
        {
            return;
        }

        var host = new Canvas
        {
            Width = 460f,
            Height = 320f
        };

        var treeView = new TreeView
        {
            Width = 320f,
            Height = 260f
        };

        var root = CreateNode("InkkSlinger", isExpanded: true);
        var claude = CreateNode(".claude", isExpanded: true);
        var classTelemetryAuthor = CreateNode("class-telemetry-author", isExpanded: false);
        classTelemetryAuthor.Items.Add(CreateNode("SKILL.md", isExpanded: false));
        classTelemetryAuthor.Items.Add(CreateNode("notes.md", isExpanded: false));
        classTelemetryAuthor.Items.Add(CreateNode("refs", isExpanded: false));
        claude.Items.Add(classTelemetryAuthor);
        claude.Items.Add(CreateNode("inkkoops-diagnostics-contributor-author", isExpanded: false));
        claude.Items.Add(CreateNode("pre-warm-author", isExpanded: false));
        root.Items.Add(claude);

        var git = CreateNode(".git", isExpanded: false);
        git.Items.Add(CreateNode("hooks", isExpanded: false));
        root.Items.Add(git);

        treeView.Items.Add(root);
        host.AddChild(treeView);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        Assert.False(classTelemetryAuthor.IsExpanded);
        classTelemetryAuthor.IsExpanded = true;
        RunLayout(uiRoot);

        Assert.True(classTelemetryAuthor.IsExpanded);
        Assert.True(treeView.GetFrameworkElementSnapshotForDiagnostics().MeasureCallCount > 0);
        Assert.True(treeView.GetControlSnapshotForDiagnostics().GetVisualChildrenCallCount > 0);
    }

    private static async Task RunWorkerAsync(string workerMethodName, string failurePrefix)
    {
        var repositoryRoot = FindRepositoryRoot();
        var testProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Tests", "InkkSlinger.Tests.csproj");
        var workerName = $"{typeof(TreeViewExpandHangCaptureTests).FullName}.{workerMethodName}";
        var environmentVariables = new Dictionary<string, string>
        {
            [WorkerEnabledEnvironmentVariable] = "1"
        };

        await RunDotNetProcessAsync(
            repositoryRoot,
            $"test \"{testProjectPath}\" --no-build --no-restore --filter \"FullyQualifiedName={workerName}\"",
            failurePrefix,
            environmentVariables);
    }

    private static async Task RunDotNetProcessAsync(
        string workingDirectory,
        string arguments,
        string failurePrefix,
        IReadOnlyDictionary<string, string> environmentVariables)
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

        foreach (var pair in environmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        string stdout;
        string stderr;

        try
        {
            using var timeout = new CancellationTokenSource(WorkerTimeout);
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
                $"{failurePrefix}{Environment.NewLine}Process timed out after {WorkerTimeout.TotalSeconds:0} seconds.{Environment.NewLine}" +
                $"Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            return;
        }

        Assert.True(
            process!.ExitCode == 0,
            $"{failurePrefix}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
    }

    private static TreeViewItem CreateNode(string header, bool isExpanded)
    {
        return new TreeViewItem
        {
            Header = header,
            IsExpanded = isExpanded
        };
    }

    private static void RunLayout(UiRoot uiRoot)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 460, 320));
    }

    private static bool IsWorkerEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(WorkerEnabledEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
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
}
