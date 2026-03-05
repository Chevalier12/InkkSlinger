using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class MainMenuView : UserControl
{
    public MainMenuView()
    {
        InitializeComponent();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font != null && this.FindName("DemoTextBox") is TextBox demoTextBox)
        {
            demoTextBox.Font = font;
        }
    }
}


