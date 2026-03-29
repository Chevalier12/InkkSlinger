namespace InkkSlinger;

public sealed class InkkOopsContentTextDiagnosticsContributor : IInkkOopsDiagnosticsContributor
{
    public int Order => 20;

    public void Contribute(InkkOopsDiagnosticsContext context, UIElement element, InkkOopsElementDiagnosticsBuilder builder)
    {
        var text = element switch
        {
            Button button => Label.ExtractAutomationText(button.Content),
            Label label => Label.ExtractAutomationText(label.Content),
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.Add("text", Escape(text));
        }
    }

    private static string Escape(string text)
    {
        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
