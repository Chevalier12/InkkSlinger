using Microsoft.Xna.Framework;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class TextBoxFontFallbackTests
{
    [Fact]
    public void Measure_WhenFontStashEnabled_AndFontNull_ExpandsWidthForText()
    {
        if (!FontStashTextRenderer.IsEnabled)
        {
            return;
        }

        var textBox = new TextBox
        {
            Text = "TextBox fallback text",
            Font = null,
            TextWrapping = TextWrapping.NoWrap
        };

        textBox.Measure(new Vector2(500f, 120f));

        var chromeWidth = textBox.Padding.Horizontal + (textBox.BorderThickness * 2f);
        Assert.True(textBox.DesiredSize.X > chromeWidth);
    }

    [Fact]
    public void ClickingNearTextEnd_WithNullFont_ShouldInsertAtEnd()
    {
        if (!FontStashTextRenderer.IsEnabled)
        {
            return;
        }

        var textBox = new TextBox
        {
            Text = "hello",
            Font = null,
            TextWrapping = TextWrapping.NoWrap,
            Width = 260f,
            Height = 48f
        };

        textBox.Measure(new Vector2(260f, 48f));
        textBox.Arrange(new LayoutRect(0f, 0f, 260f, 48f));
        textBox.SetFocusedFromInput(true);

        var textWidth = FontStashTextRenderer.MeasureWidth(null, textBox.Text);
        var clickPoint = new Vector2(
            textBox.LayoutSlot.X + textBox.Padding.Left + textBox.BorderThickness + textWidth + 1f,
            textBox.LayoutSlot.Y + textBox.Padding.Top + 2f);

        Assert.True(textBox.HandlePointerDownFromInput(clickPoint, extendSelection: false));
        Assert.True(textBox.HandleTextInputFromInput('X'));
        Assert.Equal("helloX", textBox.Text);
    }
}
