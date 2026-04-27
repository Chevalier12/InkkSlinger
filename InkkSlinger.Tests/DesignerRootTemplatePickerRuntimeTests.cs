using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

    [Fact]
    public async Task RuntimeRun_Designer_RootTemplateComboBox_BottomRow_ShouldFullyRevealVirtualizingStackPanelItem()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", "inkkoops", DesignerRootTemplateComboBoxBottomRowVisibilityScenario.ScriptName);

        var run = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            DesignerRootTemplateComboBoxBottomRowVisibilityScenario.ScriptName,
            projectPath,
            artifactsRoot);

        Assert.Equal(nameof(InkkOopsRunStatus.Completed), run.Status);
        Assert.True(Directory.Exists(run.ArtifactDirectory), $"Expected runtime artifacts under '{run.ArtifactDirectory}'.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "result.json")), "Expected result.json to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "action.log")), "Expected action.log to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "root-template-dropdown-bottom.png")), "Expected the bottom-row frame capture artifact to be written.");
    }

    [Fact]
    public async Task RuntimeRun_Designer_RootTemplateComboBox_ReopenAfterSelectingBottomItem_ShouldPaintVisibleTextBeforeAnyScroll()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", "inkkoops", DesignerRootTemplateComboBoxReopenAfterBottomSelectionTextPaintScenario.ScriptName);

        var run = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            DesignerRootTemplateComboBoxReopenAfterBottomSelectionTextPaintScenario.ScriptName,
            projectPath,
            artifactsRoot);

        Assert.Equal(nameof(InkkOopsRunStatus.Completed), run.Status);
        Assert.True(Directory.Exists(run.ArtifactDirectory), $"Expected runtime artifacts under '{run.ArtifactDirectory}'.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "result.json")), "Expected result.json to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "action.log")), "Expected action.log to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "root-template-reopen-selected-bottom-current-frame-text-sample.txt")), "Expected the current-frame text sample artifact to be written.");
    }

    [Fact]
    public async Task RuntimeRun_Designer_RootTemplateComboBox_ScrollBarThumbDrag_ShouldNotJitter()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
        var artifactsRoot = Path.Combine(repoRoot, "artifacts", "inkkoops", DesignerRootTemplateComboBoxScrollBarThumbDragJitterScenario.ScriptName);

        var run = await RunRuntimeScenarioFromTestAssemblyAllowCompletedArtifactsAsync(
            DesignerRootTemplateComboBoxScrollBarThumbDragJitterScenario.ScriptName,
            projectPath,
            artifactsRoot);

        Assert.Equal(nameof(InkkOopsRunStatus.Completed), run.Status);
        Assert.True(Directory.Exists(run.ArtifactDirectory), $"Expected runtime artifacts under '{run.ArtifactDirectory}'.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "result.json")), "Expected result.json to be written.");
        Assert.True(File.Exists(Path.Combine(run.ArtifactDirectory, "action.log")), "Expected action.log to be written.");
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
        return new InkkOopsScriptBuilder(ScriptName)
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

public sealed class DesignerRootTemplateComboBoxBottomRowVisibilityScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-root-template-combobox-bottom-row-visibility";
    private const string BottomItemText = "VirtualizingStackPanel";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        return new InkkOopsScriptBuilder(ScriptName)
            .ResizeWindow(1440, 900)
            .WaitForInteractive("RootTemplateComboBox", 240)
            .Hover("RootTemplateComboBox", dwellFrames: 1)
            .Click(
                "RootTemplateComboBox",
                InkkOopsPointerAnchor.Center,
                InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitFrames(2)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", true)
            .Add(new ScrollRootTemplateComboBoxDropDownToBottomItemCommand(BottomItemText))
            .CaptureFrame("root-template-dropdown-bottom")
            .Add(new AssertRootTemplateComboBoxDropDownItemFullyVisibleCommand(BottomItemText))
            .Build();
    }
}

