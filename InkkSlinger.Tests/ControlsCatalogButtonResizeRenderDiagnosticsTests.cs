using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogButtonResizeRenderDiagnosticsTests
{
    private const int LargeWidth = 1360;
    private const int LargeHeight = 860;
    private const int SmallWidth = 960;
    private const int SmallHeight = 540;

    [Fact]
    public void ControlsCatalog_ButtonView_ShrinkWhileHovered_FinalLayoutMatchesFreshSmallLayout()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var freshCatalog = CreateCatalogHost();
            var freshUiRoot = freshCatalog.UiRoot;
            RunSteadyLayout(freshCatalog.Host, freshUiRoot, SmallWidth, SmallHeight);
            var freshView = GetSelectedButtonView(freshCatalog.Catalog);
            var freshSnapshot = CaptureButtonViewLayoutSnapshot(freshCatalog.Catalog, freshView);

            var resizedCatalog = CreateCatalogHost();
            var resizedUiRoot = resizedCatalog.UiRoot;
            RunSteadyLayout(resizedCatalog.Host, resizedUiRoot, LargeWidth, LargeHeight);

            var resizedView = GetSelectedButtonView(resizedCatalog.Catalog);
            var hoverButton = FindButtonByText(resizedView, "Centered");
            var hoverPoint = GetCenter(hoverButton);
            MovePointer(resizedUiRoot, new Vector2(8f, 8f));
            MovePointer(resizedUiRoot, hoverPoint);
            AdvanceFrames(resizedCatalog.Host, resizedUiRoot, LargeWidth, LargeHeight, frameCount: 12, elapsedMs: 16);

            RunSteadyLayout(resizedCatalog.Host, resizedUiRoot, SmallWidth, SmallHeight);
            var finalSnapshot = CaptureButtonViewLayoutSnapshot(resizedCatalog.Catalog, GetSelectedButtonView(resizedCatalog.Catalog));

            AssertSnapshotsClose(freshSnapshot, finalSnapshot);
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void ControlsCatalog_ButtonView_ShrinkWhileHovered_WritesRetainedRenderDiagnosticsLog()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var freshCatalog = CreateCatalogHost();
            RunSteadyLayout(freshCatalog.Host, freshCatalog.UiRoot, SmallWidth, SmallHeight);
            var freshView = GetSelectedButtonView(freshCatalog.Catalog);
            var freshSnapshot = CaptureButtonViewLayoutSnapshot(freshCatalog.Catalog, freshView);
            var freshDrawOrder = DescribeVisuals(
                freshCatalog.UiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(0f, 0f, SmallWidth, SmallHeight)));

            var resizedCatalog = CreateCatalogHost();
            var resizedUiRoot = resizedCatalog.UiRoot;
            RunSteadyLayout(resizedCatalog.Host, resizedUiRoot, LargeWidth, LargeHeight);

            var resizedView = GetSelectedButtonView(resizedCatalog.Catalog);
            var hoverButton = FindButtonByText(resizedView, "Centered");
            var hoverPoint = GetCenter(hoverButton);
            MovePointer(resizedUiRoot, new Vector2(8f, 8f));
            MovePointer(resizedUiRoot, hoverPoint);
            AdvanceFrames(resizedCatalog.Host, resizedUiRoot, LargeWidth, LargeHeight, frameCount: 12, elapsedMs: 16);

            resizedUiRoot.RebuildRenderListForTests();
            resizedUiRoot.ResetDirtyStateForTests();
            resizedCatalog.Host.ClearRenderInvalidationRecursive();
            resizedUiRoot.CompleteDrawStateForTests();
            resizedUiRoot.ClearDirtyBoundsEventTraceForTests();

            RunFrame(resizedCatalog.Host, resizedUiRoot, SmallWidth, SmallHeight, totalMs: 512, elapsedMs: 16);
            var outsidePoint = new Vector2(MathF.Max(8f, SmallWidth - 24f), 12f);
            MovePointer(resizedUiRoot, outsidePoint);
            AdvanceFrames(resizedCatalog.Host, resizedUiRoot, SmallWidth, SmallHeight, frameCount: 12, elapsedMs: 16);

            var finalView = GetSelectedButtonView(resizedCatalog.Catalog);
            var finalSnapshot = CaptureButtonViewLayoutSnapshot(resizedCatalog.Catalog, finalView);
            var finalDrawOrder = DescribeVisuals(
                resizedUiRoot.GetRetainedDrawOrderForClipForTests(new LayoutRect(0f, 0f, SmallWidth, SmallHeight)));
            var invalidation = resizedUiRoot.GetRenderInvalidationDebugSnapshotForTests();
            var performance = resizedUiRoot.GetPerformanceTelemetrySnapshotForTests();
            var render = resizedUiRoot.GetRenderTelemetrySnapshotForTests();
            var dirtyBoundsTrace = string.Join(" | ", resizedUiRoot.GetDirtyBoundsEventTraceForTests());
            var dirtyRegions = resizedUiRoot.GetDirtyRegionSummaryForTests();
            var dirtyRootSummary = resizedUiRoot.GetLastSynchronizedDirtyRootSummaryForTests();

            var logPath = GetDiagnosticsLogPath("controls-catalog-button-resize-render-diagnostics");
            var lines = new List<string>
            {
                "scenario=Controls Catalog Button view dramatic resize while preview button hover animation is active",
                $"timestamp_utc={DateTime.UtcNow:O}",
                $"large_viewport={LargeWidth}x{LargeHeight}",
                $"small_viewport={SmallWidth}x{SmallHeight}",
                "step_1=load App.xml so catalog buttons use animated scale and drop-shadow template",
                "step_2=open Controls Catalog Button view at large viewport",
                "step_3=hover the preview 'Centered' button until hover animation grows",
                "step_4=resize to the small viewport used by the manual repro",
                "step_5=move pointer away and advance animation frames",
                $"fresh_small_layout={freshSnapshot}",
                $"resized_small_layout={finalSnapshot}",
                $"layout_matches_fresh_small={SnapshotsClose(freshSnapshot, finalSnapshot)}",
                $"fresh_small_draw_order_count={freshDrawOrder.Count}",
                $"resized_small_draw_order_count={finalDrawOrder.Count}",
                $"fresh_small_draw_order_sample={string.Join(" -> ", freshDrawOrder.Take(20))}",
                $"resized_small_draw_order_sample={string.Join(" -> ", finalDrawOrder.Take(20))}",
                $"draw_order_matches_fresh_small={freshDrawOrder.SequenceEqual(finalDrawOrder, StringComparer.Ordinal)}",
                $"dirty_root_summary={dirtyRootSummary}",
                $"dirty_regions={dirtyRegions}",
                $"is_full_dirty={resizedUiRoot.IsFullDirtyForTests()}",
                $"dirty_root_count={performance.DirtyRootCount}",
                $"retained_traversals={performance.RetainedTraversalCount}",
                $"ancestor_refresh_nodes={performance.AncestorMetadataRefreshNodeCount}",
                $"last_dirty_region_traversals={render.DirtyRegionTraversalCount}",
                $"last_dirty_threshold_fallbacks={render.DirtyRegionThresholdFallbackCount}",
                $"full_dirty_viewport_change_count={render.FullDirtyViewportChangeCount}",
                $"requested_invalidation_source={invalidation.RequestedSourceType}#{invalidation.RequestedSourceName}",
                $"effective_invalidation_source={invalidation.EffectiveSourceType}#{invalidation.EffectiveSourceName}",
                $"dirty_bounds_visual={invalidation.DirtyBoundsVisualType}#{invalidation.DirtyBoundsVisualName}",
                $"dirty_bounds_used_hint={invalidation.DirtyBoundsUsedHint}",
                $"dirty_bounds_rect={(invalidation.HasDirtyBounds ? FormatRect(invalidation.DirtyBounds) : "none")}",
                $"dirty_bounds_trace={dirtyBoundsTrace}"
            };

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllLines(logPath, lines);

            Assert.True(File.Exists(logPath));
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void ControlsCatalog_ButtonView_ShrinkThenHoverSequence_KeepsRetainedTreeSnapshotsConsistent()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var catalogHost = CreateCatalogHost();
            var uiRoot = catalogHost.UiRoot;
            RunSteadyLayout(catalogHost.Host, uiRoot, LargeWidth, LargeHeight);

            var initialReport = uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests();
            Assert.Equal("ok", initialReport);

            RunSteadyLayout(catalogHost.Host, uiRoot, SmallWidth, SmallHeight);
            var resizedView = GetSelectedButtonView(catalogHost.Catalog);
            var accentButton = FindButtonByText(resizedView, "Accent");
            var dangerButton = FindButtonByText(resizedView, "Danger");
            var compactButton = FindButtonByText(resizedView, "Compact");
            var countedButton = FindButtonByText(resizedView, "Click me");

            MovePointer(uiRoot, new Vector2(8f, 8f));
            AdvanceFrames(catalogHost.Host, uiRoot, SmallWidth, SmallHeight, frameCount: 4, elapsedMs: 16);

            var hoverButtons = new[] { accentButton, dangerButton, compactButton, countedButton };
            foreach (var button in hoverButtons)
            {
                MovePointer(uiRoot, GetCenter(button));
                AdvanceFrames(catalogHost.Host, uiRoot, SmallWidth, SmallHeight, frameCount: 8, elapsedMs: 16);

                var report = uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests();
                Assert.True(
                    string.Equals("ok", report, StringComparison.Ordinal),
                    $"Retained tree drifted after hover on '{button.GetContentText()}': {report}");
            }
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void ControlsCatalog_ButtonView_ShrinkThenGrowBack_KeepsRetainedTreeSnapshotsConsistent()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var catalogHost = CreateCatalogHost();
            var uiRoot = catalogHost.UiRoot;
            RunSteadyLayout(catalogHost.Host, uiRoot, LargeWidth, LargeHeight);
            Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

            RunSteadyLayout(catalogHost.Host, uiRoot, 736, LargeHeight);
            Assert.Equal("ok", uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests());

            RunSteadyLayout(catalogHost.Host, uiRoot, 900, LargeHeight);
            var report = uiRoot.ValidateRetainedTreeAgainstCurrentVisualStateForTests();
            Assert.True(
                string.Equals("ok", report, StringComparison.Ordinal),
                $"Retained tree drifted after shrink/grow resize sequence: {report}");
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    [Fact]
    public void ControlsCatalog_ButtonView_NarrowLayout_KeepsButtonRowsWithinPreviewWidth()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();
            AnimationManager.Current.ResetForTests();
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();

            var catalogHost = CreateCatalogHost();
            var uiRoot = catalogHost.UiRoot;
            RunSteadyLayout(catalogHost.Host, uiRoot, 736, LargeHeight);

            var buttonView = GetSelectedButtonView(catalogHost.Catalog);
            var previewHost = Assert.IsType<ContentControl>(catalogHost.Catalog.FindName("PreviewHost"));
            var previewRight = previewHost.LayoutSlot.X + previewHost.LayoutSlot.Width + 0.5f;

            var accentButton = FindButtonByText(buttonView, "Accent");
            var dangerButton = FindButtonByText(buttonView, "Danger");
            var thickBorderButton = FindButtonByText(buttonView, "Thick border");
            var compactButton = FindButtonByText(buttonView, "Compact");
            var defaultButton = FindButtonByText(buttonView, "Default");
            var spaciousButton = FindButtonByText(buttonView, "Spacious");

            Assert.InRange(accentButton.LayoutSlot.X + accentButton.LayoutSlot.Width, 0f, previewRight);
            Assert.InRange(dangerButton.LayoutSlot.X + dangerButton.LayoutSlot.Width, 0f, previewRight);
            Assert.InRange(thickBorderButton.LayoutSlot.X + thickBorderButton.LayoutSlot.Width, 0f, previewRight);
            Assert.InRange(compactButton.LayoutSlot.X + compactButton.LayoutSlot.Width, 0f, previewRight);
            Assert.InRange(defaultButton.LayoutSlot.X + defaultButton.LayoutSlot.Width, 0f, previewRight);
            Assert.InRange(spaciousButton.LayoutSlot.X + spaciousButton.LayoutSlot.Width, 0f, previewRight);
        }
        finally
        {
            RestoreApplicationResources(backup);
            FocusManager.ClearFocus();
            FocusManager.ClearPointerCapture();
        }
    }

    private static (Grid Host, ControlsCatalogView Catalog, UiRoot UiRoot) CreateCatalogHost()
    {
        var host = new Grid
        {
            Width = LargeWidth,
            Height = LargeHeight
        };
        var catalog = new ControlsCatalogView();
        catalog.ShowControl("Button");
        host.AddChild(catalog);
        return (host, catalog, new UiRoot(host));
    }

    private static ButtonView GetSelectedButtonView(ControlsCatalogView catalog)
    {
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        return Assert.IsType<ButtonView>(previewHost.Content);
    }

    private static Button FindButtonByText(UIElement root, string text)
    {
        return FindFirstVisualChild<Button>(
                   root,
                   button => string.Equals(button.GetContentText(), text, StringComparison.Ordinal))
               ?? throw new InvalidOperationException($"Could not find button '{text}'.");
    }

    private static ButtonViewLayoutSnapshot CaptureButtonViewLayoutSnapshot(ControlsCatalogView catalog, ButtonView view)
    {
        var selectedControlLabel = Assert.IsType<Label>(catalog.FindName("SelectedControlLabel"));
        var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
        var wrappingDescription = FindTextBlockByText(
            view,
            "Wrapped button labels should be hosted in a TextBlock so they reflow inside the button's available width.");
        var alignmentDescription = FindTextBlockByText(
            view,
            "HorizontalAlignment controls whether the button sizes to content or fills its container.");
        var centeredButton = FindButtonByText(view, "Centered");
        var stretchedButton = FindButtonByText(view, "Stretched — fills available width");
        var leftAlignedButton = FindButtonByText(view, "Left-aligned");

        return new ButtonViewLayoutSnapshot(
            selectedControlLabel.LayoutSlot,
            previewHost.LayoutSlot,
            wrappingDescription.LayoutSlot,
            alignmentDescription.LayoutSlot,
            leftAlignedButton.LayoutSlot,
            centeredButton.LayoutSlot,
            stretchedButton.LayoutSlot);
    }

    private static TextBlock FindTextBlockByText(UIElement root, string text)
    {
        return FindFirstVisualChild<TextBlock>(
                   root,
                   block => string.Equals(block.Text, text, StringComparison.Ordinal))
               ?? throw new InvalidOperationException($"Could not find TextBlock '{text}'.");
    }

    private static T? FindFirstVisualChild<T>(UIElement root, Func<T, bool>? predicate = null)
        where T : UIElement
    {
        if (root is T typed && (predicate == null || predicate(typed)))
        {
            return typed;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var match = FindFirstVisualChild(child, predicate);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }


    private static void AssertSnapshotsClose(ButtonViewLayoutSnapshot expected, ButtonViewLayoutSnapshot actual)
    {
        Assert.True(
            SnapshotsClose(expected, actual),
            $"Expected final resized Button view layout to match fresh small layout.{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");
    }

    private static bool SnapshotsClose(ButtonViewLayoutSnapshot expected, ButtonViewLayoutSnapshot actual)
    {
        return AreClose(expected.SelectedControlLabel, actual.SelectedControlLabel) &&
               AreClose(expected.PreviewHost, actual.PreviewHost) &&
               AreClose(expected.WrappingDescription, actual.WrappingDescription) &&
               AreClose(expected.AlignmentDescription, actual.AlignmentDescription) &&
               AreClose(expected.LeftAlignedButton, actual.LeftAlignedButton) &&
               AreClose(expected.CenteredButton, actual.CenteredButton) &&
               AreClose(expected.StretchedButton, actual.StretchedButton);
    }

    private static bool AreClose(LayoutRect expected, LayoutRect actual)
    {
        return MathF.Abs(expected.X - actual.X) <= 0.5f &&
               MathF.Abs(expected.Y - actual.Y) <= 0.5f &&
               MathF.Abs(expected.Width - actual.Width) <= 0.5f &&
               MathF.Abs(expected.Height - actual.Height) <= 0.5f;
    }

    private static IReadOnlyList<string> DescribeVisuals(IReadOnlyList<UIElement> visuals)
    {
        return visuals.Select(DescribeVisual).ToArray();
    }

    private static string DescribeVisual(UIElement visual)
    {
        if (visual is Button button)
        {
            return $"Button[{button.GetContentText()}]";
        }

        if (visual is TextBlock textBlock)
        {
            var text = textBlock.Text ?? string.Empty;
            if (text.Length > 48)
            {
                text = text[..48];
            }

            return $"TextBlock[{text}]";
        }

        return visual switch
        {
            FrameworkElement { Name.Length: > 0 } frameworkElement => $"{frameworkElement.GetType().Name}#{frameworkElement.Name}",
            _ => visual.GetType().Name
        };
    }

    private static Vector2 GetCenter(UIElement element)
    {
        return new Vector2(
            element.LayoutSlot.X + (element.LayoutSlot.Width * 0.5f),
            element.LayoutSlot.Y + (element.LayoutSlot.Height * 0.5f));
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
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
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = pointerMoved || leftPressed || leftReleased,
            WheelDelta = 0,
            LeftPressed = leftPressed,
            LeftReleased = leftReleased,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void AdvanceFrames(FrameworkElement host, UiRoot uiRoot, int width, int height, int frameCount, int elapsedMs)
    {
        for (var i = 0; i < frameCount; i++)
        {
            RunFrame(host, uiRoot, width, height, totalMs: 16 + (i * elapsedMs), elapsedMs);
        }
    }

    private static void RunSteadyLayout(FrameworkElement host, UiRoot uiRoot, int width, int height)
    {
        RunFrame(host, uiRoot, width, height, totalMs: 16, elapsedMs: 16);
        RunFrame(host, uiRoot, width, height, totalMs: 32, elapsedMs: 16);
        RunFrame(host, uiRoot, width, height, totalMs: 48, elapsedMs: 16);
    }

    private static void RunFrame(FrameworkElement host, UiRoot uiRoot, int width, int height, int totalMs, int elapsedMs)
    {
        host.Width = width;
        host.Height = height;
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(totalMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }

    private static string FormatRect(LayoutRect rect)
    {
        return $"({rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###})";
    }

    private static string GetDiagnosticsLogPath(string fileNameWithoutExtension)
    {
        var root = FindRepositoryRoot();
        return Path.Combine(root, "artifacts", "diagnostics", $"{fileNameWithoutExtension}.txt");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.EnumerateFiles("InkkSlinger.sln").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test base directory.");
    }

    private static ResourceSnapshot CaptureApplicationResources()
    {
        var resources = UiApplication.Current.Resources;
        return new ResourceSnapshot(resources.ToList(), resources.MergedDictionaries.ToList());
    }

    private static void RestoreApplicationResources(ResourceSnapshot snapshot)
    {
        TestApplicationResources.Restore(snapshot.Entries, snapshot.MergedDictionaries);
    }

    private static void LoadRootAppResources()
    {
        var appPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "App.xml"));
        Assert.True(File.Exists(appPath), $"Expected App.xml at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private readonly record struct ResourceSnapshot(
        IReadOnlyList<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);

    private readonly record struct ButtonViewLayoutSnapshot(
        LayoutRect SelectedControlLabel,
        LayoutRect PreviewHost,
        LayoutRect WrappingDescription,
        LayoutRect AlignmentDescription,
        LayoutRect LeftAlignedButton,
        LayoutRect CenteredButton,
        LayoutRect StretchedButton)
    {
        public override string ToString()
        {
            return $"selected={FormatRect(SelectedControlLabel)}; preview={FormatRect(PreviewHost)}; wrappingText={FormatRect(WrappingDescription)}; alignmentText={FormatRect(AlignmentDescription)}; left={FormatRect(LeftAlignedButton)}; center={FormatRect(CenteredButton)}; stretch={FormatRect(StretchedButton)}";
        }
    }

}
