using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace InkkSlinger.WpfLab.Tests;

public sealed class WpfMouseWheelProofTests
{
    [Fact]
    public void MouseWheel_RoutesThroughPreviewAndBubbleHandlers()
    {
        var source = new Border
        {
            Height = 24d,
            Width = 24d
        };
        var root = new Grid();
        root.Children.Add(source);
        var events = new List<string>();

        root.PreviewMouseWheel += (_, e) => events.Add($"preview:{e.Source?.GetType().Name}");
        root.MouseWheel += (_, e) => events.Add($"bubble:{e.Source?.GetType().Name}");

        var window = CreateWindow(root, width: 320d, height: 220d);
        try
        {
            DispatchMouseWheel(source, delta: -120);

            Assert.Equal(
                ["preview:Border", "bubble:Border"],
                events);
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [Fact]
    public void ScrollViewer_MouseWheel_ScrollsHoveredSurface_WithoutKeyboardFocus()
    {
        var focusButton = new Button
        {
            Content = "Keep focus",
            Margin = new Thickness(8d)
        };

        var viewer = CreateScrollableViewer(height: 140d, itemCount: 48, out var source);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.Children.Add(focusButton);
        Grid.SetRow(viewer, 1);
        root.Children.Add(viewer);

        var window = CreateWindow(root, width: 360d, height: 300d);
        try
        {
            Assert.True(focusButton.Focus(), "Expected the focus button to accept keyboard focus before wheel input.");
            PumpDispatcher();
            Assert.True(focusButton.IsKeyboardFocused, "Expected the focus button to hold keyboard focus before wheel input.");

            DispatchMouseWheel(source, delta: -120);

            Assert.True(viewer.VerticalOffset > 0d, $"Expected wheel input over the ScrollViewer content to scroll it without focus. offset={viewer.VerticalOffset:0.###}");
            Assert.True(focusButton.IsKeyboardFocused, "Expected wheel scrolling over a hovered ScrollViewer not to move keyboard focus away from the existing focused control.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [Fact]
    public void TextBox_MouseWheel_ScrollsWithoutTakingKeyboardFocus()
    {
        var focusButton = new Button
        {
            Content = "Keep focus",
            Margin = new Thickness(8d)
        };

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 120d,
            Margin = new Thickness(8d),
            Text = string.Join(Environment.NewLine, Enumerable.Range(1, 80).Select(static i => $"Line {i}")),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 260d
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.Children.Add(focusButton);
        Grid.SetRow(textBox, 1);
        root.Children.Add(textBox);

        var window = CreateWindow(root, width: 360d, height: 300d);
        try
        {
            Assert.True(focusButton.Focus(), "Expected the focus button to accept keyboard focus before wheel input.");
            PumpDispatcher();
            Assert.True(focusButton.IsKeyboardFocused, "Expected the focus button to hold keyboard focus before wheel input.");

            var beforeOffset = textBox.VerticalOffset;
            DispatchMouseWheel(textBox, delta: -120);

            Assert.True(textBox.VerticalOffset > beforeOffset, $"Expected wheel input over the unfocused TextBox to scroll its content. before={beforeOffset:0.###}, after={textBox.VerticalOffset:0.###}");
            Assert.False(textBox.IsKeyboardFocused, "Expected wheel input over the TextBox not to give it keyboard focus.");
            Assert.True(focusButton.IsKeyboardFocused, "Expected the previously focused control to keep keyboard focus after wheel scrolling a hovered TextBox.");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    [Fact]
    public void NestedScrollViewer_MouseWheel_AtInnerEdge_DoesNotAutomaticallyHandoffToOuterViewer()
    {
        var innerViewer = CreateScrollableViewer(height: 120d, itemCount: 40, out var innerSource);
        var outerStack = new StackPanel();
        outerStack.Children.Add(new Border { Height = 60d });
        outerStack.Children.Add(innerViewer);
        outerStack.Children.Add(new Border { Height = 320d });

        var outerViewer = new ScrollViewer
        {
            Content = outerStack,
            Height = 180d,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 300d
        };

        var window = CreateWindow(outerViewer, width: 360d, height: 260d);
        try
        {
            innerViewer.ScrollToEnd();
            PumpDispatcher();
            Assert.True(innerViewer.ScrollableHeight > 0d, "Expected the inner ScrollViewer to have a vertical scroll range.");
            Assert.True(outerViewer.ScrollableHeight > 0d, "Expected the outer ScrollViewer to have a vertical scroll range.");

            var outerBefore = outerViewer.VerticalOffset;
            var innerBefore = innerViewer.VerticalOffset;
            DispatchMouseWheel(innerSource, delta: -120);

            Assert.True(Math.Abs(innerViewer.VerticalOffset - innerBefore) <= 0.1d, $"Expected the inner ScrollViewer to stay pinned at its edge. before={innerBefore:0.###}, after={innerViewer.VerticalOffset:0.###}");
            Assert.True(Math.Abs(outerViewer.VerticalOffset - outerBefore) <= 0.1d, $"Expected WPF not to automatically hand wheel scrolling from the inner edge to the outer ScrollViewer. before={outerBefore:0.###}, after={outerViewer.VerticalOffset:0.###}");
        }
        finally
        {
            CloseWindow(window);
        }
    }

    private static Window CreateWindow(UIElement content, double width, double height)
    {
        EnsureApplication();

        var window = new Window
        {
            Content = content,
            Height = height,
            Left = -10000d,
            ShowInTaskbar = false,
            Top = -10000d,
            Width = width,
            WindowStyle = WindowStyle.ToolWindow
        };

        window.Show();
        window.Activate();
        PumpDispatcher();
        window.UpdateLayout();
        PumpDispatcher();
        return window;
    }

    private static void CloseWindow(Window window)
    {
        window.Close();
        PumpDispatcher();
    }

    private static ScrollViewer CreateScrollableViewer(double height, int itemCount, out Border source)
    {
        var stack = new StackPanel();
        source = new Border
        {
            Background = Brushes.LightSteelBlue,
            Height = 24d,
            Margin = new Thickness(0d, 0d, 0d, 4d)
        };
        stack.Children.Add(source);

        for (var i = 0; i < itemCount; i++)
        {
            stack.Children.Add(new Border
            {
                Height = 24d,
                Margin = new Thickness(0d, 0d, 0d, 4d)
            });
        }

        return new ScrollViewer
        {
            Content = stack,
            Height = height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Width = 280d
        };
    }

    private static void DispatchMouseWheel(UIElement source, int delta)
    {
        var previewArgs = CreateMouseWheelEventArgs(source, delta, UIElement.PreviewMouseWheelEvent);
        source.RaiseEvent(previewArgs);
        PumpDispatcher();

        if (previewArgs.Handled)
        {
            return;
        }

        var bubbleArgs = CreateMouseWheelEventArgs(source, delta, UIElement.MouseWheelEvent);
        source.RaiseEvent(bubbleArgs);
        PumpDispatcher();
    }

    private static MouseWheelEventArgs CreateMouseWheelEventArgs(object source, int delta, RoutedEvent routedEvent)
    {
        return new MouseWheelEventArgs(InputManager.Current.PrimaryMouseDevice, Environment.TickCount, delta)
        {
            RoutedEvent = routedEvent,
            Source = source
        };
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            _ = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}