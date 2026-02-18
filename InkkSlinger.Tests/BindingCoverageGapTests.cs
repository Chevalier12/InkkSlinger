using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace InkkSlinger.Tests;

public class BindingCoverageGapTests
{
    [Fact]
    public void BindingGroup_CommitEdit_WhenMemberFails_RollsBackEarlierWrites()
    {
        var vm = new AtomicGroupViewModel
        {
            First = "one",
            Second = "two"
        };
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
            Path = nameof(AtomicGroupViewModel.First),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });
        BindingOperations.SetBinding(second, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(AtomicGroupViewModel.Second),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            ValidatesOnExceptions = true
        });

        first.Text = "changed-first";
        second.Text = "boom";

        var committed = root.BindingGroup!.CommitEdit();

        Assert.False(committed);
        Assert.Equal("one", vm.First);
        Assert.Equal("two", vm.Second);
    }

    [Fact]
    public void BindingGroup_CommitEdit_WhenNestedPathMemberFails_RollsBackEarlierNestedWrite()
    {
        var vm = new NestedAtomicGroupViewModel();
        vm.Node.Left = "left-old";
        vm.Node.Right = "right-old";

        var root = new Grid
        {
            BindingGroup = new BindingGroup()
        };
        var left = new TextBox();
        var right = new TextBox();
        root.AddChild(left);
        root.AddChild(right);

        BindingOperations.SetBinding(left, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = "Node.Left",
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit
        });
        BindingOperations.SetBinding(right, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = "Node.Right",
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            ValidatesOnExceptions = true
        });

        left.Text = "left-new";
        right.Text = "boom";

        var committed = root.BindingGroup!.CommitEdit();

        Assert.False(committed);
        Assert.Equal("left-old", vm.Node.Left);
        Assert.Equal("right-old", vm.Node.Right);
    }

    [Fact]
    public void PriorityBinding_ActiveChildSwitches_AndTwoWayWritesToCurrentActiveChild()
    {
        var high = new NotifyValueViewModel { Value = "high-1" };
        var low = new NotifyValueViewModel { Value = "low-1" };
        var gate = new ThrowWhenDisabledConverter { Enabled = true };
        var textBox = new TextBox();

        var priorityBinding = new PriorityBinding
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = high,
            Path = nameof(NotifyValueViewModel.Value),
            Converter = gate,
            ValidatesOnExceptions = true
        });
        priorityBinding.Bindings.Add(new Binding
        {
            Source = low,
            Path = nameof(NotifyValueViewModel.Value)
        });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, priorityBinding);
        Assert.Equal("high-1", textBox.Text);

        gate.Enabled = false;
        high.Value = "high-2";
        Assert.Equal("low-1", textBox.Text);

        textBox.Text = "write-low";
        Assert.Equal("high-2", high.Value);
        Assert.Equal("write-low", low.Value);
    }

    [Fact]
    public void PriorityBinding_UpdateSource_WhenValidatesOnExceptionsFalse_Throws()
    {
        var vm = new ThrowingSetterViewModel();
        var textBox = new TextBox();
        var priorityBinding = new PriorityBinding
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = vm,
            Path = nameof(ThrowingSetterViewModel.Value),
            ValidatesOnExceptions = false
        });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, priorityBinding);

        var ex = Assert.ThrowsAny<Exception>(() => textBox.Text = "next");
        AssertContainsInvalidOperation(ex, "setter failed");
    }

    [Fact]
    public void PriorityBinding_WithBindingGroupName_UsesNamedAncestorGroup()
    {
        var vm = new NotifyValueViewModel { Value = "seed" };
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

        var priorityBinding = new PriorityBinding
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            BindingGroupName = "Outer"
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = vm,
            Path = nameof(NotifyValueViewModel.Value)
        });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, priorityBinding);
        textBox.Text = "changed";

        _ = inner.BindingGroup!.CommitEdit();
        Assert.Equal("seed", vm.Value);

        _ = outer.BindingGroup!.CommitEdit();
        Assert.Equal("changed", vm.Value);
    }

    [Fact]
    public void Binding_UpdateSourceExceptionFilter_ReturnsValidationErrorObject_UsesIt()
    {
        var vm = new ThrowingSetterViewModel();
        var textBox = new TextBox();
        var binding = new Binding
        {
            Source = vm,
            Path = nameof(ThrowingSetterViewModel.Value),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnExceptions = true
        };
        binding.UpdateSourceExceptionFilter = (_, _) => new ValidationError(null, binding, "custom-validation-error");

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, binding);
        textBox.Text = "x";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Contains(
            Validation.GetErrors(textBox),
            error => string.Equals(error.ErrorContent?.ToString(), "custom-validation-error", StringComparison.Ordinal));
    }

    [Fact]
    public void Binding_UpdateSourceExceptionFilter_ReturnsNull_FallsBackToException()
    {
        var vm = new ThrowingSetterViewModel();
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = vm,
                Path = nameof(ThrowingSetterViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                UpdateSourceExceptionFilter = static (_, _) => null
            });

        textBox.Text = "x";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Contains(
            Validation.GetErrors(textBox),
            error => error.ErrorContent is Exception);
    }

    [Fact]
    public void Binding_UpdateSourceExceptionFilter_WhenFilterThrows_Propagates()
    {
        var vm = new ThrowingSetterViewModel();
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = vm,
                Path = nameof(ThrowingSetterViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnExceptions = true,
                UpdateSourceExceptionFilter = static (_, _) => throw new InvalidOperationException("filter-failed")
            });

        var ex = Assert.Throws<InvalidOperationException>(() => textBox.Text = "x");
        Assert.Equal("filter-failed", ex.Message);
    }

    [Fact]
    public void ClearBinding_MultiBinding_ClearsErrorsProducedByThatBinding()
    {
        var vm = new PairViewModel { Left = "L", Right = "R" };
        var textBox = new TextBox();
        var multiBinding = new MultiBinding
        {
            Converter = new WrongCountConverter(),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        multiBinding.Bindings.Add(new Binding { Source = vm, Path = nameof(PairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = vm, Path = nameof(PairViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);
        textBox.Text = "any";
        Assert.True(Validation.GetHasError(textBox));

        BindingOperations.ClearBinding(textBox, TextBox.TextProperty);

        Assert.False(Validation.GetHasError(textBox));
        Assert.Empty(Validation.GetErrors(textBox));
    }

    [Fact]
    public void ClearBinding_PriorityBinding_ClearsErrorsProducedByThatBinding()
    {
        var textBox = new TextBox();
        var priorityBinding = new PriorityBinding
        {
            FallbackValue = "fallback"
        };
        priorityBinding.Bindings.Add(new Binding
        {
            Source = new NumberHolder { Number = 42f },
            Path = nameof(NumberHolder.Number)
        });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, priorityBinding);
        Assert.True(Validation.GetHasError(textBox));

        BindingOperations.ClearBinding(textBox, TextBox.TextProperty);

        Assert.False(Validation.GetHasError(textBox));
        Assert.Empty(Validation.GetErrors(textBox));
    }

    [Fact]
    public void BindingGroupName_NamedGroup_RebindsAcrossDeepReparent()
    {
        var vm = new NotifyValueViewModel { Value = "start" };
        var hostA = new Grid
        {
            BindingGroup = new BindingGroup { Name = "SharedName" }
        };
        var hostB = new Grid
        {
            BindingGroup = new BindingGroup { Name = "SharedName" }
        };
        var deepA1 = new StackPanel();
        var deepA2 = new Grid();
        var deepB1 = new StackPanel();
        var deepB2 = new Grid();
        var textBox = new TextBox();

        hostA.AddChild(deepA1);
        deepA1.AddChild(deepA2);
        deepA2.AddChild(textBox);

        hostB.AddChild(deepB1);
        deepB1.AddChild(deepB2);

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, new Binding
        {
            Source = vm,
            Path = nameof(NotifyValueViewModel.Value),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
            BindingGroupName = "SharedName"
        });

        textBox.Text = "from-a";
        _ = hostA.BindingGroup!.CommitEdit();
        Assert.Equal("from-a", vm.Value);

        _ = deepA2.RemoveChild(textBox);
        deepB2.AddChild(textBox);
        textBox.Text = "from-b";

        _ = hostA.BindingGroup!.CommitEdit();
        Assert.Equal("from-a", vm.Value);

        _ = hostB.BindingGroup!.CommitEdit();
        Assert.Equal("from-b", vm.Value);
    }

    [Theory]
    [InlineData(BindingMode.TwoWay, UpdateSourceTrigger.PropertyChanged, true, false, true, 1)]
    [InlineData(BindingMode.TwoWay, UpdateSourceTrigger.PropertyChanged, false, true, false, 1)]
    [InlineData(BindingMode.OneWayToSource, UpdateSourceTrigger.PropertyChanged, true, false, true, 2)]
    [InlineData(BindingMode.OneWay, UpdateSourceTrigger.PropertyChanged, true, false, false, 0)]
    public void Binding_ModeTriggerValidation_PropertyChangedMatrix_BehavesAsExpected(
        BindingMode mode,
        UpdateSourceTrigger trigger,
        bool validatesOnExceptions,
        bool shouldThrow,
        bool shouldSetError,
        int expectedSetCalls)
    {
        var vm = new CountingThrowingSetterViewModel();
        var textBox = new TextBox();
        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = vm,
                Path = nameof(CountingThrowingSetterViewModel.Value),
                Mode = mode,
                UpdateSourceTrigger = trigger,
                ValidatesOnExceptions = validatesOnExceptions
            });

        if (shouldThrow)
        {
            var ex = Assert.ThrowsAny<Exception>(() => textBox.Text = "x");
            AssertContainsInvalidOperation(ex, "setter failed");
        }
        else
        {
            textBox.Text = "x";
        }

        Assert.Equal(expectedSetCalls, vm.SetCalls);
        Assert.Equal(shouldSetError, Validation.GetHasError(textBox));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Binding_ModeTriggerValidation_ExplicitMatrix_BehavesAsExpected(
        bool validatesOnExceptions,
        bool shouldThrowOnExplicitUpdate)
    {
        var vm = new CountingThrowingSetterViewModel();
        var textBox = new TextBox();
        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = vm,
                Path = nameof(CountingThrowingSetterViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
                ValidatesOnExceptions = validatesOnExceptions
            });

        textBox.Text = "x";
        Assert.Equal(0, vm.SetCalls);
        Assert.False(Validation.GetHasError(textBox));

        if (shouldThrowOnExplicitUpdate)
        {
            var ex = Assert.ThrowsAny<Exception>(() => BindingOperations.UpdateSource(textBox, TextBox.TextProperty));
            AssertContainsInvalidOperation(ex, "setter failed");
            Assert.False(Validation.GetHasError(textBox));
        }
        else
        {
            BindingOperations.UpdateSource(textBox, TextBox.TextProperty);
            Assert.True(Validation.GetHasError(textBox));
        }

        Assert.Equal(1, vm.SetCalls);
    }

    private sealed class AtomicGroupViewModel
    {
        private string _second = string.Empty;

        public string First { get; set; } = string.Empty;

        public string Second
        {
            get => _second;
            set
            {
                if (string.Equals(value, "boom", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("second setter failed");
                }

                _second = value;
            }
        }
    }

    private sealed class NestedAtomicGroupViewModel
    {
        public NestedNode Node { get; } = new();
    }

    private sealed class NestedNode
    {
        private string _right = string.Empty;

        public string Left { get; set; } = string.Empty;

        public string Right
        {
            get => _right;
            set
            {
                if (string.Equals(value, "boom", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("right setter failed");
                }

                _right = value;
            }
        }
    }

    private sealed class NotifyValueViewModel : INotifyPropertyChanged
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

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (!Enabled)
            {
                throw new InvalidOperationException("disabled");
            }

            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
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

    private sealed class PairViewModel
    {
        public string Left { get; set; } = string.Empty;

        public string Right { get; set; } = string.Empty;
    }

    private sealed class WrongCountConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return string.Join("|", values);
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, System.Globalization.CultureInfo culture)
        {
            return [value];
        }
    }

    private sealed class CountingThrowingSetterViewModel
    {
        private string _value = "seed";

        public int SetCalls { get; private set; }

        public string Value
        {
            get => _value;
            set
            {
                SetCalls++;
                throw new InvalidOperationException("setter failed");
            }
        }
    }

    private static void AssertContainsInvalidOperation(Exception ex, string message)
    {
        if (ex is InvalidOperationException invalidOperationException)
        {
            Assert.Equal(message, invalidOperationException.Message);
            return;
        }

        var inner = ex.InnerException;
        Assert.NotNull(inner);
        var innerInvalidOp = Assert.IsType<InvalidOperationException>(inner);
        Assert.Equal(message, innerInvalidOp.Message);
    }

    private sealed class NumberHolder
    {
        public float Number { get; set; }
    }
}
