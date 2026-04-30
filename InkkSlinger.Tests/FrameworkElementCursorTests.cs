using Xunit;

namespace InkkSlinger.Tests;

public sealed class FrameworkElementCursorTests
{
    [Fact]
    public void Cursor_StoresAndRetrievesValue_ThroughDependencyProperty()
    {
        var textBox = new TextBox();
        textBox.Cursor = "IBeam";
        Assert.Equal("IBeam", textBox.Cursor);
        Assert.Equal("IBeam", textBox.GetValue(FrameworkElement.CursorProperty));
    }

    [Fact]
    public void Cursor_DefaultValue_IsEmptyString()
    {
        var textBox = new TextBox();
        Assert.Equal(string.Empty, textBox.Cursor);
    }

    [Fact]
    public void Cursor_CanBeAssignedAndOverwritten()
    {
        var textBox = new TextBox();
        textBox.Cursor = "Arrow";
        Assert.Equal("Arrow", textBox.Cursor);

        textBox.Cursor = "Hand";
        Assert.Equal("Hand", textBox.Cursor);

        textBox.Cursor = "IBeam";
        Assert.Equal("IBeam", textBox.Cursor);
    }
}
