using System.ComponentModel;
using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class FoundationTests
{
    public FoundationTests()
    {
        Dispatcher.ResetForTests();
        Dispatcher.InitializeForCurrentThread();
        UiApplication.Current.Resources.Clear();
        InputManager.MouseCapturedElement?.ReleaseMouseCapture();
        FocusManager.SetFocusedElement(null);
    }

    [Fact]
    public void DependencyProperty_Precedence_And_Inheritance_Work()
    {
        var root = new Panel();
        var child = new TestControl();
        root.AddChild(child);

        var style = new Style(typeof(TestControl));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0.3f));
        child.Style = style;

        Assert.Equal(0.3f, child.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.Style, child.GetValueSource(UIElement.OpacityProperty));

        child.SetValue(UIElement.OpacityProperty, 0.7f);
        Assert.Equal(0.7f, child.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.Local, child.GetValueSource(UIElement.OpacityProperty));

        root.SetValue(UIElement.IsEnabledProperty, false);
        Assert.False(child.IsEnabled);
        Assert.Equal(DependencyPropertyValueSource.Inherited, child.GetValueSource(UIElement.IsEnabledProperty));
    }

    [Fact]
    public void StyleTriggers_BasedOn_Reacts_To_Base_Conditions()
    {
        var control = new TestControl();

        var baseStyle = new Style(typeof(TestControl));
        var baseTrigger = new Trigger(UIElement.IsEnabledProperty, false);
        baseTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.25f));
        baseStyle.Triggers.Add(baseTrigger);

        var derivedStyle = new Style(typeof(TestControl))
        {
            BasedOn = baseStyle
        };
        derivedStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8f));

        control.Style = derivedStyle;
        Assert.Equal(0.8f, control.Opacity, 3);

        control.IsEnabled = false;
        Assert.Equal(0.25f, control.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.StyleTrigger, control.GetValueSource(UIElement.OpacityProperty));

        control.IsEnabled = true;
        Assert.Equal(0.8f, control.Opacity, 3);
    }

    [Fact]
    public void DataTrigger_Reacts_To_DataContext_PropertyChanges()
    {
        var control = new TestControl();
        var viewModel = new TestViewModel { Title = "Idle" };
        control.DataContext = viewModel;

        var style = new Style(typeof(TestControl));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8f));

        var activeTrigger = new DataTrigger(
            new Binding { Path = nameof(TestViewModel.Title) },
            "Active");
        activeTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.25f));
        style.Triggers.Add(activeTrigger);

        control.Style = style;
        Assert.Equal(0.8f, control.Opacity, 3);

        viewModel.Title = "Active";
        Assert.Equal(0.25f, control.Opacity, 3);

        viewModel.Title = "Idle";
        Assert.Equal(0.8f, control.Opacity, 3);
    }

    [Fact]
    public void DataTrigger_ElementName_Reacts_To_SourceChanges_And_Reparent()
    {
        var firstHost = new Panel();
        var secondHost = new Panel();

        var firstSource = new Label { Name = "ModeLabel", Text = "Build" };
        var secondSource = new Label { Name = "ModeLabel", Text = "Inspect" };

        firstHost.AddChild(firstSource);
        secondHost.AddChild(secondSource);

        var button = new Button();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0.9f));
        style.Triggers.Add(new DataTrigger(
            new Binding
            {
                ElementName = "ModeLabel",
                Path = nameof(Label.Text)
            },
            "Build")
        {
            Setters =
            {
                new Setter(UIElement.OpacityProperty, 0.25f)
            }
        });

        button.Style = style;
        firstHost.AddChild(button);

        Assert.Equal(0.25f, button.Opacity, 3);

        firstSource.Text = "Review";
        Assert.Equal(0.9f, button.Opacity, 3);

        firstHost.RemoveChild(button);
        secondHost.AddChild(button);
        Assert.Equal(0.9f, button.Opacity, 3);

        secondSource.Text = "Build";
        Assert.Equal(0.25f, button.Opacity, 3);
    }

    [Fact]
    public void DataTrigger_RelativeSourceSelf_Reacts_To_TargetPropertyChanges()
    {
        var button = new Button();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BorderBrushProperty, new Color(0x22, 0x33, 0x44)));
        style.Triggers.Add(new DataTrigger(
            new Binding
            {
                RelativeSourceMode = RelativeSourceMode.Self,
                Path = nameof(UIElement.IsEnabled)
            },
            false)
        {
            Setters =
            {
                new Setter(Button.BorderBrushProperty, Color.Red)
            }
        });

        button.Style = style;
        Assert.Equal(new Color(0x22, 0x33, 0x44), button.BorderBrush);

        button.IsEnabled = false;
        Assert.Equal(Color.Red, button.BorderBrush);

        button.IsEnabled = true;
        Assert.Equal(new Color(0x22, 0x33, 0x44), button.BorderBrush);
    }

    [Fact]
    public void DataTrigger_NestedPath_Reacts_To_Leaf_And_IntermediateChanges()
    {
        var button = new Button();
        var vm = new TestNestedSourceViewModel
        {
            Child = new TestNestedLeafViewModel { Value = "Idle" }
        };

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8f));
        style.Triggers.Add(new DataTrigger(
            new Binding { Path = "Child.Value" },
            "Active")
        {
            Setters =
            {
                new Setter(UIElement.OpacityProperty, 0.2f)
            }
        });

        button.DataContext = vm;
        button.Style = style;

        Assert.Equal(0.8f, button.Opacity, 3);

        vm.Child!.Value = "Active";
        Assert.Equal(0.2f, button.Opacity, 3);

        vm.Child = new TestNestedLeafViewModel { Value = "Idle" };
        Assert.Equal(0.8f, button.Opacity, 3);

        vm.Child.Value = "Active";
        Assert.Equal(0.2f, button.Opacity, 3);
    }

    [Fact]
    public void MultiDataTrigger_Reacts_WhenAllConditionsMatch()
    {
        var button = new Button();
        var vm = new MultiConditionViewModel
        {
            IsEnabledFlag = false,
            Mode = "Idle"
        };

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BorderBrushProperty, new Color(0x22, 0x33, 0x44)));

        var multi = new MultiDataTrigger();
        multi.Conditions.Add(new Condition
        {
            Binding = new Binding { Path = nameof(MultiConditionViewModel.IsEnabledFlag) },
            Value = true
        });
        multi.Conditions.Add(new Condition
        {
            Binding = new Binding { Path = nameof(MultiConditionViewModel.Mode) },
            Value = "Build"
        });
        multi.Setters.Add(new Setter(Button.BorderBrushProperty, Color.LimeGreen));
        style.Triggers.Add(multi);

        button.DataContext = vm;
        button.Style = style;

        Assert.Equal(new Color(0x22, 0x33, 0x44), button.BorderBrush);

        vm.IsEnabledFlag = true;
        Assert.Equal(new Color(0x22, 0x33, 0x44), button.BorderBrush);

        vm.Mode = "Build";
        Assert.Equal(Color.LimeGreen, button.BorderBrush);

        vm.Mode = "Idle";
        Assert.Equal(new Color(0x22, 0x33, 0x44), button.BorderBrush);
    }

    [Fact]
    public void Trigger_EnterExitActions_RunOnStateTransitions()
    {
        var button = new Button();
        var style = new Style(typeof(Button));

        var trigger = new Trigger(UIElement.IsEnabledProperty, false);
        var enterAction = new CountingTriggerAction();
        var exitAction = new CountingTriggerAction();
        trigger.EnterActions.Add(enterAction);
        trigger.ExitActions.Add(exitAction);
        style.Triggers.Add(trigger);

        button.Style = style;
        Assert.Equal(0, enterAction.Count);
        Assert.Equal(0, exitAction.Count);

        button.IsEnabled = false;
        Assert.Equal(1, enterAction.Count);
        Assert.Equal(0, exitAction.Count);

        button.IsEnabled = true;
        Assert.Equal(1, enterAction.Count);
        Assert.Equal(1, exitAction.Count);
    }

    [Fact]
    public void Trigger_Actions_ReentrantConditionChange_StabilizesWithExpectedTransitions()
    {
        var button = new Button();
        var style = new Style(typeof(Button));

        var trigger = new Trigger(UIElement.IsEnabledProperty, false);
        var enterCount = new CountingTriggerAction();
        var exitCount = new CountingTriggerAction();
        trigger.EnterActions.Add(enterCount);
        trigger.EnterActions.Add(new SetValueAction(UIElement.IsEnabledProperty, true));
        trigger.ExitActions.Add(exitCount);
        style.Triggers.Add(trigger);

        button.Style = style;
        button.IsEnabled = false;

        Assert.True(button.IsEnabled);
        Assert.Equal(1, enterCount.Count);
        Assert.Equal(1, exitCount.Count);
    }

    [Fact]
    public void XamlLoader_Parses_MultiDataTrigger_With_EnterExitActions()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="PrimaryButton"
                                      Text="Run">
                                <Button.Style>
                                  <Style TargetType="Button">
                                    <Style.Setters>
                                      <Setter Property="BorderBrush" Value="#102030" />
                                    </Style.Setters>
                                    <Style.Triggers>
                                      <MultiDataTrigger>
                                        <MultiDataTrigger.Conditions>
                                          <Condition Binding="{Binding IsPrimaryEnabled}" Value="true" />
                                          <Condition Binding="{Binding Mode}" Value="Build" />
                                        </MultiDataTrigger.Conditions>
                                        <Setter Property="Background" Value="#204060" />
                                        <MultiDataTrigger.EnterActions>
                                          <SetValueAction Property="BorderBrush" Value="#55AA55" />
                                        </MultiDataTrigger.EnterActions>
                                        <MultiDataTrigger.ExitActions>
                                          <SetValueAction Property="BorderBrush" Value="#AA5555" />
                                        </MultiDataTrigger.ExitActions>
                                      </MultiDataTrigger>
                                    </Style.Triggers>
                                  </Style>
                                </Button.Style>
                              </Button>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        var vm = new MultiConditionViewModel { IsPrimaryEnabled = false, Mode = "Idle" };
        button.DataContext = vm;

        Assert.Equal(new Color(0x10, 0x20, 0x30), button.BorderBrush);

        vm.IsPrimaryEnabled = true;
        vm.Mode = "Build";

        Assert.Equal(new Color(0x20, 0x40, 0x60), button.Background);
        Assert.Equal(new Color(0x55, 0xAA, 0x55), button.BorderBrush);

        vm.Mode = "Idle";
        Assert.Equal(new Color(0xAA, 0x55, 0x55), button.BorderBrush);
    }

    [Fact]
    public void XamlLoader_MultiDataTrigger_ConditionValue_IsRequired()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button Text="Run">
                                <Button.Style>
                                  <Style TargetType="Button">
                                    <Style.Triggers>
                                      <MultiDataTrigger>
                                        <Condition Binding="{Binding IsPrimaryEnabled}" />
                                        <Setter Property="Background" Value="#204060" />
                                      </MultiDataTrigger>
                                    </Style.Triggers>
                                  </Style>
                                </Button.Style>
                              </Button>
                            </UserControl>
                            """;

        var view = new UserControl();
        var ex = Assert.Throws<InvalidOperationException>(() => XamlLoader.LoadIntoFromString(view, xaml, null));
        Assert.Contains("Condition", ex.Message);
        Assert.Contains("Value", ex.Message);
    }

    [Fact]
    public void MultiDataTrigger_ConditionBinding_IsRequired()
    {
        var button = new Button();
        var style = new Style(typeof(Button));
        var multi = new MultiDataTrigger();
        multi.Conditions.Add(new Condition { Value = true });
        multi.Setters.Add(new Setter(Button.BackgroundProperty, new Color(0x20, 0x40, 0x60)));
        style.Triggers.Add(multi);

        var ex = Assert.Throws<InvalidOperationException>(() => button.Style = style);
        Assert.Contains("requires a Binding", ex.Message);
    }

    [Fact]
    public void Binding_TwoWay_Propagates_Null()
    {
        var vm = new TestViewModel { Title = "Initial" };
        var control = new TestControl();
        control.DataContext = vm;

        BindingOperations.SetBinding(
            control,
            FrameworkElement.NameProperty,
            new Binding
            {
                Path = nameof(TestViewModel.Title),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        Assert.Equal("Initial", control.Name);

        control.Name = null!;
        Assert.Null(vm.Title);

        vm.Title = "Updated";
        Assert.Equal("Updated", control.Name);
    }

    [Fact]
    public void Binding_Reparent_Rebinds_ElementName_And_FindAncestor()
    {
        var firstRoot = new Panel { Name = "FirstRoot" };
        var secondRoot = new Panel { Name = "SecondRoot" };

        var firstSource = new Label { Name = "SharedSource", Text = "Alpha" };
        var secondSource = new Label { Name = "SharedSource", Text = "Beta" };
        firstRoot.AddChild(firstSource);
        secondRoot.AddChild(secondSource);

        var target = new Button();
        BindingOperations.SetBinding(
            target,
            Button.TextProperty,
            new Binding
            {
                ElementName = "SharedSource",
                Path = nameof(Label.Text)
            });

        BindingOperations.SetBinding(
            target,
            Control.CommandParameterProperty,
            new Binding
            {
                RelativeSourceMode = RelativeSourceMode.FindAncestor,
                RelativeSourceAncestorType = typeof(Panel),
                Path = nameof(FrameworkElement.Name)
            });

        firstRoot.AddChild(target);
        Assert.Equal("Alpha", target.Text);
        Assert.Equal("FirstRoot", target.CommandParameter);

        firstRoot.RemoveChild(target);
        secondRoot.AddChild(target);
        Assert.Equal("Beta", target.Text);
        Assert.Equal("SecondRoot", target.CommandParameter);

        firstSource.Text = "OldTree";
        Assert.Equal("Beta", target.Text);

        secondSource.Text = "NewTree";
        Assert.Equal("NewTree", target.Text);
    }

    [Fact]
    public void Binding_InheritedDataContext_Swap_Detaches_PreviousSources()
    {
        var root = new Panel();
        var label = new Label();
        root.AddChild(label);

        BindingOperations.SetBinding(
            label,
            Label.TextProperty,
            new Binding
            {
                Path = nameof(TestViewModel.Title)
            });

        var first = new TestViewModel { Title = "First" };
        var second = new TestViewModel { Title = "Second" };
        var third = new TestViewModel { Title = "Third" };

        root.DataContext = first;
        Assert.Equal("First", label.Text);

        root.DataContext = second;
        Assert.Equal("Second", label.Text);
        first.Title = "First-Stale";
        Assert.Equal("Second", label.Text);

        root.DataContext = third;
        Assert.Equal("Third", label.Text);
        second.Title = "Second-Stale";
        Assert.Equal("Third", label.Text);

        third.Title = "Third-Updated";
        Assert.Equal("Third-Updated", label.Text);
    }

    [Fact]
    public void Binding_NestedPath_Rebinds_When_Intermediate_Source_Changes()
    {
        var host = new TestNestedSourceViewModel
        {
            Child = new TestNestedLeafViewModel { Value = "Initial" }
        };

        var label = new Label { DataContext = host };
        BindingOperations.SetBinding(
            label,
            Label.TextProperty,
            new Binding
            {
                Path = "Child.Value"
            });

        var oldChild = host.Child!;
        Assert.Equal("Initial", label.Text);

        oldChild.Value = "OldChild-Updated";
        Assert.Equal("OldChild-Updated", label.Text);

        var newChild = new TestNestedLeafViewModel { Value = "NewChild" };
        host.Child = newChild;
        Assert.Equal("NewChild", label.Text);

        oldChild.Value = "Stale";
        Assert.Equal("NewChild", label.Text);

        newChild.Value = "Current";
        Assert.Equal("Current", label.Text);
    }

    [Fact]
    public void Resource_Propagation_Updates_Descendants()
    {
        var root = new Panel();
        var child = new TestControl();
        root.AddChild(child);

        child.SetResourceReference(UIElement.OpacityProperty, "PrimaryOpacity");

        root.Resources["PrimaryOpacity"] = 0.4f;
        Assert.Equal(0.4f, child.Opacity, 3);

        root.Resources["PrimaryOpacity"] = 0.9f;
        Assert.Equal(0.9f, child.Opacity, 3);
    }

    [Fact]
    public void RoutedEvents_PreviewTunnel_Then_Bubble()
    {
        var root = new Panel { Name = "Root" };
        var child = new TestControl { Name = "Child" };
        root.AddChild(child);

        var trace = new System.Collections.Generic.List<string>();

        root.PreviewMouseDown += (_, _) => trace.Add("RootPreview");
        child.PreviewMouseDown += (_, _) => trace.Add("ChildPreview");
        child.MouseDown += (_, _) => trace.Add("ChildBubble");
        root.MouseDown += (_, _) => trace.Add("RootBubble");

        child.FireMouseDown(new Vector2(1, 1), MouseButton.Left, 1, ModifierKeys.None);

        Assert.Equal(new[] { "RootPreview", "ChildPreview", "ChildBubble", "RootBubble" }, trace);
    }

    [Fact]
    public void TemplateBinding_And_NamedPartLookup_Work()
    {
        var control = new TestControl();

        var template = new ControlTemplate(_ =>
        {
            var panel = new Panel { Name = "RootPart" };
            var part = new TestControl { Name = "PART_Content" };
            panel.AddChild(part);
            return panel;
        })
        .BindTemplate("PART_Content", FrameworkElement.WidthProperty, FrameworkElement.WidthProperty);

        control.Template = template;
        control.Width = 120f;
        control.ApplyTemplate();

        Assert.NotNull(control.TemplatePart);
        Assert.Equal(120f, ((FrameworkElement)control.TemplatePart!).Width, 3);

        control.Width = 42f;
        Assert.Equal(42f, ((FrameworkElement)control.TemplatePart!).Width, 3);
    }

    [Fact]
    public void DependencyProperty_Precedence_Local_StyleTrigger_Template_Style()
    {
        var partStyle = new Style(typeof(Button));
        partStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4f));

        var disabledTrigger = new Trigger(UIElement.IsEnabledProperty, false);
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.2f));
        partStyle.Triggers.Add(disabledTrigger);

        var control = new TestControl
        {
            Width = 0.7f
        };

        control.Template = new ControlTemplate(_ =>
        {
            var part = new Button
            {
                Name = "PART_Content",
                Style = partStyle
            };
            return part;
        })
        .BindTemplate("PART_Content", UIElement.OpacityProperty, FrameworkElement.WidthProperty);

        control.ApplyTemplate();
        var part = Assert.IsType<Button>(control.TemplatePart);

        Assert.Equal(0.7f, part.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.Template, part.GetValueSource(UIElement.OpacityProperty));

        part.IsEnabled = false;
        Assert.Equal(0.2f, part.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.StyleTrigger, part.GetValueSource(UIElement.OpacityProperty));

        part.Opacity = 0.9f;
        Assert.Equal(0.9f, part.Opacity, 3);
        Assert.Equal(DependencyPropertyValueSource.Local, part.GetValueSource(UIElement.OpacityProperty));
    }

    [Fact]
    public void Dispatcher_Rejects_CrossThread_UI_Mutations()
    {
        Dispatcher.InitializeForCurrentThread();
        var element = new TestControl();

        Exception? threadException = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                element.Opacity = 0.1f;
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.Start();
        thread.Join();

        Assert.NotNull(threadException);
        Assert.IsType<InvalidOperationException>(threadException);
    }

    [Fact]
    public void Button_Click_RaisesEvent_AndExecutesCommand()
    {
        var button = new TestButton();
        var clickCount = 0;
        object? observedParameter = null;

        button.CommandParameter = "ink";
        button.Command = new RelayCommand(parameter =>
        {
            observedParameter = parameter;
            clickCount++;
        });
        button.Measure(new Vector2(100f, 40f));
        button.Arrange(new LayoutRect(0f, 0f, 100f, 40f));

        var routedClickCount = 0;
        button.Click += (_, _) => routedClickCount++;

        button.FireMouseDownAndUp(new Vector2(4f, 4f));

        Assert.Equal(1, clickCount);
        Assert.Equal("ink", observedParameter);
        Assert.Equal(1, routedClickCount);
    }

    [Fact]
    public void Border_AccountsFor_BorderThickness_AndPadding()
    {
        var border = new Border
        {
            BorderThickness = new Thickness(2f),
            Padding = new Thickness(3f)
        };

        var child = new FixedSizeElement(50f, 20f);
        border.Child = child;

        border.Measure(new Vector2(200f, 200f));
        border.Arrange(new LayoutRect(0f, 0f, border.DesiredSize.X, border.DesiredSize.Y));

        Assert.Equal(60f, border.DesiredSize.X, 3);
        Assert.Equal(30f, border.DesiredSize.Y, 3);
        Assert.Equal(5f, child.LayoutSlot.X, 3);
        Assert.Equal(5f, child.LayoutSlot.Y, 3);
        Assert.Equal(50f, child.LayoutSlot.Width, 3);
        Assert.Equal(20f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void ArrangedChild_Uses_AlignedParentOrigin()
    {
        var border = new Border
        {
            Width = 60f,
            Height = 30f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(2f),
            Padding = new Thickness(3f)
        };

        var child = new FixedSizeElement(10f, 8f);
        border.Child = child;

        border.Measure(new Vector2(200f, 200f));
        border.Arrange(new LayoutRect(0f, 0f, 200f, 200f));

        Assert.Equal(70f, border.LayoutSlot.X, 3);
        Assert.Equal(85f, border.LayoutSlot.Y, 3);
        Assert.Equal(75f, child.LayoutSlot.X, 3);
        Assert.Equal(90f, child.LayoutSlot.Y, 3);
    }

    [Fact]
    public void StackPanel_Vertical_StacksChildren_UsingDesiredHeight()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var first = new FixedSizeElement(100f, 20f);
        var second = new FixedSizeElement(80f, 30f);

        stack.AddChild(first);
        stack.AddChild(second);

        stack.Measure(new Vector2(300f, 300f));
        stack.Arrange(new LayoutRect(10f, 20f, 120f, 100f));

        Assert.Equal(100f, stack.DesiredSize.X, 3);
        Assert.Equal(50f, stack.DesiredSize.Y, 3);
        Assert.Equal(10f, first.LayoutSlot.X, 3);
        Assert.Equal(20f, first.LayoutSlot.Y, 3);
        Assert.Equal(120f, first.LayoutSlot.Width, 3);
        Assert.Equal(20f, first.LayoutSlot.Height, 3);
        Assert.Equal(10f, second.LayoutSlot.X, 3);
        Assert.Equal(40f, second.LayoutSlot.Y, 3);
        Assert.Equal(120f, second.LayoutSlot.Width, 3);
        Assert.Equal(30f, second.LayoutSlot.Height, 3);
    }

    [Fact]
    public void Grid_PlacesChild_InConfiguredCell()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50f) });

        var child = new FixedSizeElement(10f, 10f);
        Grid.SetColumn(child, 1);
        Grid.SetRow(child, 1);
        grid.AddChild(child);

        grid.Measure(new Vector2(200f, 100f));
        grid.Arrange(new LayoutRect(0f, 0f, 200f, 100f));

        Assert.Equal(100f, child.LayoutSlot.X, 3);
        Assert.Equal(50f, child.LayoutSlot.Y, 3);
        Assert.Equal(100f, child.LayoutSlot.Width, 3);
        Assert.Equal(50f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void Grid_SpansAcrossColumns_AndRows()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60f) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30f) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40f) });

        var child = new FixedSizeElement(20f, 20f);
        Grid.SetColumn(child, 1);
        Grid.SetColumnSpan(child, 2);
        Grid.SetRow(child, 0);
        Grid.SetRowSpan(child, 2);
        grid.AddChild(child);

        grid.Measure(new Vector2(150f, 70f));
        grid.Arrange(new LayoutRect(5f, 7f, 150f, 70f));

        Assert.Equal(45f, child.LayoutSlot.X, 3);
        Assert.Equal(7f, child.LayoutSlot.Y, 3);
        Assert.Equal(110f, child.LayoutSlot.Width, 3);
        Assert.Equal(70f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void Grid_AutoAndStar_ShareAvailableSpace()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40f) });

        var autoChild = new FixedSizeElement(40f, 20f);
        Grid.SetColumn(autoChild, 0);
        grid.AddChild(autoChild);

        var starChild = new FixedSizeElement(10f, 20f);
        Grid.SetColumn(starChild, 1);
        grid.AddChild(starChild);

        grid.Measure(new Vector2(300f, 40f));
        grid.Arrange(new LayoutRect(0f, 0f, 300f, 40f));

        Assert.Equal(40f, autoChild.LayoutSlot.Width, 3);
        Assert.Equal(260f, starChild.LayoutSlot.Width, 3);
        Assert.Equal(40f, starChild.LayoutSlot.X, 3);
    }

    [Fact]
    public void UserControl_InsetsContent_WithBorderAndPadding()
    {
        var userControl = new UserControl
        {
            BorderThickness = new Thickness(2f),
            Padding = new Thickness(3f)
        };

        var child = new FixedSizeElement(40f, 20f);
        userControl.Content = child;

        userControl.Measure(new Vector2(200f, 200f));
        userControl.Arrange(new LayoutRect(10f, 15f, userControl.DesiredSize.X, userControl.DesiredSize.Y));

        Assert.Equal(50f, userControl.DesiredSize.X, 3);
        Assert.Equal(30f, userControl.DesiredSize.Y, 3);
        Assert.Equal(15f, child.LayoutSlot.X, 3);
        Assert.Equal(20f, child.LayoutSlot.Y, 3);
        Assert.Equal(40f, child.LayoutSlot.Width, 3);
        Assert.Equal(20f, child.LayoutSlot.Height, 3);
    }

    [Fact]
    public void UserControl_Rejects_NonVisualContent()
    {
        var userControl = new UserControl();

        var ex = Assert.Throws<InvalidOperationException>(() => userControl.SetValue(ContentControl.ContentProperty, "not visual"));
        Assert.Contains("must be a UIElement", ex.Message);
    }

    [Fact]
    public void UserControl_Rejects_CustomTemplate()
    {
        var userControl = new UserControl();
        var template = new ControlTemplate(_ => new Panel());

        var ex = Assert.Throws<NotSupportedException>(() => userControl.Template = template);
        Assert.Contains("does not support custom ControlTemplate", ex.Message);
    }

    [Fact]
    public void Layout_Invalidation_Propagates_From_Child_Visibility_Changes()
    {
        var panel = new Panel();
        var first = new FixedSizeElement(100f, 50f);
        var second = new FixedSizeElement(60f, 120f);

        panel.AddChild(first);
        panel.AddChild(second);

        panel.Measure(new Vector2(500f, 500f));
        Assert.Equal(100f, panel.DesiredSize.X, 3);
        Assert.Equal(120f, panel.DesiredSize.Y, 3);

        second.IsVisible = false;
        panel.Measure(new Vector2(500f, 500f));

        Assert.Equal(100f, panel.DesiredSize.X, 3);
        Assert.Equal(50f, panel.DesiredSize.Y, 3);
    }

    [Fact]
    public void Grid_Recomputes_ColumnWidths_When_Viewport_Resizes()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var first = new FixedSizeElement(10f, 10f);
        var second = new FixedSizeElement(10f, 10f);
        Grid.SetColumn(second, 1);

        grid.AddChild(first);
        grid.AddChild(second);

        grid.Measure(new Vector2(300f, 100f));
        grid.Arrange(new LayoutRect(0f, 0f, 300f, 100f));
        Assert.Equal(150f, first.LayoutSlot.Width, 3);
        Assert.Equal(150f, second.LayoutSlot.Width, 3);
        Assert.Equal(150f, second.LayoutSlot.X, 3);

        grid.Measure(new Vector2(451f, 100f));
        grid.Arrange(new LayoutRect(0f, 0f, 451f, 100f));
        Assert.Equal(225.5f, first.LayoutSlot.Width, 3);
        Assert.Equal(225.5f, second.LayoutSlot.Width, 3);
        Assert.Equal(225.5f, second.LayoutSlot.X, 3);
    }

    [Fact]
    public void Popup_Coerces_Placement_When_Host_Resizes()
    {
        var host = new Panel();
        host.Measure(new Vector2(800f, 600f));
        host.Arrange(new LayoutRect(0f, 0f, 800f, 600f));

        var popup = new Popup
        {
            Width = 260f,
            Height = 160f,
            Left = 700f,
            Top = 500f
        };

        popup.Show(host);
        host.UpdateLayout();

        Assert.Equal(540f, popup.Left, 3);
        Assert.Equal(440f, popup.Top, 3);

        host.Arrange(new LayoutRect(0f, 0f, 500f, 350f));
        host.UpdateLayout();

        Assert.Equal(240f, popup.Left, 3);
        Assert.Equal(190f, popup.Top, 3);
    }

    [Fact]
    public void FrameworkElement_Arrange_Skips_When_Rect_Unchanged()
    {
        var element = new FixedSizeElement(80f, 20f);
        var layoutUpdatedCount = 0;
        element.LayoutUpdated += (_, _) => layoutUpdatedCount++;

        element.Measure(new Vector2(200f, 100f));
        element.Arrange(new LayoutRect(0f, 0f, 200f, 100f));
        Assert.Equal(1, layoutUpdatedCount);

        element.Arrange(new LayoutRect(0f, 0f, 200f, 100f));
        Assert.Equal(1, layoutUpdatedCount);

        element.Arrange(new LayoutRect(0f, 0f, 300f, 100f));
        Assert.Equal(2, layoutUpdatedCount);
    }

    [Fact]
    public void Panel_ZIndex_Change_Invalidates_Arrange()
    {
        var panel = new Panel();
        var first = new CountingArrangeElement(20f, 20f);
        var second = new CountingArrangeElement(20f, 20f);

        panel.AddChild(first);
        panel.AddChild(second);

        var panelLayoutUpdatedCount = 0;
        panel.LayoutUpdated += (_, _) => panelLayoutUpdatedCount++;

        panel.Measure(new Vector2(100f, 100f));
        panel.Arrange(new LayoutRect(0f, 0f, 100f, 100f));
        Assert.Equal(1, panelLayoutUpdatedCount);

        Panel.SetZIndex(second, 10);
        panel.Arrange(new LayoutRect(0f, 0f, 100f, 100f));

        Assert.Equal(2, panelLayoutUpdatedCount);
    }

    [Fact]
    public void XamlLoader_AssignsNames_And_WiresEvents()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="PrimaryButton"
                                      Text="Run"
                                      Loaded="OnPrimaryLoaded"/>
                            </UserControl>
                            """;

        var codeBehind = new TestViewCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.PrimaryButton);
        Assert.NotNull(view.Content);

        var button = Assert.IsType<Button>(codeBehind.PrimaryButton);
        button.RaiseLoaded();
        Assert.Equal(1, codeBehind.LoadedCount);
    }

    [Fact]
    public void XamlLoader_Diagnostics_Include_Attribute_Element_And_LineInfo()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button Bogus="x" />
                            </UserControl>
                            """;

        var view = new UserControl();
        var ex = Assert.Throws<InvalidOperationException>(() => XamlLoader.LoadIntoFromString(view, xaml, null));

        Assert.Contains("Failed to apply attribute 'Bogus' on 'Button'", ex.Message);
        Assert.Contains("Line", ex.Message);
        Assert.Contains("Position", ex.Message);
    }

    [Fact]
    public void XamlLoader_Diagnostics_Include_StaticResource_Resolution_Error_Context()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button Style="{StaticResource MissingStyle}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        var ex = Assert.Throws<InvalidOperationException>(() => XamlLoader.LoadIntoFromString(view, xaml, null));

        Assert.Contains("Failed to apply attribute 'Style' on 'Button'", ex.Message);
        Assert.Contains("StaticResource key 'MissingStyle' was not found.", ex.Message);
        Assert.Contains("Line", ex.Message);
    }

    [Fact]
    public void XamlLoader_Parses_TextBlock_TextWrapping()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <TextBlock Text="Wrapped text" TextWrapping="Wrap" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var textBlock = Assert.IsType<TextBlock>(view.Content);
        Assert.Equal(TextWrapping.Wrap, textBlock.TextWrapping);
    }

    [Fact]
    public void XamlLoader_Parses_Button_TextWrapping()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button Text="Wrapped button text" TextWrapping="Wrap" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(TextWrapping.Wrap, button.TextWrapping);
    }

    [Fact]
    public void XamlLoader_Parses_Label_TextWrapping()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Label Text="Wrapped label text" TextWrapping="Wrap" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var label = Assert.IsType<Label>(view.Content);
        Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
    }

    [Fact]
    public void XamlLoader_Ignores_XsiSchemaLocation()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                                         xsi:schemaLocation="urn:inkkslinger-ui Schemas/InkkSlinger.UI.xsd">
                              <Label x:Name="StatusLabel" Text="Hello"/>
                            </UserControl>
                            """;

        var codeBehind = new TestViewCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.StatusLabel);
        Assert.Equal("Hello", codeBehind.StatusLabel!.Text);
    }

    [Fact]
    public void XamlLoader_Parses_Grid_Definitions_From_PropertyElements()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid x:Name="RootGrid">
                                <Grid.ColumnDefinitions>
                                  <ColumnDefinition Width="2*" />
                                  <ColumnDefinition Width="120" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                  <RowDefinition Height="Auto" />
                                  <RowDefinition Height="*" />
                                </Grid.RowDefinitions>
                                <Label x:Name="StatusLabel"
                                       Text="Loaded"
                                       Grid.Row="1"
                                       Grid.Column="1" />
                              </Grid>
                            </UserControl>
                            """;

        var codeBehind = new TestGridViewCodeBehind();
        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, codeBehind);

        Assert.NotNull(codeBehind.RootGrid);
        Assert.Equal(2, codeBehind.RootGrid!.ColumnDefinitions.Count);
        Assert.Equal(2, codeBehind.RootGrid.RowDefinitions.Count);
        Assert.Equal(GridUnitType.Star, codeBehind.RootGrid.ColumnDefinitions[0].Width.GridUnitType);
        Assert.Equal(2f, codeBehind.RootGrid.ColumnDefinitions[0].Width.Value, 3);
        Assert.Equal(GridUnitType.Pixel, codeBehind.RootGrid.ColumnDefinitions[1].Width.GridUnitType);
        Assert.Equal(120f, codeBehind.RootGrid.ColumnDefinitions[1].Width.Value, 3);
        Assert.Equal(GridUnitType.Auto, codeBehind.RootGrid.RowDefinitions[0].Height.GridUnitType);
        Assert.Equal(GridUnitType.Star, codeBehind.RootGrid.RowDefinitions[1].Height.GridUnitType);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_For_OneWay()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Label x:Name="StatusLabel"
                                     Text="{Binding Title}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var label = Assert.IsType<Label>(view.Content);
        var vm = new TestViewModel { Title = "Initial" };
        view.DataContext = vm;

        Assert.Equal("Initial", label.Text);

        vm.Title = "Updated";
        Assert.Equal("Updated", label.Text);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_For_TwoWay()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Label x:Name="StatusLabel"
                                     Text="{Binding Title, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var label = Assert.IsType<Label>(view.Content);
        var vm = new TestViewModel { Title = "Initial" };
        view.DataContext = vm;

        label.Text = "FromView";
        Assert.Equal("FromView", vm.Title);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_With_Source_StaticResource()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Label Text="{Binding Source={StaticResource Vm}, Path=Title}" />
                            </UserControl>
                            """;

        var vm = new TestViewModel { Title = "FromSource" };
        UiApplication.Current.Resources["Vm"] = vm;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var label = Assert.IsType<Label>(view.Content);
        Assert.Equal("FromSource", label.Text);

        vm.Title = "Updated";
        Assert.Equal("Updated", label.Text);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_With_ElementName()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid>
                                <Label x:Name="SourceLabel" Text="Alpha" />
                                <Label x:Name="MirrorLabel"
                                       Grid.Row="1"
                                       Text="{Binding ElementName=SourceLabel, Path=Text}" />
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var grid = Assert.IsType<Grid>(view.Content);
        var source = Assert.IsType<Label>(grid.Children[0]);
        var mirror = Assert.IsType<Label>(grid.Children[1]);

        Assert.Equal("Alpha", mirror.Text);
        source.Text = "Beta";
        Assert.Equal("Beta", mirror.Text);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_With_RelativeSource_Self()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="SelfButton"
                                      Text="Echo"
                                      CommandParameter="{Binding RelativeSource={RelativeSource Self}, Path=Text}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal("Echo", button.CommandParameter);

        button.Text = "Updated";
        Assert.Equal("Updated", button.CommandParameter);
    }

    [Fact]
    public void XamlLoader_Parses_BindingMarkup_With_RelativeSource_FindAncestor()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid x:Name="RootGrid">
                                <StackPanel>
                                  <Label x:Name="ChildLabel"
                                         Text="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=Grid}, Path=Name}" />
                                </StackPanel>
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var rootGrid = Assert.IsType<Grid>(view.Content);
        var stack = Assert.IsType<StackPanel>(rootGrid.Children[0]);
        var label = Assert.IsType<Label>(stack.Children[0]);

        Assert.Equal("RootGrid", label.Text);
        rootGrid.Name = "UpdatedGrid";
        Assert.Equal("UpdatedGrid", label.Text);
    }

    [Fact]
    public void XamlLoader_Parses_BindingElement_With_Source_StaticResource()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="StateDrivenStyle" TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="BorderBrush" Value="#B9B9B9" />
                                  </Style.Setters>
                                  <Style.Triggers>
                                    <DataTrigger Value="false">
                                      <DataTrigger.Binding>
                                        <Binding Source="{StaticResource Vm}" Path="IsPrimaryEnabled" />
                                      </DataTrigger.Binding>
                                      <Setter Property="BorderBrush" Value="#FF0000" />
                                    </DataTrigger>
                                  </Style.Triggers>
                                </Style>
                              </UserControl.Resources>
                              <Button Text="Run" Style="{StaticResource StateDrivenStyle}" />
                            </UserControl>
                            """;

        var vm = new TestButtonStateViewModel { IsPrimaryEnabled = true };
        UiApplication.Current.Resources["Vm"] = vm;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(new Color(185, 185, 185), button.BorderBrush);

        vm.IsPrimaryEnabled = false;
        Assert.Equal(new Color(255, 0, 0), button.BorderBrush);
    }

    [Fact]
    public void XamlLoader_ValueTypeBinding_DoesNotFail_When_DataContext_IsInitiallyNull()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="PrimaryButton"
                                      IsEnabled="{Binding IsPrimaryEnabled}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.True(button.IsEnabled);

        var vm = new TestButtonStateViewModel { IsPrimaryEnabled = false };
        view.DataContext = vm;
        Assert.False(button.IsEnabled);
    }

    [Fact]
    public void XamlLoader_Parses_InlineStyle_With_DataTrigger()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Button x:Name="PrimaryButton"
                                      Text="Styled">
                                <Button.Style>
                                  <Style TargetType="Button">
                                    <Style.Setters>
                                      <Setter Property="Background" Value="#101820" />
                                    </Style.Setters>
                                    <Style.Triggers>
                                      <DataTrigger Binding="{Binding IsPrimaryEnabled}" Value="false">
                                        <Setter Property="BorderBrush" Value="#FF0000" />
                                      </DataTrigger>
                                    </Style.Triggers>
                                  </Style>
                                </Button.Style>
                              </Button>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(new Color(185, 185, 185), button.BorderBrush);

        view.DataContext = new TestButtonStateViewModel { IsPrimaryEnabled = false };
        Assert.Equal(new Color(255, 0, 0), button.BorderBrush);
    }

    [Fact]
    public void XamlLoader_Parses_Resources_And_StaticResource_StyleReference()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="AccentButtonStyle" TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="Background" Value="#123456" />
                                  </Style.Setters>
                                </Style>
                              </UserControl.Resources>
                              <Button Text="Run" Style="{StaticResource AccentButtonStyle}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(new Color(0x12, 0x34, 0x56), button.Background);
    }

    [Fact]
    public void XamlLoader_Resolves_StaticResource_For_Deep_Child_Before_Tree_Attach()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="PrimaryButtonStyle" TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="Background" Value="#123456" />
                                  </Style.Setters>
                                </Style>
                              </UserControl.Resources>
                              <Grid>
                                <Button Text="Run" Style="{StaticResource PrimaryButtonStyle}" />
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var grid = Assert.IsType<Grid>(view.Content);
        var button = Assert.IsType<Button>(Assert.Single(grid.Children));
        Assert.Equal(new Color(0x12, 0x34, 0x56), button.Background);
    }

    [Fact]
    public void XamlLoader_Resolves_StaticResource_From_Unattached_Ancestor_During_Construction()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Grid>
                                <Grid.Resources>
                                  <Style x:Key="NestedButtonStyle" TargetType="Button">
                                    <Style.Setters>
                                      <Setter Property="Background" Value="#2468AC" />
                                    </Style.Setters>
                                  </Style>
                                </Grid.Resources>
                                <StackPanel>
                                  <Button Text="Run" Style="{StaticResource NestedButtonStyle}" />
                                </StackPanel>
                              </Grid>
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var grid = Assert.IsType<Grid>(view.Content);
        var stack = Assert.IsType<StackPanel>(Assert.Single(grid.Children));
        var button = Assert.IsType<Button>(Assert.Single(stack.Children));
        Assert.Equal(new Color(0x24, 0x68, 0xAC), button.Background);
    }

    [Fact]
    public void XamlLoader_Resolves_StaticResource_Inside_Setter()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="PayloadStyle" TargetType="Button" />
                                <Style x:Key="CarrierStyle" TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="CommandParameter" Value="{StaticResource PayloadStyle}" />
                                  </Style.Setters>
                                </Style>
                              </UserControl.Resources>
                              <Button Text="Run" Style="{StaticResource CarrierStyle}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        var payload = Assert.IsType<Style>(button.CommandParameter);
        Assert.Equal(typeof(Button), payload.TargetType);
    }

    [Fact]
    public void XamlLoader_Parses_Style_BasedOn_From_StaticResource()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style x:Key="BaseButtonStyle" TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="Background" Value="#102030" />
                                  </Style.Setters>
                                </Style>
                                <Style x:Key="DerivedButtonStyle"
                                       TargetType="Button"
                                       BasedOn="{StaticResource BaseButtonStyle}">
                                  <Style.Setters>
                                    <Setter Property="BorderBrush" Value="#405060" />
                                  </Style.Setters>
                                </Style>
                              </UserControl.Resources>
                              <Button Text="Run" Style="{StaticResource DerivedButtonStyle}" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(new Color(0x10, 0x20, 0x30), button.Background);
        Assert.Equal(new Color(0x40, 0x50, 0x60), button.BorderBrush);
    }

    [Fact]
    public void XamlLoader_Applies_Implicit_Style_By_TargetType_From_Resources()
    {
        const string xaml = """
                            <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <UserControl.Resources>
                                <Style TargetType="Button">
                                  <Style.Setters>
                                    <Setter Property="Background" Value="#224466" />
                                  </Style.Setters>
                                </Style>
                              </UserControl.Resources>
                              <Button Text="Implicit" />
                            </UserControl>
                            """;

        var view = new UserControl();
        XamlLoader.LoadIntoFromString(view, xaml, null);

        var button = Assert.IsType<Button>(view.Content);
        Assert.Equal(new Color(0x22, 0x44, 0x66), button.Background);
    }

    [Fact]
    public void ResourceResolver_Prefers_Local_Ancestor_Then_Application()
    {
        UiApplication.Current.Resources["Accent"] = "App";

        var root = new Panel();
        root.Resources["Accent"] = "Root";

        var parent = new Panel();
        parent.Resources["Accent"] = "Parent";
        root.AddChild(parent);

        var leaf = new Label();
        parent.AddChild(leaf);

        Assert.True(leaf.TryFindResource("Accent", out var value));
        Assert.Equal("Parent", value);

        leaf.Resources["Accent"] = "Leaf";
        Assert.True(leaf.TryFindResource("Accent", out value));
        Assert.Equal("Leaf", value);

        leaf.Resources.Remove("Accent");
        parent.Resources.Remove("Accent");
        Assert.True(leaf.TryFindResource("Accent", out value));
        Assert.Equal("Root", value);

        root.Resources.Remove("Accent");
        Assert.True(leaf.TryFindResource("Accent", out value));
        Assert.Equal("App", value);
    }

    [Fact]
    public void ImplicitStyle_Replaces_On_Reparent_And_ResourceChanges()
    {
        var firstHost = new Panel();
        var secondHost = new Panel();

        var firstStyle = BuildButtonStyle(new Color(0x11, 0x22, 0x33));
        var secondStyle = BuildButtonStyle(new Color(0x44, 0x55, 0x66));
        var updatedSecondStyle = BuildButtonStyle(new Color(0x77, 0x88, 0x99));

        firstHost.Resources[typeof(Button)] = firstStyle;
        secondHost.Resources[typeof(Button)] = secondStyle;

        var button = new Button();
        firstHost.AddChild(button);

        Assert.Same(firstStyle, button.Style);
        Assert.Equal(new Color(0x11, 0x22, 0x33), button.Background);

        firstHost.RemoveChild(button);
        secondHost.AddChild(button);

        Assert.Same(secondStyle, button.Style);
        Assert.Equal(new Color(0x44, 0x55, 0x66), button.Background);

        secondHost.Resources[typeof(Button)] = updatedSecondStyle;
        Assert.Same(updatedSecondStyle, button.Style);
        Assert.Equal(new Color(0x77, 0x88, 0x99), button.Background);
    }

    [Fact]
    public void Popup_ShowAndClose_AttachesAndDetaches_FromHost()
    {
        var host = new Panel();
        var window = new Popup
        {
            Width = 220f,
            Height = 120f
        };

        window.Show(host);
        Assert.True(window.IsOpen);
        Assert.Contains(window, host.Children);

        window.Close();
        Assert.False(window.IsOpen);
        Assert.DoesNotContain(window, host.Children);
    }

    [Fact]
    public void Popup_DragMove_Updates_LeftAndTop()
    {
        var host = new Panel
        {
            Width = 800f,
            Height = 600f
        };

        var window = new TestPopup
        {
            Width = 260f,
            Height = 160f,
            Left = 100f,
            Top = 90f
        };

        window.Show(host);
        host.Measure(new Vector2(800f, 600f));
        host.Arrange(new LayoutRect(0f, 0f, 800f, 600f));

        var startLeft = window.Left;
        var startTop = window.Top;
        var startPoint = new Vector2(window.LayoutSlot.X + 20f, window.LayoutSlot.Y + 10f);
        var movePoint = new Vector2(startPoint.X + 70f, startPoint.Y + 40f);

        window.FireLeftDown(startPoint);
        window.FireMove(movePoint);
        window.FireLeftUp(movePoint);

        Assert.Equal(startLeft + 70f, window.Left, 2);
        Assert.Equal(startTop + 40f, window.Top, 2);
    }

    [Fact]
    public void MouseCapture_IsReleased_When_CapturedElement_BecomesDisabled()
    {
        var host = new Panel
        {
            Width = 800f,
            Height = 600f
        };

        var window = new TestPopup
        {
            Width = 260f,
            Height = 160f,
            Left = 100f,
            Top = 90f
        };

        window.Show(host);
        host.Measure(new Vector2(800f, 600f));
        host.Arrange(new LayoutRect(0f, 0f, 800f, 600f));

        var startPoint = new Vector2(window.LayoutSlot.X + 20f, window.LayoutSlot.Y + 10f);
        window.FireLeftDown(startPoint);
        Assert.Same(window, InputManager.MouseCapturedElement);

        window.IsEnabled = false;
        Assert.Null(InputManager.MouseCapturedElement);
    }

    [Fact]
    public void Popup_Close_Restores_PreviousFocus()
    {
        var host = new Panel();
        var opener = new Button { Focusable = true };
        host.AddChild(opener);
        Assert.True(opener.Focus());
        Assert.Same(opener, FocusManager.FocusedElement);

        var popup = new Popup
        {
            Width = 220f,
            Height = 120f
        };

        popup.Show(host);
        Assert.Same(popup, FocusManager.FocusedElement);

        popup.Close();
        Assert.Same(opener, FocusManager.FocusedElement);
    }

    [Fact]
    public void FocusManager_TabNavigation_Traces_NestedFocusableElements()
    {
        var root = new Panel();
        var left = new Button { Name = "Left" };
        var container = new StackPanel();
        var middle = new Button { Name = "Middle" };
        var right = new Button { Name = "Right" };

        root.AddChild(left);
        root.AddChild(container);
        container.AddChild(middle);
        container.AddChild(right);

        Assert.True(FocusManager.SetFocusedElement(left));
        Assert.Same(left, FocusManager.FocusedElement);

        Assert.True(FocusManager.MoveFocus(root));
        Assert.Same(middle, FocusManager.FocusedElement);

        Assert.True(FocusManager.MoveFocus(root));
        Assert.Same(right, FocusManager.FocusedElement);

        Assert.True(FocusManager.MoveFocus(root, backwards: true));
        Assert.Same(middle, FocusManager.FocusedElement);
    }

    private sealed class TestControl : Control
    {
        public UIElement? TemplatePart { get; private set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            TemplatePart = GetTemplateChild("PART_Content");
        }

        public void FireMouseDown(Vector2 position, MouseButton button, int clickCount, ModifierKeys modifiers)
        {
            RaisePreviewMouseDown(position, button, clickCount, modifiers);
            RaiseMouseDown(position, button, clickCount, modifiers);
        }
    }

    private sealed class TestButton : Button
    {
        public void FireMouseDownAndUp(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaisePreviewMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public FixedSizeElement(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }
    }

    private sealed class CountingArrangeElement : FrameworkElement
    {
        private readonly Vector2 _size;

        public CountingArrangeElement(float width, float height)
        {
            _size = new Vector2(width, height);
        }

        public int ArrangeOverrideCount { get; private set; }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            return _size;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            ArrangeOverrideCount++;
            return finalSize;
        }
    }

    private sealed class TestPopup : Popup
    {
        public void FireLeftDown(Vector2 position)
        {
            RaisePreviewMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseDown(position, MouseButton.Left, 1, ModifierKeys.None);
        }

        public void FireMove(Vector2 position)
        {
            RaisePreviewMouseMove(position, ModifierKeys.None);
            RaiseMouseMove(position, ModifierKeys.None);
        }

        public void FireLeftUp(Vector2 position)
        {
            RaisePreviewMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
            RaiseMouseUp(position, MouseButton.Left, 1, ModifierKeys.None);
        }
    }

    private sealed class TestViewCodeBehind
    {
        public Button? PrimaryButton { get; set; }
        public Label? StatusLabel { get; set; }
        public int LoadedCount { get; private set; }

        public void OnPrimaryLoaded(object? sender, EventArgs e)
        {
            LoadedCount++;
        }
    }

    private sealed class TestGridViewCodeBehind
    {
        public Grid? RootGrid { get; set; }
    }

    private sealed class TestViewModel : INotifyPropertyChanged
    {
        private string? _title;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Title
        {
            get => _title;
            set
            {
                if (_title == value)
                {
                    return;
                }

                _title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }
    }

    private sealed class TestButtonStateViewModel : INotifyPropertyChanged
    {
        private bool _isPrimaryEnabled;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsPrimaryEnabled
        {
            get => _isPrimaryEnabled;
            set
            {
                if (_isPrimaryEnabled == value)
                {
                    return;
                }

                _isPrimaryEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPrimaryEnabled)));
            }
        }
    }

    private static Style BuildButtonStyle(Color background)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, background));
        return style;
    }

    private sealed class TestNestedSourceViewModel : INotifyPropertyChanged
    {
        private TestNestedLeafViewModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TestNestedLeafViewModel? Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                {
                    return;
                }

                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class TestNestedLeafViewModel : INotifyPropertyChanged
    {
        private string _value = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class MultiConditionViewModel : INotifyPropertyChanged
    {
        private bool _isEnabledFlag;
        private bool _isPrimaryEnabled;
        private string _mode = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsEnabledFlag
        {
            get => _isEnabledFlag;
            set
            {
                if (_isEnabledFlag == value)
                {
                    return;
                }

                _isEnabledFlag = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabledFlag)));
            }
        }

        public bool IsPrimaryEnabled
        {
            get => _isPrimaryEnabled;
            set
            {
                if (_isPrimaryEnabled == value)
                {
                    return;
                }

                _isPrimaryEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPrimaryEnabled)));
            }
        }

        public string Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mode)));
            }
        }
    }

    private sealed class CountingTriggerAction : TriggerAction
    {
        public int Count { get; private set; }

        public override void Invoke(DependencyObject target)
        {
            Count++;
        }
    }
}
