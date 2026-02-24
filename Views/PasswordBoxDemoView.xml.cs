using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class PasswordBoxDemoView : UserControl
{
    public PasswordBoxDemoView()
    {
        var markupPath = Path.Combine(AppContext.BaseDirectory, "Views", "PasswordBoxDemoView.xml");
        XamlLoader.LoadInto(this, markupPath, this);
    }

    public void SetFont(SpriteFont? font)
    {
        if (font != null && this.FindName("PasswordInput") is PasswordBox passwordInput)
        {
            passwordInput.Font = font;
        }
    }
}

