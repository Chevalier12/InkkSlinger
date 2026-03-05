using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PasswordBoxDemoView : UserControl
{
    public PasswordBoxDemoView()
    {
        InitializeComponent();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font != null && this.FindName("PasswordInput") is PasswordBox passwordInput)
        {
            passwordInput.Font = font;
        }
    }
}


