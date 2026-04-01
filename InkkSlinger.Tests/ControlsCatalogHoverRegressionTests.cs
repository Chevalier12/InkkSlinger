using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ControlsCatalogHoverRegressionTests
{
    [Fact]
    public void HoveringFromViewerGutterIntoButton_ShouldActivateButtonHover()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var button = host.Children.OfType<Button>().First();

        var verticalBar = viewer!.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, gutterPoint);

        Assert.False(button.IsMouseOver);

        var buttonPoint = new Vector2(
            button.LayoutSlot.X + (button.LayoutSlot.Width * 0.5f),
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, buttonPoint);

        Assert.True(
            button.IsMouseOver,
            $"Expected button hover to recover after moving from viewer gutter. button={button.GetContentText()}, gutter=({gutterPoint.X:0.###},{gutterPoint.Y:0.###}), buttonPoint=({buttonPoint.X:0.###},{buttonPoint.Y:0.###})");
    }

    [Fact]
    public void HoveringScrolledSidebarButton_InvalidatesTemplatedButtonOwner()
    {
        var view = new ControlsCatalogView();
        var uiRoot = new UiRoot(view);
        RunLayout(uiRoot, 1280, 820, 16);

        var viewer = FindFirstVisualChild<ScrollViewer>(view);
        Assert.NotNull(viewer);

        viewer!.ScrollToVerticalOffset(320f);
        RunLayout(uiRoot, 1280, 820, 32);

        var host = Assert.IsType<StackPanel>(view.FindName("ControlButtonsHost"));
        var viewport = GetViewerViewportRect(viewer);

        var verticalBar = viewer.GetVisualChildren()
            .OfType<ScrollBar>()
            .FirstOrDefault(static bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);
        Assert.NotNull(verticalBar);

        var (button, buttonPoint) = FindVisibleSidebarButtonHit(view, host, viewport, verticalBar!.LayoutSlot.X);

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        view.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        var gutterPoint = new Vector2(
            verticalBar!.LayoutSlot.X - 0.25f,
            button.LayoutSlot.Y + (button.LayoutSlot.Height * 0.5f));
        MovePointer(uiRoot, gutterPoint);

        MovePointer(uiRoot, buttonPoint);
        uiRoot.SynchronizeRetainedRenderListForTests();

        var invalidationSnapshot = uiRoot.GetRenderInvalidationDebugSnapshotForTests();
        var dirtyRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();

        Assert.True(button.IsMouseOver);
        Assert.Equal("ScrollViewer", invalidationSnapshot.EffectiveSourceType);
        Assert.Equal("ScrollViewer", invalidationSnapshot.DirtyBoundsVisualType);
        Assert.Equal("Button", dirtyRootSummary);
    }

    [Fact]
    public void HoveringFromListBoxIntoSidebarButton_ShouldActivateSidebarHover()
    {
        var root = new Canvas
        {
            Width = 1000f,
            Height = 700f
        };

        var sidebarHost = new StackPanel();
        for (var i = 0; i < 18; i++)
        {
            sidebarHost.AddChild(new Button
            {
                Content = $"Control {i}",
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        var sidebarViewer = new ScrollViewer
        {
            Width = 260f,
            Height = 620f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = sidebarHost
        };
        root.AddChild(sidebarViewer);
        Canvas.SetLeft(sidebarViewer, 12f);
        Canvas.SetTop(sidebarViewer, 12f);

        var listBox = new ListBox
        {
            Width = 340f,
            Height = 260f
        };
        listBox.Items.Add("Alpha");
        listBox.Items.Add("Beta");
        listBox.Items.Add("Gamma");
        listBox.Items.Add("Delta");
        root.AddChild(listBox);
        Canvas.SetLeft(listBox, 340f);
        Canvas.SetTop(listBox, 56f);

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 1000, 700, 16);

        var previewItemPoint = new Vector2(
            listBox.LayoutSlot.X + MathF.Max(12f, listBox.LayoutSlot.Width * 0.35f),
            listBox.LayoutSlot.Y + MathF.Max(12f, listBox.LayoutSlot.Height * 0.25f));
        var previewHit = VisualTreeHelper.HitTest(root, previewItemPoint);
        Assert.NotNull(FindAncestor<ListBoxItem>(previewHit));
        MovePointer(uiRoot, previewItemPoint);

        var firstSidebarButton = sidebarHost.Children.OfType<Button>().First();
        var sidebarButtonPoint = new Vector2(
            firstSidebarButton.LayoutSlot.X + (firstSidebarButton.LayoutSlot.Width * 0.5f),
            firstSidebarButton.LayoutSlot.Y + (firstSidebarButton.LayoutSlot.Height * 0.5f));
        var preMoveHit = VisualTreeHelper.HitTest(root, sidebarButtonPoint);
        var preMoveButton = FindAncestor<Button>(preMoveHit);
        MovePointer(uiRoot, sidebarButtonPoint);

        Assert.True(
            firstSidebarButton.IsMouseOver,
            $"Expected sidebar hover to activate after leaving ListBox. sidebar={firstSidebarButton.GetContentText()}, listBoxPoint=({previewItemPoint.X:0.###},{previewItemPoint.Y:0.###}), sidebarPoint=({sidebarButtonPoint.X:0.###},{sidebarButtonPoint.Y:0.###}), preMoveHit={preMoveHit?.GetType().Name ?? "null"}, preMoveButton={preMoveButton?.GetContentText() ?? "null"}");
    }

    [Fact]
    public void HoveringRenderedExpanderHeader_ShouldNotResolveToolbarButtons()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 1920f,
                Height = 991f
            };
            var catalog = new ControlsCatalogView
            {
                Width = 1920f,
                Height = 991f
            };
            host.AddChild(catalog);
            catalog.ShowControl("Expander");

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 1920, 991, 16);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var previewRoot = Assert.IsType<ExpanderView>(previewHost.Content);
            var previewViewer = FindFirstVisualChild<ScrollViewer>(previewRoot);
            Assert.NotNull(previewViewer);
            var expander = Assert.IsType<Expander>(previewRoot.FindName("PlaygroundExpander"));
            var expandButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundExpandButton"));
            var collapseButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundCollapseButton"));
            var toggleButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundToggleButton"));
            var resetButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundResetButton"));

            previewViewer!.ScrollToVerticalOffset(96f);
            RunLayout(uiRoot, 1920, 991, 32);

            var snapshot = expander.GetExpanderSnapshotForDiagnostics();
            var renderedHeaderRect = new LayoutRect(
                snapshot.HeaderRectX,
                snapshot.HeaderRectY - previewViewer.VerticalOffset,
                snapshot.HeaderRectWidth,
                snapshot.HeaderRectHeight);

            Button? badButton = null;
            UIElement? badHit = null;
            Vector2 badPoint = default;

            var minX = Math.Max(0, (int)MathF.Floor(renderedHeaderRect.X));
            var maxX = Math.Max(minX, (int)MathF.Ceiling(renderedHeaderRect.X + MathF.Min(renderedHeaderRect.Width, 220f)));
            var minY = Math.Max(0, (int)MathF.Floor(renderedHeaderRect.Y));
            var maxY = Math.Max(minY, (int)MathF.Ceiling(renderedHeaderRect.Y + renderedHeaderRect.Height));

            for (var y = minY; y < maxY && badButton == null; y += 2)
            {
                for (var x = minX; x < maxX; x += 2)
                {
                    var point = new Vector2(x, y);
                    var hit = VisualTreeHelper.HitTest(host, point);
                    var button = FindAncestor<Button>(hit);
                    if (ReferenceEquals(button, expandButton) ||
                        ReferenceEquals(button, collapseButton) ||
                        ReferenceEquals(button, toggleButton) ||
                        ReferenceEquals(button, resetButton))
                    {
                        badButton = button;
                        badHit = hit;
                        badPoint = point;
                        break;
                    }
                }
            }

            Assert.True(
                badButton == null,
                $"Expected rendered Expander header area to stay isolated from toolbar buttons after scroll, but point=({badPoint.X:0.###},{badPoint.Y:0.###}) hit={badHit?.GetType().Name ?? "null"} button={badButton?.GetContentText() ?? "null"} header=({renderedHeaderRect.X:0.###},{renderedHeaderRect.Y:0.###},{renderedHeaderRect.Width:0.###},{renderedHeaderRect.Height:0.###}) viewerOffset={previewViewer.VerticalOffset:0.###}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void MovingFromExpanderSubtitleIntoHeaderEdge_ShouldNotActivateToolbarHover()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 1920f,
                Height = 991f
            };
            var catalog = new ControlsCatalogView
            {
                Width = 1920f,
                Height = 991f
            };
            host.AddChild(catalog);
            catalog.ShowControl("Expander");

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 1920, 991, 16);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var previewRoot = Assert.IsType<ExpanderView>(previewHost.Content);
            var previewViewer = FindFirstVisualChild<ScrollViewer>(previewRoot);
            Assert.NotNull(previewViewer);
            var expander = Assert.IsType<Expander>(previewRoot.FindName("PlaygroundExpander"));
            var expandButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundExpandButton"));
            var collapseButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundCollapseButton"));
            var toggleButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundToggleButton"));
            var resetButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundResetButton"));
            var headerStack = Assert.IsType<StackPanel>(expander.Header);
            var titleText = Assert.IsType<TextBlock>(headerStack.Children[0]);
            var subtitleText = Assert.IsType<TextBlock>(headerStack.Children[1]);

            previewViewer!.ScrollToVerticalOffset(96f);
            RunLayout(uiRoot, 1920, 991, 48);

            var subtitlePoint = GetScrolledCenter(subtitleText.LayoutSlot, previewViewer.VerticalOffset);
            MovePointer(uiRoot, subtitlePoint);

            var snapshot = expander.GetExpanderSnapshotForDiagnostics();
            var edgePoint = new Vector2(
                snapshot.HeaderRectX - 3f,
                snapshot.HeaderRectY - previewViewer.VerticalOffset + MathF.Min(snapshot.HeaderRectHeight - 1f, 32f));

            Button? badButton = null;
            Vector2 badPoint = default;
            string? badPath = null;

            var sweepSteps = 24;
            for (var i = 1; i <= sweepSteps; i++)
            {
                var point = Vector2.Lerp(subtitlePoint, edgePoint, i / (float)sweepSteps);
                MovePointer(uiRoot, point);

                if (expandButton.IsMouseOver || collapseButton.IsMouseOver || toggleButton.IsMouseOver || resetButton.IsMouseOver)
                {
                    badButton = new[] { expandButton, collapseButton, toggleButton, resetButton }.First(static button => button.IsMouseOver);
                    badPoint = point;
                    badPath = uiRoot.LastPointerResolvePathForDiagnostics;
                    break;
                }
            }

            Assert.True(
                badButton == null,
                $"Expected moving from the rendered Expander subtitle into the header edge to keep toolbar hover off, but point=({badPoint.X:0.###},{badPoint.Y:0.###}) activated={badButton?.GetContentText() ?? "null"} resolvePath={badPath ?? "null"} subtitle=({subtitlePoint.X:0.###},{subtitlePoint.Y:0.###}) edge=({edgePoint.X:0.###},{edgePoint.Y:0.###}) viewerOffset={previewViewer.VerticalOffset:0.###} titleText={titleText.Text}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    [Fact]
    public void ArtifactReplay_ExpanderHeaderTrace_ShouldNotActivateToolbarHover()
    {
        var backup = CaptureApplicationResources();
        try
        {
            LoadRootAppResources();

            var host = new Canvas
            {
                Width = 1920f,
                Height = 991f
            };
            var catalog = new ControlsCatalogView
            {
                Width = 1920f,
                Height = 991f
            };
            host.AddChild(catalog);
            catalog.ShowControl("Expander");

            var uiRoot = new UiRoot(host);
            RunLayout(uiRoot, 1920, 991, 16);

            var previewHost = Assert.IsType<ContentControl>(catalog.FindName("PreviewHost"));
            var previewRoot = Assert.IsType<ExpanderView>(previewHost.Content);
            var previewViewer = FindFirstVisualChild<ScrollViewer>(previewRoot);
            Assert.NotNull(previewViewer);
            var expandButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundExpandButton"));
            var collapseButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundCollapseButton"));
            var toggleButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundToggleButton"));
            var resetButton = Assert.IsType<Button>(previewRoot.FindName("PlaygroundResetButton"));

            previewViewer!.ScrollToVerticalOffset(96f);
            RunLayout(uiRoot, 1920, 991, 32);

            var trace = new[]
            {
                new Vector2(374f, 313f),
                new Vector2(373f, 310f),
                new Vector2(371f, 306f),
                new Vector2(369f, 303f),
                new Vector2(367f, 300f),
                new Vector2(366f, 299f),
                new Vector2(366f, 297f),
                new Vector2(364f, 295f),
                new Vector2(362f, 293f),
                new Vector2(359f, 291f),
                new Vector2(359f, 290f),
                new Vector2(358f, 290f),
                new Vector2(357f, 290f),
                new Vector2(356f, 290f),
                new Vector2(356f, 289f),
                new Vector2(355f, 289f),
                new Vector2(354f, 289f),
                new Vector2(353f, 289f)
            };

            Button? badButton = null;
            Vector2 badPoint = default;
            string? badPath = null;

            foreach (var point in trace)
            {
                MovePointer(uiRoot, point);
                if (expandButton.IsMouseOver || collapseButton.IsMouseOver || toggleButton.IsMouseOver || resetButton.IsMouseOver)
                {
                    badButton = new[] { expandButton, collapseButton, toggleButton, resetButton }.First(static button => button.IsMouseOver);
                    badPoint = point;
                    badPath = uiRoot.LastPointerResolvePathForDiagnostics;
                    break;
                }
            }

            Assert.True(
                badButton == null,
                $"Expected artifact replay trace to stay off toolbar buttons, but point=({badPoint.X:0.###},{badPoint.Y:0.###}) activated={badButton?.GetContentText() ?? "null"} resolvePath={badPath ?? "null"} viewerOffset={previewViewer.VerticalOffset:0.###}.");
        }
        finally
        {
            RestoreApplicationResources(backup);
        }
    }

    private static void MovePointer(UiRoot uiRoot, Vector2 point)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(point, pointerMoved: true));
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static Vector2 GetScrolledCenter(LayoutRect rect, float verticalOffset)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f) - verticalOffset);
    }

    private static LayoutRect GetViewerViewportRect(ScrollViewer viewer)
    {
        if (viewer.TryGetContentViewportClipRect(out var viewport))
        {
            return viewport;
        }

        throw new InvalidOperationException("Sidebar ScrollViewer did not expose a viewport.");
    }

    private static bool Intersects(LayoutRect left, LayoutRect right)
    {
        return left.X < right.X + right.Width &&
               right.X < left.X + left.Width &&
               left.Y < right.Y + right.Height &&
               right.Y < left.Y + left.Height;
    }

    private static (Button Button, Vector2 Point) FindVisibleSidebarButtonHit(
        UIElement root,
        StackPanel host,
        LayoutRect viewport,
        float scrollbarLeft)
    {
        var minX = Math.Max(0, (int)MathF.Floor(viewport.X));
        var maxX = Math.Max(minX, (int)MathF.Ceiling(MathF.Min(scrollbarLeft - 1f, viewport.X + viewport.Width)));
        var minY = Math.Max(0, (int)MathF.Floor(viewport.Y));
        var maxY = Math.Max(minY, (int)MathF.Ceiling(viewport.Y + viewport.Height));

        for (var y = minY; y < maxY; y += 2)
        {
            for (var x = minX; x < maxX; x += 2)
            {
                var point = new Vector2(x, y);
                var hit = VisualTreeHelper.HitTest(root, point);
                var button = FindAncestor<Button>(hit);
                if (button != null && host.Children.OfType<Button>().Contains(button))
                {
                    return (button, point);
                }
            }
        }

        throw new InvalidOperationException("Could not locate visible sidebar button hit point.");
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

    private static TElement? FindFirstVisualChild<TElement>(UIElement root, Func<TElement, bool>? predicate = null)
        where TElement : UIElement
    {
        if (root is TElement match && (predicate == null || predicate(match)))
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualChild(child, predicate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TElement? FindAncestor<TElement>(UIElement? start)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement match)
            {
                return match;
            }
        }

        return null;
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
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
        Assert.True(File.Exists(appPath), $"Expected App.xml to exist at '{appPath}'.");
        XamlLoader.LoadApplicationResourcesFromFile(appPath, clearExisting: true);
    }

    private readonly record struct ResourceSnapshot(
        IReadOnlyList<KeyValuePair<object, object>> Entries,
        IReadOnlyList<ResourceDictionary> MergedDictionaries);
}
