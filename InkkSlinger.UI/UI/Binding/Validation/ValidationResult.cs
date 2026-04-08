namespace InkkSlinger;

public sealed class ValidationResult
{
    public static readonly ValidationResult ValidResult = new(isValid: true, errorContent: null);

    public ValidationResult(bool isValid, object? errorContent)
    {
        IsValid = isValid;
        ErrorContent = errorContent;
    }

    public bool IsValid { get; }

    public object? ErrorContent { get; }
}
