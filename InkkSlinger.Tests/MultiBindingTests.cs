using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xunit;

namespace InkkSlinger.Tests;

public class MultiBindingTests
{
    [Fact]
    public void MultiBinding_UpdatesTargetFromTwoSources()
    {
        var viewModel = new PairViewModel { Left = "L", Right = "R" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new PairConverter(),
            Mode = BindingMode.OneWay
        };
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        Assert.Equal("L|R", textBox.Text);

        viewModel.Left = "X";

        Assert.Equal("X|R", textBox.Text);
    }

    [Fact]
    public void MultiBinding_ConvertBackUpdatesChildSources()
    {
        var viewModel = new PairViewModel { Left = "A", Right = "B" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new PairConverter(),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        textBox.Text = "Left|Right";

        Assert.Equal("Left", viewModel.Left);
        Assert.Equal("Right", viewModel.Right);
    }

    [Fact]
    public void MultiBinding_UsesChildFallbackWhenPathUnavailable()
    {
        var viewModel = new PairViewModel { Left = "A", Right = "B" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new PairConverter(),
            Mode = BindingMode.OneWay
        };

        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = new object(), Path = "Missing", FallbackValue = "Fallback" });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        Assert.Equal("A|Fallback", textBox.Text);
    }

    [Fact]
    public void MultiBinding_ConvertBackMismatchSetsValidationError()
    {
        var viewModel = new PairViewModel { Left = "L", Right = "R" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new MismatchConverter(),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };

        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(PairViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        textBox.Text = "x|y";

        Assert.True(Validation.GetHasError(textBox));
    }

    [Fact]
    public void MultiBinding_NotifyDataErrors_TracksAllChildPropertiesOnSameSource()
    {
        var viewModel = new NotifyPairErrorViewModel { Left = "L", Right = "R" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new PairConverter(),
            Mode = BindingMode.OneWay,
            ValidatesOnNotifyDataErrors = true
        };
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(NotifyPairErrorViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(NotifyPairErrorViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        viewModel.SetError(nameof(NotifyPairErrorViewModel.Right), "right invalid");

        Assert.True(Validation.GetHasError(textBox));
    }

    [Fact]
    public void MultiBinding_ConvertBackWriteFailureSetsValidationError()
    {
        var viewModel = new NumericPairViewModel { Left = 1, Right = "ok" };
        var textBox = new TextBox();

        var multiBinding = new MultiBinding
        {
            Converter = new InvalidLeftConvertBackConverter(),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(NumericPairViewModel.Left) });
        multiBinding.Bindings.Add(new Binding { Source = viewModel, Path = nameof(NumericPairViewModel.Right) });

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, multiBinding);

        textBox.Text = "2|changed";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Equal(1, viewModel.Left);
    }

    private sealed class PairConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            return $"{values[0]}|{values[1]}";
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            var text = value?.ToString() ?? string.Empty;
            var parts = text.Split('|');
            return
            [
                parts.Length > 0 ? parts[0] : string.Empty,
                parts.Length > 1 ? parts[1] : string.Empty
            ];
        }
    }

    private sealed class MismatchConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            return string.Join("|", values);
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            return [value];
        }
    }

    private sealed class InvalidLeftConvertBackConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            return $"{values[0]}|{values[1]}";
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            return ["not-an-int", "changed"];
        }
    }

    private sealed class PairViewModel : INotifyPropertyChanged
    {
        private string _left = string.Empty;
        private string _right = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Left
        {
            get => _left;
            set
            {
                if (string.Equals(_left, value, StringComparison.Ordinal))
                {
                    return;
                }

                _left = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Left)));
            }
        }

        public string Right
        {
            get => _right;
            set
            {
                if (string.Equals(_right, value, StringComparison.Ordinal))
                {
                    return;
                }

                _right = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Right)));
            }
        }
    }

    private sealed class NumericPairViewModel
    {
        public int Left { get; set; }

        public string Right { get; set; } = string.Empty;
    }

    private sealed class NotifyPairErrorViewModel : INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> _errorsByProperty = new(StringComparer.Ordinal);

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errorsByProperty.Count > 0;

        public string Left { get; set; } = string.Empty;

        public string Right { get; set; } = string.Empty;

        public IEnumerable GetErrors(string? propertyName)
        {
            var key = propertyName ?? string.Empty;
            return _errorsByProperty.TryGetValue(key, out var errors)
                ? errors
                : Array.Empty<string>();
        }

        public void SetError(string propertyName, string error)
        {
            _errorsByProperty[propertyName] = [error];
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }
}
