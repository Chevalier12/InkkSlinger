using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TriggerActionStoryboardControlTests
{
    [Fact]
    public void SetStoryboardSpeedRatio_Runtime_AcceleratesControllableStoryboard()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var baselineButton = new Button { Opacity = 1f };
            var acceleratedButton = new Button { Opacity = 1f };

            manager.BeginStoryboard(
                BuildOpacityStoryboard(fillBehavior: FillBehavior.HoldEnd),
                baselineButton,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            manager.BeginStoryboard(
                BuildOpacityStoryboard(fillBehavior: FillBehavior.HoldEnd),
                acceleratedButton,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            var action = new SetStoryboardSpeedRatio
            {
                BeginStoryboardName = "Pulse",
                SpeedRatio = 2f
            };
            action.Invoke(acceleratedButton);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250)));

            Assert.InRange(baselineButton.Opacity, 0.7f, 0.8f);
            Assert.InRange(acceleratedButton.Opacity, 0.45f, 0.55f);
            Assert.True(acceleratedButton.Opacity < baselineButton.Opacity);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void SetStoryboardSpeedRatio_Runtime_InvalidSpeedRatio_Throws()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button { Opacity = 1f };
            manager.BeginStoryboard(
                BuildOpacityStoryboard(fillBehavior: FillBehavior.HoldEnd),
                button,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            var invalidValues = new[] { 0f, -1f, float.NaN, float.PositiveInfinity };
            foreach (var invalidValue in invalidValues)
            {
                var action = new SetStoryboardSpeedRatio
                {
                    BeginStoryboardName = "Pulse",
                    SpeedRatio = invalidValue
                };

                var ex = Assert.Throws<InvalidOperationException>(() => action.Invoke(button));
                Assert.Contains("finite positive SpeedRatio", ex.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void SkipStoryboardToFill_Runtime_HoldEnd_AppliesAndHoldsFinalValue()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button { Opacity = 1f };
            manager.BeginStoryboard(
                BuildOpacityStoryboard(fillBehavior: FillBehavior.HoldEnd),
                button,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            new SkipStoryboardToFill
            {
                BeginStoryboardName = "Pulse"
            }.Invoke(button);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)));
            Assert.InRange(button.Opacity, -0.001f, 0.001f);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(300)));
            Assert.InRange(button.Opacity, -0.001f, 0.001f);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void SkipStoryboardToFill_Runtime_Stop_ClearsContributionToBaseValue()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button { Opacity = 0.85f };
            manager.BeginStoryboard(
                BuildOpacityStoryboard(fillBehavior: FillBehavior.Stop),
                button,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            new SkipStoryboardToFill
            {
                BeginStoryboardName = "Pulse"
            }.Invoke(button);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)));
            Assert.InRange(button.Opacity, 0.849f, 0.851f);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void SkipStoryboardToFill_Runtime_ForeverTimeline_IsNoOp()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button { Opacity = 1f };
            var storyboard = BuildOpacityStoryboard(fillBehavior: FillBehavior.HoldEnd);
            storyboard.RepeatBehavior = RepeatBehavior.Forever;

            manager.BeginStoryboard(
                storyboard,
                button,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            new SkipStoryboardToFill
            {
                BeginStoryboardName = "Pulse"
            }.Invoke(button);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250)));
            Assert.InRange(button.Opacity, 0.7f, 0.8f);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void SkipStoryboardToFill_Runtime_ZeroDurationHoldEnd_HoldsFinalValue()
    {
        var manager = AnimationManager.Current;
        manager.ResetForTests();

        try
        {
            var button = new Button { Opacity = 1f };
            var storyboard = new Storyboard();
            storyboard.Children.Add(new DoubleAnimation
            {
                TargetProperty = "Opacity",
                To = 0f,
                Duration = TimeSpan.Zero,
                FillBehavior = FillBehavior.HoldEnd
            });

            manager.BeginStoryboard(
                storyboard,
                button,
                controlName: "Pulse",
                resolveTargetByName: null,
                isControllable: true,
                handoff: HandoffBehavior.SnapshotAndReplace);

            new SkipStoryboardToFill
            {
                BeginStoryboardName = "Pulse"
            }.Invoke(button);

            manager.Update(new GameTime(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
            Assert.InRange(button.Opacity, -0.001f, 0.001f);
        }
        finally
        {
            manager.ResetForTests();
        }
    }

    [Fact]
    public void Xaml_TriggerEnterActions_Supports_SetStoryboardSpeedRatio_And_SkipStoryboardToFill()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.EnterActions>
            <BeginStoryboard Name="Pulse">
              <Storyboard>
                <DoubleAnimation Storyboard.TargetProperty="Opacity" To="0.2" Duration="0:0:1" />
              </Storyboard>
            </BeginStoryboard>
            <SetStoryboardSpeedRatio BeginStoryboardName="Pulse" SpeedRatio="2" />
            <SkipStoryboardToFill BeginStoryboardName="Pulse" />
          </Trigger.EnterActions>
        </Trigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var style = Assert.IsType<Style>(root.Resources["ProbeStyle"]);
        var trigger = Assert.IsType<Trigger>(Assert.Single(style.Triggers));

        Assert.Equal(
            new[] { nameof(BeginStoryboard), nameof(SetStoryboardSpeedRatio), nameof(SkipStoryboardToFill) },
            trigger.EnterActions.Select(static a => a.GetType().Name).ToArray());
    }

    [Fact]
    public void Xaml_EventTriggerActions_Supports_SetStoryboardSpeedRatio_And_SkipStoryboardToFill()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="{x:Type Button}">
            <Border>
              <ContentPresenter />
            </Border>
            <ControlTemplate.Triggers>
              <EventTrigger RoutedEvent="MouseEnter">
                <BeginStoryboard Name="Pulse">
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetProperty="Opacity" To="0.4" Duration="0:0:1" />
                  </Storyboard>
                </BeginStoryboard>
                <SetStoryboardSpeedRatio BeginStoryboardName="Pulse" SpeedRatio="1.5" />
                <SkipStoryboardToFill BeginStoryboardName="Pulse" />
              </EventTrigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var root = (UserControl)XamlLoader.LoadFromString(xaml);
        var style = Assert.IsType<Style>(root.Resources["ProbeStyle"]);
        var templateSetter = Assert.Single(style.Setters.OfType<Setter>(), s => ReferenceEquals(s.Property, Control.TemplateProperty));
        var template = Assert.IsType<ControlTemplate>(templateSetter.Value);
        var eventTrigger = Assert.IsType<EventTrigger>(Assert.Single(template.Triggers));

        Assert.Equal(
            new[] { nameof(BeginStoryboard), nameof(SetStoryboardSpeedRatio), nameof(SkipStoryboardToFill) },
            eventTrigger.Actions.Select(static a => a.GetType().Name).ToArray());
    }

    [Fact]
    public void Xaml_SetStoryboardSpeedRatio_WithInvalidSpeedRatio_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.EnterActions>
            <SetStoryboardSpeedRatio BeginStoryboardName="Pulse" SpeedRatio="0" />
          </Trigger.EnterActions>
        </Trigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("must be finite and greater than 0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_SkipStoryboardToFill_MissingBeginStoryboardName_ThrowsClearError()
    {
        const string xaml = """
<UserControl xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <UserControl.Resources>
    <Style x:Key="ProbeStyle" TargetType="{x:Type Button}">
      <Style.Triggers>
        <Trigger Property="IsEnabled" Value="False">
          <Trigger.EnterActions>
            <SkipStoryboardToFill />
          </Trigger.EnterActions>
        </Trigger>
      </Style.Triggers>
    </Style>
  </UserControl.Resources>
</UserControl>
""";

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => XamlLoader.LoadFromString(xaml));
        Assert.Contains("BeginStoryboardName", ex.Message, StringComparison.Ordinal);
    }

    private static Storyboard BuildOpacityStoryboard(FillBehavior fillBehavior)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = "Opacity",
            To = 0f,
            Duration = TimeSpan.FromSeconds(1),
            FillBehavior = fillBehavior
        });
        return storyboard;
    }
}
