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
            var sourceEditor = shell.SourceEditorControl;

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
    public void PreviewScrollViewer_ThumbRemainsInteractive_AfterShrinkScrollAndReexpand()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            LoadDesignerApplicationResources();

            var shell = new DesignerShellView
            {
                SourceText = BuildLargeScrollablePreviewXml()
            };
            var uiRoot = new UiRoot(shell);

            RunFrames(uiRoot, 6);
            Assert.True(shell.RefreshPreview());
            RunFrames(uiRoot, 10);

            var previewViewer = Assert.IsType<ScrollViewer>(shell.FindName("PreviewScrollViewer"));
            var previewDockSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewDockSplitter"));
            var previewSourceSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewSourceSplitter"));

            Assert.True(previewViewer.ExtentWidth > previewViewer.ViewportWidth + 0.01f);
            Assert.True(previewViewer.ExtentHeight > previewViewer.ViewportHeight + 0.01f);

            var verticalBar = GetPrivateScrollBar(previewViewer, "_verticalBar");
            var verticalThumb = FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb");
            Assert.NotNull(verticalThumb);

            var firstThumbCenter = GetCenter(verticalBar.GetThumbRectForInput());
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(firstThumbCenter, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(firstThumbCenter, leftPressed: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(firstThumbCenter.X, firstThumbCenter.Y + 40f), pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(firstThumbCenter.X, firstThumbCenter.Y + 40f), leftReleased: true));
            RunFrames(uiRoot, 2);

            Assert.True(previewViewer.VerticalOffset > 0.01f);

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(-650f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 12);
            DragSplitter(uiRoot, previewSourceSplitter, new Vector2(96f, -560f), travelFrames: 18);
            RunFrames(uiRoot, 18);

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(900f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 12);
            DragSplitter(uiRoot, previewSourceSplitter, new Vector2(-96f, 700f), travelFrames: 18);
            RunFrames(uiRoot, 18);

            verticalBar = GetPrivateScrollBar(previewViewer, "_verticalBar");
            verticalThumb = FindNamedVisualChild<Thumb>(verticalBar, "PART_Thumb");
            Assert.NotNull(verticalThumb);

            var secondThumbCenter = GetCenter(verticalBar.GetThumbRectForInput());
            var hit = VisualTreeHelper.HitTest(shell, secondThumbCenter);
            Assert.Same(verticalThumb, hit);

            var offsetBeforeSecondDrag = previewViewer.VerticalOffset;
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(secondThumbCenter, pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(secondThumbCenter, leftPressed: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(secondThumbCenter.X, secondThumbCenter.Y + 28f), pointerMoved: true));
            uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(secondThumbCenter.X, secondThumbCenter.Y + 28f), leftReleased: true));
            RunFrames(uiRoot, 2);

            Assert.True(previewViewer.VerticalOffset > offsetBeforeSecondDrag + 0.01f);
        }
        finally
        {
            RestoreApplicationResources(snapshot);
        }
    }

    [Fact]
    public void PreviewScrollViewer_ScrollBarVisibilityToggle_TriggersVisualStructureDirty()
    {
        var snapshot = CaptureApplicationResources();
        try
        {
            LoadDesignerApplicationResources();

            var shell = new DesignerShellView
            {
                SourceText = BuildResizeSensitivePreviewXml()
            };
            var uiRoot = new UiRoot(shell);

            RunFrames(uiRoot, 6);
            Assert.True(shell.RefreshPreview());
            RunFrames(uiRoot, 10);

            var previewViewer = Assert.IsType<ScrollViewer>(shell.FindName("PreviewScrollViewer"));
            var previewDockSplitter = Assert.IsType<GridSplitter>(shell.FindName("PreviewDockSplitter"));

            Assert.True(previewViewer.ExtentWidth <= previewViewer.ViewportWidth + 0.01f);
            Assert.True(previewViewer.ExtentHeight <= previewViewer.ViewportHeight + 0.01f);
            Assert.Equal(0, CountVisualChildrenOfType<ScrollBar>(previewViewer));

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(-650f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 18);

            Assert.True(previewViewer.ExtentWidth > previewViewer.ViewportWidth + 0.01f ||
                        previewViewer.ExtentHeight > previewViewer.ViewportHeight + 0.01f);
            Assert.True(CountVisualChildrenOfType<ScrollBar>(previewViewer) > 0);

            uiRoot.CompleteDrawStateForTests();
            uiRoot.ResetDirtyStateForTests();
            shell.ClearRenderInvalidationRecursive();
            var beforeMetrics = uiRoot.GetMetricsSnapshot();

            DragSplitter(uiRoot, previewDockSplitter, new Vector2(900f, 0f), travelFrames: 18);
            RunFrames(uiRoot, 18);

            var afterMetrics = uiRoot.GetMetricsSnapshot();

            Assert.True(previewViewer.ExtentWidth <= previewViewer.ViewportWidth + 0.01f);
            Assert.True(previewViewer.ExtentHeight <= previewViewer.ViewportHeight + 0.01f);
            Assert.Equal(0, CountVisualChildrenOfType<ScrollBar>(previewViewer));
            Assert.True(afterMetrics.VisualStructureChangeCount > beforeMetrics.VisualStructureChangeCount);
            Assert.True(uiRoot.IsFullDirtyForTests(), "Expected a full dirty redraw when preview scroll bars disappear after re-expanding the designer shell.");
            Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());
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
                        // DragSplitter(uiRoot, previewSourceSplitter, new Vector2(96f, -560f), travelFrames: 18);
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

    private static string BuildLargeScrollablePreviewXml()
    {
        return """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     Width="2200"
                     Height="1600"
                     Background="#111827">
          <Canvas Width="2200" Height="1600">
            <Border Width="2100"
                    Height="1500"
                    Background="#182230"
                    BorderBrush="#35506B"
                    BorderThickness="1"
                    CornerRadius="12"
                    Padding="18">
              <StackPanel>
                <TextBlock Text="Scrollable Preview"
                           Foreground="#E7EDF5"
                           FontSize="28"
                           FontWeight="SemiBold" />
                <TextBlock Text="Use the splitters, then keep scrolling."
                           Foreground="#8AA3B8"
                           Margin="0,6,0,12" />
                <Button Content="Preview Action"
                        Width="220"
                        Height="48"
                        Background="#1F8EFA"
                        BorderBrush="#56A7F7"
                        BorderThickness="1" />
              </StackPanel>
            </Border>
          </Canvas>
        </UserControl>
        """;
    }

        private static string BuildResizeSensitivePreviewXml()
        {
                return """
                <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                         Width="560"
                                         Height="240"
                                         Background="#111827">
                    <Canvas Width="560" Height="240">
                        <Border Width="560"
                                        Height="240"
                                        Background="#182230"
                                        BorderBrush="#35506B"
                                        BorderThickness="1"
                                        CornerRadius="12"
                                        Padding="18">
                            <StackPanel>
                                <TextBlock Text="Resize-sensitive Preview"
                                                     Foreground="#E7EDF5"
                                                     FontSize="28"
                                                     FontWeight="SemiBold" />
                                <TextBlock Text="This should fit at the default designer size and overflow only after splitter shrink."
                                                     Foreground="#8AA3B8"
                                                     Margin="0,6,0,12" />
                                <Button Content="Preview Action"
                                                Width="220"
                                                Height="48"
                                                Background="#1F8EFA"
                                                BorderBrush="#56A7F7"
                                                BorderThickness="1" />
                            </StackPanel>
                        </Border>
                    </Canvas>
                </UserControl>
                """;
        }

    private static ScrollBar GetPrivateScrollBar(ScrollViewer viewer, string fieldName)
    {
        var field = typeof(ScrollViewer).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ScrollBar>(field!.GetValue(viewer));
    }

    private static TElement? FindNamedVisualChild<TElement>(UIElement root, string name)
        where TElement : FrameworkElement
    {
        if (root is TElement typed && typed.Name == name)
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindNamedVisualChild<TElement>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static int CountVisualChildrenOfType<TElement>(UIElement root)
        where TElement : UIElement
    {
        var count = 0;
        foreach (var child in root.GetVisualChildren())
        {
            if (child is TElement)
            {
                count++;
            }
        }

        return count;
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