public sealed class DesignerRootTemplateComboBoxReopenAfterBottomSelectionTextPaintScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-root-template-combobox-reopen-after-bottom-selection-text-paint";
    private const string BottomItemText = "VirtualizingStackPanel";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        return new InkkOopsScriptBuilder(ScriptName)
            .ResizeWindow(1440, 900)
            .WaitForInteractive("RootTemplateComboBox", 240)
            .Hover("RootTemplateComboBox", dwellFrames: 1)
            .Click(
                "RootTemplateComboBox",
                InkkOopsPointerAnchor.Center,
                InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitFrames(2)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", true)
            .Add(new ScrollRootTemplateComboBoxDropDownToBottomItemCommand(BottomItemText))
            .Add(new AssertRootTemplateComboBoxDropDownItemFullyVisibleCommand(BottomItemText))
            .Add(new ClickRootTemplateComboBoxDropDownItemCommand(BottomItemText))
            .WaitFrames(2)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", false)
            .Click(
                "RootTemplateComboBox",
                InkkOopsPointerAnchor.Center,
                InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitFrames(2)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", true)
            .Add(new AssertRootTemplateComboBoxFirstVisibleTextPaintedInCurrentFrameCommand(
                "root-template-reopen-selected-bottom-current-frame-text-sample"))
            .Build();
    }
}

public sealed class DesignerRootTemplateComboBoxScrollBarThumbDragJitterScenario : IInkkOopsScriptDefinition
{
    public const string ScriptName = "designer-root-template-combobox-scrollbar-thumb-drag-jitter";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        return new InkkOopsScriptBuilder(ScriptName)
            .ResizeWindow(1440, 900)
            .WaitForInteractive("RootTemplateComboBox", 240)
            .Hover("RootTemplateComboBox", dwellFrames: 1)
            .Click(
                "RootTemplateComboBox",
                InkkOopsPointerAnchor.Center,
                InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitFrames(2)
            .AssertProperty("RootTemplateComboBox", "IsDropDownOpen", true)
            .Add(new AssertRootTemplateComboBoxDropDownThumbDragStableCommand())
            .Build();
    }
}

file sealed class ClickRootTemplateComboBoxDropDownItemCommand : IInkkOopsCommand
{
    private readonly string _itemText;

    public ClickRootTemplateComboBoxDropDownItemCommand(string itemText)
    {
        _itemText = itemText ?? throw new ArgumentNullException(nameof(itemText));
    }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"ClickRootTemplateComboBoxDropDownItem('{_itemText}')";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var point = await session.QueryOnUiThreadAsync(() => ResolveItemCenter(session, _itemText), cancellationToken).ConfigureAwait(false);
        await session.MovePointerAsync(point, InkkOopsPointerMotion.WithTravelFrames(2), cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        await session.ReleasePointerAsync(point, MouseButton.Left, cancellationToken).ConfigureAwait(false);
    }

    private static System.Numerics.Vector2 ResolveItemCenter(InkkOopsSession session, string itemText)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, itemText);
        var textBlock = RootTemplateComboBoxDropDownTestHelpers.FindBottomMostTextBlock(listBox, itemText) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, $"Could not find realized TextBlock text '{itemText}'.");
        var item = RootTemplateComboBoxDropDownTestHelpers.FindAncestor<ListBoxItem>(textBlock) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"TextBlock '{itemText}' has no ListBoxItem ancestor.");
        if (!item.TryGetRenderBoundsInRootSpace(out var itemBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"ListBoxItem '{itemText}' does not expose render bounds.");
        }

        return new System.Numerics.Vector2(itemBounds.X + (itemBounds.Width / 2f), itemBounds.Y + (itemBounds.Height / 2f));
    }
}

