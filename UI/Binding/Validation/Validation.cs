using System.Collections.Generic;

namespace InkkSlinger;

public static class Validation
{
    public static readonly DependencyProperty HasErrorProperty =
        DependencyProperty.RegisterAttached(
            "HasError",
            typeof(bool),
            typeof(Validation),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty ErrorsProperty =
        DependencyProperty.RegisterAttached(
            "Errors",
            typeof(List<ValidationError>),
            typeof(Validation),
            new FrameworkPropertyMetadata(null));

    private static readonly DependencyProperty ErrorsByOwnerProperty =
        DependencyProperty.RegisterAttached(
            "ErrorsByOwner",
            typeof(Dictionary<object, List<ValidationError>>),
            typeof(Validation),
            new FrameworkPropertyMetadata(null));

    public static bool GetHasError(DependencyObject dependencyObject)
    {
        return dependencyObject.GetValue<bool>(HasErrorProperty);
    }

    public static IReadOnlyList<ValidationError> GetErrors(DependencyObject dependencyObject)
    {
        if (dependencyObject.GetValue(ErrorsProperty) is List<ValidationError> errors)
        {
            return errors.AsReadOnly();
        }

        return [];
    }

    internal static void SetErrors(DependencyObject dependencyObject, object owner, List<ValidationError> errors)
    {
        var errorsByOwner = GetOrCreateErrorsByOwner(dependencyObject);
        errorsByOwner[owner] = new List<ValidationError>(errors);
        PublishAggregateErrors(dependencyObject, errorsByOwner);
    }

    internal static void ClearErrors(DependencyObject dependencyObject, object owner)
    {
        if (dependencyObject.GetValue(ErrorsByOwnerProperty) is not Dictionary<object, List<ValidationError>> errorsByOwner)
        {
            return;
        }

        _ = errorsByOwner.Remove(owner);
        PublishAggregateErrors(dependencyObject, errorsByOwner);
    }

    private static Dictionary<object, List<ValidationError>> GetOrCreateErrorsByOwner(DependencyObject dependencyObject)
    {
        if (dependencyObject.GetValue(ErrorsByOwnerProperty) is Dictionary<object, List<ValidationError>> errorsByOwner)
        {
            return errorsByOwner;
        }

        errorsByOwner = new Dictionary<object, List<ValidationError>>();
        dependencyObject.SetValue(ErrorsByOwnerProperty, errorsByOwner);
        return errorsByOwner;
    }

    private static void PublishAggregateErrors(
        DependencyObject dependencyObject,
        Dictionary<object, List<ValidationError>> errorsByOwner)
    {
        var aggregatedErrors = new List<ValidationError>();
        foreach (var pair in errorsByOwner)
        {
            aggregatedErrors.AddRange(pair.Value);
        }

        dependencyObject.SetValue(HasErrorProperty, aggregatedErrors.Count > 0);
        dependencyObject.SetValue(ErrorsProperty, aggregatedErrors);
    }
}
