using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xunit;

namespace InkkSlinger.Tests;

public class BindingParityGap5Tests
{
    [Fact]
    public void PriorityBinding_SelectsFallbackChildThenSwitchesToHigherPriorityChild()
    {
        var highPriority = new NotifyTextViewModel { Value = "A" };
        var lowPriority = new NotifyTextViewModel { Value = "B" };
        var textBlock = new TextBlock();
        var gateConverter = new ThrowWhenDisabledConverter { Enabled = false };

        var priorityBinding = new PriorityBinding
        {
            FallbackValue = "fallback"
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = highPriority,
            Path = nameof(NotifyTextViewModel.Value),
            Converter = gateConverter,
            ValidatesOnExceptions = true
        });
        priorityBinding.Bindings.Add(new Binding
        {
            Source = lowPriority,
            Path = nameof(NotifyTextViewModel.Value)
        });

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, priorityBinding);

        Assert.Equal("B", textBlock.Text);

        gateConverter.Enabled = true;
        highPriority.Value = "A2";

        Assert.Equal("A2", textBlock.Text);
    }

    [Fact]
    public void PriorityBinding_WhenNoChildResolves_UsesFallbackAndSetsValidation()
    {
        var textBlock = new TextBlock();
        var priorityBinding = new PriorityBinding
        {
            FallbackValue = "fallback"
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = new NumberHolder { Number = 42f },
            Path = nameof(NumberHolder.Number)
        });

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty, priorityBinding);

        Assert.Equal("fallback", textBlock.Text);
        Assert.True(Validation.GetHasError(textBlock));
    }

    [Fact]
    public void PriorityBinding_TwoWayWritesBackUsingActiveChildBinding()
    {
        var target = new TextBox();
        var writable = new NotifyTextViewModel { Value = "start" };
        var priorityBinding = new PriorityBinding
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = null,
            Path = nameof(NotifyTextViewModel.Value)
        });
        priorityBinding.Bindings.Add(new Binding
        {
            Source = writable,
            Path = nameof(NotifyTextViewModel.Value)
        });

        BindingOperations.SetBinding(target, TextBox.TextProperty, priorityBinding);
        target.Text = "updated";

        Assert.Equal("updated", writable.Value);
    }

    [Fact]
    public void Binding_UpdateSourceExceptionFilter_MapsExceptionToValidationError()
    {
        var textBox = new TextBox();
        var source = new ThrowingSetterViewModel();
        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = source,
                Path = nameof(ThrowingSetterViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                UpdateSourceExceptionFilter = static (_, _) => "binding-filtered"
            });

        textBox.Text = "next";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Contains(
            Validation.GetErrors(textBox),
            error => string.Equals(error.ErrorContent?.ToString(), "binding-filtered", StringComparison.Ordinal));
    }

    [Fact]
    public void MultiBinding_UpdateSourceExceptionFilter_MapsExceptionToValidationError()
    {
        var textBox = new TextBox();
        var source = new ThrowingSetterViewModel();
        var multiBinding = new MultiBinding
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            Converter = new ThrowingConvertBackMultiConverter(),
            ValidatesOnExceptions = true,
            UpdateSourceExceptionFilter = static (_, _) => "multi-filtered"
        };
        multiBinding.Bindings.Add(new Binding { Source = source, Path = nameof(ThrowingSetterViewModel.Value) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);
        textBox.Text = "x";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Contains(
            Validation.GetErrors(textBox),
            error => string.Equals(error.ErrorContent?.ToString(), "multi-filtered", StringComparison.Ordinal));
    }

    [Fact]
    public void BindingGroup_CommitEdit_CommitsGroupedBindings()
    {
        var vm = new TwoFieldViewModel { First = "1", Second = "2" };
        var root = new Grid
        {
            BindingGroup = new BindingGroup()
        };
        var first = new TextBox();
        var second = new TextBox();
        root.AddChild(first);
        root.AddChild(second);

        BindingOperations.SetBinding(first, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(TwoFieldViewModel.First),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });
        BindingOperations.SetBinding(second, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(TwoFieldViewModel.Second),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });

        first.Text = "10";
        second.Text = "20";

        var committed = root.BindingGroup!.CommitEdit();

        Assert.True(committed);
        Assert.Equal("10", vm.First);
        Assert.Equal("20", vm.Second);
    }

    [Fact]
    public void BindingGroupName_UsesMatchingNamedAncestorGroup()
    {
        var vm = new TwoFieldViewModel { First = "A" };
        var outer = new Grid
        {
            BindingGroup = new BindingGroup { Name = "Outer" }
        };
        var inner = new Grid
        {
            BindingGroup = new BindingGroup { Name = "Inner" }
        };
        var textBox = new TextBox();
        outer.AddChild(inner);
        inner.AddChild(textBox);

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(TwoFieldViewModel.First),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            BindingGroupName = "Outer"
        });

        textBox.Text = "B";
        _ = inner.BindingGroup!.CommitEdit();
        Assert.Equal("A", vm.First);

        _ = outer.BindingGroup!.CommitEdit();
        Assert.Equal("B", vm.First);
    }

    [Fact]
    public void BindingGroup_InheritedGroupRebindsAfterReparent()
    {
        var vm = new TwoFieldViewModel { First = "x" };
        var hostA = new Grid
        {
            BindingGroup = new BindingGroup { Name = "A" }
        };
        var hostB = new Grid
        {
            BindingGroup = new BindingGroup { Name = "B" }
        };
        var textBox = new TextBox();
        hostA.AddChild(textBox);

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(TwoFieldViewModel.First),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });

        textBox.Text = "from-a";
        _ = hostA.BindingGroup!.CommitEdit();
        Assert.Equal("from-a", vm.First);

        _ = hostA.RemoveChild(textBox);
        hostB.AddChild(textBox);
        textBox.Text = "from-b";
        _ = hostA.BindingGroup!.CommitEdit();
        Assert.Equal("from-a", vm.First);

        _ = hostB.BindingGroup!.CommitEdit();
        Assert.Equal("from-b", vm.First);
    }

    private sealed class NotifyTextViewModel : INotifyPropertyChanged
    {
        private string _value = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Value
        {
            get => _value;
            set
            {
                if (string.Equals(_value, value, StringComparison.Ordinal))
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    private sealed class ThrowWhenDisabledConverter : IValueConverter
    {
        public bool Enabled { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (!Enabled)
            {
                throw new InvalidOperationException("disabled");
            }

            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }

    private sealed class ThrowingSetterViewModel
    {
        private string _value = "seed";

        public string Value
        {
            get => _value;
            set => throw new InvalidOperationException("setter failed");
        }
    }

    private sealed class ThrowingConvertBackMultiConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            return values.Length > 0 ? values[0] : string.Empty;
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new InvalidOperationException("convert back failed");
        }
    }

    private sealed class TwoFieldViewModel
    {
        public string First { get; set; } = string.Empty;

        public string Second { get; set; } = string.Empty;
    }

    private sealed class NumberHolder
    {
        public float Number { get; set; }
    }
}
