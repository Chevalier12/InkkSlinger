using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class EventSetterTests
{
    [Fact]
    public void XamlStyle_EventSetter_ParseAndApply_InvokesCodeBehindHandler()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeButtonStyle" TargetType="{x:Type Button}">
      <EventSetter Event="Click" Handler="OnStyledClick" />
    </Style>
  </UserControl.Resources>
  <Button Name="ProbeButton" Style="{StaticResource ProbeButtonStyle}" Text="Run" />
</UserControl>
""";

        var view = new EventSetterCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var button = Assert.IsType<Button>(view.FindName("ProbeButton"));
        InvokeButtonClick(button);

        Assert.Equal(1, view.StyledClickCount);
    }

    [Fact]
    public void ApplyTwice_DoesNotDuplicateHandlers()
    {
        var clickCount = 0;
        var style = new Style(typeof(Button));
        style.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => clickCount++)));

        var button = new Button();
        style.Apply(button);
        style.Apply(button);

        InvokeButtonClick(button);

        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void StyleReplacement_DetachesPriorEventSetterHandlers()
    {
        var oldCount = 0;
        var newCount = 0;
        var oldStyle = new Style(typeof(Button));
        oldStyle.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => oldCount++)));

        var newStyle = new Style(typeof(Button));
        newStyle.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => newCount++)));

        var button = new Button
        {
            Style = oldStyle
        };

        button.Style = newStyle;
        InvokeButtonClick(button);

        Assert.Equal(0, oldCount);
        Assert.Equal(1, newCount);
    }

    [Fact]
    public void BasedOn_EventSetterInvocationOrder_IsBaseThenDerived()
    {
        var invocationOrder = new List<string>();
        var baseStyle = new Style(typeof(Button));
        baseStyle.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => invocationOrder.Add("Base"))));

        var derivedStyle = new Style(typeof(Button))
        {
            BasedOn = baseStyle
        };
        derivedStyle.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => invocationOrder.Add("Derived"))));

        var button = new Button();
        derivedStyle.Apply(button);
        InvokeButtonClick(button);

        Assert.Equal(["Base", "Derived"], invocationOrder);
    }

    [Fact]
    public void CodeOnlyEventSetter_DetachStopsFurtherInvocations()
    {
        var clickCount = 0;
        var style = new Style(typeof(Button));
        style.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => clickCount++)));

        var button = new Button();
        style.Apply(button);
        InvokeButtonClick(button);
        Assert.Equal(1, clickCount);

        style.Detach(button);
        InvokeButtonClick(button);
        Assert.Equal(1, clickCount);
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        onClick.Invoke(button, null);
    }

    private sealed class EventSetterCodeBehindHost : UserControl
    {
        public int StyledClickCount { get; private set; }

        private void OnStyledClick(object? sender, RoutedSimpleEventArgs args)
        {
            StyledClickCount++;
        }
    }
}
