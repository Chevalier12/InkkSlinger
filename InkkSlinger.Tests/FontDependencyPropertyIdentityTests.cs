using Xunit;

namespace InkkSlinger.Tests;

public sealed class FontDependencyPropertyIdentityTests
{
    [Fact]
    public void ControlDerivedTypes_ReuseBaseFontDependencyPropertyIdentity()
    {
        Assert.Same(Control.FontProperty, Button.FontProperty);
        Assert.Same(Control.FontProperty, TextBox.FontProperty);
        Assert.Same(Control.FontProperty, PasswordBox.FontProperty);
        Assert.Same(Control.FontProperty, RichTextBox.FontProperty);
        Assert.Same(Control.FontProperty, ComboBox.FontProperty);
        Assert.Same(Control.FontProperty, MenuItem.FontProperty);
        Assert.Same(Control.FontProperty, TabControl.FontProperty);
        Assert.Same(Control.FontProperty, Popup.FontProperty);
        Assert.Same(Control.FontProperty, GroupBox.FontProperty);
        Assert.Same(Control.FontProperty, Expander.FontProperty);
        Assert.Same(Control.FontProperty, StatusBarItem.FontProperty);
        Assert.Same(Control.FontProperty, DocumentViewer.FontProperty);
        Assert.Same(Control.FontProperty, DataGrid.FontProperty);
        Assert.Same(Control.FontProperty, DataGridCell.FontProperty);
        Assert.Same(Control.FontProperty, DataGridRowHeader.FontProperty);
        Assert.Same(Control.FontProperty, TreeView.FontProperty);
        Assert.Same(Control.FontProperty, TreeViewItem.FontProperty);
    }

    [Fact]
    public void Window_KeepsDistinctTypographyDependencyProperties()
    {
        Assert.NotSame(FrameworkElement.FontFamilyProperty, Window.FontFamilyProperty);
        Assert.NotSame(FrameworkElement.FontSizeProperty, Window.FontSizeProperty);
        Assert.NotSame(FrameworkElement.FontWeightProperty, Window.FontWeightProperty);
    }
}