file sealed class AssertRootTemplateComboBoxFirstVisibleTextPaintedInCurrentFrameCommand : IInkkOopsCommand
{
    private const string ProbeItemText = "VirtualizingStackPanel";
    private const int MinimumBrightPixels = 6;
    private readonly string _artifactName;

    public AssertRootTemplateComboBoxFirstVisibleTextPaintedInCurrentFrameCommand(string artifactName)
    {
        _artifactName = string.IsNullOrWhiteSpace(artifactName)
            ? throw new ArgumentException("Artifact name is required.", nameof(artifactName))
            : artifactName;
    }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertRootTemplateComboBoxFirstVisibleTextPaintedInCurrentFrame('{_artifactName}')";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var target = await session.QueryOnUiThreadAsync(() => ResolveFirstVisibleText(session), cancellationToken).ConfigureAwait(false);
        var sample = await session.SampleCurrentFrameRegionAsync(target.Bounds, cancellationToken).ConfigureAwait(false);
        var report = FormatReport(target, sample);
        session.Artifacts.BufferTextArtifact(_artifactName + ".txt", report);

        if (sample.BrightPixelCount >= MinimumBrightPixels)
        {
            return;
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Clipped,
            "Root template dropdown visible text was logically realized but not painted in the current frame before any further scroll. " +
            report.Replace(Environment.NewLine, " ", StringComparison.Ordinal));
    }

    private static VisibleTextTarget ResolveFirstVisibleText(InkkOopsSession session)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, ProbeItemText);
        var scrollViewer = RootTemplateComboBoxDropDownTestHelpers.FindDescendant<ScrollViewer>(listBox) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ListBox has no ScrollViewer.");
        if (!scrollViewer.TryGetContentViewportClipRect(out var viewportClip))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ScrollViewer does not expose a content viewport clip.");
        }

        foreach (var textBlock in RootTemplateComboBoxDropDownTestHelpers.EnumerateVisualsForDiagnostics(listBox).OfType<TextBlock>())
        {
            if (string.IsNullOrWhiteSpace(textBlock.Text) || !textBlock.TryGetRenderBoundsInRootSpace(out var bounds))
            {
                continue;
            }

            var intersectsViewport = bounds.Y + bounds.Height > viewportClip.Y + 0.5f &&
                                     bounds.Y < viewportClip.Y + viewportClip.Height - 0.5f;
            if (!intersectsViewport)
            {
                continue;
            }

            return new VisibleTextTarget(textBlock.Text, bounds, viewportClip);
        }

        throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, "Could not find any realized visible TextBlock in the reopened root template dropdown.");
    }

    private static string FormatReport(VisibleTextTarget target, InkkOopsFrameRegionSample sample)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"text={target.Text}");
        builder.AppendLine($"textBounds={RootTemplateComboBoxDropDownTestHelpers.FormatRect(target.Bounds)}");
        builder.AppendLine($"viewportClip={RootTemplateComboBoxDropDownTestHelpers.FormatRect(target.ViewportClip)}");
        builder.AppendLine($"sampleRect=({sample.X},{sample.Y},{sample.Width},{sample.Height})");
        builder.AppendLine($"totalPixels={sample.TotalPixelCount}");
        builder.AppendLine($"brightPixels={sample.BrightPixelCount}");
        builder.AppendLine($"maxLuma={sample.MaxLuma}");
        builder.AppendLine($"averageLuma={sample.AverageLuma:0.###}");
        builder.AppendLine($"minimumBrightPixels={MinimumBrightPixels}");
        return builder.ToString();
    }

    private readonly record struct VisibleTextTarget(string Text, LayoutRect Bounds, LayoutRect ViewportClip);
}

