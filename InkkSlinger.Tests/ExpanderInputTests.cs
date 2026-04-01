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
        var view = new ExpanderView();
        var uiRoot = new UiRoot(view);

        RunLayout(uiRoot, 1902, 973);

        var expander = Assert.IsType<Expander>(view.FindName("PlaygroundExpander"));
        var expandedHeight = expander.ActualHeight;
        Assert.True(expandedHeight > 200f, $"Expected playground Expander to start expanded, but got {expandedHeight:0.##}.");

        expander.IsExpanded = false;
        RunLayout(uiRoot, 1902, 973);
        Assert.True(expander.ActualHeight < expandedHeight, $"Expected playground Expander to collapse below its expanded height, but got {expander.ActualHeight:0.##} from initial {expandedHeight:0.##}.");

        expander.IsExpanded = true;
        RunLayout(uiRoot, 1902, 973);

        Assert.True(
            expander.ActualHeight >= expandedHeight - 0.1f,
            $"Expected playground Expander to restore full actual height after re-expand, but got {expander.ActualHeight:0.##} from initial {expandedHeight:0.##}.");
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
