using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Xunit;
using Vector2 = System.Numerics.Vector2;

namespace InkkSlinger.Tests;

public sealed class InkkOopsCommandIntegrationTests
{
    [Fact]
    public void TargetResolver_Prefers_XName_Then_AutomationId_Then_AutomationName()
    {
        var root = new Canvas();
        var named = new Button { Name = "Shared", Content = "Named", Width = 120f, Height = 32f };
        var automationId = new Button { Content = "IdOnly", Width = 120f, Height = 32f };
        var automationName = new Button { Content = "NameOnly", Width = 120f, Height = 32f };
        AutomationProperties.SetAutomationId(automationId, "IdMatch");
        AutomationProperties.SetName(automationName, "NameMatch");
        root.AddChild(named);
        root.AddChild(automationId);
        root.AddChild(automationName);

        using var host = new InkkOopsTestHost(root);

        var xNameReport = InkkOopsTargetResolver.Resolve(host, new InkkOopsTargetReference("Shared"));
        var idReport = InkkOopsTargetResolver.Resolve(host, new InkkOopsTargetReference("IdMatch"));
        var nameReport = InkkOopsTargetResolver.Resolve(host, new InkkOopsTargetReference("NameMatch"));

        Assert.Equal(InkkOopsTargetResolutionSource.XName, xNameReport.Source);
        Assert.Same(named, xNameReport.Element);
        Assert.Equal(InkkOopsTargetResolutionSource.AutomationId, idReport.Source);
        Assert.Same(automationId, idReport.Element);
        Assert.Equal(InkkOopsTargetResolutionSource.AutomationName, nameReport.Source);
        Assert.Same(automationName, nameReport.Element);
    }

    [Fact]
    public async Task Hover_And_Click_Commands_Update_Button_State()
    {
        var button = new Button
        {
            Name = "TargetButton",
            Content = "Click Me",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "hover-click");
        var session = new InkkOopsSession(host, artifacts);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        var framesBeforeHover = host.AdvancedFrameCount;

        await new InkkOopsHoverTargetCommand(new InkkOopsTargetReference("TargetButton")).ExecuteAsync(session);
        await new InkkOopsClickTargetCommand(new InkkOopsTargetReference("TargetButton")).ExecuteAsync(session);

        Assert.True(button.IsMouseOver);
        Assert.Equal(1, clicks);
        Assert.True(host.AdvancedFrameCount > framesBeforeHover);
    }

    [Fact]
    public async Task MovePointer_Command_Uses_Smooth_Path_Before_Reaching_Target()
    {
        var button = new Button
        {
            Name = "MoveTarget",
            Content = "Move Me",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "move-pointer");
        var session = new InkkOopsSession(host, artifacts);
        var targetPoint = new Vector2(700f, 496f);
        var framesBeforeMove = host.AdvancedFrameCount;

        await new InkkOopsMovePointerCommand(targetPoint).ExecuteAsync(session);

        Assert.True(button.IsMouseOver);
        Assert.True(host.AdvancedFrameCount > framesBeforeMove);
    }

    [Fact]
    public async Task MovePointer_Command_With_TravelFrames_Uses_Explicit_Frame_Count()
    {
        var root = new Canvas { Width = 800f, Height = 600f };
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "move-pointer-travel-frames");
        var session = new InkkOopsSession(host, artifacts);
        var framesBeforeMove = host.AdvancedFrameCount;

        await new InkkOopsMovePointerCommand(
            new Vector2(700f, 500f),
            InkkOopsPointerMotion.WithTravelFrames(4))
            .ExecuteAsync(session);

