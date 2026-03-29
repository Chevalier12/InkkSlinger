using Xunit;

namespace InkkSlinger.Tests;

public sealed class FontDependencyPropertyIdentityTests
{
    [Fact]
    public void ControlDerivedTypes_InheritFrameworkTypographyProperties()
    {
        var expectedFamily = new FontFamily("Segoe UI");
        var host = new StackPanel
        {
            FontFamily = expectedFamily,
            FontSize = 18f,
            FontWeight = "SemiBold",
            FontStyle = "Italic"
        };

        var descendants = new FrameworkElement[]
        {
            new Button(),
            new TextBox(),
            new PasswordBox(),
            new RichTextBox(),
            new ComboBox(),
            new MenuItem(),
            new TabControl(),
            new Popup(),
            new GroupBox(),
            new Expander(),
            new StatusBarItem(),
            new DocumentViewer(),
            new DataGrid(),
            new DataGridCell(),
            new DataGridRowHeader(),
            new TreeView(),
            new TreeViewItem()
        };

        foreach (var descendant in descendants)
        {
            host.AddChild(descendant);

            Assert.Equal(expectedFamily, descendant.FontFamily);
            Assert.Equal(18f, descendant.FontSize);
            Assert.Equal("SemiBold", descendant.FontWeight);
            Assert.Equal("Italic", descendant.FontStyle);
            Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontFamilyProperty));
            Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontSizeProperty));
            Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontWeightProperty));
            Assert.Equal(DependencyPropertyValueSource.Inherited, descendant.GetValueSource(FrameworkElement.FontStyleProperty));
        }
    }

    [Fact]
    public void Window_KeepsDistinctTypographyDependencyProperties()
    {
        Assert.NotSame(FrameworkElement.FontFamilyProperty, Window.FontFamilyProperty);
        Assert.NotSame(FrameworkElement.FontSizeProperty, Window.FontSizeProperty);
        Assert.NotSame(FrameworkElement.FontWeightProperty, Window.FontWeightProperty);
        Assert.NotSame(FrameworkElement.FontStyleProperty, Window.FontStyleProperty);
    }
}
