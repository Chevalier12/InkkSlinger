namespace InkkSlinger.Tests;

internal static class LabelTestExtensions
{
    public static string GetContentText(this Label label)
    {
        return Label.ExtractAutomationText(label.Content);
    }
}
