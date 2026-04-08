using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkkSlinger;

public sealed class Binding : BindingBase
{
    public string Path { get; set; } = string.Empty;

    public object? Source { get; set; }

    public string? ElementName { get; set; }

    public RelativeSourceMode RelativeSourceMode { get; set; } = RelativeSourceMode.None;

    public Type? RelativeSourceAncestorType { get; set; }

    public int RelativeSourceAncestorLevel { get; set; } = 1;

    public IValueConverter? Converter { get; set; }

    public object? ConverterParameter { get; set; }

    public CultureInfo? ConverterCulture { get; set; }

    public IList<ValidationRule> ValidationRules { get; } = new List<ValidationRule>();

    public bool ValidatesOnDataErrors { get; set; }

    public bool ValidatesOnNotifyDataErrors { get; set; }

    public bool ValidatesOnExceptions { get; set; }

    public string? BindingGroupName { get; set; }

    public UpdateSourceExceptionFilterCallback? UpdateSourceExceptionFilter { get; set; }
}