file sealed class AssertRootTemplateComboBoxDropDownThumbDragStableCommand : IInkkOopsCommand
{
    private const string ProbeItemText = "VirtualizingStackPanel";
    private const int StepCount = 24;
    private const float StepPixels = 3f;
    private const float AllowedPointerToThumbCenterDrift = 1.25f;
    private const float AllowedReverseJitter = 0.25f;

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return "AssertRootTemplateComboBoxDropDownThumbDragStable()";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var start = await session.QueryOnUiThreadAsync(() => ResolveThumbCenter(session), cancellationToken).ConfigureAwait(false);
        var samples = new List<ThumbDragSample>(StepCount + 1);
        samples.Add(await session.QueryOnUiThreadAsync(() => CaptureSample(session, start, 0), cancellationToken).ConfigureAwait(false));

        await session.MovePointerAsync(start, InkkOopsPointerMotion.WithTravelFrames(2), cancellationToken).ConfigureAwait(false);
        await session.PressPointerAsync(start, MouseButton.Left, cancellationToken).ConfigureAwait(false);

        var pointer = start;
        try
        {
            for (var step = 1; step <= StepCount; step++)
            {
                pointer = new System.Numerics.Vector2(start.X, start.Y + (step * StepPixels));
                await session.MovePointerAsync(pointer, InkkOopsPointerMotion.WithTravelFrames(1), cancellationToken).ConfigureAwait(false);
                await session.WaitFramesAsync(1, cancellationToken).ConfigureAwait(false);
                samples.Add(await session.QueryOnUiThreadAsync(() => CaptureSample(session, pointer, step), cancellationToken).ConfigureAwait(false));
            }
        }
        finally
        {
            await session.ReleasePointerAsync(pointer, MouseButton.Left, cancellationToken).ConfigureAwait(false);
        }

        AssertStableDrag(samples);
    }

    private static System.Numerics.Vector2 ResolveThumbCenter(InkkOopsSession session)
    {
        var scrollBar = ResolveVerticalScrollBar(session);
        var thumbRect = scrollBar.GetThumbRectForInput();
        return new System.Numerics.Vector2(
            thumbRect.X + (thumbRect.Width / 2f),
            thumbRect.Y + (thumbRect.Height / 2f));
    }

    private static ThumbDragSample CaptureSample(InkkOopsSession session, System.Numerics.Vector2 pointer, int step)
    {
        var scrollBar = ResolveVerticalScrollBar(session);
        var scrollViewer = ResolveDropDownScrollViewer(session);
        var thumb = RootTemplateComboBoxDropDownTestHelpers.FindDescendant<Thumb>(scrollBar) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown vertical ScrollBar has no Thumb descendant.");
        var captured = FocusManager.GetCapturedPointerElement();
        var thumbRect = scrollBar.GetThumbRectForInput();
        var thumbCenterY = thumbRect.Y + (thumbRect.Height / 2f);
        var pointerToThumbCenter = pointer.Y - thumbCenterY;

        return new ThumbDragSample(
            step,
            pointer.X,
            pointer.Y,
            thumbRect.X,
            thumbRect.Y,
            thumbRect.Width,
            thumbRect.Height,
            scrollViewer.VerticalOffset,
            scrollViewer.ExtentHeight,
            scrollViewer.ViewportHeight,
            ReferenceEquals(captured, thumb),
            pointerToThumbCenter);
    }

    private static ScrollBar ResolveVerticalScrollBar(InkkOopsSession session)
    {
        var scrollViewer = ResolveDropDownScrollViewer(session);
        var field = typeof(ScrollViewer).GetField("_verticalBar", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, "Could not resolve ScrollViewer._verticalBar field.");
        return field.GetValue(scrollViewer) as ScrollBar ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ScrollViewer._verticalBar was not a ScrollBar.");
    }

    private static ScrollViewer ResolveDropDownScrollViewer(InkkOopsSession session)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, ProbeItemText);
        return RootTemplateComboBoxDropDownTestHelpers.FindDescendant<ScrollViewer>(listBox) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ListBox has no ScrollViewer.");
    }

    private static void AssertStableDrag(IReadOnlyList<ThumbDragSample> samples)
    {
        if (samples.Count == 0)
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, "No thumb drag samples were captured.");
        }

        var startDrift = samples[0].PointerToThumbCenter;
        ThumbDragSample? previous = null;
        foreach (var sample in samples)
        {
            if (sample.Step > 0 && !sample.CapturedThumb)
            {
                throw CreateFailure("dropdown scrollbar thumb lost pointer capture", samples, sample);
            }

            var driftDelta = MathF.Abs(sample.PointerToThumbCenter - startDrift);
            if (driftDelta > AllowedPointerToThumbCenterDrift)
            {
                throw CreateFailure($"held pointer drifted away from thumb center by {driftDelta:0.###} px", samples, sample);
            }

            if (previous is { } before && sample.ThumbY + AllowedReverseJitter < before.ThumbY)
            {
                throw CreateFailure($"thumb moved upward during downward drag by {before.ThumbY - sample.ThumbY:0.###} px", samples, sample);
            }

            previous = sample;
        }
    }

    private static InkkOopsCommandException CreateFailure(string reason, IReadOnlyList<ThumbDragSample> samples, ThumbDragSample failingSample)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Root template dropdown scrollbar thumb drag jitter repro failed: {reason}.");
        builder.AppendLine($"Failing sample: {failingSample.Describe()}");
        builder.AppendLine("Samples:");
        foreach (var sample in samples)
        {
            builder.AppendLine(sample.Describe());
        }

        return new InkkOopsCommandException(InkkOopsFailureCategory.Clipped, builder.ToString());
    }

    private readonly record struct ThumbDragSample(
        int Step,
        float PointerX,
        float PointerY,
        float ThumbX,
        float ThumbY,
        float ThumbWidth,
        float ThumbHeight,
        float VerticalOffset,
        float ExtentHeight,
        float ViewportHeight,
        bool CapturedThumb,
        float PointerToThumbCenter)
    {
        public string Describe()
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"step={Step} pointer=({PointerX:0.###},{PointerY:0.###}) thumb=({ThumbX:0.###},{ThumbY:0.###},{ThumbWidth:0.###},{ThumbHeight:0.###}) drift={PointerToThumbCenter:0.###} offset={VerticalOffset:0.###} extent={ExtentHeight:0.###} viewport={ViewportHeight:0.###} captured={CapturedThumb}");
        }
    }
}

