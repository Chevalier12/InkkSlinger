using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public class UiRootTelemetryTests
{
    [Fact]
    public void Constructor_EnablesRetainedRenderingAndDirtyRegionsByDefault()
    {
        var uiRoot = new UiRoot(new Panel());

        Assert.True(uiRoot.UseRetainedRenderList);
        Assert.True(uiRoot.UseDirtyRegionRendering);
        Assert.True(uiRoot.GetMetricsSnapshot().UseRetainedRenderList);
        Assert.True(uiRoot.GetMetricsSnapshot().UseDirtyRegionRendering);
    }

    [Fact]
    public void MetricsSnapshot_TracksLayoutAndDrawSkipCounters()
    {
        AnimationManager.Current.ResetForTests();
        var root = new Panel();
        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 1280, 720);

        uiRoot.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);
        _ = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)), viewport);

        uiRoot.CompleteDrawStateForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);
        _ = uiRoot.ShouldDrawThisFrame(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);

        var snapshot = uiRoot.GetMetricsSnapshot();
        Assert.True(snapshot.LayoutExecutedFrameCount >= 1);
        Assert.True(snapshot.LayoutSkippedFrameCount >= 1);
        Assert.True(snapshot.DrawSkippedFrameCount >= 1);
    }

    [Fact]
    public void MetricsSnapshot_ReflectsRolloutToggleStates()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root)
        {
            UseRetainedRenderList = false,
            UseDirtyRegionRendering = false,
            UseConditionalDrawScheduling = false
        };

        var snapshot = uiRoot.GetMetricsSnapshot();
        Assert.False(snapshot.UseRetainedRenderList);
        Assert.False(snapshot.UseDirtyRegionRendering);
        Assert.False(snapshot.UseConditionalDrawScheduling);
    }

    [Fact]
    public void DisablingDirtyRegionRendering_SkipsDirtyRegionAccumulation()
    {
        var root = new Panel();
        var child = new Border();
        root.AddChild(child);
        child.SetLayoutSlot(new LayoutRect(10f, 10f, 20f, 20f));

        var uiRoot = new UiRoot(root)
        {
            UseDirtyRegionRendering = false
        };
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();

        child.InvalidateVisual();

        Assert.Empty(uiRoot.GetDirtyRegionsSnapshotForTests());
        Assert.False(uiRoot.IsFullDirtyForTests());
    }

    [Fact]
    public void VisualTreeMetricsSnapshot_AggregatesLayoutAndUpdateCounters()
    {
        var root = new StackPanel();
        root.AddChild(new Label { Content = "Header" });
        root.AddChild(new ProgressBar { IsIndeterminate = true });
        root.AddChild(new Border { Child = new Label { Content = "Body" } });

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        var snapshot = uiRoot.GetVisualTreeMetricsSnapshot();
        var workSnapshot = uiRoot.GetVisualTreeWorkMetricsSnapshotForTests();

        Assert.True(snapshot.VisualCount >= 4);
        Assert.True(snapshot.FrameworkElementCount >= 4);
        Assert.True(snapshot.HighCostVisualCount >= 2);
        Assert.True(snapshot.MaxDepth >= 2);
        Assert.True(snapshot.MeasureCallCount >= 4);
        Assert.True(snapshot.ArrangeCallCount >= 4);
        Assert.True(workSnapshot.MeasureWorkCount >= 4);
        Assert.True(workSnapshot.ArrangeWorkCount >= 4);
        Assert.True(workSnapshot.MeasureWorkCount <= snapshot.MeasureCallCount);
        Assert.True(workSnapshot.ArrangeWorkCount <= snapshot.ArrangeCallCount);
        Assert.Equal(1, snapshot.UpdateCallCount);
        Assert.True(snapshot.MeasureInvalidationCount >= 1);

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, perfSnapshot.FrameUpdateParticipantCount);
    }

    [Fact]
    public void PassiveVisualTree_UpdatePhase_DoesNotWalkNonParticipantVisuals()
    {
        var root = new StackPanel();
        root.AddChild(new Label { Content = "Header" });
        root.AddChild(new Border { Child = new Label { Content = "Body" } });

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 800, 600));

        var snapshot = uiRoot.GetVisualTreeMetricsSnapshot();
        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();

        Assert.Equal(0, snapshot.UpdateCallCount);
        Assert.Equal(0, perfSnapshot.FrameUpdateParticipantCount);
    }

    [Fact]
    public void FrameUpdateParticipants_TrackVisualAttachAndDetach()
    {
        var root = new Panel();
        var progressBar = new ProgressBar
        {
            IsIndeterminate = true
        };
        root.AddChild(progressBar);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 180));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);

        root.RemoveChild(progressBar);
        progressBar.IsIndeterminate = false;

        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, 320, 180));
        Assert.Equal(0, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);
    }

    [Fact]
    public void PerformanceTelemetry_StableActiveUpdateParticipants_DoNotRefreshRegistryEachFrame()
    {
        var root = new Panel();
        root.AddChild(new ProgressBar
        {
            IsIndeterminate = true
        });

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 320, 180);

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        var first = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, first.FrameUpdateParticipantCount);
        Assert.Equal(1, first.FrameUpdateParticipantRefreshCount);

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);
        var second = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, second.FrameUpdateParticipantCount);
        Assert.Equal(0, second.FrameUpdateParticipantRefreshCount);
    }

    [Fact]
    public void PerformanceTelemetry_RenderOnlyParticipantActivation_RefreshesRegistryWhenVisualAlreadyDirty()
    {
        var root = new Panel();
        var progressBar = new ProgressBar();
        root.AddChild(progressBar);

        var uiRoot = new UiRoot(root);
        var viewport = new Viewport(0, 0, 320, 180);

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), viewport);
        Assert.Equal(0, uiRoot.GetPerformanceTelemetrySnapshotForTests().FrameUpdateParticipantCount);

        uiRoot.ResetDirtyStateForTests();
        uiRoot.CompleteDrawStateForTests();
        root.ClearRenderInvalidationRecursive();

        progressBar.InvalidateVisual();
        progressBar.IsIndeterminate = true;

        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16)), viewport);

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, perfSnapshot.FrameUpdateParticipantCount);
        Assert.Equal(1, progressBar.UpdateCallCount);
    }

    [Fact]
    public void PerformanceTelemetry_ContextMenuOpenState_ReusesMaintainedOverlayRegistry()
    {
        var host = new Canvas
        {
            Width = 320f,
            Height = 200f
        };
        var button = new Button
        {
            Width = 120f,
            Height = 36f,
            Content = "Open"
        };
        host.AddChild(button);
        Canvas.SetLeft(button, 24f);
        Canvas.SetTop(button, 24f);

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(new MenuItem { Header = "Item" });

        var uiRoot = new UiRoot(host);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 200));

        contextMenu.OpenAt(host, 120f, 80f, button);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(125f, 85f), pointerMoved: true));

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(0, perfSnapshot.OverlayRegistryScanCount);
        Assert.Equal("ContextMenuOpenHitTest", uiRoot.LastPointerResolvePathForDiagnostics);
    }

    [Fact]
    public void PerformanceTelemetry_KeyboardMenuScope_ReusesStableScopeAcrossKeyEvents()
    {
        var root = new StackPanel();
        var menu = new Menu();
        var fileItem = new MenuItem { Header = "_File" };
        fileItem.Items.Add(new MenuItem { Header = "Open" });
        menu.Items.Add(fileItem);
        var editor = new TextBox();
        root.AddChild(menu);
        root.AddChild(editor);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 400, 240));
        uiRoot.SetFocusedElementForTests(editor);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.A));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().MenuScopeBuildCount);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.B));

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(1, perfSnapshot.MenuScopeBuildCount);
        Assert.False(menu.IsMenuMode);
    }

    [Fact]
    public void PerformanceTelemetry_MenuStateMutation_InvalidatesKeyboardMenuScopeCache()
    {
        var root = new Canvas
        {
            Width = 960f,
            Height = 320f
        };

        var leftMenu = BuildMenu("_File", "_Edit", out _, out _);
        root.AddChild(leftMenu);
        Canvas.SetLeft(leftMenu, 20f);
        Canvas.SetTop(leftMenu, 20f);

        var rightMenu = BuildMenu("_File", "_Edit", out var rightFile, out _);
        root.AddChild(rightMenu);
        Canvas.SetLeft(rightMenu, 520f);
        Canvas.SetTop(rightMenu, 20f);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 960, 320));
        uiRoot.SetFocusedElementForTests(leftMenu);

        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.A));
        Assert.Equal(1, uiRoot.GetPerformanceTelemetrySnapshotForTests().MenuScopeBuildCount);

        rightMenu.EnterMenuMode(rightFile, leftMenu);
        uiRoot.RunInputDeltaForTests(CreateKeyDownDelta(Keys.B));

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(2, perfSnapshot.MenuScopeBuildCount);
        Assert.True(rightMenu.IsMenuMode);
    }

    [Fact]
    public void PerformanceTelemetry_WheelOnlyInput_DoesNotRequeryCommands()
    {
        FocusManager.ClearFocus();
        try
        {
            var root = new StackPanel();
            var target = new TextBox();
            var source = new Button();
            root.AddChild(target);
            root.AddChild(source);

            var command = new RoutedCommand("Probe", typeof(UiRootTelemetryTests));
            var canExecuteProbeCount = 0;
            target.CommandBindings.Add(
                new CommandBinding(
                    command,
                    (_, _) => { },
                    (_, args) =>
                    {
                        canExecuteProbeCount++;
                        args.CanExecute = true;
                    }));

            source.CommandTarget = target;
            source.Command = command;

            var uiRoot = new UiRoot(root);
            uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 200));

            canExecuteProbeCount = 0;
            uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(24f, 24f), wheelDelta: -120));

            Assert.Equal(0, canExecuteProbeCount);
            Assert.True(source.IsEnabled);
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void PerformanceTelemetry_InputCaches_StructureMutation_EvictsDisconnectedEntriesImmediately()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 200f));

        var scrollViewer = CreateTelemetryScrollViewer();
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 200));

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(40f, 40f), wheelDelta: -120));

        var populatedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.True(populatedCounts.ConnectionCacheEntryCount > 0 || populatedCounts.AncestorCacheEntryCount > 0);

        root.RemoveChild(scrollViewer);

        var preservedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.Equal(0, preservedCounts.ConnectionCacheEntryCount);
        Assert.Equal(0, preservedCounts.AncestorCacheEntryCount);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(new Vector2(40f, 40f), pointerMoved: true));

        Assert.NotSame(scrollViewer, uiRoot.GetHoveredElementForDiagnostics());
    }

    [Fact]
    public void PerformanceTelemetry_InputCaches_StructureMutation_EvictsOnlyChangedSubtree()
    {
        var root = new Grid();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 400f, 220f));
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var leftViewer = CreateTelemetryScrollViewer();
        Grid.SetColumn(leftViewer, 0);
        root.AddChild(leftViewer);

        var rightViewer = CreateTelemetryScrollViewer();
        Grid.SetColumn(rightViewer, 1);
        root.AddChild(rightViewer);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 400, 220));

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(40f, 40f), wheelDelta: -120));
        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(240f, 40f), wheelDelta: -120));

        var populatedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        var totalPopulatedCount = populatedCounts.ConnectionCacheEntryCount + populatedCounts.AncestorCacheEntryCount;
        Assert.True(totalPopulatedCount > 0);

        root.RemoveChild(leftViewer);

        var preservedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        var totalPreservedCount = preservedCounts.ConnectionCacheEntryCount + preservedCounts.AncestorCacheEntryCount;
        Assert.True(totalPreservedCount > 0);
        Assert.True(totalPreservedCount < totalPopulatedCount);
    }

    [Fact]
    public void WheelScroll_WithinScrollViewer_PreservesStationaryButtonHoverUntilPointerMoves()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 240f));

        var stack = new StackPanel();
        var buttons = new List<Button>();
        for (var i = 0; i < 8; i++)
        {
            var button = new Button
            {
                Content = $"Item {i}",
                Width = 180f,
                Height = 40f
            };
            buttons.Add(button);
            stack.AddChild(button);
        }

        var viewer = new ScrollViewer
        {
            Content = stack,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(viewer);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 240));

        var pointer = GetCenter(buttons[0].LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        Assert.Same(buttons[0], uiRoot.GetHoveredElementForDiagnostics());
        Assert.True(buttons[0].IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(pointer, wheelDelta: -120));

        Assert.Same(buttons[0], uiRoot.GetHoveredElementForDiagnostics());
        Assert.True(buttons[0].IsMouseOver);
        Assert.False(buttons[1].IsMouseOver);

        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, pointerMoved: true));

        Assert.Same(buttons[1], uiRoot.GetHoveredElementForDiagnostics());
        Assert.False(buttons[0].IsMouseOver);
        Assert.True(buttons[1].IsMouseOver);
    }

    [Fact]
    public void PerformanceTelemetry_RenderInvalidation_PreservesStructuralInputCaches()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 200f));

        var content = new StackPanel();
        content.AddChild(new Border { Height = 40f });
        content.AddChild(new Border { Height = 220f });

        var scrollViewer = new ScrollViewer
        {
            Content = content,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        uiRoot.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)), new Viewport(0, 0, 320, 200));

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(40f, 40f), wheelDelta: -120));

        var populatedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.True(populatedCounts.ConnectionCacheEntryCount > 0 || populatedCounts.AncestorCacheEntryCount > 0);

        scrollViewer.Opacity = 0.5f;

        var preservedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.Equal(populatedCounts.ConnectionCacheEntryCount, preservedCounts.ConnectionCacheEntryCount);
        Assert.Equal(populatedCounts.AncestorCacheEntryCount, preservedCounts.AncestorCacheEntryCount);
    }

    [Fact]
    public void PerformanceTelemetry_LayoutPass_PreservesStructuralInputCaches()
    {
        var root = new Panel();
        root.SetLayoutSlot(new LayoutRect(0f, 0f, 320f, 200f));

        var content = new StackPanel();
        content.AddChild(new Border { Height = 40f });
        content.AddChild(new Border { Height = 220f });

        var scrollViewer = new ScrollViewer
        {
            Content = content,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.AddChild(scrollViewer);

        var uiRoot = new UiRoot(root);
        var firstFrame = new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var secondFrame = new GameTime(TimeSpan.FromMilliseconds(32), TimeSpan.FromMilliseconds(16));
        var viewport = new Viewport(0, 0, 320, 200);
        uiRoot.Update(firstFrame, viewport);

        uiRoot.RunInputDeltaForTests(CreatePointerWheelDelta(new Vector2(40f, 40f), wheelDelta: -120));

        var populatedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.True(populatedCounts.ConnectionCacheEntryCount > 0 || populatedCounts.AncestorCacheEntryCount > 0);

        scrollViewer.Width = 200f;
        uiRoot.Update(secondFrame, viewport);

        var preservedCounts = uiRoot.GetInputCacheEntryCountsForTests();
        Assert.Equal(populatedCounts.ConnectionCacheEntryCount, preservedCounts.ConnectionCacheEntryCount);
        Assert.Equal(populatedCounts.AncestorCacheEntryCount, preservedCounts.AncestorCacheEntryCount);
    }

    [Fact]
    public void MetricsSnapshot_TracksRetainedTreeRebuildsAndStructureChanges()
    {
        var root = new Panel();
        var uiRoot = new UiRoot(root);

        var child = new Border();
        root.AddChild(child);

        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        child.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var snapshot = uiRoot.GetMetricsSnapshot();

        Assert.True(snapshot.VisualStructureChangeCount >= 1);
        Assert.True(snapshot.RetainedFullRebuildCount >= 1);
        Assert.True(snapshot.RetainedSubtreeSyncCount >= 1);
        Assert.True(snapshot.RetainedRenderNodeCount >= 2);
        Assert.True(snapshot.LastRetainedDirtyVisualCount >= 1);
    }

    [Fact]
    public void PerformanceTelemetry_TracksAncestorMetadataRefreshWork()
    {
        var root = new Panel();
        var parent = new Panel();
        var left = new Border();
        var right = new Border();
        parent.AddChild(left);
        parent.AddChild(right);
        root.AddChild(parent);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();
        uiRoot.SynchronizeRetainedRenderListForTests();

        var perfSnapshot = uiRoot.GetPerformanceTelemetrySnapshotForTests();
        Assert.Equal(2, perfSnapshot.AncestorMetadataRefreshNodeCount);
    }

    [Fact]
    public void DirtyRootTelemetry_AfterRetainedSync_PreservesCoalescedAndSynchronizedRootSummaries()
    {
        var root = new Panel();
        var left = new Border { Name = "Left" };
        var right = new Border { Name = "Right" };
        root.AddChild(left);
        root.AddChild(right);

        var uiRoot = new UiRoot(root);
        uiRoot.RebuildRenderListForTests();
        uiRoot.ResetDirtyStateForTests();
        root.ClearRenderInvalidationRecursive();
        uiRoot.CompleteDrawStateForTests();

        left.InvalidateVisual();
        right.InvalidateVisual();

        uiRoot.SynchronizeRetainedRenderListForTests();

        var dirtyQueueSummary = uiRoot.GetDirtyRenderQueueSummaryForTests();
        var syncedRootSummary = uiRoot.GetLastSynchronizedDirtyRootSummaryForTests();

        Assert.Equal("Border#Left | Border#Right", dirtyQueueSummary);
        Assert.Equal("Border#Left | Border#Right", syncedRootSummary);
    }

    private static InputDelta CreateKeyDownDelta(Keys key, KeyboardState? keyboard = null)
    {
        var state = keyboard ?? default;
        var pointer = new Vector2(12f, 12f);
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(state, default, pointer),
            PressedKeys = new List<Keys> { key },
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = 0,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerDelta(Vector2 pointer, bool pointerMoved)
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
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static InputDelta CreatePointerWheelDelta(Vector2 pointer, int wheelDelta)
    {
        return new InputDelta
        {
            Previous = new InputSnapshot(default, default, pointer),
            Current = new InputSnapshot(default, default, pointer),
            PressedKeys = new List<Keys>(),
            ReleasedKeys = new List<Keys>(),
            TextInput = new List<char>(),
            PointerMoved = false,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static ScrollViewer CreateTelemetryScrollViewer()
    {
        var content = new StackPanel();
        content.AddChild(new Border { Height = 40f });
        content.AddChild(new Border { Height = 220f });

        return new ScrollViewer
        {
            Content = content,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static Menu BuildMenu(
        string fileHeader,
        string editHeader,
        out MenuItem file,
        out MenuItem edit)
    {
        var menu = new Menu
        {
            Width = 300f,
            Height = 30f
        };

        file = new MenuItem { Header = fileHeader };
        file.Items.Add(new MenuItem { Header = "New" });
        file.Items.Add(new MenuItem { Header = "Open" });

        edit = new MenuItem { Header = editHeader };
        edit.Items.Add(new MenuItem { Header = "Undo" });

        menu.Items.Add(file);
        menu.Items.Add(edit);
        return menu;
    }
}
