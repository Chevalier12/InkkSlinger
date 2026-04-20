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

        private const string UnknownTagXml = """
                <UserControl xmlns="urn:inkkslinger-ui"
                                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <hahahaha />
                </UserControl>
                """;

        private const string PropertyElementXml = """
                <Grid xmlns="urn:inkkslinger-ui">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                    </Grid.RowDefinitions>
                </Grid>
                """;

        private const string UnknownPropertyElementXml = """
                <Grid xmlns="urn:inkkslinger-ui">
                    <Grid.Hahahaha />
                </Grid>
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
    public void Classify_DoesNotHighlightUnknownTagNames()
    {
        var tokens = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.Classify(UnknownTagXml);
        var highlightedTexts = tokens.Select(token => UnknownTagXml.Substring(token.Start, token.Length)).ToArray();

        Assert.Contains("UserControl", highlightedTexts);
        Assert.DoesNotContain("hahahaha", highlightedTexts);
    }

    [Fact]
    public void Classify_RecognizesPropertyElementTagNames()
    {
        var tokens = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.Classify(PropertyElementXml);
        var highlightedTexts = tokens.Select(token => PropertyElementXml.Substring(token.Start, token.Length)).ToArray();

        Assert.Contains("Grid", highlightedTexts);
        Assert.Contains("Grid.RowDefinitions", highlightedTexts);
        Assert.Contains("RowDefinition", highlightedTexts);
        Assert.Contains("Height", highlightedTexts);
    }

    [Fact]
    public void Classify_DoesNotHighlightUnknownPropertyElementTagNames()
    {
        var tokens = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.Classify(UnknownPropertyElementXml);
        var highlightedTexts = tokens.Select(token => UnknownPropertyElementXml.Substring(token.Start, token.Length)).ToArray();

        Assert.Contains("Grid", highlightedTexts);
        Assert.DoesNotContain("Grid.Hahahaha", highlightedTexts);
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

    [Fact]
    public void CreateHighlightedDocument_LeavesUnknownTagNamesInDefaultColor()
    {
        var colors = new InkkSlinger.Designer.DesignerXmlSyntaxColors(
            new Color(10, 20, 30),
            new Color(40, 50, 60),
            new Color(70, 80, 90));

        var document = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.CreateHighlightedDocument(UnknownTagXml, colors);
        var runs = document.Blocks
            .OfType<Paragraph>()
            .SelectMany(static paragraph => paragraph.Inlines.OfType<Run>())
            .Where(static run => !string.IsNullOrEmpty(run.Text))
            .ToArray();

        Assert.Contains(runs, run => run.Text.Contains("hahahaha", StringComparison.Ordinal) && run.Foreground == colors.DefaultForeground);
        Assert.DoesNotContain(runs, run => run.Text.Contains("hahahaha", StringComparison.Ordinal) && run.Foreground == colors.ControlTypeForeground);
    }

    [Fact]
    public void CreateHighlightedDocument_AssignsPropertyColorToPropertyElementTagNames()
    {
        var colors = new InkkSlinger.Designer.DesignerXmlSyntaxColors(
            new Color(10, 20, 30),
            new Color(40, 50, 60),
            new Color(70, 80, 90));

        var document = InkkSlinger.Designer.DesignerXmlSyntaxHighlighter.CreateHighlightedDocument(PropertyElementXml, colors);
        var runs = document.Blocks
            .OfType<Paragraph>()
            .SelectMany(static paragraph => paragraph.Inlines.OfType<Run>())
            .Where(static run => !string.IsNullOrEmpty(run.Text))
            .ToArray();

        Assert.Contains(runs, run => run.Text == "Grid.RowDefinitions" && run.Foreground == colors.PropertyForeground);
        Assert.Contains(runs, run => run.Text == "RowDefinition" && run.Foreground == colors.ControlTypeForeground);
        Assert.DoesNotContain(runs, run => run.Text.Contains("Grid.RowDefinitions", StringComparison.Ordinal) && run.Foreground == colors.DefaultForeground);
    }
}