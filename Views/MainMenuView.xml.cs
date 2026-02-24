using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "MainMenuView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
    }

    public void SetFont(SpriteFont? font)
    {
        if (font != null && this.FindName("DemoTextBox") is TextBox demoTextBox)
        {
            demoTextBox.Font = font;
        }
    }
}

