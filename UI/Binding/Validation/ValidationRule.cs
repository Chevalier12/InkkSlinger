using System;
using System.Globalization;

namespace InkkSlinger;

public abstract class ValidationRule
{
    public abstract ValidationResult Validate(object? value, CultureInfo cultureInfo);
}
