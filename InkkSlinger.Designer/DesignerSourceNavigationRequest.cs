namespace InkkSlinger.Designer;

public sealed class DesignerSourceNavigationRequest
{
    public DesignerSourceNavigationRequest(int lineNumber)
    {
        LineNumber = lineNumber;
    }

    public int LineNumber { get; }
}