        Assert.Equal(4, host.AdvancedFrameCount - framesBeforeMove);
    }

    [Fact]
    public async Task MovePointer_Command_With_EaseInOut_Produces_NonLinear_Samples()
    {
        var root = new Canvas { Width = 800f, Height = 600f };
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "move-pointer-ease");
        var session = new InkkOopsSession(host, artifacts);
        var traceStart = host.PointerTrace.Count;

        await new InkkOopsMovePointerCommand(
            new Vector2(700f, 300f),
            InkkOopsPointerMotion.WithTravelFrames(4, InkkOopsPointerEasing.EaseInOut))
            .ExecuteAsync(session);

        var samples = host.PointerTrace.Skip(traceStart).ToArray();
        Assert.True(samples.Length >= 5);

        var firstDelta = samples[1].X - samples[0].X;
        var secondDelta = samples[2].X - samples[1].X;

        Assert.True(Math.Abs(firstDelta - secondDelta) > 0.001f);
    }

    [Fact]
    public async Task MovePointerTo_And_LeaveTarget_Commands_Enter_And_Exit_Hover_State()
    {
        var button = new Button
        {
            Name = "HoverButton",
            Content = "Hover",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "move-leave");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsMovePointerTargetCommand(new InkkOopsTargetReference("HoverButton")).ExecuteAsync(session);
        Assert.True(button.IsMouseOver);

        await new InkkOopsLeaveTargetCommand(new InkkOopsTargetReference("HoverButton")).ExecuteAsync(session);
        Assert.False(button.IsMouseOver);
    }

    [Fact]
    public async Task Hover_Command_With_DwellFrames_Waits_Explicitly()
    {
        var button = new Button
        {
            Name = "DwellButton",
            Content = "Hover",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "hover-dwell");
        var session = new InkkOopsSession(host, artifacts);
        var framesBeforeHover = host.AdvancedFrameCount;

        await new InkkOopsHoverTargetCommand(
            new InkkOopsTargetReference("DwellButton"),
            dwellFrames: 3,
            motion: InkkOopsPointerMotion.WithTravelFrames(2))
            .ExecuteAsync(session);

        Assert.True(button.IsMouseOver);
        Assert.True(host.AdvancedFrameCount - framesBeforeHover >= 5);
    }

    [Fact]
    public async Task DoubleClick_Command_Raises_Click_Twice()
    {
        var button = new Button
        {
            Name = "DoubleClickButton",
            Content = "Click Twice",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "double-click");
        var session = new InkkOopsSession(host, artifacts);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        await new InkkOopsDoubleClickTargetCommand(new InkkOopsTargetReference("DoubleClickButton")).ExecuteAsync(session);

        Assert.Equal(2, clicks);
    }

    [Fact]
    public async Task RightClick_Command_Opens_ContextMenu()
    {
        var button = new Button
        {
            Name = "ContextButton",
            Content = "Open",
            Width = 120f,
            Height = 32f
        };
        Canvas.SetLeft(button, 640f);
        Canvas.SetTop(button, 480f);
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Item" });
        ContextMenu.SetContextMenu(button, menu);

        var root = new Canvas { Width = 800f, Height = 600f };
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "right-click");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsRightClickTargetCommand(new InkkOopsTargetReference("ContextButton")).ExecuteAsync(session);

        Assert.True(menu.IsOpen);
    }

    [Fact]
    public async Task Invoke_Command_Uses_Automation_And_Emits_Invoke_Event()
    {
        var button = new Button
        {
            Name = "InvokeButton",
            Content = "Invoke",
            Width = 120f,
            Height = 32f
        };
        var root = new Canvas();
        root.AddChild(button);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "invoke");
        var session = new InkkOopsSession(host, artifacts);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        await new InkkOopsInvokeTargetCommand(new InkkOopsTargetReference("InvokeButton")).ExecuteAsync(session);
        await host.AdvanceFrameAsync(1);

        Assert.Equal(1, clicks);
        Assert.Contains(host.GetAutomationEventsSnapshot(), static entry => entry.EventType == AutomationEventType.Invoke);
    }

    [Fact]
    public async Task Scroll_Commands_Update_ScrollViewer_Offsets()
    {
        var stack = new StackPanel();
        for (var i = 0; i < 50; i++)
        {
            stack.AddChild(new Label { Content = $"Row {i}" });
        }

        var viewer = new ScrollViewer
        {
            Name = "MyScrollViewer",
            Content = stack,
            Width = 120f,
            Height = 80f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var root = new Canvas();
        root.AddChild(viewer);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "scroll");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsScrollToCommand(new InkkOopsTargetReference("MyScrollViewer"), 0f, 60f).ExecuteAsync(session);
        var offsetAfterScrollTo = viewer.VerticalOffset;
        await new InkkOopsScrollByCommand(new InkkOopsTargetReference("MyScrollViewer"), 0f, 20f).ExecuteAsync(session);

        Assert.True(offsetAfterScrollTo > 0f);
        Assert.True(viewer.VerticalOffset >= offsetAfterScrollTo);
    }

    [Fact]
    public async Task Drag_Command_Raises_Thumb_Delta()
    {
        var thumb = new Thumb
        {
            Name = "DragThumb",
            Width = 24f,
            Height = 24f
        };
        Canvas.SetLeft(thumb, 10f);
        Canvas.SetTop(thumb, 10f);
        var root = new Canvas { Width = 200f, Height = 100f };
        root.AddChild(thumb);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "drag");
        var session = new InkkOopsSession(host, artifacts);
        var dragDeltaCalls = 0;
        var framesBeforeDrag = host.AdvancedFrameCount;
        thumb.DragDelta += (_, args) =>
        {
            if (Math.Abs(args.HorizontalChange) > 0.001f || Math.Abs(args.VerticalChange) > 0.001f)
            {
                dragDeltaCalls++;
            }
        };

        await new InkkOopsDragTargetCommand(new InkkOopsTargetReference("DragThumb"), 40f, 0f).ExecuteAsync(session);

        Assert.True(dragDeltaCalls > 0);
        Assert.False(thumb.IsDragging);
        Assert.True(host.AdvancedFrameCount > framesBeforeDrag);
    }

    [Fact]
    public async Task DragPath_Command_Raises_Thumb_Delta_Across_Waypoints()
    {
        var thumb = new Thumb
        {
            Name = "PathThumb",
            Width = 24f,
            Height = 24f
        };
        Canvas.SetLeft(thumb, 10f);
        Canvas.SetTop(thumb, 10f);
        var root = new Canvas { Width = 240f, Height = 160f };
        root.AddChild(thumb);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "drag-path");
        var session = new InkkOopsSession(host, artifacts);
        var dragDeltaCalls = 0;
        thumb.DragDelta += (_, args) =>
        {
            if (Math.Abs(args.HorizontalChange) > 0.001f || Math.Abs(args.VerticalChange) > 0.001f)
            {
                dragDeltaCalls++;
            }
        };

        await new InkkOopsDragPathTargetCommand(
            new InkkOopsTargetReference("PathThumb"),
            [new Vector2(70f, 22f), new Vector2(110f, 50f)])
            .ExecuteAsync(session);

        Assert.True(dragDeltaCalls > 0);
        Assert.False(thumb.IsDragging);
    }

    [Fact]
    public async Task WheelTarget_Command_Scrolls_Viewer()
    {
        var stack = new StackPanel();
        for (var i = 0; i < 50; i++)
        {
            stack.AddChild(new Label { Content = $"Row {i}" });
        }

        var viewer = new ScrollViewer
        {
            Name = "WheelViewer",
            Content = stack,
            Width = 120f,
            Height = 80f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var root = new Canvas();
        root.AddChild(viewer);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "wheel-target");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsWheelTargetCommand(new InkkOopsTargetReference("WheelViewer"), -120).ExecuteAsync(session);

        Assert.True(viewer.VerticalOffset > 0f);
    }

    [Fact]
    public async Task ScrollIntoView_Command_Makes_Target_Row_Visible()
    {
        var stack = new StackPanel { Name = "RowsHost" };
        for (var i = 0; i < 40; i++)
        {
            stack.AddChild(new Label
            {
                Name = i == 30 ? "TargetRow" : $"Row{i}",
                Content = $"Row {i}",
                Height = 24f
            });
        }

        var viewer = new ScrollViewer
        {
            Name = "MyScrollViewer",
            Content = stack,
            Width = 180f,
            Height = 120f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var root = new Canvas();
        root.AddChild(viewer);
        using var host = new InkkOopsTestHost(root);
        using var artifacts = new InkkOopsArtifacts(host.ArtifactRoot, "scroll-into-view");
        var session = new InkkOopsSession(host, artifacts);

        await new InkkOopsScrollIntoViewCommand(
            new InkkOopsTargetReference("MyScrollViewer"),
            new InkkOopsTargetReference("TargetRow"))
            .ExecuteAsync(session);
        await host.AdvanceFrameAsync(2);

        var target = (Label)session.ResolveRequiredTarget(new InkkOopsTargetReference("TargetRow"));
        Assert.True(viewer.VerticalOffset > 0f);
        Assert.True(target.TryGetRenderBoundsInRootSpace(out var targetBounds));
        Assert.True(viewer.TryGetContentViewportClipRect(out var viewportBounds));
        Assert.True(targetBounds.Y >= viewportBounds.Y - 0.5f);
        Assert.True(targetBounds.Y + targetBounds.Height <= viewportBounds.Y + viewportBounds.Height + 0.5f);
    }
}
