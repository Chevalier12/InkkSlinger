namespace InkkSlinger;

public sealed class ValidationError
{
    public ValidationError(ValidationRule? ruleInError, object? bindingInError, object? errorContent)
    {
        RuleInError = ruleInError;
        BindingInError = bindingInError;
        ErrorContent = errorContent;
    }

    public ValidationRule? RuleInError { get; }

    public object? BindingInError { get; }

    public object? ErrorContent { get; }
}
