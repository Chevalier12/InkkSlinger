using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InkkSlinger.Designer;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerHoverTelemetryRuntimeReproTests
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task SourceInspectorBackgroundHoverSequence_RuntimeRun_WritesFocusedHoverTelemetry()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tempAppData = Path.Combine(Path.GetTempPath(), "inkkslinger-designer-hover-telemetry-repro", Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "inkkoops", "designer-hover-telemetry-repro");
        var projectPath = repositoryRoot.Replace('\\', '/');

        SeedRecentProject(tempAppData, projectPath);

        try
        {
            var designerProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "InkkSlinger.Designer.csproj");
            var scriptAssemblyPath = typeof(DesignerHoverTelemetryRuntimeReproTests).Assembly.Location;
            var result = await RunDesignerScenarioAsync(
                repositoryRoot,
                designerProjectPath,
                artifactRoot,
                tempAppData,
                DesignerSourceInspectorHoverTelemetryRuntimeScenario.ScriptName,
                scriptAssemblyPath);

            Assert.Equal(0, result.ExitCode);

            var runDirectory = GetLatestRunDirectory(artifactRoot, DesignerSourceInspectorHoverTelemetryRuntimeScenario.ScriptName);
            using var resultDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDirectory, "result.json")));
            Assert.Equal("Completed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(DesignerSourceInspectorHoverTelemetryRuntimeScenario.ScriptName, resultDocument.RootElement.GetProperty("scriptName").GetString());

            var setupEvidence = File.ReadAllText(Path.Combine(runDirectory, "after-setup.txt"));
            Assert.Contains("shell=DesignerShellView", setupEvidence);
            Assert.Contains("focused=ComboBox#EditorComboBox", setupEvidence);
            Assert.Contains("background_editor=ComboBox#EditorComboBox", setupEvidence);
            Assert.Contains("preview_scroll_viewer=ScrollViewer#PreviewScrollViewer", setupEvidence);
            Assert.Contains("preview_host=ContentControl#PreviewHost", setupEvidence);

            var comboHoverTelemetry = File.ReadAllText(Path.Combine(runDirectory, "after-hover-editor-combobox.txt"));
            Assert.Contains("hovered=ComboBox#EditorComboBox", comboHoverTelemetry);
            Assert.Contains("focused=ComboBox#EditorComboBox", comboHoverTelemetry);

            var editorHoverTelemetry = File.ReadAllText(Path.Combine(runDirectory, "after-hover-part-editor.txt"));
            Assert.Contains("hovered=RichTextBox#PART_Editor", editorHoverTelemetry);
            Assert.Contains("focused=ComboBox#EditorComboBox", editorHoverTelemetry);

            var previewHoverTelemetry = File.ReadAllText(Path.Combine(runDirectory, "after-hover-preview-scrollviewer.txt"));
            Assert.DoesNotContain("hovered=TabItem#SourceTab", previewHoverTelemetry, StringComparison.Ordinal);
            Assert.DoesNotContain("hovered=ContentControl#PreviewHost", previewHoverTelemetry, StringComparison.Ordinal);
            Assert.Contains("hovered=StackPanel", previewHoverTelemetry);
            Assert.Contains("focused=ComboBox#EditorComboBox", previewHoverTelemetry);

            var actionLog = File.ReadAllText(Path.Combine(runDirectory, "action.log"));
            Assert.Contains("HoverResolvedSourceInspectorBackgroundEditor", actionLog);
            Assert.Contains("Name('PART_Editor')", actionLog);
            Assert.Contains("Name('PreviewScrollViewer')", actionLog);

            Assert.True(File.Exists(Path.Combine(runDirectory, "after-setup.png")), "Expected the configured shell frame capture.");
            Assert.True(File.Exists(Path.Combine(runDirectory, "after-hover-preview-scrollviewer.png")), "Expected the post-preview hover frame capture.");
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
        string scriptName,
        string scriptAssemblyPath)
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
            "--inkkoops-script-assembly",
            Quote(scriptAssemblyPath),
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
        startInfo.Environment["INKKSLINGER_INKKOOPS_RUN_KIND"] = "designer-hover-telemetry-repro";

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
                $"Designer hover telemetry RUN timed out after {RunTimeout.TotalSeconds:0} seconds.{Environment.NewLine}" +
                $"Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            return new ProcessRunResult(-1, stdout, stderr);
        }

        Assert.True(
            process!.ExitCode == 0,
            $"Designer hover telemetry RUN failed.{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}" +
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
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        return directory ?? throw new DirectoryNotFoundException($"Could not find run directory for script '{scriptName}' under '{artifactRoot}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "InkkSlinger.sln")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    private static string Quote(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? string.Concat('"', value, '"')
            : value;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
}

public sealed class DesignerSourceInspectorHoverTelemetryRuntimeScenario : IInkkOopsScriptDefinition
{
    private const string BorderHoverViewXml = """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Background="#101820">
          <StackPanel>
            <Border x:Name="ChromeBorder"
                    Background="#223344"
                    BorderBrush="#334455"
                    BorderThickness="1" />
          </StackPanel>
        </UserControl>
        """;

    public const string ScriptName = "designer-source-inspector-hover-telemetry-repro";

    public string Name => ScriptName;

    public InkkOopsScript CreateScript()
    {
        var recentProjectEntry = InkkOopsTargetSelector.Within(
            InkkOopsTargetSelector.Name("RecentProjectsItemsControl"),
            InkkOopsTargetSelector.Name("InkkSlinger"));

        return new InkkOopsScriptBuilder(Name)
            .ResizeWindow(1280, 900)
            .WaitForVisible("RecentProjectsItemsControl", maxFrames: 240)
            .Click(recentProjectEntry, anchor: null, InkkOopsPointerMotion.WithTravelFrames(4))
            .WaitForVisible("ProjectExplorerTree", maxFrames: 360, InkkOopsPointerAnchor.OffsetBy(80f, 14f))
            .WaitForVisible("SourceEditorPane", maxFrames: 360)
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .Add(new ConfigureDesignerSourceInspectorHoverStateCommand(BorderHoverViewXml, "Border", "Background", "after-setup"))
            .WaitForVisible("PART_Editor", maxFrames: 240)
            .WaitForVisible("PreviewScrollViewer", maxFrames: 240)
            .Add(new HoverResolvedSourceInspectorBackgroundEditorCommand(dwellFrames: 2))
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-hover-editor-combobox")
            .Hover("PART_Editor", dwellFrames: 2)
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-hover-part-editor")
            .Hover("PreviewScrollViewer", dwellFrames: 2)
            .WaitForIdle(InkkOopsIdlePolicy.DiagnosticsStable)
            .DumpTelemetry("after-hover-preview-scrollviewer")
            .CaptureFrame("after-hover-preview-scrollviewer")
            .Build();
    }
}

internal sealed class ConfigureDesignerSourceInspectorHoverStateCommand : IInkkOopsCommand
{
    public ConfigureDesignerSourceInspectorHoverStateCommand(string sourceText, string elementName, string propertyName, string artifactName)
    {
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        ElementName = string.IsNullOrWhiteSpace(elementName) ? throw new ArgumentException("Element name is required.", nameof(elementName)) : elementName;
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? throw new ArgumentException("Property name is required.", nameof(propertyName)) : propertyName;
        ArtifactName = string.IsNullOrWhiteSpace(artifactName) ? throw new ArgumentException("Artifact name is required.", nameof(artifactName)) : artifactName;
    }

    public string SourceText { get; }

    public string ElementName { get; }

    public string PropertyName { get; }

    public string ArtifactName { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Diagnostic;

    public string Describe()
    {
        return $"ConfigureDesignerSourceInspectorHoverState({ElementName}.{PropertyName}, artifact: {ArtifactName})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await session.ExecuteOnUiThreadAsync(() => ConfigureOnUiThread(session), cancellationToken).ConfigureAwait(false);
        await session.WaitForIdleAsync(InkkOopsIdlePolicy.DiagnosticsStable, cancellationToken).ConfigureAwait(false);
        await session.ExecuteOnUiThreadAsync(() => WriteEvidence(session), cancellationToken).ConfigureAwait(false);
        await session.CaptureFrameAsync(ArtifactName, cancellationToken).ConfigureAwait(false);
    }

    private void ConfigureOnUiThread(InkkOopsSession session)
    {
        var shell = ResolveShell(session);
        ExpandSourcePane(shell);
        shell.SourceText = SourceText;
        shell.RefreshPreview();

        var sourceEditorView = shell.SourceEditorView;
        var selectionIndex = SourceText.IndexOf($"<{ElementName}", StringComparison.Ordinal);
        if (selectionIndex < 0)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unresolved,
                $"Could not find '<{ElementName}' in the configured source text.");
        }

        sourceEditorView.Editor.Select(selectionIndex + 1, 0);
        shell.UpdateLayout();
        sourceEditorView.UpdateLayout();
        FilterSourcePropertyInspector(sourceEditorView);

        var backgroundEditor = ResolveBackgroundEditor(session, shell);
        FocusManager.SetFocus(backgroundEditor);
        shell.UpdateLayout();
        sourceEditorView.UpdateLayout();
    }

    private void WriteEvidence(InkkOopsSession session)
    {
        var shell = ResolveShell(session);
        var sourceEditorView = shell.SourceEditorView;
        var backgroundEditor = ResolveBackgroundEditor(session, shell);
        var backgroundEditorCandidates = FindNamedDescendants(sourceEditorView, "EditorComboBox").OfType<ComboBox>().ToArray();
        var partEditor = session.ResolveRequiredTarget(new InkkOopsTargetReference("PART_Editor"));
        var previewScrollViewer = session.ResolveRequiredTarget(new InkkOopsTargetReference("PreviewScrollViewer"));
        var previewHost = session.ResolveRequiredTarget(new InkkOopsTargetReference("PreviewHost"));

        var builder = new StringBuilder();
        builder.AppendLine($"shell={shell.GetType().Name}");
        builder.AppendLine($"source_editor_view={InkkOopsTargetResolver.DescribeElement(sourceEditorView)}");
        builder.AppendLine($"background_editor={InkkOopsTargetResolver.DescribeElement(backgroundEditor)}");
        builder.AppendLine($"part_editor={InkkOopsTargetResolver.DescribeElement(partEditor)}");
        builder.AppendLine($"preview_scroll_viewer={InkkOopsTargetResolver.DescribeElement(previewScrollViewer)}");
        builder.AppendLine($"preview_host={InkkOopsTargetResolver.DescribeElement(previewHost)}");
        builder.AppendLine($"source_pane_height={GetSourcePaneHeight(shell):0.###}");
        builder.AppendLine($"focused={InkkOopsTargetResolver.DescribeElement(FocusManager.GetFocusedElement())}");
        builder.AppendLine($"hovered={InkkOopsTargetResolver.DescribeElement(session.UiRoot.GetHoveredElementForDiagnostics())}");
        builder.AppendLine($"selection_start={sourceEditorView.Editor.SelectionStart}");
        builder.AppendLine($"selection_length={sourceEditorView.Editor.SelectionLength}");
        builder.AppendLine($"source_contains_element={SourceText.Contains($"<{ElementName}", StringComparison.Ordinal)}");
        builder.AppendLine($"property_name={PropertyName}");
        builder.AppendLine($"background_editor_candidate_count={backgroundEditorCandidates.Length}");
        for (var index = 0; index < backgroundEditorCandidates.Length; index++)
        {
            var candidate = backgroundEditorCandidates[index];
            var visibleArea = TryGetVisibleBoundsForInput(candidate, session.Host.GetViewportBounds(), out var visibleBounds)
                ? visibleBounds.Width * visibleBounds.Height
                : 0f;
            builder.AppendLine(
                $"background_editor_candidate[{index}]={InkkOopsTargetResolver.DescribeElement(candidate)} selected={ReferenceEquals(candidate, backgroundEditor)} focused={ReferenceEquals(candidate, FocusManager.GetFocusedElement())} visible_area={visibleArea:0.###}");
        }
        session.Artifacts.BufferTextArtifact($"{ArtifactName}.txt", builder.ToString());
    }

    internal static ComboBox ResolveBackgroundEditor(InkkOopsSession session, DesignerShellView shell)
    {
        var candidates = FindNamedDescendants(shell.SourceEditorView, "EditorComboBox").OfType<ComboBox>().ToArray();
        if (candidates.Length == 0)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unresolved,
                "Could not resolve EditorComboBox for the source inspector editor.");
        }

        var viewport = session.Host.GetViewportBounds();
        return candidates
            .OrderByDescending(candidate => TryGetVisibleBoundsForInput(candidate, viewport, out var visibleBounds) ? visibleBounds.Width * visibleBounds.Height : 0f)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .First();
    }

    private void FilterSourcePropertyInspector(DesignerSourceEditorView sourceEditorView)
    {
        var filterTextBox = sourceEditorView.FindName("SourcePropertyInspectorFilterTextBox") as TextBox;
        var scrollViewer = sourceEditorView.FindName("SourcePropertyInspectorScrollViewer") as ScrollViewer;
        if (filterTextBox == null || scrollViewer == null)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unresolved,
                "Could not resolve the source property inspector filter controls.");
        }

        filterTextBox.Text = PropertyName;
        sourceEditorView.UpdateLayout();
        scrollViewer.ScrollToVerticalOffset(0f);
        scrollViewer.UpdateLayout();
    }

    private static void ExpandSourcePane(DesignerShellView shell)
    {
        var rootGrid = shell.FindName("DesignerShellRootGrid") as Grid;
        if (rootGrid == null || rootGrid.RowDefinitions.Count < 5)
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Unresolved,
                "Could not resolve DesignerShellRootGrid row definitions for source-pane sizing.");
        }

        rootGrid.RowDefinitions[4].Height = new GridLength(360f);
        shell.UpdateLayout();
    }

    private static float GetSourcePaneHeight(DesignerShellView shell)
    {
        var rootGrid = shell.FindName("DesignerShellRootGrid") as Grid;
        if (rootGrid == null || rootGrid.RowDefinitions.Count < 5)
        {
            return -1f;
        }

        return rootGrid.RowDefinitions[4].ActualHeight;
    }

    internal static DesignerShellView ResolveShell(InkkOopsSession session)
    {
        var root = session.Host.GetVisualRootElement();
        var shell = root as DesignerShellView ?? FindDescendant<DesignerShellView>(root);
        if (shell != null)
        {
            return shell;
        }

        throw new InkkOopsCommandException(
            InkkOopsFailureCategory.Unresolved,
            $"Expected DesignerShellView in the visual root subtree, but found {InkkOopsTargetResolver.DescribeElement(root)}.");
    }

    private static T? FindDescendant<T>(UIElement? root)
        where T : UIElement
    {
        if (root == null)
        {
            return null;
        }

        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is T descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<UIElement> FindNamedDescendants(UIElement? root, string name)
    {
        if (root == null)
        {
            yield break;
        }

        foreach (var child in root.GetVisualChildren())
        {
            if (child is FrameworkElement frameworkElement &&
                string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
            {
                yield return child;
            }

            foreach (var descendant in FindNamedDescendants(child, name))
            {
                yield return descendant;
            }
        }
    }

    internal static bool TryGetVisibleBoundsForInput(UIElement element, LayoutRect viewport, out LayoutRect visibleBounds)
    {
        if (!element.TryGetRenderBoundsInRootSpace(out var bounds) || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            visibleBounds = default;
            return false;
        }

        visibleBounds = bounds;
        for (var current = element.VisualParent; current != null; current = current.VisualParent)
        {
            if (!current.TryGetRenderBoundsInRootSpace(out var currentBounds) || currentBounds.Width <= 0f || currentBounds.Height <= 0f)
            {
                visibleBounds = default;
                return false;
            }

            visibleBounds = IntersectRects(visibleBounds, currentBounds);
            if (visibleBounds.Width <= 0f || visibleBounds.Height <= 0f)
            {
                return false;
            }
        }

        visibleBounds = IntersectRects(visibleBounds, viewport);
        return visibleBounds.Width > 0f && visibleBounds.Height > 0f;
    }

    private static LayoutRect IntersectRects(LayoutRect first, LayoutRect second)
    {
        var left = MathF.Max(first.X, second.X);
        var top = MathF.Max(first.Y, second.Y);
        var right = MathF.Min(first.X + first.Width, second.X + second.Width);
        var bottom = MathF.Min(first.Y + first.Height, second.Y + second.Height);
        if (right <= left || bottom <= top)
        {
            return new LayoutRect(left, top, 0f, 0f);
        }

        return new LayoutRect(left, top, right - left, bottom - top);
    }
}

internal sealed class HoverResolvedSourceInspectorBackgroundEditorCommand : IInkkOopsCommand
{
    public HoverResolvedSourceInspectorBackgroundEditorCommand(int dwellFrames = 0, InkkOopsPointerMotion? motion = null)
    {
        DwellFrames = Math.Max(0, dwellFrames);
        Motion = motion ?? InkkOopsPointerMotion.Default;
    }

    public int DwellFrames { get; }

    public InkkOopsPointerMotion Motion { get; }

    public InkkOopsExecutionMode ExecutionMode => InkkOopsExecutionMode.Pointer;

    public string Describe()
    {
        return $"HoverResolvedSourceInspectorBackgroundEditor(dwellFrames: {DwellFrames}, travelFrames: {Motion.TravelFrames}, easing: {Motion.Easing})";
    }

    public async Task ExecuteAsync(InkkOopsSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var shell = ConfigureDesignerSourceInspectorHoverStateCommand.ResolveShell(session);
        var editor = ConfigureDesignerSourceInspectorHoverStateCommand.ResolveBackgroundEditor(session, shell);
        if (!ConfigureDesignerSourceInspectorHoverStateCommand.TryGetVisibleBoundsForInput(editor, session.Host.GetViewportBounds(), out var visibleBounds))
        {
            throw new InkkOopsCommandException(
                InkkOopsFailureCategory.Offscreen,
                "Resolved source inspector background editor does not have visible bounds in the viewport.");
        }

        var point = new System.Numerics.Vector2(
            visibleBounds.X + MathF.Min(8f, MathF.Max(0f, visibleBounds.Width - 1f)),
            visibleBounds.Y + MathF.Min(8f, MathF.Max(0f, visibleBounds.Height - 1f)));
        await session.MovePointerAsync(point, Motion, cancellationToken).ConfigureAwait(false);
        if (DwellFrames > 0)
        {
            await session.WaitFramesAsync(DwellFrames, cancellationToken).ConfigureAwait(false);
        }
    }
}
