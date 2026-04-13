using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public class DesignerXmlSyntaxHighlighterTests
{
    private const string SampleXml = """
        <UserControl xmlns="urn:inkkslinger-ui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     x:Name="Root"
                     Background="#101820">
          <Grid>
            <TextBlock Grid.Row="1"
                       Text="Hello" />
          </Grid>
        </UserControl>
        """;

    [Fact]
    public void Classify_RecognizesControlTypeNamesAndPropertyNamesOnly()
    {
        var tokens = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.Classify(SampleXml);
        var highlightedTexts = tokens.Select(token => SampleXml.Substring(token.Start, token.Length)).ToArray();

        Assert.Contains("UserControl", highlightedTexts);
        Assert.Contains("Grid", highlightedTexts);
        Assert.Contains("TextBlock", highlightedTexts);
        Assert.Contains("x:Name", highlightedTexts);
        Assert.Contains("Background", highlightedTexts);
        Assert.Contains("Grid.Row", highlightedTexts);
        Assert.Contains("Text", highlightedTexts);
        Assert.DoesNotContain("xmlns", highlightedTexts);
        Assert.DoesNotContain("urn:inkkslinger-ui", highlightedTexts);
    }

    [Fact]
    public void CreateHighlightedDocument_AssignsExpectedColorsToHighlightedRuns()
    {
        var colors = new InkkSlinger.Designer.DesignerXmlSyntaxColors(
            new Color(10, 20, 30),
            new Color(40, 50, 60),
            new Color(70, 80, 90));

        var document = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.CreateHighlightedDocument(SampleXml, colors);
        var runs = document.Blocks
            .OfType<Paragraph>()
            .SelectMany(static paragraph => paragraph.Inlines.OfType<Run>())
            .Where(static run => !string.IsNullOrEmpty(run.Text))
            .ToArray();

        Assert.Contains(runs, run => run.Text == "UserControl" && run.Foreground == colors.ControlTypeForeground);
        Assert.Contains(runs, run => run.Text == "TextBlock" && run.Foreground == colors.ControlTypeForeground);
        Assert.Contains(runs, run => run.Text == "Background" && run.Foreground == colors.PropertyForeground);
        Assert.Contains(runs, run => run.Text == "Grid.Row" && run.Foreground == colors.PropertyForeground);
        Assert.Contains(runs, run => run.Text == "Text" && run.Foreground == colors.PropertyForeground);
        Assert.Contains(runs, run => run.Text.Contains("Hello", StringComparison.Ordinal) && run.Foreground == colors.DefaultForeground);
    }
}