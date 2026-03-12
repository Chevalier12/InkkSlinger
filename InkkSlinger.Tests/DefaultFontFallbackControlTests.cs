using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace InkkSlinger.Tests;

public sealed class DefaultFontFallbackControlTests
{
    [Fact]
    public void CheckBox_WithNullFont_UsesRegisteredDefaultFontForMeasure()
    {
        var defaultFont = CreatePlaceholderFont();
        UiTextRenderer.SetDefaultFont(defaultFont);
        try
        {
            var checkBox = new CheckBox
            {
                Text = "Enable feature",
                Font = null
            };

            checkBox.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            Assert.True(checkBox.DesiredSize.X > checkBox.Padding.Horizontal + 14f);
        }
        finally
        {
            UiTextRenderer.SetDefaultFont(null);
        }
    }

    [Fact]
    public void RadioButton_WithNullFont_UsesRegisteredDefaultFontForMeasure()
    {
        var defaultFont = CreatePlaceholderFont();
        UiTextRenderer.SetDefaultFont(defaultFont);
        try
        {
            var radioButton = new RadioButton
            {
                Text = "Option A",
                Font = null
            };

            radioButton.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            Assert.True(radioButton.DesiredSize.X > radioButton.Padding.Horizontal + 14f);
        }
        finally
        {
            UiTextRenderer.SetDefaultFont(null);
        }
    }

    [Fact]
    public void TabControl_WithNullFont_UsesRegisteredDefaultFontForHeaderMeasure()
    {
        var defaultFont = CreatePlaceholderFont();
        UiTextRenderer.SetDefaultFont(defaultFont);
        try
        {
            var tabControl = new TabControl
            {
                Font = null
            };
            tabControl.Items.Add("General");
            tabControl.Items.Add("Advanced");
            tabControl.SelectedIndex = 0;

            tabControl.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            Assert.True(tabControl.DesiredSize.X > 0f);
            Assert.True(tabControl.DesiredSize.Y > 0f);
        }
        finally
        {
            UiTextRenderer.SetDefaultFont(null);
        }
    }

    private static SpriteFont CreatePlaceholderFont()
    {
        return (SpriteFont)RuntimeHelpers.GetUninitializedObject(typeof(SpriteFont));
    }
}
