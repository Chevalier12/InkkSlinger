using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace InkkSlinger.Tests;

public class BindingParityExpansionTests
{
    [Fact]
    public void BindingModeDefault_UsesOneWayForRegularDependencyProperty()
    {
        var viewModel = new NumberViewModel { Value = 10f };
        var border = new Border();

        BindingOperations.SetBinding(
            border,
            FrameworkElement.WidthProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(NumberViewModel.Value),
                Mode = BindingMode.Default
            });

        Assert.Equal(10f, border.Width);

        border.Width = 42f;

        Assert.Equal(10f, viewModel.Value);
    }

    [Fact]
    public void BindingModeDefault_UsesTwoWayForTextBoxTextPropertyAndLostFocusTrigger()
    {
        var viewModel = new TextViewModel { Text = "alpha" };
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(TextViewModel.Text),
                Mode = BindingMode.Default,
                UpdateSourceTrigger = UpdateSourceTrigger.Default
            });

        Assert.Equal("alpha", textBox.Text);

        textBox.Text = "beta";
        Assert.Equal("alpha", viewModel.Text);

        RaiseLostFocus(textBox);
        Assert.Equal("beta", viewModel.Text);
    }

    [Fact]
    public void OneWayToSource_PushesTargetToSourceWithoutSourceToTargetSync()
    {
        var viewModel = new NumberViewModel { Value = 1f };
        var border = new Border { Width = 21f };

        BindingOperations.SetBinding(
            border,
            FrameworkElement.WidthProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(NumberViewModel.Value),
                Mode = BindingMode.OneWayToSource,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

        Assert.Equal(21f, viewModel.Value);

        viewModel.Value = 99f;
        Assert.Equal(21f, border.Width);

        border.Width = 64f;
        Assert.Equal(64f, viewModel.Value);
    }

    [Fact]
    public void Converter_ConvertsBothDirections()
    {
        var viewModel = new TextViewModel { Text = "x" };
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(TextViewModel.Text),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new PrefixConverter("view:")
            });

        Assert.Equal("view:x", textBox.Text);

        textBox.Text = "view:y";

        Assert.Equal("y", viewModel.Text);
    }

    [Fact]
    public void ConverterException_WithValidatesOnExceptions_SetsValidationError()
    {
        var viewModel = new TextViewModel { Text = "x" };
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(TextViewModel.Text),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new ThrowingConvertBackConverter(),
                ValidatesOnExceptions = true
            });

        textBox.Text = "boom";

        Assert.True(Validation.GetHasError(textBox));
        Assert.Equal("x", viewModel.Text);
    }

    [Fact]
    public void ConverterException_WithoutValidatesOnExceptions_Throws()
    {
        var viewModel = new TextViewModel { Text = "x" };
        var textBox = new TextBox();

        BindingOperations.SetBinding(
            textBox,
            TextBox.TextProperty,
            new Binding
            {
                Source = viewModel,
                Path = nameof(TextViewModel.Text),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new ThrowingConvertBackConverter(),
                ValidatesOnExceptions = false
            });

        Assert.ThrowsAny<Exception>(() => textBox.Text = "boom");
    }

    private static void RaiseLostFocus(UIElement element)
    {
        var raiseMethod = typeof(UIElement).GetMethod("RaiseRoutedEventInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(raiseMethod);

        raiseMethod!.Invoke(
            element,
            [UIElement.LostFocusEvent, new FocusChangedRoutedEventArgs(UIElement.LostFocusEvent, element, null)]);
    }

    private sealed class NumberViewModel
    {
        public float Value { get; set; }
    }

    private sealed class TextViewModel
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class PrefixConverter : IValueConverter
    {
        private readonly string _prefix;

        public PrefixConverter(string prefix)
        {
            _prefix = prefix;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return _prefix + (value?.ToString() ?? string.Empty);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var text = value?.ToString() ?? string.Empty;
            return text.StartsWith(_prefix, StringComparison.Ordinal)
                ? text[_prefix.Length..]
                : text;
        }
    }

    private sealed class ThrowingConvertBackConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new InvalidOperationException("convert back failed");
        }
    }
}
