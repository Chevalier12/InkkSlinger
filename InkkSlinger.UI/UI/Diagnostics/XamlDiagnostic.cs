namespace InkkSlinger;

public sealed record XamlDiagnostic(
    XamlDiagnosticCode Code,
    string Message,
    string? ElementName,
    string? PropertyName,
    int? Line,
    int? Position,
    string? Hint);