file sealed class ScrollRootTemplateComboBoxDropDownToBottomItemCommand : IInkkOopsCommand
{
    private readonly string _itemText;

    public ScrollRootTemplateComboBoxDropDownToBottomItemCommand(string itemText)
    {
        _itemText = itemText ?? throw new ArgumentNullException(nameof(itemText));
    }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Semantic;

    public string Describe()
    {
        return $"ScrollRootTemplateComboBoxDropDownToBottomItem('{_itemText}')";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ExecuteOnUiThreadAsync(() => ScrollDropDownItemIntoView(session, _itemText), cancellationToken).ConfigureAwait(false);
        await session.WaitFramesAsync(4, cancellationToken).ConfigureAwait(false);
        if (await session.QueryOnUiThreadAsync(() => IsItemVisibleInDropDownViewport(session, _itemText), cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, $"Root template dropdown item '{_itemText}' was not realized after ScrollIntoView.");
    }

    private static void ScrollDropDownItemIntoView(InkkOopsSession session, string itemText)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, itemText);
        var scrollViewer = RootTemplateComboBoxDropDownTestHelpers.FindDescendant<ScrollViewer>(listBox) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ListBox has no ScrollViewer.");
        scrollViewer.ScrollToVerticalOffset(10000f);
    }

    private static bool IsItemVisibleInDropDownViewport(InkkOopsSession session, string itemText)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, itemText);
        var textBlock = RootTemplateComboBoxDropDownTestHelpers.FindBottomMostTextBlock(listBox, itemText);
        if (textBlock == null || !textBlock.TryGetRenderBoundsInRootSpace(out var textBounds))
        {
            return false;
        }

        var scrollViewer = RootTemplateComboBoxDropDownTestHelpers.FindAncestor<ScrollViewer>(textBlock);
        if (scrollViewer == null || !scrollViewer.TryGetContentViewportClipRect(out var viewportClip))
        {
            return false;
        }

        return textBounds.Y + textBounds.Height >= viewportClip.Y - 0.5f &&
               textBounds.Y <= viewportClip.Y + viewportClip.Height + 0.5f;
    }
}

file sealed class AssertRootTemplateComboBoxDropDownItemFullyVisibleCommand : IInkkOopsCommand
{
    private readonly string _itemText;

    public AssertRootTemplateComboBoxDropDownItemFullyVisibleCommand(string itemText)
    {
        _itemText = itemText ?? throw new ArgumentNullException(nameof(itemText));
    }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"AssertRootTemplateComboBoxDropDownItemFullyVisible('{_itemText}')";
    }

    public Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.ExecuteOnUiThreadAsync(() => AssertFullyVisible(session, _itemText), cancellationToken);
    }

    private static void AssertFullyVisible(InkkOopsSession session, string itemText)
    {
        var listBox = RootTemplateComboBoxDropDownTestHelpers.FindRootTemplateDropDownListBox(session, itemText);
        var textBlock = RootTemplateComboBoxDropDownTestHelpers.FindBottomMostTextBlock(listBox, itemText) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, $"Could not find realized TextBlock text '{itemText}'.");
        if (!textBlock.TryGetRenderBoundsInRootSpace(out var textBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"TextBlock '{itemText}' does not expose render bounds.");
        }

        var listBoxItem = RootTemplateComboBoxDropDownTestHelpers.FindAncestor<ListBoxItem>(textBlock) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"TextBlock '{itemText}' has no ListBoxItem ancestor.");
        if (!listBoxItem.TryGetRenderBoundsInRootSpace(out var itemBounds))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"ListBoxItem '{itemText}' does not expose render bounds.");
        }

        var scrollViewer = RootTemplateComboBoxDropDownTestHelpers.FindAncestor<ScrollViewer>(textBlock) ??
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, $"TextBlock '{itemText}' has no ScrollViewer ancestor.");
        if (!scrollViewer.TryGetContentViewportClipRect(out var viewportClip))
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "Root template dropdown ScrollViewer does not expose a content viewport clip.");
        }

        var bottomDelta = itemBounds.Y + itemBounds.Height - (viewportClip.Y + viewportClip.Height);
        var topDelta = viewportClip.Y - itemBounds.Y;
        var fullyInsideClip = topDelta <= 0.5f && bottomDelta <= 0.5f;
        var centerPoint = new System.Numerics.Vector2(textBounds.X + (textBounds.Width / 2f), textBounds.Y + (textBounds.Height / 2f));
        var hitTestAtCenter = textBlock.HitTest(centerPoint);

        if (fullyInsideClip && hitTestAtCenter)
        {
            return;
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Clipped,
            $"Root template dropdown item '{itemText}' is clipped. " +
            $"itemBounds={RootTemplateComboBoxDropDownTestHelpers.FormatRect(itemBounds)} " +
            $"textBounds={RootTemplateComboBoxDropDownTestHelpers.FormatRect(textBounds)} " +
            $"viewportClip={RootTemplateComboBoxDropDownTestHelpers.FormatRect(viewportClip)} " +
            $"topDelta={topDelta:0.###} bottomDelta={bottomDelta:0.###} " +
            $"center=({centerPoint.X:0.###},{centerPoint.Y:0.###}) hitTestAtCenter={hitTestAtCenter}.");
    }
}

