using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class ExpanderInputTests
{
    [Fact]
    public void ClickingHeader_ShouldToggleIsExpanded()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 260f
        };

        var expander = new Expander
        {
            Width = 360f,
            Height = 200f,
            Header = "Expander Header",
            Content = new Label { Content = "Expander Content" },
            IsExpanded = true
        };
        host.AddChild(expander);
        Canvas.SetLeft(expander, 30f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        var expandedHeight = expander.ActualHeight;
        Assert.True(expandedHeight > 40f);

        var headerPoint = new Vector2(expander.LayoutSlot.X + 8f, expander.LayoutSlot.Y + 8f);
        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.False(expander.IsExpanded);
        Assert.True(expander.ActualHeight < expandedHeight);

        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.True(expander.IsExpanded);
    }

    [Fact]
    public void ClickingScrolledHeader_ShouldToggleIsExpanded()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 260f
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Width = 360f
        };

        contentPanel.AddChild(new Border
        {
            Height = 120f,
            Child = new Label { Content = "Spacer" }
        });

        var expander = new Expander
        {
            Width = 360f,
            Height = 180f,
            Header = "Scrolled Expander Header",
            Content = new Label { Content = "Expander Content" },
            IsExpanded = true
        };
        contentPanel.AddChild(expander);

        var viewer = new ScrollViewer
        {
            Width = 380f,
            Height = 140f,
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        host.AddChild(viewer);
        Canvas.SetLeft(viewer, 20f);
        Canvas.SetTop(viewer, 20f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);

        viewer.ScrollToVerticalOffset(80f);
        RunLayout(uiRoot);

        Assert.True(expander.TryGetRenderBoundsInRootSpace(out var expanderBounds));
        var headerPoint = new Vector2(expanderBounds.X + 8f, expanderBounds.Y + 8f);

        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.False(expander.IsExpanded);

        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);
        Assert.True(expander.IsExpanded);
    }

    [Fact]
    public void CollapsedExpander_ShouldExcludeContentFromRetainedVisualOrder()
    {
        var host = new Canvas
        {
            Width = 460f,
            Height = 260f
        };

        var content = new Label { Content = "Expander Content" };
        var expander = new Expander
        {
            Width = 360f,
            Height = 200f,
            Header = "Expander Header",
            Content = content,
            IsExpanded = true
        };
        host.AddChild(expander);
        Canvas.SetLeft(expander, 30f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot);
        Assert.Contains(content, uiRoot.GetRetainedVisualOrderForTests());

        var headerPoint = new Vector2(expander.LayoutSlot.X + 8f, expander.LayoutSlot.Y + 8f);
        Click(uiRoot, headerPoint);
        RunLayout(uiRoot);

        Assert.False(expander.IsExpanded);
        Assert.DoesNotContain(content, uiRoot.GetRetainedVisualOrderForTests());
    }

    [Fact]
    public void ReExpanding_WrappedChecklistContent_RestoresBorderLayout()
    {
        var host = new Canvas
        {
            Width = 700f,
            Height = 520f
        };

        var summaryText = new TextBlock
        {
            Text = "A live release checklist gives the content area enough density to make open and collapsed changes obvious.",
            TextWrapping = TextWrapping.Wrap
        };

        var checklistPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        checklistPanel.AddChild(summaryText);
        checklistPanel.AddChild(CreateChecklistItem("QA pass: verify keyboard and pointer access around the header hit-target."));
        checklistPanel.AddChild(CreateChecklistItem("Telemetry pass: watch Expanded and Collapsed counts update without recreating the control."));
        checklistPanel.AddChild(CreateChecklistItem("Direction pass: switch to Up, Left, or Right to see the header consume a different edge before content is arranged."));

        var contentBorder = new Border
        {
            Padding = new Thickness(15f),
            BorderThickness = new Thickness(1f),
            Child = checklistPanel
        };

        var expander = new Expander
        {
            Width = 420f,
            Height = 260f,
            Header = "Release checklist",
            Padding = new Thickness(8f),
            Content = contentBorder,
            IsExpanded = true
        };

        host.AddChild(expander);
        Canvas.SetLeft(expander, 40f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 700, 520);

        var expandedHeight = expander.ActualHeight;
        var expandedContentWidth = contentBorder.ActualWidth;
        Assert.True(expandedContentWidth > 0f, $"Expected content border to have non-zero width before collapse, but got {expandedContentWidth:0.##}.");

        expander.IsExpanded = false;
        RunLayout(uiRoot, 700, 520);
        Assert.Equal(0f, contentBorder.ActualWidth, 0.01f);
        Assert.Equal(0f, contentBorder.ActualHeight, 0.01f);

        expander.IsExpanded = true;
        RunLayout(uiRoot, 700, 520);

        Assert.True(expander.ActualHeight >= expandedHeight - 0.1f, $"Expected expanded height to recover after re-expand, but got {expander.ActualHeight:0.##} from initial {expandedHeight:0.##}.");
        Assert.True(contentBorder.ActualWidth >= expandedContentWidth - 0.1f, $"Expected content border width to recover after re-expand, but got {contentBorder.ActualWidth:0.##} from initial {expandedContentWidth:0.##}.");
        Assert.True(summaryText.ActualWidth > 0f, $"Expected wrapped summary text to regain a non-zero layout width after re-expand, but got {summaryText.ActualWidth:0.##}.");
    }

    [Fact]
    public void ReExpanding_ExpanderViewPlayground_RestoresExpandedActualHeight()
    {
        TestApplicationResources.LoadDemoAppResources();
        var view = new ExpanderView();
        var uiRoot = new UiRoot(view);

        RunLayout(uiRoot, 1902, 973);

        var expander = Assert.IsType<Expander>(view.FindName("PlaygroundExpander"));
        var expandedHeight = expander.ActualHeight;
        Assert.True(expandedHeight > 200f, $"Expected playground Expander to start expanded, but got {expandedHeight:0.##}.");

        expander.IsExpanded = false;
        RunLayout(uiRoot, 1902, 973);
        var collapsedHeight = expander.ActualHeight;
        Assert.True(collapsedHeight < expandedHeight, $"Expected playground Expander to collapse below its expanded height, but got {collapsedHeight:0.##} from initial {expandedHeight:0.##}.");

        expander.IsExpanded = true;
        RunLayout(uiRoot, 1902, 973);

        Assert.True(
            expander.ActualHeight > collapsedHeight + 40f,
            $"Expected playground Expander to grow materially after re-expand, but got {expander.ActualHeight:0.##} from collapsed {collapsedHeight:0.##}.");
        Assert.True(
            expander.ActualHeight > 200f,
            $"Expected playground Expander to return to a substantial expanded height after re-expand, but got {expander.ActualHeight:0.##}.");
    }

    [Fact]
    public void ExpanderTelemetry_CapturesRuntimeBranchesAndAggregateReset()
    {
        _ = Expander.GetTelemetryAndReset();

        var host = new Canvas
        {
            Width = 520f,
            Height = 320f
        };

        var expander = new Expander
        {
            Width = 360f,
            Height = 220f,
            Header = "Telemetry Header",
            Content = new Border
            {
                Padding = new Thickness(10f),
                Child = new TextBlock
                {
                    Text = "Telemetry content body",
                    TextWrapping = TextWrapping.Wrap
                }
            },
            IsExpanded = true
        };

        host.AddChild(expander);
        Canvas.SetLeft(expander, 30f);
        Canvas.SetTop(expander, 24f);

        var uiRoot = new UiRoot(host);
        RunLayout(uiRoot, 520, 320);

        var headerElement = new TextBlock { Text = "Telemetry Header Element" };
        expander.Header = headerElement;
        RunLayout(uiRoot, 520, 320);

        expander.Header = "Telemetry Header";
        RunLayout(uiRoot, 520, 320);

        Assert.False(expander.HandlePointerDownFromInput(new Vector2(4f, 4f)));
        Assert.False(expander.HandlePointerUpFromInput(new Vector2(4f, 4f)));

        var headerPoint = new Vector2(expander.LayoutSlot.X + 8f, expander.LayoutSlot.Y + 8f);
        Assert.True(expander.HandlePointerDownFromInput(headerPoint));
        Assert.True(expander.HandlePointerUpFromInput(headerPoint));
        RunLayout(uiRoot, 520, 320);

        Assert.True(expander.HandlePointerDownFromInput(headerPoint));
        Assert.False(expander.HandlePointerUpFromInput(new Vector2(headerPoint.X, headerPoint.Y + expander.ActualHeight + 20f)));

        Assert.True(expander.HandlePointerDownFromInput(headerPoint));
        Assert.True(expander.HandlePointerUpFromInput(headerPoint));
        RunLayout(uiRoot, 520, 320);

        var runtime = expander.GetExpanderSnapshotForDiagnostics();
        Assert.True(runtime.MeasureOverrideCallCount > 0);
        Assert.True(runtime.HeaderMeasureCount > 0);
        Assert.True(runtime.HeaderMeasureTextPathCount > 0);
        Assert.True(runtime.HeaderMeasureElementPathCount > 0);
        Assert.True(runtime.ContentMeasuredWhenExpandedCount > 0);
        Assert.True(runtime.ArrangeOverrideCallCount > 0);
        Assert.True(runtime.ArrangeHeaderMeasureCacheHitCount > 0 || runtime.ArrangeHeaderMeasureCacheMissCount > 0);
        Assert.True(runtime.ExpandCount > 0);
        Assert.True(runtime.CollapseCount > 0);
        Assert.True(runtime.HeaderPointerDownCount > 0);
        Assert.True(runtime.HeaderPointerDownMissCount > 0);
        Assert.True(runtime.HeaderPointerUpToggleCount > 0);
        Assert.True(runtime.HeaderPointerUpMissCount > 0);
        Assert.True(runtime.HeaderPointerUpReleaseOutsideCount > 0);
        Assert.True(runtime.HeaderUpdateCount >= 2);
        Assert.True(runtime.HeaderUpdateAttachElementCount > 0);
        Assert.True(runtime.HeaderUpdateTextHeaderCount > 0);

        var aggregate = Expander.GetTelemetryAndReset();
        Assert.True(aggregate.MeasureOverrideCallCount > 0);
        Assert.True(aggregate.HeaderMeasureCount > 0);
        Assert.True(aggregate.HeaderMeasureTextPathCount > 0);
        Assert.True(aggregate.HeaderMeasureElementPathCount > 0);
        Assert.True(aggregate.ContentMeasuredWhenExpandedCount > 0);
        Assert.True(aggregate.ArrangeOverrideCallCount > 0);
        Assert.True(aggregate.ExpandCount > 0);
        Assert.True(aggregate.CollapseCount > 0);
        Assert.True(aggregate.HeaderPointerDownCount > 0);
        Assert.True(aggregate.HeaderPointerDownMissCount > 0);
        Assert.True(aggregate.HeaderPointerUpToggleCount > 0);
        Assert.True(aggregate.HeaderPointerUpMissCount > 0);
        Assert.True(aggregate.HeaderPointerUpReleaseOutsideCount > 0);
        Assert.True(aggregate.HeaderUpdateCount >= 2);

        var cleared = Expander.GetTelemetryAndReset();
        Assert.Equal(0, cleared.MeasureOverrideCallCount);
        Assert.Equal(0, cleared.HeaderMeasureCount);
        Assert.Equal(0, cleared.ArrangeOverrideCallCount);
        Assert.Equal(0, cleared.ExpandCount);
        Assert.Equal(0, cleared.HeaderPointerDownCount);
    }

    private static Border CreateChecklistItem(string text)
    {
        return new Border
        {
            Padding = new Thickness(10f),
            BorderThickness = new Thickness(1f),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static void Click(UiRoot uiRoot, Vector2 pointer)
    {
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftPressed: true));
        uiRoot.RunInputDeltaForTests(CreatePointerDelta(pointer, leftReleased: true));
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

    private static void RunLayout(UiRoot uiRoot, int width = 460, int height = 260)
    {
        uiRoot.Update(
            new GameTime(System.TimeSpan.FromMilliseconds(16), System.TimeSpan.FromMilliseconds(16)),
            new Viewport(0, 0, width, height));
    }
}
