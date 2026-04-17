using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class RoutedEventMutationTests
{
    [Fact]
    public void ClickHandlers_CanMutateHandlerCollection_DuringDispatch()
    {
        var button = new Button();
        var firstCalls = 0;
        var secondCalls = 0;
        System.EventHandler<RoutedSimpleEventArgs>? first = null;
        System.EventHandler<RoutedSimpleEventArgs>? second = null;

        first = (_, _) =>
        {
            firstCalls++;
            button.RemoveHandler(Button.ClickEvent, first!);
        };

        second = (_, _) =>
        {
            secondCalls++;
            button.RemoveHandler(Button.ClickEvent, first!);
        };

        button.AddHandler(Button.ClickEvent, first);
        button.AddHandler(Button.ClickEvent, second);

        button.InvokeFromInput();
        button.InvokeFromInput();

        Assert.Equal(1, firstCalls);
        Assert.Equal(2, secondCalls);
    }

    [Fact]
    public void PreviewAndBubbleMouseRoutes_ShareHandledStateAcrossOneLogicalEvent()
    {
        var root = new StackPanel();
        var child = new Border();
        root.AddChild(child);

        MouseRoutedEventArgs? previewArgs = null;
        MouseRoutedEventArgs? bubbleArgs = null;
        var genericBubbleCalls = 0;

        child.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonDownEvent, (_, args) =>
        {
            previewArgs = args;
            args.Handled = true;
        });
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, (_, _) => genericBubbleCalls++);
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, (_, args) => bubbleArgs = args, handledEventsToo: true);

        var args = new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, Vector2.Zero, MouseButton.Left);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseLeftButtonDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseLeftButtonDownEvent, args);

        Assert.Same(previewArgs, bubbleArgs);
        Assert.Same(args, bubbleArgs);
        Assert.Same(UIElement.MouseLeftButtonDownEvent, args.RoutedEvent);
        Assert.Equal(0, genericBubbleCalls);
    }

    [Fact]
    public void MouseButtonSpecificRoutes_OnlyRaiseMatchingButtonEvents()
    {
        var root = new StackPanel();
        var child = new Border();
        root.AddChild(child);

        var leftCalls = 0;
        var rightCalls = 0;

        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonDownEvent, (_, _) => leftCalls++, handledEventsToo: true);
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseRightButtonDownEvent, (_, _) => rightCalls++, handledEventsToo: true);

        var args = new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, Vector2.Zero, MouseButton.Right);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseRightButtonDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseDownEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseRightButtonDownEvent, args);

        Assert.Equal(0, leftCalls);
        Assert.Equal(1, rightCalls);
    }

    [Fact]
    public void PreviewAndBubbleMouseUpRoutes_ShareHandledStateAcrossOneLogicalEvent()
    {
        var root = new StackPanel();
        var child = new Border();
        root.AddChild(child);

        MouseRoutedEventArgs? previewArgs = null;
        MouseRoutedEventArgs? bubbleArgs = null;
        var genericBubbleCalls = 0;

        child.AddHandler<MouseRoutedEventArgs>(UIElement.PreviewMouseLeftButtonUpEvent, (_, args) =>
        {
            previewArgs = args;
            args.Handled = true;
        });
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseUpEvent, (_, _) => genericBubbleCalls++);
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonUpEvent, (_, args) => bubbleArgs = args, handledEventsToo: true);

        var args = new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, Vector2.Zero, MouseButton.Left);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseLeftButtonUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseLeftButtonUpEvent, args);

        Assert.Same(previewArgs, bubbleArgs);
        Assert.Same(args, bubbleArgs);
        Assert.Same(UIElement.MouseLeftButtonUpEvent, args.RoutedEvent);
        Assert.Equal(0, genericBubbleCalls);
    }

    [Fact]
    public void MouseUpButtonSpecificRoutes_OnlyRaiseMatchingButtonEvents()
    {
        var root = new StackPanel();
        var child = new Border();
        root.AddChild(child);

        var leftCalls = 0;
        var rightCalls = 0;

        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseLeftButtonUpEvent, (_, _) => leftCalls++, handledEventsToo: true);
        root.AddHandler<MouseRoutedEventArgs>(UIElement.MouseRightButtonUpEvent, (_, _) => rightCalls++, handledEventsToo: true);

        var args = new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, Vector2.Zero, MouseButton.Right);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.PreviewMouseRightButtonUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseUpEvent, args);
        child.RaiseRoutedEventInternal(UIElement.MouseRightButtonUpEvent, args);

        Assert.Equal(0, leftCalls);
        Assert.Equal(1, rightCalls);
    }
}
