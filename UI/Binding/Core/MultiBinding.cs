using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkkSlinger;

public sealed class MultiBinding : BindingBase
{
    public IList<Binding> Bindings { get; } = new List<Binding>();

    public IMultiValueConverter? Converter { get; set; }

    public object? ConverterParameter { get; set; }

    public CultureInfo? ConverterCulture { get; set; }

    public IList<ValidationRule> ValidationRules { get; } = new List<ValidationRule>();

    public bool ValidatesOnDataErrors { get; set; }

    public bool ValidatesOnNotifyDataErrors { get; set; }

    public bool ValidatesOnExceptions { get; set; }
}