file static class RootTemplateComboBoxDropDownTestHelpers
{
    public static ListBox FindRootTemplateDropDownListBox(InkkOopsSession session, string itemText)
    {
        var comboBox = EnumerateVisuals(session.UiRoot.VisualRoot)
            .OfType<ComboBox>()
            .FirstOrDefault(static combo => string.Equals(combo.Name, "RootTemplateComboBox", StringComparison.Ordinal));
        if (comboBox is not { IsDropDownOpen: true })
        {
            throw new InkkOopsCommandException(InkkOopsFailureCategory.Unrealized, "RootTemplateComboBox dropdown is not open.");
        }

        foreach (var listBox in EnumerateVisuals(session.UiRoot.VisualRoot).OfType<ListBox>())
        {
            if (listBox.Items.Any(item => ItemTextMatches(item, itemText)))
            {
                return listBox;
            }
        }

        throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, $"Could not find root template dropdown ListBox containing '{itemText}'.");
    }

    public static object FindListBoxItemModel(ListBox listBox, string itemText)
    {
        foreach (var item in listBox.Items)
        {
            if (ItemTextMatches(item, itemText))
            {
                return item;
            }
        }

        throw new InkkOopsCommandException(InkkOopsFailureCategory.Unresolved, $"Could not find root template dropdown item '{itemText}'.");
    }

    public static TElement? FindDescendant<TElement>(UIElement root)
        where TElement : UIElement
    {
        return EnumerateVisuals(root).OfType<TElement>().FirstOrDefault();
    }

    public static TElement? FindAncestor<TElement>(UIElement element)
        where TElement : UIElement
    {
        for (var current = element.VisualParent ?? element.LogicalParent;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                return typed;
            }
        }

        return null;
    }

    public static TextBlock? FindBottomMostTextBlock(UIElement root, string text)
    {
        TextBlock? best = null;
        var bestY = float.NegativeInfinity;
        foreach (var textBlock in EnumerateVisuals(root).OfType<TextBlock>())
        {
            if (!string.Equals(textBlock.Text, text, StringComparison.Ordinal) ||
                !textBlock.TryGetRenderBoundsInRootSpace(out var bounds))
            {
                continue;
            }

            if (bounds.Y > bestY)
            {
                best = textBlock;
                bestY = bounds.Y;
            }
        }

        return best;
    }

    private static IEnumerable<UIElement> EnumerateVisuals(UIElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    public static IEnumerable<UIElement> EnumerateVisualsForDiagnostics(UIElement root)
    {
        return EnumerateVisuals(root);
    }

    private static bool ItemTextMatches(object? item, string expectedText)
    {
        if (item == null)
        {
            return false;
        }

        return string.Equals(ReadStringProperty(item, "DisplayName"), expectedText, StringComparison.Ordinal) ||
               string.Equals(ReadStringProperty(item, "ElementName"), expectedText, StringComparison.Ordinal) ||
               string.Equals(Convert.ToString(item, CultureInfo.InvariantCulture), expectedText, StringComparison.Ordinal) ||
               (Convert.ToString(item, CultureInfo.InvariantCulture)?.Contains(expectedText, StringComparison.Ordinal) == true);
    }

    private static string ReadStringProperty(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        if (property?.CanRead != true || property.GetIndexParameters().Length != 0)
        {
            return string.Empty;
        }

        return Convert.ToString(property.GetValue(item), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###})";
    }
}