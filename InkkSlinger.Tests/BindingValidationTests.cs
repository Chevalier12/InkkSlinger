using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xunit;

namespace InkkSlinger.Tests;

public class BindingValidationTests
{
    [Fact]
    public void ValidationRule_BlocksSourceCommitAndSetsValidationState()
    {
        var viewModel = new TextValidationViewModel { Value = "ok" };
        var textBox = new TextBox();
        var binding = new Binding
        {
            Source = viewModel,
            Path = nameof(TextValidationViewModel.Value),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        binding.ValidationRules.Add(new DisallowExclamationRule());

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, binding);

        textBox.Text = "bad!";

        Assert.Equal("ok", viewModel.Value);
        Assert.True(Validation.GetHasError(textBox));
        Assert.Single(Validation.GetErrors(textBox));
    }

    [Fact]
    public void IDataErrorInfo_ValidationStateTracksErrors()
    {
        var viewModel = new DataErrorInfoViewModel();
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(DataErrorInfoViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnDataErrors = true
            });

        textBox.Text = "bad";
        Assert.True(Validation.GetHasError(textBox));

        textBox.Text = "good";
        Assert.False(Validation.GetHasError(textBox));
    }

    [Fact]
    public void INotifyDataErrorInfo_UpdatesValidationOnErrorsChanged()
    {
        var viewModel = new NotifyDataErrorViewModel();
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(NotifyDataErrorViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnNotifyDataErrors = true
            });

        viewModel.SetError(nameof(NotifyDataErrorViewModel.Value), "broken");
        Assert.True(Validation.GetHasError(textBox));

        viewModel.ClearError(nameof(NotifyDataErrorViewModel.Value));
        Assert.False(Validation.GetHasError(textBox));
    }

    [Fact]
    public void ClearBinding_ClearsValidationErrors()
    {
        var viewModel = new DataErrorInfoViewModel();
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(DataErrorInfoViewModel.Value),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ValidatesOnDataErrors = true
            });

        textBox.Text = "bad";
        Assert.True(Validation.GetHasError(textBox));

        BindingOperations.ClearBinding(textBox, TextBox.TextProperty);

        Assert.False(Validation.GetHasError(textBox));
        Assert.Empty(Validation.GetErrors(textBox));
    }

    [Fact]
    public void ClearBinding_PreservesValidationErrorsFromOtherBindings()
    {
        var viewModel = new MultiBindingValidationViewModel
        {
            Text = "ok",
            WidthValue = 10f
        };
        var textBox = new TextBox();

        var textBinding = new Binding
        {
            Source = viewModel,
            Path = nameof(MultiBindingValidationViewModel.Text),
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        textBinding.ValidationRules.Add(new DisallowExclamationRule());

        BindingOperations.SetBinding(textBox, TextBox.TextProperty, textBinding);
        BindingOperations.SetBinding(
            textBox,
            FrameworkElement.WidthProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(MultiBindingValidationViewModel.WidthValue),
                Mode = BindingMode.OneWay,
                ValidatesOnDataErrors = true
            });

        textBox.Text = "bad!";
        Assert.True(Validation.GetHasError(textBox));

        BindingOperations.ClearBinding(textBox, TextBox.TextProperty);

        Assert.True(Validation.GetHasError(textBox));
        Assert.Contains(
            Validation.GetErrors(textBox),
            error => string.Equals(error.ErrorContent?.ToString(), "Width is invalid", StringComparison.Ordinal));
    }

    private sealed class DisallowExclamationRule : ValidationRule
    {
        public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
        {
            var text = value?.ToString() ?? string.Empty;
            return text.Contains('!')
                ? new ValidationResult(false, "Exclamation marks are not allowed")
                : ValidationResult.ValidResult;
        }
    }

    private sealed class TextValidationViewModel
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class DataErrorInfoViewModel : IDataErrorInfo
    {
        private string _value = string.Empty;

        public string this[string columnName]
        {
            get
            {
                if (!string.Equals(columnName, nameof(Value), StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                return string.Equals(Value, "bad", StringComparison.Ordinal) ? "Value is invalid" : string.Empty;
            }
        }

        public string Error => string.Empty;

        public string Value
        {
            get => _value;
            set => _value = value;
        }
    }

    private sealed class NotifyDataErrorViewModel : INotifyDataErrorInfo
    {
        private readonly Dictionary<string, List<string>> _errorsByProperty = new(StringComparer.Ordinal);

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errorsByProperty.Count > 0;

        public string Value { get; set; } = string.Empty;

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

        public void ClearError(string propertyName)
        {
            _errorsByProperty.Remove(propertyName);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private sealed class MultiBindingValidationViewModel : IDataErrorInfo
    {
        public string Text { get; set; } = string.Empty;

        public float WidthValue { get; set; }

        public string this[string columnName]
        {
            get
            {
                return string.Equals(columnName, nameof(WidthValue), StringComparison.Ordinal)
                    ? "Width is invalid"
                    : string.Empty;
            }
        }

        public string Error => string.Empty;
    }
}
