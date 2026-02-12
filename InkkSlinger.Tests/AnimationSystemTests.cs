using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class AnimationSystemTests
{
    public AnimationSystemTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        AnimationManager.Current.ResetForTests();
    }

    [Fact]
    public void AnimationValue_OverridesLocal_AndRestoresLocalAfterRemove()
    {
        var button = new Button { Opacity = 0.8f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);

        Assert.Equal(DependencyPropertyValueSource.Animation, button.GetValueSource(UIElement.OpacityProperty));
        Assert.Equal(0.5f, button.Opacity, 2);

        storyboard.Remove(button);
        UpdateAt(0.6);

        Assert.Equal(DependencyPropertyValueSource.Local, button.GetValueSource(UIElement.OpacityProperty));
        Assert.Equal(0.8f, button.Opacity, 2);
    }

    [Fact]
    public void DoubleAnimationUsingKeyFrames_InterpolatesSegments()
    {
        var button = new Button { Opacity = 0f };
        var animation = new DoubleAnimationUsingKeyFrames
        {
            TargetProperty = nameof(UIElement.Opacity),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.4f, TimeSpan.FromSeconds(0.5)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1f, TimeSpan.FromSeconds(1)));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(button, isControllable: true);

        UpdateAt(0.25);
        Assert.Equal(0.2f, button.Opacity, 2);

        UpdateAt(0.75);
        Assert.Equal(0.7f, button.Opacity, 2);
    }

    [Fact]
    public void DoubleAnimationUsingKeyFrames_UniformKeyTimes_AreDistributedEvenly()
    {
        var button = new Button { Opacity = 0f };
        var animation = new DoubleAnimationUsingKeyFrames
        {
            TargetProperty = nameof(UIElement.Opacity),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.4f, KeyTime = KeyTime.Uniform });
        animation.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 1f, KeyTime = KeyTime.Uniform });

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(button, isControllable: true);

        UpdateAt(0.25);
        Assert.Equal(0.2f, button.Opacity, 2);

        UpdateAt(0.75);
        Assert.Equal(0.7f, button.Opacity, 2);
    }

    [Fact]
    public void DoubleAnimationUsingKeyFrames_PacedKeyTimes_AreDistanceWeighted()
    {
        var button = new Button { Opacity = 0f };
        var animation = new DoubleAnimationUsingKeyFrames
        {
            TargetProperty = nameof(UIElement.Opacity),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.9f, KeyTime = KeyTime.Paced });
        animation.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 1f, KeyTime = KeyTime.Paced });

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(button, isControllable: true);

        UpdateAt(0.5);
        Assert.InRange(button.Opacity, 0.49f, 0.51f);

        UpdateAt(0.9);
        Assert.InRange(button.Opacity, 0.89f, 0.91f);
    }

    [Fact]
    public void KeySpline_WithLinearControlPoints_ProducesLinearProgress()
    {
        var spline = new KeySpline(new Vector2(0f, 0f), new Vector2(1f, 1f));
        var eased = spline.Ease(0.5f);
        Assert.InRange(eased, 0.49f, 0.51f);
    }

    [Fact]
    public void KeySpline_CanBiasProgress_NonLinearly()
    {
        var spline = new KeySpline(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
        var eased = spline.Ease(0.5f);
        Assert.True(eased > 0.7f);
    }

    [Fact]
    public void PointAnimation_InterpolatesVector2Value()
    {
        var host = new AnimationVectorHost { Anchor = new Vector2(0f, 0f) };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new PointAnimation
        {
            TargetProperty = nameof(AnimationVectorHost.Anchor),
            From = new Vector2(0f, 0f),
            To = new Vector2(10f, 20f),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(host, isControllable: true);
        UpdateAt(0.5);

        Assert.Equal(5f, host.Anchor.X, 2);
        Assert.Equal(10f, host.Anchor.Y, 2);
    }

    [Fact]
    public void ThicknessAnimation_InterpolatesFrameworkElementMargin()
    {
        var button = new Button { Margin = new Thickness(0f, 0f, 0f, 0f) };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new ThicknessAnimation
        {
            TargetProperty = nameof(FrameworkElement.Margin),
            From = new Thickness(0f, 0f, 0f, 0f),
            To = new Thickness(10f, 20f, 30f, 40f),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);

        Assert.Equal(5f, button.Margin.Left, 2);
        Assert.Equal(10f, button.Margin.Top, 2);
        Assert.Equal(15f, button.Margin.Right, 2);
        Assert.Equal(20f, button.Margin.Bottom, 2);
    }

    [Fact]
    public void PointAnimationUsingKeyFrames_InterpolatesSegments()
    {
        var host = new AnimationVectorHost { Anchor = new Vector2(0f, 0f) };
        var animation = new PointAnimationUsingKeyFrames
        {
            TargetProperty = nameof(AnimationVectorHost.Anchor),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearPointKeyFrame(new Vector2(4f, 8f), TimeSpan.FromSeconds(0.5)));
        animation.KeyFrames.Add(new LinearPointKeyFrame(new Vector2(10f, 20f), TimeSpan.FromSeconds(1)));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(host, isControllable: true);

        UpdateAt(0.25);
        Assert.Equal(2f, host.Anchor.X, 2);
        Assert.Equal(4f, host.Anchor.Y, 2);

        UpdateAt(0.75);
        Assert.Equal(7f, host.Anchor.X, 2);
        Assert.Equal(14f, host.Anchor.Y, 2);
    }

    [Fact]
    public void ThicknessAnimationUsingKeyFrames_InterpolatesSegments()
    {
        var button = new Button { Margin = new Thickness(0f, 0f, 0f, 0f) };
        var animation = new ThicknessAnimationUsingKeyFrames
        {
            TargetProperty = nameof(FrameworkElement.Margin),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(4f, 4f, 4f, 4f), TimeSpan.FromSeconds(0.5)));
        animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(10f, 20f, 30f, 40f), TimeSpan.FromSeconds(1)));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(button, isControllable: true);

        UpdateAt(0.25);
        Assert.Equal(2f, button.Margin.Left, 2);

        UpdateAt(0.75);
        Assert.Equal(7f, button.Margin.Left, 2);
        Assert.Equal(12f, button.Margin.Top, 2);
        Assert.Equal(17f, button.Margin.Right, 2);
        Assert.Equal(22f, button.Margin.Bottom, 2);
    }

    [Fact]
    public void Int32Animation_InterpolatesIntegerValue()
    {
        var host = new AnimationIntHost { Counter = 0 };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new Int32Animation
        {
            TargetProperty = nameof(AnimationIntHost.Counter),
            From = 0,
            To = 10,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(host, isControllable: true);
        UpdateAt(0.5);

        Assert.Equal(5, host.Counter);
    }

    [Fact]
    public void Int32AnimationUsingKeyFrames_InterpolatesSegments()
    {
        var host = new AnimationIntHost { Counter = 0 };
        var animation = new Int32AnimationUsingKeyFrames
        {
            TargetProperty = nameof(AnimationIntHost.Counter),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        };
        animation.KeyFrames.Add(new LinearInt32KeyFrame(4, TimeSpan.FromSeconds(0.5)));
        animation.KeyFrames.Add(new LinearInt32KeyFrame(10, TimeSpan.FromSeconds(1)));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin(host, isControllable: true);

        UpdateAt(0.25);
        Assert.Equal(2, host.Counter);

        UpdateAt(0.75);
        Assert.Equal(7, host.Counter);
    }

    [Fact]
    public void Storyboard_TargetProperty_AttachedSyntax_Resolves()
    {
        var button = new Button { Opacity = 0f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = "(UIElement.Opacity)",
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);

        Assert.True(button.Opacity > 0.45f);
    }

    [Fact]
    public void Storyboard_PauseResumeSeek_Works()
    {
        var button = new Button { Opacity = 0f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.4);
        var pausedAt = button.Opacity;

        storyboard.Pause(button);
        UpdateAt(0.8);
        Assert.Equal(pausedAt, button.Opacity, 3);

        storyboard.Resume(button);
        UpdateAt(1.0);
        Assert.True(button.Opacity > pausedAt);

        storyboard.Seek(button, TimeSpan.FromSeconds(0.2));
        UpdateAt(1.1);
        Assert.True(button.Opacity < 0.35f);
    }

    [Fact]
    public void SeekStoryboard_UsesOriginDuration()
    {
        var button = new Button { Opacity = 0f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });
        storyboard.Begin(button, isControllable: true);

        var seek = new SeekStoryboard
        {
            BeginStoryboardName = string.Empty,
            Offset = TimeSpan.FromSeconds(0.2),
            Origin = TimeSeekOrigin.Duration
        };

        // Wire through controllable channel for parity with actions.
        AnimationManager.Current.BeginStoryboard(storyboard, button, "SeekToken", null, true, HandoffBehavior.SnapshotAndReplace);
        seek.BeginStoryboardName = "SeekToken";
        seek.Invoke(button);

        UpdateAt(0.05);
        Assert.True(button.Opacity > 0.7f);
    }

    [Fact]
    public void SeekStoryboard_OriginDuration_AccountsForChildBeginTime()
    {
        var button = new Button { Opacity = 0f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            BeginTime = TimeSpan.FromSeconds(0.5),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(storyboard, button, "SeekWithDelay", null, true, HandoffBehavior.SnapshotAndReplace);
        var seek = new SeekStoryboard
        {
            BeginStoryboardName = "SeekWithDelay",
            Offset = TimeSpan.FromSeconds(0.25),
            Origin = TimeSeekOrigin.Duration
        };

        seek.Invoke(button);
        UpdateAt(0.05);

        Assert.InRange(button.Opacity, 0.70f, 0.80f);
    }

    [Fact]
    public void FillBehaviorStop_ClearsAnimatedValue_AtTimelineBoundary()
    {
        var button = new Button { Opacity = 0.8f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            FillBehavior = FillBehavior.Stop
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(1.0);

        Assert.Equal(DependencyPropertyValueSource.Local, button.GetValueSource(UIElement.OpacityProperty));
        Assert.Equal(0.8f, button.Opacity, 2);
    }

    [Fact]
    public void TriggerActions_BeginAndStopStoryboard_AffectValue()
    {
        var button = new Button();
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        var style = new Style(typeof(Button));
        var trigger = new Trigger(UIElement.IsEnabledProperty, false);
        trigger.EnterActions.Add(new BeginStoryboard { Name = "Pulse", Storyboard = storyboard });
        trigger.ExitActions.Add(new StopStoryboard { BeginStoryboardName = "Pulse" });
        style.Triggers.Add(trigger);
        button.Style = style;

        button.IsEnabled = false;
        UpdateAt(0.5);
        Assert.True(button.Opacity > 0.45f);

        button.IsEnabled = true;
        UpdateAt(0.6);
        Assert.NotEqual(DependencyPropertyValueSource.Animation, button.GetValueSource(UIElement.OpacityProperty));
    }

    [Fact]
    public void XamlLoader_ParsesStoryboardTriggerActions_AndTargetMetadata()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="HostButton"
                                      Text="Animate">
                                <Button.Style>
                                  <Style TargetType="Button">
                                    <Style.Triggers>
                                      <Trigger Property="IsEnabled" Value="False">
                                        <Trigger.EnterActions>
                                          <BeginStoryboard Name="FadeIn">
                                            <Storyboard>
                                              <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                                               From="0"
                                                               To="1"
                                                               Duration="0:0:1" />
                                            </Storyboard>
                                          </BeginStoryboard>
                                        </Trigger.EnterActions>
                                        <Trigger.ExitActions>
                                          <StopStoryboard BeginStoryboardName="FadeIn" />
                                        </Trigger.ExitActions>
                                      </Trigger>
                                    </Style.Triggers>
                                  </Style>
                                </Button.Style>
                              </Button>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);
        var button = Assert.IsType<Button>(view.Content);

        button.IsEnabled = false;
        UpdateAt(0.5);
        Assert.True(button.Opacity > 0.45f);

        button.IsEnabled = true;
        UpdateAt(0.6);
        Assert.NotEqual(DependencyPropertyValueSource.Animation, button.GetValueSource(UIElement.OpacityProperty));
    }

    [Fact]
    public void VisualStateGroup_UsesStoryboardWhenProvided()
    {
        var button = new Button { Opacity = 0f };
        var state = new VisualState("Visible");
        state.Storyboard = new Storyboard();
        state.Storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        var group = new VisualStateGroup("CommonStates");
        group.States.Add(state);

        Assert.True(group.GoToState(button, "Visible"));
        UpdateAt(0.5);
        Assert.True(button.Opacity > 0.45f);
    }

    [Fact]
    public void RepeatAndAutoReverse_AreApplied()
    {
        var button = new Button { Opacity = 0f };
        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            RepeatBehavior = new RepeatBehavior(2d),
            AutoReverse = true
        });

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);
        var firstHalf = button.Opacity;
        UpdateAt(1.5);
        var reverseHalf = button.Opacity;

        Assert.True(firstHalf > 0.45f);
        Assert.True(reverseHalf < 0.55f);
    }

    [Fact]
    public void Storyboard_TargetName_ResolvesViaNameScope()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid x:Name="RootGrid">
                                <Button x:Name="TargetButton" Opacity="0" />
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);
        var grid = Assert.IsType<Grid>(view.Content);
        var button = Assert.IsType<Button>(Assert.Single(grid.Children));

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "TargetButton",
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        storyboard.Begin(grid, isControllable: true);
        UpdateAt(0.5);

        Assert.True(button.Opacity > 0.45f);
    }

    [Fact]
    public void Storyboard_TargetProperty_NestedClrPath_Animates()
    {
        var host = new Button();
        var group = new TransformGroup();
        var rotate = new RotateTransform { Angle = 0f };
        group.Children.Add(rotate);

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "TransformRoot",
            TargetProperty = "Children[0].Angle",
            From = 0f,
            To = 90f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            host,
            controlName: null,
            resolveTargetByName: name => string.Equals(name, "TransformRoot", StringComparison.Ordinal) ? group : null,
            isControllable: true,
            handoff: HandoffBehavior.SnapshotAndReplace);

        UpdateAt(0.5);
        Assert.True(rotate.Angle > 40f && rotate.Angle < 50f);
    }

    [Fact]
    public void Storyboard_TargetProperty_OwnerQualifiedNestedPath_Animates()
    {
        var host = new Button();
        var group = new TransformGroup();
        var rotate = new RotateTransform { Angle = 0f };
        group.Children.Add(rotate);

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "TransformRoot",
            TargetProperty = "(TransformGroup.Children)[0].(RotateTransform.Angle)",
            From = 0f,
            To = 90f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            host,
            controlName: null,
            resolveTargetByName: name => string.Equals(name, "TransformRoot", StringComparison.Ordinal) ? group : null,
            isControllable: true,
            handoff: HandoffBehavior.SnapshotAndReplace);

        UpdateAt(0.5);
        Assert.True(rotate.Angle > 40f && rotate.Angle < 50f);
    }

    [Fact]
    public void Storyboard_TargetProperty_MultiIndexCollectionPath_Animates()
    {
        var host = new Button();
        var model = new IndexedPathModel();
        model.Buckets.Add(new System.Collections.Generic.List<IndexedPathLeaf>
        {
            new() { Value = 0f },
            new() { Value = 0f }
        });

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "PathRoot",
            TargetProperty = "Buckets[0][1].Value",
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            host,
            controlName: null,
            resolveTargetByName: name => string.Equals(name, "PathRoot", StringComparison.Ordinal) ? model : null,
            isControllable: true,
            handoff: HandoffBehavior.SnapshotAndReplace);

        UpdateAt(0.5);
        Assert.InRange(model.Buckets[0][1].Value, 0.49f, 0.51f);
    }

    [Fact]
    public void Storyboard_TargetProperty_StringIndexerPath_Animates()
    {
        var host = new Button();
        var model = new IndexedPathMapModel();
        model.Map["first"] = new IndexedPathLeaf { Value = 0f };

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "PathRoot",
            TargetProperty = "Map[first].Value",
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            host,
            controlName: null,
            resolveTargetByName: name => string.Equals(name, "PathRoot", StringComparison.Ordinal) ? model : null,
            isControllable: true,
            handoff: HandoffBehavior.SnapshotAndReplace);

        UpdateAt(0.5);
        Assert.InRange(model.Map["first"].Value, 0.49f, 0.51f);
    }

    [Fact]
    public void Storyboard_TargetProperty_OwnerQualifiedPath_WithWhitespace_Animates()
    {
        var host = new Button();
        var group = new TransformGroup();
        var rotate = new RotateTransform { Angle = 0f };
        group.Children.Add(rotate);

        var storyboard = new Storyboard();
        storyboard.Children.Add(new DoubleAnimation
        {
            TargetName = "TransformRoot",
            TargetProperty = " ( TransformGroup.Children ) [ 0 ] . ( RotateTransform.Angle ) ",
            From = 0f,
            To = 90f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        AnimationManager.Current.BeginStoryboard(
            storyboard,
            host,
            controlName: null,
            resolveTargetByName: name => string.Equals(name, "TransformRoot", StringComparison.Ordinal) ? group : null,
            isControllable: true,
            handoff: HandoffBehavior.SnapshotAndReplace);

        UpdateAt(0.5);
        Assert.True(rotate.Angle > 40f && rotate.Angle < 50f);
    }

    [Fact]
    public void Storyboard_NestedChildStoryboard_AppliesBeginTimeAndSpeedRatio()
    {
        var button = new Button { Opacity = 0f };
        var outer = new Storyboard();
        var nested = new Storyboard
        {
            BeginTime = TimeSpan.FromSeconds(0.2),
            SpeedRatio = 2f
        };
        nested.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });
        outer.Children.Add(nested);

        outer.Begin(button, isControllable: true);
        UpdateAt(0.45);

        Assert.InRange(button.Opacity, 0.48f, 0.52f);
    }

    [Fact]
    public void HandoffBehavior_Compose_CombinesConcurrentAnimations()
    {
        var button = new Button { Opacity = 0f };

        var first = new Storyboard();
        first.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        var second = new Storyboard();
        second.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        first.Begin(button, isControllable: true, handoff: HandoffBehavior.SnapshotAndReplace);
        UpdateAt(0.5);
        var firstOnly = button.Opacity;

        second.Begin(button, isControllable: true, handoff: HandoffBehavior.Compose);
        UpdateAt(1.0);
        var composed = button.Opacity;

        Assert.True(firstOnly > 0.45f);
        Assert.True(composed > firstOnly);
    }

    [Fact]
    public void HandoffBehavior_Compose_CombinesThicknessAnimations()
    {
        var button = new Button { Margin = new Thickness(0f, 0f, 0f, 0f) };

        var first = new Storyboard();
        first.Children.Add(new ThicknessAnimation
        {
            TargetProperty = nameof(FrameworkElement.Margin),
            From = new Thickness(0f, 0f, 0f, 0f),
            To = new Thickness(10f, 10f, 10f, 10f),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        var second = new Storyboard();
        second.Children.Add(new ThicknessAnimation
        {
            TargetProperty = nameof(FrameworkElement.Margin),
            To = new Thickness(20f, 20f, 20f, 20f),
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        first.Begin(button, isControllable: true, handoff: HandoffBehavior.SnapshotAndReplace);
        UpdateAt(0.5);
        var firstOnly = button.Margin.Left;

        second.Begin(button, isControllable: true, handoff: HandoffBehavior.Compose);
        UpdateAt(1.0);
        var composed = button.Margin.Left;

        Assert.True(firstOnly > 4.5f);
        Assert.True(composed > firstOnly);
    }

    [Fact]
    public void HandoffBehavior_SnapshotAndReplace_ReplacesPreviousLane()
    {
        var button = new Button { Opacity = 0f };

        var first = new Storyboard();
        first.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(2))
        });

        var second = new Storyboard();
        second.Children.Add(new DoubleAnimation
        {
            TargetProperty = nameof(UIElement.Opacity),
            From = 0f,
            To = 1f,
            Duration = new Duration(TimeSpan.FromSeconds(1))
        });

        first.Begin(button, isControllable: true, handoff: HandoffBehavior.SnapshotAndReplace);
        UpdateAt(0.5);

        second.Begin(button, isControllable: true, handoff: HandoffBehavior.SnapshotAndReplace);
        UpdateAt(1.0);

        // If first lane was not replaced we'd be above this by additive composition.
        Assert.InRange(button.Opacity, 0.45f, 0.55f);
    }

    [Fact]
    public void EventTrigger_StartsStoryboard_OnRoutedEvent()
    {
        var button = new TriggerTestButton { Opacity = 0f };
        var style = new Style(typeof(TriggerTestButton));

        var trigger = new EventTrigger { RoutedEvent = "MouseEnter" };
        trigger.Actions.Add(new BeginStoryboard
        {
            Name = "HoverFade",
            Storyboard = new Storyboard
            {
                Children =
                {
                    new DoubleAnimation
                    {
                        TargetProperty = nameof(UIElement.Opacity),
                        From = 0f,
                        To = 1f,
                        Duration = new Duration(TimeSpan.FromSeconds(1))
                    }
                }
            }
        });
        style.Triggers.Add(trigger);
        button.Style = style;

        button.FireMouseEnter();
        UpdateAt(0.5);
        Assert.True(button.Opacity > 0.45f);
    }

    [Fact]
    public void XamlLoader_ParsesEventTrigger_And_ObjectAnimationUsingKeyFrames()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="HostButton" Text="Idle">
                                <Button.Style>
                                  <Style TargetType="Button">
                                    <Style.Triggers>
                                      <EventTrigger RoutedEvent="MouseEnter">
                                        <EventTrigger.Actions>
                                          <BeginStoryboard Name="SwapText">
                                            <Storyboard>
                                              <ObjectAnimationUsingKeyFrames Storyboard.TargetProperty="Text">
                                                <DiscreteObjectKeyFrame KeyTime="0:0:0.1" Value="Hovered" />
                                              </ObjectAnimationUsingKeyFrames>
                                            </Storyboard>
                                          </BeginStoryboard>
                                        </EventTrigger.Actions>
                                      </EventTrigger>
                                    </Style.Triggers>
                                  </Style>
                                </Button.Style>
                              </Button>
                            </UserControl>
                            """;

        var host = new UserControl();
        XamlLoader.LoadIntoFromString(host, xaml, null);
        var button = Assert.IsType<Button>(host.Content);

        var probe = new TriggerTestButton();
        probe.Style = button.Style;
        probe.Text = "Idle";
        probe.FireMouseEnter();
        UpdateAt(0.2);

        Assert.Equal("Hovered", probe.Text);
    }

    [Fact]
    public void XamlLoader_Parses_PointAndThicknessAnimationTypes()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="Motion">
                                  <PointAnimation Storyboard.TargetProperty="Anchor"
                                                  From="0,0"
                                                  To="10,20"
                                                  Duration="0:0:1" />
                                  <ThicknessAnimation Storyboard.TargetProperty="Margin"
                                                      From="0"
                                                      To="4,8,12,16"
                                                      Duration="0:0:1" />
                                  <PointAnimationUsingKeyFrames Storyboard.TargetProperty="Anchor">
                                    <LinearPointKeyFrame KeyTime="0:0:0.5" Value="5,10" />
                                    <SplinePointKeyFrame KeyTime="0:0:1" Value="10,20" />
                                  </PointAnimationUsingKeyFrames>
                                  <ThicknessAnimationUsingKeyFrames Storyboard.TargetProperty="Margin">
                                    <LinearThicknessKeyFrame KeyTime="0:0:0.5" Value="1" />
                                    <SplineThicknessKeyFrame KeyTime="0:0:1" Value="4,8,12,16" />
                                  </ThicknessAnimationUsingKeyFrames>
                                </Storyboard>
                              </UserControl.Resources>
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        Assert.True(host.Resources.TryGetValue("Motion", out var storyboardObject));
        var storyboard = Assert.IsType<Storyboard>(storyboardObject);
        Assert.Equal(4, storyboard.Children.Count);

        var pointAnimation = Assert.IsType<PointAnimation>(storyboard.Children[0]);
        Assert.Equal(new Vector2(0f, 0f), pointAnimation.From);
        Assert.Equal(new Vector2(10f, 20f), pointAnimation.To);

        var thicknessAnimation = Assert.IsType<ThicknessAnimation>(storyboard.Children[1]);
        Assert.Equal(12f, thicknessAnimation.To!.Value.Right, 2);

        var pointKeyFrames = Assert.IsType<PointAnimationUsingKeyFrames>(storyboard.Children[2]);
        Assert.Equal(2, pointKeyFrames.KeyFrames.Count);
        Assert.IsType<SplinePointKeyFrame>(pointKeyFrames.KeyFrames[1]);

        var thicknessKeyFrames = Assert.IsType<ThicknessAnimationUsingKeyFrames>(storyboard.Children[3]);
        Assert.Equal(2, thicknessKeyFrames.KeyFrames.Count);
        Assert.IsType<SplineThicknessKeyFrame>(thicknessKeyFrames.KeyFrames[1]);
    }

    [Fact]
    public void XamlLoader_Parses_DoubleAnimation_EasingFunction_AndAppliesCurve()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="EaseMotion">
                                  <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                                   From="0"
                                                   To="1"
                                                   Duration="0:0:1">
                                    <DoubleAnimation.EasingFunction>
                                      <QuadraticEase EasingMode="EaseIn" />
                                    </DoubleAnimation.EasingFunction>
                                  </DoubleAnimation>
                                </Storyboard>
                              </UserControl.Resources>
                              <Button x:Name="TargetButton" Opacity="0" />
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var button = Assert.IsType<Button>(host.Content);
        var storyboard = Assert.IsType<Storyboard>(host.Resources["EaseMotion"]);

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);

        Assert.True(button.Opacity > 0.2f && button.Opacity < 0.3f);
    }

    [Fact]
    public void XamlLoader_Parses_SplineKeyFrame_KeySpline_EasingFunction()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="SplineMotion">
                                  <DoubleAnimationUsingKeyFrames Storyboard.TargetProperty="Opacity">
                                    <SplineDoubleKeyFrame KeyTime="0:0:1" Value="1">
                                      <SplineDoubleKeyFrame.KeySpline>
                                        <CubicEase EasingMode="EaseIn" />
                                      </SplineDoubleKeyFrame.KeySpline>
                                    </SplineDoubleKeyFrame>
                                  </DoubleAnimationUsingKeyFrames>
                                </Storyboard>
                              </UserControl.Resources>
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var storyboard = Assert.IsType<Storyboard>(host.Resources["SplineMotion"]);
        var animation = Assert.IsType<DoubleAnimationUsingKeyFrames>(Assert.Single(storyboard.Children));
        var keyFrame = Assert.IsType<SplineDoubleKeyFrame>(Assert.Single(animation.KeyFrames));
        Assert.IsType<CubicEase>(keyFrame.KeySpline);
    }

    [Fact]
    public void XamlLoader_Parses_UniformAndPaced_KeyTimeValues()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="KeyTimeMotion">
                                  <DoubleAnimationUsingKeyFrames Storyboard.TargetProperty="Opacity"
                                                                 Duration="0:0:1">
                                    <LinearDoubleKeyFrame KeyTime="Uniform" Value="0.9" />
                                    <LinearDoubleKeyFrame KeyTime="Paced" Value="1" />
                                  </DoubleAnimationUsingKeyFrames>
                                </Storyboard>
                              </UserControl.Resources>
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var storyboard = Assert.IsType<Storyboard>(host.Resources["KeyTimeMotion"]);
        var animation = Assert.IsType<DoubleAnimationUsingKeyFrames>(Assert.Single(storyboard.Children));
        Assert.Equal(KeyTimeType.Uniform, animation.KeyFrames[0].KeyTime.Type);
        Assert.Equal(KeyTimeType.Paced, animation.KeyFrames[1].KeyTime.Type);
    }

    [Fact]
    public void XamlLoader_Parses_KeySpline_Element_And_UsesBezierInterpolation()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="SplineMotion">
                                  <DoubleAnimationUsingKeyFrames Storyboard.TargetProperty="Opacity"
                                                                 Duration="0:0:1">
                                    <SplineDoubleKeyFrame KeyTime="0:0:1" Value="1">
                                      <SplineDoubleKeyFrame.KeySpline>
                                        <KeySpline ControlPoint1="0.1,0.9" ControlPoint2="0.2,1" />
                                      </SplineDoubleKeyFrame.KeySpline>
                                    </SplineDoubleKeyFrame>
                                  </DoubleAnimationUsingKeyFrames>
                                </Storyboard>
                              </UserControl.Resources>
                              <Button Opacity="0" />
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var storyboard = Assert.IsType<Storyboard>(host.Resources["SplineMotion"]);
        var button = Assert.IsType<Button>(host.Content);

        storyboard.Begin(button, isControllable: true);
        UpdateAt(0.5);
        Assert.True(button.Opacity > 0.7f);
    }

    [Fact]
    public void XamlLoader_Parses_Int32AnimationTypes()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Storyboard x:Key="CounterMotion">
                                  <Int32Animation Storyboard.TargetProperty="Counter"
                                                  From="0"
                                                  To="10"
                                                  Duration="0:0:1">
                                    <Int32Animation.EasingFunction>
                                      <QuadraticEase EasingMode="EaseIn" />
                                    </Int32Animation.EasingFunction>
                                  </Int32Animation>
                                  <Int32AnimationUsingKeyFrames Storyboard.TargetProperty="Counter">
                                    <LinearInt32KeyFrame KeyTime="0:0:0.5" Value="4" />
                                    <SplineInt32KeyFrame KeyTime="0:0:1" Value="10">
                                      <SplineInt32KeyFrame.KeySpline>
                                        <SineEase EasingMode="EaseIn" />
                                      </SplineInt32KeyFrame.KeySpline>
                                    </SplineInt32KeyFrame>
                                  </Int32AnimationUsingKeyFrames>
                                </Storyboard>
                              </UserControl.Resources>
                            </UserControl>
                            """;

        var host = Assert.IsType<UserControl>(XamlLoader.LoadFromString(xaml));
        var storyboard = Assert.IsType<Storyboard>(host.Resources["CounterMotion"]);
        Assert.Equal(2, storyboard.Children.Count);

        var intAnimation = Assert.IsType<Int32Animation>(storyboard.Children[0]);
        Assert.IsType<QuadraticEase>(intAnimation.EasingFunction);

        var intKeyFrameAnimation = Assert.IsType<Int32AnimationUsingKeyFrames>(storyboard.Children[1]);
        var spline = Assert.IsType<SplineInt32KeyFrame>(intKeyFrameAnimation.KeyFrames[1]);
        Assert.IsType<SineEase>(spline.KeySpline);
    }

    [Fact]
    public void EventTrigger_OwnerQualifiedRoutedEvent_Resolves()
    {
        var button = new TriggerTestButton { Opacity = 1f };
        var style = new Style(typeof(TriggerTestButton));
        style.Triggers.Add(new EventTrigger
        {
            RoutedEvent = "Button.Click",
            Actions =
            {
                new SetValueAction(UIElement.OpacityProperty, 0.2f)
            }
        });

        button.Style = style;
        button.FireClick();

        Assert.Equal(0.2f, button.Opacity, 3);
    }

    [Fact]
    public void MultipleEventTriggers_OnSameElement_DoNotClobberEachOther()
    {
        var button = new TriggerTestButton { Opacity = 1f };
        var style = new Style(typeof(TriggerTestButton));
        style.Triggers.Add(new EventTrigger
        {
            RoutedEvent = "MouseEnter",
            Actions =
            {
                new SetValueAction(UIElement.OpacityProperty, 0.2f)
            }
        });
        style.Triggers.Add(new EventTrigger
        {
            RoutedEvent = "Click",
            Actions =
            {
                new SetValueAction(UIElement.OpacityProperty, 0.7f)
            }
        });

        button.Style = style;
        button.FireMouseEnter();
        Assert.Equal(0.2f, button.Opacity, 3);

        button.FireClick();
        Assert.Equal(0.7f, button.Opacity, 3);
    }

    [Fact]
    public void EventTrigger_SourceName_ResolvesTemplatePart()
    {
        var host = new TemplateEventHost();
        host.Template = new ControlTemplate(_ =>
        {
            return new TriggerTestButton { Name = "PART_Source" };
        });

        var style = new Style(typeof(TemplateEventHost));
        style.Triggers.Add(new EventTrigger
        {
            RoutedEvent = "Click",
            SourceName = "PART_Source",
            Actions =
            {
                new SetValueAction(UIElement.OpacityProperty, 0.3f)
            }
        });
        host.ApplyTemplate();
        host.Style = style;

        var source = Assert.IsType<TriggerTestButton>(host.SourcePart);
        source.FireClick();

        Assert.Equal(0.3f, host.Opacity, 3);
    }

    private static void UpdateAt(double seconds)
    {
        var total = TimeSpan.FromSeconds(seconds);
        AnimationManager.Current.Update(new GameTime(total, TimeSpan.FromSeconds(1d / 60d)));
    }

    private sealed class TriggerTestButton : Button
    {
        public void FireMouseEnter()
        {
            RaiseMouseEnter(Vector2.Zero, ModifierKeys.None);
        }

        public void FireClick()
        {
            OnClick();
        }
    }

    private sealed class TemplateEventHost : Control
    {
        public UIElement? SourcePart { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var method = typeof(Control).GetMethod(
                "GetTemplateChild",
                BindingFlags.Instance | BindingFlags.NonPublic);
            SourcePart = method?.Invoke(this, new object?[] { "PART_Source" }) as UIElement;
        }
    }

    private sealed class AnimationVectorHost : FrameworkElement
    {
        public static readonly DependencyProperty AnchorProperty =
            DependencyProperty.Register(
                nameof(Anchor),
                typeof(Vector2),
                typeof(AnimationVectorHost),
                new FrameworkPropertyMetadata(Vector2.Zero));

        public Vector2 Anchor
        {
            get => GetValue<Vector2>(AnchorProperty);
            set => SetValue(AnchorProperty, value);
        }
    }

    private sealed class AnimationIntHost : FrameworkElement
    {
        public static readonly DependencyProperty CounterProperty =
            DependencyProperty.Register(
                nameof(Counter),
                typeof(int),
                typeof(AnimationIntHost),
                new FrameworkPropertyMetadata(0));

        public int Counter
        {
            get => GetValue<int>(CounterProperty);
            set => SetValue(CounterProperty, value);
        }
    }

    private sealed class IndexedPathLeaf
    {
        public float Value { get; set; }
    }

    private sealed class IndexedPathModel
    {
        public System.Collections.Generic.List<System.Collections.Generic.List<IndexedPathLeaf>> Buckets { get; } = new();
    }

    private sealed class IndexedPathMapModel
    {
        public System.Collections.Generic.Dictionary<string, IndexedPathLeaf> Map { get; } = new();
    }
}
