using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    public void XamlStyle_EventSetter_WithHandledEventsToo_ParsesAndHandlesHandledEvents()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeButtonStyle" TargetType="{x:Type Button}">
      <EventSetter Event="Click" Handler="OnStyledClick" HandledEventsToo="True" />
    </Style>
  </UserControl.Resources>
  <Button Name="ProbeButton" Style="{StaticResource ProbeButtonStyle}" Text="Run" />
</UserControl>
""";

        var view = new EventSetterCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var style = Assert.IsType<Style>(view.Resources["ProbeButtonStyle"]);
        var eventSetter = Assert.IsType<EventSetter>(Assert.Single(style.Setters));
        Assert.True(eventSetter.HandledEventsToo);
    }

    [Fact]
    public void XamlStyle_EventSetter_NoArgumentHandler_IsInvoked()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeButtonStyle" TargetType="{x:Type Button}">
      <EventSetter Event="Click" Handler="OnNoArgs" />
    </Style>
  </UserControl.Resources>
  <Button Name="ProbeButton" Style="{StaticResource ProbeButtonStyle}" Text="Run" />
</UserControl>
""";

        var view = new EventSetterCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var button = Assert.IsType<Button>(view.FindName("ProbeButton"));
        InvokeButtonClick(button);

        Assert.Equal(1, view.NoArgClickCount);
    }

    [Fact]
    public void XamlStyle_EventSetter_EventArgsOnlyHandler_IsInvoked()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeButtonStyle" TargetType="{x:Type Button}">
      <EventSetter Event="Click" Handler="OnArgsOnly" />
    </Style>
  </UserControl.Resources>
  <Button Name="ProbeButton" Style="{StaticResource ProbeButtonStyle}" Text="Run" />
</UserControl>
""";

        var view = new EventSetterCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var button = Assert.IsType<Button>(view.FindName("ProbeButton"));
        InvokeButtonClick(button);

        Assert.Equal(1, view.ArgsOnlyClickCount);
    }

    [Fact]
    public void XamlStyle_EventSetter_SenderAndArgsAsObject_IsInvoked()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeButtonStyle" TargetType="{x:Type Button}">
      <EventSetter Event="Click" Handler="OnObjectArgs" />
    </Style>
  </UserControl.Resources>
  <Button Name="ProbeButton" Style="{StaticResource ProbeButtonStyle}" Text="Run" />
</UserControl>
""";

        var view = new EventSetterCodeBehindHost();
        XamlLoader.LoadIntoFromString(view, xaml, view);

        var button = Assert.IsType<Button>(view.FindName("ProbeButton"));
        InvokeButtonClick(button);

        Assert.Equal(1, view.ObjectArgClickCount);
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
    public void CodeOnlyEventSetter_HandledEventsTooFalse_SkipsHandledEvents()
    {
        var clickCount = 0;
        var style = new Style(typeof(Button));
        style.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => clickCount++),
                handledEventsToo: false));

        var button = new Button();
        button.AddHandler(
            Button.ClickEvent,
            new EventHandler<RoutedSimpleEventArgs>((_, args) => args.Handled = true));

        style.Apply(button);
        InvokeButtonClick(button);

        Assert.Equal(0, clickCount);
    }

    [Fact]
    public void CodeOnlyEventSetter_HandledEventsTooTrue_ReceivesHandledEvents()
    {
        var clickCount = 0;
        var style = new Style(typeof(Button));
        style.Setters.Add(
            new EventSetter(
                "Click",
                new EventHandler<RoutedSimpleEventArgs>((_, _) => clickCount++),
                handledEventsToo: true));

        var button = new Button();
        button.AddHandler(
            Button.ClickEvent,
            new EventHandler<RoutedSimpleEventArgs>((_, args) => args.Handled = true));

        style.Apply(button);
        InvokeButtonClick(button);

        Assert.Equal(1, clickCount);
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

    [Fact]
    public void CodeOnlyEventSetter_MismatchedSenderType_ThrowsWithContextualDetails()
    {
        static void InvalidHandler(TextBox sender, RoutedSimpleEventArgs args)
        {
            _ = sender;
            _ = args;
        }

        var style = new Style(typeof(Button));
        style.Setters.Add(new EventSetter("Click", (Action<TextBox, RoutedSimpleEventArgs>)InvalidHandler));

        var button = new Button();
        style.Apply(button);

        var ex = Assert.Throws<InvalidOperationException>(() => InvokeButtonClick(button));
        Assert.Contains("Style.TargetType 'Button'", ex.Message);
        Assert.Contains("event 'Click'", ex.Message);
        Assert.Contains("InvalidHandler", ex.Message);
        Assert.NotNull(ex.InnerException);
        Assert.Contains("parameter type 'TextBox'", ex.InnerException!.Message);
    }

    private static void InvokeButtonClick(Button button)
    {
        var onClick = typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onClick);
        try
        {
            onClick.Invoke(button, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private sealed class EventSetterCodeBehindHost : UserControl
    {
        public int StyledClickCount { get; private set; }
        public int NoArgClickCount { get; private set; }
        public int ArgsOnlyClickCount { get; private set; }
        public int ObjectArgClickCount { get; private set; }

        private void OnStyledClick(object? sender, RoutedSimpleEventArgs args)
        {
            StyledClickCount++;
        }

        private void OnNoArgs()
        {
            NoArgClickCount++;
        }

        private void OnArgsOnly(RoutedSimpleEventArgs args)
        {
            ArgsOnlyClickCount++;
        }

        private void OnObjectArgs(object? sender, object? args)
        {
            ObjectArgClickCount++;
        }
    }
}
