using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using InkkSlinger.Designer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DesignerSplitterHangCaptureTests
{
    private const string WorkerEnabledEnvironmentVariable = "INKKSLINGER_DESIGNER_SPLITTER_HANG_CAPTURE_WORKER";
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 820;
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task PreviewDockThenSourceSplitterDrag_WorkerProcess_CompletesWithoutHang()
    {
        await RunWorkerAsync(nameof(PreviewDockThenSourceSplitterDrag_Worker), "Designer splitter hang capture worker failed.");
    }

    [Fact]
    public void PreviewDockThenSourceSplitterDrag_Worker()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(WorkerEnabledEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var snapshot = CaptureApplicationResources();
        try
        {
            LoadDesignerApplicationResources();

            var shell = new DesignerShellView();
            var uiRoot = new UiRoot(shell);

            RunFrames(uiRoot, 6);
            Assert.True(shell.RefreshPreview());
            RunFrames(uiRoot, 10);

            var previewDockSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewDockSplitter"));
            var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));

            Assert.True(previewDockSplitter.ActualWidth > 0f);
            Assert.True(previewSourceSplitter.ActualHeight > 0f);

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(-650f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 12);

            DragSplitter(uiRoot, previewSourceSplitter, new Vector2(96f, -560f), travelFrames: 18);
            RunFrames(uiRoot, 18);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void PreviewDockThenSourceSplitterDrag_ViewportGrowth_ReflowsDesignerShell()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            LoadDesignerApplicationResources();

            var shell = new DesignerShellView();
            var uiRoot = new UiRoot(shell);

            RunFrames(uiRoot, 6);
            Assert.True(shell.RefreshPreview());
            RunFrames(uiRoot, 10);

            var previewDockSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewDockSplitter"));
            var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));
            var sourceEditor = Assert.IsType<RichTextBox>(shell.FindName("SourceEditor"));

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(-650f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 12);

            DragSplitter(uiRoot, previewSourceSplitter, new Vector2(96f, -560f), travelFrames: 18);
            RunFrames(uiRoot, 18);

            var dockSplitterXBeforeGrowth = previewDockSplitter.LayoutSlot.X;
            var sourceBottomBeforeGrowth = sourceEditor.LayoutSlot.Y + sourceEditor.LayoutSlot.Height;

            RunFrames(uiRoot, 18, 1600, 1000);

            Assert.True(previewDockSplitter.LayoutSlot.X > dockSplitterXBeforeGrowth + 100f);
            Assert.True(sourceEditor.LayoutSlot.Y + sourceEditor.LayoutSlot.Height > sourceBottomBeforeGrowth + 100f);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public async Task WrappedTextReuse_WithForcedVisualCycle_WorkerProcess_CompletesWithoutHang()
    {
        await RunWorkerAsync(
            nameof(WrappedTextReuse_WithForcedVisualCycle_Worker),
            "Wrapped text reuse hang capture worker failed.");
    }

    [Fact]
    public void WrappedTextReuse_WithForcedVisualCycle_Worker()
    {
        if (!IsWorkerEnabled())
        {
            return;
        }

        var border = new Border();
        var grid = new Grid();
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel()
        };

        var panel = Assert.IsType<StackPanel>(viewer.Content);
        var text = new TextBlock
        {
            Text = "Known regression typography cycle repro",
            TextWrapping = TextWrapping.Wrap
        };

        panel.AddChild(text);
        grid.AddChild(viewer);
        border.Child = grid;

        border.Measure(new Vector2(320f, 240f));
        text.Measure(new Vector2(180f, 120f));

        ForceVisualParent(border, text);
        try
        {
            var canReuseMethod = typeof(TextBlock).GetMethod(
                "CanReuseWrappedLayoutForWidthRange",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(canReuseMethod);

            var result = canReuseMethod!.Invoke(text, new object[] { 180f, 220f });

            Assert.IsType<bool>(result);
        }
        finally
        {
            ForceVisualParent(border, null);
        }
    }

    private static async Task RunWorkerAsync(string workerMethodName, string failurePrefix)
    {
        var repositoryRoot = FindRepositoryRoot();
        var testProjectPath = Path.Combine(repositoryRoot, "InkkSlinger.Tests", "InkkSlinger.Tests.csproj");
        var workerName = $"{typeof(DesignerSplitterHangCaptureTests).FullName}.{workerMethodName}";
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
                $"{failurePrefix}{Environment.NewLine}Process timed out after {WorkerTimeout.TotalSeconds:0} seconds.{Environment.NewLine}Arguments: {arguments}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
            return;
        }

        Assert.True(
            process!.ExitCode == 0,
            $"{failurePrefix}{Environment.NewLine}ExitCode: {process.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
    }

    private static void DragSplitter(UiRoot uiRoot, GridSplitter splitter, Vector2 delta, int travelFrames)
    {
        var start = GetCenter(splitter.LayoutSlot);
        var end = start + delta;

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, pointerMoved: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(start, leftPressed: true));
        RunFrames(uiRoot, 1);

        for (var frame = 1; frame <= travelFrames; frame++)
        {
            var progress = frame / (float)travelFrames;
            var pointer = Vector2.Lerp(start, end, progress);
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));
            RunFrames(uiRoot, 1);
        }

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(end, leftReleased: true));
        RunFrames(uiRoot, 1);
    }

    private static InputDelta CreatePointerDelta(
        Vector2 pointer,
        bool pointerMoved = false,
        bool leftPressed = false,
        bool leftReleased = false)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunFrames(UiRoot uiRoot, int frameCount, int viewportWidth = ViewportWidth, int viewportHeight = ViewportHeight)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            uiRoot.Update(
                new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
                new Viewport(0, 0, viewportWidth, viewportHeight));
        }
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static Dictionary<object, object> CaptureApplicationResources()
    {
        return new Dictionary<object, object>(UiApplication.Current.Resources);
    }

    private static void RestoreApplicationResources(Dictionary<object, object> snapshot)
    {
        TestApplicationResources.Restore(snapshot);
    }

    private static void LoadDesignerApplicationResources()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appXmlPath = Path.Combine(repositoryRoot, "InkkSlinger.Designer", "App.xml");
        Assert.True(File.Exists(appXmlPath), $"Expected Designer App.xml to exist at '{appXmlPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appXmlPath, clearExisting: true);
    }

    private static bool IsWorkerEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(WorkerEnabledEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
    }

    private static void ForceVisualParent(UIElement child, UIElement? parent)
    {
        var backingField = typeof(UIElement).GetField("<VisualParent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(backingField);

        backingField!.SetValue(child, parent);
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