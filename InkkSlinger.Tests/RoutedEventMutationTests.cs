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
}
