using System;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public partial class DatePickerView : UserControl
{
    public DatePickerView()
    {
        InitializeComponent();
    }

    public void SetFont(SpriteFont? font)
    {
        if (font == null)
        {
            return;
        }

        ControlDemoSupport.ApplyFontRecursive(this, font);
    }
}


