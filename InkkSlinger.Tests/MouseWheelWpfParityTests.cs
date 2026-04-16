using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class MouseWheelWpfParityTests
{
    [Fact]
    public void MouseWheel_RoutesThroughPreviewAndBubbleHandlers()
    {
        var source = new Border
        {
            Width = 24f,
            Height = 24f
        };
        var root = new Grid();
        root.AddChild(source);

        var events = new List<string>();
        root.AddHandler<MouseWheelRoutedEventArgs>(
            UIElement.PreviewMouseWheelEvent,
            (_, args) => events.Add($"preview:{args.OriginalSource?.GetType().Name}"));
        root.AddHandler<MouseWheelRoutedEventArgs>(
            UIElement.MouseWheelEvent,
            (_, args) => events.Add($"bubble:{args.OriginalSource?.GetType().Name}"));

        var uiRoot = new UiRoot(root);
        RunLayout(uiRoot, 320, 220, 16);

        var pointer = GetCenter(source.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

        Assert.Equal(["preview:Border", "bubble:Border"], events);
    }

    [Fact]
    public void ScrollViewer_MouseWheel_ScrollsHoveredSurface_WithoutKeyboardFocus()
    {
        FocusManager.ClearFocus();
        try
        {
            var focusButton = new Button
            {
                Content = "Keep focus",
                Height = 32f,
                Margin = new Thickness(8f)
            };

            var viewer = CreateScrollableViewer(height: 140f, itemCount: 48, out var source);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            root.AddChild(focusButton);
            Grid.SetRow(viewer, 1);
            root.AddChild(viewer);

            var uiRoot = new UiRoot(root);
            RunLayout(uiRoot, 360, 300, 16);

            FocusManager.SetFocus(focusButton);
            Assert.True(focusButton.IsFocused);

            var pointer = GetCenter(source.LayoutSlot);
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

            Assert.True(viewer.VerticalOffset > 0f, $"Expected wheel input over the ScrollViewer content to scroll it without moving focus. offset={viewer.VerticalOffset:0.###}");
            Assert.True(focusButton.IsFocused, "Expected wheel scrolling over a hovered ScrollViewer not to move keyboard focus away from the existing focused control.");
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void TextBox_MouseWheel_ScrollsWithoutTakingKeyboardFocus()
    {
        FocusManager.ClearFocus();
        try
        {
            var focusButton = new Button
            {
                Content = "Keep focus",
                Height = 32f,
                Margin = new Thickness(8f)
            };

            var textBox = new TextBox
            {
                Height = 120f,
                Margin = new Thickness(8f),
                Text = string.Join("\n", Enumerable.Range(1, 80).Select(static i => $"Line {i}")),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Width = 260f
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            root.AddChild(focusButton);
            Grid.SetRow(textBox, 1);
            root.AddChild(textBox);

            var uiRoot = new UiRoot(root);
            RunLayout(uiRoot, 360, 300, 16);

            FocusManager.SetFocus(focusButton);
            Assert.True(focusButton.IsFocused);

            var beforeOffset = GetPrivateFloat(textBox, "_verticalOffset");
            var pointer = GetCenter(textBox.LayoutSlot);
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

            var afterOffset = GetPrivateFloat(textBox, "_verticalOffset");
            Assert.True(afterOffset > beforeOffset, $"Expected wheel input over the unfocused TextBox to scroll its content. before={beforeOffset:0.###}, after={afterOffset:0.###}");
            Assert.False(textBox.IsFocused, "Expected wheel input over the TextBox not to give it keyboard focus.");
            Assert.True(focusButton.IsFocused, "Expected the previously focused control to keep keyboard focus after wheel scrolling a hovered TextBox.");
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    [Fact]
    public void NestedScrollViewer_MouseWheel_AtInnerEdge_DoesNotAutomaticallyHandoffToOuterViewer()
    {
        var innerViewer = CreateScrollableViewer(height: 120f, itemCount: 40, out var innerSource);
        var outerStack = new StackPanel();
        outerStack.AddChild(new Border { Height = 60f });
        outerStack.AddChild(innerViewer);
        outerStack.AddChild(new Border { Height = 320f });

        var outerViewer = new ScrollViewer
        {
            Content = outerStack,
            Height = 180f,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 300f
        };

        var uiRoot = new UiRoot(outerViewer);
        RunLayout(uiRoot, 360, 260, 16);

        innerViewer.ScrollToVerticalOffset(float.MaxValue);
        RunLayout(uiRoot, 360, 260, 32);
        Assert.True(innerViewer.ExtentHeight > innerViewer.ViewportHeight, "Expected the inner ScrollViewer to have a vertical scroll range.");
        Assert.True(outerViewer.ExtentHeight > outerViewer.ViewportHeight, "Expected the outer ScrollViewer to have a vertical scroll range.");

        var outerBefore = outerViewer.VerticalOffset;
        var innerBefore = innerViewer.VerticalOffset;
        var pointer = GetCenter(innerSource.LayoutSlot);
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
        uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

        Assert.True(MathF.Abs(innerViewer.VerticalOffset - innerBefore) <= 0.1f, $"Expected the inner ScrollViewer to stay pinned at its edge. before={innerBefore:0.###}, after={innerViewer.VerticalOffset:0.###}");
        Assert.True(MathF.Abs(outerViewer.VerticalOffset - outerBefore) <= 0.1f, $"Expected wheel input at the inner edge not to automatically hand off to the outer ScrollViewer. before={outerBefore:0.###}, after={outerViewer.VerticalOffset:0.###}");
    }

    [Fact]
    public void SingleLineTextBoxInsideScrollViewer_MouseWheel_ScrollsOuterViewer_WithoutTakingFocus()
    {
        FocusManager.ClearFocus();
        try
        {
            var focusButton = new Button
            {
                Content = "Keep focus",
                Height = 32f,
                Margin = new Thickness(8f)
            };

            var host = new StackPanel();
            host.AddChild(new Border { Height = 32f });
            var textBox = new TextBox
            {
                Width = 240f,
                Margin = new Thickness(8f),
                Text = "hover me"
            };
            host.AddChild(textBox);
            for (var i = 0; i < 30; i++)
            {
                host.AddChild(new Border { Height = 28f, Margin = new Thickness(0f, 0f, 0f, 4f) });
            }

            var viewer = new ScrollViewer
            {
                Content = host,
                Height = 160f,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Width = 280f
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            root.AddChild(focusButton);
            Grid.SetRow(viewer, 1);
            root.AddChild(viewer);

            var uiRoot = new UiRoot(root);
            RunLayout(uiRoot, 360, 260, 16);

            FocusManager.SetFocus(focusButton);
            Assert.True(focusButton.IsFocused);
            var pointer = GetCenter(textBox.LayoutSlot);
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: true, position: pointer));
            uiRoot.RunInputDeltaForTests(CreateDelta(pointerMoved: false, position: pointer, wheelDelta: -120));

            Assert.True(viewer.VerticalOffset > 0f, $"Expected wheel input over a non-scrollable TextBox inside a ScrollViewer to fall through to the outer ScrollViewer. offset={viewer.VerticalOffset:0.###}");
            Assert.False(textBox.IsFocused, "Expected wheel input over the inner TextBox not to give it keyboard focus.");
            Assert.True(focusButton.IsFocused, "Expected the previously focused control to keep keyboard focus after wheel scrolling over the inner TextBox.");
        }
        finally
        {
            FocusManager.ClearFocus();
        }
    }

    private static ScrollViewer CreateScrollableViewer(float height, int itemCount, out Border source)
    {
        var stack = new StackPanel();
        source = new Border
        {
            Height = 24f,
            Margin = new Thickness(0f, 0f, 0f, 4f),
            Width = 240f
        };
        stack.AddChild(source);

        for (var i = 0; i < itemCount; i++)
        {
            stack.AddChild(new Border
            {
                Height = 24f,
                Margin = new Thickness(0f, 0f, 0f, 4f)
            });
        }

        return new ScrollViewer
        {
            Content = stack,
            Height = height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 280f
        };
    }

    private static float GetPrivateFloat(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<float>(field!.GetValue(target));
    }

    private static Vector2 GetCenter(LayoutRect rect)
    {
        return new Vector2(rect.X + (rect.Width * 0.5f), rect.Y + (rect.Height * 0.5f));
    }

    private static InputDelta CreateDelta(bool pointerMoved, Vector2 position, int wheelDelta = 0)
    {
        var previous = new InputSnapshot(default, default, position);
        var current = new InputSnapshot(default, default, position);
        return new InputDelta
        {
            Previous = previous,
            Current = current,
            PressedKeys = [],
            ReleasedKeys = [],
            TextInput = [],
            PointerMoved = pointerMoved,
            WheelDelta = wheelDelta,
            LeftPressed = false,
            LeftReleased = false,
            RightPressed = false,
            RightReleased = false,
            MiddlePressed = false,
            MiddleReleased = false
        };
    }

    private static void RunLayout(UiRoot uiRoot, int width, int height, int elapsedMs)
    {
        uiRoot.Update(
            new GameTime(TimeSpan.FromMilliseconds(elapsedMs), TimeSpan.FromMilliseconds(elapsedMs)),
            new Viewport(0, 0, width, height));
    }
}